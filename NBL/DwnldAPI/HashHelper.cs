using System.Security.Cryptography;

namespace NBL.Services;

public static class HashHelper
{
    public static async Task<string> CalculateSha256Async(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using FileStream stream =
                new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    81920,
                    useAsync: true);

            using SHA256 sha256 = SHA256.Create();

            byte[] hash =
                await sha256.ComputeHashAsync(
                    stream,
                    cancellationToken);

            return Convert.ToHexString(
                hash).ToLowerInvariant();
        }
        catch
        {
            return string.Empty;
        }
    }
}