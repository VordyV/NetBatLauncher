
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Irihi.Avalonia.Shared.Contracts;
using NBL;
using NBL.Models;
using NBL.Services;
using NBLApp.Localization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace NBLApp.Forms;

public partial class SettingsGear : UserControl
{
    private readonly Game Game;
    private readonly DisplayModeService DisplayModeService = new();

    private VideoSettingsService? VideoSettingsService;
    private GameVideoSettings VideoSettings = new();
    private bool VideoSettingsAvailable;

    private AudioSettingsService? AudioSettingsService;
    private GameAudioSettings AudioSettings = new();
    private bool AudioSettingsAvailable;

    public string LogDirectory => Logger.LogDirectory;
    public string LauncherVersion => Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";

        base.OnDetachedFromVisualTree(e);
    }
    public SettingsGear(Game game)
    {
        Game = game;
        InitializeComponent();
        DataContext = this;

        LoadLanguageSettings();
        LoadVideoSettings();
        LoadAudioSettings();
        LoadGameLanguage();
        LoadGameKey();
    }

    private void LoadLanguageSettings()
    {
        var registry = Game.GetRegistry();
        if (!registry.HasSection("launcher"))
            registry.AddSection("launcher");

        ComboBoxLanguage.SelectedIndex = registry.Get("launcher", "language", "auto") switch
        {
            "ru-RU" => 1,
            "en-US" => 2,
            _ => 0
        };
    }

    private void SaveLanguageSettings()
    {
        string language = ComboBoxLanguage.SelectedIndex switch
        {
            1 => "ru-RU",
            2 => "en-US",
            _ => "auto"
        };

        var registry = Game.GetRegistry();
        if (!registry.HasSection("launcher"))
            registry.AddSection("launcher");

        registry.Set("launcher", "language", language);
        Locale.SetLanguage(language);
    }

    private void LoadGameLanguage()
    {
        GameLanguageComboBox.SelectedIndex =
            LanguageKeyService.GetLanguage() == "Russian" ? 1 : 0;
    }

    private bool SaveGameLanguage()
    {
        string language = GameLanguageComboBox.SelectedIndex == 1 ? "Russian" : "English";
        return LanguageKeyService.GetLanguage() == language ||
               LanguageKeyService.SetLanguage(language);
    }

    private void LoadGameKey()
    {
        TextBoxGameKeyMd5.Text = RegistryKeyService.GetKeyMd5() ?? "";
    }

    private void ButtonGenerateGameKey_OnClick(object? sender, RoutedEventArgs e)
    {
        if (RegistryKeyService.GenerateAndSetKey())
            LoadGameKey();
    }

    private async void ButtonCopyGameKeyHash_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            string? hash = TextBoxGameKeyMd5.Text;
            if (string.IsNullOrWhiteSpace(hash))
                return;

            await TopLevel.GetTopLevel(this)?.Clipboard?.SetTextAsync(hash)!;
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to copy game key hash", ex);
        }
    }

    private void DisableVideoSettings()
    {
        VideoSettingsAvailable = false;

        foreach (var control in new Control[]
        {
            ComboBoxResolution, ComboBoxRefreshRate,
            ComboBoxTerrain, ComboBoxGeometry, ComboBoxLighting,
            ComboBoxDynamicLighting, ComboBoxDynamicShadows,
            ComboBoxEffects, ComboBoxTextures, ComboBoxTextureFiltering,
            ComboBoxAntialiasing, SliderViewDistance, CheckBoxBloom
        })
            control.IsEnabled = false;

        TextBlockVideoSettingsStatus.IsVisible = true;
    }

    private void LoadVideoSettings()
    {
        try
        {
            VideoSettingsService = new VideoSettingsService(VideoSettingsPath.GetPath());
            VideoSettings = VideoSettingsService.Load();
            VideoSettingsAvailable = true;
            TextBlockVideoSettingsStatus.IsVisible = false;

            var modes = DisplayModeService.GetModes();
            var resolutions = modes.Select(x => $"{x.Width}x{x.Height}").Distinct().ToList();

            ComboBoxResolution.ItemsSource = resolutions;
            string current = $"{VideoSettings.ResolutionWidth}x{VideoSettings.ResolutionHeight}";
            ComboBoxResolution.SelectedIndex = Math.Max(0, resolutions.IndexOf(current));

            FillQualityComboBox(ComboBoxTerrain, VideoSettings.TerrainQuality);
            FillQualityComboBox(ComboBoxGeometry, VideoSettings.GeometryQuality);
            FillQualityComboBox(ComboBoxLighting, VideoSettings.LightingQuality);
            FillQualityComboBox(ComboBoxDynamicLighting, VideoSettings.DynamicLightingQuality);
            FillQualityComboBox(ComboBoxDynamicShadows, VideoSettings.DynamicShadowsQuality);
            FillQualityComboBox(ComboBoxEffects, VideoSettings.EffectsQuality);
            FillQualityComboBox(ComboBoxTextures, VideoSettings.TextureQuality);
            FillQualityComboBox(ComboBoxTextureFiltering, VideoSettings.TextureFilteringQuality);

            ComboBoxAntialiasing.ItemsSource = new[] { Locale.Get("Off"), "2x", "4x", "8x" };
            ComboBoxAntialiasing.SelectedIndex = VideoSettings.AntialiasingSamples switch
            {
                2 => 1,
                4 => 2,
                8 => 3,
                _ => 0
            };

            SliderViewDistance.Value = Math.Clamp(VideoSettings.ViewDistanceScale, 0f, 1f);
            CheckBoxBloom.IsChecked = VideoSettings.UseBloom;

            LoadRefreshRates();
        }
        catch
        {
            DisableVideoSettings();
        }
    }

    private void FillQualityComboBox(ComboBox comboBox, int value)
    {
        comboBox.ItemsSource = new[]
        {
            Locale.Get("Low"), Locale.Get("Medium"),
            Locale.Get("High"), Locale.Get("VeryHigh")
        };
        comboBox.SelectedIndex = Math.Clamp(value, 0, 3);
    }

    private void LoadRefreshRates()
    {
        if (!VideoSettingsAvailable)
            return;

        string? resolution = ComboBoxResolution.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(resolution))
            return;

        var rates = DisplayModeService.GetModes()
            .Where(x => $"{x.Width}x{x.Height}" == resolution)
            .Select(x => x.RefreshRate)
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        ComboBoxRefreshRate.ItemsSource = rates;
        int index = rates.IndexOf(VideoSettings.RefreshRate);
        ComboBoxRefreshRate.SelectedIndex = index >= 0 ? index : 0;
    }

    private void ComboBoxResolution_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (VideoSettingsAvailable)
            LoadRefreshRates();
    }

    private void SaveVideoSettings()
    {
        if (!VideoSettingsAvailable || VideoSettingsService == null)
            return;

        if (ComboBoxResolution.SelectedItem is string resolution)
        {
            var parts = resolution.Split('x');
            if (parts.Length == 2 &&
                int.TryParse(parts[0], out int width) &&
                int.TryParse(parts[1], out int height))
            {
                VideoSettings.ResolutionWidth = width;
                VideoSettings.ResolutionHeight = height;
            }
        }

        if (ComboBoxRefreshRate.SelectedItem is int rate)
            VideoSettings.RefreshRate = rate;

        VideoSettings.TerrainQuality = ComboBoxTerrain.SelectedIndex;
        VideoSettings.GeometryQuality = ComboBoxGeometry.SelectedIndex;
        VideoSettings.LightingQuality = ComboBoxLighting.SelectedIndex;
        VideoSettings.DynamicLightingQuality = ComboBoxDynamicLighting.SelectedIndex;
        VideoSettings.DynamicShadowsQuality = ComboBoxDynamicShadows.SelectedIndex;
        VideoSettings.EffectsQuality = ComboBoxEffects.SelectedIndex;
        VideoSettings.TextureQuality = ComboBoxTextures.SelectedIndex;
        VideoSettings.TextureFilteringQuality = ComboBoxTextureFiltering.SelectedIndex;

        VideoSettings.AntialiasingSamples = ComboBoxAntialiasing.SelectedIndex switch
        {
            1 => 2,
            2 => 4,
            3 => 8,
            _ => 0
        };

        VideoSettings.ViewDistanceScale = (float)SliderViewDistance.Value;
        VideoSettings.UseBloom = CheckBoxBloom.IsChecked == true;

        VideoSettingsService.Save(VideoSettings);
    }

    private void DisableAudioSettings()
    {
        AudioSettingsAvailable = false;

        foreach (var control in new Control[]
        {
            SliderEffectsVolume, SliderMusicVolume, SliderHelpVoiceVolume,
            ComboBoxSoundQuality, CheckBoxVoipEnabled,
            SliderVoipPlaybackVolume, SliderVoipCaptureVolume,
            CheckBoxVoipPushToTalk, CheckBoxVoipBoost
        })
            control.IsEnabled = false;

        TextBlockAudioSettingsStatus.IsVisible = true;
    }

    private void LoadAudioSettings()
    {
        try
        {
            AudioSettingsService = new AudioSettingsService(AudioSettingsPath.GetPath());
            AudioSettings = AudioSettingsService.Load();
            AudioSettingsAvailable = true;
            TextBlockAudioSettingsStatus.IsVisible = false;

            SliderEffectsVolume.Value = Math.Clamp(AudioSettings.EffectsVolume, 0f, 1f);
            SliderMusicVolume.Value = Math.Clamp(AudioSettings.MusicVolume, 0f, 1f);
            SliderHelpVoiceVolume.Value = Math.Clamp(AudioSettings.HelpVoiceVolume, 0f, 1f);

            ComboBoxSoundQuality.ItemsSource = new[]
            {
                Locale.Get("Low"), Locale.Get("Medium"), Locale.Get("High")
            };

            ComboBoxSoundQuality.SelectedIndex = AudioSettings.SoundQuality switch
            {
                "Medium" => 1,
                "High" => 2,
                _ => 0
            };

            CheckBoxVoipEnabled.IsChecked = AudioSettings.VoipEnabled;
            SliderVoipPlaybackVolume.Value = Math.Clamp(AudioSettings.VoipPlaybackVolume, 0f, 1f);
            SliderVoipCaptureVolume.Value = Math.Clamp(AudioSettings.VoipCaptureVolume, 0f, 1f);
            CheckBoxVoipPushToTalk.IsChecked = AudioSettings.VoipUsePushToTalk;
            CheckBoxVoipBoost.IsChecked = AudioSettings.VoipBoostEnabled;
        }
        catch
        {
            DisableAudioSettings();
        }
    }

    private void SaveAudioSettings()
    {
        if (!AudioSettingsAvailable || AudioSettingsService == null)
            return;

        AudioSettings.EffectsVolume = (float)SliderEffectsVolume.Value;
        AudioSettings.MusicVolume = (float)SliderMusicVolume.Value;
        AudioSettings.HelpVoiceVolume = (float)SliderHelpVoiceVolume.Value;
        AudioSettings.SoundQuality = ComboBoxSoundQuality.SelectedIndex switch
        {
            1 => "Medium",
            2 => "High",
            _ => "Low"
        };

        AudioSettings.VoipEnabled = CheckBoxVoipEnabled.IsChecked == true;
        AudioSettings.VoipPlaybackVolume = (float)SliderVoipPlaybackVolume.Value;
        AudioSettings.VoipCaptureVolume = (float)SliderVoipCaptureVolume.Value;
        AudioSettings.VoipUsePushToTalk = CheckBoxVoipPushToTalk.IsChecked == true;
        AudioSettings.VoipBoostEnabled = CheckBoxVoipBoost.IsChecked == true;

        AudioSettingsService.Save(AudioSettings);
    }

    private void UpdateVolumeText(TextBlock textBlock, double value)
    {
        textBlock.Text = $"{Math.Round(value * 100)}%";
    }

    private void SliderEffectsVolume_OnValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        => UpdateVolumeText(TextBlockEffectsVolume, e.NewValue);

    private void SliderMusicVolume_OnValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        => UpdateVolumeText(TextBlockMusicVolume, e.NewValue);

    private void SliderHelpVoiceVolume_OnValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        => UpdateVolumeText(TextBlockHelpVoiceVolume, e.NewValue);

    private void SliderVoipPlaybackVolume_OnValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        => UpdateVolumeText(TextBlockVoipPlaybackVolume, e.NewValue);

    private void SliderVoipCaptureVolume_OnValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        => UpdateVolumeText(TextBlockVoipCaptureVolume, e.NewValue);

    private async void OpenLogs_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel != null)
                await topLevel.Launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(LogDirectory));
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to open log directory", ex);
        }
    }

    private async void ButtonSave_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            SaveLanguageSettings();
            if (!SaveGameLanguage())
                return;

            SaveVideoSettings();
            SaveAudioSettings();

            if (DataContext is IDialogContext ctx)
                ctx.Close();
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to save settings", ex);
        }
    }

    private void ButtonCancel_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is IDialogContext ctx)
            ctx.Close();
    }

    private async void ButtonUninstall_OnClick(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner)
            return;

        var cancel = new Button { Content = Locale.Get("UninstallGameCancel"), MinWidth = 90, IsCancel = true };
        var confirm = new Button
        {
            Content = Locale.Get("UninstallGameConfirmButton"),
            MinWidth = 90,
            Background = new SolidColorBrush(Color.Parse("#C62828")),
            Foreground = Brushes.White,
            IsDefault = true
        };

        var dialog = new Window
        {
            Title = Locale.Get("UninstallGameTitle"),
            Width = 400,
            Height = 190,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 18,
                Children =
                {
                    new TextBlock
                    {
                        Text = Locale.Get("UninstallGameConfirm"),
                        TextWrapping = TextWrapping.Wrap
                    },
                    new StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                        Spacing = 10,
                        Children = { cancel, confirm }
                    }
                }
            }
        };

        cancel.Click += (_, _) => dialog.Close(false);
        confirm.Click += (_, _) => dialog.Close(true);

        if (!await dialog.ShowDialog<bool>(owner))
            return;

        try
        {
            await Game.UninstallAsync();
            if (DataContext is IDialogContext ctx)
                ctx.Close();
        }
        catch (Exception ex)
        {
            var ok = new Button
            {
                Content = Locale.Get("CommonOk"),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
            };

            var error = new Window
            {
                Title = Locale.Get("UninstallGameError"),
                Width = 400,
                Height = 160,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new StackPanel
                {
                    Margin = new Thickness(20),
                    Spacing = 15,
                    Children =
                    {
                        new TextBlock { Text = ex.Message, TextWrapping = TextWrapping.Wrap },
                        ok
                    }
                }
            };

            ok.Click += (_, _) => error.Close();
            await error.ShowDialog(owner);
        }
    }
}
