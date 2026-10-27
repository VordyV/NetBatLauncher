using System.Text.Json.Serialization;

namespace NBLAPI;

public class File
{
    [JsonPropertyName("checksum_sha256")]
    public string Checksum { get; set; }
    [JsonPropertyName("url")]
    public string Url { get; set; }
    [JsonPropertyName("path")]
    public string Path { get; set; }
    [JsonPropertyName("size")]
    public int Size { get; set; }
}