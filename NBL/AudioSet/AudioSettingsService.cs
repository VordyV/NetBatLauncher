using NBL.Models;
using System;
using System.Globalization;
using System.IO;

namespace NBL.Services;

public class AudioSettingsService
{
    private readonly string Path;

    public AudioSettingsService(string path)
    {
        this.Path = path;
    }

    public GameAudioSettings Load()
    {
        GameAudioSettings settings = new();

        foreach (string line in File.ReadAllLines(this.Path))
        {
            string trimmed = line.Trim();

            if (trimmed.StartsWith("AudioSettings.setVoipEnabled"))
                settings.VoipEnabled = GetBool(trimmed);
            else if (trimmed.StartsWith("AudioSettings.setVoipPlaybackVolume"))
                settings.VoipPlaybackVolume = GetFloat(trimmed);
            else if (trimmed.StartsWith("AudioSettings.setVoipCaptureVolume"))
                settings.VoipCaptureVolume = GetFloat(trimmed);
            else if (trimmed.StartsWith("AudioSettings.setVoipCaptureThreshold"))
                settings.VoipCaptureThreshold = GetFloat(trimmed);
            else if (trimmed.StartsWith("AudioSettings.setVoipBoostEnabled"))
                settings.VoipBoostEnabled = GetBool(trimmed);
            else if (trimmed.StartsWith("AudioSettings.setVoipUsePushToTalk"))
                settings.VoipUsePushToTalk = GetBool(trimmed);
            else if (trimmed.StartsWith("AudioSettings.setProvider"))
                settings.Provider = GetString(trimmed);
            else if (trimmed.StartsWith("AudioSettings.setSoundQuality"))
                settings.SoundQuality = GetString(trimmed);
            else if (trimmed.StartsWith("AudioSettings.setEffectsVolume"))
                settings.EffectsVolume = GetFloat(trimmed);
            else if (trimmed.StartsWith("AudioSettings.setMusicVolume"))
                settings.MusicVolume = GetFloat(trimmed);
            else if (trimmed.StartsWith("AudioSettings.setHelpVoiceVolume"))
                settings.HelpVoiceVolume = GetFloat(trimmed);
            else if (trimmed.StartsWith("AudioSettings.setEnglishOnlyVoices"))
                settings.EnglishOnlyVoices = GetBool(trimmed);
            else if (trimmed.StartsWith("AudioSettings.setEnableEAX"))
                settings.EnableEAX = GetBool(trimmed);
        }

        return settings;
    }

    public void Save(GameAudioSettings settings)
    {
        string[] lines = File.ReadAllLines(this.Path);

        for (int i = 0; i < lines.Length; i++)
        {
            string trimmed = lines[i].TrimStart();

            if (trimmed.StartsWith("AudioSettings.setVoipEnabled"))
                lines[i] = $"AudioSettings.setVoipEnabled {(settings.VoipEnabled ? 1 : 0)}";
            else if (trimmed.StartsWith("AudioSettings.setVoipPlaybackVolume"))
                lines[i] = $"AudioSettings.setVoipPlaybackVolume {FormatFloat(settings.VoipPlaybackVolume)}";
            else if (trimmed.StartsWith("AudioSettings.setVoipCaptureVolume"))
                lines[i] = $"AudioSettings.setVoipCaptureVolume {FormatFloat(settings.VoipCaptureVolume)}";
            else if (trimmed.StartsWith("AudioSettings.setVoipCaptureThreshold"))
                lines[i] = $"AudioSettings.setVoipCaptureThreshold {FormatFloat(settings.VoipCaptureThreshold)}";
            else if (trimmed.StartsWith("AudioSettings.setVoipBoostEnabled"))
                lines[i] = $"AudioSettings.setVoipBoostEnabled {(settings.VoipBoostEnabled ? 1 : 0)}";
            else if (trimmed.StartsWith("AudioSettings.setVoipUsePushToTalk"))
                lines[i] = $"AudioSettings.setVoipUsePushToTalk {(settings.VoipUsePushToTalk ? 1 : 0)}";
            else if (trimmed.StartsWith("AudioSettings.setProvider"))
                lines[i] = $"AudioSettings.setProvider \"{settings.Provider}\"";
            else if (trimmed.StartsWith("AudioSettings.setSoundQuality"))
                lines[i] = $"AudioSettings.setSoundQuality \"{settings.SoundQuality}\"";
            else if (trimmed.StartsWith("AudioSettings.setEffectsVolume"))
                lines[i] = $"AudioSettings.setEffectsVolume {FormatFloat(settings.EffectsVolume)}";
            else if (trimmed.StartsWith("AudioSettings.setMusicVolume"))
                lines[i] = $"AudioSettings.setMusicVolume {FormatFloat(settings.MusicVolume)}";
            else if (trimmed.StartsWith("AudioSettings.setHelpVoiceVolume"))
                lines[i] = $"AudioSettings.setHelpVoiceVolume {FormatFloat(settings.HelpVoiceVolume)}";
            else if (trimmed.StartsWith("AudioSettings.setEnglishOnlyVoices"))
                lines[i] = $"AudioSettings.setEnglishOnlyVoices {(settings.EnglishOnlyVoices ? 1 : 0)}";
            else if (trimmed.StartsWith("AudioSettings.setEnableEAX"))
                lines[i] = $"AudioSettings.setEnableEAX {(settings.EnableEAX ? 1 : 0)}";
        }

        File.WriteAllLines(this.Path, lines);
    }

    private static bool GetBool(string line)
    {
        return GetValue(line) == "1";
    }

    private static float GetFloat(string line)
    {
        return float.Parse(
            GetValue(line),
            CultureInfo.InvariantCulture);
    }

    private static string GetString(string line)
    {
        return GetValue(line).Trim('"');
    }

    private static string GetValue(string line)
    {
        int index = line.IndexOf(' ');

        if (index < 0)
            return string.Empty;

        return line[(index + 1)..].Trim();
    }

    private static string FormatFloat(float value)
    {
        return value.ToString(
            "0.######",
            CultureInfo.InvariantCulture);
    }
}