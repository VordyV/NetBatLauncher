using System.Text.Json.Serialization;

namespace NBLAPI;

public class GameServer
{
    [JsonPropertyName("address")]
    public string Address { get; set; }
    [JsonPropertyName("query_port")]
    public ushort QueryPort { get; set; }
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}