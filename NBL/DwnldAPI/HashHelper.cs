using System.Security.Cryptography;
namespace NBL.Services;
public static class HashHelper
{
    public static async Task<string> CalculateMd5Async(
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
            using MD5 md5 = MD5.Create();
            byte[] hash =
                await md5.ComputeHashAsync(
                    stream,
                    cancellationToken);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
        catch
        {
            return string.Empty;
        }
    }
}