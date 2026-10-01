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
        if (args.Length > 0 &&
            args[0] == "--set-game-language")
        {
            int result =
                GameLanguageService.HandleCommandLine(
                    args);

            Environment.Exit(result);
        }

        if (args.Length > 0 &&
            args[0] == "--set-game-key")
        {
            int result =
                GameKeyService.HandleCommandLine(
                    args);

            Environment.Exit(result);
        }

        AutoUpdater.Start(UpdateUrl);

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}