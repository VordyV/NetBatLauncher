using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;
using NBL;
using NBLApp.Controls;
using NBLApp.Forms;
using System;
using System.Linq;
using System.Threading.Tasks;
using Ursa.Controls;
namespace NBLApp.Views;
public partial class MainView : View
{
    public MainView()
    {
        InitializeComponent();
    }
    protected ViewPresenter<Launcher> Pages;
    protected Game Game;
    protected bool isLoaded =
        false;
    public MainView(
        Launcher launcher,
        ViewPresenter<Launcher> viewPresenter,
        object? arg)
        : base(
            launcher,
            viewPresenter,
            arg)
    {
        this.Loaded +=
            async (sender, args) =>
                await this.OnLoaded();
        this.Game =
            launcher.GetGame(
                (string)arg);
        this.Game.OnChangeStatus +=
            this.UpdateButtonMA;
        this.Pages =
            new ViewPresenter<Launcher>(
                arg: this.Launcher,
                new()
                {
                {
                    "general",
                    (l, vp, arg) =>
                        new MainGeneralView(
                            l,
                            vp,
                            arg)
                }
                });
        InitializeComponent();
        this.DataContext =
            this;
        this.MainContent.Content =
            this.Pages.Content;
        this.Pages.ShowView(
            "general",
            this.Game.Id);
    }
    protected async Task OnLoaded()
    {
        await this.Game.CheckInstall();
        Console.WriteLine(
            this.Game.Status);
        if (this.Game.IsInstall)
        {
            try
            {
                this.Game.GetCurrentClient();
            }
            catch (GameNotSetRegistryException)
            {
                Console.WriteLine(
                    "Клиент игры не установлен.");
            }
            await this.UpdateClients(
                await this.Game.GetClients());
        }
        this.isLoaded =
            true;
    }
    protected async Task UpdateClients(
        ClientData[] clients)
    {
        this.ComboBoxClients.Items.Clear();
        byte i = 0;
        byte currentClientId = 0;
        foreach (var client in clients)
        {
            this.ComboBoxClients.Items.Add(
                new ComboBoxItem
                {
                    Content =
                        client.Name,
                    Name =
                        $"ComboBoxItemClients_{client.ID}"
                });
            if (this.Game.Client == client.ID)
            {
                currentClientId =
                    i;
            }
            i++;
        }
        this.ComboBoxClients.SelectedIndex =
            currentClientId;
    }
    private async Task UpdateButtonMA(GameStatus status)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            switch (status)
            {
                case GameStatus.NotInstalled:
                    this.Button_MainAction.Content =
                        "УСТАНОВИТЬ ИГРУ";
                    break;
                case GameStatus.NotRunning:
                    this.Button_MainAction.Content =
                        "ЗАПУСТИТЬ ИГРУ";
                    break;
                case GameStatus.Running:
                    this.Button_MainAction.Content =
                        "ИГРА ЗАПУЩЕНА";
                    break;
                case GameStatus.Stopping:
                    this.Button_MainAction.Content =
                        "ОСТАНОВКА...";
                    break;
            }
        });
        await Task.CompletedTask;
    }
    protected async void Button_MainAction_OnClick(
        object? sender,
        RoutedEventArgs e)
    {
        try
        {
            switch (this.Game.Status)
            {
                case GameStatus.NotInstalled:
                    {
                        var context =
                            new DialogContext();
                        await OverlayDialog.ShowCustomModal<SimpInst>(
                            new SimpInst(
                                this.Game)
                            {
                                DataContext =
                                    context
                            },
                            context,
                            hostId: "main",
                            new OverlayDialogOptions
                            {
                                CanResize =
                                    false
                            });
                        await this.Game.CheckInstall();
                        if (this.Game.IsInstall)
                        {
                            await this.UpdateClients(
                                await this.Game.GetClients());
                        }
                        break;
                    }
                case GameStatus.NotRunning:
                    {
                        await this.Game.Launch();
                        break;
                    }
                case GameStatus.Running:
                    {
                        // Пока ничего не делаем.
                        // Позже здесь можно сделать
                        // фокусирование окна игры.
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
            Console.WriteLine(
                exception);
            Notify.ShowError(
                "Не удалось запустить игру",
                exception.Message);
        }
    }
    private async void ButtonSettings_OnClick(
    object? sender,
    RoutedEventArgs e)
    {
        var context =
            new DialogContext();
        await OverlayDialog.ShowCustomModal<SettingsGear>(
            new SettingsGear(
                this.Game)
            {
                DataContext =
                    context
            },
            context,
            hostId: "main",
            new OverlayDialogOptions
            {
                CanResize =
                    false
            });
    }
    private async void ComboBoxClients_OnSelectionChanged(
        object? sender,
        SelectionChangedEventArgs e)
    {
        if (!this.isLoaded ||
            !this.Game.IsInstall ||
            this.ComboBoxClients == null ||
            this.ComboBoxClients.SelectedItem is not ComboBoxItem item)
        {
            return;
        }
        try
        {
            string clientId =
                item.Name?
                    .Split('_')
                    .Last()
                ?? "";
            Console.WriteLine(
                clientId);
            await this.Game.ChangeClientGame(
                clientId);
            this.Game.SetCurrentClient(
                clientId);
        }
        catch (Exception exception)
        {
            Notify.ShowError(
                "Клиент игры не был изменен",
                exception.Message);
        }
    }
    private async void ButtonParams_OnClick(
        object? sender,
        RoutedEventArgs e)
    {
        var context =
            new DialogContext();
        await OverlayDialog.ShowCustomModal<LaunchSettings>(
            new LaunchSettings(
                this.Game)
            {
                DataContext =
                    context
            },
            context,
            hostId: "main",
            new OverlayDialogOptions
            {
                CanResize =
                    false
            });
    }
}