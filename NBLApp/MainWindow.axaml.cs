using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Interactivity;
using NBL;
using NBLApp.Controls;
using NBLApp.Localization;
using NBLApp.Views;
using System;
using System.Threading.Tasks;
using Window = Avalonia.Controls.Window;
using WindowNotificationManager = Ursa.Controls.WindowNotificationManager;

namespace NBLApp;

public partial class MainWindow : Window
{
    protected bool IsMaximum = false;
    protected Launcher Launcher;
    protected ViewPresenter<Launcher> ViewPresenter;
    protected WindowNotificationManager NotificationManager;
    public string LauncherTitle => Locale.Get("LauncherTitle");

    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(Launcher launcher)
    {
        Locale.SetLanguage("ru-RU");

        this.Loaded += async (sender, args) => await this.OnLoaded();

        this.Launcher = launcher;

        this.ViewPresenter = new ViewPresenter<Launcher>(this.Launcher, views: new()
        {
            {"main", (l, vp, arg) => new MainView(l, vp, arg)},
        });

        InitializeComponent();
        this.DataContext = this;

        this.NotificationManager = new WindowNotificationManager(this);
        this.NotificationManager.Position = NotificationPosition.BottomRight;

        Notify.Init(this.NotificationManager);

        this.MainContent.Content = this.ViewPresenter.Content;
        this.ViewPresenter.LoadView("main", this.Launcher.GetGames()[0]);

    }
    private void Locale_LanguageChanged(object? sender, EventArgs e)
    {
        this.DataContext = null;
        this.DataContext = this;
    }

    private async Task OnLoaded()
    {
        await this.Launcher.Registry.Read(createMissing: true);
    }

    private void Window_OnClickHide(object? sender, RoutedEventArgs e)
    {
        this.WindowState = WindowState.Minimized;
    }

    private void Window_OnClickMinMax(object? sender, RoutedEventArgs e)
    {
        if (!this.IsMaximum)
        {
            this.WindowState = WindowState.Maximized;
        }
        else
        {
            this.WindowState = WindowState.Normal;
        }

        this.IsMaximum = !this.IsMaximum;
    }

    private void Window_OnClickClose(object? sender, RoutedEventArgs e)
    {
        this.Close();
    }
}