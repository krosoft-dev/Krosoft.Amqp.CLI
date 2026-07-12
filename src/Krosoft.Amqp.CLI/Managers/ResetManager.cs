using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Krosoft.Amqp.CLI.Helpers;
using Krosoft.Amqp.CLI.Interfaces;
using Krosoft.Amqp.CLI.Models;

namespace Krosoft.Amqp.CLI.Managers;

internal class ResetManager : IResetManager
{
    public async Task<int> Reset(string profilePath)
    {
        var (profile, error) = await ProfileLoader.LoadAsync(profilePath);
        if (profile is null)
        {
            WriteError(error!);
            return -1;
        }

        DisplayHeader($"RESET — {profile.Name}");

        var portainerBase = profile.Portainer.Url.TrimEnd('/');
        using var portainerClient = new HttpClient();

        try
        {
            await SetPortainerAuth(portainerClient, portainerBase, profile.Portainer);
        }
        catch (Exception ex)
        {
            WriteError($"Authentification Portainer échouée : {ex.Message}");
            return -1;
        }

        var containerIds = await ResolveContainerIds(portainerClient, portainerBase, profile);
        if (containerIds.Count == 0)
        {
            WriteError("Aucun container résolu — opération annulée.");
            return -1;
        }

        Console.WriteLine("\n[1/3] Arrêt des containers...");
        foreach (var (name, id) in containerIds)
        {
            await StopContainer(portainerClient, portainerBase, profile.Portainer.EndpointId, name, id);
        }

        Console.WriteLine("\n[2/3] Purge des files AMQP...");
        using var amqpClient = new HttpClient();
        var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{profile.Amqp.Username}:{profile.Amqp.Password}"));
        amqpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);

        var brokerName = await DiscoverBrokerName(amqpClient, profile.Amqp.Url) ?? profile.Amqp.BrokerName;
        Console.WriteLine($"  Broker : {brokerName}");

        foreach (var queue in profile.Amqp.Queues)
        {
            await PurgeQueue(amqpClient, profile.Amqp.Url, brokerName, queue);
        }

        Console.WriteLine("\n[3/3] Démarrage des containers...");
        foreach (var (name, id) in containerIds)
        {
            await StartContainer(portainerClient, portainerBase, profile.Portainer.EndpointId, name, id);
        }

        WriteColored(ConsoleColor.Green, "\nReset terminé avec succès.\n");
        return 0;
    }

    private static async Task SetPortainerAuth(HttpClient client, string baseUrl, PortainerProfile portainer)
    {
        if (!string.IsNullOrWhiteSpace(portainer.ApiKey))
        {
            client.DefaultRequestHeaders.Add("X-API-Key", portainer.ApiKey);
            Console.WriteLine("  Auth Portainer : API key");
            return;
        }

        if (string.IsNullOrWhiteSpace(portainer.Username) || string.IsNullOrWhiteSpace(portainer.Password))
        {
            throw new InvalidOperationException("Le profil doit contenir soit 'apiKey', soit 'username' + 'password'.");
        }

        var payload = JsonSerializer.Serialize(new { portainer.Username, portainer.Password });
        var response = await client.PostAsync($"{baseUrl}/api/auth", new StringContent(payload, Encoding.UTF8, "application/json"));
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var jwt = doc.RootElement.GetProperty("jwt").GetString() ?? throw new InvalidOperationException("Token JWT absent de la réponse Portainer.");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        Console.WriteLine("  Auth Portainer : JWT");
    }

    private static async Task<Dictionary<string, string>> ResolveContainerIds(HttpClient client, string baseUrl, Profile profile)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var response = await client.GetAsync($"{baseUrl}/api/endpoints/{profile.Portainer.EndpointId}/docker/containers/json?all=true");
        if (!response.IsSuccessStatusCode)
        {
            WriteError($"Impossible de lister les containers Portainer : {response.StatusCode}");
            return result;
        }

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);

        foreach (var container in doc.RootElement.EnumerateArray())
        {
            var id = container.GetProperty("Id").GetString() ?? string.Empty;
            var names = container.GetProperty("Names")
                                 .EnumerateArray()
                                 .Select(n => n.GetString()?.TrimStart('/') ?? string.Empty)
                                 .ToList();

            foreach (var targetName in profile.Containers.Where(t => !result.ContainsKey(t)))
            {
                if (names.Any(n => string.Equals(n, targetName, StringComparison.OrdinalIgnoreCase)))
                {
                    result[targetName] = id;
                }
            }
        }

        foreach (var missing in profile.Containers.Where(c => !result.ContainsKey(c)))
        {
            WriteError($"  Container introuvable : {missing}");
        }

        return result;
    }

    private static async Task StopContainer(HttpClient client, string baseUrl, int endpointId, string name, string id)
    {
        var response = await client.PostAsync($"{baseUrl}/api/endpoints/{endpointId}/docker/containers/{id}/stop", null);
        if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotModified)
        {
            WriteColored(ConsoleColor.Green, $"  [OK] {name} arrêté");
        }
        else
        {
            WriteError($"  [ERREUR] Arrêt de {name} : {response.StatusCode}");
        }
    }

    private static async Task StartContainer(HttpClient client, string baseUrl, int endpointId, string name, string id)
    {
        var response = await client.PostAsync($"{baseUrl}/api/endpoints/{endpointId}/docker/containers/{id}/start", null);
        if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotModified)
        {
            WriteColored(ConsoleColor.Green, $"  [OK] {name} démarré");
        }
        else
        {
            WriteError($"  [ERREUR] Démarrage de {name} : {response.StatusCode}");
        }
    }

    private static async Task<string?> DiscoverBrokerName(HttpClient client, string artemisUrl)
    {
        try
        {
            var response = await client.GetAsync($"{artemisUrl}/console/jolokia/search/org.apache.activemq.artemis:broker=*");
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);

            if (!doc.RootElement.TryGetProperty("value", out var value))
            {
                return null;
            }

            foreach (var item in value.EnumerateArray())
            {
                var mbean = item.GetString();
                if (mbean is null)
                {
                    continue;
                }

                var match = Regex.Match(mbean, @"broker=""([^""]+)""");
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }
        }
        catch
        {
            // fallback silencieux
        }

        return null;
    }

    private static async Task<string?> FindQueueMbean(HttpClient client, string artemisUrl, string brokerName, string queueName)
    {
        // Essayer plusieurs patterns du plus ciblé au plus large
        var patterns = new[]
        {
            $"org.apache.activemq.artemis:broker=\"{brokerName}\",component=addresses,subcomponent=queues,*",
            $"org.apache.activemq.artemis:broker=\"{brokerName}\",*",
            "org.apache.activemq.artemis:*"
        };

        foreach (var pattern in patterns)
        {
            try
            {
                var response = await client.GetAsync($"{artemisUrl}/console/jolokia/search/{pattern}");
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                var content = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(content);

                if (!doc.RootElement.TryGetProperty("value", out var value))
                {
                    continue;
                }

                var mbeans = value.EnumerateArray()
                                  .Select(item => item.GetString())
                                  .Where(m => m is not null)
                                  .ToList();

                if (mbeans.Count == 0)
                {
                    continue;
                }

                var target = $"queue=\"{queueName}\"";
                var found = mbeans.FirstOrDefault(mbean => mbean!.Contains(target, StringComparison.OrdinalIgnoreCase));
                if (found is not null)
                {
                    return found;
                }

                // Diagnostic : affiche un échantillon si aucun match
                Console.WriteLine($"  [DEBUG] pattern={pattern} → {mbeans.Count} mbeans, aucun match pour queue=\"{queueName}\"");
                if (mbeans.Count <= 5)
                {
                    foreach (var m in mbeans)
                    {
                        Console.WriteLine($"    {m}");
                    }
                }
                else
                {
                    foreach (var m in mbeans.Where(m => m!.Contains("queue=", StringComparison.OrdinalIgnoreCase)).Take(5))
                    {
                        Console.WriteLine($"    {m}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [DEBUG] Erreur search ({pattern}) : {ex.Message}");
            }
        }

        return null;
    }

    private static async Task PurgeQueue(HttpClient client, string artemisUrl, string brokerName, string queueName)
    {
        var mbean = await FindQueueMbean(client, artemisUrl, brokerName, queueName);
        if (mbean is null)
        {
            WriteError($"  [ERREUR] MBean introuvable pour la queue : {queueName}");
            return;
        }

        var messageCount = await GetMessageCount(client, artemisUrl, mbean);
        var countLabel = messageCount.HasValue ? $"{messageCount.Value} message(s)" : "? message(s)";

        var payload = JsonSerializer.Serialize(new { type = "exec", mbean, operation = "removeAllMessages()" });
        var response = await client.PostAsync($"{artemisUrl}/console/jolokia/",
                                              new StringContent(payload, Encoding.UTF8, "application/json"));

        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            WriteError($"  [ERREUR] HTTP {(int)response.StatusCode} pour {queueName}");
            return;
        }

        using var doc = JsonDocument.Parse(content);
        var status = doc.RootElement.TryGetProperty("status", out var s) ? s.GetInt32() : 0;

        if (status == 200)
        {
            WriteColored(ConsoleColor.Green, $"  [OK] {queueName} purgée ({countLabel})");
            return;
        }

        var error = doc.RootElement.TryGetProperty("error", out var e) ? e.GetString() : null;
        WriteError($"  [ERREUR] Jolokia status={status} pour {queueName}" + (error is not null ? $" — {error}" : string.Empty));
    }

    private static async Task<long?> GetMessageCount(HttpClient client, string artemisUrl, string mbean)
    {
        try
        {
            var response = await client.GetAsync($"{artemisUrl}/console/jolokia/read/{mbean}/MessageCount");
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);

            return doc.RootElement.TryGetProperty("value", out var value) && value.TryGetInt64(out var count)
                ? count
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static void DisplayHeader(string title)
    {
        const int totalWidth = 100;
        var paddingWidth = (totalWidth - title.Length) / 2;
        var padding = new string(' ', paddingWidth);
        var border = new string('═', totalWidth);

        WriteColored(ConsoleColor.Green, $"╔{border}╗");
        WriteColored(ConsoleColor.Green, title.Length % 2 != 0
                         ? $"║{padding} {title}{padding}║"
                         : $"║{padding}{title}{padding}║");
        WriteColored(ConsoleColor.Green, $"╚{border}╝\n");
    }

    private static void WriteColored(ConsoleColor color, string text)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ResetColor();
    }

    private static void WriteError(string message) => WriteColored(ConsoleColor.Red, message);
}