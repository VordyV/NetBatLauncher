using System.Diagnostics;
using System.Net;
using System.Text.Json.Serialization;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;

namespace NBLNet;

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
        using Ping pingSender = new Ping();
        PingReply reply = await pingSender.SendPingAsync(this.MasterServerAddress, 5000);
        if (reply.Status != IPStatus.Success) return -1;
        return reply.RoundtripTime;
    }
}