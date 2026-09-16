using Avalonia.Controls;
using Avalonia.Interactivity;
using NBL;
using NBL.Models;
using NBL.Services;
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
    protected Game Game;
    public SettingsGear(Game game)
    {
        this.Game = game;
        InitializeComponent();
        this.DataContext =
            this;
        this.DisplayModeService =
            new DisplayModeService();
        this.LoadVideoSettings();
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
                    "Выключено",
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
                "Низкое",
                "Среднее",
                "Высокое",
                "Очень высокое"
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
            this.SaveVideoSettings();
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
}