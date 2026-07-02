using Rpg.Application.Battle;
using Rpg.Application.Battle.Adapters;
using Rpg.Application.Battle.Commands;
using Rpg.Application.Battle.Reports;
using Rpg.Application.Battle.Settlement;
using Rpg.Application.Battle.Snapshots;
using Rpg.Application.World;
using Rpg.Application.BattleGroups;
using Rpg.Domain.BattleGroups;
using Rpg.Domain.Corps;
using Rpg.Domain.Heroes;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Results;
Environment.SetEnvironmentVariable("RPG_GAMELOG_DIR", Path.Combine(Path.GetTempPath(), "rpg-test-logs"));
TargetBattleGroupTacticalRegionRegressionCases.Register(Run);
TargetBattleCenteredRegionRegressionCases.Register(Run);
TargetBattleCombatZoneRegressionCases.Register(Run);
Run("corps strength clamps and visible soldiers are derived", CorpsStrengthClampsAndVisibleSoldiersAreDerived);
Run("runtime source stays isolated from domain and presentation owners", RuntimeSourceStaysIsolated); Run("runtime tick resolver stays slim after TD-001 decomposition", TargetBattleTickResolverDecompositionGuard.RuntimeTickResolverStaysSlimAfterTd001Decomposition); Run("runtime tick resolver delegates attack and movement mutation to services", TargetBattleTickResolverDecompositionGuard.RuntimeTickResolverDelegatesAttackAndMovementMutationToServices);
Run("oversized code files are tracked and no new ones are introduced", OversizedCodeFilesAreTrackedAndNoNewOnesAreIntroduced);
Run("runtime owns stable in-memory actor state", RuntimeOwnsStableInMemoryActorState);
Run("runtime auto battle resolves opposing factions from actor state", RuntimeAutoBattleResolvesOpposingFactionsFromActorState);
Run("runtime uses 8-neighbor square-grid movement", RuntimeUsesEightNeighborSquareGridMovement);
Run("runtime diagonal adjacency is not in basic attack range", RuntimeDiagonalAdjacencyIsNotInBasicAttackRange);
Run("runtime attack speed gates attack cadence", RuntimeAttackSpeedGatesAttackCadence);
Run("runtime adjacent opponents resolve same-tick attacks without actor-id initiative", TargetBattleAttackCadenceRegressionCases.RuntimeAdjacentOpponentsResolveSameTickAttacksWithoutActorIdInitiative);
Run("runtime mover cannot attack until anchored on later tick", TargetBattleAttackCadenceRegressionCases.RuntimeMoverCannotAttackUntilAnchoredOnLaterTick);
Run("runtime defeated actor move proposal is discarded after same-tick damage", TargetBattleAttackCadenceRegressionCases.RuntimeDefeatedActorMoveProposalIsDiscardedAfterSameTickDamage);
Run("runtime does not burst stored attack charge after approach", TargetBattleAttackCadenceRegressionCases.RuntimeDoesNotBurstStoredAttackChargeAfterApproach);
Run("runtime attack recovery prevents consecutive tick damage", TargetBattleAttackCadenceRegressionCases.RuntimeAttackRecoveryPreventsConsecutiveTickDamage);
Run("runtime attack cadence uses actor action seconds", TargetBattleAttackCadenceRegressionCases.RuntimeAttackCadenceUsesActorActionSeconds);
Run("runtime movement cadence uses move step seconds", TargetBattleAttackCadenceRegressionCases.RuntimeMovementCadenceUsesMoveStepSeconds);
Run("runtime movement phase allows next-tick movement continuation", TargetBattleAttackCadenceRegressionCases.RuntimeMovementPhaseAllowsNextTickMovementContinuation); Run("runtime fixed-clock hands off next movement same tick after boundary", TargetBattleAttackCadenceRegressionCases.RuntimeFixedClockHandsOffNextMovementSameTickAfterBoundary); Run("runtime fixed-clock defers attack after movement boundary", TargetBattleAttackCadenceRegressionCases.RuntimeFixedClockDefersAttackAfterMovementBoundary); TargetBattleContinuousStepHandoffRegressionCases.Register(Run); TargetBattleCommitBufferRegressionCases.Register(Run); TargetBattleEffectCommitBufferRegressionCases.Register(Run); TargetBattleActionPhaseCoordinatorRegressionCases.Register(Run); TargetBattleDecisionPhaseCoordinatorRegressionCases.Register(Run); TargetBattleResolutionPhaseCoordinatorRegressionCases.Register(Run);
Run("runtime session begin defers combat resolution until advance", TargetBattleAttackCadenceRegressionCases.RuntimeSessionBeginDefersCombatResolutionUntilAdvance);
TargetBattleActionDiagnosticsRegressionCases.Register(Run); Run("runtime action diagnostics are logged", TargetBattleAttackCadenceRegressionCases.RuntimeActionDiagnosticsAreLogged);
Run("runtime uses snapshot combat hit points and attack damage", TargetBattleAttackCadenceRegressionCases.RuntimeUsesSnapshotCombatHitPointsAndAttackDamage); Run("runtime event stream order golden locks attack movement plan and defeat", TargetBattleEventOrderGoldenRegressionCases.RuntimeEventStreamOrderGoldenLocksAttackMovementPlanAndDefeat); Run("runtime multi-defeat dictionary order golden locks current order", TargetBattleEventOrderGoldenRegressionCases.RuntimeMultiDefeatDictionaryOrderGoldenLocksCurrentOrder); Run("runtime attack stream slice golden locks engagement before movement", TargetBattleEventOrderGoldenRegressionCases.RuntimeAttackStreamSliceGoldenLocksEngagementBeforeMovement); Run("runtime retarget order golden locks dead target retarget without stream writes", TargetBattleEventOrderGoldenRegressionCases.RuntimeRetargetOrderGoldenLocksDeadTargetRetargetWithoutStreamWrites); Run("runtime reservation battle group id tiebreak golden locks move order", TargetBattleEventOrderGoldenRegressionCases.RuntimeReservationBattleGroupIdTiebreakGoldenLocksMoveOrder); Run("runtime failed attack contexts golden locks failure results without combat events", TargetBattleEventOrderGoldenRegressionCases.RuntimeFailedAttackContextsGoldenLocksFailureResultsWithoutCombatEvents); TargetBattleEventOrderGoldenRegressionCases.RegisterTd002SliceCGoldens(Run);
TargetBattleRuntimeCorrectnessRegressionCases.Register(Run); Run("runtime movement events carry authoritative cells", RuntimeMovementEventsCarryAuthoritativeCells);
Run("runtime hold-line command keeps player corps from advancing", TargetBattleCommandRegressionCases.RuntimeHoldLineCommandKeepsPlayerCorpsFromAdvancing);
Run("runtime focus-fire command targets lowest-health enemy corps", TargetBattleCommandRegressionCases.RuntimeFocusFireCommandTargetsLowestHealthEnemyCorps);
TargetBattleHeroSkillRegressionCases.Register(Run);
TargetBattleDisplacementCommitBoundaryRegressionCases.Register(Run); TargetBattleThunderMarkSkillRegressionCases.Register(Run);
TargetBattleSkillConfigurationAuthorityRegressionCases.Register(Run);
Run("runtime AI executor boundary uses typed requests", TargetBattleAiRuntimeRegressionCases.RuntimeAiExecutorBoundaryUsesTypedRequests);
Run("runtime AI executor consumes facts without mutable runtime authority", TargetBattleAiRuntimeRegressionCases.RuntimeAiExecutorConsumesFactsWithoutMutableRuntimeAuthority);
Run("runtime AI executor delegates to behavior tree boundary", TargetBattleAiRuntimeRegressionCases.RuntimeAiExecutorDelegatesToBehaviorTreeBoundary);
Run("runtime behavior tree selects target from candidate facts", TargetBattleAiRuntimeRegressionCases.RuntimeBehaviorTreeSelectsTargetFromCandidateFacts);
Run("runtime tick resolver does not preselect ordinary targets before behavior tree", TargetBattleAiRuntimeRegressionCases.RuntimeTickResolverDoesNotPreselectOrdinaryTargetsBeforeBehaviorTree);
Run("runtime behavior tree nodes use selector and sequence semantics", TargetBattleAiRuntimeRegressionCases.RuntimeBehaviorTreeNodesUseSelectorAndSequenceSemantics);
Run("runtime behavior tree preserves local combat request order", TargetBattleAiRuntimeRegressionCases.RuntimeBehaviorTreePreservesLocalCombatRequestOrder);
Run("battle grid map reader does not consume complex tileset navigation data", TargetBattleNavigationRegressionCases.BattleGridMapReaderDoesNotConsumeComplexTileSetNavigationData);
Run("battle tilesets only expose walkable navigation custom data", TargetBattleNavigationRegressionCases.BattleTileSetsOnlyExposeWalkableNavigationCustomData);
Run("battle navigation topology compiler produces final edges before runtime", TargetBattleNavigationRegressionCases.BattleNavigationTopologyCompilerProducesFinalEdgesBeforeRuntime);
Run("runtime navigation graph consumes topology data layer only", TargetBattleNavigationRegressionCases.RuntimeNavigationGraphConsumesTopologyDataLayerOnly);
Run("runtime navigation graph does not fallback from production create to actors", TargetBattleNavigationRegressionCases.RuntimeNavigationGraphDoesNotFallbackFromProductionCreateToActors);
Run("runtime navigation main loop uses local neighbor planner instead of actor astar", TargetBattleNavigationRegressionCases.RuntimeNavigationMainLoopUsesLocalNeighborPlannerInsteadOfActorAStar);
Run("battle navigation topology diagnostics print nodes edges and placements", TargetBattleNavigationRegressionCases.BattleNavigationTopologyDiagnosticsPrintNodesEdgesAndPlacements);
Run("battle navigation snapshot builder excludes underground water from topology", TargetBattleNavigationRegressionCases.BattleNavigationSnapshotBuilderExcludesUndergroundWaterFromTopology);
Run("battle navigation snapshot builder excludes negative height fallback surfaces", TargetBattleNavigationRegressionCases.BattleNavigationSnapshotBuilderExcludesNegativeHeightFallbackSurfaces);
Run("runtime navigation consumes authored surface snapshot", TargetBattleNavigationRegressionCases.RuntimeNavigationConsumesAuthoredSurfaceSnapshot);
Run("runtime navigation changes height only through authored connections", TargetBattleNavigationRegressionCases.RuntimeNavigationChangesHeightOnlyThroughAuthoredConnections);
Run("runtime navigation diagnostics explain unreachable advance", TargetBattleNavigationRegressionCases.RuntimeNavigationDiagnosticsExplainUnreachableAdvance);
Run("battle navigation snapshot builder exports uniform cost for walkable surfaces", TargetBattleNavigationRegressionCases.BattleNavigationSnapshotBuilderExportsUniformCostForWalkableSurfaces);
Run("runtime navigation rejects diagonal corner cutting", TargetBattleNavigationRegressionCases.RuntimeNavigationRejectsDiagonalCornerCutting);
Run("runtime navigation rejects projected diagonal corner cutting", TargetBattleNavigationRegressionCases.RuntimeNavigationRejectsProjectedDiagonalCornerCutting);
Run("runtime navigation keeps top corridor instead of dipping into lower protrusion", TargetBattleNavigationRegressionCases.RuntimeNavigationKeepsTopCorridorInsteadOfDippingIntoLowerProtrusion);
Run("runtime navigation keeps second ally on top corridor instead of lower protrusion", TargetBattleNavigationRegressionCases.RuntimeNavigationKeepsSecondAllyOnTopCorridorInsteadOfLowerProtrusion);
Run("runtime square-grid combat avoids physics and full-map pathfinding authority", RuntimeSquareGridCombatAvoidsPhysicsAndFullMapPathfindingAuthority);
Run("runtime copies snapshot footprint to corps actors", TargetBattleFootprintRegressionCases.RuntimeCopiesSnapshotFootprintToCorpsActors);
Run("runtime footprint range uses rectangle edges", TargetBattleFootprintRegressionCases.RuntimeFootprintRangeUsesRectangleEdges);
Run("runtime footprint corner adjacency is not attack range", TargetBattleFootprintRegressionCases.RuntimeFootprintCornerAdjacencyIsNotAttackRange);
Run("runtime large attacker uses footprint edge for orthogonal attack", TargetBattleFootprintRegressionCases.RuntimeLargeAttackerUsesFootprintEdgeForOrthogonalAttack);
Run("runtime footprint occupancy blocks covered cells", TargetBattleFootprintRegressionCases.RuntimeFootprintOccupancyBlocksCoveredCells);
Run("runtime pathfinder routes around blocked anchor", TargetBattleFootprintRegressionCases.RuntimePathfinderRoutesAroundBlockedAnchor);
Run("runtime pathfinder routes around large unit interior", TargetBattleFootprintRegressionCases.RuntimePathfinderRoutesAroundLargeUnitInterior);
Run("runtime large footprint cannot move onto anchor with missing covered surface", TargetBattleFootprintRegressionCases.RuntimeLargeFootprintCannotMoveOntoAnchorWithMissingCoveredSurface);
Run("runtime backline advances behind blocked frontline", TargetBattleCongestionRegressionCases.RuntimeBacklineAdvancesBehindBlockedFrontline);
Run("runtime future occupancy does not force immediate detour", TargetBattleCongestionRegressionCases.RuntimeFutureOccupancyDoesNotForceImmediateDetour);
Run("runtime projected occupancy allows direct first step then replans", TargetBattleCongestionRegressionCases.RuntimeProjectedOccupancyAllowsDirectFirstStepThenReplans);
Run("runtime tries alternate same-tick reservation candidate", TargetBattleCongestionRegressionCases.RuntimeTriesAlternateSameTickReservationCandidate);
Run("runtime can switch assault target for faster attack opportunity", TargetBattleMovementIntentRegressionCases.RuntimeCanSwitchAssaultTargetForFasterAttackOpportunity);
Run("runtime mover retargets when target dies before movement resolves", TargetBattleMovementIntentRegressionCases.RuntimeMoverRetargetsWhenTargetDiesBeforeMovementResolves);
Run("runtime support unit does not move away from engaged target for far flank", TargetBattleMovementIntentRegressionCases.RuntimeSupportUnitDoesNotMoveAwayFromEngagedTargetForFarFlank);
Run("runtime assault target selection prefers fastest attack opportunity", TargetBattleMovementIntentRegressionCases.RuntimeAssaultTargetSelectionPrefersFastestAttackOpportunity);
TargetBattleMultiUnitNavigationRegressionCases.Register(Run);
TargetBattleBonefieldPacingRegressionCases.Register(Run);
TargetBattleLocalCombatPositionRegressionCases.Register(Run);
TargetBattleMovementIntentRegressionRegistration.Register(Run);
Run("runtime performance counters separate navigation and logging costs", TargetBattlePerformanceRegressionCases.RuntimePerformanceCountersSeparateNavigationAndLoggingCosts);
Run("runtime combat slot scans stay bounded near target on large topology", TargetBattlePerformanceRegressionCases.RuntimeCombatSlotScansStayBoundedNearTargetOnLargeTopology); Run("runtime local combat position selection uses local neighbor resolver", TargetBattlePerformanceRegressionCases.RuntimeLocalCombatPositionSelectionUsesLocalNeighborResolver); Run("runtime local combat movement does not build flow fields on large topology", TargetBattlePerformanceRegressionCases.RuntimeLocalCombatMovementDoesNotBuildFlowFieldsOnLargeTopology); Run("runtime local combat goal fields are not hot path", TargetBattlePerformanceRegressionCases.RuntimeLocalCombatGoalFieldsAreNotHotPath); Run("runtime navigation hot paths avoid string keys and linq sorts", TargetBattlePerformanceRegressionCases.RuntimeNavigationHotPathsAvoidStringKeysAndLinqSorts); Run("route topology caches region travel from entry anchor", TargetBattlePerformanceRegressionCases.RouteTopologyCachesRegionTravelFromEntryAnchor); Run("runtime spike diagnostics write automatic summary", TargetBattlePerformanceRegressionCases.RuntimeSpikeDiagnosticsWriteAutomaticSummary);
Run("high-frequency battle presentation logs use trace channel", TargetBattlePerformanceRegressionCases.HighFrequencyBattlePresentationLogsUseTraceChannel);
Run("runtime rejects invalid battle handoff", RuntimeRejectsInvalidBattleHandoff);
Run("domain source stays isolated from runtime and Godot scene nodes", DomainSourceStaysIsolated);
Run("snapshot copies battle group facts", SnapshotCopiesBattleGroupFacts);
Run("battle group lifecycle rejects invalid identities", BattleGroupLifecycleRejectsInvalidIdentities);
Run("battle group lifecycle preserves state on invalid lock", BattleGroupLifecyclePreservesStateOnInvalidLock);
Run("battle group lifecycle releases only active battle groups", BattleGroupLifecycleReleasesOnlyActiveBattleGroups);
Run("command validation distinguishes application rejection", CommandValidationDistinguishesApplicationRejection);
Run("settlement rejects incomplete result", SettlementRejectsIncompleteResult);
Run("settlement rejects invalid complete results and missing event boundaries", SettlementRejectsInvalidCompleteResultsAndMissingEventBoundaries);
Run("rejected settlement report is diagnostic", RejectedSettlementReportIsDiagnostic);
Run("report and settlement consume the same event ids", ReportAndSettlementConsumeSameEventIds);
Run("legacy garrison adapter creates explicit battle groups", LegacyGarrisonAdapterCreatesExplicitBattleGroups);
Run("battle group session probe snapshots player and enemy forces", BattleGroupSessionProbeSnapshotsPlayerAndEnemyForces);
Run("battle group session probe copies initial corps command to player snapshot", TargetBattleCommandRegressionCases.BattleGroupSessionProbeCopiesInitialCorpsCommandToPlayerSnapshot); Run("battle group session probe copies battle group plan to player snapshot", TargetBattleCommandRegressionCases.BattleGroupSessionProbeCopiesBattleGroupPlanToPlayerSnapshot); Run("battle group session probe applies per company objective plans", TargetBattleCommandRegressionCases.BattleGroupSessionProbeAppliesPerCompanyObjectivePlans); Run("battle group session probe applies enemy objective plans", TargetBattleCommandRegressionCases.BattleGroupSessionProbeAppliesEnemyObjectivePlans);
Run("legacy result adapter preserves request and outcome ids", LegacyResultAdapterPreservesRequestAndOutcomeIds);
Run("legacy result adapter copies runtime survival into force results", LegacyResultAdapterCopiesRuntimeSurvivalIntoForceResults);
Run("legacy result adapter maps failed handoff to disaster", LegacyResultAdapterMapsFailedHandoffToDisaster);
Run("battle group vertical slice settles and reports from runtime facts", BattleGroupVerticalSliceSettlesAndReports);
Run("mixed valid and missing hero handoff rejects settlement and normal report", MixedValidAndMissingHeroHandoffRejectsSettlementAndNormalReport);
static void CorpsStrengthClampsAndVisibleSoldiersAreDerived()
{
    CorpsState corps = new() { CorpsId = "corps_1", CorpsDefinitionId = "shield", CorpsStrength = 140 };
    corps.ClampStrength();
    AssertEqual(100, corps.CorpsStrength, "strength upper clamp");
    corps.CorpsStrength = -8;
    corps.ClampStrength();
    AssertEqual(0, corps.CorpsStrength, "strength lower clamp");
    corps.CorpsStrength = 80;
    AssertEqual(4, CorpsStrengthPolicy.CalculateVisibleSoldiers(corps.CorpsStrength, 5), "derived visible soldiers");
}
static void RuntimeSourceStaysIsolated()
{
    string source = CombinedSource("src", "Runtime", "Battle");
    AssertTrue(!source.Contains("StrategicWorldState", StringComparison.Ordinal), "runtime must not reference StrategicWorldState");
    AssertTrue(!source.Contains("WorldSiteRoot", StringComparison.Ordinal), "runtime must not reference WorldSiteRoot");
    AssertTrue(!source.Contains("Godot.Control", StringComparison.Ordinal), "runtime must not reference Godot UI controls");
    AssertTrue(!source.Contains("Rpg.Domain", StringComparison.Ordinal), "runtime must not reference Domain owners");
    AssertTrue(!source.Contains("Rpg.Presentation", StringComparison.Ordinal), "runtime must not reference Presentation owners");
    AssertTrue(!source.Contains("using Godot", StringComparison.Ordinal), "runtime must not reference Godot");
    AssertTrue(!source.Contains("Godot.Node", StringComparison.Ordinal), "runtime must not reference scene nodes");
    AssertTrue(!source.Contains("SaveService", StringComparison.Ordinal), "runtime must not reference save services");
    AssertTrue(!source.Contains("StrategicWorldSaveService", StringComparison.Ordinal), "runtime must not reference save services");
    AssertTrue(!source.Contains("BattleStartRequest", StringComparison.Ordinal), "runtime must not reference legacy battle requests");
    AssertTrue(!source.Contains("BattleResult", StringComparison.Ordinal), "runtime must not reference legacy battle results");
    AssertTrue(!source.Contains("AutoBattle", StringComparison.Ordinal), "runtime must not reference old auto battle");
    AssertTrue(!source.Contains("temporary workaround", StringComparison.OrdinalIgnoreCase) && !source.Contains("just for now", StringComparison.OrdinalIgnoreCase) && !source.Contains("throwaway", StringComparison.OrdinalIgnoreCase), "runtime source must not describe core flow as temporary workaround, just for now, or throwaway");
}
static void OversizedCodeFilesAreTrackedAndNoNewOnesAreIntroduced()
{
    string root = ProjectRoot();
    var allowedOversized = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["src/Presentation/Battle/BattleGridHighlightOverlay.cs"] = 1070, ["src/Presentation/Battle/Entities/BattleUnitRoot.cs"] = 1112,
        ["src/Presentation/Battle/Entities/UnitAnimationComponent.cs"] = 1117, ["src/Presentation/World/Sites/WorldSiteRoot.SiteManagementHud.cs"] = 1032,
        ["tests/BattleHitFeedbackRegression/BattleHitFeedbackRegressionCases.BattlePresentation.cs"] = 1162, ["tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.HeroCorps.cs"] = 1306,
        ["tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs"] = 1424
    };
    var oversized = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
        .Where(path => !IsIgnoredCodePath(root, path))
        .Select(path => (RelativePath: ToRepoPath(root, path), LineCount: File.ReadLines(path).Count()))
        .Where(item => item.LineCount > 1000)
        .ToList();
    List<string> unexpected = oversized.Where(item => !allowedOversized.ContainsKey(item.RelativePath)).Select(item => $"{item.RelativePath}:{item.LineCount}").ToList();
    List<string> grown = oversized.Where(item => allowedOversized.TryGetValue(item.RelativePath, out int maxLines) && item.LineCount > maxLines).Select(item => $"{item.RelativePath}:{item.LineCount}>{allowedOversized[item.RelativePath]}").ToList();
    AssertTrue(unexpected.Count == 0, $"new oversized code files must be split or added to the decomposition proposal: {string.Join(", ", unexpected)}");
    AssertTrue(grown.Count == 0, $"tracked oversized code files grew beyond their accepted line budgets: {string.Join(", ", grown)}");
}
static void RuntimeOwnsStableInMemoryActorState()
{
    BattleStartSnapshot snapshot = new()
    {
        SnapshotId = "snapshot_1",
        BattleId = "battle_1",
        TargetLocationId = "site_1",
        BattleGroups =
        {
            new BattleGroupSnapshot
            {
                BattleGroupId = "group_1",
                FactionId = "player",
                SourceForceId = "force_player",
                HeroId = "hero_1",
                HeroDefinitionId = "hero_def_1",
                CorpsId = "corps_1",
                CorpsDefinitionId = "shield",
                CorpsStrength = 80,
                SourceLocationId = "city_1",
                CellX = 2,
                CellY = 3
            }
        }
    };
    TargetBattleTestTopology.CompileAroundGroups(snapshot);
    BattleRuntimeSessionResult result = new BattleRuntimeSession().RunMinimal(snapshot);
    AssertTrue(result.Outcome.IsComplete, "valid snapshot should complete minimal runtime");
    AssertEqual("snapshot_1", result.FinalState.SnapshotId, "runtime state snapshot id");
    AssertEqual(2, result.FinalState.Actors.Count, "one battle group should create hero and corps runtime actors");
    AssertTrue(
        result.FinalState.Actors.Any(actor =>
            actor.Kind == BattleRuntimeActorKind.Hero &&
            actor.SourceStateId == "hero_1"),
        "runtime hero actor should retain source state id for settlement attribution");
    AssertTrue(
        result.FinalState.Actors.Any(actor =>
            actor.Kind == BattleRuntimeActorKind.Corps &&
            actor.SourceStateId == "corps_1" &&
            actor.HitPoints == 80),
        "runtime corps actor should derive hit points from corps strength");
    AssertEqual(2, result.Outcome.ActorOutcomes.Count, "outcome should report runtime actor results");
    AssertTrue(
        result.FinalState.Actors.Any(actor =>
            actor.Kind == BattleRuntimeActorKind.Corps &&
            actor.GridX == 2 &&
            actor.GridY == 3),
        "runtime actor should start from battle snapshot grid placement");
    AssertTrue(
        result.Outcome.ActorOutcomes.All(actor => actor.FactionId == "player" && actor.SourceForceId == "force_player"),
        "runtime outcome should preserve faction and source force attribution");
}
static void RuntimeAutoBattleResolvesOpposingFactionsFromActorState()
{
    BattleStartSnapshot victorySnapshot = BuildOpposedSnapshot("battle_victory", playerStrength: 80, enemyStrength: 20);
    BattleRuntimeSessionResult victory = new BattleRuntimeSession().RunMinimal(victorySnapshot);

    AssertEqual(BattleTerminationReason.NormalVictory, victory.Outcome.TerminationReason, "stronger player side should win");
    AssertTrue(
        victory.EventStream.Events.Any(item => item.Kind == BattleEventKind.MovementCompleted),
        "auto battle should advance units before contact");
    AssertTrue(
        victory.EventStream.Events.Any(item => item.Kind == BattleEventKind.DamageApplied),
        "auto battle should apply damage instead of ending immediately");
    AssertTrue(
        victory.Outcome.ActorOutcomes.Any(item =>
            item.Kind == BattleRuntimeActorKind.Corps &&
            item.FactionId == "enemy" &&
            !item.Survived),
        "enemy corps should be defeated in a player victory");
    BattleStartSnapshot defeatSnapshot = BuildOpposedSnapshot("battle_defeat", playerStrength: 20, enemyStrength: 80);
    BattleRuntimeSessionResult defeat = new BattleRuntimeSession().RunMinimal(defeatSnapshot);
    AssertEqual(BattleTerminationReason.NormalDefeat, defeat.Outcome.TerminationReason, "stronger enemy side should defeat player");
    AssertTrue(
        defeat.Outcome.ActorOutcomes.Any(item =>
            item.Kind == BattleRuntimeActorKind.Corps &&
            item.FactionId == "player" &&
            !item.Survived),
        "player corps should be defeated in a player defeat");
}

static void RuntimeUsesEightNeighborSquareGridMovement()
{
    BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_diagonal_move", 80, 80, enemyCellX: 2, enemyCellY: 2);
    BattleRuntimeSessionResult result = new BattleRuntimeSession().RunMinimal(snapshot);
    BattleEvent firstMove = result.EventStream.Events
        .FirstOrDefault(item => item.Kind == BattleEventKind.MovementCompleted);

    AssertTrue(firstMove != null, "a corps should move before diagonal contact");
    AssertEqual(1, firstMove.ToGridX, "first diagonal move to x");
    AssertEqual(1, firstMove.ToGridY, "first diagonal move to y");
    AssertTrue(
        firstMove.FromGridX != firstMove.ToGridX &&
        firstMove.FromGridY != firstMove.ToGridY,
        "first square-grid approach should use a diagonal neighbor when both axes need closing");
}

static void RuntimeDiagonalAdjacencyIsNotInBasicAttackRange()
{
    BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_diagonal_attack", 80, 80, enemyCellX: 1, enemyCellY: 1);
    BattleRuntimeSessionResult result = new BattleRuntimeSession().RunMinimal(snapshot);
    BattleEvent[] combatEvents = result.EventStream.Events
        .Where(item => item.Kind is BattleEventKind.MovementCompleted or BattleEventKind.DamageApplied)
        .ToArray();

    AssertTrue(combatEvents.Length > 0, "diagonal adjacency should produce combat events");
    AssertEqual(BattleEventKind.MovementCompleted, combatEvents[0].Kind, "diagonal adjacent units should move into an orthogonal attack lane before attacking");
}

static void RuntimeAttackSpeedGatesAttackCadence()
{
    BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_attack_speed", 100, 100, enemyCellX: 1, enemyCellY: 0);
    snapshot.BattleGroups.Single(item => item.SourceForceId == "force_player").AttackSpeed = 0.5;
    BattleRuntimeSessionResult result = new BattleRuntimeSession().RunMinimal(snapshot);

    BattleRuntimeActor playerActor = result.FinalState.Actors.Single(item => item.ActorId == "force_player:1");
    AssertEqual(0.5, playerActor.AttackSpeed, "runtime actor should copy attack speed from snapshot");
    AssertEqual(2.4, playerActor.AttackActionSeconds, "runtime actor should derive slower action seconds from attack speed");

    BattleEvent[] playerDamageEvents = result.EventStream.Events
        .Where(item => item.Kind == BattleEventKind.DamageApplied && item.ActorId == "force_player:1")
        .ToArray();

    AssertTrue(playerDamageEvents.Length >= 2, "slow attack speed should still eventually attack more than once");
    AssertTrue(Math.Abs(playerDamageEvents[0].RuntimeTimeSeconds) <= 0.0001, "initial attack should be ready at contact");
    AssertTrue(
        playerDamageEvents.Skip(1).First().RuntimeTimeSeconds >= 2.4 - 0.0001,
        "0.5 attack speed should wait for the slower action duration before the second attack");
}

static void RuntimeMovementEventsCarryAuthoritativeCells()
{
    BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_move_cells", 80, 80, enemyCellX: 3, enemyCellY: 0);
    BattleRuntimeSessionResult result = new BattleRuntimeSession().RunMinimal(snapshot);

    BattleEvent move = result.EventStream.Events.FirstOrDefault(item => item.Kind == BattleEventKind.MovementCompleted);
    AssertTrue(move != null, "runtime should emit movement");
    AssertTrue(move.HasMovementCells, "movement event should mark authoritative cells");
    AssertTrue(move.FromGridX != move.ToGridX || move.FromGridY != move.ToGridY, "movement event should carry a changed destination");
}

static void RuntimeSquareGridCombatAvoidsPhysicsAndFullMapPathfindingAuthority()
{
    string source = CombinedSource("src", "Runtime", "Battle");
    AssertTrue(!source.Contains("Area2D", StringComparison.Ordinal), "runtime combat must not depend on Godot Area2D authority");
    AssertTrue(!source.Contains("CollisionShape2D", StringComparison.Ordinal), "runtime combat must not depend on Godot collision shapes");
    AssertTrue(!source.Contains("MovementRangeFinder", StringComparison.Ordinal), "runtime combat must not run full-map pathfinding per actor in this slice");
}

static bool IsIgnoredCodePath(string root, string path)
{
    string relative = ToRepoPath(root, path);
    return relative.StartsWith(".godot/", StringComparison.Ordinal) ||
           relative.Contains("/bin/", StringComparison.Ordinal) ||
           relative.Contains("/obj/", StringComparison.Ordinal);
}

static string ToRepoPath(string root, string path)
{
    return Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');
}

static void RuntimeRejectsInvalidBattleHandoff()
{
    AssertInvalidBattleHandoff(new BattleRuntimeSession().RunMinimal(null), "null snapshot");
    AssertInvalidBattleHandoff(new BattleRuntimeSession().RunMinimal(new BattleStartSnapshot()), "blank snapshot ids");
    AssertInvalidBattleHandoff(new BattleRuntimeSession().RunMinimal(new BattleStartSnapshot
    {
        SnapshotId = "snapshot_empty",
        BattleId = "battle_empty"
    }), "empty battle groups");
    AssertInvalidBattleHandoff(new BattleRuntimeSession().RunMinimal(new BattleStartSnapshot
    {
        SnapshotId = "snapshot_null_groups",
        BattleId = "battle_null_groups",
        BattleGroups = null
    }), "null battle groups");
    AssertInvalidBattleHandoff(new BattleRuntimeSession().RunMinimal(new BattleStartSnapshot
    {
        SnapshotId = "snapshot_blank_group",
        BattleId = "battle_blank_group",
        BattleGroups = { new BattleGroupSnapshot() }
    }), "blank battle group payload");
    AssertInvalidBattleHandoff(new BattleRuntimeSession().RunMinimal(new BattleStartSnapshot
    {
        SnapshotId = "snapshot_null_group",
        BattleId = "battle_null_group",
        BattleGroups = { null! }
    }), "null battle group payload");
}

static void AssertInvalidBattleHandoff(BattleRuntimeSessionResult result, string message)
{
    AssertTrue(!result.Outcome.IsComplete, "invalid handoff must not complete");
    AssertEqual(BattleTerminationReason.RuntimeException, result.Outcome.TerminationReason, $"{message} termination");
    AssertTrue(
        result.EventStream.Events.Any(item =>
            (item.Kind == BattleEventKind.CommandRejected || item.Kind == BattleEventKind.BattleEnded)
            && item.ReasonCode == "battle_snapshot_invalid"),
        $"{message} should emit rejection event");
}

static void DomainSourceStaysIsolated()
{
    string source = string.Join("\n", new[]
    {
        CombinedSource("src", "Domain", "Heroes"),
        CombinedSource("src", "Domain", "Corps"),
        CombinedSource("src", "Domain", "BattleGroups"),
        CombinedSource("src", "Domain", "Equipment")
    });
    AssertTrue(!source.Contains("Rpg.Runtime", StringComparison.Ordinal), "domain must not reference runtime");
    AssertTrue(!source.Contains("Godot.Node", StringComparison.Ordinal), "domain must not reference scene nodes");
    AssertTrue(!source.Contains("Godot.Control", StringComparison.Ordinal), "domain must not reference UI controls");
}

static void SnapshotCopiesBattleGroupFacts()
{
    HeroState hero = new() { HeroId = "hero_1", HeroDefinitionId = "hero_def_1", Level = 3 };
    CorpsState corps = new() { CorpsId = "corps_1", CorpsDefinitionId = "shield", Level = 2, CorpsStrength = 77 };
    BattleGroupState group = new()
    {
        BattleGroupId = "group_1",
        HeroId = hero.HeroId,
        CorpsId = corps.CorpsId,
        CurrentLocationId = "city_1",
        Status = BattleGroupStatus.Stationed
    };

    BattleStartSnapshot snapshot = new BattleSnapshotBuilder().Build(
        "snapshot_1",
        "battle_1",
        "site_1",
        new[] { group },
        new Dictionary<string, HeroState> { [hero.HeroId] = hero },
        new Dictionary<string, CorpsState> { [corps.CorpsId] = corps });

    AssertEqual("snapshot_1", snapshot.SnapshotId, "snapshot id");
    AssertEqual("battle_1", snapshot.BattleId, "battle id");
    AssertEqual(1, snapshot.BattleGroups.Count, "battle group count");
    AssertEqual("hero_def_1", snapshot.BattleGroups[0].HeroDefinitionId, "hero definition copied");
    AssertEqual("shield", snapshot.BattleGroups[0].CorpsDefinitionId, "corps definition copied");
    corps.CorpsStrength = 12;
    AssertEqual(77, snapshot.BattleGroups[0].CorpsStrength, "snapshot must not track live domain object");
}

static void BattleGroupLifecycleRejectsInvalidIdentities()
{
    BattleGroupLifecycleService service = new();

    AssertThrows<ArgumentException>(
        () => service.CreateAndStation("", "hero_1", "corps_1", "city_1"),
        "blank group id should throw");
    AssertThrows<ArgumentException>(
        () => service.CreateAndStation("group_1", " ", "corps_1", "city_1"),
        "blank hero id should throw");
    AssertThrows<ArgumentException>(
        () => service.CreateAndStation("group_1", "hero_1", null, "city_1"),
        "blank corps id should throw");
    AssertThrows<ArgumentException>(
        () => service.CreateAndStation("group_1", "hero_1", "corps_1", ""),
        "blank location id should throw");
}

static void BattleGroupLifecyclePreservesStateOnInvalidLock()
{
    BattleGroupLifecycleService service = new();
    BattleGroupState group = new()
    {
        BattleGroupId = "group_1",
        HeroId = "hero_1",
        CorpsId = "corps_1",
        CurrentLocationId = "city_1",
        Status = BattleGroupStatus.Stationed,
        ActiveBattleId = "existing_battle"
    };

    bool locked = service.TryLockForBattle(group, " ");

    AssertTrue(!locked, "blank battle id should not lock");
    AssertEqual(BattleGroupStatus.Stationed, group.Status, "status unchanged");
    AssertEqual("existing_battle", group.ActiveBattleId, "active battle unchanged");

    group.BattleGroupId = "";
    locked = service.TryLockForBattle(group, "battle_1");

    AssertTrue(!locked, "blank group identity should not lock");
    AssertEqual(BattleGroupStatus.Stationed, group.Status, "blank group status unchanged");
    AssertEqual("existing_battle", group.ActiveBattleId, "blank group active battle unchanged");

    group.BattleGroupId = "group_1";
    group.HeroId = "";
    locked = service.TryLockForBattle(group, "battle_1");

    AssertTrue(!locked, "blank hero identity should not lock");
    AssertEqual(BattleGroupStatus.Stationed, group.Status, "blank hero status unchanged");
    AssertEqual("existing_battle", group.ActiveBattleId, "blank hero active battle unchanged");

    group.HeroId = "hero_1";
    group.CorpsId = "";
    locked = service.TryLockForBattle(group, "battle_1");

    AssertTrue(!locked, "blank corps identity should not lock");
    AssertEqual(BattleGroupStatus.Stationed, group.Status, "blank corps status unchanged");
    AssertEqual("existing_battle", group.ActiveBattleId, "blank corps active battle unchanged");

    AssertTrue(!service.TryLockForBattle(null, "battle_1"), "null group should not lock");

    BattleGroupState recovering = new()
    {
        BattleGroupId = "group_recovering",
        HeroId = "hero_1",
        CorpsId = "corps_1",
        Status = BattleGroupStatus.Recovering,
        ActiveBattleId = "recovering_battle"
    };

    locked = service.TryLockForBattle(recovering, "battle_1");

    AssertTrue(!locked, "non sortie group should not lock");
    AssertEqual(BattleGroupStatus.Recovering, recovering.Status, "non sortie status unchanged");
    AssertEqual("recovering_battle", recovering.ActiveBattleId, "non sortie active battle unchanged");
}

static void BattleGroupLifecycleReleasesOnlyActiveBattleGroups()
{
    BattleGroupLifecycleService service = new();
    BattleGroupState recovering = new()
    {
        BattleGroupId = "group_recovering",
        Status = BattleGroupStatus.Recovering,
        ActiveBattleId = "battle_1"
    };
    BattleGroupState stationed = new()
    {
        BattleGroupId = "group_stationed",
        Status = BattleGroupStatus.InBattle,
        CurrentLocationId = "city_1",
        ActiveBattleId = "battle_1"
    };
    BattleGroupState unstationed = new()
    {
        BattleGroupId = "group_unstationed",
        Status = BattleGroupStatus.InBattle,
        ActiveBattleId = "battle_1"
    };

    service.ReleaseAfterBattle(recovering);
    service.ReleaseAfterBattle(stationed);
    service.ReleaseAfterBattle(unstationed);

    AssertEqual(BattleGroupStatus.Recovering, recovering.Status, "non battle status unchanged");
    AssertEqual("battle_1", recovering.ActiveBattleId, "non battle active id unchanged");
    AssertEqual(BattleGroupStatus.Stationed, stationed.Status, "stationed group released to station");
    AssertEqual("", stationed.ActiveBattleId, "stationed group active id cleared");
    AssertEqual(BattleGroupStatus.Available, unstationed.Status, "unstationed group released to available");
    AssertEqual("", unstationed.ActiveBattleId, "unstationed group active id cleared");
}

static void CommandValidationDistinguishesApplicationRejection()
{
    CommandRequest request = new()
    {
        BattleId = "battle_1",
        BattleGroupId = "group_missing",
        Channel = CommandChannel.Corps,
        Kind = CommandKind.Attack
    };

    CommandValidationResult result = new BattleCommandApplicationValidator()
        .Validate(request, new[] { "group_1" }, allowHero: true, allowCorps: true, allowCombined: true);

    AssertTrue(!result.Accepted, "missing group should reject");
    AssertEqual(CommandRejectionStage.Application, result.RejectionStage, "rejection stage");
    AssertEqual("battle_group_unavailable", result.ReasonCode, "reason code");
}

static void SettlementRejectsIncompleteResult()
{
    BattleOutcomeResult result = new()
    {
        SnapshotId = "snapshot_1",
        BattleId = "battle_1",
        IsComplete = false,
        TerminationReason = BattleTerminationReason.RuntimeException
    };

    SettlementPlan plan = new BattleSettlementService().BuildPlan("snapshot_1", result, BattleEventStream.Empty);

    AssertTrue(!plan.Accepted, "incomplete result should reject");
    AssertEqual("battle_result_incomplete", plan.RejectionReason, "rejection reason");
}

static void SettlementRejectsInvalidCompleteResultsAndMissingEventBoundaries()
{
    BattleSettlementService service = new();
    BattleOutcomeResult acceptedShape = BattleOutcomeResult.Completed("snapshot_1", "battle_1", BattleTerminationReason.NormalVictory);
    BattleEventStream matchingEnd = EndedStream("battle_1");

    AssertRejectedPlan(
        service.BuildPlan("snapshot_1", null, matchingEnd),
        "battle_result_missing",
        "null result");
    AssertRejectedPlan(
        service.BuildPlan("snapshot_1", BattleOutcomeResult.Completed("snapshot_1", "", BattleTerminationReason.NormalVictory), matchingEnd),
        "battle_result_missing_battle_id",
        "blank battle id");
    AssertRejectedPlan(
        service.BuildPlan("snapshot_1", BattleOutcomeResult.Completed("snapshot_1", "battle_1", BattleTerminationReason.None), matchingEnd),
        "battle_result_invalid_termination",
        "none termination");
    AssertRejectedPlan(
        service.BuildPlan("snapshot_1", BattleOutcomeResult.Completed("snapshot_1", "battle_1", BattleTerminationReason.RuntimeException), matchingEnd),
        "battle_result_invalid_termination",
        "runtime exception termination");
    AssertRejectedPlan(
        service.BuildPlan("snapshot_1", BattleOutcomeResult.Completed("snapshot_1", "battle_1", BattleTerminationReason.Interrupted), matchingEnd),
        "battle_result_invalid_termination",
        "interrupted termination");
    AssertRejectedPlan(
        service.BuildPlan("snapshot_1", acceptedShape, null),
        "battle_event_boundary_missing",
        "null event stream");
    AssertRejectedPlan(
        service.BuildPlan("snapshot_1", acceptedShape, BattleEventStream.Empty),
        "battle_event_boundary_missing",
        "empty event stream");
    AssertRejectedPlan(
        service.BuildPlan("snapshot_1", acceptedShape, StartedStream("battle_1")),
        "battle_event_boundary_missing",
        "missing battle ended event");
    AssertRejectedPlan(
        service.BuildPlan("snapshot_1", acceptedShape, EndedStream("other_battle")),
        "battle_event_boundary_missing",
        "mismatched battle ended event");
}

static void RejectedSettlementReportIsDiagnostic()
{
    BattleOutcomeResult result = new()
    {
        SnapshotId = "snapshot_1",
        BattleId = "battle_1",
        IsComplete = false,
        TerminationReason = BattleTerminationReason.RuntimeException
    };
    SettlementPlan rejectedPlan = new()
    {
        Accepted = false,
        SnapshotId = "snapshot_1",
        BattleId = "battle_1",
        RejectionReason = "battle_result_incomplete"
    };

    BattleReportRecord report = new BattleReportBuilder().Build(result, StartedStream("battle_1"), rejectedPlan);

    AssertEqual("", report.ReportId, "rejected report must not have normal report id");
    AssertEqual("SettlementRejected", report.OutcomeSummary, "rejected report outcome");
    AssertTrue(report.FailureCandidates.Contains("battle_result_incomplete"), "rejected report failure reason");
}

static void ReportAndSettlementConsumeSameEventIds()
{
    BattleEventStream stream = new();
    stream.Add(new BattleEvent { EventId = "event_1", BattleId = "battle_1", Kind = BattleEventKind.CommandAccepted });
    stream.Add(new BattleEvent { EventId = "event_2", BattleId = "battle_1", Kind = BattleEventKind.DamageApplied });
    stream.Add(new BattleEvent { EventId = "event_3", BattleId = "battle_1", Kind = BattleEventKind.BattleEnded });
    BattleOutcomeResult result = BattleOutcomeResult.Completed("snapshot_1", "battle_1", BattleTerminationReason.NormalVictory);

    SettlementPlan plan = new BattleSettlementService().BuildPlan("snapshot_1", result, stream);
    BattleReportRecord report = new BattleReportBuilder().Build(result, stream, plan);

    AssertSequence(new[] { "event_1", "event_2", "event_3" }, plan.SourceEventIds, "settlement source events");
    AssertSequence(new[] { "event_1", "event_2", "event_3" }, report.SourceEventIds, "report source events");
}

static void LegacyGarrisonAdapterCreatesExplicitBattleGroups()
{
    Rpg.Domain.World.WorldSiteState site = new() { SiteId = "city_1" };
    site.Garrison.Add(new Rpg.Domain.World.GarrisonState { UnitTypeId = StrategicWorldIds.UnitMilitia, Count = 2 });

    Rpg.Application.Battle.Adapters.LegacyBattleGroupSeedAdapter adapter = new();
    IReadOnlyList<BattleGroupState> groups = adapter.SeedFromGarrison(site, "hero_seed");

    AssertEqual(2, groups.Count, "group count");
    AssertEqual("city_1", groups[0].CurrentLocationId, "location copied");
    AssertTrue(groups.All(item => !string.IsNullOrWhiteSpace(item.HeroId)), "hero ids assigned");
    AssertTrue(groups.All(item => !string.IsNullOrWhiteSpace(item.CorpsId)), "corps ids assigned");
}

static void BattleGroupSessionProbeSnapshotsPlayerAndEnemyForces()
{
    BattleStartRequest request = new()
    {
        RequestId = "request_1",
        ContextId = "battle_1",
        TargetSiteId = "site_1"
    };
    request.PlayerForces.Add(new BattleForceRequest
    {
        ForceId = "force_player",
        UnitDefinitionId = "player_corps",
        FactionId = "player",
        Count = 1,
        FootprintWidth = 2,
        FootprintHeight = 2,
        AttackSpeed = 0.75
    });
    request.EnemyForces.Add(new BattleForceRequest
    {
        ForceId = "force_enemy",
        UnitDefinitionId = "enemy_corps",
        FactionId = "enemy",
        Count = 1
    });
    TargetBattleTestTopology.CompileRequestRect(request, -2, -2, 10, 4);

    BattleGroupSessionProbeResult result = new BattleGroupSessionProbeService().Probe(request);

    AssertTrue(result.Success, "probe should accept opposed player and enemy forces");
    AssertEqual(2, result.Snapshot.BattleGroups.Count, "snapshot should include both sides");
    AssertTrue(
        result.Snapshot.BattleGroups.Any(item => item.FactionId == "player" && item.SourceForceId == "force_player"),
        "player force should keep faction and source force identity");
    BattleGroupSnapshot playerGroup = result.Snapshot.BattleGroups.Single(item => item.SourceForceId == "force_player");
    AssertEqual(2, playerGroup.FootprintWidth, "player force footprint width should enter snapshot");
    AssertEqual(2, playerGroup.FootprintHeight, "player force footprint height should enter snapshot");
    AssertEqual(0.75, playerGroup.AttackSpeed, "player force attack speed should enter snapshot");
    AssertTrue(
        result.Snapshot.BattleGroups.Any(item => item.FactionId == "enemy" && item.SourceForceId == "force_enemy"),
        "enemy force should keep faction and source force identity");
}

static void LegacyResultAdapterPreservesRequestAndOutcomeIds()
{
    BattleOutcomeResult outcome = BattleOutcomeResult.Completed("snapshot_1", "battle_1", BattleTerminationReason.NormalVictory);
    Rpg.Application.Battle.BattleResult result = new Rpg.Application.Battle.Adapters.LegacyBattleResultAdapter()
        .ToLegacyResult("request_1", Rpg.Application.Battle.BattleKind.AssaultSite, outcome);

    AssertEqual("request_1", result.RequestId, "legacy request id");
    AssertEqual("battle_1", result.ContextId, "legacy context id");
    AssertEqual(Rpg.Application.Battle.BattleOutcome.Victory, result.Outcome, "legacy outcome");
}

static void LegacyResultAdapterCopiesRuntimeSurvivalIntoForceResults()
{
    Rpg.Application.Battle.BattleStartRequest request = new()
    {
        RequestId = "request_1",
        BattleKind = Rpg.Application.Battle.BattleKind.AssaultSite
    };
    request.PlayerForces.Add(new BattleForceRequest
    {
        ForceId = "force_player",
        SourceKind = "army",
        SourceId = "army_1",
        UnitDefinitionId = "player_corps",
        Count = 2
    });
    request.EnemyForces.Add(new BattleForceRequest
    {
        ForceId = "force_enemy",
        SourceKind = "garrison",
        SourceId = "site_1",
        UnitDefinitionId = "enemy_corps",
        Count = 1
    });
    BattleOutcomeResult outcome = BattleOutcomeResult.Completed("snapshot_1", "battle_1", BattleTerminationReason.NormalVictory);
    outcome.ActorOutcomes.Add(new BattleActorOutcome
    {
        Kind = BattleRuntimeActorKind.Corps,
        SourceForceId = "force_player",
        Survived = true
    });
    outcome.ActorOutcomes.Add(new BattleActorOutcome
    {
        Kind = BattleRuntimeActorKind.Corps,
        SourceForceId = "force_player",
        Survived = false
    });
    outcome.ActorOutcomes.Add(new BattleActorOutcome
    {
        Kind = BattleRuntimeActorKind.Corps,
        SourceForceId = "force_enemy",
        Survived = false
    });

    Rpg.Application.Battle.BattleResult result = new LegacyBattleResultAdapter()
        .ToLegacyResult(request, outcome);

    BattleForceResult player = result.ForceResults.Single(item => item.ForceId == "force_player");
    BattleForceResult enemy = result.ForceResults.Single(item => item.ForceId == "force_enemy");
    AssertEqual(2, player.InitialCount, "player initial count");
    AssertEqual(1, player.SurvivedCount, "player survived count");
    AssertEqual(1, player.DefeatedCount, "player defeated count");
    AssertEqual(1, enemy.InitialCount, "enemy initial count");
    AssertEqual(0, enemy.SurvivedCount, "enemy survived count");
    AssertEqual(1, enemy.DefeatedCount, "enemy defeated count");
}
static void LegacyResultAdapterMapsFailedHandoffToDisaster()
{
    LegacyBattleResultAdapter adapter = new();

    Rpg.Application.Battle.BattleResult nullOutcome = adapter
        .ToLegacyResult("request_null", Rpg.Application.Battle.BattleKind.AssaultSite, null);

    BattleOutcomeResult runtimeExceptionOutcome = new()
    {
        SnapshotId = "snapshot_1",
        BattleId = "battle_1",
        IsComplete = false,
        TerminationReason = BattleTerminationReason.RuntimeException
    };
    Rpg.Application.Battle.BattleResult incompleteRuntimeException = adapter
        .ToLegacyResult("request_runtime_exception", Rpg.Application.Battle.BattleKind.AssaultSite, runtimeExceptionOutcome);

    AssertEqual(Rpg.Application.Battle.BattleOutcome.Disaster, nullOutcome.Outcome, "null outcome maps to disaster");
    AssertTrue(nullOutcome.Outcome != Rpg.Application.Battle.BattleOutcome.Victory, "null outcome must not map to victory");
    AssertTrue(nullOutcome.Outcome != Rpg.Application.Battle.BattleOutcome.Defeat, "null outcome must not map to defeat");
    AssertEqual(Rpg.Application.Battle.BattleOutcome.Disaster, incompleteRuntimeException.Outcome, "incomplete runtime exception maps to disaster");
    AssertTrue(incompleteRuntimeException.Outcome != Rpg.Application.Battle.BattleOutcome.Victory, "incomplete runtime exception must not map to victory");
    AssertTrue(incompleteRuntimeException.Outcome != Rpg.Application.Battle.BattleOutcome.Defeat, "incomplete runtime exception must not map to defeat");
}
static BattleStartSnapshot BuildOpposedSnapshot(
    string battleId,
    int playerStrength,
    int enemyStrength,
    int enemyCellX = 6,
    int enemyCellY = 0)
{
    BattleStartSnapshot snapshot = new()
    {
        SnapshotId = $"snapshot_{battleId}",
        BattleId = battleId,
        TargetLocationId = "site_1",
        BattleGroups =
        {
            new BattleGroupSnapshot
            {
                BattleGroupId = "group_player",
                FactionId = "player",
                SourceForceId = "force_player",
                HeroId = "hero_player",
                HeroDefinitionId = "hero_def_player",
                CorpsId = "corps_player",
                CorpsDefinitionId = "player_corps",
                CorpsStrength = playerStrength,
                AttackImpactDelaySeconds = 0,
                SourceLocationId = "city_player",
                CellX = 0,
                CellY = 0
            },
            new BattleGroupSnapshot
            {
                BattleGroupId = "group_enemy",
                FactionId = "enemy",
                SourceForceId = "force_enemy",
                HeroId = "hero_enemy",
                HeroDefinitionId = "hero_def_enemy",
                CorpsId = "corps_enemy",
                CorpsDefinitionId = "enemy_corps",
                CorpsStrength = enemyStrength,
                AttackImpactDelaySeconds = 0,
                SourceLocationId = "site_1",
                CellX = enemyCellX,
                CellY = enemyCellY
            }
        }
    };
    TargetBattleTestTopology.CompileAroundGroups(snapshot);
    return snapshot;
}
static void BattleGroupVerticalSliceSettlesAndReports()
{
    HeroState hero = new() { HeroId = "hero_1", HeroDefinitionId = "hero_def_1", Level = 3 };
    CorpsState corps = new() { CorpsId = "corps_1", CorpsDefinitionId = "shield", Level = 2, CorpsStrength = 90 };
    BattleGroupState group = new BattleGroupLifecycleService().CreateAndStation("group_1", hero.HeroId, corps.CorpsId, "city_1");

    Rpg.Application.Battle.BattleGroupBattleFlowService flow = new();
    Rpg.Application.Battle.BattleGroupBattleFlowResult result = flow.RunMinimalBattle(
        "snapshot_1",
        "battle_1",
        "site_1",
        new[] { group },
        new Dictionary<string, HeroState> { [hero.HeroId] = hero },
        new Dictionary<string, CorpsState> { [corps.CorpsId] = corps });

    AssertTrue(result.SettlementPlan.Accepted, "settlement accepted");
    AssertEqual("battle_1", result.Report.BattleId, "report battle id");
    AssertTrue(result.SettlementPlan.Deltas.ChangedBattleGroupIds.Contains("group_1"), "settlement should mark changed battle group");
    AssertTrue(result.SettlementPlan.Deltas.ChangedHeroIds.Contains("hero_1"), "settlement should mark changed hero from runtime outcome");
    AssertTrue(result.SettlementPlan.Deltas.ChangedCorpsIds.Contains("corps_1"), "settlement should mark changed corps from runtime outcome");
    AssertSequence(result.RuntimeResult.EventStream.EventIds, result.SettlementPlan.SourceEventIds, "settlement uses runtime events");
    AssertSequence(result.RuntimeResult.EventStream.EventIds, result.Report.SourceEventIds, "report uses runtime events");
    AssertSequence(result.SettlementPlan.SourceEventIds, result.Report.SourceEventIds, "same source events");

    Rpg.Application.Battle.BattleGroupBattleFlowResult rejected = flow.RunMinimalBattle(
        "snapshot_missing",
        "battle_missing",
        "site_1",
        new[] { group },
        new Dictionary<string, HeroState>(),
        new Dictionary<string, CorpsState> { [corps.CorpsId] = corps });

    AssertTrue(!rejected.RuntimeResult.Outcome.IsComplete, "missing hero handoff must not complete");
    AssertTrue(!rejected.SettlementPlan.Accepted, "missing hero handoff must not settle");
    AssertEqual("battle_result_incomplete", rejected.SettlementPlan.RejectionReason, "missing hero settlement rejection");
}

static void MixedValidAndMissingHeroHandoffRejectsSettlementAndNormalReport()
{
    HeroState hero = new() { HeroId = "hero_1", HeroDefinitionId = "hero_def_1", Level = 3 };
    CorpsState corps = new() { CorpsId = "corps_1", CorpsDefinitionId = "shield", Level = 2, CorpsStrength = 90 };
    CorpsState missingHeroCorps = new() { CorpsId = "corps_2", CorpsDefinitionId = "pike", Level = 1, CorpsStrength = 60 };
    BattleGroupState validGroup = new BattleGroupLifecycleService().CreateAndStation("group_1", hero.HeroId, corps.CorpsId, "city_1");
    BattleGroupState missingHeroGroup = new()
    {
        BattleGroupId = "group_missing_hero",
        HeroId = "hero_missing",
        CorpsId = missingHeroCorps.CorpsId,
        CurrentLocationId = "city_1",
        Status = BattleGroupStatus.Stationed
    };

    Rpg.Application.Battle.BattleGroupBattleFlowResult result = new Rpg.Application.Battle.BattleGroupBattleFlowService()
        .RunMinimalBattle(
            "snapshot_mixed",
            "battle_mixed",
            "site_1",
            new[] { validGroup, missingHeroGroup },
            new Dictionary<string, HeroState> { [hero.HeroId] = hero },
            new Dictionary<string, CorpsState>
            {
                [corps.CorpsId] = corps,
                [missingHeroCorps.CorpsId] = missingHeroCorps
            });

    AssertEqual(2, result.Snapshot.BattleGroups.Count, "snapshot keeps requested group count");
    AssertTrue(!result.RuntimeResult.Outcome.IsComplete, "mixed invalid handoff must not complete");
    AssertEqual(BattleTerminationReason.RuntimeException, result.RuntimeResult.Outcome.TerminationReason, "mixed invalid handoff termination");
    AssertTrue(!result.SettlementPlan.Accepted, "mixed invalid handoff settlement rejected");
    AssertEqual("battle_result_incomplete", result.SettlementPlan.RejectionReason, "mixed invalid handoff settlement reason");
    AssertEqual("", result.Report.ReportId, "mixed invalid handoff must not create normal report");
    AssertEqual("SettlementRejected", result.Report.OutcomeSummary, "mixed invalid handoff report outcome");
    AssertTrue(result.Report.FailureCandidates.Contains("battle_result_incomplete"), "mixed invalid handoff report failure reason");
}
static BattleEventStream StartedStream(string battleId)
{
    BattleEventStream stream = new();
    stream.Add(new BattleEvent { EventId = $"{battleId}:started", BattleId = battleId, Kind = BattleEventKind.BattleStarted });
    return stream;
}
static BattleEventStream EndedStream(string battleId)
{
    BattleEventStream stream = StartedStream(battleId);
    stream.Add(new BattleEvent { EventId = $"{battleId}:ended", BattleId = battleId, Kind = BattleEventKind.BattleEnded });
    return stream;
}
static void AssertRejectedPlan(SettlementPlan plan, string reason, string message)
{
    AssertTrue(!plan.Accepted, $"{message} should reject");
    AssertEqual(reason, plan.RejectionReason, $"{message} rejection reason");
}
static string CombinedSource(params string[] pathParts)
{
    string root = ProjectRoot();
    string path = Path.Combine(new[] { root }.Concat(pathParts).ToArray());
    if (!Directory.Exists(path))
    {
        return "";
    }

    return string.Join("\n", Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories)
        .OrderBy(item => item, StringComparer.Ordinal)
        .Select(File.ReadAllText));
}
static string ProjectRoot()
{
    DirectoryInfo? current = new(AppContext.BaseDirectory);
    while (current != null && !File.Exists(Path.Combine(current.FullName, "rpg.csproj")))
    {
        current = current.Parent;
    }

    return current?.FullName ?? throw new InvalidOperationException("project root not found");
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
        Environment.ExitCode = 1;
    }
}
static void AssertTrue(bool condition, string message)
{
    if (!condition)
    {
        throw new Exception(message);
    }
}

static void AssertEqual<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new Exception($"{message}: expected={expected} actual={actual}");
    }
}
static void AssertThrows<T>(Action action, string message)
    where T : Exception
{
    try
    {
        action();
    }
    catch (T)
    {
        return;
    }
    catch (Exception exception)
    {
        throw new Exception($"{message}: expected={typeof(T).Name} actual={exception.GetType().Name}");
    }

    throw new Exception($"{message}: expected={typeof(T).Name} actual=no exception");
}
static void AssertSequence<T>(IReadOnlyList<T> expected, IReadOnlyList<T> actual, string message)
{
    if (!expected.SequenceEqual(actual))
    {
        throw new Exception($"{message}: expected=[{string.Join(",", expected)}] actual=[{string.Join(",", actual)}]");
    }
}
