using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace NBL;
public class Launcher
{
    public static string[] InvalidClientFileExtensions { get; } =
        new[] { ".lnk" };
    public static string ClientId { get; } =
        "Unknown";
    public static string ClientName { get; } =
        "Unknown";
    public static string ClientsDir { get; } =
        ".clients";
    public static string ClientManifestFilename { get; } =
        ".manifest";
    public static string StateSnapshotFilename { get; } =
        ".statesnapshot";
    public Configurator Registry { get; }
    protected Dictionary<string, Game> Games;
    
    public Launcher(string pathConfig)
    {
        this.Games = new();
        this.Registry = new Configurator(pathConfig);
    }
    public void RegisterGame(
        string gameId,
        string name,
        string shortName,
        string[] determinants,
        ILaunchParam[] launchParams)
    {
        if (!this.Games.TryAdd(
                gameId,
                new Game(
                    launcher: this,
                    registry: this.Registry,
                    id: gameId,
                    name: name,
                    shortName: shortName,
                    determinants: determinants,
                    launchParams: launchParams)))
        {
            throw new GameAlreadyRegException(
                $"Игра с этим ID '{gameId}' уже зарегистрирована");
        }
    }
    public Game GetGame(string gameId)
    {
        Game? game;
        if (!this.Games.TryGetValue(
                gameId,
                out game))
        {
            throw new GameNotFoundException(
                $"Игра с этим ID '{gameId}' не зарегистрирована");
        }
        return game;
    }
    public List<string> GetGames() =>
        this.Games.Keys.ToList();
    public void AddGameRegistry(
        string gameId,
        string path)
    {
        Game game =
            this.GetGame(gameId);
        if (game.IsInstall)
        {
            throw new GameInstallException(
                $"Игра '{gameId}' уже зарегистрирована в реестре");
        }
        this.Registry.AddSection(gameId);
        this.Registry.Set(
            gameId,
            "path",
            path);
    }
    public async Task<ClientData[]> GetClients(
        string gameId)
    {
        Game game =
            this.GetGame(gameId);
        string? path =
            game.GetPath();
        if (path == null ||
            !Directory.Exists(
                Path.Combine(
                    path,
                    Launcher.ClientsDir)))
        {
            return Array.Empty<ClientData>();
        }
        List<ClientData> clients =
            new();
        string data;
        ClientData? client;
        foreach (var dir in Directory.GetDirectories(
                     Path.Combine(
                         path,
                         Launcher.ClientsDir)))
        {
            string manifest =
                Path.Combine(
                    dir,
                    Launcher.ClientManifestFilename);
            if (!File.Exists(manifest))
                continue;
            data =
                await File.ReadAllTextAsync(
                    manifest);
            try
            {
                client =
                    JsonSerializer.Deserialize<ClientData>(
                        data);
                if (client == null)
                    throw new Exception();
                clients.Add(client);
            }
            catch
            {
                Console.WriteLine(
                    $"Не удалось прочитать манифест клиента '{Path.GetDirectoryName(dir)}'. Файл поврежден.");
            }
        }
        return clients.ToArray();
    }
    public async Task SetStateSnapshot(
        string gameId,
        StateSnapshot state)
    {
        Game game =
            this.GetGame(gameId);
        string path =
            game.GetPath();
        string data =
            JsonSerializer.Serialize(state);
        await File.WriteAllTextAsync(
            Path.Combine(
                path,
                Launcher.StateSnapshotFilename),
            data);
    }
    public async Task<StateSnapshot?> GetStateSnapshot(
        string gameId)
    {
        Game game =
            this.GetGame(gameId);
        string path =
            game.GetPath();
        string snapshotPath =
            Path.Combine(
                path,
                Launcher.StateSnapshotFilename);
        if (!File.Exists(snapshotPath))
            return null;
        string data =
            await File.ReadAllTextAsync(
                snapshotPath);
        return JsonSerializer.Deserialize<StateSnapshot>(
            data);
    }
    public async Task ChangeClientGame(
        string gameId,
        string clientId)
    {
        Game game =
            this.GetGame(gameId);
        string path =
            game.GetPath();
        StateSnapshot? currentState =
            await this.GetStateSnapshot(
                gameId);
        if (currentState == null)
            return;
        ClientData newClient =
            game.Clients[clientId];
        string refClientId =
            game.GetReferenceClient();
        ClientData refClient =
            game.Clients[refClientId];
        StateSnapshot newState =
            new StateSnapshot
            {
                Files = currentState.Files
            };
        string clientsDir =
            Path.Combine(
                path,
                Launcher.ClientsDir);
        string newClientDir =
            Path.Combine(
                clientsDir,
                newClient.ID);
        string refClientDir =
            Path.Combine(
                clientsDir,
                refClientId);
        foreach (var file in currentState.Files)
        {
            if (newClient.Files.Contains(file.Key) &&
                file.Value != newClient.ID)
            {
                File.Copy(
                    Path.Combine(
                        newClientDir,
                        file.Key),
                    Path.Combine(
                        path,
                        file.Key),
                    overwrite: true);
                newState.Files[file.Key] =
                    newClient.ID;
            }
            if (!newClient.Files.Contains(file.Key) &&
                refClient.Files.Contains(file.Key) &&
                file.Value != refClientId)
            {
                File.Copy(
                    Path.Combine(
                        refClientDir,
                        file.Key),
                    Path.Combine(
                        path,
                        file.Key),
                    overwrite: true);
                newState.Files[file.Key] =
                    refClientId;
            }
            if (!newClient.Files.Contains(file.Key) &&
                !refClient.Files.Contains(file.Key))
            {
                File.Delete(
                    Path.Combine(
                        path,
                        file.Key));
                newState.Files.Remove(
                    file.Key);
            }
        }
        foreach (var file in newClient.Files)
        {
            if (!currentState.Files.ContainsKey(file))
            {
                File.Copy(
                    Path.Combine(
                        newClientDir,
                        file),
                    Path.Combine(
                        path,
                        file),
                    overwrite: true);
                newState.Files[file] =
                    newClient.ID;
            }
        }
        await game.SetStateSnapshot(
            newState);
    }
}