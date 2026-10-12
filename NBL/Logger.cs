using System;
using System.IO;
using System.Text;
using System.Management;
using System.Runtime.InteropServices;

namespace NBL.Services;

public static class Logger
{
    private static readonly object LockObject = new();

    private const int LogRetentionDays = 30;

    public static string LogDirectory { get; } =
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "NetBat2142",
            "Logs");

    private static string LatestLogFile =>
        Path.Combine(LogDirectory, "latest.log");

    private static string CurrentLogFile { get; set; } = LatestLogFile;

    public static void Initialize()
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);

            PrepareLogs();

            Info("========================================");
            Info("NetBat2142 Launcher started");

            Info($"Version: {GetVersion()}");
            Info(" ");
            Info($"Windows: {RuntimeInformation.OSDescription}");
            Info($"OS Version: {Environment.OSVersion.Version}");
            Info($"OS Architecture: {RuntimeInformation.OSArchitecture}");
            Info($"Process Architecture: {RuntimeInformation.ProcessArchitecture}");
            Info($"64-bit OS: {Environment.Is64BitOperatingSystem}");
            Info($"64-bit Process: {Environment.Is64BitProcess}");

            Info($".NET Runtime: {RuntimeInformation.FrameworkDescription}");
            Info($"CLR Version: {Environment.Version}");
            Info(" ");
            LogCpu();
            LogRam();
            LogGpu();
            Info(" ");
            Info($"Current Directory: {Environment.CurrentDirectory}");
            Info($"Application Path: {Environment.ProcessPath}");

            Info($"Log Directory: {LogDirectory}");
            Info($"Log File: {CurrentLogFile}");

            Info("========================================");
        }
        catch (Exception ex)
        {
            try
            {
                Write(
                    "ERROR",
                    $"Logger initialization failed.{Environment.NewLine}{ex}");
            }
            catch
            {
            }
        }
    }

    private static void LogCpu()
    {
        try
        {
            using var cpuSearcher = new ManagementObjectSearcher(
                "SELECT Name FROM Win32_Processor");

            foreach (var item in cpuSearcher.Get())
            {
                var name = item["Name"]?.ToString();

                if (!string.IsNullOrWhiteSpace(name))
                {
                    Info($"CPU: {name}");
                }
                else
                {
                    Info("CPU: Unknown");
                }

                break;
            }
        }
        catch (Exception ex)
        {
            Warning($"Failed to detect CPU: {ex.Message}");
            Info("CPU: Unknown");
        }

        Info($"Logical Processors: {Environment.ProcessorCount}");
    }

    private static void LogRam()
    {
        try
        {
            using var memorySearcher = new ManagementObjectSearcher(
                "SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");

            foreach (var item in memorySearcher.Get())
            {
                if (item["TotalPhysicalMemory"] is ulong bytes)
                {
                    var gigabytes =
                        bytes / 1024d / 1024d / 1024d;

                    Info($"RAM: {gigabytes:F1} GB");
                }
                else
                {
                    Info("RAM: Unknown");
                }

                break;
            }
        }
        catch (Exception ex)
        {
            Warning($"Failed to detect RAM: {ex.Message}");
            Info("RAM: Unknown");
        }
    }

    private static void LogGpu()
    {
        try
        {
            using var gpuSearcher = new ManagementObjectSearcher(
                "SELECT Name, AdapterRAM FROM Win32_VideoController");

            var gpuFound = false;

            foreach (var item in gpuSearcher.Get())
            {
                gpuFound = true;

                var name =
                    item["Name"]?.ToString() ?? "Unknown";

                if (item["AdapterRAM"] is uint adapterRam &&
                    adapterRam > 0)
                {
                    var vramGb =
                        adapterRam /
                        1024d /
                        1024d /
                        1024d;

                    Info(
                        $"GPU: {name} " +
                        $"({vramGb:F1} GB VRAM)");
                }
                else
                {
                    Info($"GPU: {name}");
                }
            }

            if (!gpuFound)
            {
                Info("GPU: Unknown");
            }
        }
        catch (Exception ex)
        {
            Warning($"Failed to detect GPU: {ex.Message}");
            Info("GPU: Unknown");
        }
    }

    private static void PrepareLogs()
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);

            if (File.Exists(LatestLogFile))
            {
                var archivedLogFile = Path.Combine(
                    LogDirectory,
                    $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log");

                // Если запусков несколько за одну секунду
                if (File.Exists(archivedLogFile))
                {
                    archivedLogFile = Path.Combine(
                        LogDirectory,
                        $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}.log");
                }

                File.Move(
                    LatestLogFile,
                    archivedLogFile);
            }

            DeleteOldLogs();

            CurrentLogFile = LatestLogFile;
        }
        catch
        {
            CurrentLogFile = LatestLogFile;
        }
    }

    private static void DeleteOldLogs()
    {
        try
        {
            var cutoffDate =
                DateTime.Now.AddDays(-LogRetentionDays);

            foreach (var file in Directory.EnumerateFiles(
                LogDirectory,
                "*.log"))
            {
                if (string.Equals(
                    Path.GetFileName(file),
                    "latest.log",
                    StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    var lastWriteTime =
                        File.GetLastWriteTime(file);

                    if (lastWriteTime < cutoffDate)
                    {
                        File.Delete(file);
                    }
                }
                catch
                {
                    // Ignore individual file deletion errors.
                }
            }
        }
        catch
        {
            // Ignore cleanup errors.
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

    public static void Error(
        string message,
        Exception exception)
    {
        Write(
            "ERROR",
            $"{message}{Environment.NewLine}{exception}");
    }

    public static void Debug(string message)
    {
        Write("DEBUG", message);
    }

    private static void Write(
        string level,
        string message)
    {
        try
        {
            lock (LockObject)
            {
                Directory.CreateDirectory(LogDirectory);

                var line =
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] " +
                    $"[{level}] {message}";

                File.AppendAllText(
                    CurrentLogFile,
                    line + Environment.NewLine,
                    Encoding.UTF8);
            }
        }
        catch
        {
            // Logger must never crash the launcher.
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