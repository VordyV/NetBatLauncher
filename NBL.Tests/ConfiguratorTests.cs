using Xunit;

namespace NBL.Tests;

public sealed class ConfiguratorTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("nbl-cfg-").FullName;

    private string P(string name) => Path.Combine(_dir, name);

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Fact]
    public async Task AddSection_KeepsExistingSection()
    {
        var c = new Configurator(P("c.json"));
        c.ReadSync(true);
        c.AddSection("g");
        c.Set("g", "path", "X");
        c.AddSection("g");

        Assert.Equal("X", c.Get<string>("g", "path"));
        await c.Write();
    }

    [Fact]
    public void ReadSync_CorruptFile_FallsBackToDefaultsAndKeepsCopy()
    {
        File.WriteAllText(P("c.json"), "{ broken");
        var c = new Configurator(P("c.json"));

        c.ReadSync();

        Assert.False(c.HasSection("x"));
        Assert.True(File.Exists(P("c.json.corrupt")));
    }

    [Fact]
    public void ReadSync_CorruptFile_RestoresFromBackup()
    {
        File.WriteAllText(P("c.json"), "{ broken");
        File.WriteAllText(P("c.json.bck"), "{\"b\":{}}");
        var c = new Configurator(P("c.json"));

        c.ReadSync();

        Assert.True(c.HasSection("b"));
    }

    [Fact]
    public void ReadSync_NonObjectRoot_FallsBackToDefaults()
    {
        File.WriteAllText(P("c.json"), "[1,2,3]");
        var c = new Configurator(P("c.json"));

        c.ReadSync();

        Assert.Empty(c.Sections());
    }

    [Fact]
    public async Task Write_KeepsPreviousVersionAsBackup()
    {
        var c = new Configurator(P("c.json"));
        c.ReadSync(true);
        c.AddSection("a");
        await c.Write();
        c.AddSection("b");
        await c.Write();

        string backup = File.ReadAllText(P("c.json.bck"));
        string current = File.ReadAllText(P("c.json"));

        Assert.Contains("\"a\"", backup);
        Assert.DoesNotContain("\"b\"", backup);
        Assert.Contains("\"b\"", current);
    }

    [Fact]
    public async Task ConcurrentSetAndWrite_DoNotThrow()
    {
        var c = new Configurator(P("c.json"));
        c.ReadSync(true);
        c.AddSection("s");

        var setters = Enumerable.Range(0, 8).Select(t => Task.Run(() =>
        {
            for (int i = 0; i < 200; i++) c.Set("s", $"k{t}-{i}", i);
        }));
        var writer = Task.Run(async () =>
        {
            for (int i = 0; i < 20; i++) await c.Write();
        });

        await Task.WhenAll(setters.Append(writer));
        await c.Write();

        Assert.Equal(8 * 200, c.Options("s").Count);
    }
}
