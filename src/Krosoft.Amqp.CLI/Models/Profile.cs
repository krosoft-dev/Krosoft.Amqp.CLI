using System.Text.Json.Serialization;

namespace Krosoft.Amqp.CLI.Models;

internal record PortainerProfile(
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("username")] string? Username,
    [property: JsonPropertyName("password")] string? Password,
    [property: JsonPropertyName("apiKey")] string? ApiKey,
    [property: JsonPropertyName("endpointId")] int EndpointId
);

internal record AmqpProfile(
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("password")] string Password,
    [property: JsonPropertyName("brokerName")] string BrokerName,
    [property: JsonPropertyName("queues")] List<string> Queues,
    [property: JsonPropertyName("amqpUrl")] string? AmqpUrl = null
);

internal record Profile(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("portainer")] PortainerProfile Portainer,
    [property: JsonPropertyName("amqp")] AmqpProfile Amqp,
    [property: JsonPropertyName("containers")] List<string> Containers
);
