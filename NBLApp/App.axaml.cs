using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AutoUpdaterDotNET;
using NBL;
using NBL.Services;
using System;
using System.IO;
using static NBL.LaunchParamDict;

namespace NBLApp;

public partial class App : Application
{
    public Launcher Launcher;

    public override void Initialize()
    {
        Logger.Info("App.Initialize started");

        try
        {
            string configDirectory =
                Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData),
                    "NetBat2142");

            Logger.Info($"Config directory: {configDirectory}");

            Directory.CreateDirectory(configDirectory);

            string configPath =
                Path.Combine(
                    configDirectory,
                    "nblconfig.registry");

            string serverAddress = "http://api.nbl2142.fun";

            Logger.Info($"Config file: {configPath}");

            Launcher =
                new Launcher(pathConfig: configPath, serverAddress: serverAddress);

            Logger.Info("Launcher instance created");

            LoadLanguage();

            Logger.Info("Language loaded");

            AvaloniaXamlLoader.Load(this);

            Logger.Info("Avalonia XAML loaded");

            Console.WriteLine(Localization.Locale.Get("LauncherTitle"));
            Console.WriteLine(Localization.Locale.Get("Settings"));
            Console.WriteLine(Localization.Locale.Get("Parameters"));
            Console.WriteLine(Localization.Locale.Get("FullScreen"));
            ILaunchParam[] launchParams = new ILaunchParam[]
            {
                new LaunchParamDict()
                {
                    Id = "resolution",
                    Name = "Resolution",
                    Description = null,
                    ShortDescription = "Game window resolution",
                    Dictionary = new()
                    {
                        {"2560×1440", "+szx 2560 +szy 1440"},
                        {"1920×1080", "+szx 1920 +szy 1080"},
                        {"1600×900", "+szx 1600 +szy 900"},
                        {"1280×1024", "+szx 1280 +szy 1024"},
                        {"1024×768", "+szx 1024 +szy 768"},
                        {"800×600", "+szx 800 +szy 600"}
                    }
                },
                
                new LaunchParamBool()
                {
                    Id = "fullscreen",
                    Name = Localization.Locale.Get("FullScreen"),
                    Description = "Game launches in full screen on the monitor",
                    ShortDescription = "Start game in full screen mode",
                },
                new LaunchParamBool()
                {
                    Id = "widescreen",
                    Name = Localization.Locale.Get("WideScreen"),
                    Description = "Game window will be able to accept, in addition to 4:3, also 16:9",
                    ShortDescription = "Change the FOV to fit Wide-Screens",
                },
                new LaunchParamBool()
                {
                    Id = "multi",
                    Name = Localization.Locale.Get("Multi"),
                    Description = "Allows launching multiple game windows simultaneously",
                    ShortDescription = "Allow starting multiple BF2 instances",
                },
                new LaunchParamBool()
                {
                    Id = "lowPriority",
                    Name = Localization.Locale.Get("LowPriority"),
                    Description = null,
                    ShortDescription = "Run the game with slightly lower priority",
                },
                new LaunchParamBool()
                {
                    Id = "noSound",
                    Name = Localization.Locale.Get("NoSound"),
                    Description = null,
                    ShortDescription = "Start game without sound",
                },

                new LaunchParamCustom()
                {
                    Id = "customparam",
                    Name = "CustomParam",
                    Description = null,
                    ShortDescription = "",
                    FullFormat = true
                }
            };

            Launcher.RegisterGame(
                gameId: "bf2142",
                name: "Battlefield 2142",
                shortName: "BF2142",
                determinants: new[] { "bf2142.exe" },
                launchParams: launchParams);

            Logger.Info("Battlefield 2142 registered");
        }
        catch (Exception ex)
        {
            Logger.Error("Error during App.Initialize", ex);
            throw;
        }
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Logger.Info("App.OnFrameworkInitializationCompleted started");

        try
        {
            if (ApplicationLifetime
                is IClassicDesktopStyleApplicationLifetime desktop)
            {
                Logger.Info("Creating MainWindow");

                desktop.MainWindow =
                    new MainWindow(
                        Launcher);

                Logger.Info("MainWindow created");
            }

            base.OnFrameworkInitializationCompleted();

            Logger.Info("Framework initialization completed");
        }
        catch (Exception ex)
        {
            Logger.Error(
                "Error during framework initialization",
                ex);

            throw;
        }
    }

    private void LoadLanguage()
    {
        Logger.Info("Loading launcher language");

        try
        {
            Logger.Info("Reading launcher registry");

            Launcher.Registry.ReadSync(createMissing: true);

            Logger.Info("Launcher registry read successfully");

            if (!Launcher.Registry.HasSection("launcher"))
            {
                Logger.Info(
                    "Launcher section does not exist, creating it");

                Launcher.Registry.AddSection("launcher");
            }

            string language =
                Launcher.Registry.Get(
                    "launcher",
                    "language",
                    "auto");

            Logger.Info($"Configured launcher language: {language}");

            Localization.Locale.SetLanguage(language);

            Logger.Info(
                $"Launcher language applied: {language}");
        }
        catch (Exception ex)
        {
            Logger.Error(
                "Failed to load launcher language",
                ex);

            throw;
        }
    }
}