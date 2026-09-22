using System;
using System.IO;
using System.Text.RegularExpressions;

namespace NBL.Services;

public static class AudioSettingsPath
{
    public static string GetPath()
    {
        string profilePath =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.MyDocuments),
                "Battlefield 2142",
                "Profiles");

        string globalPath =
            Path.Combine(
                profilePath,
                "Global.con");

        if (!File.Exists(globalPath))
        {
            throw new FileNotFoundException(
                "Global.con не найден.",
                globalPath);
        }

        string[] lines = File.ReadAllLines(globalPath);

        string? defaultUser = null;
        string? lastOnlineUser = null;

        foreach (string line in lines)
        {
            Match defaultMatch =
                Regex.Match(
                    line,
                    @"GlobalSettings\.setDefaultUser\s+""([^""]*)""");

            if (defaultMatch.Success)
                defaultUser = defaultMatch.Groups[1].Value;

            Match lastOnlineMatch =
                Regex.Match(
                    line,
                    @"GlobalSettings\.setLastOnlineUser\s+""([^""]*)""");

            if (lastOnlineMatch.Success)
                lastOnlineUser = lastOnlineMatch.Groups[1].Value;
        }

        string? profile =
            !string.IsNullOrWhiteSpace(defaultUser)
                ? defaultUser
                : lastOnlineUser;

        if (string.IsNullOrWhiteSpace(profile))
            throw new FileNotFoundException(
                "Не удалось определить текущий профиль Battlefield 2142.",
                globalPath);

        string audioPath =
            Path.Combine(
                profilePath,
                profile,
                "Audio.con");

        if (!File.Exists(audioPath))
            throw new FileNotFoundException(
                "Audio.con не найден.",
                audioPath);

        return audioPath;
    }
}