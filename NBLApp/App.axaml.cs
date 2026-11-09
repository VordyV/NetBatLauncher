using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AutoUpdaterDotNET;
using NBL;
using NBL.Services;
using System;
using System.IO;
using static NBL.LaunchParamDict;
using NBLApp.Localization;

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

            ILaunchParam[] launchParams =
            [
                new LaunchParamDict
    {
        Id = "resolution",
        Name = Locale.Get("Resolution"),
        NameKey = "Resolution",
        Description = null,
        DescriptionKey = null,
        ShortDescription = "Game window resolution",
        Dictionary = new()
        {
            ["2560×1440"] = "+szx 2560 +szy 1440",
            ["1920×1080"] = "+szx 1920 +szy 1080",
            ["1920x1440"] = "+szx 1920 +szy 1440",
            ["1600×900"] = "+szx 1600 +szy 900",
            ["1280×1024"] = "+szx 1280 +szy 1024",
            ["1024×768"] = "+szx 1024 +szy 768",
            ["800×600"] = "+szx 800 +szy 600"
        }
    },
    new LaunchParamBool
    {
        Id = "fullscreen",
        Name = Locale.Get("FullScreen"),
        NameKey = "FullScreen",
        Description = Locale.Get("DescFullscreen"),
        DescriptionKey = "DescFullscreen",
        ShortDescription = ""
    },
    new LaunchParamBool
    {
        Id = "widescreen",
        Name = Locale.Get("WideScreen"),
        NameKey = "WideScreen",
        Description = Locale.Get("DescWidescreen"),
        DescriptionKey = "DescWidescreen",
        ShortDescription = ""
    },
    new LaunchParamBool
    {
        Id = "multi",
        Name = Locale.Get("Multi"),
        NameKey = "Multi",
        Description = Locale.Get("DescMulti"),
        DescriptionKey = "DescMulti",
        ShortDescription = ""
    },
    new LaunchParamBool
    {
        Id = "lowPriority",
        Name = Locale.Get("LowPriority"),
        NameKey = "LowPriority",
        Description = null,
        ShortDescription = ""
    },
    new LaunchParamBool
    {
        Id = "noSound",
        Name = Locale.Get("NoSound"),
        NameKey = "NoSound",
        Description = null,
        ShortDescription = ""
    },
    new LaunchParamCustom
    {
        Id = "customparam",
        Name = Locale.Get("CustomParam"),
        NameKey = "CustomParam",
        Description = null,
        ShortDescription = null,
        FullFormat = true
    }
            ];


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