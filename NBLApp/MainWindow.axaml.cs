using System;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Interactivity;
using NBL;
using NBLApp.Controls;
using NBLApp.Localization;
using NBLApp.Views;
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

        this.Loaded += async (_, _) => await Safe.RunAsync(this.OnLoaded, nameof(OnLoaded));

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

    private Task OnLoaded()
    {
        var registry = this.Launcher.Registry;

        if (!registry.HasSection("launcher"))
            registry.AddSection("launcher");

        bool openMaximized = registry.Get(
            "launcher",
            "openMaximized",
            false
        );

        if (openMaximized)
        {
            this.WindowState = WindowState.Maximized;
            this.IsMaximum = true;
        }

        return Task.CompletedTask;
    }

    private void Window_OnClickHide(object? sender, RoutedEventArgs e)
    {
        this.WindowState = WindowState.Minimized;
    }

    private void Window_OnClickMinMax(object? sender, RoutedEventArgs e)
    {
        if (this.WindowState != WindowState.Maximized)
        {
            this.WindowState = WindowState.Maximized;
        }
        else
        {
            this.WindowState = WindowState.Normal;
        }

        this.IsMaximum = this.WindowState == WindowState.Maximized;
    }

    private void Window_OnClickClose(object? sender, RoutedEventArgs e)
    {
        this.Close();
    }
}