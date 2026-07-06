using System.Collections.Generic;
using Rpg.Definitions.StrategicManagement;
using Rpg.Domain.StrategicManagement;

namespace Rpg.Application.StrategicManagement;

public sealed class StrategicManagementDashboardViewModel
{
    public string FactionId { get; set; } = "";
    public StrategicLocationDashboardViewModel SelectedLocation { get; set; } = new();
    public StrategicCityManagementViewModel SelectedCity { get; set; } = new();
    public List<StrategicResourceViewModel> Resources { get; set; } = new();
    public List<StrategicHeroAssignmentViewModel> Heroes { get; set; } = new();
    public StrategicBattleFeedbackViewModel LatestBattleFeedback { get; set; } = new();
}

public sealed class StrategicLocationDashboardViewModel
{
    public string LocationId { get; set; } = "";
    public string MapSiteId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public StrategicLocationKind Kind { get; set; } = StrategicLocationKind.Unknown;
    public string KindDisplayName { get; set; } = "";
    public string OwnerFactionId { get; set; } = "";
    public StrategicLocationControlState ControlState { get; set; } = StrategicLocationControlState.Unknown;
    public string ControlStateDisplayName { get; set; } = "";
    public bool IsCity { get; set; }
    public bool CanManageCity { get; set; }
    public List<string> SourcePermissionTags { get; set; } = new();
    public string SourcePermissionDisplayText { get; set; } = "";
    public List<StrategicResourceProductionViewModel> ProductionPerWorldTimePulse { get; set; } = new();
    public string ProductionDisplayText { get; set; } = "";
}

public sealed class StrategicResourceProductionViewModel
{
    public string ResourceId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int Amount { get; set; }
}

public sealed class StrategicResourceViewModel
{
    public string ResourceId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int Amount { get; set; }
}

public sealed class StrategicResourceCostViewModel
{
    public string ResourceId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int Amount { get; set; }
}

public sealed class StrategicBattleFeedbackViewModel
{
    public string FeedbackId { get; set; } = "";
    public string ExpeditionId { get; set; } = "";
    public string SessionId { get; set; } = "";
    public string TargetLocationId { get; set; } = "";
    public string TargetDisplayName { get; set; } = "";
    public bool Victory { get; set; }
    public string OutcomeText { get; set; } = "";
    public string WorldChangeText { get; set; } = "";
    public string FailureReasonText { get; set; } = "";
    public string ProgressionText { get; set; } = "";
    public List<string> RewardLines { get; set; } = new();
    public List<StrategicBattleParticipantFeedbackViewModel> ParticipantFeedback { get; set; } = new();
    public List<StrategicHeroBattleFeedbackViewModel> HeroFeedback { get; set; } = new();
    public List<StrategicEquipmentSampleFeedbackViewModel> EquipmentSamples { get; set; } = new();
}

public sealed class StrategicBattleParticipantFeedbackViewModel
{
    public string HeroId { get; set; } = "";
    public string HeroDisplayName { get; set; } = "";
    public string CorpsInstanceId { get; set; } = "";
    public string CorpsDisplayName { get; set; } = "";
    public int RemainingCorpsStrength { get; set; }
    public int StrengthLoss { get; set; }
    public string ResultText { get; set; } = "";
}

public sealed class StrategicHeroBattleFeedbackViewModel
{
    public string HeroId { get; set; } = "";
    public string HeroDisplayName { get; set; } = "";
    public string ReactionText { get; set; } = "";
}

public sealed class StrategicEquipmentSampleFeedbackViewModel
{
    public string EquipmentSampleId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string SlotKind { get; set; } = "";
    public string Grade { get; set; } = "";
    public string RoleText { get; set; } = "";
    public bool IsReward { get; set; }
}

public sealed class StrategicCityManagementViewModel
{
    public string LocationId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string CityIdentityId { get; set; } = "";
    public string CityIdentityDisplayName { get; set; } = "";
    public int CityForceCapacity { get; set; }
    public int ReserveForces { get; set; }
    public int ActiveForces { get; set; }
    public int RemainingForceCapacity { get; set; }
    public StrategicConscriptionViewModel Conscription { get; set; } = new();
    public List<StrategicConstructionRegionViewModel> ConstructionRegions { get; set; } = new();
    public List<StrategicBuildingInstanceViewModel> Buildings { get; set; } = new();
    public List<StrategicBuildingOptionViewModel> BuildingOptions { get; set; } = new();
    public List<StrategicMusterTemplateViewModel> MusterTemplates { get; set; } = new();
    public List<StrategicCorpsInstanceViewModel> CorpsInstances { get; set; } = new();
    public List<StrategicHeroCompanyViewModel> HeroCompanies { get; set; } = new();
}

public sealed class StrategicConscriptionViewModel
{
    public string CurrentIntensityId { get; set; } = "";
    public int CityForceCapacity { get; set; }
    public int ReserveForces { get; set; }
    public int ActiveForces { get; set; }
    public int RemainingForceCapacity { get; set; }
    public StrategicConscriptionManualOptionViewModel ManualOption { get; set; } = new();
    public List<StrategicConscriptionIntensityOptionViewModel> IntensityOptions { get; set; } = new();
}

public sealed class StrategicConscriptionManualOptionViewModel
{
    public int ReserveGain { get; set; }
    public bool CanConscript { get; set; }
    public string DisabledReason { get; set; } = "";
    public List<StrategicResourceCostViewModel> Cost { get; set; } = new();
}

public sealed class StrategicConscriptionIntensityOptionViewModel
{
    public string IntensityId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int ReserveGain { get; set; }
    public bool RequiresTrainingGround { get; set; }
    public bool IsCurrent { get; set; }
    public bool CanSelect { get; set; }
    public string DisabledReason { get; set; } = "";
    public List<StrategicResourceCostViewModel> Cost { get; set; } = new();
}

public sealed class StrategicConstructionRegionViewModel
{
    public string RegionId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int OriginX { get; set; }
    public int OriginY { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

public sealed class StrategicBuildingInstanceViewModel
{
    public string BuildingInstanceId { get; set; } = "";
    public string BuildingDefinitionId { get; set; } = "";
    public string ConstructionRegionId { get; set; } = "";
    public string RegionDisplayName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string CategoryId { get; set; } = "";
    public int GridX { get; set; }
    public int GridY { get; set; }
    public int FootprintWidth { get; set; }
    public int FootprintHeight { get; set; }
    public int Level { get; set; }
    public bool IsConstructed { get; set; }
    public string BattleAnchorId { get; set; } = "";
}

public sealed class StrategicBuildingOptionViewModel
{
    public string BuildingDefinitionId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string IconPath { get; set; } = "";
    public string CategoryId { get; set; } = "";
    public int FootprintWidth { get; set; }
    public int FootprintHeight { get; set; }
    public bool CanBuild { get; set; }
    public string DisabledReason { get; set; } = "";
    public string DefaultRegionId { get; set; } = "";
    public int DefaultGridX { get; set; }
    public int DefaultGridY { get; set; }
    public List<StrategicResourceCostViewModel> BuildCost { get; set; } = new();
}

public sealed class StrategicMusterTemplateViewModel
{
    public string CorpsDefinitionId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string BattleUnitId { get; set; } = "";
    public int ReserveForceCost { get; set; }
    public bool CanCreate { get; set; }
    public List<string> DisabledReasons { get; set; } = new();
    public List<StrategicResourceCostViewModel> CreationCost { get; set; } = new();
}

public sealed class StrategicCorpsInstanceViewModel
{
    public string CorpsInstanceId { get; set; } = "";
    public string CorpsDefinitionId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string BattleUnitId { get; set; } = "";
    public string HomeCityId { get; set; } = "";
    public int Strength { get; set; }
    public int Level { get; set; }
    public int EquipmentLevel { get; set; }
    public int Experience { get; set; }
    public StrategicCorpsInstanceStatus Status { get; set; }
    public string AssignedHeroId { get; set; } = "";
    public bool CanReplenish { get; set; }
    public string ReplenishDisabledReason { get; set; } = "";
    public int ReplenishReserveCost { get; set; }
    public List<StrategicResourceCostViewModel> ReplenishCost { get; set; } = new();
}

public sealed class StrategicHeroAssignmentViewModel
{
    public string HeroId { get; set; } = "";
    public string HeroDefinitionId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string BattleUnitId { get; set; } = "";
    public bool HasAssignedCorps { get; set; }
    public string AssignedCorpsInstanceId { get; set; } = "";
    public string AssignedCorpsDefinitionId { get; set; } = "";
    public string AssignedCorpsDisplayName { get; set; } = "";
    public StrategicHeroCorpsAptitudeGrade AptitudeGrade { get; set; }
}

public sealed class StrategicHeroCompanyViewModel
{
    public string HeroId { get; set; } = "";
    public string HeroDefinitionId { get; set; } = "";
    public string HeroDisplayName { get; set; } = "";
    public string HeroBattleUnitId { get; set; } = "";
    public string CorpsInstanceId { get; set; } = "";
    public string CorpsDefinitionId { get; set; } = "";
    public string CorpsDisplayName { get; set; } = "";
    public string CorpsBattleUnitId { get; set; } = "";
    public string SourceCityId { get; set; } = "";
    public int Strength { get; set; }
    public int Level { get; set; }
    public int EquipmentLevel { get; set; }
    public StrategicHeroCorpsAptitudeGrade AptitudeGrade { get; set; }
    public bool CanCreateExpedition { get; set; }
    public string DisabledReason { get; set; } = "";
}
