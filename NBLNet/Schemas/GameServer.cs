using System.Text.Json.Serialization;
using OpenGSQ.Protocols;
using OpenGSQ.Responses.GameSpy2;

namespace NBLNet;

public class GameServer
{
    [JsonPropertyName("address")]
    public string Address { get; set; }
    [JsonPropertyName("query_port")]
    public ushort QueryPort { get; set; }
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    public Status? Status;

    public async Task LoadData(int timeout = 3000)
    {
        GameSpy3 gs3 = new GameSpy3(this.Address, this.QueryPort, timeout: timeout);
        this.Status = await gs3.GetStatus();
    }
}