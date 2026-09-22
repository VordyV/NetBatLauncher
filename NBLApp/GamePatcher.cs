using System;
using System.IO;
using System.Reflection;

namespace NBL.Services;

public static class GameFilePatcher
{
    private static readonly string[] GameFiles =
    {
        "bf2142.exe",
        "RendDX9.dll",
        "RendDX9_ori.dll"
    };

    public static void Install(string gamePath)
    {
        Assembly assembly =
            Assembly.GetEntryAssembly()
            ?? throw new InvalidOperationException(
                "Не удалось получить сборку лаунчера.");

        string[] resources =
            assembly.GetManifestResourceNames();

        Console.WriteLine("=== Embedded Resources ===");

        foreach (string resource in resources)
        {
            Console.WriteLine(resource);
        }

        Console.WriteLine("=========================");

        foreach (string fileName in GameFiles)
        {
            string? resourceName = null;

            foreach (string resource in resources)
            {
                if (resource.EndsWith(
                    "." + fileName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    resourceName = resource;
                    break;
                }
            }

            if (resourceName == null)
            {
                throw new FileNotFoundException(
                    $"Не найден встроенный файл {fileName}");
            }

            using Stream? resourceStream =
                assembly.GetManifestResourceStream(
                    resourceName);

            if (resourceStream == null)
            {
                throw new FileNotFoundException(
                    $"Не удалось открыть встроенный файл {fileName}");
            }

            string destination =
                Path.Combine(gamePath, fileName);

            using FileStream file =
                new(
                    destination,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None);

            resourceStream.CopyTo(file);
        }
    }
}