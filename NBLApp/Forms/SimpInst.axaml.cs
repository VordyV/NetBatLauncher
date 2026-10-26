
using Avalonia.Controls;
using Avalonia.Interactivity;
using Irihi.Avalonia.Shared.Contracts;
using NBL;
using NBL.Services;
using NBLApp.Localization;
using System;
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
    private readonly string? _clientId;
    private readonly bool _syncOnly;
    public bool OperationSucceeded { get; private set; }
    private const string DefaultGamePath = @"C:\Games\Battlefield 2142";

    public SimpInst()
    {
        InitializeComponent();
        ApplyLocalization();
    }

    public SimpInst(Launcher launcher, Game game)
    {
        Game = game;
        Launcher = launcher;
        InitializeComponent();
        PathPickerGamePath.SelectedPathsText = DefaultGamePath;
        ApplyLocalization();
        Logger.Debug($"SimpInst opened: game='{Game.Id}'");
    }

    public SimpInst(Launcher launcher, Game game, string clientId, bool syncOnly)
    {
        Game = game;
        Launcher = launcher;
        _clientId = clientId;
        _syncOnly = syncOnly;

        InitializeComponent();
        ApplyLocalization();

        TextBlockPath.Text = $"{(_syncOnly ? "Синхронизация" : "Установка клиента")}: {ClientName(clientId)}";
        PathPickerGamePath.IsVisible = false;
        ButtonSave.IsVisible = false;
        PanelProgress.IsVisible = true;
        TextBlockStatus.Text = _syncOnly ? "Подготовка синхронизации..." : "Подготовка клиента...";

        Loaded += async (_, _) => await RunClientOperationAsync();
        Logger.Info($"Client dialog opened: client={clientId}, sync={syncOnly}");
    }

    private static string ClientName(string id) => id.ToLowerInvariant() switch
    {
        "original" => "Original",
        "respy" => "Respy",
        "novgames" => "NovGames",
        _ => id
    };

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
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to apply SimpInst localization: {ex.Message}");
        }
    }

    private void ButtonCancel_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_installationCancellation != null)
        {
            _installationCancellation.Cancel();
            return;
        }

        if (DataContext is IDialogContext ctx)
            ctx.Close();
    }

    protected void ShowError(string text)
    {
        Logger.Error($"Installation UI error: {text}");
        TextBlockError.IsVisible = true;
        TextBlockError.Text = text;
    }

    private async void ButtonSave_OnClick(object? sender, RoutedEventArgs e)
    {
        string? path = PathPickerGamePath.SelectedPathsText;

        if (string.IsNullOrWhiteSpace(path))
        {
            ShowError(Locale.Get("InvalidGamePath"));
            return;
        }

        TextBlockError.IsVisible = false;
        await InstallGame(path.Trim());
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
            var token = _installationCancellation.Token;

            await update.InstallGameAsync(Launcher.ServerAddress, "bf2142", path, progress, token);
            TextBlockStatus.Text = Locale.Get("InstallFinishing");

            await update.InstallClientAsync(Launcher.ServerAddress, "bf2142", path, Launcher.ClientId, progress, token);
            await Game.GetClients();

            if (!Game.Clients.ContainsKey(Launcher.ClientId))
                throw new Exception("Не удалось создать манифест Original.");

            Game.SetReferenceClient(Launcher.ClientId);
            await Game.SetStateSnapshot(new StateSnapshot { Files = new() });
            await Game.ChangeClientGame(Launcher.ClientId);
            Game.SetCurrentClient(Launcher.ClientId);

            if (!RegistryKeyService.RegisterGame(path))
            {
                Logger.Warning(
                    $"Game registry registration failed: path='{path}'");
            }
            else
            {
                Logger.Info(
                    $"Game registry registration completed: path='{path}'");
            }

            if (string.IsNullOrWhiteSpace(update.SessionId))
            {
                Logger.Warning("Игра установлена, но API не вернул session_id.");
            }
            else if (!await update.EndSessionAsync(
                Launcher.ServerAddress, update.SessionId))
            {
                Logger.Warning("Игра установлена, но сервер не подтвердил завершение сессии.");
            }

            TextBlockStatus.Text = Locale.Get("InstallCompleted");
            ProgressBarOverall.Value = 100;
            TextBlockOverall.Text = $"{Locale.Get("InstallOverallProgress")}: 100%";
            TextBlockSpeed.Text = Locale.Get("InstallSpeed") + ": —";
            TextBlockEta.Text = Locale.Get("InstallRemaining") + ": 00:00";

            await Task.Delay(500);
            if (DataContext is IDialogContext ctx) ctx.Close();
        }
        catch (OperationCanceledException)
        {
            TextBlockStatus.Text = Locale.Get("InstallCancelled");
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

    private async Task RunClientOperationAsync()
    {
        if (_clientId == null) return;

        try
        {
            SetInstallingState(true);
            _installationCancellation = new CancellationTokenSource();

            var update = new ClientUpdateService();
            var progress = new Progress<ClientUpdateProgress>(UpdateProgress);
            string gamePath = Game.GetPath();

            TextBlockStatus.Text = _syncOnly
                ? "Проверка файлов клиента..."
                : "Загрузка файлов клиента...";

            if (_syncOnly)
                await update.SyncClientAsync(Launcher.ServerAddress, Game.Id, gamePath,
                    _clientId, progress, _installationCancellation.Token);
            else
                await update.InstallClientAsync(Launcher.ServerAddress, Game.Id, gamePath,
                    _clientId, progress, _installationCancellation.Token);

            OperationSucceeded = true;
            TextBlockStatus.Text = _syncOnly ? "Синхронизация завершена" : "Клиент установлен";
            ProgressBarOverall.Value = 100;
            TextBlockOverall.Text = $"{Locale.Get("InstallOverallProgress")}: 100%";
            TextBlockSpeed.Text = Locale.Get("InstallSpeed") + ": —";
            TextBlockEta.Text = Locale.Get("InstallRemaining") + ": 00:00";

            await Task.Delay(500);
            if (DataContext is IDialogContext ctx) ctx.Close();
        }
        catch (OperationCanceledException)
        {
            TextBlockStatus.Text = "Операция отменена";
            SetInstallingState(false);
        }
        catch (Exception ex)
        {
            Logger.Error($"Client operation failed: client={_clientId}", ex);
            TextBlockStatus.Text = "Ошибка операции";
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
            $"{Locale.Get("InstallOverallProgress")}: {progress.Percent}% " +
            $"({progress.CompletedFiles}/{progress.TotalFiles}) — " +
            $"{FormatBytes(progress.DownloadedBytes)} / {FormatBytes(progress.TotalBytes)}";

        TextBlockSpeed.Text = progress.BytesPerSecond > 0
            ? $"{Locale.Get("InstallSpeed")}: {FormatSpeed(progress.BytesPerSecond)}"
            : $"{Locale.Get("InstallSpeed")}: —";

        TextBlockEta.Text = progress.EstimatedTimeRemaining.HasValue
            ? $"{Locale.Get("InstallRemaining")}: {FormatTime(progress.EstimatedTimeRemaining.Value)}"
            : $"{Locale.Get("InstallRemaining")}: —";

        TextBlockDownloads.Text = string.Join(Environment.NewLine,
            progress.ActiveDownloads.Select(d =>
                $"{d.FileName} — {d.Percent}% ({FormatBytes(d.DownloadedBytes)} / {FormatBytes(d.TotalBytes)})"));
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 0) return "—";

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

    private static string FormatSpeed(double speed) => speed < 1024
        ? $"{speed:F0} B/s"
        : speed < 1024 * 1024
            ? $"{speed / 1024:F1} KB/s"
            : $"{speed / (1024 * 1024):F1} MB/s";

    private static string FormatTime(TimeSpan time) =>
        $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";

    private void SetInstallingState(bool installing)
    {
        PanelProgress.IsVisible = installing;
        PathPickerGamePath.IsEnabled = !installing;
        ButtonSave.IsEnabled = !installing;
        ButtonCancel.Content = installing ? Locale.Get("Cancel") : Locale.Get("Close");
    }
}
