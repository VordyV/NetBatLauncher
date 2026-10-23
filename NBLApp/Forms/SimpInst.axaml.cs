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
    protected Game Game;
    protected Launcher Launcher;

    private CancellationTokenSource? _installationCancellation;
    private const string DefaultGamePath = @"C:\Games\Battlefield 2142";

    public SimpInst()
    {
        InitializeComponent();

        ApplyLocalization();
    }

    public SimpInst(Launcher launcher, Game game)
    {
        this.Game = game;
        this.Launcher = launcher;

        InitializeComponent();

        PathPickerGamePath.SelectedPathsText = DefaultGamePath;

        ApplyLocalization();

        Logger.Debug($"SimpInst opened: game='{Game.Id}'");
    }

    private void ApplyLocalization()
    {
        try
        {
            TextBlockPath.Text = Locale.Get("GamePath");
            TextBlockStatus.Text = Locale.Get("Status");
            TextBlockSpeed.Text = Locale.Get("InstallSpeed") + ": —";
            TextBlockEta.Text = Locale.Get("InstallRemaining") + ": —";
            TextBlockOverall.Text = Locale.Get("InstallOverallProgress") + ": 0%";
            ButtonCancel.Content = Locale.Get("Cancel");
            ButtonSave.Content = Locale.Get("Install");

            Logger.Debug("SimpInst localization applied");
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to apply SimpInst localization: {ex.Message}");
        }
    }

    private void ButtonCancel_OnClick(object? sender, RoutedEventArgs e)
    {
        Logger.Info("Installation cancel button clicked");

        if (_installationCancellation != null)
        {
            Logger.Info("Cancelling game installation");
            _installationCancellation.Cancel();
            return;
        }

        if (DataContext is IDialogContext ctx)
        {
            Logger.Debug("Closing installation dialog");
            ctx.Close();
        }
    }

    protected void ShowError(string text)
    {
        Logger.Error($"Installation UI error: {text}");
        TextBlockError.IsVisible = true;
        TextBlockError.Text = text;
    }

    private async void ButtonSave_OnClick(object? sender, RoutedEventArgs e)
    {
        Logger.Info("Install button clicked");
        string? path = PathPickerGamePath.SelectedPathsText;

        if (string.IsNullOrWhiteSpace(path))
        {
            Logger.Warning("Installation cancelled: game path is empty");
            ShowError(Locale.Get("InvalidGamePath"));
            return;
        }

        path = path.Trim();

        Logger.Info($"Selected game installation path: '{path}'");
        TextBlockError.IsVisible = false;
        await InstallGame(path);
    }


    private async Task InstallGame(string path)
    {
        Logger.Info($"Game installation started: game='{Game.Id}', path='{path}'");

        try
        {
            SetInstallingState(true);
            _installationCancellation = new CancellationTokenSource();
            TextBlockStatus.Text = Locale.Get("InstallStatus");
            Directory.CreateDirectory(path);
            Game.AddGameRegistry(path);

            var update = new ClientUpdateService();
            var progress = new Progress<ClientUpdateProgress>(UpdateProgress);

            await update.InstallGameAsync(Launcher.ServerAddress, "bf2142", path, progress, _installationCancellation.Token);
            TextBlockStatus.Text = Locale.Get("InstallFinishing");

            await update.InstallClientAsync(Launcher.ServerAddress, "bf2142", path, Launcher.ClientId, progress, _installationCancellation.Token);
            await Game.GetClients();
            Logger.Info($"Clients after reload: {string.Join(", ", Game.Clients.Keys)}");

            if (!Game.Clients.TryGetValue(Launcher.ClientId, out ClientData? client))
                throw new Exception("Не удалось создать манифест Original.");

            Game.SetReferenceClient(Launcher.ClientId);
            await Game.SetStateSnapshot(new StateSnapshot
            {
                Files = new()
            });


            await Game.ChangeClientGame(Launcher.ClientId);
            Game.SetCurrentClient(Launcher.ClientId);

            // Завершаем сессию скачивания на сервере.
            bool sessionEnded = await update.EndSessionAsync(
                Launcher.ServerAddress);

            if (!sessionEnded)
                Logger.Warning("Игра установлена, но сервер не подтвердил завершение сессии.");

            TextBlockStatus.Text = Locale.Get("InstallCompleted");


            TextBlockStatus.Text = Locale.Get("InstallCompleted");
            ProgressBarOverall.Value = 100;
            TextBlockOverall.Text = $"{Locale.Get("InstallOverallProgress")}: 100%";
            TextBlockSpeed.Text = Locale.Get("InstallSpeed") + ": —";
            TextBlockEta.Text = Locale.Get("InstallRemaining") + ": 00:00";

            Logger.Info($"Game installation completed: game='{Game.Id}', path='{path}'");
            await Task.Delay(500);
            if (DataContext is IDialogContext ctx) ctx.Close();
        }
        catch (OperationCanceledException)
        {
            Logger.Warning($"Game installation cancelled: game='{Game.Id}'");
            TextBlockStatus.Text = Locale.Get("InstallCancelled");
            TextBlockSpeed.Text = Locale.Get("InstallSpeed") + ": —";
            TextBlockEta.Text = Locale.Get("InstallRemaining") + ": —";
            SetInstallingState(false);
        }
        catch (Exception ex)
        {
            Logger.Error($"Game installation failed: game='{Game.Id}', path='{path}'", ex);
            ShowError(ex.Message);
            SetInstallingState(false);
        }
        finally
        {
            _installationCancellation?.Dispose();
            _installationCancellation = null;
        }
    }

    private void UpdateProgress(ClientUpdateProgress progress)
    {
        PanelProgress.IsVisible = true;

        TextBlockStatus.Text = progress.IsDownloading
            ? Locale.Get("InstallDownloading")
            : Locale.Get("InstallChecking");

        ProgressBarOverall.Value = progress.Percent;

        TextBlockOverall.Text =
            $"{Locale.Get("InstallOverallProgress")}: " +
            $"{progress.Percent}% ({progress.CompletedFiles}/{progress.TotalFiles}) " +
            $"— {FormatBytes(progress.DownloadedBytes)} / {FormatBytes(progress.TotalBytes)}";

        TextBlockSpeed.Text = progress.BytesPerSecond > 0
            ? $"{Locale.Get("InstallSpeed")}: {FormatSpeed(progress.BytesPerSecond)}"
            : $"{Locale.Get("InstallSpeed")}: —";

        TextBlockEta.Text = progress.EstimatedTimeRemaining.HasValue
            ? $"{Locale.Get("InstallRemaining")}: " +
              FormatTime(progress.EstimatedTimeRemaining.Value)
            : $"{Locale.Get("InstallRemaining")}: —";

        TextBlockDownloads.Text = progress.ActiveDownloads.Count > 0
            ? string.Join(
                Environment.NewLine,
                progress.ActiveDownloads.Select(download =>
                    $"{download.FileName} — {download.Percent}% " +
                    $"({FormatBytes(download.DownloadedBytes)} / " +
                    $"{FormatBytes(download.TotalBytes)})"))
            : string.Empty;
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 0)
            return "—";

        string[] units = { "Б", "КБ", "МБ", "ГБ", "ТБ" };
        double size = bytes;
        int unit = 0;

        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return $"{size:F1} {units[unit]}";
    }

    private static string FormatSpeed(double bytesPerSecond)
    {
        if (bytesPerSecond < 1024)
        {
            return $"{bytesPerSecond:F0} B/s";
        }

        if (bytesPerSecond < 1024 * 1024)
        {
            return $"{bytesPerSecond / 1024:F1} KB/s";
        }

        if (bytesPerSecond < 1024 * 1024 * 1024)
        {
            return $"{bytesPerSecond / (1024 * 1024):F1} MB/s";
        }

        return $"{bytesPerSecond / (1024 * 1024 * 1024):F2} GB/s";
    }

    private static string FormatTime(TimeSpan time)
    {
        return $"{(int)time.TotalHours:00}." +$"{time.Minutes:00}." +$"{time.Seconds:00}";
    }

    private void SetInstallingState(bool installing)
    {
        PanelProgress.IsVisible = installing;
        PathPickerGamePath.IsEnabled = !installing;
        ButtonSave.IsEnabled = !installing;
        ButtonCancel.Content = installing ? Locale.Get("Cancel") : Locale.Get("Close");
    }
}