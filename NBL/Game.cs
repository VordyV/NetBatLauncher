using System.Diagnostics;
using System.Text.Json;
using static NBL.LaunchParamDict;
namespace NBL;
public class Game
{
    public string Id { get; }
    public string Name { get; }
    public string ShortName { get; }
    public GameStatus Status { get; protected set; } =
        GameStatus.NotInstalled;
    public bool IsInstall { get; protected set; }
    public string[] Determinants { get; }
    public string ReferenceClient { get; protected set; } =
        string.Empty;
    public string Client { get; protected set; } =
        string.Empty;
    public Dictionary<string, ClientData> Clients { get; protected set; } =
        new();
    public Dictionary<string, ILaunchParam> LaunchParams { get; protected set; } =
        new();
    public event Func<GameStatus, Task>? OnChangeStatus;
    protected Launcher Launcher;
    protected Configurator Registry;
    private Process? _gameProcess;
    public Game(
        Launcher launcher,
        Configurator registry,
        string id,
        string name,
        string shortName,
        string[] determinants,
        ILaunchParam[] launchParams)
    {
        this.Launcher = launcher;
        this.Registry = registry;
        this.Id = id;
        this.Name = name;
        this.ShortName = shortName;
        this.Determinants = determinants;
        foreach (var param in launchParams)
        {
            this.LaunchParams.Add(
                param.Id,
                param);
        }
    }
    public async Task<bool> CheckInstall()
    {
        await this.Launcher.Registry.WaitReadiness();
        bool isInstall =
            this.HasGameInRegistry("path") &&
            this.CheckPathInstall();
        this.IsInstall =
            isInstall;
        if (!this.IsInstall)
        {
            await this.SetStatus(
                GameStatus.NotInstalled);
            return false;
        }
        if (this.IsGameRunning())
        {
            await this.SetStatus(
                GameStatus.Running);
        }
        else
        {
            await this.SetStatus(
                GameStatus.NotRunning);
        }
        return true;
    }
    public bool CheckPathInstall()
    {
        if (!this.HasGameInRegistry("path"))
            return false;
        string path =
            this.Registry.Get<string>(
                this.Id,
                "path");
        if (!Directory.Exists(path))
            return false;
        string exePath =
            Path.Combine(
                path,
                "BF2142.exe");
        return File.Exists(exePath);
    }
    public void AddGameRegistry(string path)
    {
        this.Launcher.AddGameRegistry(
            this.Id,
            path);
    }
    protected async Task SetStatus(
        GameStatus status)
    {
        this.Status =
            status;
        if (this.OnChangeStatus != null)
        {
            await this.OnChangeStatus(
                status);
        }
    }
    public bool IdentifyGame(string path)
    {
        if (!Directory.Exists(path))
            return false;
        string pf = "";
        foreach (string filename in this.Determinants)
        {
            pf =
                Path.Combine(
                    path,
                    filename);
            if (!File.Exists(pf))
                return false;
        }
        return true;
    }
    public async Task<ClientData> GenerateDefaultClient()
    {
        string pathGame =
            this.GetPath();
        string pathDirClients =
            Path.Combine(
                pathGame,
                Launcher.ClientsDir);
        if (!Directory.Exists(pathDirClients))
        {
            Directory.CreateDirectory(
                pathDirClients);
        }
        string pathNewClient =
            Path.Combine(
                pathDirClients,
                Launcher.ClientId);
        if (Directory.Exists(pathNewClient))
        {
            Directory.Delete(
                pathNewClient,
                true);
        }
        Directory.CreateDirectory(
            pathNewClient);
        List<string> files =
            new();
        foreach (var file in Directory.GetFiles(pathGame))
        {
            string extension =
                Path.GetExtension(file);
            string fileName =
                Path.GetFileName(file);
            if (Launcher.InvalidClientFileExtensions.Contains(
                    extension))
            {
                continue;
            }
            files.Add(
                fileName);
            File.Copy(
                file,
                Path.Combine(
                    pathNewClient,
                    fileName));
        }
        ClientData client =
            new ClientData
            {
                ID =
                    Launcher.ClientId,
                Name =
                    Launcher.ClientName,
                Files =
                    files.ToArray()
            };
        string txtMF =
            JsonSerializer.Serialize(
                client,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });
        await File.WriteAllTextAsync(
            Path.Combine(
                pathNewClient,
                Launcher.ClientManifestFilename),
            txtMF);
        return client;
    }
    protected bool HasGameInRegistry(
        string? option = null)
    {
        if (option == null)
        {
            return this.Registry.HasSection(
                this.Id);
        }
        return
            this.Registry.HasSection(this.Id) &&
            this.Registry.HasOption(
                this.Id,
                option);
    }
    protected void CheckGameInRegistry()
    {
        if (!this.HasGameInRegistry())
        {
            throw new GameNotSetRegistryException(
                $"Игра '{this.Id}' не была добавлена в реестр лаунчера");
        }
    }
    public string GetPath()
    {
        if (!this.HasGameInRegistry("path"))
        {
            throw new GamePathNotSetException(
                $"Игра '{this.Id}' путь не установлен");
        }
        string path =
            this.Registry.Get<string>(
                this.Id,
                "path");
        if (!Directory.Exists(path))
        {
            throw new GamePathNotFoundException(
                $"Путь '{path}' игры '{this.Id}' не найден");
        }
        return path;
    }
    public bool TryGetPath(
        out string? data)
    {
        data = null;
        if (!this.HasGameInRegistry("path"))
            return false;
        string path =
            this.Registry.Get<string>(
                this.Id,
                "path");
        if (!Directory.Exists(path))
            return false;
        data =
            path;
        return true;
    }
    public void SetReferenceClient(
        string clientId)
    {
        this.CheckGameInRegistry();
        this.Registry.Set(
            this.Id,
            "referenceClient",
            clientId);
        this.ReferenceClient =
            clientId;
    }
    public string GetReferenceClient()
    {
        if (!this.HasGameInRegistry(
                "referenceClient"))
        {
            throw new GameNotSetRegistryException(
                $"Ссылка на игру '{this.Id}' не установлена");
        }
        string clientId =
            this.Registry.Get<string>(
                this.Id,
                "referenceClient");
        this.ReferenceClient =
            clientId;
        return clientId;
    }
    public void SetCurrentClient(
        string clientId)
    {
        this.CheckGameInRegistry();
        this.Registry.Set(
            this.Id,
            "client",
            clientId);
        this.Client =
            clientId;
    }
    public string GetCurrentClient()
    {
        if (!this.HasGameInRegistry("client"))
        {
            throw new GameNotSetRegistryException(
                $"Клиент игры '{this.Id}' не установлен");
        }
        string clientId =
            this.Registry.Get<string>(
                this.Id,
                "client");
        this.Client =
            clientId;
        return clientId;
    }
    public async Task<ClientData[]> GetClients()
    {
        ClientData[] clients =
            await this.Launcher.GetClients(
                this.Id);
        this.Clients.Clear();
        foreach (var client in clients)
        {
            this.Clients.Add(
                client.ID,
                client);
        }
        return clients;
    }
    public async Task ChangeClientGame(
        string clientId)
    {
        await this.Launcher.ChangeClientGame(
            this.Id,
            clientId);
    }
    public async Task SetStateSnapshot(
        StateSnapshot state)
    {
        await this.Launcher.SetStateSnapshot(
            this.Id,
            state);
    }
    public async Task<StateSnapshot?> GetStateSnapshot()
    {
        return await this.Launcher.GetStateSnapshot(
            this.Id);
    }
    public string[] BuildLaunchParams(
        Dictionary<string, object> parameters)
    {
        List<string> args =
            new();
        foreach (var param in parameters)
        {
            if (!this.LaunchParams.TryGetValue(
                    param.Key,
                    out ILaunchParam? launchParam))
            {
                throw new LaunchParamNotFoundException(
                    $"Параметр запуска '{param.Key}' не зарегистрирован");
            }
            if (launchParam is LaunchParamStr p)
            {
                foreach (var item in p.BuildArgs(
                             (string)param.Value))
                {
                    args.Add(item);
                }
            }
            else if (launchParam is LaunchParamBool p2)
            {
                foreach (var item in p2.BuildArgs(
                             (bool)param.Value))
                {
                    args.Add(item);
                }
            }
            else if (launchParam is LaunchParamDict p3)
            {
                foreach (var item in p3.BuildArgs(
                             (string)param.Value))
                {
                    args.Add(item);
                }
            }
            else if (launchParam is LaunchOptionDict p4)
            {
                foreach (string item in
                         p4.BuildArgs((string[])param.Value))
                {
                    args.Add(item);
                }
            }
            else if (launchParam is LaunchParamCustom custom)
            {
                foreach (string item in
                         custom.BuildArgs((string)param.Value))
                {
                    args.Add(item);
                }
            }
        }
        return args.ToArray();
    }
    public void SetLaunchParameters(
        Dictionary<string, object> parameters)
    {
        BuildLaunchParams(parameters);
        this.CheckGameInRegistry();
        foreach (var parameter in parameters)
        {
            this.Registry.Set(
                this.Id,
                $"launch.{parameter.Key}",
                parameter.Value);
        }
    }
    public Dictionary<string, object> GetLaunchParameters()
    {
        Dictionary<string, object> parameters =
            new();
        foreach (var launchParam in this.LaunchParams.Values)
        {
            if (launchParam is LaunchParamBool)
            {
                bool value = false;
                if (this.HasGameInRegistry())
                {
                    value =
                        this.Registry.Get(
                            this.Id,
                            $"launch.{launchParam.Id}",
                            false);
                }
                parameters[launchParam.Id] =
                    value;
            }
            else if (launchParam is LaunchParamDict dict)
            {
                string defaultValue =
                    dict.Dictionary.Keys.FirstOrDefault()
                    ?? "";
                string value =
                    defaultValue;
                if (this.HasGameInRegistry())
                {
                    value =
                        this.Registry.Get(
                            this.Id,
                            $"launch.{launchParam.Id}",
                            defaultValue);
                }
                if (!dict.Dictionary.ContainsKey(value))
                {
                    value =
                        defaultValue;
                }
                parameters[launchParam.Id] =
                    value;
            }
            else if (launchParam is LaunchOptionDict options)
            {
                string[] defaultValue =
                    Array.Empty<string>();
                if (this.HasGameInRegistry())
                {
                    defaultValue =
                        this.Registry.Get(
                            this.Id,
                            $"launch.{launchParam.Id}",
                            defaultValue);
                }
                parameters[launchParam.Id] =
                    defaultValue.Where(
                        options.Dictionary.ContainsKey).ToArray();
            }
            else if (launchParam is LaunchOptionDict Additinaloptions)
            {
                string[] defaultValue =
                    Array.Empty<string>();
                string[] value =
                    defaultValue;
                if (this.HasGameInRegistry())
                {
                    value =
                        this.Registry.Get(
                            this.Id,
                            $"launch.{launchParam.Id}",
                            defaultValue);
                }
                parameters[launchParam.Id] =
                    value
                        .Where(Additinaloptions.Dictionary.ContainsKey)
                        .ToArray();
            }
            else if (launchParam is LaunchParamCustom)
            {
                string value = "";
                if (this.HasGameInRegistry())
                {
                    value =
                        this.Registry.Get(
                            this.Id,
                            $"launch.{launchParam.Id}",
                            "");
                }
                parameters[launchParam.Id] = value;
            }
        }
        return parameters;
    }
    public string[] BuildSavedLaunchParams()
    {
        return BuildLaunchParams(
            GetLaunchParameters());
    }
    public bool IsGameRunning()
    {
        if (_gameProcess == null)
            return false;
        try
        {
            if (_gameProcess.HasExited)
            {
                _gameProcess.Dispose();
                _gameProcess = null;
                return false;
            }
            return true;
        }
        catch
        {
            _gameProcess = null;
            return false;
        }
    }
    public async Task Launch()
    {
        if (!this.IsInstall)
        {
            throw new GameInstallException(
                "Игра не установлена");
        }
        if (this.IsGameRunning())
        {
            return;
        }
        string gamePath =
            this.GetPath();
        string exePath =
            Path.Combine(
                gamePath,
                "BF2142.exe");
        if (!File.Exists(exePath))
        {
            throw new FileNotFoundException(
                "BF2142.exe не найден",
                exePath);
        }
        string[] args =
            this.BuildSavedLaunchParams();
        ProcessStartInfo startInfo =
            new ProcessStartInfo
            {
                FileName =
                    exePath,
                WorkingDirectory =
                    gamePath,
                UseShellExecute =
                    false
            };
        foreach (string arg in args)
        {
            startInfo.ArgumentList.Add(
                arg);
        }
        _gameProcess =
            new Process
            {
                StartInfo =
                    startInfo,
                EnableRaisingEvents =
                    true
            };
        _gameProcess.Exited +=
            async (_, _) =>
            {
                Process? process =
                    _gameProcess;
                _gameProcess =
                    null;
                if (process != null)
                {
                    process.Dispose();
                }
                await SetStatus(
                    GameStatus.NotRunning);
            };
        if (!_gameProcess.Start())
        {
            _gameProcess.Dispose();
            _gameProcess = null;
            throw new Exception(
                "Не удалось запустить BF2142.exe");
        }
        await SetStatus(
            GameStatus.Running);
    }
}
