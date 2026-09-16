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
            await updateService.UpdateAsync(
                server,
                path,
                progress,
                _installationCancellation.Token);
            string clientFilesPath =
                Path.Combine(
                    AppContext.BaseDirectory,
                    "ClientFiles");
            string sourceBf2142 =
                Path.Combine(
                    clientFilesPath,
                    "BF2142.exe");
            string sourceRendDx9 =
                Path.Combine(
                    clientFilesPath,
                    "RendDX9.dll");
            string sourceRendDx9Ori =
                Path.Combine(
                    clientFilesPath,
                    "RendDX9_ori.dll");
            string targetBf2142 =
                Path.Combine(
                    path,
                    "BF2142.exe");
            string targetRendDx9 =
                Path.Combine(
                    path,
                    "RendDX9.dll");
            string targetRendDx9Ori =
                Path.Combine(
                    path,
                    "RendDX9_ori.dll");
            if (!File.Exists(sourceBf2142))
            {
                throw new FileNotFoundException(
                    "Не найден BF2142.exe.",
                    sourceBf2142);
            }
            if (!File.Exists(sourceRendDx9))
            {
                throw new FileNotFoundException(
                    "Не найден RendDX9.dll.",
                    sourceRendDx9);
            }
            if (!File.Exists(sourceRendDx9Ori))
            {
                throw new FileNotFoundException(
                    "Не найден RendDX9_ori.dll.",
                    sourceRendDx9Ori);
            }
            File.Copy(
                sourceBf2142,
                targetBf2142,
                true);
            File.Copy(
                sourceRendDx9,
                targetRendDx9,
                true);
            File.Copy(
                sourceRendDx9Ori,
                targetRendDx9Ori,
                true);
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