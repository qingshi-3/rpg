namespace Rpg.Definitions.World;

public enum WorldConditionKind
{
    Always = 0,
    SiteControlStateIs = 1,
    SiteOwnerIs = 2,
    HasResourceAtLeast = 3,
    HasAvailablePopulation = 4,
    HasFacility = 5,
    HasEmptyFacilitySlot = 6,
    HasGarrisonAtLeast = 7,
    ThreatStageIs = 8,
    NoActiveThreatOfRule = 9,
    HasGarrisonCapacity = 10
}
