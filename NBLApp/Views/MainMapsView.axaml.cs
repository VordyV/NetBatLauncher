using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using NBL;
using NBL.Services;
using NBLApp.Controls;
using NBLApp.Localization;
using NBLNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace NBLApp.Views;

public partial class MainMapsView : View
{
    private readonly List<MapInfo> _maps = new();
    private MapInfo? _selectedMap;
    private string? _selectedMode;
    private int _selectedSize;
    private MapLocalization _localization = new();

    private static readonly Dictionary<string, string> ModeNames = new()
    {
        ["gpm_cq"] = "MapsModeConquest",
        ["gpm_ti"] = "MapsModeTitan",
        ["gpm_nv"] = "MapsModeNoVehicles",
        ["gpm_coop"] = "MapsModeCoop",
        ["gpm_sp"] = "MapsModeSingleplayer",
        ["gpm_sl"] = "MapsModeAssaultLines"
    };
    private static readonly HashSet<string> VanillaMaps = new(StringComparer.OrdinalIgnoreCase)
{
    "Camp_Gibraltar",
    "Cerbere_Landing",
    "Fall_of_Berlin",
    "Liberation_of_Leipzig",
    "Minsk",
    "Shuhia_Taiba",
    "Suez_Canal",
    "Tunis_Harbor",
    "Verdun",
    "Wake_Island_2142",
    "Highway_Tampa",
    "Strike_at_Karkand",
    "Operation_Blue_Pearl",
    "Sidi_Power_Plant",
    "Belgrade",
    "Bridge_at_Remagen",
    "Operation_Shingle",
    "Molokai",
    "Port_Bavaria",
    "Yellow_knife",
    };
    private enum MapFilter
    {
        All,
        Vanilla,
        Mods
    }

    private MapFilter _mapFilter = MapFilter.All;
    public MainMapsView()
    {
        InitializeComponent();
        UpdateTexts();
        RefreshFilterButtons();
    }

    public MainMapsView(Launcher launcher, ViewPresenter<Launcher> presenter, object? arg)
     : base(launcher, presenter, arg)
    {
        InitializeComponent();
        UpdateTexts();
        RefreshFilterButtons();

        Cursor = new Cursor(StandardCursorType.Hand);
        Loaded += (_, _) =>
        {
            LocaleProvider.Instance.LanguageChanged -= OnLanguageChanged;
            LocaleProvider.Instance.LanguageChanged += OnLanguageChanged;

            LoadMaps();
        };

        Unloaded += (_, _) =>
            LocaleProvider.Instance.LanguageChanged -= OnLanguageChanged;
    }

    private void LoadMaps()
    {
        _maps.Clear();

        if (Arg is not NBL.Game game)
            return;

        string gamePath = game.GetPath();

        string localizationPath = Path.Combine(
            gamePath, "mods", "bf2142", "Localization", "strings.csv");

        _localization = MapLocalization.Load(localizationPath);

        Logger.Info($"Map localization path: {localizationPath}");
        Logger.Info($"Map localization file exists: {System.IO.File.Exists(localizationPath)}");
        Logger.Info($"Map localization entries: {_localization.Count}");

        string levels = Path.Combine(
            gamePath, "mods", "bf2142", "Levels");

        if (Directory.Exists(levels))
            foreach (string dir in Directory.GetDirectories(levels))
            {
                string id = Path.GetFileName(dir);
                string info = Path.Combine(dir, "Info");

                if (!Directory.Exists(info))
                    continue;

                var modes = Directory.GetFiles(info, "*_menuMap.png")
                    .Select(f => Path.GetFileName(f).Split('_'))
                    .Where(p => p.Length == 4 && int.TryParse(p[2], out _))
                    .GroupBy(p => $"{p[0]}_{p[1]}")
                    .ToDictionary(g => g.Key,
                        g => g.Select(p => int.Parse(p[2])).Order().ToList());

                if (modes.Count == 0)
                    continue;

                string name = id.Replace('_', ' ');
                string description = "";
                string locId = "";
                try
                {
                    var xml = XDocument.Load(Path.Combine(info, $"{id}.desc"));
                    name = (xml.Root?.Element("name")?.Value.Trim() ?? name).Replace('_', ' ');
                    var briefing = xml.Root?.Element("briefing");

                    locId = briefing?.Attribute("locid")?.Value ?? "";
                    description = briefing?.Value.Trim() ?? "";
                    Logger.Info($"Map: {id}, LocId: '{locId}'");
                }
                catch { }

                _maps.Add(new MapInfo(id, name, description, locId, info, modes, VanillaMaps.Contains(id)));
            }

        MapsCount.Text = _maps.Count.ToString();
        ApplySearch();
    }
    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            UpdateTexts();

            if (_selectedMap != null)
            {
                DetailDescription.Text = _localization.Get(
                    _selectedMap.LocId,
                    _selectedMap.Description);

                RefreshModeButtons();
            }
        });
    }
    private void ApplySearch()
    {
        string query = SearchBox.Text?.Trim() ?? "";

        var filteredMaps = _maps.Where(m =>
            (_mapFilter == MapFilter.All ||
             (_mapFilter == MapFilter.Vanilla && m.IsVanilla) ||
             (_mapFilter == MapFilter.Mods && !m.IsVanilla)) &&
            (m.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
             m.Id.Contains(query, StringComparison.OrdinalIgnoreCase))
        ).ToList();

        MapTilesPanel.Children.Clear();

        foreach (var map in filteredMaps)
            MapTilesPanel.Children.Add(CreateTile(map));

        MapsCount.Text = filteredMaps.Count.ToString();
        EmptyState.IsVisible = filteredMaps.Count == 0;
    }

    private Border CreateTile(MapInfo map)
    {
        string mode = map.Modes.Keys.First();
        int size = map.Modes[mode].Contains(16) ? 16 : map.Modes[mode][0];
        string preview = Path.Combine(map.Info, $"{mode}_{size}_preview.png");

        var content = new StackPanel { Spacing = 8 };

        content.Children.Add(new Border
        {
            Height = 145,
            Background = Brush.Parse("#22000000"),
            Child = System.IO.File.Exists(preview)
                ? new Image { Source = new Bitmap(preview), Stretch = Stretch.Uniform }
                : null
        });

        content.Children.Add(new TextBlock
        {
            Text = map.Name,
            FontSize = 17,
            FontWeight = FontWeight.SemiBold
        });

        content.Children.Add(new TextBlock
        {
            Text = Locale.Get(map.IsVanilla
                ? "MapsTypeVanilla"
                : "MapsTypeMod"),
            FontSize = 11,
            FontWeight = FontWeight.Bold,
            Opacity = 0.65
        });

        var tile = new Border
        {
            Width = 270,
            Margin = new Thickness(0, 0, 12, 12),
            Padding = new Thickness(10),
            Background = Brush.Parse("#55000000"),
            BorderBrush = Brush.Parse("#33FFFFFF"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = content
        };

        tile.PointerPressed += (_, _) => ShowMap(map);

        tile.PointerEntered += (_, _) =>
        {
            tile.Background = Brush.Parse("#774A4A4A");
            tile.BorderBrush = Brush.Parse("#88FFFFFF");
        };

        tile.PointerExited += (_, _) =>
        {
            tile.Background = Brush.Parse("#55000000");
            tile.BorderBrush = Brush.Parse("#33FFFFFF");
        };
        return tile;
    }


    private void ShowMap(MapInfo map)
    {
        _selectedMap = map;
        CatalogPanel.IsVisible = false;
        DetailPanel.IsVisible = true;

        DetailName.Text = map.Name;
        DetailDescription.Text = _localization.Get(map.LocId,map.Description);

        SelectMode(map.Modes.Keys.First());
    }

    private void SelectMode(string mode)
    {
        _selectedMode = mode;
        var sizes = _selectedMap!.Modes[mode];
        _selectedSize = sizes.Contains(32) ? 32 : sizes[0];

        RefreshModeButtons();

        SizeButtonsPanel.Children.Clear();

        foreach (int size in sizes)
            SizeButtonsPanel.Children.Add(CreateOptionButton(
                size.ToString(),
                size == _selectedSize,
                () => SelectSize(size)));

        UpdateMapImage();
    }

    private void SelectSize(int size)
    {
        _selectedSize = size;

        string mode = _selectedMode!;
        var sizes = _selectedMap!.Modes[mode];

        SizeButtonsPanel.Children.Clear();

        foreach (int value in sizes)
            SizeButtonsPanel.Children.Add(CreateOptionButton(
                value.ToString(),
                value == size,
                () => SelectSize(value)));

        UpdateMapImage();
    }

    private Button CreateOptionButton(string text, bool selected, Action click)
    {
        var button = new Button
        {
            Content = text,
            Padding = new Thickness(12, 7),
            Margin = new Thickness(0, 0, 6, 6),
            Background = Brush.Parse(selected ? "#665A9FEA" : "#33000000"),
            BorderBrush = Brush.Parse(selected ? "#FF5A9FEA" : "#33FFFFFF"),
            BorderThickness = new Thickness(1)
        };

        button.Click += (_, _) => click();
        return button;
    }

    private void UpdateMapImage()
    {
        if (_selectedMap == null || _selectedMode == null)
            return;

        string path = Path.Combine(_selectedMap.Info,
            $"{_selectedMode}_{_selectedSize}_menuMap.png");

        DetailImage.Source = System.IO.File.Exists(path) ? new Bitmap(path) : null;
    }
    private void RefreshModeButtons()
    {
        if (_selectedMap == null)
            return;

        ModeButtonsPanel.Children.Clear();

        foreach (string key in _selectedMap.Modes.Keys)
        {
            string name = ModeNames.TryGetValue(key, out string? resourceKey)
                ? Locale.Get(resourceKey)
                : key;

            ModeButtonsPanel.Children.Add(CreateOptionButton(
                name,
                key == _selectedMode,
                () => SelectMode(key)));
        }
    }
    private void RefreshFilterButtons()
    {
        MapFiltersPanel.Children.Clear();

        MapFiltersPanel.Children.Add(CreateOptionButton(
            Locale.Get("MapsFilterAll"),
            _mapFilter == MapFilter.All,
            () =>
            {
                _mapFilter = MapFilter.All;
                RefreshFilterButtons();
                ApplySearch();
            }));

        MapFiltersPanel.Children.Add(CreateOptionButton(
            Locale.Get("MapsFilterVanilla"),
            _mapFilter == MapFilter.Vanilla,
            () =>
            {
                _mapFilter = MapFilter.Vanilla;
                RefreshFilterButtons();
                ApplySearch();
            }));

        MapFiltersPanel.Children.Add(CreateOptionButton(
            Locale.Get("MapsFilterMods"),
            _mapFilter == MapFilter.Mods,
            () =>
            {
                _mapFilter = MapFilter.Mods;
                RefreshFilterButtons();
                ApplySearch();
            }));
    }
    private void UpdateTexts()
    {
        MapsTitle.Text = Locale.Get("Maps");
        SearchBox.Watermark = Locale.Get("MapsSearchPlaceholder");
        EmptyState.Text = Locale.Get("MapsEmpty");
        BackButton.Content = "← " + Locale.Get("MapsBack");
        DescriptionTitle.Text = Locale.Get("MapsDescription");
        GameModeTitle.Text = Locale.Get("MapsGameMode");
        MapSizeTitle.Text = Locale.Get("MapsSize");
    }
    private void Back_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        DetailPanel.IsVisible = false;
        CatalogPanel.IsVisible = true;
    }
    private void SearchBox_OnTextChanged(object? sender,
        TextChangedEventArgs e) => ApplySearch();

    private record MapInfo(
        string Id,
        string Name,
        string Description,
        string LocId,
        string Info,
        Dictionary<string, List<int>> Modes,
        bool IsVanilla);
}
