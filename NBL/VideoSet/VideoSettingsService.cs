using System.Globalization;
using System.Text.RegularExpressions;
using NBL.Models;
namespace NBL.Services;
public class VideoSettingsService
{
    private readonly string _videoConfigPath;
    public VideoSettingsService(string videoConfigPath)
    {
        _videoConfigPath = videoConfigPath;
    }
    public GameVideoSettings Load()
    {
        var settings = new GameVideoSettings();
        if (!File.Exists(_videoConfigPath))
            return settings;
        foreach (string line in File.ReadAllLines(_videoConfigPath))
        {
            string[] parts = line.Split(' ', 2);
            if (parts.Length != 2)
                continue;
            string name = parts[0];
            string value = parts[1].Trim();
            switch (name)
            {
                case "VideoSettings.setResolution":
                    ParseResolution(value, settings);
                    break;
                case "VideoSettings.setTerrainQuality":
                    settings.TerrainQuality =
                        ParseInt(value, settings.TerrainQuality);
                    break;
                case "VideoSettings.setGeometryQuality":
                    settings.GeometryQuality =
                        ParseInt(value, settings.GeometryQuality);
                    break;
                case "VideoSettings.setLightingQuality":
                    settings.LightingQuality =
                        ParseInt(value, settings.LightingQuality);
                    break;
                case "VideoSettings.setDynamicLightingQuality":
                    settings.DynamicLightingQuality =
                        ParseInt(value, settings.DynamicLightingQuality);
                    break;
                case "VideoSettings.setDynamicShadowsQuality":
                    settings.DynamicShadowsQuality =
                        ParseInt(value, settings.DynamicShadowsQuality);
                    break;
                case "VideoSettings.setEffectsQuality":
                    settings.EffectsQuality =
                        ParseInt(value, settings.EffectsQuality);
                    break;
                case "VideoSettings.setTextureQuality":
                    settings.TextureQuality =
                        ParseInt(value, settings.TextureQuality);
                    break;
                case "VideoSettings.setTextureFilteringQuality":
                    settings.TextureFilteringQuality =
                        ParseInt(value, settings.TextureFilteringQuality);
                    break;
                case "VideoSettings.setAntialiasing":
                    settings.AntialiasingSamples =
                        ParseAntialiasing(value);
                    break;
                case "VideoSettings.setViewDistanceScale":
                    settings.ViewDistanceScale =
                        ParseFloat(
                            value,
                            settings.ViewDistanceScale);
                    break;
                case "VideoSettings.setUseBloom":
                    settings.UseBloom =
                        value == "1";
                    break;
            }
        }
        return settings;
    }
    public void Save(GameVideoSettings settings)
    {
        if (!File.Exists(_videoConfigPath))
            return;
        string[] lines =
            File.ReadAllLines(_videoConfigPath);
        SetLine(
            lines,
            "VideoSettings.setResolution",
            $"{settings.ResolutionWidth}x" +
            $"{settings.ResolutionHeight}@" +
            $"{settings.RefreshRate}Hz");
        SetLine(
            lines,
            "VideoSettings.setTerrainQuality",
            settings.TerrainQuality.ToString());
        SetLine(
            lines,
            "VideoSettings.setGeometryQuality",
            settings.GeometryQuality.ToString());
        SetLine(
            lines,
            "VideoSettings.setLightingQuality",
            settings.LightingQuality.ToString());
        SetLine(
            lines,
            "VideoSettings.setDynamicLightingQuality",
            settings.DynamicLightingQuality.ToString());
        SetLine(
            lines,
            "VideoSettings.setDynamicShadowsQuality",
            settings.DynamicShadowsQuality.ToString());
        SetLine(
            lines,
            "VideoSettings.setEffectsQuality",
            settings.EffectsQuality.ToString());
        SetLine(
            lines,
            "VideoSettings.setTextureQuality",
            settings.TextureQuality.ToString());
        SetLine(
            lines,
            "VideoSettings.setTextureFilteringQuality",
            settings.TextureFilteringQuality.ToString());
        SetLine(
    lines,
    "VideoSettings.setAntialiasing",
    settings.AntialiasingSamples switch
    {
        2 => "2Samples",
        4 => "4Samples",
        8 => "8Samples",
        _ => "Off"
    });
        SetLine(
            lines,
            "VideoSettings.setViewDistanceScale",
            settings.ViewDistanceScale.ToString(
                CultureInfo.InvariantCulture));
        SetLine(
            lines,
            "VideoSettings.setUseBloom",
            settings.UseBloom ? "1" : "0");
        File.WriteAllLines(
            _videoConfigPath,
            lines);
    }
    private static void SetLine(
        string[] lines,
        string settingName,
        string value)
    {
        string prefix =
            $"{settingName} ";
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith(
                    prefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                lines[i] =
                    $"{prefix}{value}";
                return;
            }
        }
    }
    private static void ParseResolution(
        string value,
        GameVideoSettings settings)
    {
        Match match =
            Regex.Match(
                value,
                @"^(\d+)x(\d+)@(\d+)Hz$");
        if (!match.Success)
            return;
        settings.ResolutionWidth =
            int.Parse(match.Groups[1].Value);
        settings.ResolutionHeight =
            int.Parse(match.Groups[2].Value);
        settings.RefreshRate =
            int.Parse(match.Groups[3].Value);
    }
    private static int ParseAntialiasing(
        string value)
    {
        Match match =
            Regex.Match(
                value,
                @"^(\d+)Samples$");
        if (!match.Success)
            return 0;
        return int.Parse(
            match.Groups[1].Value);
    }
    private static int ParseInt(
        string value,
        int defaultValue)
    {
        return int.TryParse(
            value,
            out int result)
                ? result
                : defaultValue;
    }
    private static float ParseFloat(
        string value,
        float defaultValue)
    {
        return float.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out float result)
                ? result
                : defaultValue;
    }
}