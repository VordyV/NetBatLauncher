using NBL.Models;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
namespace NBL.Services;
public class ActiveDownloadProgress
{
    public int FileIndex { get; init; }
    public string FileName { get; init; } =
        string.Empty;
    public int Percent { get; init; }
    public long DownloadedBytes { get; init; }
    public long TotalBytes { get; init; }
}
public class ClientUpdateProgress
{
    public int CurrentFileNumber { get; init; }
    public int TotalFiles { get; init; }
    public long DownloadedBytes { get; init; }
    public long TotalBytes { get; init; }
    public int Percent { get; init; }
    public double BytesPerSecond { get; init; }
    public TimeSpan? EstimatedTimeRemaining { get; init; }
    public bool IsDownloading { get; init; }
    public IReadOnlyList<ActiveDownloadProgress> ActiveDownloads { get; init; }
        = Array.Empty<ActiveDownloadProgress>();
}
public class ClientUpdateService
{
    private const int MaxConcurrentDownloads = 3;
    private readonly DownloadService _downloadService;
    public ClientUpdateService()
    {
        _downloadService =
            new DownloadService();
    }
    public async Task<RemoteFileInfo[]> GetFileMapAsync(
        string server,
        CancellationToken cancellationToken = default)
    {
        string url =
            $"{server.TrimEnd('/')}/api/file/map";
        string json =
            await _downloadService.GetStringAsync(
                url,
                cancellationToken);
        var options =
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        return
            JsonSerializer.Deserialize<RemoteFileInfo[]>(
                json,
                options)
            ?? Array.Empty<RemoteFileInfo>();
    }
    public async Task UpdateAsync(
        string server,
        string gamePath,
        IProgress<ClientUpdateProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        RemoteFileInfo[] files =
            await GetFileMapAsync(
                server,
                cancellationToken);
        if (files.Length == 0)
        {
            progress?.Report(
                new ClientUpdateProgress
                {
                    CurrentFileNumber = 0,
                    TotalFiles = 0,
                    DownloadedBytes = 0,
                    TotalBytes = 0,
                    Percent = 100,
                    BytesPerSecond = 0,
                    EstimatedTimeRemaining = TimeSpan.Zero,
                    IsDownloading = false,
                    ActiveDownloads =
                        Array.Empty<ActiveDownloadProgress>()
                });
            return;
        }
        long totalBytes =
            files.Sum(
                x => Math.Max(0, x.Size));
        var downloadedByFile =
            new ConcurrentDictionary<int, long>();
        var filesToDownload =
            new List<(int Index, RemoteFileInfo File)>();
        for (int i = 0; i < files.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RemoteFileInfo file =
                files[i];
            if (string.IsNullOrWhiteSpace(file.File))
            {
                continue;
            }
            string relativePath =
                file.File.Replace(
                    '/',
                    Path.DirectorySeparatorChar);
            string localPath =
                Path.Combine(
                    gamePath,
                    relativePath);
            string? directory =
                Path.GetDirectoryName(localPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            bool needsDownload =
                await NeedsDownloadAsync(
                    localPath,
                    file,
                    cancellationToken);
            if (needsDownload)
            {
                filesToDownload.Add(
                    (i, file));
                downloadedByFile[i] = 0;
            }
            else
            {
                downloadedByFile[i] =
                    Math.Max(0, file.Size);
            }
        }
        long initialDownloaded =
            downloadedByFile.Values.Sum();
        int initialPercent =
            totalBytes > 0
                ? (int)(
                    initialDownloaded *
                    100L /
                    totalBytes)
                : 100;
        ReportProgress(
            progress,
            files,
            files.Length,
            downloadedByFile,
            new ConcurrentDictionary<int, ActiveDownloadProgress>(),
            initialDownloaded,
            totalBytes,
            initialPercent,
            0,
            null,
            false);
        if (filesToDownload.Count == 0)
        {
            ReportProgress(
                progress,
                files,
                files.Length,
                downloadedByFile,
                new ConcurrentDictionary<int, ActiveDownloadProgress>(),
                totalBytes,
                totalBytes,
                100,
                0,
                TimeSpan.Zero,
                false);
            return;
        }
        var activeDownloads =
            new ConcurrentDictionary<int, ActiveDownloadProgress>();
        using var semaphore =
            new SemaphoreSlim(
                MaxConcurrentDownloads,
                MaxConcurrentDownloads);
        Stopwatch stopwatch =
            Stopwatch.StartNew();
        var tasks =
            new List<Task>();
        foreach (var item in filesToDownload)
        {
            int fileIndex =
                item.Index;
            RemoteFileInfo file =
                item.File;
            tasks.Add(
                DownloadSingleFileAsync(
                    server,
                    gamePath,
                    fileIndex,
                    file,
                    files,
                    downloadedByFile,
                    activeDownloads,
                    semaphore,
                    totalBytes,
                    stopwatch,
                    progress,
                    cancellationToken));
        }
        await Task.WhenAll(tasks);
        stopwatch.Stop();
        ReportProgress(
            progress,
            files,
            files.Length,
            downloadedByFile,
            activeDownloads,
            totalBytes,
            totalBytes,
            100,
            CalculateSpeed(
                totalBytes,
                stopwatch.Elapsed),
            TimeSpan.Zero,
            false);
    }
    private async Task DownloadSingleFileAsync(
        string server,
        string gamePath,
        int fileIndex,
        RemoteFileInfo file,
        RemoteFileInfo[] files,
        ConcurrentDictionary<int, long> downloadedByFile,
        ConcurrentDictionary<int, ActiveDownloadProgress> activeDownloads,
        SemaphoreSlim semaphore,
        long totalBytes,
        Stopwatch stopwatch,
        IProgress<ClientUpdateProgress>? progress,
        CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(
            cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            string relativePath =
                file.File.Replace(
                    '/',
                    Path.DirectorySeparatorChar);
            string localPath =
                Path.Combine(
                    gamePath,
                    relativePath);
            activeDownloads[fileIndex] =
                new ActiveDownloadProgress
                {
                    FileIndex =
                        fileIndex,
                    FileName =
                        file.File,
                    Percent =
                        0,
                    DownloadedBytes =
                        0,
                    TotalBytes =
                        file.Size
                };
            ReportProgress(
                progress,
                files,
                fileIndex + 1,
                downloadedByFile,
                activeDownloads,
                downloadedByFile.Values.Sum(),
                totalBytes,
                CalculateOverallPercent(
                    downloadedByFile.Values.Sum(),
                    totalBytes),
                CalculateSpeed(
                    downloadedByFile.Values.Sum(),
                    stopwatch.Elapsed),
                CalculateEta(
                    downloadedByFile.Values.Sum(),
                    totalBytes,
                    stopwatch.Elapsed),
                true);
            string url =
                $"{server.TrimEnd('/')}" +
                $"/api/file/get?name=" +
                Uri.EscapeDataString(file.File);
            var fileProgress =
                new Progress<DownloadProgress>(
                    downloadProgress =>
                    {
                        long currentBytes =
                            Math.Clamp(
                                downloadProgress.SizeCurrent,
                                0,
                                Math.Max(
                                    0,
                                    file.Size));
                        downloadedByFile[fileIndex] =
                            currentBytes;
                        activeDownloads[fileIndex] =
                            new ActiveDownloadProgress
                            {
                                FileIndex =
                                    fileIndex,
                                FileName =
                                    file.File,
                                Percent =
                                    Math.Clamp(
                                        downloadProgress.Percent,
                                        0,
                                        100),
                                DownloadedBytes =
                                    currentBytes,
                                TotalBytes =
                                    file.Size
                            };
                        long overallDownloaded =
                            downloadedByFile.Values.Sum();
                        double speed =
                            CalculateSpeed(
                                overallDownloaded,
                                stopwatch.Elapsed);
                        ReportProgress(
                            progress,
                            files,
                            fileIndex + 1,
                            downloadedByFile,
                            activeDownloads,
                            overallDownloaded,
                            totalBytes,
                            CalculateOverallPercent(
                                overallDownloaded,
                                totalBytes),
                            speed,
                            CalculateEta(
                                overallDownloaded,
                                totalBytes,
                                stopwatch.Elapsed),
                            true);
                    });
            await _downloadService.DownloadFileAsync(
                url,
                localPath,
                fileProgress,
                cancellationToken);
            downloadedByFile[fileIndex] =
                Math.Max(0, file.Size);
            activeDownloads.TryRemove(
                fileIndex,
                out _);
            long overallDownloaded =
                downloadedByFile.Values.Sum();
            double speed =
                CalculateSpeed(
                    overallDownloaded,
                    stopwatch.Elapsed);
            ReportProgress(
                progress,
                files,
                fileIndex + 1,
                downloadedByFile,
                activeDownloads,
                overallDownloaded,
                totalBytes,
                CalculateOverallPercent(
                    overallDownloaded,
                    totalBytes),
                speed,
                CalculateEta(
                    overallDownloaded,
                    totalBytes,
                    stopwatch.Elapsed),
                activeDownloads.Count > 0);
        }
        finally
        {
            semaphore.Release();
        }
    }
    private static async Task<bool> NeedsDownloadAsync(
        string localPath,
        RemoteFileInfo remoteFile,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(localPath))
        {
            return true;
        }
        FileInfo localFile =
            new FileInfo(localPath);
        if (localFile.Length != remoteFile.Size)
        {
            return true;
        }
        string localMd5 =
            await HashHelper.CalculateMd5Async(
                localPath,
                cancellationToken);
        if (string.IsNullOrEmpty(localMd5))
        {
            return true;
        }
        return !string.Equals(
            localMd5,
            remoteFile.Md5,
            StringComparison.OrdinalIgnoreCase);
    }
    private static int CalculateOverallPercent(
        long downloadedBytes,
        long totalBytes)
    {
        if (totalBytes <= 0)
        {
            return 100;
        }
        return Math.Clamp(
            (int)(
                downloadedBytes *
                100L /
                totalBytes),
            0,
            100);
    }
    private static double CalculateSpeed(
        long downloadedBytes,
        TimeSpan elapsed)
    {
        if (downloadedBytes <= 0 ||
            elapsed.TotalSeconds <= 0)
        {
            return 0;
        }
        return
            downloadedBytes /
            elapsed.TotalSeconds;
    }
    private static TimeSpan? CalculateEta(
        long downloadedBytes,
        long totalBytes,
        TimeSpan elapsed)
    {
        if (downloadedBytes <= 0 ||
            totalBytes <= downloadedBytes ||
            elapsed.TotalSeconds <= 0)
        {
            return totalBytes <= downloadedBytes
                ? TimeSpan.Zero
                : null;
        }
        double speed =
            CalculateSpeed(
                downloadedBytes,
                elapsed);
        if (speed <= 0)
        {
            return null;
        }
        long remaining =
            totalBytes -
            downloadedBytes;
        return TimeSpan.FromSeconds(
            remaining / speed);
    }
    private static void ReportProgress(
        IProgress<ClientUpdateProgress>? progress,
        RemoteFileInfo[] files,
        int currentFileNumber,
        ConcurrentDictionary<int, long> downloadedByFile,
        ConcurrentDictionary<int, ActiveDownloadProgress> activeDownloads,
        long downloadedBytes,
        long totalBytes,
        int percent,
        double bytesPerSecond,
        TimeSpan? eta,
        bool isDownloading)
    {
        if (progress == null)
        {
            return;
        }
        List<ActiveDownloadProgress> active =
            activeDownloads.Values
                .OrderBy(x => x.FileIndex)
                .Take(MaxConcurrentDownloads)
                .ToList();
        progress.Report(
            new ClientUpdateProgress
            {
                CurrentFileNumber =
                    currentFileNumber,
                TotalFiles =
                    files.Length,
                DownloadedBytes =
                    downloadedBytes,
                TotalBytes =
                    totalBytes,
                Percent =
                    Math.Clamp(
                        percent,
                        0,
                        100),
                BytesPerSecond =
                    bytesPerSecond,
                EstimatedTimeRemaining =
                    eta,
                IsDownloading =
                    isDownloading,
                ActiveDownloads =
                    active
            });
    }
}