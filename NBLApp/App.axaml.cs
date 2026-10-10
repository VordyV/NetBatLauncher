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

            string serverAddress = "http://127.0.0.1:5000";

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

            ILaunchParam[] launchParams = new ILaunchParam[]
            {
                new LaunchParamDict()
                {
                    Id = "screen",
                    Name = "Screen",
                    Dictionary = new()
                    {
                        {"Widesceen", "+widescreen 1"},
                        {"Fullscreen 2560*1440", "+fullscreen 1 +szx 2560 +szy 1440"},
                        {"Fullscreen 1920*1080", "+fullscreen 1 +szx 1920 +szy 1080"},
                        {"Fullscreen 1600*900", "+fullscreen 1 +szx 1600 +szy 900"},
                        {"Fullscreen 1280*1024", "+fullscreen 1 +szx 1280 +szy 1024"},
                        {"Fullscreen 1024*768", "+fullscreen 1 +szx 1024 +szy 768"},
                        {"Fullscreen 800*600", "+fullscreen 1 +szx 800 +szy 600"}
                    }
                },

                new LaunchOptionDict()
                {
                    Id = "additional",
                    Name = "Additional",
                    Dictionary = new()
                    {
                        {"Multi", "+multi 1"},
                        {"LowPriority", "+lowPriority 1"},
                        {"NoSound", "+noSound 1"}
                    }
                },

                new LaunchParamCustom()
                {
                    Id = "customparam",
                    Name = "CustomParam",
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