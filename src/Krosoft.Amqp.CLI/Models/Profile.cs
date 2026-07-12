using System.Text.Json.Serialization;

namespace Krosoft.Amqp.CLI.Models;

internal record Profile(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("portainer")]
    PortainerProfile Portainer,
    [property: JsonPropertyName("amqp")] AmqpProfile Amqp,
    [property: JsonPropertyName("containers")]
    List<string> Containers);