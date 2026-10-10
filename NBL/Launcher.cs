using NBL.Services;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace NBL;
public class Launcher
{
    public static string[] InvalidClientFileExtensions { get; } =
        new[] { ".lnk" };
    public static string ClientId { get; } =
        "respy";
    public static string ClientName { get; } =
        "Respy";
    public static string ClientsDir { get; } =
        ".clients";
    public static string ClientManifestFilename { get; } =
        ".manifest";
    public static string StateSnapshotFilename { get; } =
        ".statesnapshot";
    public Configurator Registry { get; }
    public string ServerAddress { get; }
    
    protected Dictionary<string, Game> Games;

    public Launcher(string pathConfig, string serverAddress)
    {
        this.Games = new();
        this.Registry = new Configurator(pathConfig);
        this.ServerAddress = serverAddress;

        Logger.Info(
            $"Launcher created: config='{pathConfig}'");
    }
    public void RegisterGame(
     string gameId,
     string name,
     string shortName,
     string[] determinants,
     ILaunchParam[] launchParams)
    {
        Logger.Info(
            $"Registering game: id='{gameId}', name='{name}', shortName='{shortName}'");

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
            Logger.Error(
                $"Game registration failed: game '{gameId}' is already registered");

            throw new GameAlreadyRegException(
                $"Игра с этим ID '{gameId}' уже зарегистрирована");
        }

        Logger.Info(
            $"Game registered successfully: '{gameId}'");
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
        Logger.Info(
            $"Adding game to registry: game='{gameId}', path='{path}'");

        Game game =
            this.GetGame(gameId);

        if (game.IsInstall)
        {
            Logger.Warning(
                $"Game already registered: '{gameId}'");

            throw new GameInstallException(
                $"Игра '{gameId}' уже зарегистрирована в реестре");
        }

        this.Registry.AddSection(gameId);

        this.Registry.Set(
            gameId,
            "path",
            path);

        Logger.Info(
            $"Game registry added successfully: game='{gameId}', path='{path}'");
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
            catch (Exception ex)
            {
                Logger.Error(
                    $"Failed to read client manifest: '{manifest}'",
                    ex);
            }
        }
        Logger.Info(
    $"Loaded {clients.Count} clients for game '{gameId}'");
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

        string snapshotPath =
            Path.Combine(
                path,
                Launcher.StateSnapshotFilename);

        string data =
            JsonSerializer.Serialize(state);

        await File.WriteAllTextAsync(
            snapshotPath,
            data);

        Logger.Debug(
            $"State snapshot saved: game='{gameId}', " +
            $"files={state.Files.Count}, path='{snapshotPath}'");
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
        {
            Logger.Debug(
                $"State snapshot not found: game='{gameId}', " +
                $"path='{snapshotPath}'");

            return null;
        }

        string data =
            await File.ReadAllTextAsync(
                snapshotPath);

        StateSnapshot? state =
            JsonSerializer.Deserialize<StateSnapshot>(
                data);

        Logger.Debug(
            $"State snapshot loaded: game='{gameId}', " +
            $"files={state?.Files.Count ?? 0}");

        return state;
    }
    public async Task ChangeClientGame(
        string gameId,
        string clientId)
    {
        Logger.Info(
            $"Changing game client: game='{gameId}', client='{clientId}'");

        try
        {
            Game game =
                this.GetGame(gameId);

            string path =
                game.GetPath();

            Logger.Debug(
                $"Game path: '{path}'");

            StateSnapshot? currentState =
                await this.GetStateSnapshot(
                    gameId);

            Logger.Debug(
    $"Looking for clients: game='{gameId}', " +
    $"directory='{Path.Combine(path, Launcher.ClientsDir)}'");

            if (currentState == null)
            {
                Logger.Warning(
                    $"No state snapshot found for game '{gameId}'");

                return;
            }


            Logger.Debug(
                $"Current state contains {currentState.Files.Count} files");

            if (!game.Clients.TryGetValue(
                    clientId,
                    out ClientData? newClient))
            {
                Logger.Error(
                    $"Client '{clientId}' was not found for game '{gameId}'");

                throw new Exception(
                    $"Клиент '{clientId}' не найден");
            }

            string refClientId =
                game.GetReferenceClient();

            if (!game.Clients.TryGetValue(
                    refClientId,
                    out ClientData? refClient))
            {
                Logger.Error(
                    $"Reference client '{refClientId}' was not found");

                throw new Exception(
                    $"Reference client '{refClientId}' не найден");
            }

            Logger.Info(
                $"Switching client: '{refClientId}' -> '{newClient.ID}'");

            StateSnapshot newState =
                new StateSnapshot
                {
                    Files =
                        currentState.Files
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

            Logger.Debug(
                $"New client directory: '{newClientDir}'");

            Logger.Debug(
                $"Reference client directory: '{refClientDir}'");

            foreach (var file in currentState.Files)
            {
                if (newClient.Files.Contains(file.Key) &&
                    file.Value != newClient.ID)
                {
                    string source =
                        Path.Combine(
                            newClientDir,
                            file.Key);

                    string destination =
                        Path.Combine(
                            path,
                            file.Key);

                    Logger.Debug(
                        $"Copying client file: '{file.Key}' " +
                        $"from '{newClient.ID}'");

                    File.Copy(
                        source,
                        destination,
                        overwrite: true);

                    newState.Files[file.Key] =
                        newClient.ID;
                }

                if (!newClient.Files.Contains(file.Key) &&
                    refClient.Files.Contains(file.Key) &&
                    file.Value != refClientId)
                {
                    string source =
                        Path.Combine(
                            refClientDir,
                            file.Key);

                    string destination =
                        Path.Combine(
                            path,
                            file.Key);

                    Logger.Debug(
                        $"Restoring reference file: '{file.Key}'");

                    File.Copy(
                        source,
                        destination,
                        overwrite: true);

                    newState.Files[file.Key] =
                        refClientId;
                }

                if (!newClient.Files.Contains(file.Key) &&
                    !refClient.Files.Contains(file.Key))
                {
                    string filePath =
                        Path.Combine(
                            path,
                            file.Key);

                    Logger.Debug(
                        $"Deleting unused client file: '{file.Key}'");

                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }

                    newState.Files.Remove(
                        file.Key);
                }
            }

            foreach (var file in newClient.Files)
            {
                if (!currentState.Files.ContainsKey(file))
                {
                    string source =
                        Path.Combine(
                            newClientDir,
                            file);

                    string destination =
                        Path.Combine(
                            path,
                            file);

                    Logger.Debug(
                        $"Adding new client file: '{file}'");

                    File.Copy(
                        source,
                        destination,
                        overwrite: true);

                    newState.Files[file] =
                        newClient.ID;
                }
            }

            await game.SetStateSnapshot(
                newState);

            Logger.Info(
                $"Client switched successfully: game='{gameId}', " +
                $"client='{newClient.ID}'");
        }
        catch (Exception ex)
        {
            Logger.Error(
                $"Failed to change client: game='{gameId}', client='{clientId}'",
                ex);

            throw;
        }
    }
}