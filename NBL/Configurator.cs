using System.Text.Json;
using System.Text.Json.Nodes;
using NBL.Services;
using Timer = System.Timers.Timer;
namespace NBL;
public class Configurator
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver()
    };
    private readonly object _sync = new();
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private int _version;
    public string Path { get; }
    public string PathTemp { get; }
    public string PathBackup { get; }
    public string DefaultData { get; }
    protected JsonNode? RootNode;
    protected Timer Timer;
    protected volatile bool LockWrite = false;
    protected volatile bool LockRead = true;
    public Configurator(string path, string defaultData = "{}")
    {
        this.Path = path;
        this.PathTemp = $"{this.Path}.tmp";
        this.PathBackup = $"{this.Path}.bck";
        this.DefaultData = defaultData;
        this.Timer = new Timer(1000);
        this.Timer.Elapsed += async (_, _) =>
        {
            try { await this.Write(); }
            catch (Exception ex)
            {
                Logger.Error("Config write failed", ex);
                this.LockWrite = false;
            }
        };
    }
    public void Save()
    {
        Interlocked.Increment(ref this._version);
        this.LockWrite = true;
        if (this.Timer.Enabled) this.Timer.Stop();
        this.Timer.Start();
    }
    private JsonObject Parse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return (JsonObject)JsonNode.Parse(this.DefaultData)!;
        try { if (JsonNode.Parse(raw) is JsonObject root) return root; }
        catch (JsonException) { }
        Logger.Error($"Config is corrupted: '{this.Path}'");
        try { File.Copy(this.Path, this.Path + ".corrupt", true); }
        catch { }
        try
        {
            if (File.Exists(this.PathBackup) && JsonNode.Parse(File.ReadAllText(this.PathBackup)) is JsonObject backup)
            {
                Logger.Warning($"Config restored from backup: '{this.PathBackup}'");
                return backup;
            }
        }
        catch { }
        return (JsonObject)JsonNode.Parse(this.DefaultData)!;
    }
    public async Task Read(bool createMissing = false)
    {
        this.LockRead = true;
        try
        {
            if (createMissing && !File.Exists(this.Path)) await File.WriteAllTextAsync(this.Path, this.DefaultData);
            string raw = await File.ReadAllTextAsync(this.Path);
            lock (this._sync) this.RootNode = this.Parse(raw);
        }
        finally { this.LockRead = false; }
    }
    public void AddSection(string name)
    {
        lock (this._sync)
        {
            if (this.HasSection(name)) return;
            this.RootNode?[name] = new JsonObject();
        }
        this.Save();
    }
    public async Task Write()
    {
        this.Timer.Stop();
        await this._writeGate.WaitAsync();
        try
        {
            string data;
            int version;
            lock (this._sync)
            {
                version = Volatile.Read(ref this._version);
                data = this.RootNode?.ToJsonString(WriteOptions) ?? this.DefaultData;
            }
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    await File.WriteAllTextAsync(this.PathTemp, data);
                    if (File.Exists(this.Path)) File.Replace(this.PathTemp, this.Path, this.PathBackup, true);
                    else File.Move(this.PathTemp, this.Path, true);
                    break;
                }
                catch (Exception ex) when (attempt < 5 && ex is (IOException or UnauthorizedAccessException))
                {
                    await Task.Delay(200 * attempt);
                }
                catch (Exception ex)
                {
                    Logger.Error($"Config write failed: '{this.Path}'", ex);
                    break;
                }
            }
            if (Volatile.Read(ref this._version) == version) this.LockWrite = false;
        }
        finally { this._writeGate.Release(); }
    }
    public bool HasSection(string name)
    {
        lock (this._sync) return this.RootNode?.AsObject().ContainsKey(name) ?? false;
    }
    public bool HasOption(string sectionName, string optionName)
    {
        lock (this._sync)
        {
            JsonNode? sectionNode;
            if (this.RootNode?.AsObject().TryGetPropertyValue(sectionName, out sectionNode) ?? false)
            {
                if (sectionNode?.AsObject().ContainsKey(optionName) ?? false) return true;
            }
            return false;
        }
    }
    public void Set<T>(string sectionName, string optionName, T value)
    {
        lock (this._sync)
        {
            this.CheckSection(sectionName);
            this.RootNode![sectionName]![optionName] = JsonSerializer.SerializeToNode(value);
        }
        this.Save();
    }
    public void CheckSection(string sectionName)
    {
        if (!this.HasSection(sectionName)) throw new Exception($"Section '{sectionName}' does not exist");
    }
    public void CheckOption(string sectionName, string optionName)
    {
        this.CheckSection(sectionName);
        if (!this.HasOption(sectionName, optionName)) throw new Exception($"Option '{optionName}' of section '{sectionName}' does not exist.");
    }
    public T Get<T>(string sectionName, string optionName)
    {
        lock (this._sync)
        {
            this.CheckOption(sectionName, optionName);
            JsonNode node = this.RootNode![sectionName]![optionName]!;
            return node.Deserialize<T>() ?? throw new InvalidOperationException($"Option '{optionName}' is null.");
        }
    }
    public T Get<T>(string sectionName, string optionName, T defaultValue)
    {
        lock (this._sync)
        {
            this.CheckSection(sectionName);
            if (!this.HasOption(sectionName, optionName)) return defaultValue;
            JsonNode node = this.RootNode![sectionName]![optionName]!;
            return node.Deserialize<T>() ?? defaultValue;
        }
    }
    public void RemoveOption(string sectionName, string optionName)
    {
        lock (this._sync)
        {
            this.CheckOption(sectionName, optionName);
            this.RootNode![sectionName]!.AsObject().Remove(optionName);
        }
        this.Save();
    }
    public void RemoveSection(string sectionName)
    {
        lock (this._sync)
        {
            this.CheckSection(sectionName);
            this.RootNode!.AsObject().Remove(sectionName);
        }
        this.Save();
    }
    public List<string> Sections()
    {
        lock (this._sync)
        {
            JsonObject? jobj = this.RootNode?.AsObject();
            return jobj != null ? jobj.Select(kvp => kvp.Key).ToList() : new List<string>();
        }
    }
    public List<string> Options(string sectionName)
    {
        lock (this._sync)
        {
            this.CheckSection(sectionName);
            JsonObject jobj = this.RootNode![sectionName]!.AsObject();
            return jobj.Select(kvp => kvp.Key).ToList();
        }
    }
    public async Task WaitSafeMoment()
    {
        while (this.LockWrite)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10));
        }
    }
    public async Task WaitReadiness()
    {
        while (this.LockRead)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10));
        }
    }
    public void ReadSync(bool createMissing = false)
    {
        this.LockRead = true;
        try
        {
            if (createMissing && !File.Exists(this.Path)) File.WriteAllText(this.Path, this.DefaultData);
            string raw = File.ReadAllText(this.Path);
            lock (this._sync) this.RootNode = this.Parse(raw);
        }
        finally { this.LockRead = false; }
    }
}
