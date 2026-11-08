using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using NBL;
using NBL.Services;
using NBLApp.Controls;
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

        _refreshFailed = false;
        RefreshInfo.Text = "Получение списка серверов...";

        try
        {
            var clients = await Launcher.GetAvailableClientsAsync(GameId);
            string previous = _selectedClient;

            ClientFilter.Items.Clear();
            ClientFilter.Items.Add(new ComboBoxItem { Content = "Все клиенты", Tag = "*" });

            foreach (var client in clients)
                ClientFilter.Items.Add(new ComboBoxItem { Content = client.Name, Tag = client.Ident });

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
                            row.Map = GetInfo(info, "mapname", "map", "map_name") ?? "—";
                            row.Mode = GetInfo(info, "gametype", "gamemode", "game_mode", "mode") ?? "—";

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
                finally { semaphore.Release(); }
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
        finally { _isRefreshing = false; }
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
        name.Children.Add(new TextBlock { Text = server.Name, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
        name.Children.Add(new TextBlock { Text = server.Client, FontSize = 11, Opacity = 0.6 });

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
        RefreshInfo.Text = _refreshFailed
            ? $"Ошибка обновления · {seconds} сек. назад"
            : $"Обновлено {seconds} сек. назад";
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

    private void SearchBox_OnTextChanged(object? sender, TextChangedEventArgs e) => ApplyFilters();

    private void ClientFilter_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized || ClientFilter == null)
            return;

        _selectedClient = (ClientFilter.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "*";
        ApplyFilters();
    }

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
}