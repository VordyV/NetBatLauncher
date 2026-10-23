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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Ursa.Controls;
namespace NBLApp.Views;
public partial class MainView : View
{
    protected ViewPresenter<Launcher> Pages;
    protected Game Game;
    protected Launcher Launcher;
    protected bool isLoaded = false;
    protected bool isUpdatingClients = false;

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

    private void Locale_LanguageChanged( object? sender, EventArgs e) { this.DataContext = null; this.DataContext = this; }
    
    public MainView( Launcher launcher, ViewPresenter<Launcher> viewPresenter, object? arg) : base(launcher, viewPresenter, arg)
    {
        this.Launcher = launcher;
        Locale.LanguageChanged += this.Locale_LanguageChanged;
        this.Loaded += async (sender, args) => await this.OnLoaded();
        this.Game = launcher.GetGame((string)arg);
        this.Game.OnChangeStatus += this.UpdateButtonMA;
        this.Pages = new ViewPresenter<Launcher>( arg: this.Launcher, new()
        {
            {"general", (l, vp, arg) => new MainGeneralView( l, vp, arg) }
        });
        
        InitializeComponent();
        
        this.DataContext = this;
        this.MainContent.Content = this.Pages.Content;
        this.Pages.ShowView("general", this.Game.Id);
    }


    protected async Task OnLoaded()
    {
        await Game.CheckInstall();

        if (Game.IsInstall)
        {
            try { Game.GetCurrentClient(); }
            catch (GameNotSetRegistryException) { Console.WriteLine("Клиент игры не установлен."); }

            await UpdateClients(await Game.GetClients());
        }
        else
        {
            await UpdateClients([]);
        }

        await UpdateButtonMA(Game.Status);
        isLoaded = true;
    }

    private string GetSelectedClientId()
    {
        if (ComboBoxClients.SelectedItem is ComboBoxItem item)
            return item.Name?.Split('_').Last() ?? Launcher.ClientId;

        return Launcher.ClientId;
    }

    private bool IsClientInstalled(string clientId)
    {
        return Game.Clients.Keys.Any(id =>
            id.Equals(clientId, StringComparison.OrdinalIgnoreCase));
    }


    private async Task SwitchClientWithProfilesAsync(string clientId)
    {
        string currentClient;

        try
        {
            currentClient = Game.GetCurrentClient();
        }
        catch
        {
            currentClient = Launcher.ClientId;
        }

        var profiles = new ProfileService();

        await profiles.SwitchAsync(currentClient, clientId);

        try
        {
            await Game.ChangeClientGame(clientId);
            Game.SetCurrentClient(clientId);
        }
        catch
        {
            if (!currentClient.Equals(
                    clientId, StringComparison.OrdinalIgnoreCase))
            {
                await profiles.SwitchAsync(clientId, currentClient);
            }

            throw;
        }
    }

    protected async Task UpdateClients(ClientData[] clients)
    {
        isUpdatingClients = true;

        try
        {
            string current;

            try
            {
                current = Game.GetCurrentClient();
            }
            catch
            {
                current = Launcher.ClientId;
            }

            ApiClient[] available;

            try
            {
                available = await Launcher.GetAvailableClientsAsync(Game.Id);
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to load available clients: {ex.Message}");

                available = clients.Select(c => new ApiClient
                {
                    Ident = c.ID,
                    Name = c.Name
                }).ToArray();
            }

            ComboBoxClients.Items.Clear();
            ComboBoxClients.SelectedIndex = -1;

            foreach (var client in available)
            {
                bool installed = clients.Any(c =>
                    c.ID.Equals(client.Ident, StringComparison.OrdinalIgnoreCase));

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
            switch (status)
            {
                case GameStatus.NotInstalled:
                    Button_MainAction.Content = InstallGameText;
                    break;

                case GameStatus.NotRunning:
                    Button_MainAction.Content = IsClientInstalled(GetSelectedClientId())
                        ? LaunchGameText
                        : InstallClientText;
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
        try
        {
            switch (this.Game.Status)
            {

                case GameStatus.NotInstalled:
                    {
                        var context = new DialogContext();

                        await OverlayDialog.ShowCustomAsync<SimpInst>(
                            new SimpInst(this.Launcher, this.Game) { DataContext = context },
                            context,
                            hostId: "main",
                            options: new OverlayDialogOptions { CanResize = false });

                        await this.Game.CheckInstall();

                        await this.UpdateButtonMA(this.Game.Status);

                        if (this.Game.IsInstall)
                        {
                            await this.UpdateClients(await this.Game.GetClients());
                        }

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
                                var update = new ClientUpdateService();

                                await update.InstallClientAsync(
                                    Launcher.ServerAddress,
                                    Game.Id,
                                    Game.GetPath(),
                                    clientId);

                                await Game.GetClients();

                                if (!IsClientInstalled(clientId))
                                    throw new Exception($"Клиент '{clientId}' не установлен.");

                                await SwitchClientWithProfilesAsync(clientId);

                                await UpdateClients(await Game.GetClients());
                                await UpdateButtonMA(Game.Status);
                            }
                            catch (Exception exception)
                            {
                                Notify.ShowError(Locale.Get("ClientChangeFailed"), exception.Message);
                            }
                            finally
                            {
                                Button_MainAction.IsEnabled = true;
                            }

                            break;
                        }

                        string currentClient;

                        try
                        {
                            currentClient = Game.GetCurrentClient();
                        }
                        catch
                        {
                            currentClient = "";
                        }

                        await SwitchClientWithProfilesAsync(clientId);
                        await Game.Launch();

                        break;
                    }
                case GameStatus.Running:
                    {
                        break;
                    }
                case GameStatus.Stopping:
                    {
                        break;
                    }
            }
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception);
            Notify.ShowError(Locale.Get("GameLaunchFailed"), exception.Message);
        }
    }
    
    private async void ButtonSettings_OnClick(object? sender, RoutedEventArgs e)
    {
        var context = new DialogContext();
        await OverlayDialog.ShowCustomAsync<SettingsGear>(new SettingsGear(this.Game) {DataContext = context}, context, hostId: "main", new OverlayDialogOptions {CanResize = false});
    }

    private async void ComboBoxClients_OnSelectionChanged(
    object? sender, SelectionChangedEventArgs e)
    {
        if (!isLoaded || isUpdatingClients || !Game.IsInstall ||
            ComboBoxClients.SelectedItem is not ComboBoxItem)
            return;

        await UpdateButtonMA(Game.Status);
    }

    private async void ButtonParams_OnClick(object? sender, RoutedEventArgs e)
    {
        var context = new DialogContext();
        await OverlayDialog.ShowCustomAsync<LaunchSettings>(new LaunchSettings(this.Game) {DataContext = context}, context, hostId: "main", new OverlayDialogOptions {CanResize = false});
    }
    
    private void Telegram_Click(object? sender, RoutedEventArgs e)
    { 
        Process.Start(new ProcessStartInfo {FileName = "https://t.me/VSEM2142", UseShellExecute = true});
    }
    
}