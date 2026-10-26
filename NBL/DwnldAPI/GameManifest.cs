using System.Text.Json.Serialization;

namespace NBL.Services;

public class GameManifest
{
    [JsonPropertyName("ident")]
    public string Ident { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("files")]
    public List<GameManifestFile> Files { get; set; } = new();

    [JsonPropertyName("session_id")]
    public string? SessionId { get; set; }

}

public class GameManifestFile
{
    [JsonPropertyName("checksum_sha256")]
    public string ChecksumSha256 { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }
}
