using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using AutoUpdaterDotNET;
using NBL;
using System;
using System.IO;
using static NBL.LaunchParamDict;
namespace NBLApp;

public partial class App : Application
{
    public Launcher Launcher;
    public override void Initialize()
    {
        string configDirectory =
    Path.Combine(
        Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData),
        "NetBat2142");
        Directory.CreateDirectory(configDirectory);
        string configPath =
            Path.Combine(
                configDirectory,
                "bnconfig.registry");
        this.Launcher =
            new Launcher(pathConfig: configPath);

        this.LoadLanguage();

        AvaloniaXamlLoader.Load(this);

        Console.WriteLine(Localization.Locale.Get("LauncherTitle"));
        Console.WriteLine(Localization.Locale.Get("Settings"));
        Console.WriteLine(Localization.Locale.Get("Parameters"));

        ILaunchParam[] launchParams = new ILaunchParam[]
        {
            new LaunchParamDict() {Id = "screen", Name = "Screen", Dictionary = new()
            {
                {"Widesceen", "+widescreen 1"},
                {"Fullscreen 2560*1440", "+fullscreen 1 +szx 2560 +szy 1440"},
                {"Fullscreen 1920*1080", "+fullscreen 1 +szx 1920 +szy 1080"},
                {"Fullscreen 1600*900", "+fullscreen 1 +szx 1600 +szy 900"},
                {"Fullscreen 1280*1024", "+fullscreen 1 +szx 1280 +szy 1024"},
                {"Fullscreen 1024*768", "+fullscreen 1 +szx 1024 +szy 768"},
                {"Fullscreen 800*600", "+fullscreen 1 +szx 800 +szy 600"}
            }},
            new LaunchOptionDict() {Id = "additional", Name = "Additional", Dictionary = new()
            {
                {"Multi (Возможность запускать несколько процессов игры)", "+multi 1"},
                {"LowPriority (Запуск с низким приоритетом)", "+lowPriority 1"},
                {"NoSound (Отключение звука игры)", "+noSound 1"},
            }},
            new LaunchParamCustom() {Id = "customparam", Name = "CustomParam", FullFormat = true}
        };
        this.Launcher.RegisterGame(gameId: "bf2142", name: "Battlefield 2142", shortName: "BF2142", determinants: new[] { "bf2142.exe" }, launchParams: launchParams);
 
        AutoUpdater.Start("https://raw.githubusercontent.com/VordyV/NetBatLauncher/main/update.xml");
    }
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime
            is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow =
                new MainWindow(
                    this.Launcher);
        }

        base.OnFrameworkInitializationCompleted();
    }
    private void LoadLanguage()
    {
        this.Launcher.Registry.ReadSync(createMissing: true);

        if (!this.Launcher.Registry.HasSection("launcher"))
        {
            this.Launcher.Registry.AddSection("launcher");
        }

        string language =
            this.Launcher.Registry.Get(
                "launcher",
                "language",
                "auto");

        Localization.Locale.SetLanguage(language);
    }
}