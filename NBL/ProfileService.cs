
using System.Text.RegularExpressions;

namespace NBL.Services;

public sealed class ProfileService
{
    private readonly string ProfilesPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "Battlefield 2142",
        "Profiles");

    private readonly string StoragePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NetBat2142",
        "Profiles");

    public Task SwitchAsync(string currentClientId, string targetClientId)
    {
        if (string.IsNullOrWhiteSpace(currentClientId) ||
            string.IsNullOrWhiteSpace(targetClientId))
            throw new ArgumentException("ID клиента не может быть пустым.");

        if (currentClientId.Equals(targetClientId, StringComparison.OrdinalIgnoreCase))
            return Task.CompletedTask;

        string currentPath = GetClientPath(currentClientId);
        string targetPath = GetClientPath(targetClientId);

        Directory.CreateDirectory(ProfilesPath);
        Directory.CreateDirectory(StoragePath);

        SaveSnapshot(currentPath);

        if (!Directory.Exists(targetPath))
        {
            CopyDirectory(ProfilesPath, targetPath);
            PrepareNewClient(targetPath);

            Logger.Info($"Initial profile created: client='{targetClientId}'");
        }

        RestoreSnapshot(targetPath);

        Logger.Info(
            $"Profiles switched: '{currentClientId}' -> '{targetClientId}'");

        return Task.CompletedTask;
    }

    private string GetClientPath(string clientId)
    {
        if (clientId is "." or ".." ||
            clientId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            clientId.Contains('/') || clientId.Contains('\\'))
            throw new ArgumentException("Недопустимый ID клиента.");

        return Path.Combine(StoragePath, clientId);
    }

    private void SaveSnapshot(string destination)
    {
        string temp = destination + ".tmp";
        string old = destination + ".old";

        DeleteDirectory(temp);
        DeleteDirectory(old);

        CopyDirectory(ProfilesPath, temp);

        if (Directory.Exists(destination))
            Directory.Move(destination, old);

        try
        {
            Directory.Move(temp, destination);
            DeleteDirectory(old);
        }
        catch
        {
            if (!Directory.Exists(destination) && Directory.Exists(old))
                Directory.Move(old, destination);

            throw;
        }
    }

    private void RestoreSnapshot(string source)
    {
        string temp = ProfilesPath + ".nbl-temp";
        string old = ProfilesPath + ".nbl-old";

        DeleteDirectory(temp);
        DeleteDirectory(old);

        CopyDirectory(source, temp);

        if (Directory.Exists(ProfilesPath))
            Directory.Move(ProfilesPath, old);

        try
        {
            Directory.Move(temp, ProfilesPath);
            DeleteDirectory(old);
        }
        catch
        {
            if (!Directory.Exists(ProfilesPath) && Directory.Exists(old))
                Directory.Move(old, ProfilesPath);

            throw;
        }
    }

    private void PrepareNewClient(string path)
    {
        foreach (string file in Directory.GetFiles(
                     path, "Profile.con", SearchOption.AllDirectories))
        {
            File.Delete(file);
        }

        foreach (string file in Directory.GetFiles(
                     path, "Global.con", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(file);

            text = Regex.Replace(
                text,
                @"(?im)^(\s*GlobalSettings\.setDefaultUser\s+).*$",
                "$1\"\"");

            text = Regex.Replace(
                text,
                @"(?im)^(\s*GlobalSettings\.setLastOnlineUser\s+).*$",
                "$1\"\"");

            text = Regex.Replace(
                text,
                @"(?im)^(\s*GlobalSettings\.setEncryptedLogin\s+).*$",
                "$1\"\"");

            File.WriteAllText(file, text);
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (string directory in Directory.GetDirectories(source))
        {
            CopyDirectory(
                directory,
                Path.Combine(destination, Path.GetFileName(directory)));
        }

        foreach (string file in Directory.GetFiles(source))
        {
            File.Copy(
                file,
                Path.Combine(destination, Path.GetFileName(file)),
                true);
        }
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, true);
    }
}
