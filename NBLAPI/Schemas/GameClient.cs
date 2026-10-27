using System.Diagnostics;
using System.Net;
using System.Text.Json.Serialization;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;

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

    public async Task<long> DetermineDelay()
    {
        var p = Process.Start(new ProcessStartInfo("ping.exe", $"-n 1 {this.MasterServerAddress}") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true })!;

        string output = p.StandardOutput.ReadToEnd();
        p.WaitForExit();

        var m = Regex.Match(output, @"(?:время|time)[=<]\s*(\d+)\s*(?:мс|ms)", RegexOptions.IgnoreCase);

        return m.Success ? int.Parse(m.Groups[1].Value) : -1;
    }
}