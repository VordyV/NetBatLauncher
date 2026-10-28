using System.Net;
using System.Text.Json;

namespace NBLNet;

public class Api
{
    public string ServerAddress { get; private set; }

    private HttpClient _httpClient;

    public Api(string serverAddress)
    {
        this.ServerAddress = serverAddress;
        this._httpClient = new HttpClient();
    }

    protected string Combine(string path)
    {
        return this.ServerAddress + path;
    }
    
    protected async Task<string> Request(HttpMethod method, string path)
    {
        using HttpRequestMessage request = new HttpRequestMessage(method, this.Combine(path));
        using HttpResponseMessage response = await this._httpClient.SendAsync(request);
        
        if (response.StatusCode == HttpStatusCode.NotFound) throw new NotFountException(); // 404
        
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<ServerStatus> GetStatus()
    {
        string rowData = await this.Request(HttpMethod.Get, "/api/heartbeat");
        ushort code = ushort.Parse(rowData);
        return (ServerStatus)code;
    }

    public async Task<Game[]> GetAllGames()
    {
        string rowData = await this.Request(HttpMethod.Get, "/api/games");
        List<Game> games = new();
        
        using JsonDocument doc = JsonDocument.Parse(rowData);
        JsonElement root = doc.RootElement;
        
        foreach (JsonElement item in root.GetProperty("games").EnumerateArray())
        {
            games.Add(JsonSerializer.Deserialize<Game>(item)!);
        }
        
        return games.ToArray();
    }
    
    public async Task<GameClient[]> GetAllGameClients(string gameId)
    {
        string rowData = await this.Request(HttpMethod.Get, $"/api/games/clients?gameid={gameId}");
        List<GameClient> gameClients = new();
        
        using JsonDocument doc = JsonDocument.Parse(rowData);
        JsonElement root = doc.RootElement;
        
        foreach (JsonElement item in root.GetProperty("clients").EnumerateArray())
        {
            gameClients.Add(JsonSerializer.Deserialize<GameClient>(item)!);
        }
        
        return gameClients.ToArray();
    }
    
    public async Task<GameMod[]> GetAllGameMods(string gameId)
    {
        string rowData = await this.Request(HttpMethod.Get, $"/api/games/mods?gameid={gameId}");
        List<GameMod> gameMods = new();
        
        using JsonDocument doc = JsonDocument.Parse(rowData);
        JsonElement root = doc.RootElement;
        
        foreach (JsonElement item in root.GetProperty("mods").EnumerateArray())
        {
            gameMods.Add(JsonSerializer.Deserialize<GameMod>(item)!);
        }
        
        return gameMods.ToArray();
    }
    
    public async Task<GameServer[]> GetAllGameServers(string gameId)
    {
        string rowData = await this.Request(HttpMethod.Get, $"/api/games/servers?gameid={gameId}");
        List<GameServer> gameServers = new();
        
        using JsonDocument doc = JsonDocument.Parse(rowData);
        JsonElement root = doc.RootElement;
        
        foreach (JsonElement item in root.GetProperty("servers").EnumerateArray())
        {
            gameServers.Add(JsonSerializer.Deserialize<GameServer>(item)!);
        }
        
        return gameServers.ToArray();
    }
    
    public async Task<GameServer[]> GetAllGameServersForClient(string gameId, string clientId)
    {
        string rowData = await this.Request(HttpMethod.Get, $"/api/games/servers?gameid={gameId}&clientid={clientId}");
        List<GameServer> gameServers = new();
        
        using JsonDocument doc = JsonDocument.Parse(rowData);
        JsonElement root = doc.RootElement;
        
        foreach (JsonElement item in root.GetProperty("servers").EnumerateArray())
        {
            gameServers.Add(JsonSerializer.Deserialize<GameServer>(item)!);
        }
        
        return gameServers.ToArray();
    }

    public async Task<string?> GetGameDisclaimer(string gameId, string? langCode = null)
    {
        string rowData = await this.Request(HttpMethod.Get, $"/api/games/disclaimers?gameid={gameId}" + (langCode == null ? "" : $"&langcode={langCode}"));
        
        using JsonDocument doc = JsonDocument.Parse(rowData);
        JsonElement root = doc.RootElement;
        
        return root.GetProperty("text").GetString();
    }

    public async Task<Game> GetGame(string gameId)
    {
        string rowData = await this.Request(HttpMethod.Get, $"/api/game?gameid={gameId}");
        return JsonSerializer.Deserialize<Game>(rowData)!;
    }
    
    public async Task<GameClient> GetGameClient(string gameId, string clientId)
    {
        string rowData = await this.Request(HttpMethod.Get, $"/api/games/client?gameid={gameId}&clientid={clientId}");
        return JsonSerializer.Deserialize<GameClient>(rowData)!;
    }
    
    public async Task<GameMod> GetGameMod(string gameId, string modId)
    {
        string rowData = await this.Request(HttpMethod.Get, $"/api/games/mod?gameid={gameId}&modid={modId}");
        return JsonSerializer.Deserialize<GameMod>(rowData)!;
    }
    
    public async Task<int> GetNumberPlayers(string gameId, string clientId)
    {
        string rowData = await this.Request(HttpMethod.Get, $"/api/games/servers/numplayers?gameid={gameId}&clientid={clientId}");
        return int.Parse(rowData);
    }
    
    public async Task<int> GetAllNumberPlayers()
    {
        string rowData = await this.Request(HttpMethod.Get, $"/api/games/servers/numplayers/all");
        return int.Parse(rowData);
    }
}