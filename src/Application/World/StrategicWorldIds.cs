namespace Rpg.Application.World;

public static class StrategicWorldIds
{
    public const string DefinitionChapter01 = "chapter_01_strategic_v1";

    public const string FactionPlayer = "player";
    public const string FactionUndead = "undead";

    public const string FactionCapabilityFieldIntervention = "field_intervention";
    public const string FactionCapabilityCampLogistics = "camp_logistics";
    public const string FactionCapabilityGraveReinforcement = "grave_reinforcement";

    public const string ResourcePopulation = "population";
    public const string ResourceEconomy = "economy";
    public const string ResourceStone = "stone";

    public const string FacilityBarracks = "barracks";
    public const string FacilityMine = "mine";
    public const string FacilityDefenseTower = "defense_tower";

    public const string SitePlayerCamp = "player_camp";
    public const string SiteBonefield = "bonefield";
    public const string SiteGraveyard = "graveyard";

    public const string UnitGraveShadow = "neutral_shadow1";
    public const string UnitGraveMarksman = "neutral_shadowranged";
    public const string UnitDeathBlighter = "neutral_deathblighter";
    public static string UnitShieldHero => FirstSliceHeroCompanyIds.ShieldHeroUnit;
    public static string UnitShieldCorps => FirstSliceHeroCompanyIds.ShieldCorpsUnit;
    public static string UnitArcherHero => FirstSliceHeroCompanyIds.ArcherHeroUnit;
    public static string UnitArcherCorps => FirstSliceHeroCompanyIds.ArcherCorpsUnit;
    public static string UnitAssaultHero => FirstSliceHeroCompanyIds.AssaultHeroUnit;
    public static string UnitAssaultCorps => FirstSliceHeroCompanyIds.AssaultCorpsUnit;
    public static string UnitBonefieldLeader => FirstSliceHeroCompanyIds.BonefieldLeaderUnit;
    public static string UnitBonefieldHarassment => FirstSliceHeroCompanyIds.BonefieldHarassmentUnit;
    public static string UnitBonefieldRanged => FirstSliceHeroCompanyIds.BonefieldRangedUnit;
    // The early placeholder unit ids now point at authored battle definitions;
    // the removed placeholder packages duplicated other visuals.
    public const string UnitMilitia = "f1_shieldforger";
    public static string UnitPlayerKnight => FirstSliceHeroCompanyIds.ShieldHeroUnit;
    public const string UnitSkeletonWarrior = UnitGraveShadow;
    public const string UnitSkeletonArcher = UnitGraveMarksman;


    public const string OpportunityPoolWildernessV1 = "wilderness_v1";
    public const string OpportunityRuleWildernessV1 = "wilderness_v1_rule";
    public const string OpportunitySpiritHerbPatch = "spirit_herb_patch";
    public const string OpportunityLostCaravan = "lost_caravan";
    public const string OpportunityLooseStoneVein = "loose_stone_vein";

    public const string ActionBuildMine = "build_mine";
    public const string ActionBuildDefenseTower = "build_defense_tower";
    public const string ActionTrainMilitia = "train_militia";
    public const string ActionWaitTick = "wait_tick";
}
