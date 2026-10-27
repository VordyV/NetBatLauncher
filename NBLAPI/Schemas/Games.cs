using System.Text.Json.Serialization;

namespace NBLAPI;

public class Games
{
    [JsonPropertyName("games")]
    public Game[] List { get; set; }
}