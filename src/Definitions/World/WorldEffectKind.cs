namespace Rpg.Definitions.World;

public enum WorldEffectKind
{
    AddResource = 0,
    SpendResource = 1,
    ReserveResource = 2,
    ReleaseResourceReservation = 3,
    SetSiteControlState = 4,
    SetSiteOwner = 5,
    AddFacility = 6,
    SetFacilityState = 7,
    AddGarrison = 8,
    RemoveGarrison = 9,
    StartBattle = 10,
    CreateArmy = 11,
    AddSiteTag = 12,
    RemoveSiteTag = 13
}
