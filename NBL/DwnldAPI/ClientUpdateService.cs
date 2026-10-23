using NBL;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace NBL.Services;

public class ActiveDownloadProgress
{
    public int FileIndex { get; init; }
    public string FileName { get; init; } = string.Empty;
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
    public List<ActiveDownloadProgress> ActiveDownloads { get; init; } = new();
}

public class ClientUpdateService
{
    private const int MaxConcurrentDownloads = 3;
    private readonly DownloadService _downloadService = new();
    private readonly IntroVideoService _introVideoService = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public async Task<GameManifest> GetManifestAsync(
        string server, string gameId, CancellationToken cancellationToken = default,
        string? clientId = null)
    {
        string url = $"{server.TrimEnd('/')}/api/games/files/manifest?gameid={Uri.EscapeDataString(gameId)}";
        if (!string.IsNullOrWhiteSpace(clientId))
            url += $"&clientid={Uri.EscapeDataString(clientId)}";

        Logger.Info($"Getting manifest: {url}");
        string json = await _downloadService.GetStringAsync(url, cancellationToken);
        GameManifest manifest = JsonSerializer.Deserialize<GameManifest>(json, JsonOptions)
            ?? throw new InvalidOperationException("API вернул пустой manifest.");

        Logger.Info($"Manifest: client={clientId ?? "default"}, files={manifest.Files.Count}, firstFiles={string.Join(", ", manifest.Files.Take(5).Select(f => f.Path))}");
        return manifest;
    }


    public async Task<bool> EndSessionAsync(
        string server, CancellationToken token = default)
    {
        string url = $"{server.TrimEnd('/')}/api/games/files/manifest/endsession";

        try
        {
            Logger.Info($"Ending download session: {url}");

            using var response = await new HttpClient()
                .GetAsync(url, token);

            response.EnsureSuccessStatusCode();

            Logger.Info($"Download session ended: HTTP {(int)response.StatusCode}");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to end download session: {ex.Message}");
            return false;
        }
    }


    public async Task<GameManifest> GetClientManifestAsync(
    string server, string gameId, string clientId, CancellationToken token = default)
    {
        string url = $"{server.TrimEnd('/')}/api/games/clients/files/manifest" +
            $"?gameid={Uri.EscapeDataString(gameId)}&clientid={Uri.EscapeDataString(clientId)}";

        Logger.Info($"Getting client manifest: {url}");

        string json = await _downloadService.GetStringAsync(url, token);
        GameManifest manifest = JsonSerializer.Deserialize<GameManifest>(json, JsonOptions)
            ?? throw new InvalidOperationException("API вернул пустой client manifest.");

        if (manifest.Files.Count == 0)
            throw new InvalidDataException($"Манифест клиента '{clientId}' пуст.");

        Logger.Info($"Client manifest: client={clientId}, files={manifest.Files.Count}");
        return manifest;
    }

    public async Task DebugClientManifestsAsync(string server, string gameId)
    {
        foreach (string clientId in new[] { "original", "respy", "novgames" })
        {
            GameManifest manifest = await GetManifestAsync(server, gameId, clientId: clientId);
            Logger.Info($"=== CLIENT {clientId}: {manifest.Files.Count} files ===");

            foreach (GameManifestFile file in manifest.Files)
                Logger.Info($"[{clientId}] {file.Path} | {file.ChecksumSha256}");
        }
    }

    // Установка основной игры
    public Task InstallGameAsync(
        string server, string gameId, string gamePath,
        IProgress<ClientUpdateProgress>? progress = null,
        CancellationToken token = default)
        => UpdateAsync(server, gameId, gamePath, progress, token);

    // Обратная совместимость со старым кодом
    public async Task UpdateAsync(
        string server, string gameId, string gamePath,
        IProgress<ClientUpdateProgress>? progress = null,
        CancellationToken cancellationToken = default, string? clientId = null)
    {
        GameManifest manifest = await GetManifestAsync(
            server, gameId, cancellationToken, clientId);

        string targetPath = string.IsNullOrWhiteSpace(clientId)
            ? gamePath
            : Path.Combine(gamePath, Launcher.ClientsDir, clientId);

        await UpdateFilesAsync(
            manifest, gamePath, targetPath, clientId, progress, cancellationToken);
    }

    // Отдельная установка клиента
    public async Task InstallClientAsync(
        string server, string gameId, string gamePath, string clientId,
        IProgress<ClientUpdateProgress>? progress = null,
        CancellationToken token = default)
    {
        GameManifest manifest = await GetClientManifestAsync(server, gameId, clientId, token);
        string clientPath = Path.Combine(gamePath, Launcher.ClientsDir, clientId);

        await UpdateFilesAsync(manifest, gamePath, clientPath, clientId, progress, token);
    }

    private async Task UpdateFilesAsync(
        GameManifest manifest, string gamePath, string targetPath, string? clientId,
        IProgress<ClientUpdateProgress>? progress, CancellationToken token)
    {
        GameManifestFile[] files = manifest.Files
            .Where(f => !string.IsNullOrWhiteSpace(f.Path) &&
                        !_introVideoService.IsIntroVideoDisabled(gamePath, f.Path))
            .ToArray();

        Directory.CreateDirectory(targetPath);
        Logger.Info($"Starting update: client={clientId ?? "default"}, files={files.Length}, path={targetPath}");

        int total = files.Length;
        var downloaded = new ConcurrentDictionary<int, long>();
        var sessionDownloaded = new ConcurrentDictionary<int, long>();
        var sizes = new ConcurrentDictionary<int, long>();
        var sessionSizes = new ConcurrentDictionary<int, long>();
        var completed = new ConcurrentDictionary<int, byte>();
        var active = new ConcurrentDictionary<int, ActiveDownloadProgress>();
        var pending = new List<(int Index, GameManifestFile File)>();

        if (total == 0)
        {
            if (!string.IsNullOrWhiteSpace(clientId))
                await SaveClientManifestAsync(targetPath, clientId, files);

            progress?.Report(new ClientUpdateProgress
            {
                Percent = 100,
                EstimatedTimeRemaining = TimeSpan.Zero
            });
            return;
        }

        for (int i = 0; i < total; i++)
        {
            token.ThrowIfCancellationRequested();
            GameManifestFile file = files[i];
            string localPath = Path.Combine(
                targetPath, file.Path.Replace('/', Path.DirectorySeparatorChar));

            if (file.Size > 0)
                sizes[i] = file.Size;

            Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);

            if (await NeedsDownloadAsync(localPath, file, token))
            {
                pending.Add((i, file));
                downloaded[i] = sessionDownloaded[i] = 0;
                if (file.Size > 0)
                    sessionSizes[i] = file.Size;

                Logger.Info($"File needs download: client={clientId ?? "default"}, file={file.Path}");
            }
            else
            {
                long length = new FileInfo(localPath).Length;
                downloaded[i] = length;
                if (file.Size <= 0)
                    sizes[i] = length;

                completed[i] = 0;
                Logger.Debug($"File already up to date: {file.Path}");
            }
        }

        Stopwatch timer = Stopwatch.StartNew();
        Report(progress, completed, total, downloaded, sessionDownloaded,
            sizes, sessionSizes, active, timer, pending.Count > 0);

        if (pending.Count > 0)
        {
            using var semaphore = new SemaphoreSlim(MaxConcurrentDownloads);
            await Task.WhenAll(pending.Select(x => DownloadFileAsync(
                targetPath, x.Index, x.File, total, downloaded, sessionDownloaded,
                sizes, sessionSizes, completed, active, semaphore, timer, progress, token)));
        }

        timer.Stop();
        Report(progress, completed, total, downloaded, sessionDownloaded,
            sizes, sessionSizes, active, timer, false);

        if (!string.IsNullOrWhiteSpace(clientId))
            await SaveClientManifestAsync(targetPath, clientId, files);

        Logger.Info($"Update completed: client={clientId ?? "default"}, files={total}, elapsed={timer.Elapsed}");
    }

    private static async Task SaveClientManifestAsync(
        string path, string clientId, GameManifestFile[] files)
    {
        string name = clientId.ToLowerInvariant() switch
        {
            "original" => "Original",
            "novgames" => "NovGames",
            "respy" => "Respy",
            _ => clientId
        };

        var client = new ClientData
        {
            ID = clientId,
            Name = name,
            Files = files.Where(f => !string.IsNullOrWhiteSpace(f.Path))
                .Select(f => f.Path.Replace('/', Path.DirectorySeparatorChar)).ToArray()
        };

        await File.WriteAllTextAsync(
            Path.Combine(path, Launcher.ClientManifestFilename),
            JsonSerializer.Serialize(client, JsonOptions));

        Logger.Info($"Client manifest saved: client={clientId}, files={client.Files.Length}");
    }

    public async Task CreateOriginalClientAsync(string server, string gamePath)
    {
        GameManifest manifest = await GetClientManifestAsync(
            server, "bf2142", Launcher.ClientId);

        string clientPath = Path.Combine(gamePath, Launcher.ClientsDir, Launcher.ClientId);
        Directory.CreateDirectory(clientPath);
        string root = Path.GetFullPath(gamePath) + Path.DirectorySeparatorChar;
        string targetRoot = Path.GetFullPath(clientPath) + Path.DirectorySeparatorChar;

        var files = manifest.Files.Where(f => !string.IsNullOrWhiteSpace(f.Path)).ToArray();

        foreach (GameManifestFile file in files)
        {
            string relative = file.Path.Replace('/', Path.DirectorySeparatorChar);
            string source = Path.GetFullPath(Path.Combine(gamePath, relative));
            string destination = Path.GetFullPath(Path.Combine(clientPath, relative));

            if (!source.StartsWith(root, StringComparison.OrdinalIgnoreCase) ||
                !destination.StartsWith(targetRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Недопустимый путь Original: {file.Path}");

            if (!File.Exists(source))
                throw new FileNotFoundException($"Файл Original не найден: {file.Path}", source);

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(source, destination, true);
        }

        await SaveClientManifestAsync(clientPath, Launcher.ClientId, files);
        Logger.Info($"Original client created from installed files: files={files.Length}");
    }

    private async Task DownloadFileAsync(
        string gamePath, int index, GameManifestFile file, int total,
        ConcurrentDictionary<int, long> downloaded,
        ConcurrentDictionary<int, long> sessionDownloaded,
        ConcurrentDictionary<int, long> sizes,
        ConcurrentDictionary<int, long> sessionSizes,
        ConcurrentDictionary<int, byte> completed,
        ConcurrentDictionary<int, ActiveDownloadProgress> active,
        SemaphoreSlim semaphore, Stopwatch timer,
        IProgress<ClientUpdateProgress>? progress, CancellationToken token)
    {
        await semaphore.WaitAsync(token);
        try
        {
            token.ThrowIfCancellationRequested();
            string localPath = Path.Combine(
                gamePath, file.Path.Replace('/', Path.DirectorySeparatorChar));

            Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
            Logger.Info($"Download started: {file.Path}");

            var fileProgress = new Progress<DownloadProgress>(p =>
            {
                long current = Math.Max(0, p.SizeCurrent);
                long size = p.SizeTotal > 0 ? p.SizeTotal : file.Size;

                if (size > 0)
                    sizes[index] = sessionSizes[index] = size;

                downloaded[index] = sessionDownloaded[index] = current;
                active[index] = new ActiveDownloadProgress
                {
                    FileIndex = index,
                    FileName = file.Path,
                    Percent = size > 0 ? CalculateProgress(current, size) : Math.Clamp(p.Percent, 0, 100),
                    DownloadedBytes = current,
                    TotalBytes = size
                };

                Report(progress, completed, total, downloaded, sessionDownloaded,
                    sizes, sessionSizes, active, timer, true);
            });

            await _downloadService.DownloadFileAsync(file.Url, localPath, fileProgress, token);

            string hash = await HashHelper.CalculateSha256Async(localPath, token);
            if (!string.Equals(hash, file.ChecksumSha256, StringComparison.OrdinalIgnoreCase))
            {
                Logger.Warning($"SHA-256 mismatch: {file.Path}");
                File.Delete(localPath);
                throw new InvalidDataException(
                    $"Контрольная сумма файла '{file.Path}' не совпадает с manifest.");
            }

            long length = new FileInfo(localPath).Length;
            downloaded[index] = sessionDownloaded[index] = length;
            if (file.Size <= 0)
                sizes[index] = length;
            if (!sessionSizes.ContainsKey(index))
                sessionSizes[index] = length;

            completed[index] = 0;
            active.TryRemove(index, out _);
            Logger.Info($"File verified: {file.Path}");

            Report(progress, completed, total, downloaded, sessionDownloaded,
                sizes, sessionSizes, active, timer, active.Count > 0);
        }
        catch (Exception ex)
        {
            active.TryRemove(index, out _);
            Logger.Error($"Failed to download file: {file.Path}", ex);
            throw;
        }
        finally
        {
            semaphore.Release();
        }
    }

    private static async Task<bool> NeedsDownloadAsync(
        string path, GameManifestFile file, CancellationToken token)
    {
        if (!File.Exists(path))
            return true;

        string hash = await HashHelper.CalculateSha256Async(path, token);
        return string.IsNullOrEmpty(hash) ||
               !string.Equals(hash, file.ChecksumSha256, StringComparison.OrdinalIgnoreCase);
    }

    private static int CalculateProgress(long current, long total) =>
        total <= 0 ? 0 : Math.Clamp((int)Math.Min(100, current * 100.0 / total), 0, 100);

    private static void Report(
        IProgress<ClientUpdateProgress>? progress,
        ConcurrentDictionary<int, byte> completed, int total,
        ConcurrentDictionary<int, long> downloaded,
        ConcurrentDictionary<int, long> sessionDownloaded,
        ConcurrentDictionary<int, long> sizes,
        ConcurrentDictionary<int, long> sessionSizes,
        ConcurrentDictionary<int, ActiveDownloadProgress> active,
        Stopwatch timer, bool isDownloading)
    {
        if (progress == null)
            return;

        long bytes = downloaded.Values.Sum();
        long totalBytes = sizes.Values.Sum();
        long sessionBytes = sessionDownloaded.Values.Sum();
        long sessionTotal = sessionSizes.Values.Sum();
        double speed = timer.Elapsed.TotalSeconds > 0
            ? sessionBytes / timer.Elapsed.TotalSeconds : 0;

        TimeSpan? eta = sessionTotal <= 0 || speed <= 0
            ? null
            : TimeSpan.FromSeconds(Math.Max(0, sessionTotal - sessionBytes) / speed);

        int count = Math.Min(total, completed.Count);
        int percent = CalculateProgress(bytes, totalBytes);

        if (!isDownloading && count >= total)
        {
            percent = 100;
            eta = TimeSpan.Zero;
        }

        progress.Report(new ClientUpdateProgress
        {
            CompletedFiles = count,
            TotalFiles = total,
            DownloadedBytes = bytes,
            TotalBytes = totalBytes,
            Percent = percent,
            BytesPerSecond = speed,
            EstimatedTimeRemaining = eta,
            IsDownloading = isDownloading,
            ActiveDownloads = active.Values.OrderBy(x => x.FileIndex).ToList()
        });
    }
}
