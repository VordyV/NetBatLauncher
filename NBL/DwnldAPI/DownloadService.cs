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
    public DownloadService()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(30)
        };
    }
    public async Task<string> GetStringAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response =
            await _httpClient.GetAsync(
                url,
                cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            string error =
                await response.Content.ReadAsStringAsync(
                    cancellationToken);
            throw new HttpRequestException(
                $"HTTP {(int)response.StatusCode} " +
                $"{response.ReasonPhrase}\n" +
                $"URL: {url}\n" +
                $"Response: {error}");
        }
        return await response.Content.ReadAsStringAsync(
            cancellationToken);
    }
    public async Task DownloadFileAsync(
        string url,
        string filePath,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        string? directory =
            Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        string tempPath =
            filePath + ".download";
        try
        {
            using (HttpResponseMessage response =
                       await _httpClient.GetAsync(
                           url,
                           HttpCompletionOption.ResponseHeadersRead,
                           cancellationToken))
            {
                if (!response.IsSuccessStatusCode)
                {
                    string error =
                        await response.Content.ReadAsStringAsync(
                            cancellationToken);
                    throw new HttpRequestException(
                        $"HTTP {(int)response.StatusCode} " +
                        $"{response.ReasonPhrase}\n" +
                        $"URL: {url}\n" +
                        $"Response: {error}");
                }
                long totalBytes =
                    response.Content.Headers.ContentLength ?? -1;
                using (Stream source =
                           await response.Content.ReadAsStreamAsync(
                               cancellationToken))
                using (FileStream destination =
                           new FileStream(
                               tempPath,
                               FileMode.Create,
                               FileAccess.Write,
                               FileShare.None,
                               1024 * 1024,
                               useAsync: true))
                {
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
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            await MoveFileWithRetryAsync(
                tempPath,
                filePath,
                cancellationToken);
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch
                {
                }
            }
            throw;
        }
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
                return;
            }
            catch (IOException) when (
                attempt < maxAttempts)
            {
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
