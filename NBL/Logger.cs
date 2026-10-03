using System;
using System.IO;
using System.Text;

namespace NBL.Services;

public static class Logger
{
    private static readonly object LockObject = new();

    public static string LogDirectory { get; } =
     Path.Combine(
         Environment.GetFolderPath(
             Environment.SpecialFolder.LocalApplicationData),
         "NetBat2142",
         "Logs");

    private static readonly string LogFile =
        Path.Combine(
            LogDirectory,
            $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log");

    public static void Initialize()
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);

            Info("========================================");
            Info("NetBat2142 Launcher started");
            Info($"Version: {GetVersion()}");
            Info($"OS: {Environment.OSVersion}");
            Info($"Architecture: {System.Runtime.InteropServices.RuntimeInformation.OSArchitecture}");
            Info($"Process architecture: {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}");
            Info($"Log file: {LogFile}");
            Info("========================================");
        }
        catch
        {
        }
    }

    public static void Info(string message)
    {
        Write("INFO", message);
    }

    public static void Warning(string message)
    {
        Write("WARN", message);
    }

    public static void Error(string message)
    {
        Write("ERROR", message);
    }

    public static void Error(string message, Exception exception)
    {
        Write(
            "ERROR",
            $"{message}{Environment.NewLine}{exception}");
    }

    public static void Debug(string message)
    {
        Write("DEBUG", message);
    }

    private static void Write(string level, string message)
    {
        try
        {
            lock (LockObject)
            {
                Directory.CreateDirectory(LogDirectory);

                var line =
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";

                File.AppendAllText(
                    LogFile,
                    line + Environment.NewLine,
                    Encoding.UTF8);
            }
        }
        catch
        {
        }
    }

    private static string GetVersion()
    {
        try
        {
            return System.Reflection.Assembly
                .GetExecutingAssembly()
                .GetName()
                .Version?
                .ToString() ?? "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }
}