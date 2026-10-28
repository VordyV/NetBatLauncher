using System.Text.Json.Serialization;

namespace NBLNet;

public class GameMod
{
    [JsonPropertyName("modid")]
    public string Id { get; set; }
    [JsonPropertyName("name")]
    public string Name { get; set; }
    [JsonPropertyName("version")]
    public string Version { get; set; }
    [JsonPropertyName("developer")]
    public string Developer { get; set; }
    [JsonPropertyName("files")]
    public bool HasFiles { get; set; }
    [JsonPropertyName("default")]
    public bool HasDefault { get; set; }
}