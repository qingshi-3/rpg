using System;
using System.IO;
using System.Text;
using Godot;

namespace Rpg.Infrastructure.Logging;

public static class GameLog
{
    private const string LogDirectory = "user://logs";

    private static readonly object Sync = new();
    private static bool _sessionStarted;

    public static string CurrentLogPath { get; private set; } = "";

    public static void StartSession(string source)
    {
        lock (Sync)
        {
            EnsureLogPath();

            if (_sessionStarted)
            {
                WriteLine($"{Timestamp()} [INFO] [{source}] Session already started.");
                return;
            }

            StartSessionLocked(source);
        }
    }

    public static void Info(string category, string message)
    {
        Write("INFO", category, message);
    }

    public static void Warn(string category, string message)
    {
        Write("WARN", category, message);
    }

    public static void Error(string category, string message)
    {
        Write("ERROR", category, message);
    }

    private static void Write(string level, string category, string message)
    {
        lock (Sync)
        {
            EnsureLogPath();
            if (!_sessionStarted)
            {
                StartSessionLocked("Auto");
            }

            WriteLine($"{Timestamp()} [{level}] [{category}] {Sanitize(message)}");
        }
    }

    private static void StartSessionLocked(string source)
    {
        _sessionStarted = true;
        WriteLine("");
        WriteLine("============================================================");
        WriteLine($"{Timestamp()} [SESSION] Start source={source}");
        WriteLine($"{Timestamp()} [SESSION] Engine={Engine.GetVersionInfo()} LogPath={CurrentLogPath}");
    }

    private static void EnsureLogPath()
    {
        if (!string.IsNullOrWhiteSpace(CurrentLogPath))
        {
            return;
        }

        string directory = ProjectSettings.GlobalizePath(LogDirectory);
        Directory.CreateDirectory(directory);
        CurrentLogPath = Path.Combine(directory, $"rpg-{DateTime.Now:yyyyMMdd}.log");
    }

    private static void WriteLine(string line)
    {
        try
        {
            File.AppendAllText(CurrentLogPath, line + System.Environment.NewLine, Encoding.UTF8);
        }
        catch (Exception exception)
        {
            GD.PushWarning($"GameLog failed to write log file: {exception.Message}");
        }
    }

    private static string Timestamp()
    {
        return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
    }

    private static string Sanitize(string message)
    {
        return (message ?? "")
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }
}
