using System;
using System.IO;
namespace NBL.Services;
public class IntroVideoService
{
    private static readonly string[] MovieFiles =
    {
        "EA.bik",
        "Legal.bik",
        "Intro.bik",
        "Dice.bik"
    };
    private static readonly string[] DisabledMovieFiles =
    {
        "EA.bikz",
        "Legal.bikz",
        "Intro.bikz",
        "Dice.bikz"
    };
    public bool AreIntrosDisabled(
        string gamePath)
    {
        string moviesPath =
            GetMoviesPath(gamePath);
        if (!Directory.Exists(moviesPath))
            return false;
        foreach (string file in DisabledMovieFiles)
        {
            if (File.Exists(
                    Path.Combine(
                        moviesPath,
                        file)))
            {
                return true;
            }
        }
        return false;
    }
    public void SetIntrosDisabled(
        string gamePath,
        bool disabled)
    {
        string moviesPath =
            GetMoviesPath(gamePath);
        if (!Directory.Exists(moviesPath))
        {
            throw new DirectoryNotFoundException(
                $"BF2142 Movies folder not found: {moviesPath}");
        }
        if (disabled)
        {
            DisableMovies(moviesPath);
        }
        else
        {
            RestoreMovies(moviesPath);
        }
    }
    private static void DisableMovies(
        string moviesPath)
    {
        foreach (string file in MovieFiles)
        {
            string source =
                Path.Combine(
                    moviesPath,
                    file);
            string target =
                Path.Combine(
                    moviesPath,
                    $"{file}z");
            if (File.Exists(source)
                && !File.Exists(target))
            {
                File.Move(
                    source,
                    target);
            }
        }
    }
    private static void RestoreMovies(
        string moviesPath)
    {
        foreach (string file in DisabledMovieFiles)
        {
            string source =
                Path.Combine(
                    moviesPath,
                    file);
            string target =
                Path.Combine(
                    moviesPath,
                    file[..^1]);
            if (File.Exists(source)
                && !File.Exists(target))
            {
                File.Move(
                    source,
                    target);
            }
        }
    }
    private static string GetMoviesPath(
        string gamePath)
    {
        return Path.Combine(
            gamePath,
            "mods",
            "bf2142",
            "Movies");
    }
}