using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Amqp;
using Amqp.Framing;
using Amqp.Types;
using Krosoft.Amqp.CLI.Helpers;
using Krosoft.Amqp.CLI.Interfaces;
using Krosoft.Amqp.CLI.Models;

namespace Krosoft.Amqp.CLI.Managers;

internal class AmqpManager : IAmqpManager
{
    public async Task<int> Info(string profilePath)
    {
        var (profile, error) = await ProfileLoader.LoadAsync(profilePath);
        if (profile is null)
            return HandleError(error!);

        DisplayHeader($"INFORMATIONS DU BROKER — {profile.Name}");

        try
        {
            using var client = new HttpClient();
            var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{profile.Amqp.Username}:{profile.Amqp.Password}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);

            var brokerUrl = $"{profile.Amqp.Url}/console/jolokia/read/org.apache.activemq.artemis:broker=\"{profile.Amqp.BrokerName}\"";
            var response = await client.GetAsync(brokerUrl);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(content);

                if (doc.RootElement.TryGetProperty("value", out var value))
                {
                    Console.WriteLine($"Version         : {GetJsonProperty(value, "Version")}");
                    Console.WriteLine($"Uptime          : {GetJsonProperty(value, "Uptime")}");
                    Console.WriteLine($"Connexions      : {GetJsonLong(value, "ConnectionCount")}");
                    Console.WriteLine($"Addresses       : {GetJsonLong(value, "AddressCount")}");
                    Console.WriteLine($"Queues          : {GetJsonLong(value, "QueueCount")}");
                    Console.WriteLine($"Mémoire totale  : {GetJsonLong(value, "GlobalMaxSize"):N0} bytes");
                }
            }
            else
            {
                return HandleError($"Erreur HTTP {(int)response.StatusCode} — {await response.Content.ReadAsStringAsync()}");
            }

            return 1;
        }
        catch (Exception ex)
        {
            return HandleError($"Impossible de récupérer les informations du broker : {ex.Message}");
        }
    }

    public async Task<int> DownloadMessage(string profilePath, string queue, string messageId, string? outPath)
    {
        var (profile, error) = await ProfileLoader.LoadAsync(profilePath);
        if (profile is null)
            return HandleError(error!);

        DisplayHeader($"TÉLÉCHARGEMENT MESSAGE — {queue}");

        try
        {
            // Étape 1 — Jolokia : retrouver le message par son ID console et lire son correlation_id
            // (le BodyPreview est tronqué à 256 octets côté broker, mais le correlation_id est en tête du JSON).
            var correlationId = await ResolveCorrelationIdAsync(profile.Amqp, queue, messageId);
            if (correlationId is null)
                return HandleError($"Message {messageId} introuvable dans la file '{queue}' (ou correlation_id absent du début du body).");

            Console.WriteLine($"correlation_id résolu : {correlationId}");

            // Étape 2 — AMQP browse (non destructif) : récupérer le body complet du message ciblé.
            var body = await FetchFullBodyAsync(profile.Amqp, queue, correlationId);
            if (body is null)
                return HandleError($"Message au correlation_id {correlationId} introuvable via AMQP sur la file '{queue}'.");

            var path = string.IsNullOrWhiteSpace(outPath) ? $"message_{messageId}.json" : outPath;
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            await File.WriteAllTextAsync(path, body);

            WriteColoredLine(ConsoleColor.Green, $"Message {messageId} écrit dans : {Path.GetFullPath(path)}");
            Console.WriteLine($"Taille : {body.Length:N0} caractères");

            return 0;
        }
        catch (Exception ex)
        {
            return HandleError($"Impossible de télécharger le message : {ex.Message}");
        }
    }

    public async Task<int> ListMessages(string profilePath, string queue)
    {
        var (profile, error) = await ProfileLoader.LoadAsync(profilePath);
        if (profile is null)
            return HandleError(error!);

        DisplayHeader($"MESSAGES — {queue}");

        try
        {
            var messages = await BrowseQueueAsync(profile.Amqp, queue);
            if (messages.Count == 0)
            {
                Console.WriteLine("Aucun message dans cette file (ou file introuvable).");
                return 0;
            }

            Console.WriteLine($"{"#",-4} {"messageID",-16} {"Date",-20} {"Taille",10}  correlation_id");
            Console.WriteLine(new string('─', 100));

            var index = 1;
            foreach (var msg in messages)
            {
                var id = GetJsonProperty(msg, "messageID");
                var timestamp = FormatTimestamp(GetJsonLong(msg, "timestamp"));
                var size = GetJsonLong(msg, "persistentSize");
                var correlationId = ExtractCorrelationId(DecodeBodyPreview(msg)) ?? "—";

                Console.WriteLine($"{index,-4} {id,-16} {timestamp,-20} {size,10:N0}  {correlationId}");
                index++;
            }

            Console.WriteLine(new string('─', 100));
            Console.WriteLine($"Total : {messages.Count} message(s)");

            return 0;
        }
        catch (Exception ex)
        {
            return HandleError($"Impossible de lister les messages : {ex.Message}");
        }
    }

    private static async Task<string?> ResolveCorrelationIdAsync(AmqpProfile amqp, string queue, string messageId)
    {
        var messages = await BrowseQueueAsync(amqp, queue);
        foreach (var msg in messages)
        {
            if (GetJsonProperty(msg, "messageID") != messageId)
                continue;

            return ExtractCorrelationId(DecodeBodyPreview(msg));
        }

        return null;
    }

    // Browse Jolokia (non destructif) ; renvoie les messages clonés. Essaie anycast puis multicast.
    private static async Task<List<JsonElement>> BrowseQueueAsync(AmqpProfile amqp, string queue)
    {
        using var client = new HttpClient();
        var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{amqp.Username}:{amqp.Password}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);

        foreach (var routingType in new[] { "anycast", "multicast" })
        {
            var mbean = $"org.apache.activemq.artemis:broker=\"{amqp.BrokerName}\"," +
                        $"component=addresses,address=\"{queue}\"," +
                        $"subcomponent=queues,routing-type=\"{routingType}\",queue=\"{queue}\"";

            var request = new
            {
                type = "exec",
                mbean,
                operation = "browse()",
                arguments = Array.Empty<object>()
            };

            var response = await client.PostAsync(
                $"{amqp.Url}/console/jolokia/",
                new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json"));

            if (!response.IsSuccessStatusCode)
                continue;

            var content = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);

            if (doc.RootElement.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.Array)
                return value.EnumerateArray().Select(e => e.Clone()).ToList();
        }

        return [];
    }

    private static string FormatTimestamp(long unixMs) =>
        unixMs > 0
            ? DateTimeOffset.FromUnixTimeMilliseconds(unixMs).LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss")
            : "—";

    private static async Task<string?> FetchFullBodyAsync(AmqpProfile amqp, string queue, string correlationId)
    {
        var address = BuildAmqpAddress(amqp);
        var connection = await Connection.Factory.CreateAsync(address);
        try
        {
            var session = new Session(connection);
            // distribution-mode "copy" => browse (peek) : les messages restent dans la file.
            var source = new Source
            {
                Address = queue,
                DistributionMode = new Symbol("copy")
            };
            var receiver = new ReceiverLink(session, $"krosoft-browser-{Guid.NewGuid():N}", source, null);
            receiver.SetCredit(200);

            try
            {
                while (true)
                {
                    var message = await receiver.ReceiveAsync(TimeSpan.FromSeconds(5));
                    if (message is null)
                        break;

                    var body = MessageBodyToString(message);
                    receiver.Accept(message);

                    if (body.Contains($"\"correlation_id\":\"{correlationId}\"", StringComparison.Ordinal)
                        || ExtractCorrelationId(body) == correlationId)
                        return body;
                }
            }
            finally
            {
                await receiver.CloseAsync();
                await session.CloseAsync();
            }
        }
        finally
        {
            await connection.CloseAsync();
        }

        return null;
    }

    // amqp://user:pass@host:5672 — dérivé du champ amqpUrl du profil, complété par les identifiants AMQP.
    private static Address BuildAmqpAddress(AmqpProfile amqp)
    {
        if (string.IsNullOrWhiteSpace(amqp.AmqpUrl))
            throw new InvalidOperationException("Le champ 'amqpUrl' (ex: amqp://localhost:5672) est requis dans le profil pour la récupération AMQP.");

        var uri = new Uri(amqp.AmqpUrl);
        return new Address(uri.Host, uri.Port, amqp.Username, amqp.Password, "/", uri.Scheme);
    }

    private static string MessageBodyToString(global::Amqp.Message message) =>
        message.Body switch
        {
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            string s => s,
            null => string.Empty,
            var other => other.ToString() ?? string.Empty
        };

    private static string DecodeBodyPreview(JsonElement message)
    {
        if (message.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
            return text.GetString() ?? string.Empty;

        foreach (var key in new[] { "BodyPreview", "body" })
        {
            if (!message.TryGetProperty(key, out var raw))
                continue;

            if (raw.ValueKind == JsonValueKind.String)
            {
                var s = raw.GetString() ?? string.Empty;
                try { return Encoding.UTF8.GetString(Convert.FromBase64String(s)); }
                catch (FormatException) { return s; }
            }

            if (raw.ValueKind == JsonValueKind.Array)
            {
                var bytes = raw.EnumerateArray().Select(e => (byte)e.GetInt32()).ToArray();
                return Encoding.UTF8.GetString(bytes);
            }
        }

        return string.Empty;
    }

    private static string? ExtractCorrelationId(string body)
    {
        var match = System.Text.RegularExpressions.Regex.Match(body, "\"correlation_id\"\\s*:\\s*\"([^\"]+)\"");
        return match.Success ? match.Groups[1].Value : null;
    }

    public async Task<int> Queues2()
    {
        DisplayHeader("Queues2");

        var artemisUrl = "http://localhost:8161";
        var username = "admin";
        var password = "admin";
        var brokerName = "0.0.0.0"; // Nom du broker, souvent "0.0.0.0" ou "localhost"

        try
        {
            using var client = new HttpClient();

            var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);

            Console.WriteLine("Récupération de la liste des queues...\n");
            var queueNamesUrl = $"{artemisUrl}/console/jolokia/read/org.apache.activemq.artemis:broker=\"{brokerName}\"/QueueNames";

            var response = await client.GetAsync(queueNamesUrl);

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Erreur HTTP: {response.StatusCode}");
                Console.WriteLine(await response.Content.ReadAsStringAsync());
                return -1;
            }

            var content = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);

            var queues = new List<QueueInfo>();

            if (doc.RootElement.TryGetProperty("value", out var queueNames))
            {
                // 2. Pour chaque queue, récupérer les détails
                foreach (var queueName in queueNames.EnumerateArray())
                {
                    var queue = queueName.GetString();
                    var queueInfo = await GetQueueDetails(client, artemisUrl, brokerName, queue!);
                    if (queueInfo != null)
                    {
                        queues.Add(queueInfo);
                    }
                }
            }

            // 3. Afficher les résultats
            DisplayQueueStatistics(queues);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }

        return await ExecuteGitCommand("pull");
    }

    public Task<int> Queues3() => throw new NotImplementedException();

    public async Task<int> Queues()
    {
        DisplayHeader("Queues AMQP");

        var artemisUrl = "http://localhost:8161";
        var username = "admin";
        var password = "admin";

        try
        {
            using var client = new HttpClient();

            var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);

            var jolokiaUrl = $"{artemisUrl}/console/jolokia/read/org.apache.activemq.artemis:broker=*";

        

            var response = await client.GetAsync(jolokiaUrl);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                if (root.TryGetProperty("value", out var value))
                {
                    foreach (var broker in value.EnumerateObject())
                    {
                        WriteColoredLine(ConsoleColor.Blue, $"\nBroker: {broker.Name}");
                        Console.WriteLine();

                        if (broker.Value.TryGetProperty("QueueNames", out var queues))
                        {
                            foreach (var queue in queues.EnumerateArray())
                            {
                                Console.WriteLine($"  - {queue.GetString()}");
                            }
                        }
                    }
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();

                return HandleError($"Erreur HTTP ({response.StatusCode}) : {errorContent}");
            }

            return 1;
        }
        catch (Exception ex)
        {
            return HandleError($"Impossible de récupérer les informations du broker : {ex.Message}");
        }
    }

//////    class QueueInfo
//////    {
//////        public string? Name { get; set; }
//////        public string? Address { get; set; }
//////        public long MessageCount { get; set; }
//////        public long MessagesAdded { get; set; }
//////        public long MessagesAcknowledged { get; set; }
//////        public int ConsumerCount { get; set; }
//////        public long DeliveringCount { get; set; }
//////        public long ScheduledCount { get; set; }
//////        public bool Durable { get; set; }
//////        public bool Temporary { get; set; }
//////        public string? RoutingType { get; set; }
//////        public long MessagesExpired { get; set; }
//////        public long MessagesKilled { get; set; }
//////    }

//////    class Program
//////    {
//////        static async Task Main(string[] args)
//////        {
//////            string artemisUrl = "http://localhost:8161";
//////            string username = "admin";
//////            string password = "admin";
//////            string brokerName = "0.0.0.0"; // Nom du broker, souvent "0.0.0.0" ou "localhost"

//////            try
//////            {
//////                using var client = new HttpClient();

//////                // Configuration de l'authentification Basic
//////                var authValue = Convert.ToBase64String(
//////                    Encoding.UTF8.GetBytes($"{username}:{password}"));
//////                client.DefaultRequestHeaders.Authorization = 
//////                    new AuthenticationHeaderValue("Basic", authValue);

//////                // 1. Récupérer la liste des queues
//////                Console.WriteLine("Récupération de la liste des queues...\n");
//////                string queueNamesUrl = $"{artemisUrl}/console/jolokia/read/" +
//////                    $"org.apache.activemq.artemis:broker=\"{brokerName}\"/QueueNames";

//////                var response = await client.GetAsync(queueNamesUrl);

//////                if (!response.IsSuccessStatusCode)
//////                {
//////                    Console.WriteLine($"Erreur HTTP: {response.StatusCode}");
//////                    Console.WriteLine(await response.Content.ReadAsStringAsync());
//////                    return;
//////                }

//////                var content = await response.Content.ReadAsStringAsync();
//////                using var doc = JsonDocument.Parse(content);

//////                var queues = new List<QueueInfo>();

//////                if (doc.RootElement.TryGetProperty("value", out var queueNames))
//////                {
//////                    // 2. Pour chaque queue, récupérer les détails
//////                    foreach (var queueName in queueNames.EnumerateArray())
//////                    {
//////                        string? queue = queueName.GetString();
//////                        var queueInfo = await GetQueueDetails(client, artemisUrl, brokerName, queue!);
//////                        if (queueInfo != null)
//////                        {
//////                            queues.Add(queueInfo);
//////                        }
//////                    }
//////                }

//////                // 3. Afficher les résultats
//////                DisplayQueueStatistics(queues);

//////                // 4. Obtenir les statistiques globales du broker
//////                await DisplayBrokerStatistics(client, artemisUrl, brokerName);

//////            }
//////            catch (Exception ex)
//////            {
//////                Console.WriteLine($"Erreur: {ex.Message}");
//////                Console.WriteLine(ex.StackTrace);
//////            }
//////        }

//////        static async Task<QueueInfo?> GetQueueDetails(HttpClient client, string artemisUrl, 
//////                                                      string brokerName, string queueName)
//////        {
//////            try
//////            {
//////                // Encoder le nom de la queue pour l'URL
//////                string encodedQueueName = Uri.EscapeDataString(queueName);

//////                // Récupérer tous les attributs de la queue
//////                string queueUrl = $"{artemisUrl}/console/jolokia/read/" +
//////                    $"org.apache.activemq.artemis:broker=\"{brokerName}\"," +
//////                    $"component=addresses,address=\"{encodedQueueName}\"," +
//////                    $"subcomponent=queues,routing-type=\"anycast\",queue=\"{encodedQueueName}\"";

//////                var response = await client.GetAsync(queueUrl);

//////                if (!response.IsSuccessStatusCode)
//////                {
//////                    // Essayer avec routing-type multicast si anycast échoue
//////                    queueUrl = $"{artemisUrl}/console/jolokia/read/" +
//////                        $"org.apache.activemq.artemis:broker=\"{brokerName}\"," +
//////                        $"component=addresses,address=\"{encodedQueueName}\"," +
//////                        $"subcomponent=queues,routing-type=\"multicast\",queue=\"{encodedQueueName}\"";

//////                    response = await client.GetAsync(queueUrl);

//////                    if (!response.IsSuccessStatusCode)
//////                    {
//////                        return null;
//////                    }
//////                }

//////                var content = await response.Content.ReadAsStringAsync();
//////                using var doc = JsonDocument.Parse(content);

//////                if (doc.RootElement.TryGetProperty("value", out var value))
//////                {
//////                    return new QueueInfo
//////                    {
//////                        Name = queueName,
//////                        Address = GetJsonProperty(value, "Address"),
//////                        MessageCount = GetJsonLong(value, "MessageCount"),
//////                        MessagesAdded = GetJsonLong(value, "MessagesAdded"),
//////                        MessagesAcknowledged = GetJsonLong(value, "MessagesAcknowledged"),
//////                        ConsumerCount = GetJsonInt(value, "ConsumerCount"),
//////                        DeliveringCount = GetJsonLong(value, "DeliveringCount"),
//////                        ScheduledCount = GetJsonLong(value, "ScheduledCount"),
//////                        Durable = GetJsonBool(value, "Durable"),
//////                        Temporary = GetJsonBool(value, "Temporary"),
//////                        RoutingType = GetJsonProperty(value, "RoutingType"),
//////                        MessagesExpired = GetJsonLong(value, "MessagesExpired"),
//////                        MessagesKilled = GetJsonLong(value, "MessagesKilled")
//////                    };
//////                }
//////            }
//////            catch (Exception ex)
//////            {
//////                Console.WriteLine($"Erreur pour la queue {queueName}: {ex.Message}");
//////            }

//////            return null;
//////        }

//////        static void DisplayQueueStatistics(List<QueueInfo> queues)
//////        {
//////            Console.WriteLine("╔════════════════════════════════════════════════════════════════════════════════╗");
//////            Console.WriteLine("║                          STATISTIQUES DES QUEUES                               ║");
//////            Console.WriteLine("╚════════════════════════════════════════════════════════════════════════════════╝\n");

//////            if (queues.Count == 0)
//////            {
//////                Console.WriteLine("Aucune queue trouvée.\n");
//////                return;
//////            }

//////            foreach (var queue in queues.OrderBy(q => q.Name))
//////            {
//////                Console.WriteLine($"┌─ Queue: {queue.Name}");
//////                Console.WriteLine($"│  Address: {queue.Address}");
//////                Console.WriteLine($"│  Type: {queue.RoutingType} | Durable: {queue.Durable} | Temporaire: {queue.Temporary}");
//////                Console.WriteLine($"│");
//////                Console.WriteLine($"│  📊 Messages:");
//////                Console.WriteLine($"│     • En attente: {queue.MessageCount:N0}");
//////                Console.WriteLine($"│     • En cours de livraison: {queue.DeliveringCount:N0}");
//////                Console.WriteLine($"│     • Programmés: {queue.ScheduledCount:N0}");
//////                Console.WriteLine($"│     • Total ajoutés: {queue.MessagesAdded:N0}");
//////                Console.WriteLine($"│     • Total acquittés: {queue.MessagesAcknowledged:N0}");
//////                Console.WriteLine($"│     • Expirés: {queue.MessagesExpired:N0}");
//////                Console.WriteLine($"│     • Supprimés: {queue.MessagesKilled:N0}");
//////                Console.WriteLine($"│");
//////                Console.WriteLine($"│  👥 Consommateurs: {queue.ConsumerCount}");

//////                // Calcul du taux de traitement
//////                if (queue.MessagesAdded > 0)
//////                {
//////                    double processRate = (double)queue.MessagesAcknowledged / queue.MessagesAdded * 100;
//////                    Console.WriteLine($"│  📈 Taux de traitement: {processRate:F2}%");
//////                }

//////                Console.WriteLine("└" + new string('─', 78));
//////                Console.WriteLine();
//////            }

//////            // Statistiques globales
//////            Console.WriteLine("\n╔════════════════════════════════════════════════════════════════════════════════╗");
//////            Console.WriteLine("║                          STATISTIQUES GLOBALES                                 ║");
//////            Console.WriteLine("╚════════════════════════════════════════════════════════════════════════════════╝\n");
//////            Console.WriteLine($"Total de queues: {queues.Count}");
//////            Console.WriteLine($"Total de messages en attente: {queues.Sum(q => q.MessageCount):N0}");
//////            Console.WriteLine($"Total de messages ajoutés: {queues.Sum(q => q.MessagesAdded):N0}");
//////            Console.WriteLine($"Total de messages acquittés: {queues.Sum(q => q.MessagesAcknowledged):N0}");
//////            Console.WriteLine($"Total de consommateurs: {queues.Sum(q => q.ConsumerCount)}");
//////            Console.WriteLine($"Queues durables: {queues.Count(q => q.Durable)}");
//////            Console.WriteLine($"Queues temporaires: {queues.Count(q => q.Temporary)}");
//////            Console.WriteLine();
//////        }

//////        static async Task DisplayBrokerStatistics(HttpClient client, string artemisUrl, string brokerName)
//////        {
//////            try
//////            {
//////                string brokerUrl = $"{artemisUrl}/console/jolokia/read/" +
//////                    $"org.apache.activemq.artemis:broker=\"{brokerName}\"";

//////                var response = await client.GetAsync(brokerUrl);

//////                if (response.IsSuccessStatusCode)
//////                {
//////                    var content = await response.Content.ReadAsStringAsync();
//////                    using var doc = JsonDocument.Parse(content);

//////                    if (doc.RootElement.TryGetProperty("value", out var value))
//////                    {
//////                        Console.WriteLine("╔════════════════════════════════════════════════════════════════════════════════╗");
//////                        Console.WriteLine("║                        INFORMATIONS DU BROKER                                  ║");
//////                        Console.WriteLine("╚════════════════════════════════════════════════════════════════════════════════╝\n");
//////                        Console.WriteLine($"Version: {GetJsonProperty(value, "Version")}");
//////                        Console.WriteLine($"Uptime: {GetJsonProperty(value, "Uptime")}");
//////                        Console.WriteLine($"Total connections: {GetJsonLong(value, "ConnectionCount")}");
//////                        Console.WriteLine($"Total addresses: {GetJsonLong(value, "AddressCount")}");
//////                        Console.WriteLine($"Total queue count: {GetJsonLong(value, "QueueCount")}");
//////                        Console.WriteLine($"Mémoire totale: {GetJsonLong(value, "GlobalMaxSize"):N0} bytes");
//////                        Console.WriteLine();
//////                    }
//////                }
//////            }
//////            catch (Exception ex)
//////            {
//////                Console.WriteLine($"Impossible de récupérer les stats du broker: {ex.Message}");
//////            }
//////        }

//////        // Méthodes utilitaires pour extraire les valeurs JSON
//////        static string GetJsonProperty(JsonElement element, string property)
//////        {
//////            return element.TryGetProperty(property, out var value) ? 
//////                value.ToString() : "N/A";
//////        }

//////        static long GetJsonLong(JsonElement element, string property)
//////        {
//////            return element.TryGetProperty(property, out var value) && 
//////                value.TryGetInt64(out var result) ? result : 0;
//////        }

//////        static int GetJsonInt(JsonElement element, string property)
//////        {
//////            return element.TryGetProperty(property, out var value) && 
//////                value.TryGetInt32(out var result) ? result : 0;
//////        }

//////        static bool GetJsonBool(JsonElement element, string property)
//////        {
//////            return element.TryGetProperty(property, out var value) && 
//////                value.GetBoolean();
//////        }
//////    }
//////}

    private static async Task<QueueInfo?> GetQueueDetails(HttpClient client,
                                                          string artemisUrl,
                                                          string brokerName,
                                                          string queueName)
    {
        try
        {
            // Encoder le nom de la queue pour l'URL
            var encodedQueueName = Uri.EscapeDataString(queueName);

            // Récupérer tous les attributs de la queue
            var queueUrl = $"{artemisUrl}/console/jolokia/read/org.apache.activemq.artemis:broker=\"{brokerName}\"," +
                           $"component=addresses,address=\"{encodedQueueName}\",subcomponent=queues,routing-type=\"anycast\",queue=\"{encodedQueueName}\"";

            var response = await client.GetAsync(queueUrl);

            if (!response.IsSuccessStatusCode)
            {
                // Essayer avec routing-type multicast si anycast échoue
                queueUrl = $"{artemisUrl}/console/jolokia/read/" +
                           $"org.apache.activemq.artemis:broker=\"{brokerName}\"," +
                           $"component=addresses,address=\"{encodedQueueName}\"," +
                           $"subcomponent=queues,routing-type=\"multicast\",queue=\"{encodedQueueName}\"";

                response = await client.GetAsync(queueUrl);

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }
            }

            var content = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);

            if (doc.RootElement.TryGetProperty("value", out var value))
            {
                return new QueueInfo
                {
                    Name = queueName,
                    Address = GetJsonProperty(value, "Address"),
                    MessageCount = GetJsonLong(value, "MessageCount"),
                    MessagesAdded = GetJsonLong(value, "MessagesAdded"),
                    MessagesAcknowledged = GetJsonLong(value, "MessagesAcknowledged"),
                    ConsumerCount = GetJsonInt(value, "ConsumerCount"),
                    DeliveringCount = GetJsonLong(value, "DeliveringCount"),
                    ScheduledCount = GetJsonLong(value, "ScheduledCount"),
                    Durable = GetJsonBool(value, "Durable"),
                    Temporary = GetJsonBool(value, "Temporary"),
                    RoutingType = GetJsonProperty(value, "RoutingType"),
                    MessagesExpired = GetJsonLong(value, "MessagesExpired"),
                    MessagesKilled = GetJsonLong(value, "MessagesKilled")
                };
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur pour la queue {queueName}: {ex.Message}");
        }

        return null;
    }

    private static void DisplayQueueStatistics(List<QueueInfo> queues)
    {
        Console.WriteLine("╔════════════════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                          STATISTIQUES DES QUEUES                               ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════════════════════╝\n");

        if (queues.Count == 0)
        {
            Console.WriteLine("Aucune queue trouvée.\n");
            return;
        }

        foreach (var queue in queues.OrderBy(q => q.Name))
        {
            Console.WriteLine($"┌─ Queue: {queue.Name}");
            Console.WriteLine($"│  Address: {queue.Address}");
            Console.WriteLine($"│  Type: {queue.RoutingType} | Durable: {queue.Durable} | Temporaire: {queue.Temporary}");
            Console.WriteLine("│");
            Console.WriteLine("│  📊 Messages:");
            Console.WriteLine($"│     • En attente: {queue.MessageCount:N0}");
            Console.WriteLine($"│     • En cours de livraison: {queue.DeliveringCount:N0}");
            Console.WriteLine($"│     • Programmés: {queue.ScheduledCount:N0}");
            Console.WriteLine($"│     • Total ajoutés: {queue.MessagesAdded:N0}");
            Console.WriteLine($"│     • Total acquittés: {queue.MessagesAcknowledged:N0}");
            Console.WriteLine($"│     • Expirés: {queue.MessagesExpired:N0}");
            Console.WriteLine($"│     • Supprimés: {queue.MessagesKilled:N0}");
            Console.WriteLine("│");
            Console.WriteLine($"│  👥 Consommateurs: {queue.ConsumerCount}");

            // Calcul du taux de traitement
            if (queue.MessagesAdded > 0)
            {
                var processRate = (double)queue.MessagesAcknowledged / queue.MessagesAdded * 100;
                Console.WriteLine($"│  📈 Taux de traitement: {processRate:F2}%");
            }

            Console.WriteLine("└" + new string('─', 78));
            Console.WriteLine();
        }

        // Statistiques globales
        Console.WriteLine("\n╔════════════════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                          STATISTIQUES GLOBALES                                 ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════════════════════╝\n");
        Console.WriteLine($"Total de queues: {queues.Count}");
        Console.WriteLine($"Total de messages en attente: {queues.Sum(q => q.MessageCount):N0}");
        Console.WriteLine($"Total de messages ajoutés: {queues.Sum(q => q.MessagesAdded):N0}");
        Console.WriteLine($"Total de messages acquittés: {queues.Sum(q => q.MessagesAcknowledged):N0}");
        Console.WriteLine($"Total de consommateurs: {queues.Sum(q => q.ConsumerCount)}");
        Console.WriteLine($"Queues durables: {queues.Count(q => q.Durable)}");
        Console.WriteLine($"Queues temporaires: {queues.Count(q => q.Temporary)}");
        Console.WriteLine();
    }

    // Méthodes utilitaires pour extraire les valeurs JSON
    private static string GetJsonProperty(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) ? value.ToString() : "N/A";

    private static long GetJsonLong(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) &&
        value.TryGetInt64(out var result)
            ? result
            : 0;

    private static int GetJsonInt(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) &&
        value.TryGetInt32(out var result)
            ? result
            : 0;

    private static bool GetJsonBool(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) &&
        value.GetBoolean();

    private void DisplayHeader(string title)
    {
        const int totalWidth = 100;
        var paddingWidth = (totalWidth - title.Length) / 2;

        var padding = new string(' ', paddingWidth);

        var topBorder = new string('═', totalWidth);

        WriteColoredLine(ConsoleColor.Green, $"╔{topBorder}╗");

        if (title.Length % 2 != 0)
        {
            WriteColoredLine(ConsoleColor.Green, $"║{padding} {title}{padding}║");
        }
        else
        {
            WriteColoredLine(ConsoleColor.Green, $"║{padding}{title}{padding}║");
        }

        WriteColoredLine(ConsoleColor.Green, $"╚{topBorder}╝\n");

        Console.ResetColor();
    }

    private void WriteColoredLine(ConsoleColor color, string text)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ResetColor();
    }

    private void WriteColored(ConsoleColor color, string text)
    {
        Console.ForegroundColor = color;
        Console.Write(text);
        Console.ResetColor();
    }

    private async Task<int> ExecuteGitCommand(string arguments)
    {
        try
        {
            await Task.CompletedTask;

            return 1;
        }
        catch (Exception ex)
        {
            return HandleError($"Erreur lors de l'exécution de 'git {arguments}' : {ex.Message}");
        }
    }

    private int HandleError(string message)
    {
        WriteColoredLine(ConsoleColor.Red, message);
        return 1;
    }

    private class QueueInfo
    {
        public string? Name { get; set; }
        public string? Address { get; set; }
        public long MessageCount { get; set; }
        public long MessagesAdded { get; set; }
        public long MessagesAcknowledged { get; set; }
        public int ConsumerCount { get; set; }
        public long DeliveringCount { get; set; }
        public long ScheduledCount { get; set; }
        public bool Durable { get; set; }
        public bool Temporary { get; set; }
        public string? RoutingType { get; set; }
        public long MessagesExpired { get; set; }
        public long MessagesKilled { get; set; }
    }
}