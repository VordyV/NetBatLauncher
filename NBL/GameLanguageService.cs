using Microsoft.Win32;
using System;
using System.Diagnostics;

namespace NBL.Services;

public static class GameLanguageService
{
    private const string RegistryPath =
    @"SOFTWARE\Electronic Arts\EA GAMES\Battlefield 2142";

    public static string GetLanguage()
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

            return key?.GetValue("Language")
                as string
                ?? "English";
        }
        catch
        {
            return "English";
        }
    }

    public static bool SetLanguage(
        string language)
    {
        string currentLanguage =
            GetLanguage();

        Console.WriteLine(
            $"Game language: {currentLanguage} -> {language}");

        if (string.Equals(
            currentLanguage,
            language,
            StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

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
                "--set-game-language");

            startInfo.ArgumentList.Add(
                language);

            Console.WriteLine(
                "Starting elevated NetBat2142...");

            using Process? process =
                Process.Start(startInfo);

            if (process == null)
                return false;

            process.WaitForExit();

            Console.WriteLine(
                $"Elevated process exit code: {process.ExitCode}");

            return process.ExitCode == 0;
        }
        catch (Exception exception)
        {
            Console.WriteLine(
                $"GameLanguageService error: {exception}");

            return false;
        }
    }

    public static int HandleCommandLine(
        string[] args)
    {
        if (args.Length < 2)
            return 1;

        if (!string.Equals(
            args[0],
            "--set-game-language",
            StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        string language =
            args[1];

        if (language != "English" &&
            language != "Russian")
        {
            return 1;
        }

        try
        {
            using RegistryKey baseKey =
                RegistryKey.OpenBaseKey(
                    RegistryHive.LocalMachine,
                    RegistryView.Registry32);

            using RegistryKey? key =
                baseKey.OpenSubKey(
                    RegistryPath,
                    writable: true);

            if (key == null)
                return 1;

            key.SetValue(
                "Language",
                language,
                RegistryValueKind.String);

            return 0;
        }
        catch (Exception exception)
        {
            Console.WriteLine(
                $"Failed to set game language: {exception}");

            return 1;
        }
    }
}
