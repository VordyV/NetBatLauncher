
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using NBL;
using NBL.Services;
using NBLApp.Controls;
using NBLApp.Forms;
using NBLApp.Localization;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using NBLNet;
using Ursa.Controls;
using Game = NBL.Game;

namespace NBLApp.Views;

public partial class MainView : View
{
    protected ViewPresenter<Launcher> Pages;
    protected Game Game;
    protected Launcher Launcher;
    protected bool isLoaded = false;
    protected bool isUpdatingClients = false;
    private bool isSyncingClients;
    private bool clientSyncFailed;

    public string MainText => Locale.Get("Main");
    public string HeadquartersText => Locale.Get("Headquarters");
    public string CommunityText => Locale.Get("Community");
    public string SupportText => Locale.Get("Support");
    public string MapsText => Locale.Get("Maps");
    public string LeaderboardText => Locale.Get("Leaderboard");
    public string BanlistText => Locale.Get("Banlist");
    public string LaunchGameText => Locale.Get("LaunchGame");
    public string SettingsText => Locale.Get("Settings");
    public string PlatformText => Locale.Get("Platform");
    public string ModText => Locale.Get("Mod");
    public string ParametersText => Locale.Get("Parameters");
    public string PlayersText => Locale.Get("Players");
    public string PingText => Locale.Get("Ping");
    public string PlatformAccessText => Locale.Get("PlatformAccess");
    public string InstallGameText => Locale.Get("InstallGame");
    public string GameRunningText => Locale.Get("GameRunning");
    public string StoppingText => Locale.Get("Stopping");
    public string LaunchpadText => Locale.Get("Launchpad");
    public string InstallClientText => Locale.Get("InstallClient");

    private void Locale_LanguageChanged(object? sender, EventArgs e)
    {
        DataContext = null;
        DataContext = this;
    }

    public MainView()
    {
        InitializeComponent();
    }

    public MainView(Launcher launcher, ViewPresenter<Launcher> viewPresenter, object? arg) : base(launcher, viewPresenter, arg)
    {
        Launcher = launcher;
        Locale.LanguageChanged += Locale_LanguageChanged;
        Loaded += async (sender, args) => await OnLoaded();
        Game = launcher.GetGame((string)arg);
        Game.OnChangeStatus += UpdateButtonMA;
        Pages = new ViewPresenter<Launcher>(arg: Launcher, new()
        {
            { "general", (l, vp, arg) => new MainGeneralView(l, vp, arg) }
        });

        InitializeComponent();
        DataContext = this;
        MainContent.Content = Pages.Content;
        Pages.ShowView("general", Game.Id);
    }

    protected async Task OnLoaded()
    {
        await Game.CheckInstall();

        if (Game.IsInstall)
        {
            try { Game.GetCurrentClient(); }
            catch (GameNotSetRegistryException) { Logger.Warning("Current client not selected."); }

            var clients = await Game.GetClients();
            await UpdateClients(clients);
            await SyncInstalledClientsAsync(clients);
            await UpdateClients(await Game.GetClients());
        }
        else
            await UpdateClients([]);

        isLoaded = true;
        
        _ = this.UpdateLabelPlayers();
        _ = this.UpdateLabelPing();
        
        await UpdateButtonMA(Game.Status);
    }

    private async Task SyncInstalledClientsAsync(ClientData[] clients)
    {
        isSyncingClients = true;
        clientSyncFailed = false;
        await UpdateButtonMA(Game.Status);

        try
        {
            var update = new ClientUpdateService();
            int checkedClients = 0;

            foreach (string id in new[] { "respy", "novgames" })
            {
                if (!clients.Any(c => c.ID.Equals(id, StringComparison.OrdinalIgnoreCase)))
                    continue;

                checkedClients++;

                try
                {
                    await update.SyncClientAsync(Launcher.ServerAddress, Game.Id, Game.GetPath(), id);
                    Logger.Info($"Client synchronization completed: {id}");
                    //Notify.ShowInfo("Синхронизация завершена", $"{id}: проверка файлов выполнена успешно.");
                }
                catch (Exception ex)
                {
                    clientSyncFailed = true;
                    Logger.Error($"Client synchronization failed: {id}", ex);
                    Notify.ShowError($"Ошибка синхронизации {id}", ex.Message);
                }
            }

            if (checkedClients == 0)
                Logger.Info("Client synchronization skipped: no Respy or NovGames installation found.");
        }
        finally
        {
            isSyncingClients = false;
            await UpdateButtonMA(Game.Status);
        }
    }

    private string GetSelectedClientId()
    {
        if (ComboBoxClients.SelectedItem is ComboBoxItem item)
            return item.Name?.Split('_').Last() ?? Launcher.ClientId;

        return Launcher.ClientId;
    }

    private bool IsClientInstalled(string clientId)
    {
        return Game.Clients.Keys.Any(id => id.Equals(clientId, StringComparison.OrdinalIgnoreCase));
    }

    private async Task SwitchClientWithProfilesAsync(string clientId)
    {
        string currentClient;

        try { currentClient = Game.GetCurrentClient(); }
        catch { currentClient = Launcher.ClientId; }

        var profiles = new ProfileService();
        await profiles.SwitchAsync(currentClient, clientId);

        try
        {
            await Game.ChangeClientGame(clientId);
            Game.SetCurrentClient(clientId);
        }
        catch
        {
            if (!currentClient.Equals(clientId, StringComparison.OrdinalIgnoreCase))
                await profiles.SwitchAsync(clientId, currentClient);

            throw;
        }
    }

    protected async Task UpdateClients(ClientData[] clients)
    {
        isUpdatingClients = true;

        try
        {
            string current;

            try { current = Game.GetCurrentClient(); }
            catch { current = Launcher.ClientId; }

            ApiClient[] available;

            try { available = await Launcher.GetAvailableClientsAsync(Game.Id); }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to load available clients: {ex.Message}");
                available = clients.Select(c => new ApiClient { Ident = c.ID, Name = c.Name }).ToArray();
            }

            ComboBoxClients.Items.Clear();
            ComboBoxClients.SelectedIndex = -1;

            foreach (var client in available)
            {
                bool installed = clients.Any(c => c.ID.Equals(client.Ident, StringComparison.OrdinalIgnoreCase));

                ComboBoxClients.Items.Add(new ComboBoxItem
                {
                    Content = client.Name,
                    Name = $"ComboBoxItemClients_{client.Ident}",
                    Foreground = installed ? Brushes.White : Brushes.Gray
                });

                if (client.Ident.Equals(current, StringComparison.OrdinalIgnoreCase))
                    ComboBoxClients.SelectedIndex = ComboBoxClients.ItemCount - 1;
            }

            if (ComboBoxClients.SelectedIndex < 0 && ComboBoxClients.ItemCount > 0)
                ComboBoxClients.SelectedIndex = 0;
        }
        finally
        {
            isUpdatingClients = false;
        }

        await UpdateButtonMA(Game.Status);
    }

    private async Task UpdateButtonMA(GameStatus status)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (isSyncingClients)
            {
                Button_MainAction.Content = "Синхронизация клиентов...";
                Button_MainAction.IsEnabled = false;
                return;
            }

            Button_MainAction.IsEnabled = true;

            if (clientSyncFailed && status == GameStatus.NotRunning)
            {
                Button_MainAction.Content = "Повторить синхронизацию";
                return;
            }

            switch (status)
            {
                case GameStatus.NotInstalled:
                    Button_MainAction.Content = InstallGameText;
                    break;
                case GameStatus.NotRunning:
                    Button_MainAction.Content = IsClientInstalled(GetSelectedClientId()) ? LaunchGameText : InstallClientText;
                    break;
                case GameStatus.Running:
                    Button_MainAction.Content = GameRunningText;
                    break;
                case GameStatus.Stopping:
                    Button_MainAction.Content = StoppingText;
                    break;
            }
        });
    }

    protected async void Button_MainAction_OnClick(object? sender, RoutedEventArgs e)
    {
        if (isSyncingClients)
            return;

        try
        {
            if (clientSyncFailed && Game.Status == GameStatus.NotRunning)
            {
                await SyncInstalledClientsAsync(await Game.GetClients());
                await UpdateClients(await Game.GetClients());
                await UpdateButtonMA(Game.Status);
                return;
            }

            switch (Game.Status)
            {
                case GameStatus.NotInstalled:
                    {
                        var context = new DialogContext();
                        await OverlayDialog.ShowCustomAsync<SimpInst>(new SimpInst(Launcher, Game) { DataContext = context }, context, hostId: "main", options: new OverlayDialogOptions { CanResize = false });
                        await Game.CheckInstall();
                        await UpdateButtonMA(Game.Status);

                        if (Game.IsInstall)
                            await UpdateClients(await Game.GetClients());

                        break;
                    }

                case GameStatus.NotRunning:
                    {
                        string clientId = GetSelectedClientId();

                        if (!IsClientInstalled(clientId))
                        {
                            Button_MainAction.IsEnabled = false;

                            try
                            {
                                var context = new DialogContext();
                                var window = new SimpInst(Launcher, Game, clientId, false) { DataContext = context };
                                await OverlayDialog.ShowCustomAsync<SimpInst>(window, context, hostId: "main", options: new OverlayDialogOptions { CanResize = false });

                                if (!window.OperationSucceeded)
                                    break;

                                await Game.GetClients();

                                if (!IsClientInstalled(clientId))
                                    throw new Exception($"Клиент '{clientId}' не установлен.");

                                await SwitchClientWithProfilesAsync(clientId);
                                await UpdateClients(await Game.GetClients());
                                await UpdateButtonMA(Game.Status);
                            }
                            catch (Exception ex)
                            {
                                Notify.ShowError(Locale.Get("ClientChangeFailed"), ex.Message);
                            }
                            finally
                            {
                                Button_MainAction.IsEnabled = true;
                            }

                            break;
                        }

                        await SwitchClientWithProfilesAsync(clientId);
                        await Game.Launch();
                        break;
                    }

                case GameStatus.Running:
                case GameStatus.Stopping:
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Main action failed.", ex);
            Notify.ShowError(Locale.Get("GameLaunchFailed"), ex.Message);
        }
    }

    private async void ButtonSettings_OnClick(object? sender, RoutedEventArgs e)
    {
        var context = new DialogContext();
        await OverlayDialog.ShowCustomAsync<SettingsGear>(new SettingsGear(Game) { DataContext = context }, context, hostId: "main", new OverlayDialogOptions { CanResize = false });
    }

    private async Task UpdateLabelPlayers()
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            this.Players.IsVisible = !this.Players.IsVisible;
            this.LoadingIconPlayers.IsVisible = !this.LoadingIconPlayers.IsVisible;
        });
        
        try
        {
            int num = await this.Launcher.Server.GetNumberPlayers("bf2142", this.GetSelectedClientId());
            await Dispatcher.UIThread.InvokeAsync(() => { this.Players.Text = num.ToString(); });
        }
        catch (Exception e)
        {
            await Dispatcher.UIThread.InvokeAsync(() => { this.Players.Text = "-1"; });
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                this.Players.IsVisible = !this.Players.IsVisible;
                this.LoadingIconPlayers.IsVisible = !this.LoadingIconPlayers.IsVisible;
            });
        }
    }
    
    private async Task UpdateLabelPing()
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            this.Ping.IsVisible = !this.Ping.IsVisible;
            this.LoadingIconPing.IsVisible = !this.LoadingIconPing.IsVisible;
        });

        try
        {
            GameClient client = await this.Launcher.Server.GetGameClient("bf2142", this.GetSelectedClientId());
            long delay = await client.DetermineDelay();
            await Dispatcher.UIThread.InvokeAsync(() => { this.Ping.Text = $" {delay} ms"; });

        }
        catch (Exception e)
        {
            await Dispatcher.UIThread.InvokeAsync(() => { this.Ping.Text = "-1"; });
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                this.Ping.IsVisible = !this.Ping.IsVisible;
                this.LoadingIconPing.IsVisible = !this.LoadingIconPing.IsVisible;
            });
        }
    }

    private async void ComboBoxClients_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!isLoaded || isUpdatingClients || !Game.IsInstall || ComboBoxClients.SelectedItem is not ComboBoxItem)
            return;

        await UpdateButtonMA(Game.Status);

        _ = this.UpdateLabelPlayers();
        _ = this.UpdateLabelPing();
    }

    private async void ButtonParams_OnClick(object? sender, RoutedEventArgs e)
    {
        var context = new DialogContext();
        await OverlayDialog.ShowCustomAsync<LaunchSettings>(new LaunchSettings(Game) { DataContext = context }, context, hostId: "main", new OverlayDialogOptions { CanResize = false });
    }

    private void Telegram_Click(object? sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo { FileName = "https://t.me/VSEM2142", UseShellExecute = true });
    }
}
