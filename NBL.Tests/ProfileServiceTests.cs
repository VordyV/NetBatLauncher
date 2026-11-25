using System.Diagnostics;
using NBL.Services;
using Xunit;

namespace NBL.Tests;

public sealed class ProfileServiceTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("nbl-prof-").FullName;

    private string Storage => Path.Combine(_root, "store");
    private string Active => Path.Combine(Storage, "Active");
    private string Live => Path.Combine(_root, "docs", "Battlefield 2142", "Profiles");

    private ProfileService Create() => new(Storage, Live);

    public void Dispose()
    {
        try { if (new DirectoryInfo(Live).LinkTarget != null) Directory.Delete(Live, false); } catch { }
        try { Directory.Delete(_root, true); } catch { }
    }

    private static void Put(string dir, string file, string text)
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, file), text);
    }

    private static string Get(string dir, string file) => File.ReadAllText(Path.Combine(dir, file));

    private static bool IsLink(string path) => new DirectoryInfo(path).LinkTarget != null;

    private static void MakeJunction(string link, string target)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(link)!);
        var psi = new ProcessStartInfo("cmd.exe", $"/d /c mklink /J \"{link}\" \"{target}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var p = Process.Start(psi)!;
        p.StandardOutput.ReadToEnd();
        p.StandardError.ReadToEnd();
        p.WaitForExit();

        Assert.Equal(0, p.ExitCode);
    }

    [Fact]
    public void PrepareLive_MissingFolder_CreatesDefaults()
    {
        var s = Create();

        string path = s.PrepareLive();

        Assert.Equal(Live, path);
        Assert.False(s.IsShared);
        Assert.True(File.Exists(Path.Combine(Live, "Global.con")));
        Assert.True(File.Exists(Path.Combine(Live, "0001", "Profile.con")));
    }

    [Fact]
    public void PrepareLive_MissingFolder_RestoresLegacyActiveData()
    {
        Put(Active, "Global.con", "legacy");

        Create().PrepareLive();

        Assert.Equal("legacy", Get(Live, "Global.con"));
    }

    [Fact]
    public async Task Switch_RegularFolder_SnapshotsAndRestores()
    {
        Put(Live, "Global.con", "marker-original");
        var s = Create();

        await s.SwitchAsync("original", "respy");

        Assert.False(IsLink(Live));
        Assert.Equal("marker-original", Get(Path.Combine(Storage, "original"), "Global.con"));
        Assert.True(Directory.Exists(Path.Combine(Storage, "respy")));
        Assert.DoesNotContain("marker", Get(Live, "Global.con"));

        await s.SwitchAsync("respy", "original");

        Assert.Equal("marker-original", Get(Live, "Global.con"));
    }

    [Fact]
    public async Task Switch_OwnLegacyJunction_IsReplacedByRegularFolder()
    {
        Put(Active, "Global.con", "legacy");
        MakeJunction(Live, Active);
        Assert.True(IsLink(Live));

        await Create().SwitchAsync("original", "original");

        Assert.False(IsLink(Live));
        Assert.Equal("legacy", Get(Live, "Global.con"));
        Assert.Equal("legacy", Get(Active, "Global.con"));
    }

    [Fact]
    public async Task Switch_ForeignJunction_IsLeftAlone()
    {
        string other = Path.Combine(_root, "other");
        Put(other, "Global.con", "other");
        MakeJunction(Live, other);
        var s = Create();

        await s.SwitchAsync("original", "respy");

        Assert.True(s.IsShared);
        Assert.True(IsLink(Live));
        Assert.Single(Directory.GetFiles(other));
        Assert.Empty(Directory.GetDirectories(other));
        Assert.Equal("other", Get(other, "Global.con"));
    }

    [Fact]
    public async Task Switch_InvalidClientId_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Create().SwitchAsync("..", "respy"));
    }
}
