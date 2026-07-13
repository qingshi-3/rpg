using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;
using Rpg.Definitions.StrategicManagement;
using Rpg.Domain.StrategicManagement;
using Rpg.Infrastructure.Logging;

namespace Rpg.Application.StrategicManagement;

public sealed class StrategicManagementSaveService
{
    public const int CurrentVersion = 5;

    private const string LegacyCompatibleMapId = "mock_qinghe_chiyan";
    private const string LegacyCompatibleScenarioId = "mock_qinghe_chiyan_campaign";
    private const int LegacyCompatiblePackageRevision = 1;
    private const int LegacyCompatibleScenarioRevision = 1;

    private const string LegacyPlainsCityId = "location_plains_city";
    private const string LegacyBonefieldOutpostId = "location_bonefield_outpost";

    private static readonly JsonSerializerOptions SaveJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true
    };

    private readonly StrategicManagementDefinitionSet _definitions;
    private readonly IStrategicManagementSaveFileStore _fileStore;
    private readonly StrategicManagementGeographyInvariantService _geographyInvariants = new();

    public StrategicManagementSaveService(
        StrategicManagementDefinitionSet definitions = null,
        IStrategicManagementSaveFileStore fileStore = null)
    {
        _definitions = definitions ?? FirstStrategicManagementDefinitions.Create();
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
                MapId = _definitions.ContentIdentity.MapId,
                ScenarioId = _definitions.ContentIdentity.ScenarioId,
                PackageCompatibilityRevision = _definitions.ContentIdentity.PackageCompatibilityRevision,
                ScenarioContentRevision = _definitions.ContentIdentity.ScenarioContentRevision,
                State = state
            },
            SaveJsonOptions);
    }

    private StrategicManagementState DeserializeAndMigrate(string json, string path)
    {
        ValidateDocumentShape(json, path, out int declaredVersion);
        string normalizedJson = NormalizeLegacyExpeditionAliases(json, declaredVersion, path);
        StrategicManagementSaveDocument document = JsonSerializer.Deserialize<StrategicManagementSaveDocument>(normalizedJson, SaveJsonOptions)
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

        if (version == 2)
        {
            MigrateVersionTwoToThree(state, path);
            version = 3;
        }

        if (version == 3)
        {
            MigrateVersionThreeToFour(state, path);
            version = 4;
        }

        if (version == 4)
        {
            MigrateVersionFourToFive(document, path);
            version = 5;
        }

        if (version != CurrentVersion)
        {
            throw new InvalidDataException($"Strategic management save migration incomplete version={version} path={path}");
        }

        ValidateContentIdentity(document, path);

        NormalizeCollections(state);
        RejectRetiredLocationIds(state, path);
        _geographyInvariants.ThrowIfInvalid(_definitions, state, $"save:{path}");
        return state;
    }

    private void MigrateVersionFourToFive(StrategicManagementSaveDocument document, string path)
    {
        StrategicManagementContentIdentity identity = _definitions.ContentIdentity;
        if (!string.Equals(identity.MapId, LegacyCompatibleMapId, StringComparison.Ordinal) ||
            !string.Equals(identity.ScenarioId, LegacyCompatibleScenarioId, StringComparison.Ordinal) ||
            identity.PackageCompatibilityRevision != LegacyCompatiblePackageRevision ||
            identity.ScenarioContentRevision != LegacyCompatibleScenarioRevision)
        {
            throw new StrategicManagementSaveIdentityMismatchException(
                $"Strategic management v4 save can migrate only to the explicit mock identity path={path} selected={identity}");
        }
        document.MapId = LegacyCompatibleMapId;
        document.ScenarioId = LegacyCompatibleScenarioId;
        document.PackageCompatibilityRevision = LegacyCompatiblePackageRevision;
        document.ScenarioContentRevision = LegacyCompatibleScenarioRevision;
    }

    private void ValidateContentIdentity(StrategicManagementSaveDocument document, string path)
    {
        StrategicManagementContentIdentity expected = _definitions.ContentIdentity;
        if (!string.Equals(document.MapId, expected.MapId, StringComparison.Ordinal) ||
            !string.Equals(document.ScenarioId, expected.ScenarioId, StringComparison.Ordinal) ||
            document.PackageCompatibilityRevision != expected.PackageCompatibilityRevision ||
            document.ScenarioContentRevision != expected.ScenarioContentRevision)
        {
            throw new StrategicManagementSaveIdentityMismatchException(
                $"Strategic management save content identity mismatch path={path} expected={expected} actual=MapId={document.MapId},ScenarioId={document.ScenarioId},PackageRevision={document.PackageCompatibilityRevision},ScenarioRevision={document.ScenarioContentRevision}");
        }
    }

    private static string NormalizeLegacyExpeditionAliases(string json, int version, string path)
    {
        JsonObject root = JsonNode.Parse(json)?.AsObject()
                          ?? throw new InvalidDataException($"Invalid strategic management save path={path}");
        JsonObject state = GetRequiredObject(root, "State", path);
        JsonObject expeditions = GetRequiredObject(state, "Expeditions", path);
        foreach ((string expeditionKey, JsonNode expeditionNode) in expeditions.ToList())
        {
            if (expeditionNode is not JsonObject expedition)
            {
                throw new InvalidDataException($"Strategic management save contains malformed expedition={expeditionKey} path={path}");
            }

            bool hasHeroAlias = TryGetNodeProperty(expedition, "HeroId", out string heroAliasKey, out JsonNode heroAliasNode);
            bool hasCorpsAlias = TryGetNodeProperty(expedition, "CorpsInstanceId", out string corpsAliasKey, out JsonNode corpsAliasNode);
            if (version >= 3)
            {
                if (hasHeroAlias || hasCorpsAlias)
                {
                    throw new InvalidDataException($"Current strategic management save contains retired expedition aliases expedition={expeditionKey} path={path}");
                }

                continue;
            }

            string heroAlias = ReadLegacyString(heroAliasNode, hasHeroAlias, "HeroId", expeditionKey, path);
            string corpsAlias = ReadLegacyString(corpsAliasNode, hasCorpsAlias, "CorpsInstanceId", expeditionKey, path);
            if (hasHeroAlias != hasCorpsAlias ||
                hasHeroAlias && (string.IsNullOrWhiteSpace(heroAlias) || string.IsNullOrWhiteSpace(corpsAlias)))
            {
                throw new InvalidDataException($"Cannot migrate incomplete expedition aliases expedition={expeditionKey} path={path}");
            }

            JsonArray participants;
            if (!TryGetNodeProperty(expedition, "Participants", out _, out JsonNode participantsNode) ||
                participantsNode == null)
            {
                participants = new JsonArray();
                expedition["Participants"] = participants;
            }
            else if (participantsNode is JsonArray participantArray)
            {
                participants = participantArray;
            }
            else
            {
                throw new InvalidDataException($"Cannot migrate malformed expedition participants expedition={expeditionKey} path={path}");
            }

            if (participants.Count == 0)
            {
                if (!hasHeroAlias)
                {
                    throw new InvalidDataException($"Cannot migrate expedition without participant identity expedition={expeditionKey} path={path}");
                }

                participants.Add(new JsonObject
                {
                    ["HeroId"] = heroAlias,
                    ["CorpsInstanceId"] = corpsAlias
                });
            }
            else if (hasHeroAlias)
            {
                if (participants[0] is not JsonObject leadParticipant ||
                    !TryReadRequiredString(leadParticipant, "HeroId", out string participantHeroId) ||
                    !TryReadRequiredString(leadParticipant, "CorpsInstanceId", out string participantCorpsId) ||
                    !string.Equals(heroAlias, participantHeroId, StringComparison.Ordinal) ||
                    !string.Equals(corpsAlias, participantCorpsId, StringComparison.Ordinal))
                {
                    throw new InvalidDataException($"Cannot migrate ambiguous expedition aliases expedition={expeditionKey} path={path}");
                }
            }

            if (hasHeroAlias)
            {
                expedition.Remove(heroAliasKey);
                expedition.Remove(corpsAliasKey);
            }
        }

        return root.ToJsonString(SaveJsonOptions);
    }

    private static JsonObject GetRequiredObject(JsonObject parent, string propertyName, string path)
    {
        if (!TryGetNodeProperty(parent, propertyName, out _, out JsonNode node) || node is not JsonObject result)
        {
            throw new InvalidDataException($"Strategic management save object={propertyName} is missing or malformed path={path}");
        }

        return result;
    }

    private static bool TryGetNodeProperty(
        JsonObject source,
        string propertyName,
        out string actualName,
        out JsonNode value)
    {
        foreach ((string key, JsonNode node) in source)
        {
            if (string.Equals(key, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                actualName = key;
                value = node;
                return true;
            }
        }

        actualName = "";
        value = null;
        return false;
    }

    private static string ReadLegacyString(
        JsonNode node,
        bool exists,
        string propertyName,
        string expeditionId,
        string path)
    {
        if (!exists)
        {
            return "";
        }

        try
        {
            return node?.GetValue<string>()?.Trim() ?? "";
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidDataException(
                $"Cannot migrate malformed expedition alias={propertyName} expedition={expeditionId} path={path}",
                exception);
        }
    }

    private static bool TryReadRequiredString(JsonObject source, string propertyName, out string value)
    {
        value = "";
        if (!TryGetNodeProperty(source, propertyName, out _, out JsonNode node))
        {
            return false;
        }

        try
        {
            value = node?.GetValue<string>()?.Trim() ?? "";
            return !string.IsNullOrWhiteSpace(value);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
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

    private void MigrateVersionTwoToThree(StrategicManagementState state, string path)
    {
        // Legacy lead aliases were normalized in the versioned JSON boundary.
        // Version three persists only canonical participant rows.
        NormalizeCollections(state);
        foreach (StrategicExpeditionState expedition in state.Expeditions.Values.Where(item => item.Status == StrategicExpeditionStatus.Moving))
        {
            foreach (StrategicExpeditionParticipantState participant in expedition.Participants.Where(item => string.IsNullOrWhiteSpace(item.RollbackStationLocationId)))
            {
                if (!CanProveVersionOneRollbackStation(state, expedition, participant))
                {
                    throw new InvalidDataException(
                        $"Cannot migrate expedition rollback station path={path} expedition={expedition.ExpeditionId} hero={participant.HeroId} corps={participant.CorpsInstanceId}");
                }

                participant.RollbackStationLocationId = expedition.SourceLocationId;
            }
        }
    }

    private void MigrateVersionThreeToFour(StrategicManagementState state, string path)
    {
        NormalizeCollections(state);
        bool containsLegacy = IdentityGraphContains(state, LegacyPlainsCityId) ||
                              IdentityGraphContains(state, LegacyBonefieldOutpostId);
        int canonicalCityCount = _definitions?.CanonicalGeography?.Cities?.Count ?? 0;
        int presentCanonicalCityCount = _definitions?.CanonicalGeography?.Cities?.Keys.Count(state.Locations.ContainsKey) ?? 0;

        if (!containsLegacy)
        {
            if (presentCanonicalCityCount != canonicalCityCount)
            {
                throw new InvalidDataException(
                    $"Cannot migrate partial canonical strategic geography path={path} present={presentCanonicalCityCount} expected={canonicalCityCount}");
            }
            return;
        }

        if (presentCanonicalCityCount > 0)
        {
            throw new InvalidDataException(
                $"Cannot migrate mixed legacy/canonical strategic identity graph path={path} canonicalCities={presentCanonicalCityCount}");
        }

        MigrateLocationIdentity(state, LegacyPlainsCityId, StrategicManagementIds.LocationQingheCore, path);
        MigrateLocationIdentity(state, LegacyBonefieldOutpostId, StrategicManagementIds.LocationChiyanHighBasin, path);
        SeedCanonicalAuxiliaryControlRecords(state, path);
    }

    private void MigrateLocationIdentity(
        StrategicManagementState state,
        string legacyId,
        string canonicalId,
        string path)
    {
        if (!IdentityGraphContains(state, legacyId))
        {
            throw new InvalidDataException(
                $"Cannot migrate incomplete legacy strategic identity old={legacyId} new={canonicalId} path={path}");
        }
        if (IdentityGraphContains(state, canonicalId))
        {
            throw new InvalidDataException(
                $"Cannot migrate legacy/canonical strategic identity collision old={legacyId} new={canonicalId} path={path}");
        }
        if (!state.Locations.ContainsKey(legacyId) || !state.Cities.ContainsKey(legacyId))
        {
            throw new InvalidDataException(
                $"Cannot migrate incomplete legacy strategic identity records old={legacyId} new={canonicalId} path={path}");
        }

        RenameLocationDictionaryRecord(state.Locations, legacyId, canonicalId, location => location.LocationId, (location, id) => location.LocationId = id, "Locations", path);
        RenameLocationDictionaryRecord(state.Cities, legacyId, canonicalId, city => city.LocationId, (city, id) => city.LocationId = id, "Cities", path);

        foreach (StrategicCorpsInstanceState corps in state.CorpsInstances.Values)
        {
            corps.HomeCityId = ReplaceLocationId(corps.HomeCityId, legacyId, canonicalId);
        }
        foreach (StrategicExpeditionState expedition in state.Expeditions.Values)
        {
            expedition.SourceLocationId = ReplaceLocationId(expedition.SourceLocationId, legacyId, canonicalId);
            expedition.TargetLocationId = ReplaceLocationId(expedition.TargetLocationId, legacyId, canonicalId);
            foreach (StrategicExpeditionParticipantState participant in expedition.Participants)
            {
                participant.RollbackStationLocationId = ReplaceLocationId(
                    participant.RollbackStationLocationId,
                    legacyId,
                    canonicalId);
            }
        }
        foreach (StrategicBattleFeedbackRecord feedback in state.BattleFeedbackRecords.Values)
        {
            feedback.TargetLocationId = ReplaceLocationId(feedback.TargetLocationId, legacyId, canonicalId);
        }
    }

    private void SeedCanonicalAuxiliaryControlRecords(StrategicManagementState state, string path)
    {
        foreach (StrategicManagementCityReference city in _definitions.CanonicalGeography.Cities.Values)
        {
            if (state.Locations.ContainsKey(city.LocationId))
            {
                continue;
            }

            StrategicScenarioProvinceStart start = _definitions.Scenario.Provinces
                .Single(province => province.ProvinceId == city.ProvinceId);
            string ownerFactionId = start.OwnerFactionId;
            StrategicLocationControlState controlState = start.Control switch
            {
                StrategicScenarioControl.PlayerHeld => StrategicLocationControlState.PlayerHeld,
                StrategicScenarioControl.EnemyHeld => StrategicLocationControlState.EnemyHeld,
                _ => StrategicLocationControlState.Neutral
            };
            state.Locations.Add(city.LocationId, new StrategicLocationState
            {
                LocationId = city.LocationId,
                OwnerFactionId = ownerFactionId,
                ControlState = controlState
            });
        }

        int migratedCount = _definitions.CanonicalGeography.Cities.Keys.Count(state.Locations.ContainsKey);
        if (migratedCount != _definitions.CanonicalGeography.Cities.Count)
        {
            throw new InvalidDataException(
                $"Canonical strategic city control migration is incomplete path={path} actual={migratedCount} expected={_definitions.CanonicalGeography.Cities.Count}");
        }
    }

    private static void RenameLocationDictionaryRecord<T>(
        System.Collections.Generic.Dictionary<string, T> records,
        string legacyId,
        string canonicalId,
        Func<T, string> readId,
        Action<T, string> writeId,
        string collection,
        string path)
        where T : class
    {
        bool hasLegacy = records.TryGetValue(legacyId, out T record);
        if (records.ContainsKey(canonicalId))
        {
            throw new InvalidDataException(
                $"Cannot migrate strategic identity collision collection={collection} old={legacyId} new={canonicalId} path={path}");
        }
        if (!hasLegacy)
        {
            return;
        }
        string recordId = record == null ? "<null>" : readId(record) ?? "";
        if (record == null || !string.Equals(recordId, legacyId, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Cannot migrate strategic key/value mismatch collection={collection} key={legacyId} value={recordId} path={path}");
        }

        records.Remove(legacyId);
        writeId(record, canonicalId);
        records.Add(canonicalId, record);
    }

    private static bool IdentityGraphContains(StrategicManagementState state, string locationId)
    {
        return state.Locations.ContainsKey(locationId) ||
               state.Locations.Values.Any(item => string.Equals(item.LocationId, locationId, StringComparison.Ordinal)) ||
               state.Cities.ContainsKey(locationId) ||
               state.Cities.Values.Any(item => string.Equals(item.LocationId, locationId, StringComparison.Ordinal)) ||
               state.CorpsInstances.Values.Any(item => string.Equals(item.HomeCityId, locationId, StringComparison.Ordinal)) ||
               state.Expeditions.Values.Any(expedition =>
                   string.Equals(expedition.SourceLocationId, locationId, StringComparison.Ordinal) ||
                   string.Equals(expedition.TargetLocationId, locationId, StringComparison.Ordinal) ||
                   expedition.Participants.Any(participant =>
                       string.Equals(participant.RollbackStationLocationId, locationId, StringComparison.Ordinal))) ||
               state.BattleFeedbackRecords.Values.Any(item =>
                   string.Equals(item.TargetLocationId, locationId, StringComparison.Ordinal));
    }

    private static string ReplaceLocationId(string value, string legacyId, string canonicalId) =>
        string.Equals(value, legacyId, StringComparison.Ordinal) ? canonicalId : value ?? "";

    private static void RejectRetiredLocationIds(StrategicManagementState state, string path)
    {
        foreach (string legacyId in new[] { LegacyPlainsCityId, LegacyBonefieldOutpostId })
        {
            if (IdentityGraphContains(state, legacyId))
            {
                throw new InvalidDataException(
                    $"Current strategic management save contains retired LocationId={legacyId} path={path}");
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
               _definitions?.Locations.TryGetValue(ResolveDefinitionLocationId(sourceId), out StrategicLocationDefinition sourceDefinition) == true &&
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

    private static string ResolveDefinitionLocationId(string persistedLocationId) => persistedLocationId switch
    {
        LegacyPlainsCityId => StrategicManagementIds.LocationQingheCore,
        LegacyBonefieldOutpostId => StrategicManagementIds.LocationChiyanHighBasin,
        _ => persistedLocationId ?? ""
    };

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
            if (expedition.Participants.Count == 0)
            {
                throw new InvalidDataException($"Strategic management save expedition has no canonical participants expedition={expedition.ExpeditionId}");
            }

            if (expedition.Participants.Exists(participant => participant == null))
            {
                throw new InvalidDataException($"Strategic management save contains a null expedition participant expedition={expedition.ExpeditionId}");
            }

            if (expedition.Participants.Any(participant =>
                    string.IsNullOrWhiteSpace(participant.HeroId) ||
                    string.IsNullOrWhiteSpace(participant.CorpsInstanceId) ||
                    !state.Heroes.ContainsKey(participant.HeroId) ||
                    !state.CorpsInstances.ContainsKey(participant.CorpsInstanceId)) ||
                expedition.Participants.Select(participant => participant.HeroId).Distinct(StringComparer.Ordinal).Count() != expedition.Participants.Count ||
                expedition.Participants.Select(participant => participant.CorpsInstanceId).Distinct(StringComparer.Ordinal).Count() != expedition.Participants.Count)
            {
                throw new InvalidDataException($"Strategic management save contains invalid or ambiguous expedition participants expedition={expedition.ExpeditionId}");
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
    public string MapId { get; set; } = "";
    public string ScenarioId { get; set; } = "";
    public int PackageCompatibilityRevision { get; set; }
    public int ScenarioContentRevision { get; set; }
    public StrategicManagementState State { get; set; } = new();
}

public sealed class UnsupportedStrategicSaveVersionException : InvalidOperationException
{
    public UnsupportedStrategicSaveVersionException(int version, string path)
        : base($"Unsupported strategic management save version={version} current={StrategicManagementSaveService.CurrentVersion} path={path}")
    {
    }
}

public sealed class StrategicManagementSaveIdentityMismatchException : InvalidOperationException
{
    public StrategicManagementSaveIdentityMismatchException(string message) : base(message)
    {
    }
}
