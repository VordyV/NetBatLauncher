using System.Text.Json.Serialization;

namespace NBLAPI;

public class Game
{
    [JsonPropertyName("ident")]
    public string Id { get; set; }
    [JsonPropertyName("name")]
    public string Name { get; set; }
    [JsonPropertyName("short_name")]
    public string ShortName { get; set; }
    [JsonPropertyName("files")]
    public bool HasFiles { get; set; }
    [JsonPropertyName("default_mod_id")]
    public string DefaultModId { get; set; }
    [JsonPropertyName("reference_client_id")]
    public string ReferenceClientId { get; set; }
}