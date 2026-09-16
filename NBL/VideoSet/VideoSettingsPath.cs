using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
namespace NBL.Services;
public static class VideoSettingsPath
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
                "BF2142 Global.con not found.",
                globalPath);
        }
        string globalContent =
            File.ReadAllText(globalPath);
        // 1. Сначала пытаемся получить профиль из defaultUser.
        string? profileName =
            GetSetting(
                globalContent,
                "setDefaultUser");
        if (!string.IsNullOrWhiteSpace(profileName))
        {
            string videoPath =
                Path.Combine(
                    profilePath,
                    profileName,
                    "Video.con");
            if (File.Exists(videoPath))
            {
                return videoPath;
            }
        }
        // 2. Если defaultUser пустой, пробуем lastOnlineUser.
        string? lastOnlineUser =
            GetSetting(
                globalContent,
                "setLastOnlineUser");
        if (!string.IsNullOrWhiteSpace(lastOnlineUser))
        {
            // lastOnlineUser обычно содержит имя солдата,
            // а не имя папки профиля, поэтому здесь
            // непосредственно папку по нему не ищем.
        }
        // 3. Ищем существующие профили.
        if (Directory.Exists(profilePath))
        {
            string? existingProfile =
                Directory
                    .GetDirectories(profilePath)
                    .Select(Path.GetFileName)
                    .Where(
                        name =>
                            !string.IsNullOrWhiteSpace(name)
                            && Regex.IsMatch(
                                name,
                                @"^\d+$"))
                    .OrderBy(
                        name =>
                            name)
                    .FirstOrDefault(
                        name =>
                            File.Exists(
                                Path.Combine(
                                    profilePath,
                                    name!,
                                    "Video.con")));
            if (!string.IsNullOrWhiteSpace(existingProfile))
            {
                return Path.Combine(
                    profilePath,
                    existingProfile,
                    "Video.con");
            }
        }
        throw new InvalidOperationException(
            "BF2142 profile Video.con was not found.");
    }
    private static string? GetSetting(
        string content,
        string settingName)
    {
        Match match =
            Regex.Match(
                content,
                $@"GlobalSettings\.{Regex.Escape(settingName)}\s+""([^""]*)""");
        if (!match.Success)
        {
            return null;
        }
        string value =
            match.Groups[1].Value.Trim();
        return value;
    }
}