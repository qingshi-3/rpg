namespace Rpg.Definitions.World;

public enum WorldConditionKind
{
    Always = 0,
    SiteControlStateIs = 1,
    SiteOwnerIs = 2,
    HasResourceAtLeast = 3,
    HasAvailablePopulation = 4,
    HasGarrisonAtLeast = 7,
    HasGarrisonCapacity = 8
}
