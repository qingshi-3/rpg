using System.Collections.Generic;

namespace Rpg.Definitions.StrategicManagement;

public sealed class StrategicManagementDefinitionSet
{
    public Dictionary<string, StrategicResourceDefinition> Resources { get; set; } =
        new(System.StringComparer.Ordinal);

    public Dictionary<string, StrategicLocationDefinition> Locations { get; set; } =
        new(System.StringComparer.Ordinal);

    public Dictionary<string, StrategicBattleRewardDefinition> BattleRewards { get; set; } =
        new(System.StringComparer.Ordinal);

    public Dictionary<string, StrategicEquipmentSampleDefinition> EquipmentSamples { get; set; } =
        new(System.StringComparer.Ordinal);

    public Dictionary<string, StrategicCityIdentityDefinition> CityIdentities { get; set; } =
        new(System.StringComparer.Ordinal);

    public Dictionary<string, StrategicFacilityDefinition> Facilities { get; set; } =
        new(System.StringComparer.Ordinal);

    public Dictionary<string, StrategicCorpsDefinition> Corps { get; set; } =
        new(System.StringComparer.Ordinal);

    public Dictionary<string, StrategicHeroDefinition> Heroes { get; set; } =
        new(System.StringComparer.Ordinal);
}

public sealed class StrategicResourceDefinition
{
    public string ResourceId { get; set; } = "";
    public string DisplayName { get; set; } = "";
}

public sealed class StrategicLocationDefinition
{
    public string LocationId { get; set; } = "";
    public string MapSiteId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public StrategicLocationKind Kind { get; set; } = StrategicLocationKind.Unknown;
    public string BattleEncounterId { get; set; } = "";
    public string BattleMapDefinitionId { get; set; } = "";
    public string BattleScenePath { get; set; } = "";
    public string BattleObjectiveId { get; set; } = "";
    public string CityIdentityId { get; set; } = "";
    public int FacilitySlotCount { get; set; }
    public List<string> SourcePermissionTags { get; set; } = new();
    public List<StrategicResourceAmount> ProductionPerWorldTimePulse { get; set; } = new();
}

public sealed class StrategicBattleRewardDefinition
{
    public string RewardId { get; set; } = "";
    public string TargetLocationId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string VictorySummaryText { get; set; } = "";
    public string DefeatSummaryText { get; set; } = "";
    public string VictoryProgressionText { get; set; } = "";
    public string DefeatProgressionText { get; set; } = "";
    public string UnlockText { get; set; } = "";
    public List<StrategicResourceAmount> VictoryResourceRewards { get; set; } = new();
    public List<string> EquipmentSampleIds { get; set; } = new();
    public string RewardEquipmentSampleId { get; set; } = "";
}

public sealed class StrategicEquipmentSampleDefinition
{
    public string EquipmentSampleId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string SlotKind { get; set; } = "";
    public string Grade { get; set; } = "";
    public string RoleText { get; set; } = "";
}

public sealed class StrategicCityIdentityDefinition
{
    public string CityIdentityId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public List<string> NaturalCorpsDefinitionIds { get; set; } = new();
}

public sealed class StrategicFacilityDefinition
{
    public string FacilityDefinitionId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int SlotCost { get; set; } = 1;
    public List<string> ProvidedTags { get; set; } = new();
    public List<StrategicResourceAmount> BuildCost { get; set; } = new();
}

public sealed class StrategicCorpsDefinition
{
    public string CorpsDefinitionId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string BattleUnitId { get; set; } = "";
    public int BattleUnitCount { get; set; } = 1;
    public List<string> RequiredCityIdentityIds { get; set; } = new();
    public List<string> RequiredFacilityTags { get; set; } = new();
    public List<string> RequiredSourcePermissionTags { get; set; } = new();
    public List<StrategicResourceAmount> CreationCost { get; set; } = new();
    public string AptitudeTag { get; set; } = "";
}

public sealed class StrategicHeroDefinition
{
    public string HeroDefinitionId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string BattleUnitId { get; set; } = "";
    public List<string> AptitudeTags { get; set; } = new();
}
