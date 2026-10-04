using Avalonia;
using System;
using AutoUpdaterDotNET;
using NBL.Services;

namespace NBLApp;

class Program
{
    private const string UpdateUrl =
        "https://raw.githubusercontent.com/VordyV/NetBatLauncher/master/update.xml";

    [STAThread]
    public static void Main(string[] args)
    {
        Logger.Initialize();

        Logger.Info("Application starting");
        Logger.Info($"Arguments: {(args.Length > 0 ? string.Join(" ", args) : "none")}");

        try
        {
            if (args.Length > 0 &&
                args[0] == "--set-game-language")
            {
                Logger.Info("Command line mode: --set-game-language");

                int result =
                    GameLanguageService.HandleCommandLine(args);

                Logger.Info($"Game language command finished with code: {result}");

                Environment.Exit(result);
            }

            if (args.Length > 0 &&
                args[0] == "--set-game-key")
            {
                Logger.Info("Command line mode: --set-game-key");

                int result =
                    GameKeyService.HandleCommandLine(args);

                Logger.Info($"Game key command finished with code: {result}");

                Environment.Exit(result);
            }

            Logger.Info("Starting AutoUpdater");
            Logger.Info($"Update URL: {UpdateUrl}");

            AutoUpdater.Start("https://raw.githubusercontent.com/VordyV/NetBatLauncher/main/update.xml");

            Logger.Info("Starting Avalonia application");

            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);

            Logger.Info("Avalonia application closed");
        }
        catch (Exception ex)
        {
            Logger.Error("Unhandled exception in Program.Main", ex);

            throw;
        }
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        Logger.Info("Building Avalonia application");

        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
    }
}