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
    public int CompletedFiles { get; init; }

    public int TotalFiles { get; init; }

    public long DownloadedBytes { get; init; }

    public long TotalBytes { get; init; }

    public int Percent { get; init; }

    public double BytesPerSecond { get; init; }

    public TimeSpan? EstimatedTimeRemaining { get; init; }

    public bool IsDownloading { get; init; }

    public List<ActiveDownloadProgress> ActiveDownloads { get; init; } =
        new();
}

public class ClientUpdateService
{
    private const int MaxConcurrentDownloads = 3;

    private readonly DownloadService _downloadService;
    private readonly IntroVideoService _introVideoService;

    public ClientUpdateService()
    {
        _downloadService =
            new DownloadService();

        _introVideoService =
            new IntroVideoService();
    }

    public async Task<GameManifest> GetManifestAsync(
        string server,
        string gameId,
        CancellationToken cancellationToken = default)
    {
        string url =
            $"{server.TrimEnd('/')}" +
            $"/api/games/files/manifest?gameid=" +
            $"{Uri.EscapeDataString(gameId)}";

        Logger.Info($"Getting game manifest: {url}");

        string json =
            await _downloadService.GetStringAsync(
                url,
                cancellationToken);

        var options =
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

        GameManifest manifest =
            JsonSerializer.Deserialize<GameManifest>(
                json,
                options)
            ?? throw new InvalidOperationException(
                "API вернул пустой manifest.");

        Logger.Info(
            $"Manifest received: game={gameId}, " +
            $"files={manifest.Files.Count}");

        return manifest;
    }

    public async Task UpdateAsync(
        string server,
        string gameId,
        string gamePath,
        IProgress<ClientUpdateProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Logger.Info(
            $"Starting client update: " +
            $"game={gameId}, path={gamePath}");

        GameManifest manifest =
            await GetManifestAsync(
                server,
                gameId,
                cancellationToken);

        GameManifestFile[] files =
            manifest.Files
                .Where(file =>
                    !_introVideoService.IsIntroVideoDisabled(
                        gamePath,
                        file.Path))
                .ToArray();

        int totalFiles =
            files.Length;

        Logger.Info(
            $"Files in manifest: {manifest.Files.Count}, " +
            $"files to process: {totalFiles}");

        if (totalFiles == 0)
        {
            Logger.Info("No files need to be processed.");

            progress?.Report(
                new ClientUpdateProgress
                {
                    CompletedFiles = 0,
                    TotalFiles = 0,
                    DownloadedBytes = 0,
                    TotalBytes = 0,
                    Percent = 100,
                    BytesPerSecond = 0,
                    EstimatedTimeRemaining =
                        TimeSpan.Zero,
                    IsDownloading = false
                });

            return;
        }

        var downloadedByFile =
            new ConcurrentDictionary<int, long>();

        var totalBytesByFile =
            new ConcurrentDictionary<int, long>();

        var filesToDownload =
            new List<(int Index, GameManifestFile File)>();

        int completedFiles = 0;

        for (int i = 0; i < files.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            GameManifestFile file =
                files[i];

            if (string.IsNullOrWhiteSpace(file.Path))
            {
                completedFiles++;
                continue;
            }

            string relativePath =
                file.Path.Replace(
                    '/',
                    Path.DirectorySeparatorChar);

            string localPath =
                Path.Combine(
                    gamePath,
                    relativePath);

            string? directory =
                Path.GetDirectoryName(
                    localPath);

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

                Logger.Info(
                    $"File needs download: {file.Path}");
            }
            else
            {
                long localSize =
                    new FileInfo(localPath).Length;

                downloadedByFile[i] =
                    localSize;

                totalBytesByFile[i] =
                    localSize;

                completedFiles++;

                Logger.Info(
                    $"File already up to date: {file.Path}");
            }
        }

        var activeDownloads =
            new ConcurrentDictionary<
                int,
                ActiveDownloadProgress>();

        Stopwatch stopwatch =
            Stopwatch.StartNew();

        ReportProgress(
            progress,
            completedFiles,
            totalFiles,
            downloadedByFile,
            totalBytesByFile,
            activeDownloads,
            stopwatch,
            filesToDownload.Count > 0);

        if (filesToDownload.Count == 0)
        {
            stopwatch.Stop();

            Logger.Info(
                $"Update completed without downloads: " +
                $"files={totalFiles}");

            ReportProgress(
                progress,
                totalFiles,
                totalFiles,
                downloadedByFile,
                totalBytesByFile,
                activeDownloads,
                stopwatch,
                false);

            return;
        }

        Logger.Info(
            $"Starting downloads: " +
            $"files={filesToDownload.Count}, " +
            $"maxConcurrent={MaxConcurrentDownloads}");

        using var semaphore =
            new SemaphoreSlim(
                MaxConcurrentDownloads,
                MaxConcurrentDownloads);

        var tasks =
            new List<Task>();

        foreach (var item in filesToDownload)
        {
            tasks.Add(
                DownloadSingleFileAsync(
                    gamePath,
                    item.Index,
                    item.File,
                    totalFiles,
                    downloadedByFile,
                    totalBytesByFile,
                    activeDownloads,
                    semaphore,
                    stopwatch,
                    progress,
                    cancellationToken));
        }

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
            Logger.Warning(
                "Client update cancelled.");

            throw;
        }
        catch (Exception ex)
        {
            Logger.Error(
                "Client update failed.",
                ex);

            throw;
        }

        stopwatch.Stop();

        ReportProgress(
            progress,
            totalFiles,
            totalFiles,
            downloadedByFile,
            totalBytesByFile,
            activeDownloads,
            stopwatch,
            false);

        Logger.Info(
            $"Client update completed: " +
            $"files={totalFiles}, " +
            $"elapsed={stopwatch.Elapsed}");
    }

    private async Task DownloadSingleFileAsync(
        string gamePath,
        int fileIndex,
        GameManifestFile file,
        int totalFiles,
        ConcurrentDictionary<int, long> downloadedByFile,
        ConcurrentDictionary<int, long> totalBytesByFile,
        ConcurrentDictionary<int, ActiveDownloadProgress> activeDownloads,
        SemaphoreSlim semaphore,
        Stopwatch stopwatch,
        IProgress<ClientUpdateProgress>? progress,
        CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(
            cancellationToken);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            Logger.Info(
                $"Download started: {file.Path}");

            string relativePath =
                file.Path.Replace(
                    '/',
                    Path.DirectorySeparatorChar);

            string localPath =
                Path.Combine(
                    gamePath,
                    relativePath);

            var fileProgress =
                new Progress<DownloadProgress>(
                    downloadProgress =>
                    {
                        long currentBytes =
                            Math.Max(
                                0,
                                downloadProgress.SizeCurrent);

                        long totalFileBytes =
                            Math.Max(
                                0,
                                downloadProgress.SizeTotal);

                        if (totalFileBytes > 0)
                        {
                            totalBytesByFile[fileIndex] =
                                totalFileBytes;
                        }

                        downloadedByFile[fileIndex] =
                            currentBytes;

                        activeDownloads[fileIndex] =
                            new ActiveDownloadProgress
                            {
                                FileIndex =
                                    fileIndex,

                                FileName =
                                    file.Path,

                                Percent =
                                    downloadProgress.Percent,

                                DownloadedBytes =
                                    currentBytes,

                                TotalBytes =
                                    totalFileBytes
                            };

                        ReportProgress(
                            progress,
                            CountCompletedFiles(
                                downloadedByFile,
                                totalBytesByFile),
                            totalFiles,
                            downloadedByFile,
                            totalBytesByFile,
                            activeDownloads,
                            stopwatch,
                            true);
                    });

            await _downloadService.DownloadFileAsync(
                file.Url,
                localPath,
                fileProgress,
                cancellationToken);

            Logger.Info(
                $"Download finished, checking SHA-256: " +
                $"{file.Path}");

            string localHash =
                await HashHelper.CalculateSha256Async(
                    localPath,
                    cancellationToken);

            if (!string.Equals(
                    localHash,
                    file.ChecksumSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                Logger.Warning(
                    $"SHA-256 mismatch: {file.Path}");

                try
                {
                    File.Delete(localPath);
                }
                catch (Exception ex)
                {
                    Logger.Warning(
                        $"Could not delete invalid file: " +
                        $"{localPath}. {ex.Message}");
                }

                throw new InvalidDataException(
                    $"Контрольная сумма файла '{file.Path}' " +
                    $"не совпадает с manifest.");
            }

            long actualFileSize =
                new FileInfo(localPath).Length;

            totalBytesByFile[fileIndex] =
                actualFileSize;

            downloadedByFile[fileIndex] =
                actualFileSize;

            activeDownloads.TryRemove(
                fileIndex,
                out _);

            Logger.Info(
                $"File verified successfully: " +
                $"{file.Path}");

            ReportProgress(
                progress,
                CountCompletedFiles(
                    downloadedByFile,
                    totalBytesByFile),
                totalFiles,
                downloadedByFile,
                totalBytesByFile,
                activeDownloads,
                stopwatch,
                activeDownloads.Count > 0);
        }
        catch (Exception ex)
        {
            Logger.Error(
                $"Failed to download/update file: " +
                $"{file.Path}",
                ex);

            activeDownloads.TryRemove(
                fileIndex,
                out _);

            throw;
        }
        finally
        {
            semaphore.Release();
        }
    }

    private static int CountCompletedFiles(
        ConcurrentDictionary<int, long> downloadedByFile,
        ConcurrentDictionary<int, long> totalBytesByFile)
    {
        int completedFiles = 0;

        foreach (int fileIndex in downloadedByFile.Keys)
        {
            if (!totalBytesByFile.TryGetValue(
                    fileIndex,
                    out long totalBytes))
            {
                continue;
            }

            long downloadedBytes =
                downloadedByFile[fileIndex];

            if (totalBytes > 0 &&
                downloadedBytes >= totalBytes)
            {
                completedFiles++;
            }
        }

        return completedFiles;
    }

    private static async Task<bool> NeedsDownloadAsync(
     string localPath,
     GameManifestFile remoteFile,
     CancellationToken cancellationToken)
    {
        if (!File.Exists(localPath))
        {
            Logger.Debug(
                $"File does not exist: '{localPath}'");

            return true;
        }

        string localHash =
            await HashHelper.CalculateSha256Async(
                localPath,
                cancellationToken);

        if (string.IsNullOrEmpty(localHash))
        {
            Logger.Warning(
                $"Could not calculate SHA-256: '{localPath}'");

            return true;
        }

        bool matches =
            string.Equals(
                localHash,
                remoteFile.ChecksumSha256,
                StringComparison.OrdinalIgnoreCase);

        Logger.Debug(
            $"SHA-256 check: '{remoteFile.Path}', " +
            $"matches={matches}");

        return !matches;
    }

private static int CalculateProgress(
    long downloadedBytes,
    long totalBytes)
    {
        if (totalBytes <= 0)
        {
            return 0;
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
        int completedFiles,
        int totalFiles,
        ConcurrentDictionary<int, long> downloadedByFile,
        ConcurrentDictionary<int, long> totalBytesByFile,
        ConcurrentDictionary<int, ActiveDownloadProgress> activeDownloads,
        Stopwatch stopwatch,
        bool isDownloading)
    {
        if (progress == null)
        {
            return;
        }

        long downloadedBytes =
            downloadedByFile.Values.Sum();

        long totalBytes =
            totalBytesByFile.Values.Sum();

        double bytesPerSecond =
            CalculateSpeed(
                downloadedBytes,
                stopwatch.Elapsed);

        TimeSpan? eta =
            CalculateEta(
                downloadedBytes,
                totalBytes,
                stopwatch.Elapsed);

        int percent =
     CalculateProgress(
         downloadedBytes,
         totalBytes);

        progress.Report(
            new ClientUpdateProgress
            {
                CompletedFiles =
                    completedFiles,

                TotalFiles =
                    totalFiles,

                DownloadedBytes =
                    downloadedBytes,

                TotalBytes =
                    totalBytes,

                Percent =
                    percent,

                BytesPerSecond =
                    bytesPerSecond,

                EstimatedTimeRemaining =
                    eta,

                IsDownloading =
                    isDownloading,

                ActiveDownloads =
                    activeDownloads.Values
                        .OrderBy(
                            x => x.FileIndex)
                        .ToList()
            });
    }
}