using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Text;
using System.Security.Cryptography;

namespace NBL.Services;

public static class GameKeyService
{
    private const string RegistryPath =
        @"SOFTWARE\Electronic Arts\EA GAMES\Battlefield 2142\ergc";

    public static string GenerateKey()
    {
        Random random =
            new Random();

        string chars =
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

        StringBuilder sb =
            new StringBuilder("x9392");

        for (int i = 0; i < 20; i++)
        {
            sb.Append(
                chars[random.Next(chars.Length)]);
        }

        return sb.ToString();
    }

    public static bool SetKey(string key)
    {
        try
        {
            ProcessStartInfo startInfo =
                new ProcessStartInfo
                {
                    FileName =
                        Environment.ProcessPath!,
                    UseShellExecute = true,
                    Verb = "runas"
                };

            startInfo.ArgumentList.Add(
                "--set-game-key");

            startInfo.ArgumentList.Add(
                key);

            using Process? process =
                Process.Start(startInfo);

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

    public static bool GenerateAndSetKey()
    {
        string key =
            GenerateKey();

        return SetKey(key);
    }

    public static string? GetKey()
    {
        try
        {
            using RegistryKey baseKey =
                RegistryKey.OpenBaseKey(
                    RegistryHive.LocalMachine,
                    RegistryView.Registry32);

            using RegistryKey? key =
                baseKey.OpenSubKey(
                    RegistryPath);

            return key?.GetValue("") as string;
        }
        catch
        {
            return null;
        }
    }

    public static int HandleCommandLine(
        string[] args)
    {
        if (args.Length < 2)
            return 1;

        if (!string.Equals(
            args[0],
            "--set-game-key",
            StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        string gameKey =
            args[1];

        if (string.IsNullOrWhiteSpace(gameKey))
            return 1;

        try
        {
            using RegistryKey baseKey =
                RegistryKey.OpenBaseKey(
                    RegistryHive.LocalMachine,
                    RegistryView.Registry32);

            using RegistryKey? key =
                baseKey.CreateSubKey(
                    RegistryPath);

            if (key == null)
                return 1;

            key.SetValue(
                "",
                gameKey,
                RegistryValueKind.String);

            return 0;
        }
        catch
        {
            return 1;
        }
    }
    public static string? GetKeyMd5()
    {
        string? key = GetKey();

        if (string.IsNullOrWhiteSpace(key))
            return null;

        byte[] hash =
            MD5.HashData(
                Encoding.UTF8.GetBytes(key));

        return Convert.ToHexString(hash);
    }
}