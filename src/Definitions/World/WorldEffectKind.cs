namespace Rpg.Definitions.World;

public enum WorldEffectKind
{
    AddResource = 0,
    SpendResource = 1,
    SetSiteControlState = 4,
    SetSiteOwner = 5,
    AddGarrison = 8,
    RemoveGarrison = 9,
    StartBattle = 10,
    CreateArmy = 11,
    AddSiteTag = 12,
    RemoveSiteTag = 13
}
