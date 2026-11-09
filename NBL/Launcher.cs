using NBL.Services;
using System.Net.Http.Json;
using System.Text.Json;
using NBLNet;
using File = System.IO.File;

namespace NBL;

public class Launcher
{
    public static string[] InvalidClientFileExtensions { get; } = [".lnk"];
    public static string ClientId { get; } = "original";
    public static string ClientName { get; } = "Original";
    public static string ClientsDir { get; } = ".clients";
    public static string ClientManifestFilename { get; } = ".manifest";
    public static string StateSnapshotFilename { get; } = ".statesnapshot";
    private static readonly HttpClient Api = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public Configurator Registry { get; }
    public string ServerAddress { get; }
    public Api Server { get; }
    protected Dictionary<string, Game> Games;

    public Launcher(string pathConfig, string serverAddress)
    {
        Games = new();
        Registry = new Configurator(pathConfig);
        ServerAddress = serverAddress;
        Server = new Api(serverAddress: ServerAddress);
        Logger.Info($"Launcher created: config='{pathConfig}'");
    }

    public void RegisterGame(string gameId, string name, string shortName, string[] determinants, ILaunchParam[] launchParams)
    {
        Logger.Info($"Registering game: id='{gameId}', name='{name}', shortName='{shortName}'");
        if (!Games.TryAdd(gameId, new Game(this, Registry, gameId, name, shortName, determinants, launchParams)))
        {
            Logger.Error($"Game registration failed: game '{gameId}' is already registered");
            throw new GameAlreadyRegException($"Игра с этим ID '{gameId}' уже зарегистрирована");
        }
        Logger.Info($"Game registered successfully: '{gameId}'");
    }

    public Game GetGame(string gameId)
    {
        if (!Games.TryGetValue(gameId, out Game? game))
            throw new GameNotFoundException($"Игра с этим ID '{gameId}' не зарегистрирована");
        return game;
    }

    public List<string> GetGames() => Games.Keys.ToList();

    public void AddGameRegistry(string gameId, string path)
    {
        Logger.Info($"Adding game to registry: game='{gameId}', path='{path}'");
        Game game = GetGame(gameId);
        if (game.IsInstall)
        {
            Logger.Warning($"Game already registered: '{gameId}'");
            throw new GameInstallException($"Игра '{gameId}' уже зарегистрирована в реестре");
        }
        Registry.AddSection(gameId);
        Registry.Set(gameId, "path", path);
        Logger.Info($"Game registry added successfully: game='{gameId}', path='{path}'");
    }

    public async Task<ApiClient[]> GetAvailableClientsAsync(string gameId)
    {
        string url = $"{ServerAddress.TrimEnd('/')}/api/games/clients?gameid={Uri.EscapeDataString(gameId)}";
        var data = await Api.GetFromJsonAsync<ClientsResponse>(url, JsonOptions);
        return data?.Clients.ToArray() ?? [];
    }

    public async Task<ApiServer[]> GetServersAsync(string gameId, string clientId)
    {
        string url = $"{ServerAddress.TrimEnd('/')}/api/games/servers?clientid={Uri.EscapeDataString(clientId)}&gameid={Uri.EscapeDataString(gameId)}";
        var data = await Api.GetFromJsonAsync<ServersResponse>(url, JsonOptions);
        return data?.Servers.ToArray() ?? [];
    }

    public async Task<ClientData[]> GetClients(string gameId)
    {
        Game game = GetGame(gameId);
        string? path = game.GetPath();
        string clientsPath = path == null ? "" : Path.Combine(path, ClientsDir);
        if (path == null || !Directory.Exists(clientsPath))
            return [];

        List<ClientData> clients = new();
        foreach (var dir in Directory.GetDirectories(clientsPath))
        {
            string manifest = Path.Combine(dir, ClientManifestFilename);
            if (!File.Exists(manifest)) continue;
            try
            {
                string data = await File.ReadAllTextAsync(manifest);
                ClientData? client = JsonSerializer.Deserialize<ClientData>(data, JsonOptions);
                if (client != null)
                {
                    Logger.Info($"Client manifest loaded: id='{client.ID}', name='{client.Name}', files={client.Files.Length}, path='{manifest}'");
                    clients.Add(client);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to read client manifest: '{manifest}'", ex);
            }
        }
        Logger.Info($"Loaded {clients.Count} clients for game '{gameId}'");
        return clients.ToArray();
    }

    public async Task SetStateSnapshot(string gameId, StateSnapshot state)
    {
        Game game = GetGame(gameId);
        string path = game.GetPath();
        string snapshotPath = Path.Combine(path, StateSnapshotFilename);
        await File.WriteAllTextAsync(snapshotPath, JsonSerializer.Serialize(state));
        Logger.Debug($"State snapshot saved: game='{gameId}', files={state.Files.Count}, path='{snapshotPath}'");
    }

    public async Task<StateSnapshot?> GetStateSnapshot(string gameId)
    {
        Game game = GetGame(gameId);
        string path = game.GetPath();
        string snapshotPath = Path.Combine(path, StateSnapshotFilename);
        if (!File.Exists(snapshotPath))
        {
            Logger.Debug($"State snapshot not found: game='{gameId}', path='{snapshotPath}'");
            return null;
        }
        string data = await File.ReadAllTextAsync(snapshotPath);
        StateSnapshot? state = JsonSerializer.Deserialize<StateSnapshot>(data, JsonOptions);
        Logger.Debug($"State snapshot loaded: game='{gameId}', files={state?.Files.Count ?? 0}");
        return state;
    }


    public async Task ChangeClientGame(string gameId, string clientId)
    {
        Game game = GetGame(gameId);
        string path = game.GetPath();

        try
        {
            await game.GetClients();
            
            if (!game.Clients.TryGetValue(clientId, out ClientData? target))
                throw new Exception($"Клиент '{clientId}' не найден");

            string referenceId = game.GetReferenceClient();
            if (!game.Clients.TryGetValue(referenceId, out ClientData? reference))
                throw new Exception($"Reference-клиент '{referenceId}' не найден");

            StateSnapshot? current = await GetStateSnapshot(gameId);
            if (current == null)
                throw new Exception("Состояние игры не найдено");

            string clientsDir = Path.Combine(path, ClientsDir);
            string targetDir = Path.Combine(clientsDir, target.ID);
            string referenceDir = Path.Combine(clientsDir, reference.ID);
            string backupDir = Path.Combine(path, ".backups", "switch");

            var next = new StateSnapshot { Files = new(current.Files) };
            var targetFiles = target.Files.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var referenceFiles = reference.Files.ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (string file in target.Files.Union(reference.Files, StringComparer.OrdinalIgnoreCase))
            {
                string relative = file.Replace('/', Path.DirectorySeparatorChar);
                string destination = Path.GetFullPath(Path.Combine(path, relative));

                if (!destination.StartsWith(Path.GetFullPath(path) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"Недопустимый путь: {file}");

                string sourceDir = targetFiles.Contains(file) ? targetDir : referenceDir;
                string source = Path.GetFullPath(Path.Combine(sourceDir, relative));

                if (!File.Exists(source))
                    throw new FileNotFoundException($"Файл клиента не найден: {file}", source);

                if (File.Exists(destination) && !next.Files.ContainsKey(file))
                {
                    string backup = Path.Combine(backupDir, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
                    File.Copy(destination, backup, true);
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(source, destination, true);
                next.Files[file] = targetFiles.Contains(file) ? target.ID : reference.ID;
            }

            foreach (string file in current.Files.Keys.Except(targetFiles, StringComparer.OrdinalIgnoreCase)
                         .Except(referenceFiles, StringComparer.OrdinalIgnoreCase).ToArray())
                next.Files.Remove(file);

            await SetStateSnapshot(gameId, next);
            Logger.Info($"Client switched: game='{gameId}', client='{target.ID}'");
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to change client: game='{gameId}', client='{clientId}'", ex);
            throw;
        }
    }
}
