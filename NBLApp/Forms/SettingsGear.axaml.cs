using Avalonia.Controls;
using Avalonia.Interactivity;
using NBL;
using NBL.Models;
using NBL.Services;
using NBLApp.Localization;
using Irihi.Avalonia.Shared.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
namespace NBLApp.Forms;
public partial class SettingsGear : UserControl
{
    private VideoSettingsService? VideoSettingsService;
    private GameVideoSettings VideoSettings = new();
    private DisplayModeService DisplayModeService;
    private bool VideoSettingsAvailable;

    private AudioSettingsService? AudioSettingsService;
    private GameAudioSettings AudioSettings = new();
    private bool AudioSettingsAvailable;

    protected Game Game;
   
    public string LauncherSettingsText =>
        Locale.Get("LauncherSettings");

    public string LanguageLauncherText =>
        Locale.Get("LanguageLauncher");

    public string AutomaticText =>
        Locale.Get("Auto");

    public string RussianText =>
        Locale.Get("Russian");

    public string EnglishText =>
        Locale.Get("English");

    public string VideoSettingsText =>
        Locale.Get("VideoSettings");

    public string ResolutionText =>
        Locale.Get("Resolution");

    public string RefreshRateText =>
        Locale.Get("RefreshRate");

    public string TerrainText =>
        Locale.Get("Terrain");

    public string GeometryText =>
        Locale.Get("Geometry");

    public string LightingText =>
        Locale.Get("Lighting");

    public string DynamicLightingText =>
        Locale.Get("DynamicLighting");

    public string DynamicShadowsText =>
        Locale.Get("DynamicShadows");

    public string EffectsText =>
        Locale.Get("Effects");

    public string TexturesText =>
        Locale.Get("Textures");

    public string TextureFilteringText =>
        Locale.Get("TextureFiltering");

    public string AntialiasingText =>
        Locale.Get("Antialiasing");

    public string ViewDistanceText =>
        Locale.Get("ViewDistance");

    public string BloomText =>
        Locale.Get("Bloom");

    public string AudioSettingsText =>
        Locale.Get("AudioSettings");

    public string EffectsVolumeText =>
        Locale.Get("EffectsVolume");

    public string MusicVolumeText =>
        Locale.Get("MusicVolume");

    public string HelpVoiceVolumeText =>
        Locale.Get("HelpVoiceVolume");

    public string SoundQualityText =>
        Locale.Get("SoundQuality");

    public string VoipText =>
        Locale.Get("Voip");

    public string VoipPlaybackVolumeText =>
        Locale.Get("VoipPlaybackVolume");

    public string VoipCaptureVolumeText =>
        Locale.Get("VoipCaptureVolume");

    public string VoipPushToTalkText =>
        Locale.Get("VoipPushToTalk");

    public string VoipBoostText =>
        Locale.Get("VoipBoost");

    public string SaveText =>
        Locale.Get("Save");

    public string CancelText =>
        Locale.Get("Cancel");

    public string SettingsUnavailableText =>
        Locale.Get("SettingsUnavailable");

    public string SettingsText =>
        Locale.Get("Settings");

    public string GraphicsText =>
        Locale.Get("Graphics");

    public string AudioText =>
        Locale.Get("Audio");

    public string EnabledText =>
        Locale.Get("Enabled");
    public string ControlSettingsText =>
        Locale.Get("ControlSettings");
    public string GameLanguageText =>
        Locale.Get("GameLanguage");

    public SettingsGear(Game game)
    {
        this.Game = game;

        InitializeComponent();

        this.LoadLanguageSettings();

        this.DataContext =
     this;

        this.DisplayModeService =
            new DisplayModeService();

        this.LoadVideoSettings();
        this.LoadAudioSettings();
        this.LoadGameLanguage();
    }
    private void Locale_LanguageChanged(
        object? sender,
        EventArgs e)
    {
    }
    private void LoadLanguageSettings()
    {
        Configurator registry =
            this.Game.GetRegistry();

        if (!registry.HasSection("launcher"))
        {
            registry.AddSection("launcher");
        }

        string language =
            registry.Get(
                "launcher",
                "language",
                "auto");

        this.ComboBoxLanguage.SelectedIndex =
            language switch
            {
                "ru-RU" => 1,
                "en-US" => 2,
                _ => 0
            };
    }

    private void SaveLanguageSettings()
    {
        string language =
            this.ComboBoxLanguage.SelectedIndex switch
            {
                1 => "ru-RU",
                2 => "en-US",
                _ => "auto"
            };

        Configurator registry =
            this.Game.GetRegistry();

        if (!registry.HasSection("launcher"))
        {
            registry.AddSection("launcher");
        }

        registry.Set(
            "launcher",
            "language",
            language);

        Locale.SetLanguage(language);
    }
    private void LoadGameLanguage()
    {
        string language =
            GameLanguageService.GetLanguage();

        GameLanguageComboBox.SelectedIndex =
            language == "Russian" ? 1 : 0;
    }
    private void SaveGameLanguage()
    {
        string language =
            this.GameLanguageComboBox.SelectedIndex switch
            {
                1 => "Russian",
                _ => "English"
            };

        GameLanguageService.SetLanguage(
            language);
    }
    private void DisableVideoSettings()
    {
        this.VideoSettingsAvailable = false;
        this.ComboBoxResolution.IsEnabled = false;
        this.ComboBoxRefreshRate.IsEnabled = false;
        this.ComboBoxTerrain.IsEnabled = false;
        this.ComboBoxGeometry.IsEnabled = false;
        this.ComboBoxLighting.IsEnabled = false;
        this.ComboBoxDynamicLighting.IsEnabled = false;
        this.ComboBoxDynamicShadows.IsEnabled = false;
        this.ComboBoxEffects.IsEnabled = false;
        this.ComboBoxTextures.IsEnabled = false;
        this.ComboBoxTextureFiltering.IsEnabled = false;
        this.ComboBoxAntialiasing.IsEnabled = false;
        this.SliderViewDistance.IsEnabled = false;
        this.CheckBoxBloom.IsEnabled = false;
        this.TextBlockVideoSettingsStatus.IsVisible = true;
    }
    private void LoadVideoSettings()
    {
        try
        {
            string videoSettingsPath =
                VideoSettingsPath.GetPath();
            this.VideoSettingsService =
                new VideoSettingsService(
                    videoSettingsPath);
            this.VideoSettings =
                this.VideoSettingsService.Load();
            this.VideoSettingsAvailable =
                true;
            this.TextBlockVideoSettingsStatus.IsVisible =
                false;
            List<DisplayMode> displayModes =
                this.DisplayModeService.GetModes();
            List<string> resolutions =
                displayModes
                    .Select(x => $"{x.Width}x{x.Height}")
                    .Distinct()
                    .ToList();
            this.ComboBoxResolution.ItemsSource =
                resolutions;
            string currentResolution =
                $"{this.VideoSettings.ResolutionWidth}x" +
                $"{this.VideoSettings.ResolutionHeight}";
            int resolutionIndex =
                resolutions.IndexOf(
                    currentResolution);
            this.ComboBoxResolution.SelectedIndex =
                resolutionIndex >= 0
                    ? resolutionIndex
                    : 0;
            this.FillQualityComboBox(
                this.ComboBoxTerrain,
                this.VideoSettings.TerrainQuality);
            this.FillQualityComboBox(
                this.ComboBoxGeometry,
                this.VideoSettings.GeometryQuality);
            this.FillQualityComboBox(
                this.ComboBoxLighting,
                this.VideoSettings.LightingQuality);
            this.FillQualityComboBox(
                this.ComboBoxDynamicLighting,
                this.VideoSettings.DynamicLightingQuality);
            this.FillQualityComboBox(
                this.ComboBoxDynamicShadows,
                this.VideoSettings.DynamicShadowsQuality);
            this.FillQualityComboBox(
                this.ComboBoxEffects,
                this.VideoSettings.EffectsQuality);
            this.FillQualityComboBox(
                this.ComboBoxTextures,
                this.VideoSettings.TextureQuality);
            this.FillQualityComboBox(
                this.ComboBoxTextureFiltering,
                this.VideoSettings.TextureFilteringQuality);
            this.ComboBoxAntialiasing.ItemsSource =
                new[]
                {
                    Locale.Get("Off"),
                    "2x",
                    "4x",
                    "8x"
                };
            this.ComboBoxAntialiasing.SelectedIndex =
                this.VideoSettings.AntialiasingSamples switch
                {
                    2 => 1,
                    4 => 2,
                    8 => 3,
                    _ => 0
                };
            this.SliderViewDistance.Value =
                Math.Clamp(
                    this.VideoSettings.ViewDistanceScale,
                    0.0f,
                    1.0f);
            this.CheckBoxBloom.IsChecked =
                this.VideoSettings.UseBloom;
            this.LoadRefreshRates();
        }
        catch
        {
            this.DisableVideoSettings();
        }
    }
    private void DisableAudioSettings()
    {
        this.AudioSettingsAvailable = false;
        this.SliderEffectsVolume.IsEnabled = false;
        this.SliderMusicVolume.IsEnabled = false;
        this.SliderHelpVoiceVolume.IsEnabled = false;
        this.ComboBoxSoundQuality.IsEnabled = false;
        this.CheckBoxVoipEnabled.IsEnabled = false;
        this.SliderVoipPlaybackVolume.IsEnabled = false;
        this.SliderVoipCaptureVolume.IsEnabled = false;
        this.CheckBoxVoipPushToTalk.IsEnabled = false;
        this.CheckBoxVoipBoost.IsEnabled = false;
        this.TextBlockAudioSettingsStatus.IsVisible = true;
    }

    private void LoadRefreshRates()
    {
        if (!this.VideoSettingsAvailable)
            return;
        string? resolution =
            this.ComboBoxResolution.SelectedItem
                as string;
        if (string.IsNullOrWhiteSpace(resolution))
            return;
        List<DisplayMode> displayModes =
            this.DisplayModeService.GetModes()
                .Where(x =>
                    $"{x.Width}x{x.Height}" ==
                    resolution)
                .ToList();
        List<int> refreshRates =
            displayModes
                .Select(x => x.RefreshRate)
                .Distinct()
                .OrderBy(x => x)
                .ToList();
        this.ComboBoxRefreshRate.ItemsSource =
            refreshRates;
        int index =
            refreshRates.IndexOf(
                this.VideoSettings.RefreshRate);
        this.ComboBoxRefreshRate.SelectedIndex =
            index >= 0
                ? index
                : 0;
    }
    private void FillQualityComboBox(
        ComboBox comboBox,
        int value)
    {
        comboBox.ItemsSource =
    new[]
    {
        Locale.Get("Low"),
        Locale.Get("Medium"),
        Locale.Get("High"),
        Locale.Get("VeryHigh")
    };
        comboBox.SelectedIndex =
            Math.Clamp(
                value,
                0,
                3);
    }
    private void SaveVideoSettings()
    {
        if (!this.VideoSettingsAvailable)
            return;
        if (this.VideoSettingsService == null)
            return;
        string? resolution =
            this.ComboBoxResolution.SelectedItem
                as string;
        if (!string.IsNullOrWhiteSpace(resolution))
        {
            string[] parts =
                resolution.Split('x');
            if (parts.Length == 2 &&
                int.TryParse(
                    parts[0],
                    out int width) &&
                int.TryParse(
                    parts[1],
                    out int height))
            {
                this.VideoSettings.ResolutionWidth =
                    width;
                this.VideoSettings.ResolutionHeight =
                    height;
            }
        }
        if (this.ComboBoxRefreshRate.SelectedItem
            is int refreshRate)
        {
            this.VideoSettings.RefreshRate =
                refreshRate;
        }
        this.VideoSettings.TerrainQuality =
            this.ComboBoxTerrain.SelectedIndex;
        this.VideoSettings.GeometryQuality =
            this.ComboBoxGeometry.SelectedIndex;
        this.VideoSettings.LightingQuality =
            this.ComboBoxLighting.SelectedIndex;
        this.VideoSettings.DynamicLightingQuality =
            this.ComboBoxDynamicLighting.SelectedIndex;
        this.VideoSettings.DynamicShadowsQuality =
            this.ComboBoxDynamicShadows.SelectedIndex;
        this.VideoSettings.EffectsQuality =
            this.ComboBoxEffects.SelectedIndex;
        this.VideoSettings.TextureQuality =
            this.ComboBoxTextures.SelectedIndex;
        this.VideoSettings.TextureFilteringQuality =
            this.ComboBoxTextureFiltering.SelectedIndex;
        this.VideoSettings.AntialiasingSamples =
            this.ComboBoxAntialiasing.SelectedIndex switch
            {
                1 => 2,
                2 => 4,
                3 => 8,
                _ => 0
            };
        this.VideoSettings.ViewDistanceScale =
            (float)this.SliderViewDistance.Value;
        this.VideoSettings.UseBloom =
            this.CheckBoxBloom.IsChecked == true;
        this.VideoSettingsService.Save(
            this.VideoSettings);
    }
    private void ComboBoxResolution_OnSelectionChanged(
        object? sender,
        SelectionChangedEventArgs e)
    {
        if (!this.VideoSettingsAvailable)
            return;
        this.LoadRefreshRates();
    }
    private void ButtonSave_OnClick(
     object? sender,
     RoutedEventArgs e)
    {
        try
        {
            this.SaveLanguageSettings();
            this.SaveGameLanguage();
            this.SaveVideoSettings();
            this.SaveAudioSettings();
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception);
        }

        if (this.DataContext
            is IDialogContext ctx)
        {
            ctx.Close();
        }
    }
    private void ButtonCancel_OnClick(
        object? sender,
        RoutedEventArgs e)
    {
        if (this.DataContext
            is IDialogContext ctx)
        {
            ctx.Close();
        }
    }
    private void LoadAudioSettings()
    {
        try
        {
            string audioSettingsPath =
                AudioSettingsPath.GetPath();

            this.AudioSettingsService =
                new AudioSettingsService(
                    audioSettingsPath);

            this.AudioSettings =
                this.AudioSettingsService.Load();

            this.AudioSettingsAvailable =
                true;

            this.TextBlockAudioSettingsStatus.IsVisible =
                false;

            this.SliderEffectsVolume.Value =
                Math.Clamp(
                    this.AudioSettings.EffectsVolume,
                    0.0f,
                    1.0f);

            this.SliderMusicVolume.Value =
                Math.Clamp(
                    this.AudioSettings.MusicVolume,
                    0.0f,
                    1.0f);

            this.SliderHelpVoiceVolume.Value =
                Math.Clamp(
                    this.AudioSettings.HelpVoiceVolume,
                    0.0f,
                    1.0f);

            this.ComboBoxSoundQuality.ItemsSource =
                new[]
                {
        Locale.Get("Low"),
        Locale.Get("Medium"),
        Locale.Get("High"),
        Locale.Get("VeryHigh")
                };

            this.ComboBoxSoundQuality.SelectedIndex =
                this.AudioSettings.SoundQuality switch
                {
                    "Medium" => 1,
                    "High" => 2,
                    "VeryHigh" => 3,
                    _ => 0
                };

            this.CheckBoxVoipEnabled.IsChecked =
                this.AudioSettings.VoipEnabled;

            this.SliderVoipPlaybackVolume.Value =
                Math.Clamp(
                    this.AudioSettings.VoipPlaybackVolume,
                    0.0f,
                    1.0f);

            this.SliderVoipCaptureVolume.Value =
                Math.Clamp(
                    this.AudioSettings.VoipCaptureVolume,
                    0.0f,
                    1.0f);

            this.CheckBoxVoipPushToTalk.IsChecked =
                this.AudioSettings.VoipUsePushToTalk;

            this.CheckBoxVoipBoost.IsChecked =
                this.AudioSettings.VoipBoostEnabled;
        }
        catch
        {
            this.DisableAudioSettings();
        }
    }

    private void SaveAudioSettings()
    {
        if (!this.AudioSettingsAvailable)
            return;

        if (this.AudioSettingsService == null)
            return;

        this.AudioSettings.EffectsVolume =
            (float)this.SliderEffectsVolume.Value;

        this.AudioSettings.MusicVolume =
            (float)this.SliderMusicVolume.Value;

        this.AudioSettings.HelpVoiceVolume =
            (float)this.SliderHelpVoiceVolume.Value;

        this.AudioSettings.SoundQuality =
            this.ComboBoxSoundQuality.SelectedIndex switch
            {
                1 => "Medium",
                2 => "High",
                3 => "VeryHigh",
                _ => "Low"
            };

        this.AudioSettings.VoipEnabled =
            this.CheckBoxVoipEnabled.IsChecked == true;

        this.AudioSettings.VoipPlaybackVolume =
            (float)this.SliderVoipPlaybackVolume.Value;

        this.AudioSettings.VoipCaptureVolume =
            (float)this.SliderVoipCaptureVolume.Value;

        this.AudioSettings.VoipUsePushToTalk =
            this.CheckBoxVoipPushToTalk.IsChecked == true;

        this.AudioSettings.VoipBoostEnabled =
            this.CheckBoxVoipBoost.IsChecked == true;

        this.AudioSettingsService.Save(
            this.AudioSettings);
    }

    private void UpdateVolumeText(
    TextBlock textBlock,
    double value)
    {
        textBlock.Text =
            $"{Math.Round(value * 100)}%";
    }

    private void SliderEffectsVolume_OnValueChanged(
    object? sender,
    Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        this.UpdateVolumeText(
            this.TextBlockEffectsVolume,
            e.NewValue);
    }

    private void SliderMusicVolume_OnValueChanged(
        object? sender,
        Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        this.UpdateVolumeText(
            this.TextBlockMusicVolume,
            e.NewValue);
    }

    private void SliderHelpVoiceVolume_OnValueChanged(
        object? sender,
        Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        this.UpdateVolumeText(
            this.TextBlockHelpVoiceVolume,
            e.NewValue);
    }

    private void SliderVoipPlaybackVolume_OnValueChanged(
        object? sender,
        Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        this.UpdateVolumeText(
            this.TextBlockVoipPlaybackVolume,
            e.NewValue);
    }

    private void SliderVoipCaptureVolume_OnValueChanged(
        object? sender,
        Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        this.UpdateVolumeText(
            this.TextBlockVoipCaptureVolume,
            e.NewValue);
    }
}