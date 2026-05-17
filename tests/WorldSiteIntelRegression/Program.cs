using Godot;
using System.IO;
using Rpg.Application.Battle;
using Rpg.Application.World;
using Rpg.Definitions.World;
using Rpg.Domain.World;

System.Environment.SetEnvironmentVariable("RPG_GAMELOG_DIR", Path.Combine(Path.GetTempPath(), "rpg-headless-tests"));

Run("transparent site exposes tactical layout", TransparentSiteExposesTacticalLayout);
Run("partial site exposes strategy but hides tactical details", PartialSiteHidesTacticalDetails);
Run("obscured site reports concealment reason", ObscuredSiteReportsConcealmentReason);
Run("snapshot and stale view are built by intel service", SnapshotAndStaleViewAreBuiltByIntelService);
Run("site memory disables obscuration", SiteMemoryDisablesObscuration);
Run("transparent site active obscuration hides tactical layout", TransparentSiteActiveObscurationHidesTacticalLayout);
Run("missing intel definition does not default to transparent tactical layout", MissingIntelDefinitionDoesNotDefaultToTransparentTacticalLayout);
Run("unknown snapshot view does not leak intel", UnknownSnapshotViewDoesNotLeakIntel);
Run("exploration action writes site memory", ExplorationActionWritesSiteMemory);
Run("consuming exploration action advances world tick and writes memory", ConsumingExplorationActionAdvancesWorldTickAndWritesMemory);
Run("strategic V1 sites author explicit intel policies", StrategicV1SitesAuthorExplicitIntelPolicies);
Run("bonefield is partial intel with public entrance", BonefieldIsPartialIntelWithPublicEntrance);
Run("strategic detail garrison list is gated by tactical layout intel", StrategicDetailGarrisonListIsGatedByTacticalLayoutIntel);
Run("battle request carries structured site intel", BattleRequestCarriesStructuredSiteIntel);
Run("bonefield assault request hides hidden entrance until memory reveals it", BonefieldAssaultRequestHidesHiddenEntranceUntilMemoryRevealsIt);
Run("bonefield watch post action reveals east entrance in battle request", BonefieldWatchPostActionRevealsEastEntranceInBattleRequest);
Run("exploration battle request receives structured site intel", ExplorationBattleRequestReceivesStructuredSiteIntel);
Run("world site root applies intel to exploration battle request", WorldSiteRootAppliesIntelToExplorationBattleRequest);
Run("world site root applies exploration actions through world context", WorldSiteRootAppliesExplorationActionsThroughWorldContext);
Run("world site root exploration placement uses known player entrances", WorldSiteRootExplorationPlacementUsesKnownPlayerEntrances);
Run("world site root exploration readiness has no patrol route fallback", WorldSiteRootExplorationReadinessHasNoPatrolRouteFallback);
Run("world site root exploration current cell copy requires known entrance placement", WorldSiteRootExplorationCurrentCellCopyRequiresKnownEntrancePlacement);
Run("world site root executes StartsBattle exploration actions through battle handoff", WorldSiteRootExecutesStartsBattleExplorationActionsThroughBattleHandoff);
Run("strategic direct site entry requires tactical layout intel", StrategicDirectSiteEntryRequiresTacticalLayoutIntel);

static void TransparentSiteExposesTacticalLayout()
{
    StrategicWorldDefinition definition = BuildDefinition(WorldSiteIntelPolicy.Transparent);
    StrategicWorldState state = BuildState();

    WorldSiteIntelViewModel view = WorldSiteIntelService.BuildCurrentView(
        state,
        definition,
        "site_under_test",
        WorldIntelVisibility.Visible);

    AssertEqual(WorldSiteIntelPolicy.Transparent, view.Policy, "policy");
    AssertTrue(view.CanInspectSiteMap, "transparent site should allow direct map inspection");
    AssertTrue(view.CanInspectFullTacticalLayout, "transparent site should show tactical layout");
    AssertTrue(view.CanInspectStrategicSummary, "transparent site should show strategic summary");
    AssertTrue(view.KnownEntranceIds.Contains("front_gate"), "front gate should be known");
    AssertTrue(view.KnownEntranceIds.Contains("side_gate"), "side gate should be known");
    AssertEqual(0, view.UnknownIntelReasons.Count, "transparent site should not report hidden tactical reasons");
}

static void PartialSiteHidesTacticalDetails()
{
    StrategicWorldDefinition definition = BuildDefinition(WorldSiteIntelPolicy.Partial);
    StrategicWorldState state = BuildState();

    WorldSiteIntelViewModel view = WorldSiteIntelService.BuildCurrentView(
        state,
        definition,
        "site_under_test",
        WorldIntelVisibility.Visible);

    AssertEqual(WorldSiteIntelPolicy.Partial, view.Policy, "policy");
    AssertTrue(view.CanInspectStrategicSummary, "partial site should show strategic summary");
    AssertTrue(view.CanInspectSiteMap, "partial site should allow map inspection");
    AssertTrue(!view.CanInspectFullTacticalLayout, "partial site should hide full tactical layout");
    AssertTrue(view.KnownEntranceIds.Contains("front_gate"), "public entrance should be known");
    AssertTrue(!view.KnownEntranceIds.Contains("side_gate"), "non-public entrance should remain unknown");
    AssertTrue(
        view.UnknownIntelReasons.Any(item => item.Contains("Hidden tactical layout unknown.", StringComparison.Ordinal)),
        "partial site should name hidden tactical detail");
}

static void ObscuredSiteReportsConcealmentReason()
{
    StrategicWorldDefinition definition = BuildDefinition(WorldSiteIntelPolicy.Obscured);
    StrategicWorldState state = BuildState();

    WorldSiteIntelViewModel view = WorldSiteIntelService.BuildCurrentView(
        state,
        definition,
        "site_under_test",
        WorldIntelVisibility.Visible);

    AssertEqual(WorldSiteIntelPolicy.Obscured, view.Policy, "policy");
    AssertTrue(!view.CanInspectFullTacticalLayout, "obscured site should hide tactical layout");
    AssertTrue(view.ActiveObscurationSourceIds.Contains("grave_fog"), "obscuration id should be structured");
    AssertTrue(
        view.UnknownIntelReasons.Any(item => item.Contains("Grave Fog", StringComparison.Ordinal)),
        "obscured site should expose the concealment source");
    AssertTrue(view.AvailableApproaches.Any(item => item.ActionId == "enter_exploration"), "obscured site should offer exploration");
}

static void SnapshotAndStaleViewAreBuiltByIntelService()
{
    StrategicWorldDefinition definition = BuildDefinition(WorldSiteIntelPolicy.Partial);
    StrategicWorldState state = BuildState();
    WorldSiteDefinition siteDefinition = definition.SiteDefinitions[0];
    WorldSiteState siteState = state.SiteStates["site_under_test"];

    WorldSiteIntelSnapshot snapshot = WorldSiteIntelService.BuildSnapshot(siteDefinition, siteState, worldTick: 7);
    WorldSiteIntelViewModel stale = WorldSiteIntelService.BuildViewFromSnapshot(snapshot, WorldIntelVisibility.Revealed);

    AssertEqual("site_under_test", snapshot.SiteId, "snapshot site id");
    AssertEqual(7, snapshot.LastSeenWorldTick, "snapshot tick");
    AssertEqual(WorldIntelVisibility.Revealed, stale.Visibility, "stale view visibility");
    AssertTrue(stale.IsStale, "revealed snapshot should be marked stale");
    AssertTrue(stale.KnownEntranceIds.Contains("front_gate"), "public entrance should remain known in stale view");
}

static void SiteMemoryDisablesObscuration()
{
    StrategicWorldDefinition definition = BuildDefinition(WorldSiteIntelPolicy.Obscured);
    StrategicWorldState state = BuildState();
    state.SiteStates["site_under_test"].Memory.ResolvedPointIds.Add("disable_fog_point");

    WorldSiteIntelViewModel view = WorldSiteIntelService.BuildCurrentView(
        state,
        definition,
        "site_under_test",
        WorldIntelVisibility.Visible);

    AssertTrue(view.CanInspectFullTacticalLayout, "resolved point should disable authored obscuration");
    AssertEqual(0, view.ActiveObscurationSourceIds.Count, "disabled obscuration should not remain active");
}

static void TransparentSiteActiveObscurationHidesTacticalLayout()
{
    StrategicWorldDefinition definition = BuildDefinition(WorldSiteIntelPolicy.Transparent);
    StrategicWorldState state = BuildState();
    definition.SiteDefinitions[0].Intel.ObscurationSources[0].HidesTacticalLayout = true;

    WorldSiteIntelViewModel view = WorldSiteIntelService.BuildCurrentView(
        state,
        definition,
        "site_under_test",
        WorldIntelVisibility.Visible);

    AssertTrue(!view.CanInspectFullTacticalLayout, "active obscuration should hide transparent tactical layout");
    AssertTrue(view.ActiveObscurationSourceIds.Contains("grave_fog"), "transparent active obscuration should be listed");
    AssertTrue(!view.KnownEntranceIds.Contains("side_gate"), "hidden tactical layout should not expose non-public entrance");
}

static void UnknownSnapshotViewDoesNotLeakIntel()
{
    WorldSiteIntelSnapshot snapshot = new()
    {
        SiteId = "site_under_test",
        DisplayName = "known display",
        IntelPolicy = WorldSiteIntelPolicy.Partial.ToString(),
        StrategicSummary = "secret strategic summary",
        TacticalSummary = "secret tactical summary",
        HiddenTacticalSummary = "secret hidden summary",
        KnownEntranceIds = { "front_gate" },
        KnownExplorationPointIds = { "point_a" },
        UnknownIntelReasons = { "old hidden reason" },
        ActiveObscurationSourceIds = { "grave_fog" },
        KnownTacticalTags = { "tag_a" },
        ExplorationAdvantageTags = { "advantage_a" }
    };

    WorldSiteIntelViewModel view = WorldSiteIntelService.BuildViewFromSnapshot(
        snapshot,
        WorldIntelVisibility.Unknown);

    AssertEqual("site_under_test", view.SiteId, "unknown snapshot site id");
    AssertEqual("known display", view.DisplayName, "unknown snapshot display name");
    AssertEqual(WorldSiteIntelPolicy.Partial, view.Policy, "unknown snapshot policy");
    AssertTrue(!view.CanInspectStrategicSummary, "unknown snapshot should not allow strategic inspection");
    AssertTrue(!view.CanInspectSiteMap, "unknown snapshot should not allow map inspection");
    AssertTrue(!view.CanInspectFullTacticalLayout, "unknown snapshot should not allow tactical inspection");
    AssertEqual("", view.StrategicSummary, "unknown snapshot should not leak strategic summary");
    AssertEqual("", view.TacticalSummary, "unknown snapshot should not leak tactical summary");
    AssertEqual("", view.HiddenTacticalSummary, "unknown snapshot should not leak hidden tactical summary");
    AssertEqual(0, view.KnownEntranceIds.Count, "unknown snapshot should not leak known entrances");
    AssertEqual(0, view.KnownExplorationPointIds.Count, "unknown snapshot should not leak known points");
    AssertEqual(1, view.UnknownIntelReasons.Count, "unknown snapshot should only expose current unknown reason");
    AssertTrue(!view.UnknownIntelReasons.Contains("old hidden reason"), "unknown snapshot should not leak old hidden reasons");
    AssertEqual(0, view.ActiveObscurationSourceIds.Count, "unknown snapshot should not leak active obscuration ids");
    AssertEqual(0, view.KnownTacticalTags.Count, "unknown snapshot should not leak tactical tags");
    AssertEqual(0, view.ExplorationAdvantageTags.Count, "unknown snapshot should not leak advantage tags");
}

static void MissingIntelDefinitionDoesNotDefaultToTransparentTacticalLayout()
{
    StrategicWorldDefinition definition = BuildDefinition(WorldSiteIntelPolicy.Transparent);
    definition.SiteDefinitions[0] = new WorldSiteDefinition
    {
        Id = "site_under_test",
        DisplayName = "Test Site",
        SiteKind = WorldSiteKind.ResourceSite,
        Description = "Test site for missing intel.",
        MapPosition = new Vector2(10, 20),
        InitialOwnerFactionId = "enemy",
        InitialControlState = SiteControlState.Hostile,
        EntranceDefinitions =
        {
            new BattleEntranceDefinition
            {
                EntranceId = "front_gate",
                DisplayName = "Front Gate",
                FactionId = "player"
            },
            new BattleEntranceDefinition
            {
                EntranceId = "side_gate",
                DisplayName = "Side Gate",
                FactionId = "player"
            }
        }
    };
    AssertTrue(definition.SiteDefinitions[0].Intel == null, "default WorldSiteDefinition intel should be missing until authored");
    StrategicWorldState state = BuildState();
    state.SiteStates["site_under_test"].Garrison.Add(new GarrisonState
    {
        UnitTypeId = "hidden_unit",
        Count = 3
    });

    WorldSiteIntelViewModel view = WorldSiteIntelService.BuildCurrentView(
        state,
        definition,
        "site_under_test",
        WorldIntelVisibility.Visible);

    AssertTrue(!view.CanInspectFullTacticalLayout, "missing intel definition should not expose tactical layout");
    AssertTrue(!view.KnownEntranceIds.Contains("side_gate"), "missing intel definition should not leak hidden entrances");
    AssertTrue(
        view.UnknownIntelReasons.Any(item => item.Contains("missing_intel_definition", StringComparison.Ordinal)),
        "missing intel definition should report an explicit missing-intel reason");
}

static void ExplorationActionWritesSiteMemory()
{
    WorldSiteState site = new()
    {
        SiteId = "site_under_test",
        Exploration = new WorldSiteExplorationState(),
        Memory = new WorldSiteMemoryState()
    };

    SiteExplorationActionDefinition action = new()
    {
        Id = "clear_collapsed_mine",
        ResolvesPoint = true,
        RevealsEntranceIds = new[] { "side_path" },
        UnlocksFacilitySlotIds = new[] { "mine_slot_01" },
        ClearsHazardIds = new[] { "collapsed_mine" },
        AddsKnownTacticalTags = new[] { "side_path_available" },
        AddsExplorationAdvantageTags = new[] { "watch_post_scouted" }
    };

    WorldSiteExplorationService.ApplyActionResult(site, "collapsed_mine_point", action);

    AssertTrue(site.Memory.RevealedEntranceIds.Contains("side_path"), "entrance should reveal");
    AssertTrue(site.Memory.UnlockedFacilitySlotIds.Contains("mine_slot_01"), "facility slot should unlock");
    AssertTrue(site.Memory.ClearedHazardIds.Contains("collapsed_mine"), "hazard should clear");
    AssertTrue(site.Memory.KnownTacticalTags.Contains("side_path_available"), "tactical tag should be known");
    AssertTrue(site.Memory.ExplorationAdvantageTags.Contains("watch_post_scouted"), "advantage tag should be recorded");
    AssertTrue(site.Memory.ResolvedPointIds.Contains("collapsed_mine_point"), "resolved point should enter site memory");
}

static void ConsumingExplorationActionAdvancesWorldTickAndWritesMemory()
{
    StrategicWorldDefinition definition = BuildDefinition(WorldSiteIntelPolicy.Partial);
    StrategicWorldState state = BuildState();
    state.WorldTick = 12;
    WorldSiteState site = state.SiteStates["site_under_test"];

    SiteExplorationActionDefinition action = new()
    {
        Id = "clear_collapsed_mine",
        DisplayName = "Clear Collapsed Mine",
        ConsumesWorldTick = true,
        ResolvesPoint = true,
        RevealsEntranceIds = new[] { "side_gate" },
        ClearsHazardIds = new[] { "collapsed_mine" }
    };

    WorldActionResult result = WorldSiteExplorationService.ApplyActionResult(
        state,
        definition,
        site,
        "collapsed_mine_point",
        action);

    AssertEqual(true, result.Success, "consuming exploration action should succeed");
    AssertEqual(13, state.WorldTick, "consuming exploration action should advance one world tick");
    AssertTrue(site.Memory.ResolvedPointIds.Contains("collapsed_mine_point"), "resolved point should still enter site memory");
    AssertTrue(site.Memory.RevealedEntranceIds.Contains("side_gate"), "revealed entrance should still enter site memory");
    AssertTrue(site.Memory.ClearedHazardIds.Contains("collapsed_mine"), "cleared hazard should still enter site memory");
    AssertTrue(
        result.Message.Contains("Clear Collapsed Mine", StringComparison.Ordinal) ||
        result.Message.Contains("clear_collapsed_mine", StringComparison.Ordinal),
        "result message should identify the applied exploration action");
    AssertTrue(
        result.Events.Any(item => item.Kind == "SiteExplorationActionApplied"),
        "result events should record the exploration action application");
    AssertTrue(
        result.Events.Any(item => item.Kind == "WorldTickAdvanced" && item.Tick == 13),
        "result events should preserve world tick feedback");
}

static void StrategicV1SitesAuthorExplicitIntelPolicies()
{
    StrategicWorldDefinition definition = StrategicWorldV1DefinitionFactory.Create(loadInitialStateResource: false);
    foreach (WorldSiteDefinition site in definition.SiteDefinitions)
    {
        AssertTrue(site.Intel != null, $"V1 site should explicitly author intel: {site.Id}");
    }

    AssertEqual(
        WorldSiteIntelPolicy.Transparent,
        definition.SiteDefinitions.Single(site => site.Id == StrategicWorldIds.SitePlayerCamp).Intel.Policy,
        "player camp intel policy");
    AssertEqual(
        WorldSiteIntelPolicy.Transparent,
        definition.SiteDefinitions.Single(site => site.Id == StrategicWorldIds.SiteGraveyard).Intel.Policy,
        "graveyard intel policy");
    AssertEqual(
        WorldSiteIntelPolicy.Partial,
        definition.SiteDefinitions.Single(site => site.Id == StrategicWorldIds.SiteBonefield).Intel.Policy,
        "bonefield intel policy");
}

static void BonefieldIsPartialIntelWithPublicEntrance()
{
    StrategicWorldDefinition definition = StrategicWorldV1DefinitionFactory.Create(loadInitialStateResource: false);
    System.Environment.SetEnvironmentVariable("RPG_GAMELOG_DIR", Path.Combine(Path.GetTempPath(), "rpg-headless-tests"));
    StrategicWorldState state = new StrategicWorldService().CreateInitialState(definition);

    WorldSiteIntelViewModel view = WorldSiteIntelService.BuildCurrentView(
        state,
        definition,
        StrategicWorldIds.SiteBonefield,
        WorldIntelVisibility.Visible);

    AssertEqual(WorldSiteIntelPolicy.Partial, view.Policy, "bonefield policy");
    AssertTrue(view.KnownEntranceIds.Contains("main_entrance"), "main entrance should be public");
    AssertTrue(view.UnknownIntelReasons.Any(item => item.Contains("内侧营地", StringComparison.Ordinal)), "bonefield should name hidden tactical detail");
}

static void StrategicDetailGarrisonListIsGatedByTacticalLayoutIntel()
{
    string strategicRoot = ReadStrategicWorldRootSource();
    string method = ExtractMethodBlock(strategicRoot, "private void RefreshDetail");

    AssertTrue(
        method.Contains("WorldSiteIntelService.BuildCurrentView", StringComparison.Ordinal),
        "detail panel should build a site intel view");
    AssertTrue(
        method.Contains("AddSiteGarrisonLines(_garrisonList, site, intelView)", StringComparison.Ordinal),
        "detail panel should route garrison display through the intel-gated helper");
    AssertTrue(
        !method.Contains("foreach (GarrisonState garrison in site.Garrison)", StringComparison.Ordinal),
        "detail panel should not directly iterate exact site garrison without checking tactical layout intel");
}

static void BattleRequestCarriesStructuredSiteIntel()
{
    StrategicWorldDefinition definition = StrategicWorldV1DefinitionFactory.Create(loadInitialStateResource: false);
    System.Environment.SetEnvironmentVariable("RPG_GAMELOG_DIR", Path.Combine(Path.GetTempPath(), "rpg-headless-tests"));
    StrategicWorldState state = new StrategicWorldService().CreateInitialState(definition);
    WorldBattleRequestBuilder builder = new();

    BattleStartRequest request = builder.BuildAssaultBonefieldRequest(
        state,
        definition,
        "res://scenes/world/StrategicWorldRoot.tscn",
        "res://scenes/world/sites/WorldSiteRoot.tscn");

    AssertEqual("Partial", request.SiteIntelPolicyId, "request policy");
    AssertTrue(request.RevealedEntranceIds.Contains("main_entrance"), "request should carry revealed entrance ids");
    AssertTrue(request.ActiveObscurationSourceIds.Contains("bonefield_outer_watch"), "request should carry active obscuration source ids");
}

static void BonefieldAssaultRequestHidesHiddenEntranceUntilMemoryRevealsIt()
{
    StrategicWorldDefinition definition = StrategicWorldV1DefinitionFactory.Create(loadInitialStateResource: false);
    System.Environment.SetEnvironmentVariable("RPG_GAMELOG_DIR", Path.Combine(Path.GetTempPath(), "rpg-headless-tests"));
    StrategicWorldState state = new StrategicWorldService().CreateInitialState(definition);
    WorldBattleRequestBuilder builder = new();

    BattleStartRequest initialRequest = builder.BuildAssaultBonefieldRequest(
        state,
        definition,
        "res://scenes/world/StrategicWorldRoot.tscn",
        "res://scenes/world/sites/WorldSiteRoot.tscn");

    AssertTrue(HasEntrance(initialRequest, "main_entrance", StrategicWorldIds.FactionPlayer), "public player entrance should stay available");
    AssertTrue(!HasEntrance(initialRequest, "main_entrance_east", StrategicWorldIds.FactionPlayer), "hidden player entrance should not be available before intel reveals it");

    state.SiteStates[StrategicWorldIds.SiteBonefield].Memory.RevealedEntranceIds.Add("main_entrance_east");
    BattleStartRequest revealedRequest = builder.BuildAssaultBonefieldRequest(
        state,
        definition,
        "res://scenes/world/StrategicWorldRoot.tscn",
        "res://scenes/world/sites/WorldSiteRoot.tscn");

    AssertTrue(revealedRequest.RevealedEntranceIds.Contains("main_entrance_east"), "request should carry memory-revealed entrance id");
    AssertTrue(HasEntrance(revealedRequest, "main_entrance_east", StrategicWorldIds.FactionPlayer), "memory-revealed player entrance should become available");
}

static void BonefieldWatchPostActionRevealsEastEntranceInBattleRequest()
{
    StrategicWorldDefinition definition = StrategicWorldV1DefinitionFactory.Create(loadInitialStateResource: false);
    System.Environment.SetEnvironmentVariable("RPG_GAMELOG_DIR", Path.Combine(Path.GetTempPath(), "rpg-headless-tests"));
    StrategicWorldState state = new StrategicWorldService().CreateInitialState(definition);
    WorldSiteDefinition siteDefinition = definition.SiteDefinitions.Single(site => site.Id == StrategicWorldIds.SiteBonefield);
    SiteExplorationPointDefinition watchPost = siteDefinition.ExplorationPoints.Single(point =>
        point.Actions.Any(action => action.Id == "observe_watch_post"));
    SiteExplorationActionDefinition action = watchPost.Actions.Single(action => action.Id == "observe_watch_post");

    WorldSiteExplorationService.ApplyActionResult(
        state.SiteStates[StrategicWorldIds.SiteBonefield],
        watchPost.Id,
        action);

    BattleStartRequest request = new WorldBattleRequestBuilder().BuildAssaultBonefieldRequest(
        state,
        definition,
        "res://scenes/world/StrategicWorldRoot.tscn",
        "res://scenes/world/sites/WorldSiteRoot.tscn");

    AssertTrue(request.RevealedEntranceIds.Contains("main_entrance_east"), "watch post action should reveal east entrance into request intel");
    AssertTrue(HasEntrance(request, "main_entrance_east", StrategicWorldIds.FactionPlayer), "watch post action should make east entrance selectable");
}

static void ExplorationBattleRequestReceivesStructuredSiteIntel()
{
    StrategicWorldDefinition definition = StrategicWorldV1DefinitionFactory.Create(loadInitialStateResource: false);
    System.Environment.SetEnvironmentVariable("RPG_GAMELOG_DIR", Path.Combine(Path.GetTempPath(), "rpg-headless-tests"));
    StrategicWorldState state = new StrategicWorldService().CreateInitialState(definition);
    WorldSiteState site = state.SiteStates[StrategicWorldIds.SiteBonefield];
    site.Memory.RevealedEntranceIds.Add("main_entrance_east");
    site.Memory.KnownTacticalTags.Add("watch_post_route_known");
    site.Memory.ExplorationAdvantageTags.Add("outer_watch_scouted");

    BattleStartRequest request = WorldSiteExplorationService.BuildExplorationBattleRequest(
        StrategicWorldIds.SiteBonefield,
        "patrol:bonefield_patrol_01",
        "bonefield_patrol_01",
        new Rpg.Domain.Battle.Grid.GridSurfacePosition(2, 3, 0),
        alertLevel: 2,
        "res://scenes/world/StrategicWorldRoot.tscn",
        "res://scenes/world/sites/WorldSiteRoot.tscn");

    WorldSiteIntelService.ApplySiteIntelToRequest(
        state,
        definition,
        request,
        StrategicWorldIds.SiteBonefield);

    AssertEqual("Partial", request.SiteIntelPolicyId, "exploration request policy");
    AssertTrue(request.RevealedEntranceIds.Contains("main_entrance"), "exploration request should carry public entrance id");
    AssertTrue(request.RevealedEntranceIds.Contains("main_entrance_east"), "exploration request should carry memory-revealed entrance id");
    AssertTrue(request.KnownTacticalTags.Contains("watch_post_route_known"), "exploration request should carry memory-derived tactical tags");
    AssertTrue(request.ActiveObscurationSourceIds.Contains("bonefield_outer_watch"), "exploration request should carry active obscuration source ids");
    AssertTrue(request.ExplorationAdvantageTags.Contains("outer_watch_scouted"), "exploration request should carry memory-derived advantage tags");
}

static void WorldSiteRootAppliesIntelToExplorationBattleRequest()
{
    string worldSiteRoot = ReadWorldSiteRootSource();
    string method = ExtractMethodBlock(worldSiteRoot, "private void RequestSiteExplorationBattle");

    AssertTrue(method.Contains("WorldSiteExplorationService.BuildExplorationBattleRequest", StringComparison.Ordinal), "root should build exploration battle request in RequestSiteExplorationBattle");
    AssertTrue(method.Contains("WorldSiteIntelService.ApplySiteIntelToRequest", StringComparison.Ordinal), "root should apply structured site intel before beginning exploration battle");
}

static void WorldSiteRootAppliesExplorationActionsThroughWorldContext()
{
    string worldSiteRoot = ReadWorldSiteRootSource();
    string method = ExtractMethodBlock(worldSiteRoot, "private void ExecuteSiteExplorationPointAction");

    AssertTrue(
        method.Contains("WorldSiteExplorationService.ApplyActionResult(", StringComparison.Ordinal),
        "root should apply authored exploration point actions through the exploration service");
    AssertTrue(
        method.Contains("StrategicWorldRuntime.State", StringComparison.Ordinal) &&
        method.Contains("StrategicWorldRuntime.Definition", StringComparison.Ordinal),
        "root should pass world context so consuming exploration actions can advance WorldTick");
    AssertTrue(
        method.Contains("StrategicWorldRuntime.LastNotice = applyResult.Message", StringComparison.Ordinal),
        "root notice should use the world action result message so tick feedback is preserved");
}

static void WorldSiteRootExplorationPlacementUsesKnownPlayerEntrances()
{
    string worldSiteRoot = ReadWorldSiteRootSource();
    string ensurePlacement = ExtractMethodBlock(worldSiteRoot, "private bool EnsureVisitingArmyPlacement");
    string resolveEntry = ExtractMethodBlock(worldSiteRoot, "private bool TryResolveExplorationEntrySurface");

    AssertTrue(
        ensurePlacement.Contains("TryResolveKnownPlayerEntranceDeploymentCandidate", StringComparison.Ordinal),
        "visiting army placement should resolve deployment through currently known player entrances");
    AssertTrue(
        resolveEntry.Contains("TryResolveKnownPlayerEntranceDeploymentCandidate", StringComparison.Ordinal),
        "exploration entry placement should resolve deployment through currently known player entrances");
    AssertTrue(
        !ensurePlacement.Contains("TryResolveFirstDeploymentCandidate(direction, canEnterWater", StringComparison.Ordinal),
        "visiting army placement should not use raw TargetApproachDirection deployment candidates");
    AssertTrue(
        !resolveEntry.Contains("TryResolveFirstDeploymentCandidate(direction, canEnterWater", StringComparison.Ordinal),
        "exploration entry placement should not use raw TargetApproachDirection deployment candidates");
}

static void WorldSiteRootExplorationReadinessHasNoPatrolRouteFallback()
{
    string worldSiteRoot = ReadWorldSiteRootSource();
    string method = ExtractMethodBlock(worldSiteRoot, "private void EnsureSiteExplorationStateReady");
    string afterEntryFailure = method.Contains("if (TryResolveExplorationEntrySurface", StringComparison.Ordinal)
        ? method[(method.IndexOf("if (TryResolveExplorationEntrySurface", StringComparison.Ordinal) + 1)..]
        : method;

    AssertTrue(
        !afterEntryFailure.Contains("ExplorationPatrols", StringComparison.Ordinal) &&
        !afterEntryFailure.Contains("RouteCells", StringComparison.Ordinal),
        "exploration readiness must not place the party on patrol route cells after known entrance resolution fails");
    AssertTrue(
        afterEntryFailure.Contains("GameLog.Warn", StringComparison.Ordinal),
        "exploration readiness should log when no valid known entrance entry can be resolved");
}

static void WorldSiteRootExplorationCurrentCellCopyRequiresKnownEntrancePlacement()
{
    string worldSiteRoot = ReadWorldSiteRootSource();
    string method = ExtractMethodBlock(worldSiteRoot, "private bool EnsureVisitingArmyPlacement");
    string copyBlock = ExtractIfBlockContaining(method, "site.Exploration.CurrentCellX = partyPlacement.CellX");

    AssertTrue(
        !string.IsNullOrWhiteSpace(copyBlock),
        "visiting army placement should keep exploration current cell synchronized with a valid party placement");
    AssertTrue(
        copyBlock.Contains("IsKnownPlayerEntrancePlacement(site, definition, partyPlacement)", StringComparison.Ordinal) ||
        copyBlock.Contains("refreshedPartyPlacement", StringComparison.Ordinal),
        "exploration current cell copy must require a refreshed placement or a known player entrance placement");
}

static void WorldSiteRootExecutesStartsBattleExplorationActionsThroughBattleHandoff()
{
    string worldSiteRoot = ReadWorldSiteRootSource();
    string appendMethod = ExtractMethodBlock(worldSiteRoot, "private bool TryAppendSiteExplorationPointActions");
    string executeMethod = ExtractMethodBlock(worldSiteRoot, "private void ExecuteSiteExplorationPointAction");
    string battleMethod = ExtractMethodBlock(worldSiteRoot, "private void RequestSiteExplorationPointBattle");

    AssertTrue(
        !appendMethod.Contains("button.Disabled = action.StartsBattle", StringComparison.Ordinal),
        "StartsBattle exploration point buttons should be clickable");
    AssertTrue(
        !appendMethod.Contains("if (!action.StartsBattle)", StringComparison.Ordinal),
        "StartsBattle exploration point buttons should route to the same point action executor");
    AssertTrue(
        executeMethod.Contains("RequestSiteExplorationPointBattle(site, definition, point, action)", StringComparison.Ordinal),
        "StartsBattle point actions should invoke the real exploration point battle path");
    AssertTrue(
        !executeMethod.Contains("battle action blocked", StringComparison.Ordinal),
        "StartsBattle point actions should not remain blocked");
    AssertTrue(
        battleMethod.Contains("WorldSiteExplorationService.BuildExplorationBattleRequest", StringComparison.Ordinal) &&
        battleMethod.Contains("WorldSiteIntelService.ApplySiteIntelToRequest", StringComparison.Ordinal) &&
        battleMethod.Contains("_battleLauncher.BeginAndActivate", StringComparison.Ordinal),
        "StartsBattle point actions should build an intel-filtered request and activate battle through the handoff");
    AssertTrue(
        battleMethod.Contains("_battleLauncher.CaptureRollback", StringComparison.Ordinal),
        "StartsBattle point battle activation should use the existing rollback path on failure");
    AssertTrue(
        battleMethod.Contains("action.AlertDelta", StringComparison.Ordinal),
        "StartsBattle point battle request should include the action alert delta");
}

static void StrategicDirectSiteEntryRequiresTacticalLayoutIntel()
{
    string strategicRoot = ReadStrategicWorldRootSource();
    string canEnterMethod = NormalizeLineEndings(ExtractMethodBlock(strategicRoot, "private bool CanEnterSelectedSiteDetail"));
    string canShowMethod = NormalizeLineEndings(ExtractMethodBlock(strategicRoot, "private bool CanShowSelectedSiteDetailEntry"));
    string arrivedChoiceMethod = ExtractMethodBlock(strategicRoot, "private void AddArrivedAssaultChoiceButtons");

    AssertTrue(
        !canEnterMethod.Contains("if (CanExploreSelectedSite(site))\n        {\n            return true;\n        }", StringComparison.Ordinal),
        "direct site entry should not allow hostile exploration config before checking tactical layout intel");
    AssertTrue(
        !canShowMethod.Contains("return intelView.CanInspectFullTacticalLayout || CanExploreSelectedSite(site);", StringComparison.Ordinal),
        "ordinary detail entry visibility should not use exploration config as an alternative to tactical layout intel");
    AssertTrue(
        arrivedChoiceMethod.Contains("CanExploreSelectedSite(site)", StringComparison.Ordinal),
        "arrived assault infiltration choices should still use exploration config");
}

static StrategicWorldDefinition BuildDefinition(WorldSiteIntelPolicy policy)
{
    return new StrategicWorldDefinition
    {
        SiteDefinitions =
        {
            new WorldSiteDefinition
            {
                Id = "site_under_test",
                DisplayName = "Test Site",
                SiteKind = WorldSiteKind.ResourceSite,
                Description = "Test site for intel regression.",
                MapPosition = new Vector2(10, 20),
                InitialOwnerFactionId = "enemy",
                InitialControlState = SiteControlState.Hostile,
                Intel = new WorldSiteIntelDefinition
                {
                    Policy = policy,
                    StrategicSummary = "Enemy-held resource site.",
                    TacticalSummary = "The front gate has a small garrison.",
                    HiddenTacticalSummary = "Hidden tactical layout unknown.",
                    PublicEntranceIds = { "front_gate" },
                    ObscurationSources =
                    {
                        new WorldSiteObscurationDefinition
                        {
                            Id = "grave_fog",
                            DisplayName = "Grave Fog",
                            Description = "Grave fog hides the inner camp.",
                            HidesTacticalLayout = policy == WorldSiteIntelPolicy.Obscured,
                            DisabledByResolvedPointIds = { "disable_fog_point" }
                        }
                    }
                },
                EntranceDefinitions =
                {
                    new BattleEntranceDefinition
                    {
                        EntranceId = "front_gate",
                        DisplayName = "Front Gate",
                        FactionId = "player"
                    },
                    new BattleEntranceDefinition
                    {
                        EntranceId = "side_gate",
                        DisplayName = "Side Gate",
                        FactionId = "player"
                    }
                }
            }
        }
    };
}

static StrategicWorldState BuildState()
{
    StrategicWorldState state = new() { PlayerFactionId = "player" };
    state.SiteStates["site_under_test"] = new WorldSiteState
    {
        SiteId = "site_under_test",
        OwnerFactionId = "enemy",
        ControlState = SiteControlState.Hostile
    };
    return state;
}

static void Run(string name, Action test)
{
    try
    {
        test();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"FAIL {name}: {exception.Message}");
        System.Environment.ExitCode = 1;
    }
}

static void AssertTrue(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void AssertEqual<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{message}. Expected={expected} Actual={actual}");
    }
}

static bool HasEntrance(BattleStartRequest request, string entranceId, string factionId)
{
    return request.AvailableEntrances.Any(entrance =>
        entrance.EntranceId == entranceId &&
        entrance.FactionId == factionId);
}


static string ReadStrategicWorldRootSource()
{
    string dir = Path.Combine("src", "Presentation", "World");
    return string.Join("\n", Directory.GetFiles(dir, "StrategicWorldRoot*.cs").OrderBy(path => path).Select(File.ReadAllText));
}

static string ReadWorldSiteRootSource()
{
    string dir = Path.Combine("src", "Presentation", "World", "Sites");
    return string.Join("\n", Directory.GetFiles(dir, "WorldSiteRoot*.cs").OrderBy(path => path).Select(File.ReadAllText));
}

static string ExtractMethodBlock(string source, string signature)
{
    int start = source.IndexOf(signature, StringComparison.Ordinal);
    if (start < 0)
    {
        return "";
    }

    int brace = source.IndexOf('{', start);
    if (brace < 0)
    {
        return "";
    }

    int depth = 0;
    for (int index = brace; index < source.Length; index++)
    {
        if (source[index] == '{')
        {
            depth++;
        }
        else if (source[index] == '}')
        {
            depth--;
            if (depth == 0)
            {
                return source.Substring(start, index - start + 1);
            }
        }
    }

    return source[start..];
}

static string ExtractIfBlockContaining(string source, string needle)
{
    int needleIndex = source.IndexOf(needle, StringComparison.Ordinal);
    if (needleIndex < 0)
    {
        return "";
    }

    int ifIndex = source.LastIndexOf("if", needleIndex, StringComparison.Ordinal);
    while (ifIndex >= 0)
    {
        int paren = source.IndexOf('(', ifIndex);
        int brace = source.IndexOf('{', ifIndex);
        if (paren >= 0 && brace >= 0 && paren < brace)
        {
            int depth = 0;
            for (int index = brace; index < source.Length; index++)
            {
                if (source[index] == '{')
                {
                    depth++;
                }
                else if (source[index] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        string block = source.Substring(ifIndex, index - ifIndex + 1);
                        if (block.Contains(needle, StringComparison.Ordinal))
                        {
                            return block;
                        }
                    }
                }
            }
        }

        ifIndex = source.LastIndexOf("if", Math.Max(0, ifIndex - 1), StringComparison.Ordinal);
    }

    return "";
}

static string NormalizeLineEndings(string value)
{
    return value.Replace("\r\n", "\n", StringComparison.Ordinal);
}
