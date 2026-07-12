using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Godot;
using Rpg.Definitions.StrategicManagement;
using Rpg.Domain.StrategicManagement;
using Rpg.Infrastructure.Logging;

namespace Rpg.Application.StrategicManagement;

public sealed class StrategicManagementSaveService
{
    public const int CurrentVersion = 2;

    private static readonly JsonSerializerOptions SaveJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true
    };

    private readonly StrategicManagementDefinitionSet _definitions;
    private readonly IStrategicManagementSaveFileStore _fileStore;

    public StrategicManagementSaveService(
        StrategicManagementDefinitionSet definitions = null,
        IStrategicManagementSaveFileStore fileStore = null)
    {
        _definitions = definitions;
        _fileStore = fileStore ?? new SystemStrategicManagementSaveFileStore();
    }

    public void Save(StrategicManagementState state, string path)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        string normalizedPath = NormalizePath(path);
        string stagingPath = GetStagingPath(normalizedPath);
        string previousPath = GetPreviousPath(normalizedPath);
        string json = SerializeDocument(state);

        // A flushed, parseable same-directory staging document is the only promotion source.
        _fileStore.WriteStaging(stagingPath, json);
        _ = DeserializeAndMigrate(_fileStore.ReadAllText(stagingPath), stagingPath);
        try
        {
            _fileStore.Promote(stagingPath, normalizedPath, previousPath);
        }
        catch
        {
            _fileStore.DeleteIfExists(stagingPath);
            throw;
        }
        GameLog.Info(nameof(StrategicManagementSaveService), $"StrategicManagementStateSaved path={normalizedPath} version={CurrentVersion}");
    }

    public StrategicManagementState Load(string path)
    {
        string normalizedPath = NormalizePath(path);
        string stagingPath = GetStagingPath(normalizedPath);
        string previousPath = GetPreviousPath(normalizedPath);
        if (!_fileStore.Exists(normalizedPath) && !_fileStore.Exists(stagingPath) && !_fileStore.Exists(previousPath))
        {
            throw new FileNotFoundException("Strategic management save file does not exist.", normalizedPath);
        }

        Exception liveFailure = null;
        if (_fileStore.Exists(normalizedPath))
        {
            try
            {
                StrategicManagementState live = DeserializeAndMigrate(_fileStore.ReadAllText(normalizedPath), normalizedPath);
                GameLog.Info(nameof(StrategicManagementSaveService), $"StrategicManagementStateLoaded path={normalizedPath} source=live");
                return live;
            }
            catch (UnsupportedStrategicSaveVersionException)
            {
                throw;
            }
            catch (Exception exception) when (IsRecoverableDocumentFailure(exception))
            {
                liveFailure = exception;
            }
        }

        foreach ((string candidatePath, string source) in new[] { (stagingPath, "staging"), (previousPath, "previous") })
        {
            if (!_fileStore.Exists(candidatePath))
            {
                continue;
            }

            try
            {
                StrategicManagementState recovered = DeserializeAndMigrate(_fileStore.ReadAllText(candidatePath), candidatePath);
                GameLog.Warn(
                    nameof(StrategicManagementSaveService),
                    $"StrategicManagementStateRecovered path={normalizedPath} source={source} liveFailure={liveFailure?.GetType().Name ?? "missing"}");
                return recovered;
            }
            catch (UnsupportedStrategicSaveVersionException)
            {
                throw;
            }
            catch (Exception exception) when (IsRecoverableDocumentFailure(exception))
            {
                liveFailure ??= exception;
            }
        }

        throw new InvalidOperationException($"No complete strategic management save document can be recovered path={normalizedPath}", liveFailure);
    }

    public StrategicManagementState CloneCandidate(StrategicManagementState state)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        return DeserializeAndMigrate(SerializeDocument(state), "candidate://strategic-management");
    }

    public bool Exists(string path) => _fileStore.Exists(NormalizePath(path));

    public static string GetStagingPath(string path) => NormalizePath(path) + ".staging";

    public static string GetPreviousPath(string path) => NormalizePath(path) + ".previous";

    private string SerializeDocument(StrategicManagementState state)
    {
        return JsonSerializer.Serialize(
            new StrategicManagementSaveDocument
            {
                Version = CurrentVersion,
                State = state
            },
            SaveJsonOptions);
    }

    private StrategicManagementState DeserializeAndMigrate(string json, string path)
    {
        ValidateDocumentShape(json, path, out int declaredVersion);
        StrategicManagementSaveDocument document = JsonSerializer.Deserialize<StrategicManagementSaveDocument>(json, SaveJsonOptions)
                                                   ?? throw new InvalidDataException($"Invalid strategic management save path={path}");
        if (document.Version != declaredVersion)
        {
            throw new InvalidDataException($"Strategic management save version is inconsistent path={path}");
        }
        if (document.Version > CurrentVersion)
        {
            throw new UnsupportedStrategicSaveVersionException(document.Version, path);
        }

        if (document.Version < 1)
        {
            throw new InvalidDataException($"Unsupported old strategic management save version={document.Version} path={path}");
        }

        StrategicManagementState state = document.State
                                         ?? throw new InvalidDataException($"Strategic management save state is null path={path}");
        int version = document.Version;
        if (version == 1)
        {
            MigrateVersionOneToTwo(state, path);
            version = 2;
        }

        if (version != CurrentVersion)
        {
            throw new InvalidDataException($"Strategic management save migration incomplete version={version} path={path}");
        }

        NormalizeCollections(state);
        return state;
    }

    private void MigrateVersionOneToTwo(StrategicManagementState state, string path)
    {
        NormalizeCollections(state);
        foreach (StrategicExpeditionState expedition in state.Expeditions.Values)
        {
            if (expedition == null || expedition.Status != StrategicExpeditionStatus.Moving)
            {
                continue;
            }

            if ((expedition.Participants == null || expedition.Participants.Count == 0) &&
                !string.IsNullOrWhiteSpace(expedition.HeroId) &&
                !string.IsNullOrWhiteSpace(expedition.CorpsInstanceId))
            {
                expedition.Participants = new()
                {
                    new StrategicExpeditionParticipantState
                    {
                        HeroId = expedition.HeroId,
                        CorpsInstanceId = expedition.CorpsInstanceId
                    }
                };
            }

            foreach (StrategicExpeditionParticipantState participant in expedition.Participants ?? new())
            {
                if (!string.IsNullOrWhiteSpace(participant?.RollbackStationLocationId))
                {
                    continue;
                }

                if (!CanProveVersionOneRollbackStation(state, expedition, participant))
                {
                    throw new InvalidDataException(
                        $"Cannot migrate expedition rollback station path={path} expedition={expedition.ExpeditionId} hero={participant?.HeroId ?? ""} corps={participant?.CorpsInstanceId ?? ""}");
                }

                participant.RollbackStationLocationId = expedition.SourceLocationId;
            }
        }
    }

    private bool CanProveVersionOneRollbackStation(
        StrategicManagementState state,
        StrategicExpeditionState expedition,
        StrategicExpeditionParticipantState participant)
    {
        string sourceId = expedition?.SourceLocationId ?? "";
        return participant != null &&
               !string.IsNullOrWhiteSpace(sourceId) &&
               _definitions?.Locations.TryGetValue(sourceId, out StrategicLocationDefinition sourceDefinition) == true &&
               sourceDefinition.Kind == StrategicLocationKind.City &&
               state.Cities.ContainsKey(sourceId) &&
               state.Locations.TryGetValue(sourceId, out StrategicLocationState sourceState) &&
               string.Equals(sourceState.OwnerFactionId, expedition.FactionId, StringComparison.Ordinal) &&
               state.Heroes.TryGetValue(participant.HeroId ?? "", out StrategicHeroState hero) &&
               state.CorpsInstances.TryGetValue(participant.CorpsInstanceId ?? "", out StrategicCorpsInstanceState corps) &&
               string.Equals(hero.CurrentExpeditionId, expedition.ExpeditionId, StringComparison.Ordinal) &&
               string.Equals(corps.CurrentExpeditionId, expedition.ExpeditionId, StringComparison.Ordinal) &&
               string.IsNullOrWhiteSpace(corps.HomeCityId);
    }

    private static void NormalizeCollections(StrategicManagementState state)
    {
        state.Expeditions ??= new(StringComparer.Ordinal);
        state.BattleFeedbackRecords ??= new(StringComparer.Ordinal);
        state.BattleFeedbackRecordIdsByExpedition ??= new(StringComparer.Ordinal);
        state.BattleSettlementRecordsByExpedition ??= new(StringComparer.Ordinal);
        foreach (StrategicExpeditionState expedition in state.Expeditions.Values)
        {
            if (expedition == null)
            {
                throw new InvalidDataException("Strategic management save contains a null expedition.");
            }

            expedition.Participants ??= new();
            if (expedition.Participants.Exists(participant => participant == null))
            {
                throw new InvalidDataException($"Strategic management save contains a null expedition participant expedition={expedition.ExpeditionId}");
            }
        }

        if (state.Locations.Values.Any(value => value == null) ||
            state.Cities.Values.Any(value => value == null) ||
            state.CorpsInstances.Values.Any(value => value == null) ||
            state.Heroes.Values.Any(value => value == null))
        {
            throw new InvalidDataException("Strategic management save contains a null core state record.");
        }
    }

    private static bool IsRecoverableDocumentFailure(Exception exception)
    {
        return exception is JsonException or InvalidDataException or IOException or UnauthorizedAccessException;
    }

    private static void ValidateDocumentShape(string json, string path, out int version)
    {
        using JsonDocument parsed = JsonDocument.Parse(json);
        JsonElement root = parsed.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !TryGetProperty(root, "Version", out JsonElement versionElement) ||
            versionElement.ValueKind != JsonValueKind.Number ||
            !versionElement.TryGetInt32(out version))
        {
            throw new InvalidDataException($"Strategic management save version is missing or malformed path={path}");
        }

        if (version > CurrentVersion)
        {
            throw new UnsupportedStrategicSaveVersionException(version, path);
        }

        if (!TryGetProperty(root, "State", out JsonElement stateElement) || stateElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"Strategic management save state is missing or null path={path}");
        }

        foreach (string requiredCollection in new[] { "Locations", "Cities", "CorpsInstances", "Heroes", "Expeditions" })
        {
            if (!TryGetProperty(stateElement, requiredCollection, out JsonElement collection) ||
                collection.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException($"Strategic management save collection={requiredCollection} is missing or malformed path={path}");
            }
        }
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Strategic management save path is empty.", nameof(path));
        }

        return path.StartsWith("user://", StringComparison.Ordinal)
            ? ProjectSettings.GlobalizePath(path)
            : Path.GetFullPath(path);
    }
}

public interface IStrategicManagementSaveFileStore
{
    bool Exists(string path);
    string ReadAllText(string path);
    void WriteStaging(string path, string contents);
    void Promote(string stagingPath, string livePath, string previousPath);
    void DeleteIfExists(string path);
}

public sealed class SystemStrategicManagementSaveFileStore : IStrategicManagementSaveFileStore
{
    public bool Exists(string path) => File.Exists(path);

    public string ReadAllText(string path) => File.ReadAllText(path);

    public void WriteStaging(string path, string contents)
    {
        string directory = Path.GetDirectoryName(path) ?? "";
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using FileStream stream = new(path, FileMode.Create, System.IO.FileAccess.Write, FileShare.None);
        using StreamWriter writer = new(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), 1024, leaveOpen: true);
        writer.Write(contents ?? "");
        writer.Flush();
        stream.Flush(flushToDisk: true);
    }

    public void Promote(string stagingPath, string livePath, string previousPath)
    {
        if (!File.Exists(livePath))
        {
            File.Move(stagingPath, livePath, overwrite: true);
            return;
        }

        if (File.Exists(previousPath))
        {
            File.Delete(previousPath);
        }

        try
        {
            File.Replace(stagingPath, livePath, previousPath, ignoreMetadataErrors: true);
        }
        catch (PlatformNotSupportedException)
        {
            File.Copy(livePath, previousPath, overwrite: true);
            File.Move(stagingPath, livePath, overwrite: true);
        }
    }

    public void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

public sealed class StrategicManagementSaveDocument
{
    public int Version { get; set; }
    public StrategicManagementState State { get; set; } = new();
}

public sealed class UnsupportedStrategicSaveVersionException : InvalidOperationException
{
    public UnsupportedStrategicSaveVersionException(int version, string path)
        : base($"Unsupported strategic management save version={version} current={StrategicManagementSaveService.CurrentVersion} path={path}")
    {
    }
}
