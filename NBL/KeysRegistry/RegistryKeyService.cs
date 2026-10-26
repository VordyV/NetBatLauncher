
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace NBL.Services;

public static class RegistryKeyService
{
    private const string RegistryPath =
        @"SOFTWARE\Electronic Arts\EA GAMES\Battlefield 2142";

    private const string KeyPath = RegistryPath + @"\ergc";

    private const string GameName = "Battlefield 2142";
    private const string BuildNumber = "1.10.77.0";
    private const string GameVersion = "1.51";

    private static readonly string KeyFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NetBat2142",
        "gamekey.txt");

    public static string GenerateKey()
    {
        const string chars =
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

        using var rng = RandomNumberGenerator.Create();
        byte[] bytes = new byte[20];
        rng.GetBytes(bytes);

        var key = new StringBuilder("x9392", 25);

        foreach (byte b in bytes)
            key.Append(chars[b % chars.Length]);

        return key.ToString();
    }

    public static string? GetKey()
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(
                RegistryHive.LocalMachine, RegistryView.Registry32);

            using var key = baseKey.OpenSubKey(KeyPath);
            return key?.GetValue("") as string;
        }
        catch
        {
            return null;
        }
    }

    public static string? GetKeyMd5()
    {
        string? key = GetKey();

        if (string.IsNullOrWhiteSpace(key))
            return null;

        return Convert.ToHexString(
            MD5.HashData(Encoding.UTF8.GetBytes(key)));
    }

    public static string? GetSavedKey()
    {
        try
        {
            return File.Exists(KeyFile)
                ? File.ReadAllText(KeyFile).Trim()
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static bool SaveKey(string key)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(KeyFile)!);
            File.WriteAllText(KeyFile, key);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool GenerateAndSetKey()
    {
        string key = GetSavedKey() ?? GenerateKey();

        if (!SetKey(key))
            return false;

        SaveKey(key);
        return true;
    }

    public static bool SetKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        return RunElevated("--set-game-key", key);
    }

    public static bool RegisterGame(string gamePath)
    {
        if (string.IsNullOrWhiteSpace(gamePath))
            return false;

        gamePath = Path.GetFullPath(gamePath);

        if (!File.Exists(Path.Combine(gamePath, "BF2142.exe")))
            return false;

        string key = GetSavedKey() ?? GetKey() ?? GenerateKey();

        if (!RunElevated("--register-game", gamePath, key))
            return false;

        SaveKey(key);
        return true;
    }

    private static bool RunElevated(params string[] args)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = Environment.ProcessPath!,
                UseShellExecute = true,
                Verb = "runas"
            };

            foreach (string arg in args)
                startInfo.ArgumentList.Add(arg);

            using var process = Process.Start(startInfo);

            if (process == null)
                return false;

            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public static int HandleCommandLine(string[] args)
    {
        if (args.Length < 2)
            return 1;

        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(
                RegistryHive.LocalMachine, RegistryView.Registry32);

            if (string.Equals(args[0], "--set-game-key",
                    StringComparison.OrdinalIgnoreCase))
            {
                using var ergckey = baseKey.CreateSubKey(KeyPath);

                if (ergckey == null || string.IsNullOrWhiteSpace(args[1]))
                    return 1;

                ergckey.SetValue("", args[1], RegistryValueKind.String);
                return 0;
            }

            if (!string.Equals(args[0], "--register-game",
                    StringComparison.OrdinalIgnoreCase) || args.Length < 3)
                return 1;

            string gamePath = Path.GetFullPath(args[1]);
            string gameKey = args[2];

            if (!Directory.Exists(gamePath) ||
                !File.Exists(Path.Combine(gamePath, "BF2142.exe")) ||
                string.IsNullOrWhiteSpace(gameKey))
                return 1;

            using var game = baseKey.CreateSubKey(RegistryPath);

            if (game == null)
                return 1;

            game.SetValue("ApplicationName", GameName);
            game.SetValue("BuildNr", BuildNumber);
            game.SetValue("Version", GameVersion);
            game.SetValue("CD Drive", gamePath);
            game.SetValue("DisplayName", GameName);
            game.SetValue("ProductName", GameName);
            game.SetValue("Folder", gamePath);
            game.SetValue("InstallDir", gamePath);
            game.SetValue("Installed From", gamePath);
            game.SetValue("Suppression Exe", "");
            game.SetValue("Registration", "");

            using var version = baseKey.CreateSubKey(RegistryPath + @"\1.0");

            if (version == null)
                return 1;

            version.SetValue("DisplayName", GameName);
            version.SetValue("ProgramGroup", gamePath);

            using var key = baseKey.CreateSubKey(KeyPath);

            if (key == null)
                return 1;

            key.SetValue("", gameKey, RegistryValueKind.String);

            return 0;
        }
        catch
        {
            return 1;
        }
    }
}
