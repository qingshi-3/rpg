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
Run("strategic V1 sites author explicit intel policies", StrategicV1SitesAuthorExplicitIntelPolicies);
Run("bonefield is partial intel with public entrance", BonefieldIsPartialIntelWithPublicEntrance);
Run("strategic detail garrison list is gated by tactical layout intel", StrategicDetailGarrisonListIsGatedByTacticalLayoutIntel);
Run("battle request carries structured site intel", BattleRequestCarriesStructuredSiteIntel);
Run("bonefield assault request hides hidden entrance until memory reveals it", BonefieldAssaultRequestHidesHiddenEntranceUntilMemoryRevealsIt);
Run("strategic direct site entry requires tactical layout intel", StrategicDirectSiteEntryRequiresTacticalLayoutIntel);
Run("arrived assault opens prebattle gate directly", ArrivedAssaultOpensPrebattleGateDirectly);

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
    AssertTrue(view.AvailableApproaches.Any(item => item.ActionId == "direct_assault"), "partial site should offer direct assault");
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
    AssertTrue(view.AvailableApproaches.Any(item => item.ActionId == "direct_assault"), "obscured site should offer direct assault");
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
        UnknownIntelReasons = { "old hidden reason" },
        ActiveObscurationSourceIds = { "grave_fog" },
        KnownTacticalTags = { "tag_a" }
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
    AssertEqual(1, view.UnknownIntelReasons.Count, "unknown snapshot should only expose current unknown reason");
    AssertTrue(!view.UnknownIntelReasons.Contains("old hidden reason"), "unknown snapshot should not leak old hidden reasons");
    AssertEqual(0, view.ActiveObscurationSourceIds.Count, "unknown snapshot should not leak active obscuration ids");
    AssertEqual(0, view.KnownTacticalTags.Count, "unknown snapshot should not leak tactical tags");
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

static void StrategicDirectSiteEntryRequiresTacticalLayoutIntel()
{
    string strategicRoot = ReadStrategicWorldRootSource();
    string canEnterMethod = NormalizeLineEndings(ExtractMethodBlock(strategicRoot, "private bool CanEnterSelectedSiteDetail"));
    string canShowMethod = NormalizeLineEndings(ExtractMethodBlock(strategicRoot, "private bool CanShowSelectedSiteDetailEntry"));
    string arrivedChoiceMethod = ExtractMethodBlock(strategicRoot, "private void AddArrivedAssaultChoiceButtons");

    AssertTrue(
        !strategicRoot.Contains("CanExploreSelectedSite", StringComparison.Ordinal) &&
        !strategicRoot.Contains("EnterSelectedSiteInfiltration", StringComparison.Ordinal),
        "strategic site entry should not keep standalone site exploration entry points");
    AssertTrue(
        canEnterMethod.Contains("intelView.CanInspectFullTacticalLayout", StringComparison.Ordinal),
        "hostile direct site entry should require tactical layout intel");
    AssertTrue(
        canShowMethod.Contains("return intelView.CanInspectFullTacticalLayout;", StringComparison.Ordinal),
        "ordinary detail entry visibility should not use removed site exploration config as an alternative");
    AssertTrue(
        arrivedChoiceMethod.Contains("TryEnterBattleForArrivedArmy", StringComparison.Ordinal) &&
        !arrivedChoiceMethod.Contains("进入探索", StringComparison.Ordinal),
        "arrived assault choices should offer direct battle instead of site exploration");
}

static void ArrivedAssaultOpensPrebattleGateDirectly()
{
    string strategicRoot = ReadStrategicWorldRootSource();
    string movementMethod = NormalizeLineEndings(ExtractMethodBlock(strategicRoot, "private bool UpdateWorldArmyMovement"));
    string battleAnnouncementMethod = NormalizeLineEndings(ExtractMethodBlock(strategicRoot, "private void BeginBattleAnnouncement"));

    AssertTrue(
        movementMethod.Contains("result.BattleReadyArmyIds[0]", StringComparison.Ordinal) &&
        movementMethod.Contains("TryEnterBattleForArrivedArmy", StringComparison.Ordinal),
        "battle-ready assault arrivals should open the battle gate directly instead of waiting for a left-panel action");
    AssertTrue(
        battleAnnouncementMethod.Contains("ShowPreBattleDialog();", StringComparison.Ordinal) &&
        !battleAnnouncementMethod.Contains("ShowBattleAlertDialog();", StringComparison.Ordinal),
        "battle announcement should open the actionable pre-battle dialog directly");
    AssertTrue(
        !strategicRoot.Contains("_battleAlertDialog.Confirmed += ShowPreBattleDialog", StringComparison.Ordinal),
        "battle alert confirmation should not be the required first step before pre-battle choice");
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

static string NormalizeLineEndings(string value)
{
    return value.Replace("\r\n", "\n", StringComparison.Ordinal);
}
