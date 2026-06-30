using System;
using System.IO;
using System.Text.Json;
using Godot;
using Rpg.Domain.StrategicManagement;
using Rpg.Infrastructure.Logging;

namespace Rpg.Application.StrategicManagement;

public sealed class StrategicManagementSaveService
{
    public const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions SaveJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true
    };

    public void Save(StrategicManagementState state, string path)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        string normalizedPath = NormalizePath(path);
        string directory = Path.GetDirectoryName(normalizedPath) ?? "";
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = JsonSerializer.Serialize(
            new StrategicManagementSaveDocument
            {
                Version = CurrentVersion,
                State = state
            },
            SaveJsonOptions);
        File.WriteAllText(normalizedPath, json);
        GameLog.Info(nameof(StrategicManagementSaveService), $"StrategicManagementStateSaved path={normalizedPath}");
    }

    public StrategicManagementState Load(string path)
    {
        string normalizedPath = NormalizePath(path);
        if (!File.Exists(normalizedPath))
        {
            throw new FileNotFoundException("Strategic management save file does not exist.", normalizedPath);
        }

        StrategicManagementSaveDocument document = JsonSerializer.Deserialize<StrategicManagementSaveDocument>(
            File.ReadAllText(normalizedPath),
            SaveJsonOptions) ?? throw new InvalidOperationException($"Invalid strategic management save path={normalizedPath}");
        if (document.Version > CurrentVersion)
        {
            throw new InvalidOperationException($"Unsupported strategic management save version={document.Version} current={CurrentVersion} path={normalizedPath}");
        }

        StrategicManagementState state = document.State ?? new StrategicManagementState();
        GameLog.Info(nameof(StrategicManagementSaveService), $"StrategicManagementStateLoaded path={normalizedPath} version={document.Version}");
        return state;
    }

    public bool Exists(string path) => File.Exists(NormalizePath(path));

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Strategic management save path is empty.", nameof(path));
        }

        return path.StartsWith("user://", StringComparison.Ordinal)
            ? ProjectSettings.GlobalizePath(path)
            : path;
    }
}

public sealed class StrategicManagementSaveDocument
{
    public int Version { get; set; }
    public StrategicManagementState State { get; set; } = new();
}
