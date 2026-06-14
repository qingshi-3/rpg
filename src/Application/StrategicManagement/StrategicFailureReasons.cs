namespace Rpg.Application.StrategicManagement;

public static class StrategicFailureReasons
{
    public const string MissingDefinitions = "missing_definitions";
    public const string MissingCity = "missing_city";
    public const string MissingLocation = "missing_location";
    public const string MissingFacility = "missing_facility";
    public const string MissingCorpsDefinition = "missing_corps_definition";
    public const string MissingHero = "missing_hero";
    public const string MissingCorpsInstance = "missing_corps_instance";
    public const string MissingExpedition = "missing_expedition";
    public const string MissingCityIdentity = "missing_city_identity";
    public const string MissingSourcePermission = "missing_source_permission";
    public const string InsufficientResources = "insufficient_resources";
    public const string NoProduction = "no_production";
    public const string InvalidElapsedWorldTimePulses = "invalid_elapsed_world_time_pulses";
    public const string WorldTimePaused = "world_time_paused";
    public const string FacilitySlotsFull = "facility_slots_full";
    public const string FactionMismatch = "faction_mismatch";
    public const string CorpsAlreadyAssigned = "corps_already_assigned";
    public const string HeroAlreadyAssigned = "hero_already_assigned";
    public const string HeroHasNoAssignedCorps = "hero_has_no_assigned_corps";
    public const string CorpsNotAssignedToHero = "corps_not_assigned_to_hero";
    public const string HeroAlreadyOnExpedition = "hero_already_on_expedition";
    public const string CorpsAlreadyOnExpedition = "corps_already_on_expedition";
    public const string SourceLocationNotOwned = "source_location_not_owned";
    public const string SameLocationTarget = "same_location_target";
    public const string TargetLocationNotOwned = "target_location_not_owned";
    public const string TargetLocationNotAttackable = "target_location_not_attackable";
    public const string ExpeditionCapacityFull = "expedition_capacity_full";
    public const string ExpeditionNotCommandable = "expedition_not_commandable";
    public const string UnsupportedExpeditionIntent = "unsupported_expedition_intent";
    public const string InvalidExpeditionParticipants = "invalid_expedition_participants";
    public const string MissingBattleEntryMetadata = "missing_battle_entry_metadata";
    public const string MissingBattleResultSummary = "missing_battle_result_summary";
    public const string MissingBattleParticipantResult = "missing_battle_participant_result";
    public const string BattleResultMismatch = "battle_result_mismatch";
    public const string BattleResultAlreadyApplied = "battle_result_already_applied";
}
