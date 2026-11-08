
using NBL.Services;
using System.Diagnostics;
using System.Text.Json;
using static NBL.LaunchParamDict;

namespace NBL;

public class Game
{
    public string Id { get; }
    public string Name { get; }
    public string ShortName { get; }
    public GameStatus Status { get; protected set; } = GameStatus.NotInstalled;
    public bool IsInstall { get; protected set; }
    public string[] Determinants { get; }
    public string ReferenceClient { get; protected set; } = string.Empty;
    public string Client { get; protected set; } = string.Empty;
    public Dictionary<string, ClientData> Clients { get; protected set; } = new();
    public Dictionary<string, ILaunchParam> LaunchParams { get; protected set; } = new();

    public event Func<GameStatus, Task>? OnChangeStatus;

    protected Launcher Launcher;
    protected Configurator RegistryInternal;

    public Configurator Registry => RegistryInternal;

    public Configurator GetRegistry() => Registry;


    private Process? _gameProcess;

    public Game(Launcher launcher, Configurator registry, string id, string name,
        string shortName, string[] determinants, ILaunchParam[] launchParams)
    {
        Launcher = launcher;
        RegistryInternal = registry;
        Id = id;
        Name = name;
        ShortName = shortName;
        Determinants = determinants;

        foreach (var param in launchParams)
            LaunchParams.Add(param.Id, param);
    }

    public async Task<bool> CheckInstall()
    {
        Logger.Debug($"Checking installation: {Id}");

        await Launcher.Registry.WaitReadiness();

        if (HasGameInRegistry())
            InitializeLaunchParameters();

        IsInstall = HasGameInRegistry("path") && CheckPathInstall();

        Logger.Info($"Game installation check: {Id}, installed={IsInstall}");

        if (!IsInstall)
        {
            await SetStatus(GameStatus.NotInstalled);
            return false;
        }

        await SetStatus(IsGameRunning() ? GameStatus.Running : GameStatus.NotRunning);
        return true;
    }

    public bool CheckPathInstall()
    {
        if (!HasGameInRegistry("path"))
        {
            Logger.Warning($"Game path is not configured: {Id}");
            return false;
        }

        string path = Registry.Get<string>(Id, "path");
        string exePath = Path.Combine(path, "BF2142.exe");

        bool directoryExists = Directory.Exists(path);
        bool executableExists = File.Exists(exePath);

        Logger.Debug($"Game path check: path='{path}', directoryExists={directoryExists}, exeExists={executableExists}");

        return directoryExists && executableExists;
    }

    public void AddGameRegistry(string path) => Launcher.AddGameRegistry(Id, path);

    protected async Task SetStatus(GameStatus status)
    {
        Status = status;
        if (OnChangeStatus != null)
            await OnChangeStatus(status);
    }

    public bool IdentifyGame(string path)
    {
        if (!Directory.Exists(path))
            return false;

        foreach (string filename in Determinants)
            if (!File.Exists(Path.Combine(path, filename)))
                return false;

        return true;
    }

    public async Task<ClientData> GenerateDefaultClient()
    {
        string gamePath = GetPath();
        string clientPath = Path.Combine(gamePath, Launcher.ClientsDir, Launcher.ClientId);
        string manifestPath = Path.Combine(clientPath, Launcher.ClientManifestFilename);

        if (File.Exists(manifestPath))
            return JsonSerializer.Deserialize<ClientData>(await File.ReadAllTextAsync(manifestPath))!;

        Directory.CreateDirectory(clientPath);
        List<string> files = new();

        foreach (string file in Directory.GetFiles(gamePath))
        {
            string name = Path.GetFileName(file);
            if (Launcher.InvalidClientFileExtensions.Contains(Path.GetExtension(file)))
                continue;

            File.Copy(file, Path.Combine(clientPath, name), true);
            files.Add(name);
        }

        ClientData client = new()
        {
            ID = Launcher.ClientId,
            Name = Launcher.ClientName,
            Files = files.ToArray()
        };

        await File.WriteAllTextAsync(manifestPath,
            JsonSerializer.Serialize(client, new JsonSerializerOptions { WriteIndented = true }));

        return client;
    }

    protected bool HasGameInRegistry(string? option = null)
    {
        if (option == null)
            return Registry.HasSection(Id);

        return Registry.HasSection(Id) && Registry.HasOption(Id, option);
    }

    protected void CheckGameInRegistry()
    {
        if (!HasGameInRegistry())
            throw new GameNotSetRegistryException($"Игра '{Id}' не была добавлена в реестр лаунчера");
    }

    public string GetPath()
    {
        if (!HasGameInRegistry("path"))
        {
            Logger.Warning($"Game path is not configured: {Id}");
            throw new GamePathNotSetException($"Игра '{Id}' путь не установлен");
        }

        string path = Registry.Get<string>(Id, "path");

        if (!Directory.Exists(path))
        {
            Logger.Error($"Game path does not exist: '{path}'");
            throw new GamePathNotFoundException($"Путь '{path}' игры '{Id}' не найден");
        }

        return path;
    }

    public bool TryGetPath(out string? data)
    {
        data = null;

        if (!HasGameInRegistry("path"))
            return false;

        string path = Registry.Get<string>(Id, "path");

        if (!Directory.Exists(path))
            return false;

        data = path;
        return true;
    }

    public void SetReferenceClient(string clientId)
    {
        CheckGameInRegistry();
        Registry.Set(Id, "referenceClient", clientId);
        ReferenceClient = clientId;
    }

    public string GetReferenceClient()
    {
        if (!HasGameInRegistry("referenceClient"))
            throw new GameNotSetRegistryException($"Ссылка на игру '{Id}' не установлена");

        ReferenceClient = Registry.Get<string>(Id, "referenceClient");
        return ReferenceClient;
    }

    public void SetCurrentClient(string clientId)
    {
        CheckGameInRegistry();
        Registry.Set(Id, "client", clientId);
        Client = clientId;
    }

    public string GetCurrentClient()
    {
        if (!HasGameInRegistry("client"))
            throw new GameNotSetRegistryException($"Клиент игры '{Id}' не установлен");

        Client = Registry.Get<string>(Id, "client");
        return Client;
    }

    public async Task<ClientData[]> GetClients()
    {
        ClientData[] clients = await Launcher.GetClients(Id);
        Clients.Clear();

        foreach (var client in clients)
            Clients.Add(client.ID, client);

        return clients;
    }

    public async Task ChangeClientGame(string clientId) =>
        await Launcher.ChangeClientGame(Id, clientId);

    public async Task SetStateSnapshot(StateSnapshot state) =>
        await Launcher.SetStateSnapshot(Id, state);

    public async Task<StateSnapshot?> GetStateSnapshot() =>
        await Launcher.GetStateSnapshot(Id);

    public string[] BuildLaunchParams(Dictionary<string, object> parameters)
    {
        List<string> args = new();

        foreach (var param in parameters)
        {
            if (!LaunchParams.TryGetValue(param.Key, out ILaunchParam? launchParam))
                throw new LaunchParamNotFoundException($"Параметр запуска '{param.Key}' не зарегистрирован");

            IEnumerable<string> builtArgs = launchParam switch
            {
                LaunchParamStr p => p.BuildArgs((string)param.Value),
                LaunchParamBool p => p.BuildArgs((bool)param.Value),
                LaunchParamDict p => p.BuildArgs((string)param.Value),
                LaunchOptionDict p => p.BuildArgs((string[])param.Value),
                LaunchParamCustom p => p.BuildArgs((string)param.Value),
                _ => throw new InvalidOperationException($"Unknown launch parameter type: {launchParam.GetType().Name}")
            };

            args.AddRange(builtArgs);
        }

        Logger.Debug($"Built launch arguments for '{Id}': {(args.Count > 0 ? string.Join(" ", args) : "none")}");
        return args.ToArray();
    }

    public void SetLaunchParameters(Dictionary<string, object> parameters)
    {
        BuildLaunchParams(parameters);
        CheckGameInRegistry();

        foreach (var parameter in parameters)
            Registry.Set(Id, $"launch.{parameter.Key}", parameter.Value);
    }

    public void InitializeLaunchParameters()
    {
        if (!HasGameInRegistry("launch.widescreen"))
        {
            Registry.Set(Id, "launch.widescreen", true);
            Logger.Info($"Default launch parameter initialized: {Id}.launch.widescreen=true");
        }
    }

    public Dictionary<string, object> GetLaunchParameters()
    {
        Dictionary<string, object> parameters = new();

        foreach (var param in LaunchParams.Values)
        {
            string key = $"launch.{param.Id}";
            bool exists = HasGameInRegistry(key);

            if (param is LaunchParamBool)
            {
                bool defaultValue = param.Id == "widescreen";
                parameters[param.Id] = exists ? Registry.Get(Id, key, defaultValue) : defaultValue;
            }
            else if (param is LaunchParamStr or LaunchParamCustom)
            {
                parameters[param.Id] = exists ? Registry.Get(Id, key, "") : "";
            }
            else if (param is LaunchParamDict dict)
            {
                string defaultValue = dict.Dictionary.Keys.FirstOrDefault() ?? "";
                string value = exists ? Registry.Get(Id, key, defaultValue) : defaultValue;
                parameters[param.Id] = dict.Dictionary.ContainsKey(value) ? value : defaultValue;
            }
            else if (param is LaunchOptionDict options)
            {
                string[] value = exists ? Registry.Get(Id, key, Array.Empty<string>()) : Array.Empty<string>();
                parameters[param.Id] = value.Where(options.Dictionary.ContainsKey).ToArray();
            }
        }

        Logger.Debug($"Loaded launch parameters for '{Id}': " +
            string.Join(", ", parameters.Select(x => $"{x.Key}={FormatLaunchParameterValue(x.Value)}")));

        return parameters;
    }

    private static string FormatLaunchParameterValue(object? value) =>
        value is string[] array ? $"[{string.Join(", ", array)}]" : value?.ToString() ?? "null";

    public string[] BuildSavedLaunchParams() => BuildLaunchParams(GetLaunchParameters());

    public bool IsGameRunning()
    {
        if (_gameProcess == null)
            return false;

        try
        {
            if (!_gameProcess.HasExited)
                return true;

            _gameProcess.Dispose();
            _gameProcess = null;
            return false;
        }
        catch
        {
            _gameProcess = null;
            return false;
        }
    }
    public async Task Launch()
    {
        Logger.Info($"Launch requested: {Id}");

        if (!IsInstall)
        {
            Logger.Warning($"Launch cancelled: game is not installed: {Id}");
            throw new GameInstallException("Игра не установлена");
        }

        if (IsGameRunning())
        {
            Logger.Info($"Launch cancelled: game is already running: {Id}");
            return;
        }

        string gamePath = GetPath();
        string exePath = Path.Combine(gamePath, "BF2142.exe");

        Logger.Info($"Game executable: '{exePath}'");

        if (!File.Exists(exePath))
        {
            Logger.Error($"Game executable not found: '{exePath}'");
            throw new FileNotFoundException("BF2142.exe не найден", exePath);
        }

        string[] args = BuildSavedLaunchParams();

        Logger.Info($"Launch parameters: {(args.Length > 0 ? string.Join(" ", args) : "none")}");

        ProcessStartInfo startInfo = new()
        {
            FileName = exePath,
            WorkingDirectory = gamePath,
            UseShellExecute = false
        };

        foreach (string arg in args)
            startInfo.ArgumentList.Add(arg);

        Logger.Debug($"Working directory: '{gamePath}'");

        _gameProcess = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

        _gameProcess.Exited += async (_, _) =>
        {
            Process? process = _gameProcess;
            _gameProcess = null;

            Logger.Info($"Game process exited: {Id}");
            process?.Dispose();

            await SetStatus(GameStatus.NotRunning);
        };

        try
        {
            Logger.Info($"Starting game process: {exePath}");

            if (!_gameProcess.Start())
            {
                Logger.Error($"Process.Start returned false: {exePath}");
                _gameProcess.Dispose();
                _gameProcess = null;
                throw new Exception("Не удалось запустить BF2142.exe");
            }

            Logger.Info($"Game process started successfully: {Id}, PID={_gameProcess.Id}");
            await SetStatus(GameStatus.Running);
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to start game: {exePath}", ex);
            _gameProcess?.Dispose();
            _gameProcess = null;
            throw;
        }
    }
    public async Task UninstallAsync()
    {
        string path = System.IO.Path.GetFullPath(GetPath()).TrimEnd('\\');
        string root = System.IO.Path.GetPathRoot(path)?.TrimEnd('\\') ?? "";
        string app = AppContext.BaseDirectory.TrimEnd('\\');

        if (string.Equals(path, root, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(path, app, StringComparison.OrdinalIgnoreCase) ||
            app.StartsWith(path + "\\", StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(System.IO.Path.Combine(path, "BF2142.exe")))
            throw new InvalidOperationException("Некорректный путь установки игры.");

        if (System.Diagnostics.Process.GetProcessesByName("BF2142").Length > 0)
            throw new InvalidOperationException("Сначала закройте игру.");

        Directory.Delete(path, true);

        if (Registry.HasSection(Id))
            Registry.RemoveSection(Id);

        await Registry.WaitSafeMoment();
        CheckInstall();
    }
}
