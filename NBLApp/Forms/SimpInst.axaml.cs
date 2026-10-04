using Avalonia.Controls;
using Avalonia.Interactivity;
using Irihi.Avalonia.Shared.Contracts;
using NBL;
using NBL.Services;
using NBLApp.Localization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NBLApp.Forms;


public partial class SimpInst : UserControl
{
    protected Game Game = null!;

    private CancellationTokenSource? _installationCancellation;

    private const string DefaultGamePath =
        @"C:\Games\Battlefield 2142";

    public SimpInst()
    {
        InitializeComponent();

        ApplyLocalization();
    }

    public SimpInst(Game game)
    {
        Game = game;

        InitializeComponent();

        PathPickerGamePath.SelectedPathsText =
            DefaultGamePath;

        ApplyLocalization();

        Logger.Debug(
            $"SimpInst opened: game='{Game.Id}'");
    }

    private void ApplyLocalization()
    {
        try
        {
            TextBlockPath.Text =
                Locale.Get("InstallGamePath");

            TextBlockStatus.Text =
                Locale.Get("InstallStatus");

            TextBlockSpeed.Text =
                Locale.Get("InstallSpeed") + ": —";

            TextBlockEta.Text =
                Locale.Get("InstallRemaining") + ": —";

            TextBlockOverall.Text =
                Locale.Get("InstallOverallProgress") + ": 0%";

            ButtonCancel.Content =
                Locale.Get("Cancel");

            ButtonSave.Content =
                Locale.Get("Install");

            Logger.Debug(
                "SimpInst localization applied");
        }
        catch (Exception ex)
        {
            Logger.Warning(
                $"Failed to apply SimpInst localization: {ex.Message}");
        }
    }

    private void ButtonCancel_OnClick(
        object? sender,
        RoutedEventArgs e)
    {
        Logger.Info(
            "Installation cancel button clicked");

        if (_installationCancellation != null)
        {
            Logger.Info(
                "Cancelling game installation");

            _installationCancellation.Cancel();

            return;
        }

        if (DataContext is IDialogContext ctx)
        {
            Logger.Debug(
                "Closing installation dialog");

            ctx.Close();
        }
    }

    protected void ShowError(string text)
    {
        Logger.Error(
            $"Installation UI error: {text}");

        TextBlockError.IsVisible = true;
        TextBlockError.Text = text;
    }

    private async void ButtonSave_OnClick(
        object? sender,
        RoutedEventArgs e)
    {
        Logger.Info(
            "Install button clicked");

        string? path =
            PathPickerGamePath.SelectedPathsText;

        if (string.IsNullOrWhiteSpace(path))
        {
            Logger.Warning(
                "Installation cancelled: game path is empty");

            ShowError(
                Locale.Get("InvalidGamePath"));

            return;
        }

        path = path.Trim();

        Logger.Info(
            $"Selected game installation path: '{path}'");

        TextBlockError.IsVisible = false;

        await InstallGame(path);
    }

    private async Task InstallGame(string path)
    {
        Logger.Info(
            $"Game installation started: " +
            $"game='{Game.Id}', path='{path}'");

        try
        {
            SetInstallingState(true);

            _installationCancellation =
                new CancellationTokenSource();

            TextBlockStatus.Text =
                Locale.Get("InstallStatus");

            Logger.Debug(
                $"Creating game directory: '{path}'");

            Directory.CreateDirectory(path);

            Logger.Debug(
                $"Registering game path: '{path}'");

            Game.AddGameRegistry(path);

            ClientUpdateService updateService =
                new ClientUpdateService();

            const string server =
                "http://api.nbl2142.fun/";

            Logger.Info(
                $"Starting game file update: " +
                $"server='{server}', game='bf2142'");

            var progress =
                new Progress<ClientUpdateProgress>(
                    UpdateProgress);

            await updateService.UpdateAsync(
                server,
                "bf2142",
                path,
                progress,
                _installationCancellation.Token);

            Logger.Info(
                "Game files update completed");

            TextBlockStatus.Text =
                Locale.Get("InstallLauncherFiles");

            TextBlockStatus.Text =
                Locale.Get("InstallFinishing");

            Logger.Info(
                "Generating default client");

            ClientData client =
                await Game.GenerateDefaultClient();

            Logger.Info(
                $"Default client generated: " +
                $"id='{client.ID}', " +
                $"files={client.Files.Length}");

            Game.SetReferenceClient(
                Launcher.ClientId);

            Game.SetCurrentClient(
                Launcher.ClientId);

            Dictionary<string, string> files =
                new();

            foreach (var file in client.Files)
            {
                files.Add(
                    file,
                    Launcher.ClientId);
            }

            Logger.Debug(
                $"Creating initial state snapshot: " +
                $"files={files.Count}");

            await Game.SetStateSnapshot(
                new StateSnapshot
                {
                    Files = files
                });

            TextBlockStatus.Text =
                Locale.Get("InstallCompleted");

            ProgressBarOverall.Value =
                100;

            TextBlockOverall.Text =
                $"{Locale.Get("InstallOverallProgress")}: 100%";

            TextBlockSpeed.Text =
                Locale.Get("InstallSpeed") + ": —";

            TextBlockEta.Text =
                Locale.Get("InstallRemaining") + ": 00:00";

            Logger.Info(
                $"Game installation completed successfully: " +
                $"game='{Game.Id}', path='{path}'");

            await Task.Delay(500);

            if (DataContext is IDialogContext ctx)
            {
                ctx.Close();
            }
        }
        catch (OperationCanceledException)
        {
            Logger.Warning(
                $"Game installation cancelled: " +
                $"game='{Game.Id}'");

            TextBlockStatus.Text =
                Locale.Get("InstallCancelled");

            TextBlockSpeed.Text =
                Locale.Get("InstallSpeed") + ": —";

            TextBlockEta.Text =
                Locale.Get("InstallRemaining") + ": —";

            SetInstallingState(false);
        }
        catch (Exception exception)
        {
            Logger.Error(
                $"Game installation failed: " +
                $"game='{Game.Id}', path='{path}'",
                exception);

            ShowError(
                exception.Message);

            SetInstallingState(false);
        }
        finally
        {
            _installationCancellation?.Dispose();

            _installationCancellation = null;

            Logger.Debug(
                "Installation cancellation source disposed");
        }
    }

    private void UpdateProgress(
        ClientUpdateProgress progress)
    {
        PanelProgress.IsVisible = true;

        TextBlockStatus.Text =
            progress.IsDownloading
                ? Locale.Get("InstallDownloading")
                : Locale.Get("InstallChecking");

        ProgressBarOverall.Value =
            progress.Percent;

        TextBlockOverall.Text =
            $"{Locale.Get("InstallOverallProgress")}: " +
            $"{progress.Percent}% " +
            $"({progress.CompletedFiles}/" +
            $"{progress.TotalFiles})";

        if (progress.BytesPerSecond > 0)
        {
            TextBlockSpeed.Text =
                $"{Locale.Get("InstallSpeed")}: " +
                $"{FormatSpeed(progress.BytesPerSecond)}";
        }
        else
        {
            TextBlockSpeed.Text =
                Locale.Get("InstallSpeed") + ": —";
        }

        if (progress.EstimatedTimeRemaining.HasValue)
        {
            TextBlockEta.Text =
                $"{Locale.Get("InstallRemaining")}: " +
                $"{FormatTime(progress.EstimatedTimeRemaining.Value)}";
        }
        else
        {
            TextBlockEta.Text =
                Locale.Get("InstallRemaining") + ": —";
        }

        if (progress.ActiveDownloads.Count > 0)
        {
            TextBlockDownloads.Text =
                string.Join(
                    Environment.NewLine,
                    progress.ActiveDownloads.Select(
                        download =>
                            $"{download.FileName} — " +
                            $"{download.Percent}%"));
        }
        else
        {
            TextBlockDownloads.Text =
                string.Empty;
        }
    }

    private static string FormatSpeed(
        double bytesPerSecond)
    {
        if (bytesPerSecond < 1024)
        {
            return
                $"{bytesPerSecond:F0} B/s";
        }

        if (bytesPerSecond < 1024 * 1024)
        {
            return
                $"{bytesPerSecond / 1024:F1} KB/s";
        }

        if (bytesPerSecond < 1024 * 1024 * 1024)
        {
            return
                $"{bytesPerSecond / (1024 * 1024):F1} MB/s";
        }

        return
            $"{bytesPerSecond /
              (1024 * 1024 * 1024):F2} GB/s";
    }

    private static string FormatTime(TimeSpan time)
    {
        return
            $"{(int)time.TotalHours:00}." +
            $"{time.Minutes:00}." +
            $"{time.Seconds:00}";
    }

    private void SetInstallingState(
        bool installing)
    {
        PanelProgress.IsVisible =
            installing;

        PathPickerGamePath.IsEnabled =
            !installing;

        ButtonSave.IsEnabled =
            !installing;

        ButtonCancel.Content =
            installing
                ? Locale.Get("Cancel")
                : Locale.Get("Close");
    }
}