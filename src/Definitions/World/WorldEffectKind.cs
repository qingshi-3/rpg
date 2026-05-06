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
    CreateThreat = 10,
    SetThreatStage = 11,
    StartBattle = 12,
    CreateArmy = 13,
    AddSiteTag = 14,
    RemoveSiteTag = 15
}
