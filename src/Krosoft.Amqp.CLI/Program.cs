////using System;
////using System.Net.Http;
////using System.Net.Http.Headers;
////using System.Text;
////using System.Text.Json;
////using System.Threading.Tasks;
////using System.Collections.Generic;
////using System.Linq;

////namespace ArtemisQueueDetails
////{

////    using System;
////using System.Net.Http;
////using System.Net.Http.Headers;
////using System.Text;
////using System.Text.Json;
////using System.Threading.Tasks;

////namespace ArtemisRestQueues
////{
////    class Program
////    {
////        static async Task Main(string[] args)
////        {
////            string artemisUrl = "http://localhost:8161"; // URL de la console web Artemis
////            string username = "admin";
////            string password = "admin";

////            try
////            {
////                using var client = new HttpClient();
                
////                // Configuration de l'authentification Basic
////                var authValue = Convert.ToBase64String(
////                    Encoding.UTF8.GetBytes($"{username}:{password}"));
////                client.DefaultRequestHeaders.Authorization = 
////                    new AuthenticationHeaderValue("Basic", authValue);

////                // Endpoint Jolokia pour obtenir les informations du broker
////                string jolokiaUrl = $"{artemisUrl}/console/jolokia/read/" +
////                    "org.apache.activemq.artemis:broker=*";

////                Console.WriteLine("Récupération des informations du broker...");
////                var response = await client.GetAsync(jolokiaUrl);
                
////                if (response.IsSuccessStatusCode)
////                {
////                    var content = await response.Content.ReadAsStringAsync();
                    
////                    // Parser la réponse JSON
////                    using var doc = JsonDocument.Parse(content);
////                    var root = doc.RootElement;
                    
////                    Console.WriteLine("\n=== Queues AMQP ===");
                    
////                    // Navigation dans la structure JSON de Jolokia
////                    if (root.TryGetProperty("value", out var value))
////                    {
////                        foreach (var broker in value.EnumerateObject())
////                        {
////                            Console.WriteLine($"\nBroker: {broker.Name}");
                            
////                            if (broker.Value.TryGetProperty("QueueNames", out var queues))
////                            {
////                                foreach (var queue in queues.EnumerateArray())
////                                {
////                                    Console.WriteLine($"  - {queue.GetString()}");
////                                }
////                            }
////                        }
////                    }
////                }
////                else
////                {
////                    Console.WriteLine($"Erreur HTTP: {response.StatusCode}");
////                    var errorContent = await response.Content.ReadAsStringAsync();
////                    Console.WriteLine(errorContent);
////                }

////                // Alternative: Utilisation de l'API REST native d'Artemis
////                Console.WriteLine("\n\n=== Alternative avec API REST native ===");
////                string addressesUrl = $"{artemisUrl}/console/jolokia/exec/" +
////                    "org.apache.activemq.artemis:broker=\"0.0.0.0\"/getAddressNames";
                
////                var addressResponse = await client.PostAsync(addressesUrl, null);
                
////                if (addressResponse.IsSuccessStatusCode)
////                {
////                    var addressContent = await addressResponse.Content.ReadAsStringAsync();
////                    using var addrDoc = JsonDocument.Parse(addressContent);
                    
////                    if (addrDoc.RootElement.TryGetProperty("value", out var addresses))
////                    {
////                        Console.WriteLine("\nAdresses (peuvent contenir des queues):");
////                        foreach (var addr in addresses.EnumerateArray())
////                        {
////                            Console.WriteLine($"  - {addr.GetString()}");
////                        }
////                    }
////                }
////            }
////            catch (Exception ex)
////            {
////                Console.WriteLine($"Erreur: {ex.Message}");
////                Console.WriteLine(ex.StackTrace);
////            }
////        }
////    }
////}






////    class QueueInfo
////    {
////        public string? Name { get; set; }
////        public string? Address { get; set; }
////        public long MessageCount { get; set; }
////        public long MessagesAdded { get; set; }
////        public long MessagesAcknowledged { get; set; }
////        public int ConsumerCount { get; set; }
////        public long DeliveringCount { get; set; }
////        public long ScheduledCount { get; set; }
////        public bool Durable { get; set; }
////        public bool Temporary { get; set; }
////        public string? RoutingType { get; set; }
////        public long MessagesExpired { get; set; }
////        public long MessagesKilled { get; set; }
////    }

////    class Program
////    {
////        static async Task Main(string[] args)
////        {
////            string artemisUrl = "http://localhost:8161";
////            string username = "admin";
////            string password = "admin";
////            string brokerName = "0.0.0.0"; // Nom du broker, souvent "0.0.0.0" ou "localhost"

////            try
////            {
////                using var client = new HttpClient();
                
////                // Configuration de l'authentification Basic
////                var authValue = Convert.ToBase64String(
////                    Encoding.UTF8.GetBytes($"{username}:{password}"));
////                client.DefaultRequestHeaders.Authorization = 
////                    new AuthenticationHeaderValue("Basic", authValue);

////                // 1. Récupérer la liste des queues
////                Console.WriteLine("Récupération de la liste des queues...\n");
////                string queueNamesUrl = $"{artemisUrl}/console/jolokia/read/" +
////                    $"org.apache.activemq.artemis:broker=\"{brokerName}\"/QueueNames";

////                var response = await client.GetAsync(queueNamesUrl);
                
////                if (!response.IsSuccessStatusCode)
////                {
////                    Console.WriteLine($"Erreur HTTP: {response.StatusCode}");
////                    Console.WriteLine(await response.Content.ReadAsStringAsync());
////                    return;
////                }

////                var content = await response.Content.ReadAsStringAsync();
////                using var doc = JsonDocument.Parse(content);
                
////                var queues = new List<QueueInfo>();

////                if (doc.RootElement.TryGetProperty("value", out var queueNames))
////                {
////                    // 2. Pour chaque queue, récupérer les détails
////                    foreach (var queueName in queueNames.EnumerateArray())
////                    {
////                        string? queue = queueName.GetString();
////                        var queueInfo = await GetQueueDetails(client, artemisUrl, brokerName, queue!);
////                        if (queueInfo != null)
////                        {
////                            queues.Add(queueInfo);
////                        }
////                    }
////                }

////                // 3. Afficher les résultats
////                DisplayQueueStatistics(queues);

////                // 4. Obtenir les statistiques globales du broker
////                await DisplayBrokerStatistics(client, artemisUrl, brokerName);

////            }
////            catch (Exception ex)
////            {
////                Console.WriteLine($"Erreur: {ex.Message}");
////                Console.WriteLine(ex.StackTrace);
////            }
////        }

////        static async Task<QueueInfo?> GetQueueDetails(HttpClient client, string artemisUrl, 
////                                                      string brokerName, string queueName)
////        {
////            try
////            {
////                // Encoder le nom de la queue pour l'URL
////                string encodedQueueName = Uri.EscapeDataString(queueName);
                
////                // Récupérer tous les attributs de la queue
////                string queueUrl = $"{artemisUrl}/console/jolokia/read/" +
////                    $"org.apache.activemq.artemis:broker=\"{brokerName}\"," +
////                    $"component=addresses,address=\"{encodedQueueName}\"," +
////                    $"subcomponent=queues,routing-type=\"anycast\",queue=\"{encodedQueueName}\"";

////                var response = await client.GetAsync(queueUrl);
                
////                if (!response.IsSuccessStatusCode)
////                {
////                    // Essayer avec routing-type multicast si anycast échoue
////                    queueUrl = $"{artemisUrl}/console/jolokia/read/" +
////                        $"org.apache.activemq.artemis:broker=\"{brokerName}\"," +
////                        $"component=addresses,address=\"{encodedQueueName}\"," +
////                        $"subcomponent=queues,routing-type=\"multicast\",queue=\"{encodedQueueName}\"";
                    
////                    response = await client.GetAsync(queueUrl);
                    
////                    if (!response.IsSuccessStatusCode)
////                    {
////                        return null;
////                    }
////                }

////                var content = await response.Content.ReadAsStringAsync();
////                using var doc = JsonDocument.Parse(content);

////                if (doc.RootElement.TryGetProperty("value", out var value))
////                {
////                    return new QueueInfo
////                    {
////                        Name = queueName,
////                        Address = GetJsonProperty(value, "Address"),
////                        MessageCount = GetJsonLong(value, "MessageCount"),
////                        MessagesAdded = GetJsonLong(value, "MessagesAdded"),
////                        MessagesAcknowledged = GetJsonLong(value, "MessagesAcknowledged"),
////                        ConsumerCount = GetJsonInt(value, "ConsumerCount"),
////                        DeliveringCount = GetJsonLong(value, "DeliveringCount"),
////                        ScheduledCount = GetJsonLong(value, "ScheduledCount"),
////                        Durable = GetJsonBool(value, "Durable"),
////                        Temporary = GetJsonBool(value, "Temporary"),
////                        RoutingType = GetJsonProperty(value, "RoutingType"),
////                        MessagesExpired = GetJsonLong(value, "MessagesExpired"),
////                        MessagesKilled = GetJsonLong(value, "MessagesKilled")
////                    };
////                }
////            }
////            catch (Exception ex)
////            {
////                Console.WriteLine($"Erreur pour la queue {queueName}: {ex.Message}");
////            }

////            return null;
////        }

////        static void DisplayQueueStatistics(List<QueueInfo> queues)
////        {
////            Console.WriteLine("╔════════════════════════════════════════════════════════════════════════════════╗");
////            Console.WriteLine("║                          STATISTIQUES DES QUEUES                               ║");
////            Console.WriteLine("╚════════════════════════════════════════════════════════════════════════════════╝\n");

////            if (queues.Count == 0)
////            {
////                Console.WriteLine("Aucune queue trouvée.\n");
////                return;
////            }

////            foreach (var queue in queues.OrderBy(q => q.Name))
////            {
////                Console.WriteLine($"┌─ Queue: {queue.Name}");
////                Console.WriteLine($"│  Address: {queue.Address}");
////                Console.WriteLine($"│  Type: {queue.RoutingType} | Durable: {queue.Durable} | Temporaire: {queue.Temporary}");
////                Console.WriteLine($"│");
////                Console.WriteLine($"│  📊 Messages:");
////                Console.WriteLine($"│     • En attente: {queue.MessageCount:N0}");
////                Console.WriteLine($"│     • En cours de livraison: {queue.DeliveringCount:N0}");
////                Console.WriteLine($"│     • Programmés: {queue.ScheduledCount:N0}");
////                Console.WriteLine($"│     • Total ajoutés: {queue.MessagesAdded:N0}");
////                Console.WriteLine($"│     • Total acquittés: {queue.MessagesAcknowledged:N0}");
////                Console.WriteLine($"│     • Expirés: {queue.MessagesExpired:N0}");
////                Console.WriteLine($"│     • Supprimés: {queue.MessagesKilled:N0}");
////                Console.WriteLine($"│");
////                Console.WriteLine($"│  👥 Consommateurs: {queue.ConsumerCount}");
                
////                // Calcul du taux de traitement
////                if (queue.MessagesAdded > 0)
////                {
////                    double processRate = (double)queue.MessagesAcknowledged / queue.MessagesAdded * 100;
////                    Console.WriteLine($"│  📈 Taux de traitement: {processRate:F2}%");
////                }
                
////                Console.WriteLine("└" + new string('─', 78));
////                Console.WriteLine();
////            }

////            // Statistiques globales
////            Console.WriteLine("\n╔════════════════════════════════════════════════════════════════════════════════╗");
////            Console.WriteLine("║                          STATISTIQUES GLOBALES                                 ║");
////            Console.WriteLine("╚════════════════════════════════════════════════════════════════════════════════╝\n");
////            Console.WriteLine($"Total de queues: {queues.Count}");
////            Console.WriteLine($"Total de messages en attente: {queues.Sum(q => q.MessageCount):N0}");
////            Console.WriteLine($"Total de messages ajoutés: {queues.Sum(q => q.MessagesAdded):N0}");
////            Console.WriteLine($"Total de messages acquittés: {queues.Sum(q => q.MessagesAcknowledged):N0}");
////            Console.WriteLine($"Total de consommateurs: {queues.Sum(q => q.ConsumerCount)}");
////            Console.WriteLine($"Queues durables: {queues.Count(q => q.Durable)}");
////            Console.WriteLine($"Queues temporaires: {queues.Count(q => q.Temporary)}");
////            Console.WriteLine();
////        }

////        static async Task DisplayBrokerStatistics(HttpClient client, string artemisUrl, string brokerName)
////        {
////            try
////            {
////                string brokerUrl = $"{artemisUrl}/console/jolokia/read/" +
////                    $"org.apache.activemq.artemis:broker=\"{brokerName}\"";

////                var response = await client.GetAsync(brokerUrl);
                
////                if (response.IsSuccessStatusCode)
////                {
////                    var content = await response.Content.ReadAsStringAsync();
////                    using var doc = JsonDocument.Parse(content);

////                    if (doc.RootElement.TryGetProperty("value", out var value))
////                    {
////                        Console.WriteLine("╔════════════════════════════════════════════════════════════════════════════════╗");
////                        Console.WriteLine("║                        INFORMATIONS DU BROKER                                  ║");
////                        Console.WriteLine("╚════════════════════════════════════════════════════════════════════════════════╝\n");
////                        Console.WriteLine($"Version: {GetJsonProperty(value, "Version")}");
////                        Console.WriteLine($"Uptime: {GetJsonProperty(value, "Uptime")}");
////                        Console.WriteLine($"Total connections: {GetJsonLong(value, "ConnectionCount")}");
////                        Console.WriteLine($"Total addresses: {GetJsonLong(value, "AddressCount")}");
////                        Console.WriteLine($"Total queue count: {GetJsonLong(value, "QueueCount")}");
////                        Console.WriteLine($"Mémoire totale: {GetJsonLong(value, "GlobalMaxSize"):N0} bytes");
////                        Console.WriteLine();
////                    }
////                }
////            }
////            catch (Exception ex)
////            {
////                Console.WriteLine($"Impossible de récupérer les stats du broker: {ex.Message}");
////            }
////        }

////        // Méthodes utilitaires pour extraire les valeurs JSON
////        static string GetJsonProperty(JsonElement element, string property)
////        {
////            return element.TryGetProperty(property, out var value) ? 
////                value.ToString() : "N/A";
////        }

////        static long GetJsonLong(JsonElement element, string property)
////        {
////            return element.TryGetProperty(property, out var value) && 
////                value.TryGetInt64(out var result) ? result : 0;
////        }

////        static int GetJsonInt(JsonElement element, string property)
////        {
////            return element.TryGetProperty(property, out var value) && 
////                value.TryGetInt32(out var result) ? result : 0;
////        }

////        static bool GetJsonBool(JsonElement element, string property)
////        {
////            return element.TryGetProperty(property, out var value) && 
////                value.GetBoolean();
////        }
////    }
////}

//using System;
//using System.Net.Http;
//using System.Net.Http.Headers;
//using System.Text;
//using System.Text.Json;
//using System.Threading.Tasks;
//using System.Collections.Generic;
//using System.Linq;

//namespace ArtemisQueueMessages
//{
//    class MessageInfo
//    {
//        public long MessageID { get; set; }
//        public string? Address { get; set; }
//        public string? Type { get; set; }
//        public bool Durable { get; set; }
//        public DateTime Timestamp { get; set; }
//        public int Priority { get; set; }
//        public long Expiration { get; set; }
//        public string? UserID { get; set; }
//        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
//        public string? Body { get; set; }
//        public long BodySize { get; set; }
//        public bool Redelivered { get; set; }
//        public int DeliveryCount { get; set; }
//        public string? OriginalQueue { get; set; }
//    }

//    class Program
//    {
//        static async Task Main(string[] args)
//        {
//            string artemisUrl = "http://localhost:8161";
//            string username = "admin";
//            string password = "admin";
//            string brokerName = "0.0.0.0";
            
//            // Demander le nom de la queue à l'utilisateur
//            Console.Write("Entrez le nom de la queue (ou appuyez sur Entrée pour 'LOCAL_ARCESB_HUB'): ");
//            string queueName = Console.ReadLine() ?? throw new InvalidOperationException();
//            if (string.IsNullOrWhiteSpace(queueName))
//            {
//                queueName = "LOCAL_ARCESB_HUB";
//            }

//            try
//            {
//                using var client = new HttpClient();
//                client.Timeout = TimeSpan.FromSeconds(30);
                
//                // Configuration de l'authentification Basic
//                var authValue = Convert.ToBase64String(
//                    Encoding.UTF8.GetBytes($"{username}:{password}"));
//                client.DefaultRequestHeaders.Authorization = 
//                    new AuthenticationHeaderValue("Basic", authValue);

//                Console.WriteLine($"\nRécupération des messages de la queue '{queueName}'...\n");

//                // 1. Vérifier si la queue existe et obtenir ses infos
//                var queueExists = await CheckQueueExists(client, artemisUrl, brokerName, queueName);
//                if (!queueExists)
//                {
//                    Console.WriteLine($"❌ La queue '{queueName}' n'existe pas ou n'est pas accessible.");
//                    Console.WriteLine("\nQueues disponibles:");
//                    await ListAvailableQueues(client, artemisUrl, brokerName);
//                    return;
//                }

//                // 2. Récupérer les messages via l'opération browseMessages
//                var messages = await BrowseMessages(client, artemisUrl, brokerName, queueName);

//                // 3. Afficher les messages
//                DisplayMessages(messages, queueName);

//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"❌ Erreur: {ex.Message}");
//                Console.WriteLine(ex.StackTrace);
//            }
//        }

//        static async Task<bool> CheckQueueExists(HttpClient client, string artemisUrl, 
//            string brokerName, string queueName)
//        {
//            try
//            {
//                string encodedQueueName = Uri.EscapeDataString(queueName);
                
//                // Essayer anycast d'abord
//                string queueUrl = $"{artemisUrl}/console/jolokia/read/" +
//                    $"org.apache.activemq.artemis:broker=\"{brokerName}\"," +
//                    $"component=addresses,address=\"{encodedQueueName}\"," +
//                    $"subcomponent=queues,routing-type=\"anycast\",queue=\"{encodedQueueName}\"" +
//                    "/MessageCount";

//                var response = await client.GetAsync(queueUrl);
                
//                if (response.IsSuccessStatusCode)
//                {
//                    return true;
//                }

//                // Essayer multicast
//                queueUrl = $"{artemisUrl}/console/jolokia/read/" +
//                    $"org.apache.activemq.artemis:broker=\"{brokerName}\"," +
//                    $"component=addresses,address=\"{encodedQueueName}\"," +
//                    $"subcomponent=queues,routing-type=\"multicast\",queue=\"{encodedQueueName}\"" +
//                    "/MessageCount";

//                response = await client.GetAsync(queueUrl);
//                return response.IsSuccessStatusCode;
//            }
//            catch
//            {
//                return false;
//            }
//        }

//        static async Task<List<MessageInfo>> BrowseMessages(HttpClient client, string artemisUrl, 
//            string brokerName, string queueName)
//        {
//            var messages = new List<MessageInfo>();

//            try
//            {
//                string encodedQueueName = Uri.EscapeDataString(queueName);
                
//                // Tenter avec anycast puis multicast
//                foreach (var routingType in new[] { "anycast", "multicast" })
//                {
//                    // Appeler l'opération browseMessages via Jolokia avec POST JSON
//                    string browseUrl = $"{artemisUrl}/console/jolokia/";

//                    var jolokiaRequest = new
//                    {
//                        type = "exec",
//                        mbean = $"org.apache.activemq.artemis:broker=\"{brokerName}\"," +
//                                $"component=addresses,address=\"{queueName}\"," +
//                                $"subcomponent=queues,routing-type=\"{routingType}\",queue=\"{queueName}\"",
//                        operation = "browse()",
//                        arguments = new object[] { }
//                    };

//                    var jsonContent = new StringContent(
//                        JsonSerializer.Serialize(jolokiaRequest),
//                        Encoding.UTF8,
//                        "application/json");

//                    var response = await client.PostAsync(browseUrl, jsonContent);
                    
//                    if (!response.IsSuccessStatusCode)
//                    {
//                        // Si browse() échoue, essayer listMessagesAsJSON
//                        Console.WriteLine($"⚠️  browse() a échoué, tentative avec listMessagesAsJSON...");
                        
//                        jolokiaRequest = new
//                        {
//                            type = "exec",
//                            mbean = $"org.apache.activemq.artemis:broker=\"{brokerName}\"," +
//                                    $"component=addresses,address=\"{queueName}\"," +
//                                    $"subcomponent=queues,routing-type=\"{routingType}\",queue=\"{queueName}\"",
//                            operation = "listMessagesAsJSON",
//                            arguments = new object[] { "" } // Filter vide pour tous les messages
//                        };

//                        jsonContent = new StringContent(
//                            JsonSerializer.Serialize(jolokiaRequest),
//                            Encoding.UTF8,
//                            "application/json");

//                        response = await client.PostAsync(browseUrl, jsonContent);
//                    }
                    
//                    if (response.IsSuccessStatusCode)
//                    {
//                        var content = await response.Content.ReadAsStringAsync();
//                        using var doc = JsonDocument.Parse(content);

//                        if (doc.RootElement.TryGetProperty("value", out var value))
//                        {
//                            // La réponse contient un tableau JSON de messages
//                            if (value.ValueKind == JsonValueKind.Array)
//                            {
//                                foreach (var msgElement in value.EnumerateArray())
//                                {
//                                    var message = ParseMessage(msgElement);
//                                    if (message != null)
//                                    {
//                                        messages.Add(message);
//                                    }
//                                }
//                            }
//                            else if (value.ValueKind == JsonValueKind.String)
//                            {
//                                // Certaines versions retournent une string JSON
//                                var innerJson = JsonDocument.Parse(value.GetString() ?? throw new InvalidOperationException());
//                                foreach (var msgElement in innerJson.RootElement.EnumerateArray())
//                                {
//                                    var message = ParseMessage(msgElement);
//                                    if (message != null)
//                                    {
//                                        messages.Add(message);
//                                    }
//                                }
//                            }
//                        }
//                        break; // Succès, sortir de la boucle
//                    }
//                    else
//                    {
//                        var errorContent = await response.Content.ReadAsStringAsync();
//                        Console.WriteLine($"⚠️  Erreur {routingType}: {response.StatusCode}");
//                        Console.WriteLine($"    Détails: {errorContent}");
//                    }
//                }
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"⚠️  Erreur lors de la récupération des messages: {ex.Message}");
//                Console.WriteLine($"    Stack: {ex.StackTrace}");
//            }

//            return messages;
//        }

//        static MessageInfo? ParseMessage(JsonElement msgElement)
//        {
//            try
//            {
//                var message = new MessageInfo
//                {
//                    Properties = new Dictionary<string, object>()
//                };

//                // Parcourir toutes les propriétés du message
//                foreach (var prop in msgElement.EnumerateObject())
//                {
//                    switch (prop.Name.ToLower())
//                    {
//                        case "messageid":
                            
//                                message.MessageID  = long.Parse(prop.Value.GetString() ?? throw new InvalidOperationException());
                        
//                            break;
//                        case "address":
//                            message.Address = prop.Value.GetString();
//                            break;
//                        case "type":
//                            message.Type = prop.Value.GetInt32().ToString();
//                            break;
//                        case "durable":
//                            message.Durable = prop.Value.GetBoolean();
//                            break;
//                        case "timestamp":
//                            if (prop.Value.TryGetInt64(out var ts))
//                                message.Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(ts).DateTime;
//                            break;
//                        case "priority":
//                            if (prop.Value.TryGetInt32(out var priority))
//                                message.Priority = priority;
//                            break;
//                        case "expiration":
//                            if (prop.Value.TryGetInt64(out var exp))
//                                message.Expiration = exp;
//                            break;
//                        case "userid":
//                            message.UserID = prop.Value.GetString();
//                            break;
//                        case "text" or "body":
//                            message.Body = prop.Value.GetString();
//                            break;
//                        case "bodysize":
//                            if (prop.Value.TryGetInt64(out var size))
//                                message.BodySize = size;
//                            break;
//                        case "redelivered":
//                            message.Redelivered = prop.Value.GetBoolean();
//                            break;
//                        case "deliverycount":
//                            if (prop.Value.TryGetInt32(out var delCount))
//                                message.DeliveryCount = delCount;
//                            break;
//                        case "originalqueue":
//                            message.OriginalQueue = prop.Value.GetString();
//                            break;
//                        default:
//                            // Stocker les autres propriétés custom
//                            message.Properties[prop.Name] = prop.Value.ToString();
//                            break;
//                    }
//                }

//                return message;
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"⚠️  Erreur de parsing: {ex.Message}");
//                return null;
//            }
//        }

//        static void DisplayMessages(List<MessageInfo> messages, string queueName)
//        {
//            Console.WriteLine("╔════════════════════════════════════════════════════════════════════════════════╗");
//            Console.WriteLine($"║  Messages de la queue: {queueName.PadRight(54)} ║");
//            Console.WriteLine("╚════════════════════════════════════════════════════════════════════════════════╝\n");

//            if (messages.Count == 0)
//            {
//                Console.WriteLine("📭 Aucun message dans cette queue.\n");
//                return;
//            }

//            Console.WriteLine($"📊 Total: {messages.Count} message(s)\n");

//            int index = 1;
//            foreach (var msg in messages.OrderBy(m => m.Timestamp))
//            {
//                Console.WriteLine($"┌─ Message #{index}");
//                Console.WriteLine($"│  🆔 Message ID: {msg.MessageID}");
//                Console.WriteLine($"│  📍 Address: {msg.Address ?? "N/A"}");
//                Console.WriteLine($"│  📝 Type: {msg.Type ?? "N/A"}");
//                Console.WriteLine($"│  💾 Durable: {msg.Durable}");
//                Console.WriteLine($"│  🕒 Timestamp: {msg.Timestamp:yyyy-MM-dd HH:mm:ss.fff}");
//                Console.WriteLine($"│  ⚡ Priority: {msg.Priority}");
                
//                if (msg.Expiration > 0)
//                {
//                    var expDate = DateTimeOffset.FromUnixTimeMilliseconds(msg.Expiration).DateTime;
//                    Console.WriteLine($"│  ⏰ Expiration: {expDate:yyyy-MM-dd HH:mm:ss}");
//                }
//                else
//                {
//                    Console.WriteLine($"│  ⏰ Expiration: Jamais");
//                }

//                if (!string.IsNullOrEmpty(msg.UserID))
//                {
//                    Console.WriteLine($"│  👤 User ID: {msg.UserID}");
//                }

//                Console.WriteLine($"│  🔄 Redelivered: {msg.Redelivered}");
//                Console.WriteLine($"│  📊 Delivery Count: {msg.DeliveryCount}");
//                Console.WriteLine($"│  📏 Body Size: {msg.BodySize} bytes");

//                if (!string.IsNullOrEmpty(msg.OriginalQueue))
//                {
//                    Console.WriteLine($"│  📦 Original Queue: {msg.OriginalQueue}");
//                }

//                // Afficher les propriétés custom
//                if (msg.Properties.Count > 0)
//                {
//                    Console.WriteLine($"│");
//                    Console.WriteLine($"│  🏷️  Propriétés custom:");
//                    foreach (var prop in msg.Properties.OrderBy(p => p.Key))
//                    {
//                        Console.WriteLine($"│     • {prop.Key}: {prop.Value}");
//                    }
//                }

//                // Afficher le corps du message
//                if (!string.IsNullOrEmpty(msg.Body))
//                {
//                    Console.WriteLine($"│");
//                    Console.WriteLine($"│  📄 Corps du message:");
//                    var bodyLines = msg.Body.Split('\n');
//                    foreach (var line in bodyLines.Take(10)) // Limiter à 10 lignes
//                    {
//                        var truncated = line.Length > 70 ? line.Substring(0, 70) + "..." : line;
//                        Console.WriteLine($"│     {truncated}");
//                    }
//                    if (bodyLines.Length > 10)
//                    {
//                        Console.WriteLine($"│     ... ({bodyLines.Length - 10} lignes supplémentaires)");
//                    }
//                }

//                Console.WriteLine("└" + new string('─', 78));
//                Console.WriteLine();
//                index++;
//            }

//            // Statistiques
//            Console.WriteLine("\n╔════════════════════════════════════════════════════════════════════════════════╗");
//            Console.WriteLine("║                               STATISTIQUES                                     ║");
//            Console.WriteLine("╚════════════════════════════════════════════════════════════════════════════════╝\n");
//            Console.WriteLine($"Messages durables: {messages.Count(m => m.Durable)}");
//            Console.WriteLine($"Messages redélivrés: {messages.Count(m => m.Redelivered)}");
//            Console.WriteLine($"Taille totale: {messages.Sum(m => m.BodySize):N0} bytes");
            
//            if (messages.Any(m => m.Expiration > 0))
//            {
//                var withExpiration = messages.Where(m => m.Expiration > 0).ToList();
//                Console.WriteLine($"Messages avec expiration: {withExpiration.Count}");
//                var expired = withExpiration.Where(m => 
//                    DateTimeOffset.FromUnixTimeMilliseconds(m.Expiration).DateTime < DateTime.Now).Count();
//                if (expired > 0)
//                {
//                    Console.WriteLine($"⚠️  Messages expirés: {expired}");
//                }
//            }

//            Console.WriteLine();
//        }

//        static async Task ListAvailableQueues(HttpClient client, string artemisUrl, string brokerName)
//        {
//            try
//            {
//                string queueNamesUrl = $"{artemisUrl}/console/jolokia/read/" +
//                    $"org.apache.activemq.artemis:broker=\"{brokerName}\"/QueueNames";

//                var response = await client.GetAsync(queueNamesUrl);
                
//                if (response.IsSuccessStatusCode)
//                {
//                    var content = await response.Content.ReadAsStringAsync();
//                    using var doc = JsonDocument.Parse(content);
                    
//                    if (doc.RootElement.TryGetProperty("value", out var queueNames))
//                    {
//                        foreach (var queueName in queueNames.EnumerateArray())
//                        {
//                            Console.WriteLine($"  • {queueName.GetString()}");
//                        }
//                    }
//                }
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"Impossible de lister les queues: {ex.Message}");
//            }
//        }
//    }
//}


using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace ArtemisQueueMessages
{
    class MessageInfo
    {
        public long MessageID { get; set; }
        public string? Address { get; set; }
        public string? Type { get; set; }
        public bool Durable { get; set; }
        public DateTime Timestamp { get; set; }
        public int Priority { get; set; }
        public long Expiration { get; set; }
        public string? UserID { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
        public string? Body { get; set; }
        public long BodySize { get; set; }
        public bool Redelivered { get; set; }
        public int DeliveryCount { get; set; }
        public string? OriginalQueue { get; set; }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            string artemisUrl = "http://localhost:8161";
            string username = "admin";
            string password = "admin";
            string brokerName = "0.0.0.0";
            
            // Demander le nom de la queue à l'utilisateur
            Console.Write("Entrez le nom de la queue (ou appuyez sur Entrée pour 'LOCAL_ARCESB_HUB'): ");
            string queueName = Console.ReadLine() ?? throw new InvalidOperationException();
            if (string.IsNullOrWhiteSpace(queueName))
            {
                queueName = "LOCAL_ARCESB_HUB";
            }

            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(30);
                
                // Configuration de l'authentification Basic
                var authValue = Convert.ToBase64String(
                    Encoding.UTF8.GetBytes($"{username}:{password}"));
                client.DefaultRequestHeaders.Authorization = 
                    new AuthenticationHeaderValue("Basic", authValue);

                Console.WriteLine($"\nRécupération des messages de la queue '{queueName}'...\n");

                // 1. Vérifier si la queue existe et obtenir ses infos
                var queueExists = await CheckQueueExists(client, artemisUrl, brokerName, queueName);
                if (!queueExists)
                {
                    Console.WriteLine($"❌ La queue '{queueName}' n'existe pas ou n'est pas accessible.");
                    Console.WriteLine("\nQueues disponibles:");
                    await ListAvailableQueues(client, artemisUrl, brokerName);
                    return;
                }

                // 2. Récupérer les messages via l'opération browseMessages
                var messages = await BrowseMessages(client, artemisUrl, brokerName, queueName);

                // 3. Afficher les messages
                DisplayMessages(messages, queueName);

                // 4. Proposer l'export de messages
                if (messages.Count > 0)
                {
                    await HandleMessageExport(messages);
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erreur: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        static async Task<bool> CheckQueueExists(HttpClient client, string artemisUrl, 
            string brokerName, string queueName)
        {
            try
            {
                string encodedQueueName = Uri.EscapeDataString(queueName);
                
                // Essayer anycast d'abord
                string queueUrl = $"{artemisUrl}/console/jolokia/read/" +
                    $"org.apache.activemq.artemis:broker=\"{brokerName}\"," +
                    $"component=addresses,address=\"{encodedQueueName}\"," +
                    $"subcomponent=queues,routing-type=\"anycast\",queue=\"{encodedQueueName}\"" +
                    "/MessageCount";

                var response = await client.GetAsync(queueUrl);
                
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }

                // Essayer multicast
                queueUrl = $"{artemisUrl}/console/jolokia/read/" +
                    $"org.apache.activemq.artemis:broker=\"{brokerName}\"," +
                    $"component=addresses,address=\"{encodedQueueName}\"," +
                    $"subcomponent=queues,routing-type=\"multicast\",queue=\"{encodedQueueName}\"" +
                    "/MessageCount";

                response = await client.GetAsync(queueUrl);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        static async Task<List<MessageInfo>> BrowseMessages(HttpClient client, string artemisUrl, 
            string brokerName, string queueName)
        {
            var messages = new List<MessageInfo>();

            try
            {
                string encodedQueueName = Uri.EscapeDataString(queueName);
                
                // Tenter avec anycast puis multicast
                foreach (var routingType in new[] { "anycast", "multicast" })
                {
                    // Appeler l'opération browseMessages via Jolokia avec POST JSON
                    string browseUrl = $"{artemisUrl}/console/jolokia/";

                    var jolokiaRequest = new
                    {
                        type = "exec",
                        mbean = $"org.apache.activemq.artemis:broker=\"{brokerName}\"," +
                                $"component=addresses,address=\"{queueName}\"," +
                                $"subcomponent=queues,routing-type=\"{routingType}\",queue=\"{queueName}\"",
                        operation = "browse()",
                        arguments = new object[] { }
                    };

                    var jsonContent = new StringContent(
                        JsonSerializer.Serialize(jolokiaRequest),
                        Encoding.UTF8,
                        "application/json");

                    var response = await client.PostAsync(browseUrl, jsonContent);
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        // Si browse() échoue, essayer listMessagesAsJSON
                        Console.WriteLine($"⚠️  browse() a échoué, tentative avec listMessagesAsJSON...");
                        
                        jolokiaRequest = new
                        {
                            type = "exec",
                            mbean = $"org.apache.activemq.artemis:broker=\"{brokerName}\"," +
                                    $"component=addresses,address=\"{queueName}\"," +
                                    $"subcomponent=queues,routing-type=\"{routingType}\",queue=\"{queueName}\"",
                            operation = "listMessagesAsJSON",
                            arguments = new object[] { "" } // Filter vide pour tous les messages
                        };

                        jsonContent = new StringContent(
                            JsonSerializer.Serialize(jolokiaRequest),
                            Encoding.UTF8,
                            "application/json");

                        response = await client.PostAsync(browseUrl, jsonContent);
                    }
                    
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(content);

                        if (doc.RootElement.TryGetProperty("value", out var value))
                        {
                            // La réponse contient un tableau JSON de messages
                            if (value.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var msgElement in value.EnumerateArray())
                                {
                                    var message = ParseMessage(msgElement);
                                    if (message != null)
                                    {
                                        messages.Add(message);
                                    }
                                }
                            }
                            else if (value.ValueKind == JsonValueKind.String)
                            {
                                // Certaines versions retournent une string JSON
                                var innerJson = JsonDocument.Parse(value.GetString() ?? throw new InvalidOperationException());
                                foreach (var msgElement in innerJson.RootElement.EnumerateArray())
                                {
                                    var message = ParseMessage(msgElement);
                                    if (message != null)
                                    {
                                        messages.Add(message);
                                    }
                                }
                            }
                        }
                        break; // Succès, sortir de la boucle
                    }
                    else
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"⚠️  Erreur {routingType}: {response.StatusCode}");
                        Console.WriteLine($"    Détails: {errorContent}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️  Erreur lors de la récupération des messages: {ex.Message}");
                Console.WriteLine($"    Stack: {ex.StackTrace}");
            }

            return messages;
        }

        static MessageInfo? ParseMessage(JsonElement msgElement)
        {
            try
            {
                var message = new MessageInfo
                {
                    Properties = new Dictionary<string, object>()
                };

                // Parcourir toutes les propriétés du message
                foreach (var prop in msgElement.EnumerateObject())
                {
                    switch (prop.Name.ToLower())
                    {
                        case "messageid":
                            message.MessageID  = long.Parse(prop.Value.GetString() ?? throw new InvalidOperationException());

                            break;
                        case "address":
                            message.Address = prop.Value.GetString();
                            break;
                        case "type":
                message.Type = prop.Value.GetInt32().ToString();
                            break;
                        case "durable":
                            message.Durable = prop.Value.GetBoolean();
                            break;
                        case "timestamp":
                            if (prop.Value.TryGetInt64(out var ts))
                                message.Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(ts).DateTime;
                            break;
                        case "priority":
                            if (prop.Value.TryGetInt32(out var priority))
                                message.Priority = priority;
                            break;
                        case "expiration":
                            if (prop.Value.TryGetInt64(out var exp))
                                message.Expiration = exp;
                            break;
                        case "userid":
                            message.UserID = prop.Value.GetString();
                            break;
                        case "text" or "body":
                            message.Body = prop.Value.GetString();
                            break;
                        case "bodysize":
                            if (prop.Value.TryGetInt64(out var size))
                                message.BodySize = size;
                            break;
                        case "redelivered":
                            message.Redelivered = prop.Value.GetBoolean();
                            break;
                        case "deliverycount":
                            if (prop.Value.TryGetInt32(out var delCount))
                                message.DeliveryCount = delCount;
                            break;
                        case "originalqueue":
                            message.OriginalQueue = prop.Value.GetString();
                            break;
                        default:
                            // Stocker les autres propriétés custom
                            message.Properties[prop.Name] = prop.Value.ToString();
                            break;
                    }
                }

                return message;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️  Erreur de parsing: {ex.Message}");
                return null;
            }
        }

        static void DisplayMessages(List<MessageInfo> messages, string queueName)
        {
            Console.WriteLine("╔════════════════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine($"║  Messages de la queue: {queueName.PadRight(54)} ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════════════════════════╝\n");

            if (messages.Count == 0)
            {
                Console.WriteLine("📭 Aucun message dans cette queue.\n");
                return;
            }

            Console.WriteLine($"📊 Total: {messages.Count} message(s)\n");

            int index = 1;
            foreach (var msg in messages.OrderBy(m => m.Timestamp))
            {
                Console.WriteLine($"┌─ Message #{index}");
                Console.WriteLine($"│  🆔 Message ID: {msg.MessageID}");
                Console.WriteLine($"│  📍 Address: {msg.Address ?? "N/A"}");
                Console.WriteLine($"│  📝 Type: {msg.Type ?? "N/A"}");
                Console.WriteLine($"│  💾 Durable: {msg.Durable}");
                Console.WriteLine($"│  🕒 Timestamp: {msg.Timestamp:yyyy-MM-dd HH:mm:ss.fff}");
                Console.WriteLine($"│  ⚡ Priority: {msg.Priority}");
                
                if (msg.Expiration > 0)
                {
                    var expDate = DateTimeOffset.FromUnixTimeMilliseconds(msg.Expiration).DateTime;
                    Console.WriteLine($"│  ⏰ Expiration: {expDate:yyyy-MM-dd HH:mm:ss}");
                }
                else
                {
                    Console.WriteLine($"│  ⏰ Expiration: Jamais");
                }

                if (!string.IsNullOrEmpty(msg.UserID))
                {
                    Console.WriteLine($"│  👤 User ID: {msg.UserID}");
                }

                Console.WriteLine($"│  🔄 Redelivered: {msg.Redelivered}");
                Console.WriteLine($"│  📊 Delivery Count: {msg.DeliveryCount}");
                Console.WriteLine($"│  📏 Body Size: {msg.BodySize} bytes");

                if (!string.IsNullOrEmpty(msg.OriginalQueue))
                {
                    Console.WriteLine($"│  📦 Original Queue: {msg.OriginalQueue}");
                }

                // Afficher les propriétés custom
                if (msg.Properties.Count > 0)
                {
                    Console.WriteLine($"│");
                    Console.WriteLine($"│  🏷️  Propriétés custom:");
                    foreach (var prop in msg.Properties.OrderBy(p => p.Key))
                    {
                        Console.WriteLine($"│     • {prop.Key}: {prop.Value}");
                    }
                }

                // Afficher le corps du message
                if (!string.IsNullOrEmpty(msg.Body))
                {
                    Console.WriteLine($"│");
                    Console.WriteLine($"│  📄 Corps du message:");
                    var bodyLines = msg.Body.Split('\n');
                    foreach (var line in bodyLines.Take(10)) // Limiter à 10 lignes
                    {
                        var truncated = line.Length > 70 ? line.Substring(0, 70) + "..." : line;
                        Console.WriteLine($"│     {truncated}");
                    }
                    if (bodyLines.Length > 10)
                    {
                        Console.WriteLine($"│     ... ({bodyLines.Length - 10} lignes supplémentaires)");
                    }
                }

                Console.WriteLine("└" + new string('─', 78));
                Console.WriteLine();
                index++;
            }

            // Statistiques
            Console.WriteLine("\n╔════════════════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                               STATISTIQUES                                     ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════════════════════════╝\n");
            Console.WriteLine($"Messages durables: {messages.Count(m => m.Durable)}");
            Console.WriteLine($"Messages redélivrés: {messages.Count(m => m.Redelivered)}");
            Console.WriteLine($"Taille totale: {messages.Sum(m => m.BodySize):N0} bytes");
            
            if (messages.Any(m => m.Expiration > 0))
            {
                var withExpiration = messages.Where(m => m.Expiration > 0).ToList();
                Console.WriteLine($"Messages avec expiration: {withExpiration.Count}");
                var expired = withExpiration.Where(m => 
                    DateTimeOffset.FromUnixTimeMilliseconds(m.Expiration).DateTime < DateTime.Now).Count();
                if (expired > 0)
                {
                    Console.WriteLine($"⚠️  Messages expirés: {expired}");
                }
            }

            Console.WriteLine();
        }

        static async Task ListAvailableQueues(HttpClient client, string artemisUrl, string brokerName)
        {
            try
            {
                string queueNamesUrl = $"{artemisUrl}/console/jolokia/read/" +
                    $"org.apache.activemq.artemis:broker=\"{brokerName}\"/QueueNames";

                var response = await client.GetAsync(queueNamesUrl);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(content);
                    
                    if (doc.RootElement.TryGetProperty("value", out var queueNames))
                    {
                        foreach (var queueName in queueNames.EnumerateArray())
                        {
                            Console.WriteLine($"  • {queueName.GetString()}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Impossible de lister les queues: {ex.Message}");
            }
        }

        static async Task HandleMessageExport(List<MessageInfo> messages)
        {
            Console.WriteLine("\n╔════════════════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                            EXPORT DE MESSAGES                                  ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════════════════════════╝\n");

            while (true)
            {
                Console.Write("\nVoulez-vous exporter un message ? (O/N): ");
                var choice = Console.ReadLine()?.ToUpper();

                if (choice != "O")
                    break;

                Console.Write($"Entrez le numéro du message (1-{messages.Count}): ");
                if (int.TryParse(Console.ReadLine(), out int msgNumber) && 
                    msgNumber >= 1 && msgNumber <= messages.Count)
                {
                    var message = messages[msgNumber - 1];
                    await ExportMessage(message, msgNumber);
                }
                else
                {
                    Console.WriteLine("❌ Numéro de message invalide.");
                }
            }
        }

        static async Task ExportMessage(MessageInfo message, int messageNumber)
        {
            Console.WriteLine("\n📁 Options d'export:");
            Console.WriteLine("  1. Corps du message uniquement (txt)");
            Console.WriteLine("  2. Message complet avec métadonnées (json)");
            Console.WriteLine("  3. Message complet avec métadonnées (txt)");
            Console.WriteLine("  4. Afficher le corps complet dans la console");
            Console.Write("\nChoisissez une option (1-4): ");

            var option = Console.ReadLine();
            string filename = "";
            string content = "";

            switch (option)
            {
                case "1":
                    filename = $"message_{message.MessageID}_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                    content = message.Body ?? "[Corps vide]";
                    await File.WriteAllTextAsync(filename, content);
                    Console.WriteLine($"✅ Corps du message exporté vers: {filename}");
                    break;

                case "2":
                    filename = $"message_{message.MessageID}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                    var jsonOptions = new JsonSerializerOptions 
                    { 
                        WriteIndented = true,
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    };
                    content = JsonSerializer.Serialize(new
                    {
                        MessageID = message.MessageID,
                        Address = message.Address,
                        Type = message.Type,
                        Durable = message.Durable,
                        Timestamp = message.Timestamp,
                        Priority = message.Priority,
                        Expiration = message.Expiration > 0 
                            ? DateTimeOffset.FromUnixTimeMilliseconds(message.Expiration).DateTime.ToString("o")
                            : null,
                        UserID = message.UserID,
                        Redelivered = message.Redelivered,
                        DeliveryCount = message.DeliveryCount,
                        OriginalQueue = message.OriginalQueue,
                        BodySize = message.BodySize,
                        Properties = message.Properties,
                        Body = message.Body
                    }, jsonOptions);
                    await File.WriteAllTextAsync(filename, content);
                    Console.WriteLine($"✅ Message complet exporté vers: {filename}");
                    break;

                case "3":
                    filename = $"message_{message.MessageID}_{DateTime.Now:yyyyMMdd_HHmmss}_full.txt";
                    var sb = new StringBuilder();
                    sb.AppendLine("═══════════════════════════════════════════════════════════════════════════");
                    sb.AppendLine($"MESSAGE #{messageNumber} - Export complet");
                    sb.AppendLine("═══════════════════════════════════════════════════════════════════════════");
                    sb.AppendLine();
                    sb.AppendLine("MÉTADONNÉES:");
                    sb.AppendLine($"  Message ID      : {message.MessageID}");
                    sb.AppendLine($"  Address         : {message.Address ?? "N/A"}");
                    sb.AppendLine($"  Type            : {message.Type ?? "N/A"}");
                    sb.AppendLine($"  Durable         : {message.Durable}");
                    sb.AppendLine($"  Timestamp       : {message.Timestamp:yyyy-MM-dd HH:mm:ss.fff}");
                    sb.AppendLine($"  Priority        : {message.Priority}");
                    sb.AppendLine($"  Expiration      : {(message.Expiration > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(message.Expiration).DateTime.ToString("yyyy-MM-dd HH:mm:ss") : "Jamais")}");
                    sb.AppendLine($"  User ID         : {message.UserID ?? "N/A"}");
                    sb.AppendLine($"  Redelivered     : {message.Redelivered}");
                    sb.AppendLine($"  Delivery Count  : {message.DeliveryCount}");
                    sb.AppendLine($"  Original Queue  : {message.OriginalQueue ?? "N/A"}");
                    sb.AppendLine($"  Body Size       : {message.BodySize} bytes");
                    
                    if (message.Properties.Count > 0)
                    {
                        sb.AppendLine();
                        sb.AppendLine("PROPRIÉTÉS CUSTOM:");
                        foreach (var prop in message.Properties.OrderBy(p => p.Key))
                        {
                            sb.AppendLine($"  {prop.Key.PadRight(20)}: {prop.Value}");
                        }
                    }
                    
                    sb.AppendLine();
                    sb.AppendLine("═══════════════════════════════════════════════════════════════════════════");
                    sb.AppendLine("CORPS DU MESSAGE:");
                    sb.AppendLine("═══════════════════════════════════════════════════════════════════════════");
                    sb.AppendLine();
                    sb.AppendLine(message.Body ?? "[Corps vide]");
                    
                    await File.WriteAllTextAsync(filename, sb.ToString());
                    Console.WriteLine($"✅ Message complet exporté vers: {filename}");
                    break;

                case "4":
                    Console.WriteLine("\n╔════════════════════════════════════════════════════════════════════════════════╗");
                    Console.WriteLine($"║  CORPS COMPLET DU MESSAGE #{messageNumber}                                       ");
                    Console.WriteLine("╚════════════════════════════════════════════════════════════════════════════════╝\n");
                    Console.WriteLine(message.Body ?? "[Corps vide]");
                    Console.WriteLine("\n" + new string('─', 80));
                    break;

                default:
                    Console.WriteLine("❌ Option invalide.");
                    break;
            }
        }
    }
}