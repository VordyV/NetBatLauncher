using NBL.Services;

namespace NBL;

public static class Safe
{
    public static async Task RunAsync(Func<Task> action, string name)
    {
        try { await action(); }
        catch (Exception ex) { Logger.Error($"Unhandled error in {name}", ex); }
    }
}
