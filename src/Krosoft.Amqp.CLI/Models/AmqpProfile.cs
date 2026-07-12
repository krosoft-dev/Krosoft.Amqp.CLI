using System.Text.Json.Serialization;

namespace Krosoft.Amqp.CLI.Models;

internal record AmqpProfile(
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("username")]
    string Username,
    [property: JsonPropertyName("password")]
    string Password,
    [property: JsonPropertyName("brokerName")]
    string BrokerName,
    [property: JsonPropertyName("queues")] List<string> Queues,
    [property: JsonPropertyName("amqpUrl")]
    string? AmqpUrl = null);