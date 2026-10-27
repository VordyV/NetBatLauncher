using System.Text.Json.Serialization;

namespace NBLAPI;

public class GameClient
{
    [JsonPropertyName("ident")]
    public string Id { get; set; }
    [JsonPropertyName("name")]
    public string Name { get; set; }
    [JsonPropertyName("short_name")]
    public string ShortName { get; set; }
    [JsonPropertyName("files")]
    public bool HasFiles { get; set; }
    [JsonPropertyName("reference")]
    public bool HasReference { get; set; }
    [JsonPropertyName("address")]
    public string MasterServerAddress { get; set; }
}