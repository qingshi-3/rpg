using Godot;
using Rpg.Application.World;
using Rpg.Definitions.World;
using Rpg.Domain.World;

internal static partial class WorldSiteDeploymentCacheRegressionCases
{
internal static void StrategicArmyCommandServiceAppliesMoveToPosition()
{
    WorldArmyState army = BuildCommandableArmy("army_move");
    Dictionary<string, StrategicNavigationPath> paths = BuildCommandPaths(
        army.ArmyId,
        new Vector2(0, 0),
        new Vector2(32, 16),
        new Vector2(64, 32));

    WorldArmyCommandResult result = new WorldArmyCommandService().ApplyMoveToPosition(
        new[] { army },
        new Vector2(64, 32),
        paths,
        navigationSurfaceVersion: 7);

    AssertTrue(result.Success, $"move command should succeed reason={result.FailureReason}");
    AssertEqual(1, result.CommandedArmyIds.Count, "commanded army count");
    AssertEqual("", army.TargetSiteId, "move command target site");
    AssertEqual(new Vector2(64, 32), army.Destination, "move command destination");
    AssertEqual(WorldArmyIntent.MoveToPosition, army.Intent, "move command intent");
    AssertEqual(WorldArmyStatus.Moving, army.Status, "move command status");
    AssertTrue(!army.HasArrivalApproachOffset, "move command should clear arrival approach");
    AssertEqual(WorldSiteAttackDirection.Any, army.TargetApproachDirection, "move command should clear approach direction");
    AssertTrue(army.HasNavigationPath, "move command should apply navigation path");
    AssertEqual(7, army.NavigationSurfaceVersion, "move command navigation version");
}

internal static void StrategicArmyCommandServiceAppliesSiteCommand()
{
    WorldArmyState army = BuildCommandableArmy("army_site");
    Dictionary<string, StrategicNavigationPath> paths = BuildCommandPaths(
        army.ArmyId,
        new Vector2(2, 2),
        new Vector2(20, 20),
        new Vector2(40, 40));

    WorldArmyCommandResult result = new WorldArmyCommandService().ApplySiteCommand(
        new[] { army },
        targetSiteId: "site_target",
        destination: new Vector2(40, 40),
        arrivalApproachOffset: new Vector2(3, 4),
        approachDirection: WorldSiteAttackDirection.East,
        intent: WorldArmyIntent.AssaultSite,
        paths,
        navigationSurfaceVersion: 11);

    AssertTrue(result.Success, $"site command should succeed reason={result.FailureReason}");
    AssertEqual(1, result.CommandedArmyIds.Count, "commanded army count");
    AssertEqual("site_target", army.TargetSiteId, "site command target site");
    AssertEqual(new Vector2(40, 40), army.Destination, "site command destination");
    AssertEqual(WorldArmyIntent.AssaultSite, army.Intent, "site command intent");
    AssertEqual(WorldArmyStatus.Moving, army.Status, "site command status");
    AssertTrue(army.HasArrivalApproachOffset, "site command should set arrival approach");
    AssertEqual(new Vector2(3, 4), army.ArrivalApproachOffset, "site command arrival approach");
    AssertEqual(WorldSiteAttackDirection.East, army.TargetApproachDirection, "site command approach direction");
    AssertTrue(army.HasNavigationPath, "site command should apply navigation path");
    AssertEqual(11, army.NavigationSurfaceVersion, "site command navigation version");
}

internal static void StrategicArmyCommandServiceResetsUnsupportedAssault()
{
    WorldArmyState army = BuildCommandableArmy("army_unsupported");
    army.TargetSiteId = "unsupported_site";
    army.Intent = WorldArmyIntent.AssaultSite;
    army.Status = WorldArmyStatus.Attacking;
    army.SetNavigationPath(new[] { new Vector2(0, 0), new Vector2(8, 8) }, new Vector2(8, 8), 3);

    WorldArmyCommandResult result = new WorldArmyCommandService().ResetUnsupportedAssault(army);

    AssertTrue(result.Success, $"unsupported assault reset should succeed reason={result.FailureReason}");
    AssertEqual(1, result.CommandedArmyIds.Count, "reset army count");
    AssertEqual("unsupported_site", army.TargetSiteId, "reset should preserve target site for diagnostics and notice text");
    AssertEqual(WorldArmyIntent.None, army.Intent, "reset intent");
    AssertEqual(WorldArmyStatus.Idle, army.Status, "reset status");
    AssertTrue(!army.HasNavigationPath, "reset should clear navigation path");
}

internal static void StrategicArmyCommandServiceDefersArrivedAssaultToStandbyMove()
{
    WorldArmyState army = BuildCommandableArmy("army_defer_assault");
    army.TargetSiteId = "site_target";
    army.Intent = WorldArmyIntent.AssaultSite;
    army.Status = WorldArmyStatus.Attacking;
    army.SetArrivalApproachOffset(new Vector2(2, 0));
    army.SetTargetApproachDirection(WorldSiteAttackDirection.East);
    Dictionary<string, StrategicNavigationPath> paths = BuildCommandPaths(
        army.ArmyId,
        new Vector2(10, 10),
        new Vector2(48, 16));

    WorldArmyCommandResult result = new WorldArmyCommandService().ApplyDeferredAssaultStandbyMovement(
        army,
        new Vector2(48, 16),
        paths,
        navigationSurfaceVersion: 17);

    AssertTrue(result.Success, $"deferred assault standby command should succeed reason={result.FailureReason}");
    AssertEqual(1, result.CommandedArmyIds.Count, "deferred assault army count");
    AssertEqual("", army.TargetSiteId, "deferred assault should clear target site");
    AssertEqual(new Vector2(48, 16), army.Destination, "deferred assault standby destination");
    AssertEqual(WorldArmyIntent.MoveToPosition, army.Intent, "deferred assault intent");
    AssertEqual(WorldArmyStatus.Moving, army.Status, "deferred assault status");
    AssertTrue(!army.HasArrivalApproachOffset, "deferred assault should clear arrival approach");
    AssertEqual(WorldSiteAttackDirection.Any, army.TargetApproachDirection, "deferred assault should clear approach direction");
    AssertTrue(army.HasNavigationPath, "deferred assault should apply standby path");
    AssertEqual(17, army.NavigationSurfaceVersion, "deferred assault navigation version");
}

internal static void StrategicArmyCommandServiceAppliesResolvedSiteNavigationPoints()
{
    WorldArmyState army = BuildCommandableArmy("army_resolution");
    army.TargetSiteId = "site_target";
    army.WorldPosition = new Vector2(5, 6);
    army.Destination = new Vector2(10, 12);
    army.SetNavigationPath(new[] { new Vector2(5, 6), new Vector2(10, 12) }, new Vector2(10, 12), 3);

    WorldArmyCommandResult result = new WorldArmyCommandService().ApplyResolvedSiteNavigationPoints(
        army,
        resolvedWorldPosition: new Vector2(7, 8),
        resolvedDestination: new Vector2(20, 24),
        arrivalApproachOffset: new Vector2(-2, 3),
        approachDirection: WorldSiteAttackDirection.South);

    AssertTrue(result.Success, $"site navigation resolution should succeed reason={result.FailureReason}");
    AssertEqual(new Vector2(7, 8), army.WorldPosition, "resolved world position");
    AssertEqual(new Vector2(20, 24), army.Destination, "resolved destination");
    AssertTrue(army.HasArrivalApproachOffset, "resolved destination should update arrival approach");
    AssertEqual(new Vector2(-2, 3), army.ArrivalApproachOffset, "resolved arrival approach");
    AssertEqual(WorldSiteAttackDirection.South, army.TargetApproachDirection, "resolved approach direction");
    AssertTrue(!army.HasNavigationPath, "resolved navigation points should invalidate old path cache");
}

internal static void StrategicArmyCommandServiceAppliesCreatedExpeditionCommandState()
{
    WorldArmyState siteArmy = BuildCommandableArmy("army_expedition_site");
    siteArmy.Status = WorldArmyStatus.Moving;
    siteArmy.Intent = WorldArmyIntent.AssaultSite;
    siteArmy.Destination = new Vector2(48, 32);
    Dictionary<string, StrategicNavigationPath> paths = BuildCommandPaths(
        siteArmy.ArmyId,
        new Vector2(0, 0),
        new Vector2(24, 16),
        new Vector2(48, 32));

    WorldArmyCommandResult siteResult = new WorldArmyCommandService().ApplyCreatedExpeditionCommandState(
        siteArmy,
        WorldArmyIntent.AssaultSite,
        paths[siteArmy.ArmyId],
        navigationSurfaceVersion: 13,
        arrivalApproachOffset: new Vector2(4, -2),
        approachDirection: WorldSiteAttackDirection.North);

    AssertTrue(siteResult.Success, $"site expedition command metadata should succeed reason={siteResult.FailureReason}");
    AssertTrue(siteArmy.HasNavigationPath, "site expedition should apply command path");
    AssertEqual(13, siteArmy.NavigationSurfaceVersion, "site expedition navigation version");
    AssertTrue(siteArmy.HasArrivalApproachOffset, "site expedition should set arrival approach");
    AssertEqual(new Vector2(4, -2), siteArmy.ArrivalApproachOffset, "site expedition arrival approach");
    AssertEqual(WorldSiteAttackDirection.North, siteArmy.TargetApproachDirection, "site expedition approach direction");

    WorldArmyState mapArmy = BuildCommandableArmy("army_expedition_move");
    mapArmy.Status = WorldArmyStatus.Moving;
    mapArmy.Intent = WorldArmyIntent.MoveToPosition;
    mapArmy.SetNavigationPath(new[] { new Vector2(1, 1), new Vector2(2, 2) }, new Vector2(2, 2), 3);

    WorldArmyCommandResult mapResult = new WorldArmyCommandService().ApplyCreatedExpeditionCommandState(
        mapArmy,
        WorldArmyIntent.MoveToPosition,
        path: null,
        navigationSurfaceVersion: 14,
        arrivalApproachOffset: new Vector2(7, 7),
        approachDirection: WorldSiteAttackDirection.East);

    AssertTrue(mapResult.Success, $"map expedition command metadata should succeed reason={mapResult.FailureReason}");
    AssertTrue(!mapArmy.HasNavigationPath, "map expedition without a path should clear stale command path");
    AssertTrue(!mapArmy.HasArrivalApproachOffset, "map expedition should clear arrival approach");
    AssertEqual(WorldSiteAttackDirection.Any, mapArmy.TargetApproachDirection, "map expedition should clear approach direction");
}

internal static void StrategicArmyCommandServiceRemovesResolvedExpeditionCarrier()
{
    const string armyId = "strategic:expedition_resolved";
    const string expeditionId = "expedition_resolved";
    WorldArmyState army = BuildCommandableArmy(armyId);
    army.StrategicExpeditionId = expeditionId;
    army.StrategicHeroId = "hero";
    army.StrategicCorpsInstanceId = "corps";
    army.TargetSiteId = StrategicWorldIds.SiteBonefield;
    army.Intent = WorldArmyIntent.AssaultSite;
    army.Status = WorldArmyStatus.Attacking;
    army.SetNavigationPath(new[] { new Vector2(4, 4), new Vector2(8, 8) }, new Vector2(8, 8), 5);
    Dictionary<string, WorldArmyState> armies = new()
    {
        [army.ArmyId] = army
    };

    WorldArmyCommandResult result = new WorldArmyCommandService().RemoveResolvedStrategicExpeditionCarrier(
        armies,
        armyId,
        expeditionId,
        "battle_result_applied");

    AssertTrue(result.Success, $"resolved expedition carrier removal should succeed reason={result.FailureReason}");
    AssertEqual(1, result.CommandedArmyIds.Count, "removed carrier count");
    AssertTrue(!armies.ContainsKey(armyId), "resolved strategic expedition carrier should leave the world army map");
    AssertTrue(result.Events.Any(item => item.Kind == "WorldArmyStrategicExpeditionCarrierRemoved"), "removal should be observable in low-noise diagnostics");
}

internal static void StrategicReinforceCarrierArrivalBypassesLegacyGarrisonCapacity()
{
    StrategicWorldDefinition definition = new()
    {
        Id = "strategic_reinforce_capacity_test"
    };
    definition.SiteDefinitions.Add(BuildDefaultZoneDefinition(new Vector2I(0, 0)));
    definition.SiteDefinitions[0].Id = "target_site";
    StrategicWorldState state = new()
    {
        PlayerFactionId = StrategicWorldIds.FactionPlayer
    };
    state.SiteStates["target_site"] = new WorldSiteState
    {
        SiteId = "target_site",
        OwnerFactionId = StrategicWorldIds.FactionPlayer,
        ControlState = SiteControlState.PlayerHeld,
        Garrison =
        {
            new GarrisonState { UnitTypeId = "existing", Count = 1 }
        }
    };
    WorldArmyState army = BuildCommandableArmy("strategic_reinforce");
    army.StrategicExpeditionId = "expedition_reinforce";
    army.TargetSiteId = "target_site";
    army.Intent = WorldArmyIntent.ReinforceSite;
    army.Status = WorldArmyStatus.Moving;
    army.WorldPosition = new Vector2(12, 12);
    army.Destination = new Vector2(12, 12);
    army.SetNavigationPath(new[] { army.Destination }, army.Destination, 1);
    army.GarrisonUnits.Add(new GarrisonState { UnitTypeId = "strategic_company", Count = 2 });
    state.ArmyStates[army.ArmyId] = army;

    WorldArmyMovementResult result = new WorldArmyMovementService().AdvanceArmies(
        state,
        definition,
        delta: 0.1,
        navigationContext: new StaticNavigationContext(1));

    AssertTrue(result.ArrivedArmyIds.Contains(army.ArmyId), "strategic reinforce carrier should arrive even when legacy garrison zone is full");
    AssertTrue(result.GarrisonRejectedArmyIds.Count == 0, "strategic reinforce carrier must not be rejected by legacy garrison capacity");
    AssertEqual(WorldArmyStatus.Garrisoned, army.Status, "arrived strategic reinforce carrier should reach the owned target site");
    AssertEqual(1, state.SiteStates["target_site"].Garrison.Count, "strategic carrier arrival should not mutate legacy site garrison rows");
    AssertEqual(1, state.SiteStates["target_site"].Garrison[0].Count, "legacy target garrison count should stay unchanged");
}

internal static void StrategicArmyCommandServiceRejectsUncommandableArmies()
{
    WorldArmyCommandService service = new();
    WorldArmyState enemyArmy = BuildCommandableArmy("army_enemy");
    enemyArmy.OwnerFactionId = "enemy";
    Vector2 originalEnemyDestination = enemyArmy.Destination;

    WorldArmyCommandResult enemyResult = service.ApplyMoveToPosition(
        new[] { enemyArmy },
        new Vector2(64, 64),
        paths: null,
        navigationSurfaceVersion: 1,
        requiredOwnerFactionId: StrategicWorldIds.FactionPlayer);

    AssertTrue(!enemyResult.Success, "enemy army command should be rejected at Application boundary");
    AssertEqual("army_not_owned:army_enemy", enemyResult.FailureReason, "enemy command failure reason");
    AssertEqual(originalEnemyDestination, enemyArmy.Destination, "rejected enemy command should not mutate destination");

    WorldArmyState attackingArmy = BuildCommandableArmy("army_attacking");
    attackingArmy.Status = WorldArmyStatus.Attacking;
    attackingArmy.Intent = WorldArmyIntent.AssaultSite;
    Vector2 originalAttackingDestination = attackingArmy.Destination;

    WorldArmyCommandResult attackingResult = service.ApplySiteCommand(
        new[] { attackingArmy },
        targetSiteId: "site_target",
        destination: new Vector2(80, 80),
        arrivalApproachOffset: new Vector2(1, 2),
        approachDirection: WorldSiteAttackDirection.South,
        intent: WorldArmyIntent.AssaultSite,
        paths: null,
        navigationSurfaceVersion: 2,
        requiredOwnerFactionId: StrategicWorldIds.FactionPlayer);

    AssertTrue(!attackingResult.Success, "attacking army command should be rejected at Application boundary");
    AssertEqual("army_not_commandable:army_attacking:Attacking", attackingResult.FailureReason, "attacking command failure reason");
    AssertEqual(originalAttackingDestination, attackingArmy.Destination, "rejected attacking command should not mutate destination");

    WorldArmyState defeatedArmy = BuildCommandableArmy("army_defeated");
    defeatedArmy.Status = WorldArmyStatus.Defeated;
    WorldArmyCommandResult defeatedResult = service.ApplyMoveToPosition(
        new[] { defeatedArmy },
        new Vector2(96, 96),
        paths: null,
        navigationSurfaceVersion: 3,
        requiredOwnerFactionId: StrategicWorldIds.FactionPlayer);
    AssertTrue(!defeatedResult.Success, "defeated army command should be rejected at Application boundary");
    AssertEqual("army_not_commandable:army_defeated:Defeated", defeatedResult.FailureReason, "defeated command failure reason");

    WorldArmyState garrisonedArmy = BuildCommandableArmy("army_garrisoned");
    garrisonedArmy.Status = WorldArmyStatus.Garrisoned;
    WorldArmyCommandResult garrisonedResult = service.ApplyMoveToPosition(
        new[] { garrisonedArmy },
        new Vector2(112, 112),
        paths: null,
        navigationSurfaceVersion: 4,
        requiredOwnerFactionId: StrategicWorldIds.FactionPlayer);
    AssertTrue(!garrisonedResult.Success, "garrisoned army command should be rejected at Application boundary");
    AssertEqual("army_not_commandable:army_garrisoned:Garrisoned", garrisonedResult.FailureReason, "garrisoned command failure reason");
}

internal static void StrategicWorldRootDelegatesArmyCommandStateWrites()
{
    string source = ReadStrategicWorldRootSource();
    string moveBody = ExtractMethodBody(source, "private bool TryCommandSelectedArmies(Vector2 screenPosition)");
    string siteBody = ExtractMethodBody(source, "private bool TryCommandSelectedArmiesToSite(string siteId)");
    string expeditionBody = ExtractMethodBody(source, "private bool TryCreateExpedition(string targetSiteId, Vector2 destination, WorldArmyIntent intent)");
    string unsupportedBattleBody = ExtractMethodBody(source, "private bool TryEnterBattleForArrivedArmy(string armyId)");
    string resolutionBody = ExtractMethodBody(source, "private void ResolveMovingArmySiteNavigationPoints()");

    AssertTrue(
        source.Contains("_armyCommandService", StringComparison.Ordinal),
        "StrategicWorldRoot should use WorldArmyCommandService for durable army command writes");
    AssertTrue(
        moveBody.Contains("_armyCommandService.ApplyMoveToPosition", StringComparison.Ordinal),
        "selected-army map movement should delegate command-state writes");
    AssertTrue(
        siteBody.Contains("_armyCommandService.ApplySiteCommand", StringComparison.Ordinal),
        "selected-army site command should delegate command-state writes");
    AssertTrue(
        expeditionBody.Contains("_armyCommandService.ApplyCreatedExpeditionCommandState", StringComparison.Ordinal),
        "post-expedition command metadata should delegate command-state writes");
    AssertTrue(
        unsupportedBattleBody.Contains("_armyCommandService.ResetUnsupportedAssault", StringComparison.Ordinal),
        "unsupported assault recovery should delegate army-state reset");
    AssertTrue(
        resolutionBody.Contains("_armyCommandService.ApplyResolvedSiteNavigationPoints", StringComparison.Ordinal),
        "site navigation point re-resolution should delegate command-state writes");

    AssertNoArmyCommandAssignment(moveBody, "selected-army move command");
    AssertNoArmyCommandAssignment(siteBody, "selected-army site command");
    AssertNoArmyCommandAssignment(expeditionBody, "post-expedition command metadata");
    AssertNoArmyCommandAssignment(unsupportedBattleBody, "unsupported assault recovery");
    AssertNoArmyCommandAssignment(resolutionBody, "site navigation point re-resolution");
}

internal static void StrategicWorldRootSyncsStrategicExpeditionBeforeSelectedArmyCommands()
{
    string source = ReadStrategicWorldRootSource();
    string moveBody = ExtractMethodBody(source, "private bool TryCommandSelectedArmies(Vector2 screenPosition)");
    string siteBody = ExtractMethodBody(source, "private bool TryCommandSelectedArmiesToSite(string siteId)");
    string syncBody = ExtractMethodBody(source, "private bool TrySyncStrategicExpeditionCommand(");

    AssertTrue(
        source.Contains("RetargetExpedition(", StringComparison.Ordinal),
        "selected strategic army commands must submit a Strategic Management retarget command");
    AssertTrue(
        moveBody.Contains("TrySyncStrategicExpeditionCommand(", StringComparison.Ordinal) &&
        moveBody.IndexOf("TrySyncStrategicExpeditionCommand(", StringComparison.Ordinal) <
        moveBody.IndexOf("_armyCommandService.ApplyMoveToPosition", StringComparison.Ordinal),
        "map movement must sync strategic expedition intent before mutating the world-army adapter");
    AssertTrue(
        siteBody.Contains("TrySyncStrategicExpeditionCommand(", StringComparison.Ordinal) &&
        siteBody.IndexOf("TrySyncStrategicExpeditionCommand(", StringComparison.Ordinal) <
        siteBody.IndexOf("_armyCommandService.ApplySiteCommand", StringComparison.Ordinal),
        "site commands must sync strategic expedition intent before mutating the world-army adapter");
    AssertTrue(
        syncBody.Contains("StrategicManagementRuntime.Rules.GetExpeditionRetargetFailureReason", StringComparison.Ordinal) &&
        syncBody.Contains("StrategicManagementRuntime.Commands.RetargetExpedition", StringComparison.Ordinal),
        "strategic expedition sync should prevalidate and then mutate through Strategic Management command authority");
}

internal static void StrategicWorldRootUsesStrategicManagementMapControlAuthority()
{
    string source = ReadStrategicWorldRootSource();
    string siteBody = ExtractMethodBody(source, "private bool TryCommandSelectedArmiesToSite(string siteId)");
    string expeditionBody = ExtractMethodBody(source, "private bool TryIssueExpeditionToSite(string siteId)");
    string selectedBlockBody = ExtractMethodBody(source, "private bool IsSiteBlockedForSelectedSiteCommand(string siteId)");
    string expeditionBlockBody = ExtractMethodBody(source, "private bool IsSiteBlockedForExpeditionTarget(string siteId)");
    string mapDrawingBody = ExtractMethodBody(source, "private void DrawSiteIcons(Control canvas)");
    string battleSummaryBody = ExtractMethodBody(source, "private string BuildSiteBattleSummary(BattleStartRequest request)");
    string projectionBody = ExtractMethodBody(source, "private static bool TryBuildStrategicWorldMapSitePresentation(");

    AssertTrue(
        projectionBody.Contains("StrategicManagementRuntime.LocationMappings.TryResolveLocationIdForMapSite", StringComparison.Ordinal) &&
        projectionBody.Contains("StrategicManagementRuntime.ViewModels.BuildLocationMapView", StringComparison.Ordinal) &&
        projectionBody.Contains("StrategicWorldMapSitePresenter.Build", StringComparison.Ordinal),
        "large-map control projection should resolve the stable map-site mapping and consume the Strategic Management map view");
    AssertTrue(
        siteBody.Contains("target.CanReinforce", StringComparison.Ordinal) &&
        siteBody.Contains("target.CanAttack", StringComparison.Ordinal) &&
        expeditionBody.Contains("target.CanReinforce", StringComparison.Ordinal) &&
        expeditionBody.Contains("target.CanAttack", StringComparison.Ordinal),
        "selected armies and new expeditions should classify reinforce versus assault from one Strategic Management projection");
    AssertTrue(
        selectedBlockBody.Contains("target.NextCommand", StringComparison.Ordinal) &&
        expeditionBlockBody.Contains("target.NextCommand", StringComparison.Ordinal),
        "target blocked state should consume Strategic Management command eligibility");
    AssertTrue(
        mapDrawingBody.Contains("presentation.ControlColor", StringComparison.Ordinal) &&
        battleSummaryBody.Contains("presentation.ControlText", StringComparison.Ordinal) &&
        battleSummaryBody.Contains("presentation.OwnerFactionId", StringComparison.Ordinal),
        "map color and control text should consume the same Strategic Management projection as commands");
    AssertTrue(
        battleSummaryBody.Contains("site.DamageLevel", StringComparison.Ordinal) &&
        battleSummaryBody.Contains("site.Garrison", StringComparison.Ordinal),
        "legacy site damage and garrison may remain only as subordinate presentation facts");

    foreach (string body in new[] { siteBody, expeditionBody, selectedBlockBody, expeditionBlockBody, mapDrawingBody, battleSummaryBody })
    {
        AssertTrue(
            !body.Contains("site.OwnerFactionId", StringComparison.Ordinal) &&
            !body.Contains("site.ControlState", StringComparison.Ordinal) &&
            !body.Contains("GetSiteColor", StringComparison.Ordinal) &&
            !body.Contains("CanAcceptGarrison", StringComparison.Ordinal),
            "main-map control, classification, and command eligibility must not read legacy WorldSite authority");
    }
}

internal static void StrategicWorldRootDoesNotBlockStrategicOwnedTargetsByLegacyGarrisonCapacity()
{
    string source = ReadStrategicWorldRootSource();
    string expeditionTargetBlockBody = ExtractMethodBody(source, "private bool IsSiteBlockedForExpeditionTarget(string siteId)");
    string expeditionCreateBody = ExtractMethodBody(source, "private bool TryCreateExpedition(string targetSiteId, Vector2 destination, WorldArmyIntent intent)");

    AssertTrue(
        !expeditionTargetBlockBody.Contains("CanAcceptGarrison", StringComparison.Ordinal),
        "new Strategic Management expeditions to owned cities must not be target-blocked by legacy WorldSite garrison capacity");
    AssertTrue(
        !expeditionCreateBody.Contains("CanAcceptGarrison", StringComparison.Ordinal),
        "creating a Strategic Management expedition should rely on Strategic Management rules, not legacy garrison capacity");
}

internal static void StrategicWorldRootDefersBattleGateToStandbyWithoutBottomSheet()
{
    string source = ReadStrategicWorldRootSource();
    string deferBody = ExtractMethodBody(source, "private void DeferPendingBattleDecision()");
    string standbyBody = ExtractMethodBody(source, "private bool TryDeferPendingAssaultBattleToStandby(BattleStartRequest request)");

    AssertTrue(
        deferBody.Contains("TryDeferPendingAssaultBattleToStandby(_pendingBattleRequest)", StringComparison.Ordinal),
        "battle gate defer should move the arrived assault army into a standby move before clearing pending state");
    AssertTrue(
        deferBody.Contains("_selectedSiteId = \"\"", StringComparison.Ordinal) &&
        deferBody.Contains("_selectedOpportunityId = \"\"", StringComparison.Ordinal),
        "battle gate defer should clear selected location context so the bottom sheet does not reopen");
    AssertTrue(
        standbyBody.Contains("_armyCommandService.ApplyDeferredAssaultStandbyMovement", StringComparison.Ordinal) &&
        standbyBody.Contains("ResolveDeferredBattleStandbyDestination", StringComparison.Ordinal) &&
        standbyBody.Contains("TrySyncStrategicExpeditionCommand", StringComparison.Ordinal),
        "battle gate defer should use Application army command authority and strategic expedition sync for standby movement");
    AssertTrue(
        source.Contains("DeferredBattleStandbyDistance = 32.0f", StringComparison.Ordinal),
        "deferred battle standby should move only a few map steps, not a long strategic distance");
    AssertTrue(
        !deferBody.Contains("ResetUnsupportedAssault", StringComparison.Ordinal) &&
        !deferBody.Contains("CancelExpedition", StringComparison.Ordinal) &&
        !standbyBody.Contains("ResetUnsupportedAssault", StringComparison.Ordinal) &&
        !standbyBody.Contains("CancelExpedition", StringComparison.Ordinal),
        "battle gate defer should not retreat, cancel, or reset the arrived assault");
}

internal static void StrategicWorldRootDoesNotWaitForStrategicPreparationOnArrivedAssault()
{
    string source = ReadStrategicWorldRootSource();
    string battleEntryBody = ExtractMethodBody(source, "private bool TryEnterBattleForArrivedArmy(string armyId)");

    AssertTrue(
        battleEntryBody.Contains("strategicBattleBridge.CreateSession", StringComparison.Ordinal) &&
        battleEntryBody.Contains("TryEnterBattle(activeContextResult.Context, rollback)", StringComparison.Ordinal),
        "arrived strategic assault should create a bridge session and proceed to battle entry");
    AssertTrue(
        !battleEntryBody.Contains("HandleMissingStrategicBattlePreparationChoice", StringComparison.Ordinal) &&
        !source.Contains("private bool HandleMissingStrategicBattlePreparationChoice(", StringComparison.Ordinal) &&
        !source.Contains("MissingBattlePreparationChoice", StringComparison.Ordinal),
        "arrived strategic assault should no longer wait for a strategic preparation choice");
    AssertTrue(
        battleEntryBody.Contains("RollbackStrategicBattleEntry", StringComparison.Ordinal),
        "real bridge failures should use the focused identity-preserving rollback boundary");
}

internal static void StrategicWorldRootOffersDirectBattleTriggerForArrivedAssault()
{
    string source = ReadStrategicWorldRootSource();
    string arrivedBody = ExtractMethodBody(source, "private void AddArrivedAssaultChoiceButtons(WorldArmyState army)");

    AssertTrue(
        arrivedBody.Contains("触发战斗", StringComparison.Ordinal) &&
        arrivedBody.Contains("TryEnterBattleForArrivedArmy(army.ArmyId)", StringComparison.Ordinal),
        "arrived assault panel should offer a direct battle trigger button");
    AssertTrue(
        !source.Contains("AddArrivedAssaultBattlePreparationButtons", StringComparison.Ordinal) &&
        !source.Contains("SelectArrivedAssaultBattlePreparation", StringComparison.Ordinal) &&
        !source.Contains("SelectedCity.BattlePreparations", StringComparison.Ordinal) &&
        !source.Contains("StrategicManagementRuntime.Commands.SelectBattlePreparation", StringComparison.Ordinal),
        "arrived assault panel should not expose strategic battle-preparation choices");
}

internal static void StrategicBattleBridgeCreatesActiveContextBeforeSceneTransition()
{
    string source = ReadStrategicWorldRootSource();
    string body = ExtractMethodBody(source, "private bool TryEnterBattleForArrivedArmy(string armyId)");
    int createSessionIndex = body.IndexOf("strategicBattleBridge.CreateSession", StringComparison.Ordinal);
    int buildPreparationSeedIndex = body.IndexOf("_battleRequestBuilder.BuildAssaultBonefieldRequest", StringComparison.Ordinal);
    int createActiveContextIndex = body.IndexOf("strategicBattleBridge.CreateActiveContext", StringComparison.Ordinal);
    int enterBattleIndex = body.IndexOf("TryEnterBattle(activeContextResult.Context, rollback)", StringComparison.Ordinal);

    AssertTrue(
        body.Contains("StrategicManagementRuntime.State", StringComparison.Ordinal) &&
        body.Contains("army.StrategicExpeditionId", StringComparison.Ordinal),
        "arrived strategic expeditions should create bridge sessions from Strategic Management state");
    AssertTrue(
        createSessionIndex >= 0 &&
        buildPreparationSeedIndex > createSessionIndex &&
        createActiveContextIndex > buildPreparationSeedIndex &&
        enterBattleIndex > createActiveContextIndex,
        "strategic battle entry should create a bridge active context before scene transition");
    AssertTrue(
        !body.Contains("AttachSessionToLegacyRequest", StringComparison.Ordinal),
        "strategic battle entry should not attach session metadata as the active authority before transition");
}

internal static void StrategicBattleResultSkipsLegacyWorldApplier()
{
    string source = ReadWorldSiteRootSource();
    string body = ExtractMethodBody(source, "private WorldActionResult ApplyStrategicBattleResultToWorld(StrategicBattleActiveContext context)");
    string legacyWrapperBody = ExtractMethodBody(source, "private WorldActionResult ApplyBattleResultToWorld(BattleStartRequest request, BattleResult battleResult)");
    string legacyBody = ExtractMethodBody(source, "private WorldActionResult ApplyLegacyBattleResultToWorld(BattleStartRequest request, BattleResult battleResult)");
    int summaryIndex = body.IndexOf("bridge.BuildResultSummary", StringComparison.Ordinal);
    int strategicApplyIndex = body.IndexOf("StrategicManagementRuntime.CommitBattleResult", StringComparison.Ordinal);
    int legacyApplyIndex = legacyBody.IndexOf("_worldBattleResultApplier.Apply", StringComparison.Ordinal);
    int strategicNoticeIndex = body.IndexOf("BuildStrategicBattleFeedbackReturnNotice(strategicFeedback)", StringComparison.Ordinal);
    int strategicReturnIndex = strategicNoticeIndex < 0
        ? -1
        : body.IndexOf("return applyResult;", strategicNoticeIndex, StringComparison.Ordinal);

    AssertTrue(
        body.Contains("StrategicBattleBridgeService.GetActiveContextFailureReason", StringComparison.Ordinal),
        "Strategic Management battle result writeback should validate active context before applying summary");
    AssertTrue(
        summaryIndex >= 0 && strategicApplyIndex > summaryIndex,
        "Strategic Management result summary must be applied through the candidate-state commit boundary");
    AssertTrue(
        legacyWrapperBody.Contains("request?.StrategicExpeditionId", StringComparison.Ordinal) &&
        legacyWrapperBody.Contains("ApplyLegacyBattleResultToWorld", StringComparison.Ordinal),
        "legacy BattleResult entry should reject Strategic Management requests before old world applier compatibility");
    AssertTrue(
        legacyApplyIndex >= 0,
        "legacy world result applier may remain only for non-Strategic battle compatibility in this slice");
    AssertTrue(
        !body.Contains("_worldBattleResultApplier.Apply", StringComparison.Ordinal) &&
        strategicNoticeIndex > strategicApplyIndex &&
        strategicReturnIndex > strategicNoticeIndex,
        "Strategic Management battle result branch should not call the legacy world-state result applier");
}

internal static void StrategicBattleResultKeepsPresentationCleanupOnly()
{
    string source = ReadWorldSiteRootSource();
    string body = ExtractMethodBody(source, "private WorldActionResult ApplyStrategicBattleResultToWorld(StrategicBattleActiveContext context)");
    int strategicApplyIndex = body.IndexOf("StrategicManagementRuntime.CommitBattleResult", StringComparison.Ordinal);
    int cleanupIndex = body.IndexOf("ApplyStrategicBattleResultPresentationCleanup(context.CompatibilityRequest, applyResult, summary)", StringComparison.Ordinal);
    int carrierCleanupIndex = body.IndexOf("ApplyStrategicBattleResultWorldArmyCarrierCleanup(context.CompatibilityRequest, applyResult)", StringComparison.Ordinal);
    int strategicNoticeIndex = body.IndexOf("BuildStrategicBattleFeedbackReturnNotice(strategicFeedback)", StringComparison.Ordinal);
    int strategicReturnIndex = strategicNoticeIndex < 0
        ? -1
        : body.IndexOf("return applyResult;", strategicNoticeIndex, StringComparison.Ordinal);
    string cleanupBody = ExtractMethodBody(source, "private void ApplyStrategicBattleResultPresentationCleanup(BattleStartRequest request, WorldActionResult result, StrategicBattleResultSummary summary)");
    string carrierCleanupBody = ExtractMethodBody(source, "private void ApplyStrategicBattleResultWorldArmyCarrierCleanup(BattleStartRequest request, WorldActionResult result)");

    AssertTrue(
        cleanupIndex > strategicApplyIndex &&
        cleanupIndex < strategicReturnIndex,
        "Strategic Management battle result branch should run presentation cleanup before returning and before legacy result settlement");
    AssertTrue(
        carrierCleanupIndex > strategicApplyIndex &&
        carrierCleanupIndex < strategicReturnIndex,
        "Strategic Management battle result branch should retire the world army carrier after Strategic Management writeback succeeds");
    AssertTrue(
        cleanupBody.Contains("WorldSiteUnitPlacementKind.VisitingArmy", StringComparison.Ordinal) &&
        cleanupBody.Contains("WorldSiteUnitPlacementKind.Attacker", StringComparison.Ordinal) &&
        cleanupBody.Contains("SourceKind", StringComparison.Ordinal) &&
        cleanupBody.Contains("\"PlayerArmy\"", StringComparison.Ordinal),
        "presentation cleanup should remove only resolved legacy player-army attacker/visiting placements");
    AssertTrue(
        cleanupBody.Contains("summary.Outcome == BattleOutcome.Victory", StringComparison.Ordinal) &&
        cleanupBody.Contains("summary.ObjectiveSucceeded", StringComparison.Ordinal) &&
        cleanupBody.Contains("WorldSiteUnitPlacementKind.Defender", StringComparison.Ordinal) &&
        cleanupBody.Contains("WorldSiteUnitPlacementKind.Garrison", StringComparison.Ordinal),
        "strategic victory should also clear stale legacy defender/garrison placements from site presentation cache after Strategic Management writeback");
    AssertTrue(
        cleanupBody.Contains("EnterAftermath", StringComparison.Ordinal) ||
        cleanupBody.Contains("EnterPeacetime", StringComparison.Ordinal),
        "presentation cleanup should exit legacy Wartime site mode without running legacy strategic settlement");

    string[] forbidden =
    {
        "_worldBattleResultApplier",
        "WorldTickService",
        "AdvanceWorldTick",
        "OwnerFactionId",
        "ControlState",
        "site.Garrison",
        "ArmyStates"
    };
    foreach (string fragment in forbidden)
    {
        AssertTrue(
            !cleanupBody.Contains(fragment, StringComparison.Ordinal),
            $"presentation cleanup should not mutate old strategic authority fragment={fragment}");
    }

    AssertTrue(
        carrierCleanupBody.Contains("_armyCommandService.RemoveResolvedStrategicExpeditionCarrier", StringComparison.Ordinal),
        "world army carrier cleanup should route through the Application command boundary");
    AssertTrue(
        !carrierCleanupBody.Contains(".Remove(", StringComparison.Ordinal),
        "world army carrier cleanup should not directly remove armies from Presentation");
}

internal static void StrategicBattleResultClearsStaleLegacyHandoff()
{
    string source = ReadWorldSiteRootSource();
    string applyBody = ExtractMethodBody(source, "private WorldActionResult ApplyStrategicBattleResultToWorld(StrategicBattleActiveContext context)");
    string cleanupBody = ExtractMethodBody(source, "private static void ClearLegacyStrategicBattleHandoff(");
    int activeContextClearIndex = applyBody.IndexOf("StrategicManagementRuntime.CommitBattleResult", StringComparison.Ordinal);
    int legacyCleanupIndex = applyBody.IndexOf("ClearLegacyStrategicBattleHandoff(\"result_consumed\")", StringComparison.Ordinal);

    AssertTrue(
        activeContextClearIndex >= 0 &&
        legacyCleanupIndex > activeContextClearIndex,
        "strategic battle result should clear stale legacy handoff only after the commit boundary consumes the active context");
    AssertTrue(
        cleanupBody.Contains("BattleSessionHandoff.HasActiveLaunch", StringComparison.Ordinal) &&
        cleanupBody.Contains("BattleSessionHandoff.CancelBattle()", StringComparison.Ordinal) &&
        cleanupBody.Contains("StaleLegacyStrategicBattleHandoffCleared", StringComparison.Ordinal),
        "legacy handoff cleanup should be explicit, logged, and scoped to stale strategic battle state");
}

internal static void StrategicBattleResultPersistsStrategicStateBeforeFeedback()
{
    string source = ReadWorldSiteRootSource();
    string applyBody = ExtractMethodBody(source, "private WorldActionResult ApplyStrategicBattleResultToWorld(StrategicBattleActiveContext context)");
    int strategicApplyIndex = applyBody.IndexOf("StrategicManagementRuntime.CommitBattleResult", StringComparison.Ordinal);
    int rejectedReturnIndex = applyBody.IndexOf("if (!commitResult.Success", StringComparison.Ordinal);
    int feedbackIndex = applyBody.IndexOf("BuildStrategicBattleFeedbackReturnNotice(strategicFeedback)", StringComparison.Ordinal);

    AssertTrue(
        strategicApplyIndex >= 0 &&
        rejectedReturnIndex > strategicApplyIndex &&
        feedbackIndex > rejectedReturnIndex,
        "strategic battle result writeback should durably commit and publish Strategic Management consequences before composing player feedback");
}

internal static void WorldSiteRootRejectsStaleLegacyStrategicHandoff()
{
    string source = ReadWorldSiteRootSource();
    string resolveBody = ExtractMethodBody(source, "private bool TryResolveActiveBattleRequest(out BattleStartRequest request)");
    string legacyBody = ExtractMethodBody(source, "private static bool TryPeekLegacyNonStrategicBattleRequest(");

    AssertTrue(
        resolveBody.Contains("TryPeekLegacyNonStrategicBattleRequest(out request)", StringComparison.Ordinal),
        "WorldSiteRoot should resolve non-Strategic compatibility requests through a stale-state guard");
    AssertTrue(
        !resolveBody.Contains("return BattleSessionHandoff.TryPeekActiveRequest(out request);", StringComparison.Ordinal),
        "WorldSiteRoot must not directly fall back to the legacy handoff because stale strategic requests reopen old battles");
    AssertTrue(
        legacyBody.Contains("BattleSessionHandoff.TryPeekActiveRequest(out request)", StringComparison.Ordinal) &&
        legacyBody.Contains("IsStrategicBattleRequest(request)", StringComparison.Ordinal) &&
        legacyBody.Contains("ClearLegacyStrategicBattleHandoff(\"stale_strategic_legacy_request\")", StringComparison.Ordinal) &&
        legacyBody.Contains("return false;", StringComparison.Ordinal),
        "legacy handoff guard should clear and reject strategic requests when no active strategic context exists");
}

internal static void StrategicBattlePostResultShowsSettlementModalBeforeRouting()
{
    string source = ReadWorldSiteRootSource();
    string runtimeBody = ExtractMethodBody(source, "private async Task PlayBattleGroupRuntimeAndApplyResultAsync(");
    int clearEntitiesIndex = runtimeBody.IndexOf("ClearBattleEntities()", StringComparison.Ordinal);
    int modalIndex = runtimeBody.IndexOf("ShowPostBattleSettlementDialog(", StringComparison.Ordinal);
    int directReturnIndex = runtimeBody.IndexOf("ReturnToReturnScene(", StringComparison.Ordinal);

    AssertTrue(
        clearEntitiesIndex >= 0 &&
        modalIndex > clearEntitiesIndex,
        "battle runtime completion should clear runtime presentation and then show a settled post-battle modal before any player-facing route");
    AssertTrue(
        directReturnIndex < 0 || directReturnIndex > modalIndex,
        "battle runtime completion must not directly return to the world map before the player acknowledges the settled result modal");
    AssertTrue(
        !source.Contains("private bool ShouldReturnToWorldMapAfterBattle(", StringComparison.Ordinal),
        "post-battle route should no longer be decided by an immediate outcome gate; the settlement modal owns player acknowledgement actions");
}

internal static void StrategicBattleSettlementModalIsResourceBackedAndActionScoped()
{
    string root = ProjectRoot();
    string factorySource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "Common", "GameUiSceneFactory.cs"));
    string siteHudSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "World", "Sites", "WorldSiteRoot.SiteManagementHud.cs"));
    string rootSource = ReadWorldSiteRootSource();
    string dialogPath = Path.Combine(root, "scenes", "world", "ui", "PostBattleSettlementDialog.tscn");
    AssertTrue(File.Exists(dialogPath), $"post-battle settlement dialog should be an authored Godot scene path={dialogPath}");

    string dialogScene = File.ReadAllText(dialogPath);
    string scriptPath = Path.Combine(root, "src", "Presentation", "World", "Sites", "PostBattleSettlementDialog.cs");
    AssertTrue(File.Exists(scriptPath), $"post-battle settlement dialog should use a focused script path={scriptPath}");
    string dialogScript = File.ReadAllText(scriptPath);
    string manageButtonBlock = ExtractSceneNodeBlock(dialogScene, "[node name=\"ManageCityButton\"");
    string returnButtonBlock = ExtractSceneNodeBlock(dialogScene, "[node name=\"ReturnButton\"");
    string buildHudBody = ExtractMethodBody(siteHudSource, "private void BuildSiteHud()");
    string showDialogBody = ExtractMethodBody(siteHudSource, "private void ShowPostBattleSettlementDialog(");
    string managePressedBody = ExtractMethodBody(siteHudSource, "private void OnPostBattleSettlementManageCityPressed()");
    string returnPressedBody = ExtractMethodBody(siteHudSource, "private void OnPostBattleSettlementReturnPressed()");
    string resolveViewportBody = ExtractMethodBody(rootSource, "private Rect2 ResolveMainWorldViewportRect()");

    AssertTrue(
        factorySource.Contains("PostBattleSettlementDialogScenePath", StringComparison.Ordinal) &&
        factorySource.Contains("CreatePostBattleSettlementDialog", StringComparison.Ordinal),
        "post-battle settlement dialog should be loaded through GameUiSceneFactory");
    AssertTrue(
        buildHudBody.Contains("CreatePostBattleSettlementDialog", StringComparison.Ordinal) &&
        buildHudBody.Contains("_postBattleSettlementDialog", StringComparison.Ordinal),
        "WorldSiteRoot should instantiate the settlement dialog once under the site ModalHost");
    AssertTrue(
        dialogScene.Contains("PostBattleSettlementDialog.cs", StringComparison.Ordinal) &&
        dialogScene.Contains("[node name=\"Backdrop\"", StringComparison.Ordinal) &&
        dialogScene.Contains("[node name=\"ResultBody\"", StringComparison.Ordinal) &&
        manageButtonBlock.Contains("text = \"管理城池\"", StringComparison.Ordinal) &&
        returnButtonBlock.Contains("text = \"返回\"", StringComparison.Ordinal),
        "post-battle settlement dialog should show Chinese result text and explicit manage/return actions");
    AssertTrue(
        dialogScript.Contains("public event System.Action ManageCityPressed", StringComparison.Ordinal) &&
        dialogScript.Contains("public event System.Action ReturnPressed", StringComparison.Ordinal) &&
        dialogScript.Contains("ManageCityAvailable", StringComparison.Ordinal),
        "settlement dialog script should expose action events and gate the manage-city button");
    AssertTrue(
        rootSource.Contains("CanOpenManagedCityDetail(siteId)", StringComparison.Ordinal) &&
        rootSource.Contains("ManageCityAvailable", StringComparison.Ordinal) &&
        rootSource.Contains("SwitchToNonBattleUi(", StringComparison.Ordinal) &&
        rootSource.Contains("ReturnToReturnScene(", StringComparison.Ordinal),
        "settlement modal actions should return to the world map or open management only when the settled target is a managed player city");
    AssertTrue(
        rootSource.Contains("_postBattleSettlementDialogOpen", StringComparison.Ordinal) &&
        showDialogBody.Contains("_postBattleSettlementDialogOpen = true;", StringComparison.Ordinal) &&
        managePressedBody.Contains("_postBattleSettlementDialogOpen = false;", StringComparison.Ordinal) &&
        returnPressedBody.Contains("_postBattleSettlementDialogOpen = false;", StringComparison.Ordinal),
        "post-battle settlement modal should own an explicit open flag that is cleared by both acknowledgement actions");
    AssertTrue(
        !rootSource.Contains("ShouldReserveSiteHudWorkspace", StringComparison.Ordinal) &&
        !rootSource.Contains("ResolveWorldSiteHudViewportRect", StringComparison.Ordinal) &&
        !resolveViewportBody.Contains("_postBattleSettlementDialogOpen", StringComparison.Ordinal),
        "post-battle settlement modal should not participate in viewport reservation because entered-site management overlays the fullscreen map");
}

internal static void BattleGroupProbePreservesStrategicForceIdentity()
{
    string root = ProjectRoot();
    string source = File.ReadAllText(Path.Combine(root, "src", "Application", "Battle", "BattleGroupSessionProbeService.cs"));

    AssertTrue(
        source.Contains("force.StrategicHeroId", StringComparison.Ordinal) &&
        source.Contains("force.StrategicCorpsInstanceId", StringComparison.Ordinal) &&
        source.Contains("force.StrategicSourceLocationId", StringComparison.Ordinal),
        "legacy request snapshot probes should copy strategic hero, corps, and source-location identity when bridge metadata is present");
    AssertTrue(
        source.Contains("force.StrategicPreBattleCorpsStrength", StringComparison.Ordinal),
        "legacy request snapshot probes should preserve pre-battle corps strength for strategic participants");
}

private static WorldArmyState BuildCommandableArmy(string armyId)
{
    WorldArmyState army = new()
    {
        ArmyId = armyId,
        OwnerFactionId = StrategicWorldIds.FactionPlayer,
        SourceSiteId = StrategicWorldIds.SitePlayerCamp,
        TargetSiteId = "old_target",
        Intent = WorldArmyIntent.AssaultSite,
        Status = WorldArmyStatus.Idle
    };
    army.WorldPosition = new Vector2(0, 0);
    army.Destination = new Vector2(1, 1);
    army.SetArrivalApproachOffset(new Vector2(1, 0));
    army.SetTargetApproachDirection(WorldSiteAttackDirection.West);
    return army;
}

private static Dictionary<string, StrategicNavigationPath> BuildCommandPaths(
    string armyId,
    params Vector2[] points)
{
    StrategicNavigationPath path = new()
    {
        ProviderId = "test"
    };
    path.Points.AddRange(points);
    return new Dictionary<string, StrategicNavigationPath>
    {
        [armyId] = path
    };
}

private static void AssertNoArmyCommandAssignment(string methodBody, string context)
{
    string[] forbidden =
    {
        ".TargetSiteId =",
        ".Destination =",
        ".WorldPosition =",
        ".Intent =",
        ".Status =",
        ".SetArrivalApproachOffset(",
        ".ClearArrivalApproachOffset(",
        ".SetTargetApproachDirection(",
        ".ClearTargetApproachDirection(",
        ".SetNavigationPath(",
        ".ClearNavigationPath("
    };

    foreach (string fragment in forbidden)
    {
        AssertTrue(
            !methodBody.Contains(fragment, StringComparison.Ordinal),
            $"{context} should not directly mutate army command state fragment={fragment}");
    }
}

private sealed class StaticNavigationContext : IStrategicNavigationContext
{
    public StaticNavigationContext(int version)
    {
        Version = version;
    }

    public int Version { get; }
    public string PrimaryProviderId => "static_test";

    public bool IsSynchronized(out string failureReason)
    {
        failureReason = "";
        return true;
    }

    public bool IsPointNavigable(Vector2 mapPoint, out string failureReason)
    {
        failureReason = "";
        return true;
    }

    public bool TryGetNearestNavigablePoint(Vector2 mapPoint, int maxCellRadius, out Vector2 navigablePoint, out string failureReason)
    {
        navigablePoint = mapPoint;
        failureReason = "";
        return true;
    }

    public bool TryGetNearestReachableNavigablePoint(
        Vector2 start,
        Vector2 preferredPoint,
        int maxCellRadius,
        out Vector2 navigablePoint,
        out StrategicNavigationPath path,
        out string failureReason)
    {
        navigablePoint = preferredPoint;
        path = BuildPath(start, preferredPoint);
        failureReason = "";
        return true;
    }

    public bool TryBuildPath(Vector2 start, Vector2 destination, out StrategicNavigationPath path, out string failureReason)
    {
        path = BuildPath(start, destination);
        failureReason = "";
        return true;
    }

    private StrategicNavigationPath BuildPath(Vector2 start, Vector2 destination)
    {
        StrategicNavigationPath path = new()
        {
            ProviderId = PrimaryProviderId
        };
        path.Points.Add(start);
        path.Points.Add(destination);
        return path;
    }
}
}
