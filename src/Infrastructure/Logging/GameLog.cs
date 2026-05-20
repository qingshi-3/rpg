using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Godot;
using Rpg.Infrastructure.Diagnostics;

namespace Rpg.Infrastructure.Logging;

public static class GameLog
{
    private const string LogDirectory = "user://logs";
    private const string LogDirectoryOverrideEnvironmentVariable = "RPG_GAMELOG_DIR";

    private static readonly object Sync = new();
    private static readonly HashSet<string> EnabledTraceCategories = new(StringComparer.Ordinal);
    private static bool _sessionStarted;
    private static BattlePerformanceCounters _performanceCounters;

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

    public static void Trace(string category, string message)
    {
        lock (Sync)
        {
            if (!EnabledTraceCategories.Contains(category ?? ""))
            {
                _performanceCounters?.RecordLogSuppressed();
                return;
            }
        }

        Write("TRACE", category, message);
    }

    public static void Warn(string category, string message)
    {
        Write("WARN", category, message);
    }

    public static void Error(string category, string message)
    {
        Write("ERROR", category, message);
    }

    public static void SetPerformanceCounters(BattlePerformanceCounters counters)
    {
        lock (Sync)
        {
            _performanceCounters = counters;
        }
    }

    public static void SetTraceCategoryEnabled(string category, bool enabled)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return;
        }

        lock (Sync)
        {
            if (enabled)
            {
                EnabledTraceCategories.Add(category);
            }
            else
            {
                EnabledTraceCategories.Remove(category);
            }
        }
    }

    public static void RecordBattleMovementTweenCreated()
    {
        _performanceCounters?.RecordMovementTweenCreated();
    }

    public static void RecordBattleMovementTweenInterrupted()
    {
        _performanceCounters?.RecordMovementTweenInterrupted();
    }

    private static void Write(string level, string category, string message)
    {
        long startedAt = Stopwatch.GetTimestamp();
        lock (Sync)
        {
            EnsureLogPath();
            if (!_sessionStarted)
            {
                StartSessionLocked("Auto");
            }

            WriteLine($"{Timestamp()} [{level}] [{category}] {Sanitize(message)}");
            _performanceCounters?.RecordLogWrite(Stopwatch.GetTimestamp() - startedAt);
        }
    }

    private static void StartSessionLocked(string source)
    {
        _sessionStarted = true;
        WriteLine("");
        WriteLine("============================================================");
        WriteLine($"{Timestamp()} [SESSION] Start source={source}");
        WriteLine($"{Timestamp()} [SESSION] Engine={GetEngineInfo()} LogPath={CurrentLogPath}");
    }

    private static void EnsureLogPath()
    {
        if (!string.IsNullOrWhiteSpace(CurrentLogPath))
        {
            return;
        }

        string directory = System.Environment.GetEnvironmentVariable(LogDirectoryOverrideEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(directory))
        {
            directory = ProjectSettings.GlobalizePath(LogDirectory);
        }

        Directory.CreateDirectory(directory);
        CurrentLogPath = Path.Combine(directory, $"rpg-{DateTime.Now:yyyyMMdd}.log");
    }

    private static object GetEngineInfo()
    {
        if (!string.IsNullOrWhiteSpace(System.Environment.GetEnvironmentVariable(LogDirectoryOverrideEnvironmentVariable)))
        {
            return "outside_godot";
        }

        return Engine.GetVersionInfo();
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
