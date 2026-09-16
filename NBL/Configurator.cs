using System.Text.Json;
using System.Text.Json.Nodes;
using Timer = System.Timers.Timer;
namespace NBL;
public class Configurator
{
    public string Path { get; }
    public string PathTemp { get; }
    public string PathBackup { get; }
    public string DefaultData { get; }
    protected JsonNode? RootNode;
    protected Timer Timer;
    protected bool LockWrite = false;
    protected bool LockRead = true;
    public Configurator(string path, string defaultData = "{}")
    {
        this.Path = path;
        this.PathTemp = $"{this.Path}.tmp";
        this.PathBackup = $"{this.Path}.bck";
        this.DefaultData = defaultData;
        this.Timer = new Timer(1000);
        this.Timer.Elapsed += async (sender, args) => await this.Write();
    }
    public void Save()
    {
        this.LockWrite = true;
        if (this.Timer.Enabled) this.Timer.Stop();
        this.Timer.Start();
    }
    public async Task Read(bool createMissing = false)
    {
        this.LockRead = true;
        if (createMissing && !File.Exists(this.Path)) await File.WriteAllTextAsync(this.Path, this.DefaultData);
        string? rawData = await File.ReadAllTextAsync(this.Path);
        this.RootNode = rawData.Trim() != "" ? JsonNode.Parse(rawData) : JsonNode.Parse(this.DefaultData);
        this.LockRead = false;
    }
    public void AddSection(string name)
    {
        this.RootNode?[name] = new JsonObject();
        this.Save();
    }
    public async Task Write()
    {
        this.Timer.Stop();
        Console.WriteLine("save");
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver()
        };
        string data =
            this.RootNode?.ToJsonString(options)
            ?? this.DefaultData;
        await File.WriteAllTextAsync(this.PathTemp, data);
        File.Replace(this.PathTemp, this.Path, null);
        await File.WriteAllTextAsync(this.PathBackup, data);
        this.LockWrite = false;
    }
    public bool HasSection(string name) => this.RootNode?.AsObject().ContainsKey(name) ?? false;
    public bool HasOption(string sectionName, string optionName)
    {
        JsonNode? sectionNode;
        if (this.RootNode?.AsObject().TryGetPropertyValue(sectionName, out sectionNode) ?? false)
        {
            if (sectionNode?.AsObject().ContainsKey(optionName) ?? false) return true;
        }
        return false;
    }
    public void Set<T>(string sectionName, string optionName, T value)
    {
        this.CheckSection(sectionName);
        this.RootNode![sectionName]![optionName] =
            JsonSerializer.SerializeToNode(value);
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
        this.CheckOption(sectionName, optionName);
        JsonNode node = this.RootNode![sectionName]![optionName]!;
        return node.Deserialize<T>()
               ?? throw new InvalidOperationException(
                   $"Option '{optionName}' is null.");
    }
    public T Get<T>(string sectionName, string optionName, T defaultValue)
    {
        this.CheckSection(sectionName);
        if (!this.HasOption(sectionName, optionName))
            return defaultValue;
        JsonNode node = this.RootNode![sectionName]![optionName]!;
        return node.Deserialize<T>() ?? defaultValue;
    }
    public void RemoveOption(string sectionName, string optionName)
    {
        this.CheckOption(sectionName, optionName);
        this.RootNode![sectionName]!.AsObject().Remove(optionName);
        this.Save();
    }
    public void RemoveSection(string sectionName)
    {
        this.CheckSection(sectionName);
        this.RootNode!.AsObject().Remove(sectionName);
        this.Save();
    }
    public List<string> Sections()
    {
        JsonObject? jobj = this.RootNode?.AsObject();
        return jobj != null ? jobj.Select(kvp => kvp.Key).ToList() : new List<string>();
    }
    public List<string> Options(string sectionName)
    {
        this.CheckSection(sectionName);
        JsonObject jobj = this.RootNode![sectionName]!.AsObject();
        return jobj.Select(kvp => kvp.Key).ToList();
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
}