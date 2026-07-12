using System.Text.Json.Serialization;

namespace Krosoft.Amqp.CLI.Models;

internal record PortainerProfile(
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("username")]
    string? Username,
    [property: JsonPropertyName("password")]
    string? Password,
    [property: JsonPropertyName("apiKey")] string? ApiKey,
    [property: JsonPropertyName("endpointId")]
    int EndpointId);