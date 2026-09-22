using Avalonia.Controls;
using Avalonia.Interactivity;
using NBL;
using NBL.Services;
using Irihi.Avalonia.Shared.Contracts;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
namespace NBLApp.Forms;
public partial class SimpInst : UserControl
{
    protected Game Game = null!;
    private CancellationTokenSource? _installationCancellation;
    private const string DefaultGamePath = @"C:\Games\Battlefield 2142";
    public SimpInst()
    {
        InitializeComponent();
    }
    public SimpInst(Game game)
    {
        Game = game;
        InitializeComponent();
        PathPickerGamePath.SelectedPathsText =
            DefaultGamePath;
    }
    private void ButtonCancel_OnClick(
        object? sender,
        RoutedEventArgs e)
    {
        if (_installationCancellation != null)
        {
            _installationCancellation.Cancel();
            return;
        }
        if (DataContext is IDialogContext ctx)
        {
            ctx.Close();
        }
    }
    protected void ShowError(string text)
    {
        TextBlockError.IsVisible = true;
        TextBlockError.Text = text;
    }
    private async void ButtonSave_OnClick(
        object? sender,
        RoutedEventArgs e)
    {
        string? path =
            PathPickerGamePath.SelectedPathsText;
        if (string.IsNullOrWhiteSpace(path))
        {
            ShowError("Неверный путь");
            return;
        }
        path = path.Trim();
        TextBlockError.IsVisible = false;
        await InstallGame(path);
    }
    private async Task InstallGame(string path)
    {
        try
        {
            SetInstallingState(true);
            _installationCancellation =
                new CancellationTokenSource();
            TextBlockStatus.Text =
                "Установка...";
            Directory.CreateDirectory(path);
            Game.AddGameRegistry(path);
            ClientUpdateService updateService =
                new ClientUpdateService();
            const string server =
                "https://service.2142.stellarbear.space"; /// Жду API для получения актуального сервера
            var progress =
                new Progress<ClientUpdateProgress>(
                    UpdateProgress);
            await updateService.UpdateAsync(server, path, progress, _installationCancellation.Token);

            TextBlockStatus.Text =
                "Установка файлов лаунчера...";

            GameFilePatcher.Install(path);

            TextBlockStatus.Text =
                "Завершение...";
            ClientData client =
                await Game.GenerateDefaultClient();
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
            await Game.SetStateSnapshot(
                new StateSnapshot
                {
                    Files = files
                });
            TextBlockStatus.Text =
                "Установка завершена";
            ProgressBarOverall.Value =
                100;
            TextBlockOverall.Text =
                "Общий прогресс: 100%";
            TextBlockSpeed.Text =
                "Скорость: —";
            TextBlockEta.Text =
                "Осталось: 00:00";
            await Task.Delay(500);
            if (DataContext is IDialogContext ctx)
            {
                ctx.Close();
            }
        }
        catch (OperationCanceledException)
        {
            TextBlockStatus.Text =
                "Установка отменена";
            TextBlockSpeed.Text =
                "Скорость: —";
            TextBlockEta.Text =
                "Осталось: —";
            SetInstallingState(false);
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
            Console.WriteLine(exception);
            SetInstallingState(false);
        }
        finally
        {
            _installationCancellation?.Dispose();
            _installationCancellation = null;
        }
    }
    private void UpdateProgress(
        ClientUpdateProgress progress)
    {
        PanelProgress.IsVisible = true;
        TextBlockStatus.Text =
            "Загрузка файлов...";
        ProgressBarOverall.Value =
            progress.Percent;
        TextBlockOverall.Text =
            $"Общий прогресс: {progress.Percent}%";
        if (progress.BytesPerSecond > 0)
        {
            TextBlockSpeed.Text =
                $"Скорость: " +
                $"{FormatSpeed(progress.BytesPerSecond)}";
        }
        else
        {
            TextBlockSpeed.Text =
                "Скорость: —";
        }
        if (progress.EstimatedTimeRemaining.HasValue)
        {
            TextBlockEta.Text =
                $"Осталось: " +
                $"{FormatTime(
                    progress.EstimatedTimeRemaining.Value)}";
        }
        else
        {
            TextBlockEta.Text =
                "Осталось: —";
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
    private static string FormatTime(
        TimeSpan time)
    {
        if (time.TotalHours >= 1)
        {
            return
                time.ToString(
                    @"hh\:mm\:ss");
        }
        if (time.TotalMinutes >= 1)
        {
            return
                time.ToString(
                    @"mm\:ss");
        }
        return
            $"{Math.Max(
                0,
                (int)time.TotalSeconds):00} сек";
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
                ? "Отмена"
                : "Закрыть";
    }
}