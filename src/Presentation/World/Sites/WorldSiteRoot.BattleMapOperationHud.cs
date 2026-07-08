using Godot;

namespace Rpg.Presentation.World.Sites;

public partial class WorldSiteRoot
{
    private const string BattleMapOperationCancelAction = "ui_cancel";

    private readonly BattleMapOperationHudSuppressor _battleMapOperationHudSuppressor = new(nameof(WorldSiteRoot));
    private BattleMapOperationHudSuppressionReason _battleMapOperationHudSuppressionReason = BattleMapOperationHudSuppressionReason.None;

    private void EnterBattleMapOperationHudSuppression(BattleMapOperationHudSuppressionReason reason, string diagnosticReason)
        => _battleMapOperationHudSuppressionReason = _battleMapOperationHudSuppressor.Enter(_battleMapOperationHudSuppressionReason, reason, BuildBattleMapOperationBlockingHudControls(), diagnosticReason);

    private void ExitBattleMapOperationHudSuppression(BattleMapOperationHudSuppressionReason reason, string diagnosticReason)
        => _battleMapOperationHudSuppressionReason = _battleMapOperationHudSuppressor.Exit(_battleMapOperationHudSuppressionReason, reason, diagnosticReason);

    private void ApplyBattleMapOperationHudSuppressionVisibility(string diagnosticReason)
        => _battleMapOperationHudSuppressor.Apply(_battleMapOperationHudSuppressionReason, BuildBattleMapOperationBlockingHudControls(), diagnosticReason);

    private bool TryHandleBattleMapOperationHudSuppressionCancelInput(InputEvent inputEvent)
        => BattleMapOperationHudCancelCoordinator.TryHandle(
            inputEvent,
            _battleMapOperationHudSuppressionReason,
            BattleMapOperationCancelAction,
            () => CancelBattleRuntimeHeroSkillTargetPicking("map_operation_cancel"),
            () => { string groupKey = _draggedBattlePreparationGroupKey; RestoreBattlePreparationCompanyPlacements(); ClearBattlePreparationCompanyDragState(); RefreshBattlePreparationAfterCompanyDrag(groupKey, ""); },
            () => { ClearBattlePreparationDestinationTargeting("map_operation_cancel"); RefreshBattlePreparationPlanUi("", "battle_preparation_destination_cancel"); },
            () => ExitBattleMapOperationHudSuppression(BattleMapOperationHudSuppressionReason.RuntimeDestinationBeacon, "map_operation_cancel"),
            () => GetViewport()?.SetInputAsHandled());

    private Control[] BuildBattleMapOperationBlockingHudControls() => new[] { _sitePeacetimePanel, _siteBottomCommandHost, _battleRuntimeSummaryBar, _battleRuntimeCommandBar, _battleRuntimePauseDetailPanel, _battlePreparationRosterDock, _battlePreparationLaunchDock, _battlePreparationStartButton, _battlePreparationObjectiveThumbnailDock, _siteMinimapHost };
}
