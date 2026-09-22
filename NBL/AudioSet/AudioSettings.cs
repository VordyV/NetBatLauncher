using System;
using System.Globalization;
using System.IO;

namespace NBL.Services;

public class AudioSettings
{
    public bool VoipEnabled { get; set; }
    public bool VoipBoostEnabled { get; set; }
    public bool VoipUsePushToTalk { get; set; }

    public double VoipPlaybackVolume { get; set; }
    public double VoipCaptureVolume { get; set; }
    public double VoipCaptureThreshold { get; set; }

    public string Provider { get; set; } = "software";
    public string SoundQuality { get; set; } = "High";

    public double EffectsVolume { get; set; }
    public double MusicVolume { get; set; }
    public double HelpVoiceVolume { get; set; }

    public bool EnglishOnlyVoices { get; set; }
    public bool EnableEAX { get; set; }

    public static AudioSettings Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException(
                "Audio.con не найден.",
                path);

        AudioSettings settings = new();

        foreach (string line in File.ReadAllLines(path))
        {
            string trimmed = line.Trim();

            if (trimmed.StartsWith("AudioSettings.setVoipEnabled"))
                settings.VoipEnabled = GetBool(trimmed);

            else if (trimmed.StartsWith("AudioSettings.setVoipPlaybackVolume"))
                settings.VoipPlaybackVolume = GetDouble(trimmed);

            else if (trimmed.StartsWith("AudioSettings.setVoipCaptureVolume"))
                settings.VoipCaptureVolume = GetDouble(trimmed);

            else if (trimmed.StartsWith("AudioSettings.setVoipCaptureThreshold"))
                settings.VoipCaptureThreshold = GetDouble(trimmed);

            else if (trimmed.StartsWith("AudioSettings.setVoipBoostEnabled"))
                settings.VoipBoostEnabled = GetBool(trimmed);

            else if (trimmed.StartsWith("AudioSettings.setVoipUsePushToTalk"))
                settings.VoipUsePushToTalk = GetBool(trimmed);

            else if (trimmed.StartsWith("AudioSettings.setProvider"))
                settings.Provider = GetString(trimmed);

            else if (trimmed.StartsWith("AudioSettings.setSoundQuality"))
                settings.SoundQuality = GetString(trimmed);

            else if (trimmed.StartsWith("AudioSettings.setEffectsVolume"))
                settings.EffectsVolume = GetDouble(trimmed);

            else if (trimmed.StartsWith("AudioSettings.setMusicVolume"))
                settings.MusicVolume = GetDouble(trimmed);

            else if (trimmed.StartsWith("AudioSettings.setHelpVoiceVolume"))
                settings.HelpVoiceVolume = GetDouble(trimmed);

            else if (trimmed.StartsWith("AudioSettings.setEnglishOnlyVoices"))
                settings.EnglishOnlyVoices = GetBool(trimmed);

            else if (trimmed.StartsWith("AudioSettings.setEnableEAX"))
                settings.EnableEAX = GetBool(trimmed);
        }

        return settings;
    }

    public void Save(string path)
    {
        string[] lines = File.ReadAllLines(path);

        for (int i = 0; i < lines.Length; i++)
        {
            string trimmed = lines[i].TrimStart();

            if (trimmed.StartsWith("AudioSettings.setVoipEnabled"))
                lines[i] = $"AudioSettings.setVoipEnabled {(VoipEnabled ? 1 : 0)}";

            else if (trimmed.StartsWith("AudioSettings.setVoipPlaybackVolume"))
                lines[i] = $"AudioSettings.setVoipPlaybackVolume {FormatDouble(VoipPlaybackVolume)}";

            else if (trimmed.StartsWith("AudioSettings.setVoipCaptureVolume"))
                lines[i] = $"AudioSettings.setVoipCaptureVolume {FormatDouble(VoipCaptureVolume)}";

            else if (trimmed.StartsWith("AudioSettings.setVoipCaptureThreshold"))
                lines[i] = $"AudioSettings.setVoipCaptureThreshold {FormatDouble(VoipCaptureThreshold)}";

            else if (trimmed.StartsWith("AudioSettings.setVoipBoostEnabled"))
                lines[i] = $"AudioSettings.setVoipBoostEnabled {(VoipBoostEnabled ? 1 : 0)}";

            else if (trimmed.StartsWith("AudioSettings.setVoipUsePushToTalk"))
                lines[i] = $"AudioSettings.setVoipUsePushToTalk {(VoipUsePushToTalk ? 1 : 0)}";

            else if (trimmed.StartsWith("AudioSettings.setProvider"))
                lines[i] = $"AudioSettings.setProvider \"{Provider}\"";

            else if (trimmed.StartsWith("AudioSettings.setSoundQuality"))
                lines[i] = $"AudioSettings.setSoundQuality \"{SoundQuality}\"";

            else if (trimmed.StartsWith("AudioSettings.setEffectsVolume"))
                lines[i] = $"AudioSettings.setEffectsVolume {FormatDouble(EffectsVolume)}";

            else if (trimmed.StartsWith("AudioSettings.setMusicVolume"))
                lines[i] = $"AudioSettings.setMusicVolume {FormatDouble(MusicVolume)}";

            else if (trimmed.StartsWith("AudioSettings.setHelpVoiceVolume"))
                lines[i] = $"AudioSettings.setHelpVoiceVolume {FormatDouble(HelpVoiceVolume)}";

            else if (trimmed.StartsWith("AudioSettings.setEnglishOnlyVoices"))
                lines[i] = $"AudioSettings.setEnglishOnlyVoices {(EnglishOnlyVoices ? 1 : 0)}";

            else if (trimmed.StartsWith("AudioSettings.setEnableEAX"))
                lines[i] = $"AudioSettings.setEnableEAX {(EnableEAX ? 1 : 0)}";
        }

        File.WriteAllLines(path, lines);
    }

    private static bool GetBool(string line)
    {
        string value = GetValue(line);

        return value == "1" ||
               value.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    private static double GetDouble(string line)
    {
        string value = GetValue(line);

        return double.Parse(
            value,
            CultureInfo.InvariantCulture);
    }

    private static string GetString(string line)
    {
        return GetValue(line).Trim('"');
    }

    private static string GetValue(string line)
    {
        int space = line.IndexOf(' ');

        if (space < 0)
            return string.Empty;

        return line[(space + 1)..].Trim();
    }

    private static string FormatDouble(double value)
    {
        return value.ToString(
            "0.######",
            CultureInfo.InvariantCulture);
    }
}