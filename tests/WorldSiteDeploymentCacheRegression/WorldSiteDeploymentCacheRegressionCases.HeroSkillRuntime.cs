using Rpg.Application.Battle;
using Rpg.Application.Battle.Commands;
using Rpg.Application.Battle.Snapshots;
using Rpg.Application.World;
using Rpg.Definitions.World;
using Rpg.Domain.Battle.Grid;
using Rpg.Domain.World;
using Rpg.Presentation.Battle.Entities;
using System.Runtime.CompilerServices;

internal static partial class WorldSiteDeploymentCacheRegressionCases
{
internal static void BattleGroupProbeSnapshotIncludesFirstSliceHeroSkillDefinition()
{
    const string armyId = "army_first_slice_archer";
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
    AssertEqual(3, result.Snapshot.SkillDefinitions.Count, "probe snapshot should carry one active skill per first-slice hero");
    AssertFirstSliceSkillSnapshot(result.Snapshot.SkillDefinitions, "first_slice_skill_shield_barrier", "曦盾结界", "f1_grandmasterzir", 12);
    AssertFirstSliceSkillSnapshot(result.Snapshot.SkillDefinitions, "first_slice_skill_sun_piercer", "贯日一击", "f1_windbladecommander", 18);
    AssertFirstSliceSkillSnapshot(result.Snapshot.SkillDefinitions, "first_slice_skill_whirling_break", "回旋破阵", "f1_elyxstormblade", 16);
}

internal static void BattleSkillDefinitionsLiveInContentLayerAndMapToSnapshots()
{
    string root = ProjectRoot();
    string definitionsSource = File.ReadAllText(Path.Combine(
        root,
        "src",
        "Definitions",
        "Battle",
        "Skills",
        "FirstSliceBattleSkillDefinitions.cs"));
    string snapshotFactorySource = File.ReadAllText(Path.Combine(
        root,
        "src",
        "Application",
        "Battle",
        "Snapshots",
        "BattleSkillSnapshotFactory.cs"));
    string runtimeResolverSource = File.ReadAllText(Path.Combine(
        root,
        "src",
        "Runtime",
        "Battle",
        "BattleRuntimeHeroSkillCommandResolver.cs"));

    AssertTrue(
        definitionsSource.Contains("HeroSkillCommandIds.ShieldBarrierSkillId", StringComparison.Ordinal) &&
        definitionsSource.Contains("HeroSkillCommandIds.SunPiercerSkillId", StringComparison.Ordinal) &&
        definitionsSource.Contains("HeroSkillCommandIds.WhirlingBreakSkillId", StringComparison.Ordinal) &&
        definitionsSource.Contains("CasterUnitIds", StringComparison.Ordinal) &&
        definitionsSource.Contains("BattleSkillTargetingMode.TargetedActor", StringComparison.Ordinal) &&
        definitionsSource.Contains("Range = 8", StringComparison.Ordinal) &&
        definitionsSource.Contains("Amount = 18", StringComparison.Ordinal),
        "first-slice hero skill data and caster bindings should be defined in the content/definition layer");
    AssertTrue(
        snapshotFactorySource.Contains("FirstSliceBattleSkillDefinitions.CreateSelectedHeroSkills", StringComparison.Ordinal) &&
        snapshotFactorySource.Contains("CasterUnitIds", StringComparison.Ordinal) &&
        snapshotFactorySource.Contains("new BattleSkillSnapshot", StringComparison.Ordinal),
        "application snapshot factory should translate definitions and caster bindings into runtime snapshots");
    AssertTrue(
        !runtimeResolverSource.Contains("FirstSliceBattleSkillDefinitions", StringComparison.Ordinal) &&
        runtimeResolverSource.Contains("skill_caster_not_allowed", StringComparison.Ordinal),
        "runtime skill resolver must consume snapshot data and reject skills not bound to the caster group");
}

internal static void BattleRuntimeHudFiltersSkillsToSelectedHeroCompany()
{
    string rootSource = ReadWorldSiteRootSource();
    string targetPresentationSource = File.ReadAllText(Path.Combine(
        ProjectRoot(),
        "src",
        "Presentation",
        "World",
        "Sites",
        "BattleRuntimeHeroSkillTargetPresentation.cs"));

    AssertTrue(
        rootSource.Contains("BuildBattleRuntimeSkillSnapshots(selected)", StringComparison.Ordinal) &&
        rootSource.Contains("IsBattleRuntimeSkillAvailableForGroup", StringComparison.Ordinal) &&
        rootSource.Contains("skill.CasterUnitIds", StringComparison.Ordinal),
        "battle runtime HUD should filter skill snapshots by the selected hero company's bound hero unit");
    AssertTrue(
        targetPresentationSource.Contains("FirstSliceHeroCompanyIds.IsHeroUnit", StringComparison.Ordinal),
        "target picking should recognize every first-slice hero unit as the preferred visible caster");
}

private static void AssertFirstSliceSkillSnapshot(
    IReadOnlyList<BattleSkillSnapshot> skills,
    string expectedSkillId,
    string expectedDisplayName,
    string expectedCasterUnitId,
    int expectedDamage)
{
    BattleSkillSnapshot skill = skills.FirstOrDefault(item => item.SkillId == expectedSkillId);
    AssertTrue(skill != null, $"snapshot should include skill {expectedSkillId}");
    AssertEqual(expectedDisplayName, skill.DisplayName, $"{expectedSkillId} display name");
    AssertEqual(BattleSkillTargetingMode.TargetedActor, skill.TargetingMode, $"{expectedSkillId} targeting mode");
    AssertEqual(8, skill.Range, $"{expectedSkillId} command acceptance range");
    AssertTrue(skill.CanInterruptBasicAttackWindup, $"{expectedSkillId} should interrupt basic attack windup");
    AssertTrue(!skill.CanCancelBasicAttackRecovery, $"{expectedSkillId} should not cancel basic attack recovery by default");
    BattleSkillEffectSnapshot effect = skill.Effects.FirstOrDefault();
    AssertTrue(effect != null, $"{expectedSkillId} should expose one damage effect snapshot");
    AssertEqual(BattleSkillEffectKind.Damage, effect.Kind, $"{expectedSkillId} effect kind");
    AssertEqual(expectedDamage, effect.Amount, $"{expectedSkillId} damage payload");

    System.Reflection.PropertyInfo casterUnitIdsProperty = typeof(BattleSkillSnapshot).GetProperty("CasterUnitIds");
    AssertTrue(casterUnitIdsProperty != null, "battle skill snapshots should carry caster unit bindings");
    object value = casterUnitIdsProperty.GetValue(skill);
    IEnumerable<string> casterUnitIds = value as IEnumerable<string> ?? Array.Empty<string>();
    AssertTrue(
        casterUnitIds.Contains(expectedCasterUnitId, StringComparer.Ordinal),
        $"{expectedSkillId} should be bound to caster unit {expectedCasterUnitId}");
}

internal static void WorldSiteBattleRuntimeHeroSkillTargetClickBuildsTargetedCommand()
{
    string rootSource = ReadWorldSiteRootSource();
    string inputBody = ExtractMethodBody(rootSource, "public override void _Input(InputEvent @event)");
    string targetInputBody = ExtractMethodBody(rootSource, "private bool TryHandleBattleRuntimeHeroSkillTargetInput(InputEvent inputEvent)");
    string submitBody = ExtractMethodBody(rootSource, "private void SubmitBattleRuntimeHeroSkillCommand(");
    string buildBody = ExtractMethodBody(rootSource, "private CommandRequest BuildBattleRuntimeHeroSkillCommandRequest(");

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
        buildBody.Contains("TargetActorId = targetActorId", StringComparison.Ordinal),
        "hero skill submit should pass the clicked target actor id into the command request");
}

internal static void WorldSiteBattleRuntimeHeroSkillTargetClickBuildsCasterScopedCommand()
{
    string rootSource = ReadWorldSiteRootSource();
    string targetInputBody = ExtractMethodBody(rootSource, "private bool TryHandleBattleRuntimeHeroSkillTargetInput(InputEvent inputEvent)");
    string buildBody = ExtractMethodBody(rootSource, "private CommandRequest BuildBattleRuntimeHeroSkillCommandRequest(");
    string refreshBody = ExtractMethodBody(rootSource, "private void RefreshBattleRuntimeHeroSkillTargetPreview()");

    AssertTrue(
        targetInputBody.Contains("TryResolveBattleRuntimeHeroSkillSourceActorId", StringComparison.Ordinal),
        "target click should resolve the selected visible caster before submitting a hero skill command");
    AssertTrue(
        buildBody.Contains("SourceActorId = sourceActorId", StringComparison.Ordinal),
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
    string beginBody = ExtractMethodBody(rootSource, "private void BeginBattleRuntimeHeroSkillTargetPicking(BattleRuntimeCommandGroupView selected, string skillId)");
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

private static T CreateUninitialized<T>() where T : class
{
    return (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
}
}
