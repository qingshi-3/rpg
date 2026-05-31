using System;

internal static class TargetBattleMovementIntentRegressionRegistration
{
    public static void Register(Action<string, Action> run)
    {
        run("runtime target choice uses reachable footprint attack slots", TargetBattleMovementIntentRegressionCases.RuntimeTargetChoiceUsesReachableFootprintAttackSlots);
        run("runtime move-first plan advances to objective before distant enemy", TargetBattleMovementIntentRegressionCases.RuntimeMoveFirstPlanAdvancesToObjectiveBeforeDistantEnemy);
        run("runtime movement started does not publish lookahead correction path", TargetBattleMovementIntentRegressionCases.RuntimeMovementStartedDoesNotPublishLookaheadCorrectionPath);
        run("runtime move-first plan advances across large authored topology", TargetBattleMovementIntentRegressionCases.RuntimeMoveFirstPlanAdvancesAcrossLargeAuthoredTopology);
        run("runtime plan-scoped movement does not scan far attack slots", TargetBattleMovementIntentRegressionCases.RuntimePlanScopedMovementDoesNotScanFarAttackSlots);
        run("runtime enemy move-first plan does not scan far attack slots", TargetBattleMovementIntentRegressionCases.RuntimeEnemyMoveFirstPlanDoesNotScanFarAttackSlots);
        run("runtime enemy attack-first plan senses local player before objective", TargetBattleMovementIntentRegressionCases.RuntimeEnemyAttackFirstPlanSensesLocalPlayerBeforeObjective);
        run("explicit attack-first selection survives preparation default refresh", TargetBattlePreparationPlanRegressionCases.ExplicitAttackFirstSelectionSurvivesPreparationDefaultRefresh);
        run("runtime objective-zone plan resolves anchor from snapshot zone", TargetBattleMovementIntentRegressionCases.RuntimeObjectiveZonePlanResolvesAnchorFromSnapshotZone);
        run("runtime move-first plan seeks enemy after objective reached", TargetBattleMovementIntentRegressionCases.RuntimeMoveFirstPlanSeeksEnemyAfterObjectiveReached);
        run("local combat region uses perception overlap and cap", TargetBattleLocalCombatSituationRegressionCases.LocalCombatRegionUsesPerceptionOverlapAndCap);
        run("runtime stores local combat region for engaged group", TargetBattleLocalCombatSituationRegressionCases.RuntimeStoresLocalCombatRegionForEngagedGroup);
        run("local combat decision facts expose stored region facts", TargetBattleLocalCombatSituationRegressionCases.LocalCombatDecisionFactsExposeStoredRegionFacts);
        run("engaged targeting ignores far hostile outside local region", TargetBattleLocalCombatSituationRegressionCases.EngagedTargetingIgnoresFarHostileOutsideLocalRegion);
        run("engaged attack slots stay inside local region", TargetBattleLocalCombatSituationRegressionCases.EngagedAttackSlotsStayInsideLocalRegion);
        run("engaged no local slot degrades with reason", TargetBattleLocalCombatSituationRegressionCases.EngagedNoLocalSlotDegradesWithReason);
        run("runtime move-first joins route-blocking local fight", TargetBattleLocalCombatSituationRegressionCases.RuntimeMoveFirstJoinsRouteBlockingLocalFight);
        run("runtime full attack slots uses named support slot", TargetBattleLocalCombatSituationRegressionCases.RuntimeFullAttackSlotsUsesNamedSupportSlot);
        run("runtime hold rejects local fight outside leash", TargetBattleLocalCombatSituationRegressionCases.RuntimeHoldRejectsLocalFightOutsideLeash);
    }
}
