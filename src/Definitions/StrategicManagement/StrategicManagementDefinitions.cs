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

    public Dictionary<string, StrategicBuildingDefinition> Buildings { get; set; } =
        new(System.StringComparer.Ordinal);

    public Dictionary<string, StrategicCorpsDefinition> Corps { get; set; } =
        new(System.StringComparer.Ordinal);

    public Dictionary<string, StrategicHeroDefinition> Heroes { get; set; } =
        new(System.StringComparer.Ordinal);

    public StrategicConscriptionDefinition Conscription { get; set; } = new();
}

public sealed class StrategicResourceDefinition
{
    public string ResourceId { get; set; } = "";
    public string DisplayName { get; set; } = "";
}

public sealed class StrategicConscriptionDefinition
{
    public StrategicManualConscriptionDefinition Manual { get; set; } = new();
    public List<StrategicConscriptionIntensityDefinition> AutoIntensities { get; set; } = new();
}

public sealed class StrategicManualConscriptionDefinition
{
    public int ReserveGain { get; set; }
    public List<StrategicResourceAmount> Cost { get; set; } = new();
}

public sealed class StrategicConscriptionIntensityDefinition
{
    public string IntensityId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int ReserveGain { get; set; }
    public List<StrategicResourceAmount> Cost { get; set; } = new();
    public bool RequiresTrainingGround { get; set; }
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
    public List<StrategicConstructionRegionDefinition> ConstructionRegions { get; set; } = new();
    public List<string> SourcePermissionTags { get; set; } = new();
    public List<StrategicResourceAmount> ProductionPerWorldTimePulse { get; set; } = new();
}

public sealed class StrategicConstructionRegionDefinition
{
    public string RegionId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int OriginX { get; set; }
    public int OriginY { get; set; }
    public int Width { get; set; } = 1;
    public int Height { get; set; } = 1;
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

public sealed class StrategicBuildingDefinition
{
    public string BuildingDefinitionId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string IconPath { get; set; } = "";
    public string CategoryId { get; set; } = "";
    public int FootprintWidth { get; set; } = 1;
    public int FootprintHeight { get; set; } = 1;
    public List<StrategicResourceAmount> BuildCost { get; set; } = new();
    public StrategicBuildingProvidedCapabilities ProvidedCapabilities { get; set; } = new();
}

public sealed class StrategicBuildingProvidedCapabilities
{
    public List<StrategicResourceAmount> ResourceProductionPerWorldTimePulse { get; set; } = new();
}

public sealed class StrategicCorpsDefinition
{
    public string CorpsDefinitionId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string BattleUnitId { get; set; } = "";
    public int BattleUnitCount { get; set; } = 1;
    public int SoldierCapacityCost { get; set; } = 30;
    public List<string> RequiredCityIdentityIds { get; set; } = new();
    public List<string> RequiredBuildingCategoryIds { get; set; } = new();
    public List<StrategicResourceAmount> CreationCost { get; set; } = new();
    public List<StrategicResourceAmount> ReplenishFullCost { get; set; } = new();
    public string AptitudeTag { get; set; } = "";
}

public sealed class StrategicHeroDefinition
{
    public string HeroDefinitionId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string BattleUnitId { get; set; } = "";
    public List<string> AptitudeTags { get; set; } = new();
}
