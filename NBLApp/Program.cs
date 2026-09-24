using Avalonia;
using System;
using AutoUpdaterDotNET;
using NBL.Services;

namespace NBLApp;

class Program
{
    /// private const string UpdateUrl = "https://netbat2142api"; жду API для обновлений

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
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

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
};