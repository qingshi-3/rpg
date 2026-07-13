using System;
using System.IO;
using Godot;

namespace Rpg.Application.Config;

public static class ProjectConfigFileReader
{
    public static string ReadAllText(string path)
    {
        string normalized = path?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("Config path is empty.");
        }

        if (TryResolveProjectFilePath(normalized, out string filePath) && File.Exists(filePath))
        {
            return File.ReadAllText(filePath);
        }

        using Godot.FileAccess file = Godot.FileAccess.Open(normalized, Godot.FileAccess.ModeFlags.Read);
        if (file == null)
        {
            throw new FileNotFoundException($"Missing config file path={normalized}");
        }

        return file.GetAsText();
    }

    public static string ResolveRequiredFilePath(string path)
    {
        string normalized = path?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("Config path is empty.");
        }

        if (!TryResolveProjectFilePath(normalized, out string filePath) || !File.Exists(filePath))
        {
            throw new FileNotFoundException($"Missing project file path={normalized}");
        }

        return Path.GetFullPath(filePath);
    }

    private static bool TryResolveProjectFilePath(string path, out string filePath)
    {
        filePath = "";
        if (!path.StartsWith("res://", StringComparison.Ordinal))
        {
            filePath = path;
            return true;
        }

        DirectoryInfo directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            string candidateProject = Path.Combine(directory.FullName, "rpg.csproj");
            if (File.Exists(candidateProject))
            {
                filePath = Path.Combine(directory.FullName, path["res://".Length..].Replace('/', Path.DirectorySeparatorChar));
                return true;
            }

            directory = directory.Parent;
        }

        return false;
    }
}
