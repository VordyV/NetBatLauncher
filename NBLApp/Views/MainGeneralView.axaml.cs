
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using NBL;
using NBL.Services;
using NBLApp.Controls;
using NBLApp.Localization;
using NBLNet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ursa.Controls;

namespace NBLApp.Views;

public partial class MainGeneralView : View
{
    private const string GameId = "bf2142";
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(30) };
    private readonly List<ServerRow> _servers = new();
    private bool _isRefreshing;
    private string _selectedClient = "*";
    private bool _isInitialized;
    private readonly DispatcherTimer _ageTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private DateTime? _lastUpdated;
    private bool _refreshFailed;

    public MainGeneralView() => InitializeComponent();

    public MainGeneralView(Launcher launcher, ViewPresenter<Launcher> viewPresenter, object? arg)
     : base(launcher, viewPresenter, arg)
    {
        InitializeComponent();
        _isInitialized = true;

        _timer.Tick += async (_, _) => await RefreshServersAsync();
        _ageTimer.Tick += (_, _) => UpdateRefreshInfo();
        Loaded += async (_, _) => { _timer.Start(); _ageTimer.Start(); await RefreshServersAsync(); };
        Unloaded += (_, _) => { _timer.Stop(); _ageTimer.Stop(); };
    }

    private async Task RefreshServersAsync()
    {
        if (_isRefreshing || Launcher == null) return;

        _isRefreshing = true;
        _refreshFailed = false;
        RefreshInfo.Text = Locale.Get("ServerMonitorFetching");

        try
        {
            var clients = await Launcher.GetAvailableClientsAsync(GameId);
            string previous = _selectedClient;

            ClientFilter.Items.Clear();
            ClientFilter.Items.Add(new ComboBoxItem
            {
                Content = Locale.Get("ServerFilterAllClients"),
                Tag = "*"
            });

            foreach (var client in clients)
                ClientFilter.Items.Add(new ComboBoxItem
                {
                    Content = client.Name,
                    Tag = client.Ident
                });

            ClientFilter.SelectedIndex = Enumerable.Range(0, ClientFilter.Items.Count)
                .FirstOrDefault(i => (ClientFilter.Items[i] as ComboBoxItem)?.Tag?.ToString() == previous);

            _selectedClient = (ClientFilter.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "*";

            var servers = new List<(ApiServer Server, string ClientName, string ClientId)>();

            foreach (var client in clients)
            {
                try
                {
                    var items = await Launcher.GetServersAsync(GameId, client.Ident);
                    servers.AddRange(items.Select(s => (s, client.Name, client.Ident)));
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Failed to load servers for {client.Ident}: {ex.Message}");
                }
            }

            using var semaphore = new SemaphoreSlim(5);

            var rows = await Task.WhenAll(servers.Select(async entry =>
            {
                await semaphore.WaitAsync();

                try
                {
                    var row = new ServerRow
                    {
                        Name = string.IsNullOrWhiteSpace(entry.Server.Name) ? entry.Server.Address : entry.Server.Name!,
                        Client = entry.ClientName,
                        ClientId = entry.ClientId
                    };

                    try
                    {
                        var gameServer = new GameServer
                        {
                            Address = entry.Server.Address,
                            QueryPort = checked((ushort)entry.Server.QueryPort),
                            Name = entry.Server.Name
                        };

                        var stopwatch = Stopwatch.StartNew();
                        await gameServer.LoadData(3000);
                        stopwatch.Stop();

                        var status = gameServer.Status;
                        var info = status?.Info;

                        if (info != null)
                        {
                            row.Name = GetInfo(info, "hostname", "sv_hostname") ?? row.Name;
                            row.Map = FormatMap(GetInfo(info, "mapname", "map", "map_name") ?? "—");
                            row.Mode = FormatMode(GetInfo(info, "gametype", "gamemode", "game_mode", "mode") ?? "—");

                            int players = ParseInfo(info, "numplayers", "num_players") ?? status!.Players.Count;
                            int? maxPlayers = ParseInfo(info, "maxplayers", "max_players");

                            row.PlayerCount = players;
                            row.MaxPlayers = maxPlayers ?? 0;
                            row.Players = maxPlayers.HasValue ? $"{players} / {maxPlayers}" : players.ToString();
                            row.PingValue = stopwatch.ElapsedMilliseconds;
                            row.Ping = $"{row.PingValue} ms";
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Debug($"Server query failed: {entry.Server.Address}:{entry.Server.QueryPort} - {ex.Message}");
                    }

                    return row;
                }
                finally
                {
                    semaphore.Release();
                }
            }));

            _servers.Clear();
            _servers.AddRange(rows.OrderBy(r => r.Name));

            ApplyFilters();
            UpdateSummary();
            _lastUpdated = DateTime.Now;
            UpdateRefreshInfo();
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to refresh server monitor.", ex);
            _refreshFailed = true;
            UpdateRefreshInfo();
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private static string? GetInfo(Dictionary<string, string> info, params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = info.FirstOrDefault(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).Value;
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }

        return null;
    }

    private static int? ParseInfo(Dictionary<string, string> info, params string[] keys)
        => int.TryParse(GetInfo(info, keys), out int result) ? result : null;

    private void ApplyFilters()
    {
        string search = SearchBox.Text?.Trim() ?? "";

        var filtered = _servers.Where(s =>
            (_selectedClient == "*" || s.ClientId.Equals(_selectedClient, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrEmpty(search) ||
             s.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
             s.Map.Contains(search, StringComparison.OrdinalIgnoreCase) ||
             s.Client.Contains(search, StringComparison.OrdinalIgnoreCase) ||
             s.Mode.Contains(search, StringComparison.OrdinalIgnoreCase))).ToList();

        ServerList.Children.Clear();

        foreach (var server in filtered)
            ServerList.Children.Add(CreateServerRow(server));

        EmptyState.IsVisible = filtered.Count == 0;
    }

    private static Border CreateServerRow(ServerRow server)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("3*,2*,1.2*,1.2*,1.2*"),
            ColumnSpacing = 8
        };

        var name = new StackPanel { Spacing = 3 };
        name.Children.Add(new TextBlock
        {
            Text = server.Name,
            FontWeight = FontWeight.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        name.Children.Add(new TextBlock
        {
            Text = server.Client,
            FontSize = 11,
            Opacity = 0.6
        });

        grid.Children.Add(name);
        AddCell(grid, server.Map, 1);
        AddCell(grid, server.Mode, 2);
        AddCell(grid, server.Players, 3);
        AddCell(grid, server.Ping, 4);

        return new Border
        {
            Background = Brush.Parse("#44000000"),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 9),
            Margin = new Thickness(0, 2),
            Child = grid
        };
    }

    private void UpdateRefreshInfo()
    {
        if (_lastUpdated == null) return;

        int seconds = Math.Max(0, (int)(DateTime.Now - _lastUpdated.Value).TotalSeconds);

        string key = _refreshFailed
            ? "ServerMonitorUpdateError"
            : "ServerMonitorUpdated";

        RefreshInfo.Text = string.Format(Locale.Get(key), seconds);
    }

    private static void AddCell(Grid grid, string value, int column)
    {
        var text = new TextBlock
        {
            Text = value,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        Grid.SetColumn(text, column);
        grid.Children.Add(text);
    }

    private void UpdateSummary()
    {
        int players = _servers.Sum(s => s.PlayerCount);
        int max = _servers.Sum(s => s.MaxPlayers);

        ServerCount.Text = _servers.Count.ToString();
        PlayerCount.Text = max > 0 ? $"{players} / {max}" : players.ToString();
        PopulatedCount.Text = _servers.Count(s => s.PlayerCount > 0).ToString();
        FreeSlots.Text = max > 0 ? Math.Max(0, max - players).ToString() : "—";
    }

    private void SearchBox_OnTextChanged(object? sender, TextChangedEventArgs e)
        => ApplyFilters();

    private void ClientFilter_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized || ClientFilter == null)
            return;

        _selectedClient = (ClientFilter.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "*";
        ApplyFilters();
    }

    private static string FormatMap(string map)
    => map.Replace('_', ' ');

    private static string FormatMode(string mode)
        => mode.ToLowerInvariant() switch
        {
            "gpm_nv" => "No Vehicle",
            "gpm_cq" => "Conquest",
            "gpm_ti" => "Titan",
            _ => mode
        };

    private sealed class ServerRow
    {
        public string Name { get; set; } = "";
        public string Client { get; set; } = "";
        public string ClientId { get; set; } = "";
        public string Map { get; set; } = "—";
        public string Mode { get; set; } = "—";
        public string Players { get; set; } = "—";
        public string Ping { get; set; } = "—";
        public int PlayerCount { get; set; }
        public int MaxPlayers { get; set; }
        public long PingValue { get; set; }
    }
    public void RefreshLocalization()
    {
        if (!_isInitialized)
            return;

        MonitorTitle.Text = Locale.Get("ServerMonitorTitle");
        MonitorDescription.Text = Locale.Get("ServerMonitorDescription");

        ServerCountLabel.Text = Locale.Get("ServerCount");
        PlayerCountLabel.Text = Locale.Get("PlayerCount");
        PopulatedCountLabel.Text = Locale.Get("PopulatedCount");
        FreeSlotsLabel.Text = Locale.Get("FreeSlots");

        SearchBox.Watermark = Locale.Get("ServerSearchPlaceholder");
        PopulatedFilter.Content = Locale.Get("ServerFilterPopulated");
        FavoritesFilter.Content = Locale.Get("ServerFilterFavorites");

        ColumnName.Text = Locale.Get("ServerColumnName");
        ColumnPassword.Text = Locale.Get("ServerPassword");
        ColumnRanked.Text = Locale.Get("ServerRankedColumn");
        ColumnMap.Text = Locale.Get("ServerColumnMap");
        ColumnMode.Text = Locale.Get("ServerColumnMode");
        ColumnPlayers.Text = Locale.Get("ServerColumnPlayers");
        ColumnPing.Text = Locale.Get("ServerColumnPing");

        if (ClientFilter.Items.Count > 0 &&
            ClientFilter.Items[0] is ComboBoxItem item)
            item.Content = Locale.Get("ServerFilterAllClients");

        UpdateDynamicFilters();
        ApplyFilters();

        RefreshInfo.Text = Locale.Get(
            _isRefreshing ? "ServerMonitorFetching" :
            _lastUpdated.HasValue ? "ServerMonitorUpdated" :
            "ServerMonitorLoading");

        if (_lastUpdated.HasValue && !_isRefreshing)
            UpdateRefreshInfo();

        EmptyState.Text = Locale.Get("ServerMonitorLoading");
    }
    private void PopulatedFilter_OnChanged(object? sender, RoutedEventArgs e)
    {
        if (!_isInitialized)
            return;

        _onlyPopulated = PopulatedFilter.IsChecked == true;
        ApplyFilters();
    }

    private void FavoritesFilter_OnChanged(object? sender, RoutedEventArgs e)
    {
        if (!_isInitialized)
            return;

        _favoritesOnly = FavoritesFilter.IsChecked == true;
        ApplyFilters();
    }

    private async Task RefreshServersAsync()
    {
        if (_isRefreshing || Launcher == null)
            return;

        _isRefreshing = true;
        _refreshFailed = false;
        RefreshInfo.Text = Locale.Get("ServerMonitorFetching");

        try
        {
            var clients = await Launcher.GetAvailableClientsAsync(GameId);

            PopulateComboBox(
                ClientFilter,
                clients.Select(c => (c.Name, c.Ident)),
                _selectedClient,
                Locale.Get("ServerFilterAllClients"));

            _selectedClient = GetSelectedTag(ClientFilter);

            var servers = new List<(ApiServer Server, string ClientName, string ClientId)>();

            foreach (var client in clients)
            {
                try
                {
                    var items = await Launcher.GetServersAsync(GameId, client.Ident);
                    servers.AddRange(items.Select(s => (s, client.Name, client.Ident)));
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Failed to load servers for {client.Ident}: {ex.Message}");
                }
            }

            using var semaphore = new SemaphoreSlim(5);

            var rows = await Task.WhenAll(servers.Select(async entry =>
            {
                await semaphore.WaitAsync();

                try
                {
                    var row = new ServerRow
                    {
                        Name = string.IsNullOrWhiteSpace(entry.Server.Name)
                            ? entry.Server.Address
                            : entry.Server.Name!,
                        Client = entry.ClientName,
                        ClientId = entry.ClientId,
                        Address = entry.Server.Address,
                        QueryPort = entry.Server.QueryPort
                    };

                    try
                    {
                        var gameServer = new GameServer
                        {
                            Address = entry.Server.Address,
                            QueryPort = checked((ushort)entry.Server.QueryPort),
                            Name = entry.Server.Name
                        };

                        var stopwatch = Stopwatch.StartNew();
                        await gameServer.LoadData(3000);
                        stopwatch.Stop();

                        var status = gameServer.Status;
                        var info = status?.Info;

                        if (info == null || info.Count == 0)
                            return null;

                        row.Name = GetInfo(info, "hostname", "sv_hostname") ?? row.Name;
                        row.Map = FormatMap(GetInfo(info, "mapname", "map", "map_name") ?? "—");
                        row.Mode = GetInfo(info, "gametype", "gamemode", "game_mode", "mode") ?? "—";

                        row.HasPassword = ParsePassword(GetInfo(info, "password"));
                        row.IsRanked = ParsePassword(GetInfo(info, "bf2142_ranked"));

                        int players = ParseInfo(info, "numplayers", "num_players")
                            ?? status!.Players.Count;

                        int? maxPlayers = ParseInfo(info, "maxplayers", "max_players");

                        row.PlayerCount = players;
                        row.MaxPlayers = maxPlayers ?? 0;
                        row.Players = maxPlayers.HasValue
                            ? $"{players} / {maxPlayers}"
                            : players.ToString();

                        row.PingValue = stopwatch.ElapsedMilliseconds;
                        row.Ping = $"{row.PingValue} ms";

                        row.IsFavorite = _favorites.Contains(row.FavoriteKey);

                        return row;
                    }
                    catch (Exception ex)
                    {
                        Logger.Debug(
                            $"Server query failed: {entry.Server.Address}:{entry.Server.QueryPort} - {ex.Message}");

                        return null;
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            }));

            _servers.Clear();
            _servers.AddRange(rows.OfType<ServerRow>().OrderBy(r => r.Name));

            UpdateDynamicFilters();
            ApplyFilters();
            UpdateSummary();

            _lastUpdated = DateTime.Now;
            UpdateRefreshInfo();
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to refresh server monitor.", ex);
            _refreshFailed = true;

            if (_servers.Count == 0)
            {
                EmptyState.Text = Locale.Get("ServerMonitorEmpty");
                EmptyState.IsVisible = true;
            }

            UpdateRefreshInfo();
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private void UpdateDynamicFilters()
    {
        PopulateComboBox(
            ModeFilter,
            _servers
                .Select(s => s.Mode)
                .Where(s => !string.IsNullOrWhiteSpace(s) && s != "—")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s)
                .Select(s => (FormatMode(s), s)),
            _selectedMode,
            Locale.Get("ServerFilterAllModes"));

        PopulateComboBox(
            MapFilter,
            _servers
                .Select(s => s.Map)
                .Where(s => !string.IsNullOrWhiteSpace(s) && s != "—")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s)
                .Select(s => (s, s)),
            _selectedMap,
            Locale.Get("ServerFilterAllMaps"));

        PopulateComboBox(
            PasswordFilter,
            new[]
            {
                (Locale.Get("ServerPasswordProtected"), "1"),
                (Locale.Get("ServerPasswordFree"), "0")
            },
            _selectedPassword,
            Locale.Get("ServerFilterAnyPassword"));

        _selectedMode = GetSelectedTag(ModeFilter);
        _selectedMap = GetSelectedTag(MapFilter);
        _selectedPassword = GetSelectedTag(PasswordFilter);
    }

    private void PopulateComboBox(
        ComboBox comboBox,
        IEnumerable<(string Label, string Value)> options,
        string selectedValue,
        string allLabel)
    {
        bool previousState = _updatingFilters;
        _updatingFilters = true;

        try
        {
            comboBox.Items.Clear();

            comboBox.Items.Add(new ComboBoxItem
            {
                Content = allLabel,
                Tag = "*"
            });

            foreach (var option in options)
            {
                comboBox.Items.Add(new ComboBoxItem
                {
                    Content = option.Label,
                    Tag = option.Value
                });
            }

            int index = Enumerable.Range(0, comboBox.Items.Count)
                .FirstOrDefault(i =>
                    (comboBox.Items[i] as ComboBoxItem)?.Tag?.ToString() == selectedValue);

            comboBox.SelectedIndex = index;
        }
        finally
        {
            _updatingFilters = previousState;
        }
    }

    private static string GetSelectedTag(ComboBox comboBox)
        => (comboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "*";

    private static string? GetInfo(Dictionary<string, string> info, params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = info.FirstOrDefault(
                x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).Value;

            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private static int? ParseInfo(Dictionary<string, string> info, params string[] keys)
        => int.TryParse(GetInfo(info, keys), out int result) ? result : null;

    private static bool? ParsePassword(string? value)
        => value?.Trim().ToLowerInvariant() switch
        {
            "0" or "false" => false,
            "1" or "true" => true,
            _ => null
        };

    private void ApplyFilters()
    {
        string search = SearchBox.Text?.Trim() ?? "";

        var filtered = _servers.Where(s =>
            (_selectedClient == "*" ||
             s.ClientId.Equals(_selectedClient, StringComparison.OrdinalIgnoreCase)) &&

            (_selectedMode == "*" ||
             s.Mode.Equals(_selectedMode, StringComparison.OrdinalIgnoreCase)) &&

            (_selectedMap == "*" ||
             s.Map.Equals(_selectedMap, StringComparison.OrdinalIgnoreCase)) &&

            (_selectedPassword == "*" ||
             (_selectedPassword == "1" && s.HasPassword == true) ||
             (_selectedPassword == "0" && s.HasPassword == false)) &&

            (!_onlyPopulated || s.PlayerCount > 0) &&

            (!_favoritesOnly || s.IsFavorite) &&

            (string.IsNullOrEmpty(search) ||
             s.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
             s.Map.Contains(search, StringComparison.OrdinalIgnoreCase) ||
             s.Client.Contains(search, StringComparison.OrdinalIgnoreCase) ||
             FormatMode(s.Mode).Contains(search, StringComparison.OrdinalIgnoreCase) ||
             s.Address.Contains(search, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        ServerList.Children.Clear();

        foreach (var server in filtered)
            ServerList.Children.Add(CreateServerRow(server));

        EmptyState.IsVisible = filtered.Count == 0;
    }

    private Border CreateServerRow(ServerRow server)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(
                "2.5*,1.35*,1.15*,2*,1.2*,1*,1*"),
            ColumnSpacing = 8
        };

        var name = new StackPanel { Spacing = 3 };

        var titleLine = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 5
        };

        var favoriteButton = new Button
        {
            Content = server.IsFavorite ? "★" : "☆",
            Foreground = server.IsFavorite
                ? Brush.Parse("#FFC107")
                : Brush.Parse("#999999"),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(2, 0),
            MinWidth = 22,
            Height = 22,
            FontSize = 17,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };

        favoriteButton.Click += (_, _) => ToggleFavorite(server);
        titleLine.Children.Add(favoriteButton);

        titleLine.Children.Add(new TextBlock
        {
            Text = server.Name,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        });

        name.Children.Add(titleLine);

        name.Children.Add(new TextBlock
        {
            Text = server.Client,
            FontSize = 11,
            Opacity = 0.6
        });

        grid.Children.Add(name);

        string passwordText = server.HasPassword switch
        {
            true => $"🔒 {Locale.Get("ServerPasswordProtected")}",
            false => $"🔓 {Locale.Get("ServerPasswordFree")}",
            null => "—"
        };

        AddCell(grid, passwordText, 1);

        string rankedText = server.IsRanked switch
        {
            true => $"🏅 {Locale.Get("ServerRanked")}",
            false => Locale.Get("ServerUnranked"),
            null => "—"
        };

        AddCell(grid, rankedText, 2);

        AddCell(grid, $"🗺 {server.Map}", 3);
        AddCell(grid, FormatMode(server.Mode), 4);
        AddCell(grid, server.Players, 5);
        AddCell(grid, server.Ping, 6);

        return new Border
        {
            Background = Brush.Parse("#44000000"),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 9),
            Margin = new Thickness(0, 2),
            Child = grid
        };
    }

    private void ToggleFavorite(ServerRow server)
    {
        if (!_favorites.Add(server.FavoriteKey))
            _favorites.Remove(server.FavoriteKey);

        server.IsFavorite = _favorites.Contains(server.FavoriteKey);

        SaveFavorites();
        ApplyFilters();
    }

    private static HashSet<string> LoadFavorites()
    {
        try
        {
            if (!System.IO.File.Exists(FavoritesPath))
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var favorites = JsonSerializer.Deserialize<List<string>>(
                System.IO.File.ReadAllText(FavoritesPath));

            return new HashSet<string>(
                favorites ?? new List<string>(),
                StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to load server favorites: {ex.Message}");
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void SaveFavorites()
    {
        try
        {
            string? directory = Path.GetDirectoryName(FavoritesPath);

            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            System.IO.File.WriteAllText(
                FavoritesPath,
                JsonSerializer.Serialize(_favorites.OrderBy(x => x).ToList()));
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to save server favorites.", ex);
        }
    }

    private void UpdateRefreshInfo()
    {
        if (_lastUpdated == null)
            return;

        int seconds = Math.Max(
            0,
            (int)(DateTime.Now - _lastUpdated.Value).TotalSeconds);

        string key = _refreshFailed
            ? "ServerMonitorUpdateError"
            : "ServerMonitorUpdated";

        RefreshInfo.Text = string.Format(Locale.Get(key), seconds);
    }

    private static void AddCell(Grid grid, string value, int column)
    {
        var text = new TextBlock
        {
            Text = value,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        Grid.SetColumn(text, column);
        grid.Children.Add(text);
    }

    private void UpdateSummary()
    {
        int players = _servers.Sum(s => s.PlayerCount);
        int max = _servers.Sum(s => s.MaxPlayers);

        this.FindControl<TextBlock>("ServerCountValue")!.Text =
            _servers.Count.ToString();

        this.FindControl<TextBlock>("PlayerCountValue")!.Text =
            max > 0 ? $"{players} / {max}" : players.ToString();

        this.FindControl<TextBlock>("PopulatedCountValue")!.Text =
            _servers.Count(s => s.PlayerCount > 0).ToString();

        this.FindControl<TextBlock>("FreeSlotsValue")!.Text =
            max > 0 ? Math.Max(0, max - players).ToString() : "—";
    }

    private void SearchBox_OnTextChanged(object? sender, TextChangedEventArgs e)
        => ApplyFilters();

    private void ClientFilter_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized || _updatingFilters)
            return;

        _selectedClient = GetSelectedTag(ClientFilter);
        ApplyFilters();
    }

    private void ModeFilter_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized || _updatingFilters)
            return;

        _selectedMode = GetSelectedTag(ModeFilter);
        ApplyFilters();
    }

    private void MapFilter_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized || _updatingFilters)
            return;

        _selectedMap = GetSelectedTag(MapFilter);
        ApplyFilters();
    }

    private void PasswordFilter_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized || _updatingFilters)
            return;

        _selectedPassword = GetSelectedTag(PasswordFilter);
        ApplyFilters();
    }

    private static string FormatMap(string map)
        => map.Replace('_', ' ');

    private static string FormatMode(string mode)
        => mode.ToLowerInvariant() switch
        {
            "gpm_nv" => Locale.Get("ServerModeNoVehicle"),
            "gpm_cq" => Locale.Get("ServerModeConquest"),
            "gpm_ti" => Locale.Get("ServerModeTitan"),
            _ => mode
        };

    private void InitializeFilters()
    {
        PopulateComboBox(
            ClientFilter,
            Enumerable.Empty<(string Label, string Value)>(),
            "*",
            Locale.Get("ServerFilterAllClients"));

        PopulateComboBox(
            ModeFilter,
            Enumerable.Empty<(string Label, string Value)>(),
            "*",
            Locale.Get("ServerFilterAllModes"));

        PopulateComboBox(
            MapFilter,
            Enumerable.Empty<(string Label, string Value)>(),
            "*",
            Locale.Get("ServerFilterAllMaps"));

        PopulateComboBox(
            PasswordFilter,
            new[]
            {
                (Locale.Get("ServerPasswordProtected"), "1"),
                (Locale.Get("ServerPasswordFree"), "0")
            },
            "*",
            Locale.Get("ServerFilterAnyPassword"));

        _selectedClient = "*";
        _selectedMode = "*";
        _selectedMap = "*";
        _selectedPassword = "*";
    }

    private sealed class ServerRow
    {
        public string Name { get; set; } = "";
        public string Client { get; set; } = "";
        public string ClientId { get; set; } = "";
        public string Address { get; set; } = "";
        public int QueryPort { get; set; }

        public string Map { get; set; } = "—";
        public string Mode { get; set; } = "—";
        public string Players { get; set; } = "—";
        public string Ping { get; set; } = "—";

        public int PlayerCount { get; set; }
        public int MaxPlayers { get; set; }
        public long PingValue { get; set; }

        public bool? HasPassword { get; set; }
        public bool? IsRanked { get; set; }
        public bool IsFavorite { get; set; }

        public string FavoriteKey => $"{Address}:{QueryPort}";
    }
}
