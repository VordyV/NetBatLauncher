using System.Text.Json.Serialization;

namespace NBLNet;

public class Games
{
    [JsonPropertyName("games")]
    public Game[] List { get; set; }
}