using System.Diagnostics;
using System.Net.Http;

namespace NBL.Services;

public class DownloadProgress
{
    public long SizeCurrent { get; init; }

    public long SizeTotal { get; init; }

    public int Percent { get; init; }

    public double BytesPerSecond { get; init; }

    public TimeSpan? EstimatedTimeRemaining { get; init; }
}

public class DownloadService
{
    private readonly HttpClient _httpClient;

    private const int MaxDownloadAttempts = 3;

    public DownloadService()
    {
        _httpClient =
            new HttpClient
            {
                Timeout =
                    TimeSpan.FromMinutes(30)
            };

        Logger.Debug(
            "DownloadService initialized. " +
            "HTTP timeout: 30 minutes");
    }

    public async Task<string> GetStringAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        Logger.Info(
            $"HTTP GET started: '{url}'");

        Stopwatch stopwatch =
            Stopwatch.StartNew();

        try
        {
            using HttpResponseMessage response =
                await _httpClient.GetAsync(
                    url,
                    cancellationToken);

            Logger.Debug(
                $"HTTP response: " +
                $"status={(int)response.StatusCode} " +
                $"'{response.ReasonPhrase}', " +
                $"elapsed={stopwatch.Elapsed}");

            if (!response.IsSuccessStatusCode)
            {
                string error =
                    await response.Content.ReadAsStringAsync(
                        cancellationToken);

                Logger.Error(
                    $"HTTP GET failed: " +
                    $"status={(int)response.StatusCode}, " +
                    $"url='{url}', " +
                    $"response='{error}'");

                throw new HttpRequestException(
                    $"HTTP {(int)response.StatusCode} " +
                    $"{response.ReasonPhrase}\n" +
                    $"URL: {url}\n" +
                    $"Response: {error}");
            }

            string result =
                await response.Content.ReadAsStringAsync(
                    cancellationToken);

            Logger.Info(
                $"HTTP GET completed: " +
                $"url='{url}', " +
                $"bytes={result.Length}, " +
                $"elapsed={stopwatch.Elapsed}");

            return result;
        }
        catch (OperationCanceledException)
        {
            Logger.Warning(
                $"HTTP GET cancelled: '{url}'");

            throw;
        }
        catch (Exception ex)
        {
            Logger.Error(
                $"HTTP GET exception: '{url}'",
                ex);

            throw;
        }
    }

    public async Task DownloadFileAsync(
        string url,
        string filePath,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Logger.Info(
            $"File download started: " +
            $"url='{url}', " +
            $"destination='{filePath}'");

        string? directory =
            Path.GetDirectoryName(filePath);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string tempPath =
            filePath + ".download";

        Logger.Debug(
            $"Temporary download path: '{tempPath}'");

        Stopwatch totalStopwatch =
            Stopwatch.StartNew();

        Exception? lastException = null;

        for (int attempt = 1;
             attempt <= MaxDownloadAttempts;
             attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (attempt > 1)
                {
                    Logger.Info(
                        $"Retrying download " +
                        $"({attempt}/{MaxDownloadAttempts}): " +
                        $"'{filePath}'");
                }

                await DownloadFileAttemptAsync(
                    url,
                    filePath,
                    tempPath,
                    progress,
                    cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                await MoveFileWithRetryAsync(
                    tempPath,
                    filePath,
                    cancellationToken);

                Logger.Info(
                    $"File download completed: " +
                    $"destination='{filePath}', " +
                    $"attempt={attempt}, " +
                    $"elapsed={totalStopwatch.Elapsed}");

                return;
            }
            catch (OperationCanceledException)
            {
                Logger.Warning(
                    $"File download cancelled: " +
                    $"url='{url}'");

                throw;
            }
            catch (Exception ex)
            {
                lastException = ex;

                if (attempt >= MaxDownloadAttempts)
                {
                    Logger.Error(
                        $"File download failed after " +
                        $"{MaxDownloadAttempts} attempts: " +
                        $"url='{url}', " +
                        $"destination='{filePath}'",
                        ex);

                    break;
                }

                Logger.Warning(
                    $"Download attempt {attempt}/" +
                    $"{MaxDownloadAttempts} failed: " +
                    $"'{filePath}'. " +
                    $"{ex.GetType().Name}: {ex.Message}");

                TimeSpan delay =
                    TimeSpan.FromSeconds(
                        Math.Pow(2, attempt - 1));

                Logger.Info(
                    $"Retrying in " +
                    $"{delay.TotalSeconds:0} seconds...");

                await Task.Delay(
                    delay,
                    cancellationToken);
            }
        }

        if (File.Exists(tempPath))
        {
            try
            {
                File.Delete(tempPath);

                Logger.Debug(
                    $"Temporary file removed after failed " +
                    $"download: '{tempPath}'");
            }
            catch (Exception ex)
            {
                Logger.Warning(
                    $"Could not remove temporary file: " +
                    $"'{tempPath}'. {ex.Message}");
            }
        }

        throw lastException ??
              new IOException(
                  $"Download failed: {url}");
    }

    private async Task DownloadFileAttemptAsync(
        string url,
        string filePath,
        string tempPath,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await _httpClient.GetAsync(
                url,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

        Logger.Debug(
            $"Download HTTP response: " +
            $"status={(int)response.StatusCode} " +
            $"'{response.ReasonPhrase}', " +
            $"contentLength=" +
            $"{response.Content.Headers.ContentLength?.ToString() ?? "unknown"}");

        if (!response.IsSuccessStatusCode)
        {
            string error =
                await response.Content.ReadAsStringAsync(
                    cancellationToken);

            Logger.Warning(
                $"File download HTTP error: " +
                $"status={(int)response.StatusCode}, " +
                $"url='{url}', " +
                $"response='{error}'");

            throw new HttpRequestException(
                $"HTTP {(int)response.StatusCode} " +
                $"{response.ReasonPhrase}\n" +
                $"URL: {url}\n" +
                $"Response: {error}");
        }

        long totalBytes =
            response.Content.Headers.ContentLength ?? -1;

        using Stream source =
            await response.Content.ReadAsStreamAsync(
                cancellationToken);

        using FileStream destination =
            new FileStream(
                tempPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                1024 * 1024,
                useAsync: true);

        byte[] buffer =
            new byte[1024 * 1024];

        long downloaded = 0;

        Stopwatch stopwatch =
            Stopwatch.StartNew();

        int bytesRead;

        while ((bytesRead =
                   await source.ReadAsync(
                       buffer.AsMemory(
                           0,
                           buffer.Length),
                       cancellationToken)) > 0)
        {
            await destination.WriteAsync(
                buffer.AsMemory(
                    0,
                    bytesRead),
                cancellationToken);

            downloaded += bytesRead;

            double seconds =
                stopwatch.Elapsed.TotalSeconds;

            double bytesPerSecond =
                seconds > 0
                    ? downloaded / seconds
                    : 0;

            int percent = 0;

            if (totalBytes > 0)
            {
                percent =
                    (int)(
                        downloaded *
                        100L /
                        totalBytes);
            }

            TimeSpan? eta = null;

            if (totalBytes > 0 &&
                bytesPerSecond > 0 &&
                downloaded < totalBytes)
            {
                long remaining =
                    totalBytes - downloaded;

                eta =
                    TimeSpan.FromSeconds(
                        remaining /
                        bytesPerSecond);
            }

            progress?.Report(
                new DownloadProgress
                {
                    SizeCurrent =
                        downloaded,

                    SizeTotal =
                        totalBytes,

                    Percent =
                        Math.Clamp(
                            percent,
                            0,
                            100),

                    BytesPerSecond =
                        bytesPerSecond,

                    EstimatedTimeRemaining =
                        eta
                });
        }

        await destination.FlushAsync(
            cancellationToken);

        Logger.Debug(
            $"Download stream completed: " +
            $"bytes={downloaded}, " +
            $"expected={totalBytes}, " +
            $"elapsed={stopwatch.Elapsed}");
    }

    private static async Task MoveFileWithRetryAsync(
        string source,
        string destination,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 10;

        for (int attempt = 1;
             attempt <= maxAttempts;
             attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                File.Move(
                    source,
                    destination,
                    overwrite: true);

                if (attempt > 1)
                {
                    Logger.Info(
                        $"File move succeeded on attempt " +
                        $"{attempt}: '{destination}'");
                }

                return;
            }
            catch (IOException ex) when (
                attempt < maxAttempts)
            {
                Logger.Warning(
                    $"File move attempt {attempt}/" +
                    $"{maxAttempts} failed: " +
                    $"'{source}' -> '{destination}'. " +
                    $"{ex.Message}");

                await Task.Delay(
                    TimeSpan.FromMilliseconds(500),
                    cancellationToken);
            }
        }

        File.Move(
            source,
            destination,
            overwrite: true);
    }
}