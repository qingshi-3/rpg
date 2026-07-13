#nullable enable

using System.Collections.Generic;

namespace Rpg.Definitions.StrategicManagement;

public enum StrategicScenarioProvinceRole
{
    PlayerStart,
    FirstHostile,
    Neutral
}

public enum StrategicScenarioControl
{
    PlayerHeld,
    EnemyHeld,
    Neutral
}

public sealed record StrategicScenarioProvinceStart(
    string ProvinceId,
    StrategicScenarioProvinceRole Role,
    string OwnerFactionId,
    StrategicScenarioControl Control);

public sealed record StrategicScenarioLocationStart(
    string LocationId,
    string OwnerFactionId,
    StrategicScenarioControl Control);

public sealed record StrategicScenarioResourceStart(
    string FactionId,
    string ResourceId,
    int Amount);

public sealed record StrategicManagementScenarioDefinition(
    int Version,
    string ScenarioId,
    string MapId,
    int PackageCompatibilityRevision,
    int ScenarioContentRevision,
    int DefaultCityForceCapacity,
    int DefaultCityReserveForces,
    IReadOnlyList<StrategicScenarioProvinceStart> Provinces,
    IReadOnlyList<StrategicScenarioLocationStart> Locations,
    IReadOnlyList<StrategicScenarioResourceStart> Resources);

public sealed record StrategicManagementContentIdentity(
    string MapId,
    string ScenarioId,
    int PackageCompatibilityRevision,
    int ScenarioContentRevision)
{
    public static StrategicManagementContentIdentity Empty { get; } = new("", "", 0, 0);
}
