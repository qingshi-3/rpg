using Rpg.Application.Battle;
using Rpg.Application.Battle.Commands;
using Rpg.Application.Battle.Snapshots;
using Rpg.Application.World;
using Rpg.Definitions.World;
using Rpg.Domain.Battle.Grid;
using Rpg.Domain.World;
using Rpg.Presentation.Battle.Entities;
using Rpg.Runtime.Battle;
using System.Collections;
using System.Runtime.CompilerServices;

internal static partial class WorldSiteDeploymentCacheRegressionCases
{
internal static void BattleGroupProbeSnapshotIncludesHeroOwnedConfiguredSkillDefinitions()
{
    const string armyId = "army_first_slice_archer";
    string[] expectedSkillDefinitionIds =
    {
        "skill_thunder_tag_throw",
        "skill_thunder_mark_fold",
        "skill_thunder_spiral_break"
    };
    StrategicWorldDefinition definition = StrategicWorldV1DefinitionFactory.Create(loadInitialStateConfig: false);
    StrategicWorldState state = BuildFirstSliceAssaultState(
        definition,
        armyId,
        heroUnitId: "f1_windbladecommander",
        corpsUnitId: "f1_backlinearcher");
    BattleStartRequest request = new WorldBattleRequestBuilder().BuildAssaultBonefieldRequest(
        state,
        definition,
        "res://scenes/world/StrategicWorldRoot.tscn",
        "res://scenes/world/sites/WorldSiteRoot.tscn",
        armyId);

    BattleGroupSessionProbeResult result = new BattleGroupSessionProbeService().PrepareSnapshot(request);

    AssertTrue(result.Success, $"probe snapshot should prepare successfully failure={result.FailureReason}");
    BattleGroupSnapshot[] playerHeroGroups = result.Snapshot.BattleGroups
        .Where(group =>
            string.Equals(group.FactionId, StrategicWorldIds.FactionPlayer, StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(group.HeroId))
        .ToArray();
    AssertEqual(
        playerHeroGroups.Length * expectedSkillDefinitionIds.Length,
        result.Snapshot.SkillDefinitions.Count,
        "probe snapshot should carry a separate three-skill kit for each participating hero owner");

    foreach (BattleGroupSnapshot group in playerHeroGroups)
    {
        IReadOnlyList<BattleSkillSnapshot> ownerSkills = result.Snapshot.SkillDefinitions
            .Where(item => string.Equals(item.OwnerHeroId, group.HeroId, StringComparison.Ordinal))
            .ToArray();
        AssertEqual(3, ownerSkills.Count, $"hero={group.HeroId} should own exactly the configured three-skill kit");
        AssertEqual(
            string.Join("|", expectedSkillDefinitionIds),
            string.Join("|", ownerSkills.Select(item => item.SkillDefinitionId)),
            $"hero={group.HeroId} should reuse the shared thunder skill definitions in its own grants");
        AssertTrue(
            ownerSkills.All(item =>
                item.GrantedSkillId.StartsWith($"default_hero:{group.HeroId}:grant:", StringComparison.Ordinal) &&
                !item.GrantedSkillId.Contains("battle_group", StringComparison.Ordinal)),
            $"hero={group.HeroId} skill grants should be hero-owned, not battle-group-owned");
    }

    AssertThunderTagSkillSnapshot(result.Snapshot.SkillDefinitions);
    AssertThunderFoldSkillSnapshot(result.Snapshot.SkillDefinitions);
    AssertThunderSpiralSkillSnapshot(result.Snapshot.SkillDefinitions);
}

internal static void BattleSkillDefinitionsLiveInContentLayerAndMapToSnapshots()
{
    string root = ProjectRoot();
    string skillIndexSource = File.ReadAllText(Path.Combine(root, "config", "battle", "battle_skill_definitions.json"));
    string compilerSource = File.ReadAllText(Path.Combine(root, "src", "Application", "Battle", "Snapshots", "BattleSkillSnapshotCompiler.cs"));
    string runtimeResolverSource = File.ReadAllText(Path.Combine(root, "src", "Runtime", "Battle", "BattleRuntimeHeroSkillCommandResolver.cs"));

    AssertTrue(
        skillIndexSource.Contains("skill_shield_barrier", StringComparison.Ordinal) &&
        skillIndexSource.Contains("skill_sun_piercer", StringComparison.Ordinal) &&
        skillIndexSource.Contains("skill_thunder_tag_throw", StringComparison.Ordinal) &&
        File.Exists(Path.Combine(root, "assets", "battle", "skills", "skill_shield_barrier.tres")) &&
        File.Exists(Path.Combine(root, "assets", "battle", "skills", "skill_thunder_spiral_break.tres")),
        "first-slice hero skill data should be authored as indexed battle skill resources");
    AssertTrue(
        compilerSource.Contains("BattleSkillDefinitionResource", StringComparison.Ordinal) &&
        compilerSource.Contains("DamageSkillEffectSnapshot", StringComparison.Ordinal) &&
        compilerSource.Contains("CreateMarkSkillEffectSnapshot", StringComparison.Ordinal) &&
        compilerSource.Contains("TeleportToMarkSkillEffectSnapshot", StringComparison.Ordinal) &&
        compilerSource.Contains("ChanneledAreaDamageSkillEffectSnapshot", StringComparison.Ordinal),
        "application snapshot compiler should translate authored resources into typed runtime snapshots");
    AssertTrue(
        !runtimeResolverSource.Contains("FirstSliceBattleSkillDefinitions", StringComparison.Ordinal) &&
        runtimeResolverSource.Contains("skill_caster_not_allowed", StringComparison.Ordinal),
        "runtime skill resolver must consume snapshot data and reject skills not bound to the caster group");
}

internal static void BattleRuntimeHudFiltersSkillsToSelectedHeroCompany()
{
    string rootSource = ReadWorldSiteRootSource();
    string presentationSource = ReadWorldSitePresentationSource();
    string targetPresentationSource = File.ReadAllText(Path.Combine(
        ProjectRoot(),
        "src",
        "Presentation",
        "World",
        "Sites",
        "BattleRuntimeHeroSkillTargetPresentation.cs"));

    AssertTrue(
        rootSource.Contains("BuildBattleRuntimeSkillSnapshots(selected)", StringComparison.Ordinal) &&
        presentationSource.Contains("IsBattleRuntimeSkillAvailableForGroup", StringComparison.Ordinal) &&
        presentationSource.Contains("CasterUnitIds", StringComparison.Ordinal),
        "battle runtime HUD should filter skill snapshots by the selected battle group's bound hero unit");
    AssertTrue(
        targetPresentationSource.Contains("FirstSliceHeroCompanyIds.IsHeroUnit", StringComparison.Ordinal),
        "target picking should recognize every first-slice hero unit as the preferred visible caster");
}

private static void AssertThunderTagSkillSnapshot(IReadOnlyList<BattleSkillSnapshot> skills)
{
    BattleSkillSnapshot skill = skills.FirstOrDefault(item => item.SkillDefinitionId == "skill_thunder_tag_throw");
    AssertTrue(skill != null, "snapshot should include thunder tag throw");
    AssertEqual((BattleSkillTargetingMode)3, skill.TargetingMode, "thunder tag targeting mode");
    AssertTrue(skill.ReleasesWithoutOccupyingCaster, "thunder tag should be an offhand release that does not occupy movement state");
    AssertTrue(skill.Effects.OfType<DamageSkillEffectSnapshot>().Any(item => item.BaseDamage == 12), "thunder tag should deal impact damage");
    AssertTrue(skill.Effects.OfType<CreateMarkSkillEffectSnapshot>().Any(), "thunder tag should create a runtime mark");
}

private static void AssertThunderFoldSkillSnapshot(IReadOnlyList<BattleSkillSnapshot> skills)
{
    BattleSkillSnapshot skill = skills.FirstOrDefault(item => item.SkillDefinitionId == "skill_thunder_mark_fold");
    AssertTrue(skill != null, "snapshot should include thunder mark fold");
    AssertEqual(BattleSkillTargetingMode.TargetedCell, skill.TargetingMode, "thunder fold targeting mode");
    AssertTrue(skill.CanCancelBasicAttackRecovery, "thunder fold should be a high-tier mobility cancel");
    AssertTrue(skill.Effects.OfType<TeleportToMarkSkillEffectSnapshot>().Any(item => item.LandingRadius == 3), "thunder fold should teleport within the accepted radius around a selected mark");
}

private static void AssertThunderSpiralSkillSnapshot(IReadOnlyList<BattleSkillSnapshot> skills)
{
    BattleSkillSnapshot skill = skills.FirstOrDefault(item => item.SkillDefinitionId == "skill_thunder_spiral_break");
    AssertTrue(skill != null, "snapshot should include thunder spiral break");
    AssertEqual(BattleSkillTargetingMode.TargetedCell, skill.TargetingMode, "thunder spiral targeting mode");
    AssertEqual(3, skill.Range, "thunder spiral direction target center range");
    ChanneledAreaDamageSkillEffectSnapshot channel = skill.Effects.OfType<ChanneledAreaDamageSkillEffectSnapshot>().FirstOrDefault();
    AssertTrue(channel != null, "thunder spiral should start a channeled damage window");
    AssertEqual(14, channel.BaseDamage, "thunder spiral tick damage");
    AssertEqual(1, channel.Radius, "thunder spiral radius");
    AssertTrue(channel.DurationSeconds >= 1.4 && channel.TickIntervalSeconds > 0, "thunder spiral should carry a readable channel duration and tick interval");
}

internal static void WorldSiteBattleRuntimeHeroSkillTargetClickBuildsTargetedCommand()
{
    string rootSource = ReadWorldSiteRootSource();
    string inputBody = ExtractMethodBody(rootSource, "public override void _Input(InputEvent @event)");
    string targetInputBody = ExtractMethodBody(rootSource, "private bool TryHandleBattleRuntimeHeroSkillTargetInput(InputEvent inputEvent)");
    string submitBody = ExtractMethodBody(rootSource, "private void SubmitBattleRuntimeHeroSkillCommand(");
    string requestFactoryBody = ExtractMethodBody(ReadWorldSitePresentationSource(), "internal static CommandRequest BuildHeroSkillCommandRequest(");

    AssertTrue(
        inputBody.Contains("TryHandleBattleRuntimeHeroSkillTargetInput(@event)", StringComparison.Ordinal),
        "battle input should route mouse clicks through the hero skill target-picking handler");
    AssertTrue(
        targetInputBody.Contains("FindEntityAt(position)", StringComparison.Ordinal) &&
        targetInputBody.Contains("TryResolveBattleRuntimeHeroSkillTargetActorId", StringComparison.Ordinal) &&
        targetInputBody.Contains("SubmitBattleRuntimeHeroSkillCommand(_battleRuntimeHeroSkillTargetPickingGroup, sourceActorId, targetActorId)", StringComparison.Ordinal),
        "target-picking click should resolve the hovered battle entity and submit the selected target actor id");
    AssertTrue(
        submitBody.Contains("BuildBattleRuntimeHeroSkillCommandRequest(selected, sourceActorId, targetActorId)", StringComparison.Ordinal) &&
        requestFactoryBody.Contains("TargetActorId = targetActorId", StringComparison.Ordinal),
        "hero skill submit should pass the clicked target actor id into the command request");
}

internal static void WorldSiteBattleRuntimeHeroSkillSupportsCellAndSelfTargets()
{
    string rootSource = ReadWorldSiteRootSource();
    string beginBody = ExtractMethodBody(rootSource, "private void BeginBattleRuntimeSkillPress(BattleRuntimeCommandGroupView selected, string skillDefinitionId)");
    string targetInputBody = ExtractMethodBody(rootSource, "private bool TryHandleBattleRuntimeHeroSkillTargetInput(InputEvent inputEvent)");
    string requestFactoryBody = ExtractMethodBody(ReadWorldSitePresentationSource(), "internal static CommandRequest BuildHeroSkillCommandRequest(");

    AssertTrue(
        beginBody.Contains("IsImmediateSelfSkill(pressedSkill)", StringComparison.Ordinal) &&
        beginBody.Contains("SubmitBattleRuntimeHeroSkillCommand(selected, sourceActorId, \"\")", StringComparison.Ordinal),
        "self-centered hero skills should submit immediately after source resolution");
    AssertTrue(
        targetInputBody.Contains("BattleSkillTargetingMode.TargetedCell", StringComparison.Ordinal) &&
        targetInputBody.Contains("SubmitBattleRuntimeHeroSkillCommand(_battleRuntimeHeroSkillTargetPickingGroup, sourceActorId, \"\", position)", StringComparison.Ordinal),
        "cell-target hero skills should submit the clicked grid cell instead of requiring an enemy entity");
    AssertTrue(
        requestFactoryBody.Contains("HasTargetGrid = targetGrid.HasValue", StringComparison.Ordinal) &&
        requestFactoryBody.Contains("TargetGridX = targetGrid?.X ?? 0", StringComparison.Ordinal),
        "cell-target hero skill commands should carry target grid coordinates into CommandRequest");
}

internal static void WorldSiteBattleRuntimeThunderTagSupportsGroundOrAttachedTarget()
{
    string rootSource = ReadWorldSiteRootSource();
    string targetInputBody = ExtractMethodBody(rootSource, "private bool TryHandleBattleRuntimeHeroSkillTargetInput(InputEvent inputEvent)");

    AssertTrue(
        targetInputBody.Contains("BattleSkillTargetingMode.TargetedActorOrCell", StringComparison.Ordinal) &&
        targetInputBody.Contains("TryResolveBattleRuntimeHeroSkillTargetActorId(source, target, out string targetActorOrCellId)", StringComparison.Ordinal) &&
        targetInputBody.Contains("IsBattleRuntimeHeroSkillTargetInRange(source, target, pickedSkill.Range)", StringComparison.Ordinal) &&
        targetInputBody.Contains("IsBattleRuntimeHeroSkillCellInRange(source, position, pickedSkill.Range)", StringComparison.Ordinal) &&
        targetInputBody.Contains("SubmitBattleRuntimeHeroSkillCommand(_battleRuntimeHeroSkillTargetPickingGroup, sourceActorId, targetActorOrCellId)", StringComparison.Ordinal) &&
        targetInputBody.Contains("SubmitBattleRuntimeHeroSkillCommand(_battleRuntimeHeroSkillTargetPickingGroup, sourceActorId, \"\", position)", StringComparison.Ordinal),
        "thunder tag target picking should attach to an in-range valid unit, or create an in-range ground mark when the clicked cell has no valid target");
}

internal static void BattleRuntimeThunderFoldRequiresLiveMarkBeforeSubmit()
{
    string presentationSource = ReadWorldSitePresentationSource();
    string rootSource = ReadWorldSiteRootSource();
    string resolverBody = ExtractMethodBody(presentationSource, "internal static WorldSiteRoot.BattleRuntimeSkillUsageState Resolve(");
    string rootResolveBody = ExtractMethodBody(rootSource, "private BattleRuntimeSkillUsageState ResolveSelectedHeroSkillUsageState(");

    AssertTrue(
        resolverBody.Contains("RequiresLiveMark(skill)", StringComparison.Ordinal) &&
        resolverBody.Contains("HasLiveOwnedMark", StringComparison.Ordinal) &&
        resolverBody.Contains("BattleRuntimeSkillUsageState.Unavailable", StringComparison.Ordinal),
        "mark-dependent skills should be unavailable in the HUD until the selected hero group has a live runtime mark");
    AssertTrue(
        rootResolveBody.Contains("RuntimeController?.State?.SpatialMarks", StringComparison.Ordinal) &&
        rootResolveBody.Contains("RuntimeController?.CurrentTimeSeconds", StringComparison.Ordinal),
        "HUD skill usage resolution should read Runtime spatial marks instead of inferring fold availability from presentation events only");
}

internal static void BattleRuntimeThunderFoldUsesTwoStageTargeting()
{
    string rootSource = ReadWorldSiteRootSource();
    string presentationSource = ReadWorldSitePresentationSource();
    string targetInputBody = ExtractMethodBody(rootSource, "private bool TryHandleBattleRuntimeHeroSkillTargetInput(InputEvent inputEvent)");
    string refreshBody = ExtractMethodBody(rootSource, "private void RefreshBattleRuntimeHeroSkillTargetPreview()");
    string requestFactoryBody = ExtractMethodBody(presentationSource, "internal static CommandRequest BuildHeroSkillCommandRequest(");

    AssertTrue(
        rootSource.Contains("SkillTargetingStage", StringComparison.Ordinal) &&
        rootSource.Contains("TrySelectBattleRuntimeMark", StringComparison.Ordinal) &&
        rootSource.Contains("BuildBattleRuntimeMarkLandingCells", StringComparison.Ordinal),
        "mark-then-landing skills should keep explicit mark-selection and landing-selection UI stages");
    AssertTrue(
        targetInputBody.Contains("UsesMarkThenLandingFlow(pickedSkill)", StringComparison.Ordinal) &&
        targetInputBody.Contains("TrySelectBattleRuntimeMark", StringComparison.Ordinal) &&
        targetInputBody.Contains("BeginBattleRuntimeMarkLandingSelection", StringComparison.Ordinal) &&
        targetInputBody.Contains("TrySubmitBattleRuntimeMarkLanding", StringComparison.Ordinal),
        "first mark-targeting click should select a live mark and only the second legal landing click should submit");
    AssertTrue(
        refreshBody.Contains("BuildBattleRuntimeMarkLandingCells", StringComparison.Ordinal) &&
        refreshBody.Contains("BuildBattleRuntimeMarkCandidateCells", StringComparison.Ordinal) &&
        refreshBody.Contains("BattleGridHighlightKind.Skill", StringComparison.Ordinal),
        "mark preview should render live mark candidates, then landing preview should render candidate landing cells around the selected mark");
    AssertTrue(
        requestFactoryBody.Contains("SelectedSpatialMarkId = selectedSpatialMarkId", StringComparison.Ordinal),
        "fold command requests should carry the selected Runtime spatial mark id to Runtime validation");
}

internal static void BattleRuntimeThunderSpiralUsesDirectionPreviewAndSecondClickSubmit()
{
    string rootSource = ReadWorldSiteRootSource();
    string beginBody = ExtractMethodBody(rootSource, "private void BeginBattleRuntimeSkillPress(BattleRuntimeCommandGroupView selected, string skillDefinitionId)");
    string targetInputBody = ExtractMethodBody(rootSource, "private bool TryHandleBattleRuntimeHeroSkillTargetInput(InputEvent inputEvent)");
    string refreshBody = ExtractMethodBody(rootSource, "private void RefreshBattleRuntimeHeroSkillTargetPreview()");

    AssertTrue(
        beginBody.Contains("BeginBattleRuntimeHeroSkillTargetPicking(selected, normalizedSkillDefinitionId)", StringComparison.Ordinal) &&
        !beginBody.Contains("HeroSkillCommandIds.ThunderSpiralBreakSkillId", StringComparison.Ordinal),
        "directional area skills should use the normal target-picking entry instead of a skill-id hardcoded immediate submit");
    AssertTrue(
        targetInputBody.Contains("UsesDirectionAreaFlow(pickedSkill)", StringComparison.Ordinal) &&
        targetInputBody.Contains("TrySubmitBattleRuntimeDirectionalArea(position, sourceActorId, pickedSkill)", StringComparison.Ordinal) &&
        rootSource.Contains("ResolveBattleRuntimeDirectionalAreaCenter", StringComparison.Ordinal) &&
        rootSource.Contains("SubmitBattleRuntimeHeroSkillCommand(_battleRuntimeHeroSkillTargetPickingGroup, sourceActorId, \"\", center)", StringComparison.Ordinal),
        "directional area click handling should resolve the current mouse quadrant into a direction center and only then submit the command");
    AssertTrue(
        refreshBody.Contains("BuildBattleRuntimeDirectionalAreaCells(source, position, pickedSkill)", StringComparison.Ordinal) &&
        refreshBody.Contains("BattleGridHighlightKind.Target", StringComparison.Ordinal),
        "directional area preview should render the resolved forward target area as target highlight cells");

    BattleEntity source = BuildHeroSkillTargetEntity("player_force:1", BattleFaction.Player);
    GridOccupantComponent grid = source.GetComponent<GridOccupantComponent>();
    grid.GridX = 0;
    grid.GridY = 0;
    grid.FootprintWidth = 1;
    grid.FootprintHeight = 1;

    Type resolverType = typeof(BattleEntity).Assembly.GetType(
        "Rpg.Presentation.World.Sites.BattleRuntimeHeroSkillTargetPresentation",
        throwOnError: true)!;
    System.Reflection.MethodInfo method = resolverType.GetMethod(
        "BuildAreaPreviewCells",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
        new[] { typeof(BattleSkillTargetingSnapshot), typeof(BattleEntity), typeof(GridPosition), typeof(BattleGridMap) })!;

    BattleSkillTargetingSnapshot targeting = new()
    {
        InputFlow = BattleSkillInputFlow.SelectDirectionArea,
        TargetKind = BattleSkillTargetKind.Direction,
        AreaShape = BattleSkillAreaShape.GridRadius,
        AreaRadius = 1
    };
    var rightCells = ((IEnumerable<GridPosition>)method.Invoke(null, new object?[] { targeting, source, new GridPosition(4, 1), null })!)
        .ToHashSet();

    AssertTrue(rightCells.Contains(new GridPosition(1, -1)), "right-direction thunder spiral should include the front-left corner of the 3x3 area");
    AssertTrue(rightCells.Contains(new GridPosition(2, 0)), "right-direction thunder spiral should include the resolved area center");
    AssertTrue(rightCells.Contains(new GridPosition(3, 1)), "right-direction thunder spiral should include the front-right corner of the 3x3 area");
    AssertTrue(!rightCells.Contains(new GridPosition(-1, 0)), "right-direction thunder spiral should not highlight cells behind the caster");
}

internal static void WorldSiteBattleRuntimeHeroSkillTargetClickBuildsCasterScopedCommand()
{
    string rootSource = ReadWorldSiteRootSource();
    string targetInputBody = ExtractMethodBody(rootSource, "private bool TryHandleBattleRuntimeHeroSkillTargetInput(InputEvent inputEvent)");
    string requestFactoryBody = ExtractMethodBody(ReadWorldSitePresentationSource(), "internal static CommandRequest BuildHeroSkillCommandRequest(");
    string refreshBody = ExtractMethodBody(rootSource, "private void RefreshBattleRuntimeHeroSkillTargetPreview()");

    AssertTrue(
        targetInputBody.Contains("TryResolveBattleRuntimeHeroSkillSourceActorId", StringComparison.Ordinal),
        "target click should resolve the selected visible caster before submitting a hero skill command");
    AssertTrue(
        requestFactoryBody.Contains("SourceActorId = sourceActorId", StringComparison.Ordinal),
        "hero skill command request should carry the selected visible caster actor id");
    AssertTrue(
        refreshBody.Contains("BuildBattleRuntimeHeroSkillSourceEntity", StringComparison.Ordinal) &&
        refreshBody.Contains("BuildBattleRuntimeHeroSkillRangeCells(source)", StringComparison.Ordinal),
        "target preview should build skill range from the same visible caster submitted to runtime");
    AssertTrue(
        rootSource.Contains("ResolveBattleRuntimeHeroSkillRange", StringComparison.Ordinal) &&
        rootSource.Contains("BattleRuntimeHeroSkillTargetPresentation.BuildRangeCells(source, _activeGridMap, ResolveBattleRuntimeHeroSkillRange())", StringComparison.Ordinal),
        "target preview should read the selected skill range from runtime snapshots before drawing range cells");
    AssertTrue(
        !File.ReadAllText(Path.Combine(ProjectRoot(), "src", "Presentation", "World", "Sites", "BattleRuntimeHeroSkillTargetPresentation.cs"))
            .Contains("FirstSliceRange", StringComparison.Ordinal),
        "target presentation helper should not own a duplicate first-slice skill range constant");
}

internal static void BattleRuntimeHeroSkillPreviewBuildsDiamondRangeCells()
{
    BattleEntity source = BuildHeroSkillTargetEntity("player_force:1", BattleFaction.Player);
    GridOccupantComponent grid = source.GetComponent<GridOccupantComponent>();
    grid.GridX = 0;
    grid.GridY = 0;
    grid.FootprintWidth = 1;
    grid.FootprintHeight = 1;
    Type resolverType = typeof(BattleEntity).Assembly.GetType(
        "Rpg.Presentation.World.Sites.BattleRuntimeHeroSkillTargetPresentation",
        throwOnError: true)!;
    System.Reflection.MethodInfo method = resolverType.GetMethod(
        "BuildRangeCells",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
        new[] { typeof(BattleEntity), typeof(BattleGridMap), typeof(int) })!;

    var cells = ((IEnumerable<GridPosition>)method.Invoke(null, new object?[] { source, null, 2 })!).ToHashSet();

    AssertTrue(cells.Contains(new GridPosition(1, 1)), "skill preview should include diagonal cells inside Manhattan range");
    AssertTrue(!cells.Contains(new GridPosition(2, 2)), "skill preview should exclude square-corner cells outside the diamond");
}

internal static void BattleRuntimeHeroSkillTargetSelectionMatchesPreviewRange()
{
    BattleEntity source = BuildHeroSkillTargetEntity("player_force:1", BattleFaction.Player);
    GridOccupantComponent sourceGrid = source.GetComponent<GridOccupantComponent>();
    sourceGrid.GridX = 0;
    sourceGrid.GridY = 0;
    BattleEntity inRangeTarget = BuildHeroSkillTargetEntity("bonefield:f6_draugarlord:1", BattleFaction.Enemy);
    GridOccupantComponent inRangeGrid = inRangeTarget.GetComponent<GridOccupantComponent>();
    inRangeGrid.GridX = 3;
    inRangeGrid.GridY = 0;
    BattleEntity outOfRangeTarget = BuildHeroSkillTargetEntity("bonefield:f6_draugarlord:2", BattleFaction.Enemy);
    GridOccupantComponent outOfRangeGrid = outOfRangeTarget.GetComponent<GridOccupantComponent>();
    outOfRangeGrid.GridX = 5;
    outOfRangeGrid.GridY = 0;

    Type resolverType = typeof(BattleEntity).Assembly.GetType(
        "Rpg.Presentation.World.Sites.BattleRuntimeHeroSkillTargetPresentation",
        throwOnError: true)!;
    System.Reflection.MethodInfo targetRangeMethod = resolverType.GetMethod(
        "IsTargetInRange",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
    System.Reflection.MethodInfo cellRangeMethod = resolverType.GetMethod(
        "IsCellInRange",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

    AssertTrue(targetRangeMethod != null, "target presentation helper should expose actor target range matching the preview diamond");
    AssertTrue(cellRangeMethod != null, "target presentation helper should expose cell target range matching the preview diamond");

    AssertTrue((bool)targetRangeMethod.Invoke(null, new object?[] { source, inRangeTarget, 3 })!, "target on preview edge should be selectable");
    AssertTrue(!(bool)targetRangeMethod.Invoke(null, new object?[] { source, outOfRangeTarget, 3 })!, "target outside preview range should not be selectable");
    AssertTrue((bool)cellRangeMethod.Invoke(null, new object?[] { source, new GridPosition(2, 1), 3 })!, "cell inside diamond preview should be selectable");
    AssertTrue(!(bool)cellRangeMethod.Invoke(null, new object?[] { source, new GridPosition(3, 1), 3 })!, "cell outside diamond preview should not be selectable");
}

internal static void BattleRuntimeHeroSkillTargetClickUsesRuntimeActorId()
{
    BattleEntity source = BuildHeroSkillTargetEntity("player_force:1", BattleFaction.Player);
    BattleEntity target = BuildHeroSkillTargetEntity("bonefield:f6_draugarlord:1", BattleFaction.Enemy);
    Type resolverType = typeof(BattleEntity).Assembly.GetType(
        "Rpg.Presentation.World.Sites.BattleRuntimeHeroSkillTargetPresentation",
        throwOnError: true)!;
    System.Reflection.MethodInfo method = resolverType.GetMethod(
        "TryResolveTargetActorId",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
    object?[] arguments = { source, target, "" };

    bool resolved = (bool)method.Invoke(null, arguments)!;

    AssertTrue(resolved, "target-picking should resolve a valid hostile battle entity");
    AssertEqual(
        "bonefield:f6_draugarlord:1",
        arguments[2] as string,
        "target-picking should pass the presentation corps entity id as the runtime actor id");
}

internal static void BattleRuntimeHudUsesStrategicParticipantIdForSkillCommands()
{
    const string participantId = "strategic_participant:expedition_0001:hero_beast_tamer:corps_0003";
    BattleStartRequest request = new()
    {
        RequestId = "battle_strategic_skill_identity",
        SourceSiteId = "player_camp",
        TargetSiteId = "bonefield",
        BattleKind = BattleKind.AssaultSite
    };
    request.PlayerForces.Add(BuildStrategicParticipantForce(
        forceId: "strategic:expedition_0001:f1_elyxstormblade",
        unitDefinitionId: "f1_elyxstormblade",
        participantId,
        count: 1,
        x: 0,
        y: 0));
    request.PlayerForces.Add(BuildStrategicParticipantForce(
        forceId: "strategic:expedition_0001:f1_azuritelion",
        unitDefinitionId: "f1_azuritelion",
        participantId,
        count: 3,
        x: 0,
        y: 1));
    request.EnemyForces.Add(new BattleForceRequest
    {
        ForceId = "bonefield:f6_spiritwolf",
        UnitDefinitionId = "f6_spiritwolf",
        Count = 1,
        FactionId = "enemy",
        PreferredPlacements =
        {
            new BattleForcePlacementRequest
            {
                PlacementId = "enemy_near_skill_target",
                CellX = 3,
                CellY = 0,
                CellHeight = 0
            }
        }
    });

    string hudGroupKey = ResolveFirstRuntimeHudGroupKey(request);
    BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(
        BuildStrategicSkillIdentitySnapshot(participantId));
    BattleRuntimeActor sourceActor = controller.State.Actors.Single(item => item.ActorId == $"{participantId}:1");
    BattleRuntimeActor targetActor = controller.State.Actors.Single(item => item.ActorId == "bonefield:f6_spiritwolf:1");

    AssertEqual(participantId, hudGroupKey, "runtime HUD group key should use the strategic participant id");
    AssertEqual(participantId, sourceActor.BattleGroupId, "runtime source actor should belong to the strategic participant group");

    BattleRuntimeCommandSubmitResult submit = controller.SubmitCommand(new CommandRequest
    {
        CommandId = "cmd_strategic_hud_thunder_tag",
        BattleId = controller.BattleId,
        BattleGroupId = hudGroupKey,
        SourceActorId = sourceActor.ActorId,
        Channel = CommandChannel.Hero,
        Kind = CommandKind.CastSkill,
        SkillDefinitionId = "skill_thunder_tag_throw",
        TargetActorId = targetActor.ActorId
    });

    AssertTrue(
        submit.Accepted,
        $"strategic participant HUD group should submit a valid Runtime skill command reason={submit.ReasonCode}");
}

private static BattleStartSnapshot BuildStrategicSkillIdentitySnapshot(string participantId)
{
    BattleStartSnapshot snapshot = new()
    {
        SnapshotId = "snapshot:battle_strategic_skill_identity",
        BattleId = "battle_strategic_skill_identity",
        TargetLocationId = "bonefield",
        BattleGroups =
        {
            new BattleGroupSnapshot
            {
                BattleGroupId = "probe_group_strategic_participant_seed",
                RuntimeCommanderGroupId = participantId,
                FactionId = "player",
                SourceForceId = participantId,
                HeroId = "hero_beast_tamer",
                HeroDefinitionId = "hero_definition_beast_tamer",
                CorpsId = "corps_0003",
                CorpsDefinitionId = "f1_elyxstormblade",
                CorpsStrength = 100,
                MaxHitPoints = 100,
                AttackDamage = 10,
                AttackRange = 1,
                AttackSpeed = 1,
                MoveStepSeconds = 0.2,
                AttackActionSeconds = 0.4,
                AttackImpactDelaySeconds = 0.2,
                FootprintWidth = 1,
                FootprintHeight = 1,
                SourceLocationId = "plains_city",
                CellX = 0,
                CellY = 0
            },
            new BattleGroupSnapshot
            {
                BattleGroupId = "probe_group_bonefield",
                FactionId = "enemy",
                SourceForceId = "bonefield:f6_spiritwolf",
                HeroId = "bonefield_enemy",
                HeroDefinitionId = "bonefield_enemy_definition",
                CorpsId = "bonefield_spiritwolf",
                CorpsDefinitionId = "f6_spiritwolf",
                CorpsStrength = 100,
                MaxHitPoints = 100,
                AttackDamage = 10,
                AttackRange = 1,
                AttackSpeed = 1,
                MoveStepSeconds = 0.2,
                AttackActionSeconds = 0.4,
                AttackImpactDelaySeconds = 0.2,
                FootprintWidth = 1,
                FootprintHeight = 1,
                SourceLocationId = "bonefield",
                CellX = 3,
                CellY = 0
            }
        }
    };
    snapshot.SkillDefinitions.Add(BuildStrategicThunderTagSkillSnapshot(participantId));

    AttachFlatSnapshotTopology(snapshot);
    return snapshot;
}

private static BattleSkillSnapshot BuildStrategicThunderTagSkillSnapshot(string participantId)
{
    BattleSkillSnapshot skill = new()
    {
        SkillDefinitionId = "skill_thunder_tag_throw",
        GrantedSkillId = $"test_grant:{participantId}:thunder_tag",
        LoadoutSlotId = "primary",
        OwnerBattleGroupId = participantId,
        RuntimeCommanderGroupId = participantId,
        DisplayName = "雷符飞投",
        TargetingMode = BattleSkillTargetingMode.TargetedActorOrCell,
        Range = 8,
        Targeting = new BattleSkillTargetingSnapshot
        {
            InputFlow = BattleSkillInputFlow.SelectActorOrCell,
            TargetKind = BattleSkillTargetKind.ActorOrCell,
            Range = 8,
            RangeMetric = BattleSkillRangeMetric.Manhattan,
            PreviewProfileId = "diamond_range"
        },
        Timing = new BattleSkillTimingSnapshot(),
        InterruptPolicy = new BattleSkillInterruptPolicySnapshot
        {
            CanCancelBasicAttackRecovery = true,
            ReleasesWithoutOccupyingCaster = true
        },
        HasInterruptPolicy = true,
        CanCancelBasicAttackRecovery = true,
        ReleasesWithoutOccupyingCaster = true,
        Costs = { new LimitedUseSkillCostSnapshot { MaxUses = 1 } },
        Cooldown = new NoCooldownSkillCooldownSnapshot(),
        Presentation = new BattleSkillPresentationSnapshot { ProfileId = "skill_mark_projectile" }
    };
    skill.Effects.Add(new DamageSkillEffectSnapshot { BaseDamage = 12 });
    skill.Effects.Add(new CreateMarkSkillEffectSnapshot
    {
        MarkKind = BattleSkillMarkKind.ThunderMark,
        LifetimeSeconds = 8,
        AttachToActorWhenTargeted = true
    });
    return skill;
}

internal static void BattleRuntimePauseTargetClickSubmitsIntentWithoutAdvancingRuntime()
{
    string rootSource = ReadWorldSiteRootSource();
    string submitBody = ExtractMethodBody(rootSource, "private void SubmitBattleRuntimeHeroSkillCommand(");
    string targetInputBody = ExtractMethodBody(rootSource, "private bool TryHandleBattleRuntimeHeroSkillTargetInput(InputEvent inputEvent)");

    AssertTrue(
        submitBody.Contains("_activeBattleGroupRuntimeResolution?.RuntimeController?.SubmitCommand(commandRequest)", StringComparison.Ordinal),
        "pause-time skill target click should submit command intent to runtime");
    AssertTrue(
        !submitBody.Contains("AdvanceFixedTick", StringComparison.Ordinal) &&
        !targetInputBody.Contains("AdvanceFixedTick", StringComparison.Ordinal),
        "pause-time skill target click must not advance runtime");
    AssertTrue(
        !submitBody.Contains("SetBattleRuntimeCommandPauseActive(false", StringComparison.Ordinal) &&
        !targetInputBody.Contains("SetBattleRuntimeCommandPauseActive(false", StringComparison.Ordinal),
        "pause-time skill target click must not resume or unpause battle");
}

internal static void BattleRuntimeHeroSkillTargetPickingUsesStaticHighlightOverlayAndClearsOnExit()
{
    string rootSource = ReadWorldSiteRootSource();
    string beginBody = ExtractMethodBody(rootSource, "private void BeginBattleRuntimeHeroSkillTargetPicking(BattleRuntimeCommandGroupView selected, string skillDefinitionId)");
    string refreshBody = ExtractMethodBody(rootSource, "private void RefreshBattleRuntimeHeroSkillTargetPreview()");
    string cancelBody = ExtractMethodBody(rootSource, "private void CancelBattleRuntimeHeroSkillTargetPicking(");
    string pauseBody = ExtractMethodBody(rootSource, "private void SetBattleRuntimeCommandPauseActive(bool paused, string reason)");

    AssertTrue(
        beginBody.Contains("_battleRuntimeHeroSkillTargetPickingActive = true", StringComparison.Ordinal) &&
        beginBody.Contains("RefreshBattleRuntimeHeroSkillTargetPreview()", StringComparison.Ordinal),
        "skill button should enter an explicit target-picking state and refresh preview once");
    AssertTrue(
        refreshBody.Contains("_highlightOverlay?.SetCellsBatch", StringComparison.Ordinal) &&
        refreshBody.Contains("BattleGridHighlightKind.Skill", StringComparison.Ordinal) &&
        refreshBody.Contains("BattleGridHighlightKind.Target", StringComparison.Ordinal) &&
        refreshBody.Contains("_unitRoot?.SetAttackTargetPreviewByEntityId(targetActorId)", StringComparison.Ordinal) &&
        !refreshBody.Contains("_highlightOverlay?.SetTargetPointers", StringComparison.Ordinal),
        "target-picking preview should use static skill range cells plus unit-level target focus, not pointer arrows");
    AssertTrue(
        cancelBody.Contains("_highlightOverlay?.ClearCells(BattleGridHighlightKind.Skill)", StringComparison.Ordinal) &&
        cancelBody.Contains("_highlightOverlay?.ClearCells(BattleGridHighlightKind.Target)", StringComparison.Ordinal) &&
        cancelBody.Contains("_unitRoot?.ClearAttackTargetPreview()", StringComparison.Ordinal),
        "leaving target-picking mode should clear skill range, target lock ring, and unit target focus");
    AssertTrue(
        pauseBody.Contains("CancelBattleRuntimeHeroSkillTargetPicking(\"pause_off\")", StringComparison.Ordinal),
        "turning tactical pause off should exit target-picking preview state");
}

private static BattleEntity BuildHeroSkillTargetEntity(string entityId, BattleFaction faction)
{
    BattleEntity entity = CreateUninitialized<BattleEntity>();
    entity.EntityId = entityId;
    var components = new Dictionary<Type, BattleEntityComponent>
    {
        [typeof(FactionComponent)] = CreateUninitialized<FactionComponent>(),
        [typeof(GridOccupantComponent)] = CreateUninitialized<GridOccupantComponent>(),
        [typeof(TargetableComponent)] = CreateUninitialized<TargetableComponent>()
    };
    ((FactionComponent)components[typeof(FactionComponent)]).Faction = faction;
    ((TargetableComponent)components[typeof(TargetableComponent)]).IsTargetable = true;

    typeof(BattleEntity)
        .GetField("_components", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
        .SetValue(entity, components);
    return entity;
}

private static BattleForceRequest BuildStrategicParticipantForce(
    string forceId,
    string unitDefinitionId,
    string participantId,
    int count,
    int x,
    int y)
{
    return new BattleForceRequest
    {
        ForceId = forceId,
        CommandGroupId = participantId,
        SourceKind = "StrategicExpeditionParticipant",
        SourceId = participantId,
        UnitDefinitionId = unitDefinitionId,
        StrategicParticipantId = participantId,
        Count = count,
        FactionId = "player",
        PreferredPlacements =
        {
            new BattleForcePlacementRequest
            {
                PlacementId = $"{forceId}:preferred",
                CellX = x,
                CellY = y,
                CellHeight = 0
            }
        }
    };
}

private static string ResolveFirstRuntimeHudGroupKey(BattleStartRequest request)
{
    Type modelType = typeof(BattleEntity).Assembly.GetType(
        "Rpg.Presentation.World.Sites.BattleRuntimeCommandHudModel",
        throwOnError: true)!;
    System.Reflection.MethodInfo method = modelType.GetMethod(
        "BuildPlayerGroups",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
    object? result = method.Invoke(null, new object?[]
    {
        request,
        new Func<string, string>(unitId => unitId)
    });
    IEnumerable groups = result as IEnumerable ?? Array.Empty<object>();
    object firstGroup = groups.Cast<object>().Single();
    return firstGroup.GetType().GetProperty("GroupKey")?.GetValue(firstGroup) as string ?? "";
}

private static T CreateUninitialized<T>() where T : class
{
    return (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
}
}
