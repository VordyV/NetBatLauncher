using System.Text.Json.Serialization;

namespace NBL.Services;

public class GameManifest
{
    [JsonPropertyName("ident")]
    public string Ident { get; set; } = string.Empty;

    [JsonPropertyName("files")]
    public List<GameManifestFile> Files { get; set; } = new();
}

public class GameManifestFile
{
    [JsonPropertyName("checksum_sha256")]
    public string ChecksumSha256 { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;
}