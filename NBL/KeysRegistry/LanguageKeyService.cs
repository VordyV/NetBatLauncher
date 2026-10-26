
using Microsoft.Win32;
using System;
using System.Diagnostics;

namespace NBL.Services;

public static class LanguageKeyService
{
    private const string RegistryPath =
        @"SOFTWARE\Electronic Arts\EA GAMES\Battlefield 2142";

    public static string GetLanguage()
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(
                RegistryHive.LocalMachine, RegistryView.Registry32);

            using var key = baseKey.OpenSubKey(RegistryPath);

            return key?.GetValue("Language") as string ?? "English";
        }
        catch
        {
            return "English";
        }
    }

    public static bool SetLanguage(string language)
    {
        if (language != "English" && language != "Russian")
            return false;

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = Environment.ProcessPath!,
                UseShellExecute = true,
                Verb = "runas"
            };

            startInfo.ArgumentList.Add("--set-game-language");
            startInfo.ArgumentList.Add(language);

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
        if (args.Length < 2 ||
            !string.Equals(args[0], "--set-game-language",
                StringComparison.OrdinalIgnoreCase))
            return 1;

        string language = args[1];

        if (language != "English" && language != "Russian")
            return 1;

        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(
                RegistryHive.LocalMachine, RegistryView.Registry32);

            using var key = baseKey.CreateSubKey(RegistryPath);

            if (key == null)
                return 1;

            key.SetValue("Language", language, RegistryValueKind.String);
            key.SetValue("Locale",
                language == "Russian" ? "ru" : "en",
                RegistryValueKind.String);

            using var version = baseKey.CreateSubKey(RegistryPath + @"\1.0");

            if (version == null)
                return 1;

            version.SetValue("LanguageName", language);
            version.SetValue("Language",
                language == "Russian" ? 16 : 9,
                RegistryValueKind.DWord);

            return 0;
        }
        catch
        {
            return 1;
        }
    }
}
