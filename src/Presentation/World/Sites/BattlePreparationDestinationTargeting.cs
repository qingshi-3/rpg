using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle;
using Rpg.Domain.Battle.Grid;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Battle;

namespace Rpg.Presentation.World.Sites;

public partial class WorldSiteRoot
{
    private const string BattlePreparationDestinationGuideOverlayScenePath = "res://scenes/world/ui/BattlePreparationDestinationGuideOverlay.tscn";

    private void EnsureBattlePreparationDestinationGuideOverlay()
    {
        if (_battlePreparationDestinationGuideOverlay != null && IsLiveNode(_battlePreparationDestinationGuideOverlay))
        {
            return;
        }

        Node parent = _highlightOverlay?.GetParent() ?? _unitRoot ?? _mapRoot ?? this;
        _battlePreparationDestinationGuideOverlay = parent.GetNodeOrNull<BattlePreparationDestinationGuideOverlay>("BattlePreparationDestinationGuideOverlay");
        if (_battlePreparationDestinationGuideOverlay == null)
        {
            _battlePreparationDestinationGuideOverlayScene ??= GD.Load<PackedScene>(BattlePreparationDestinationGuideOverlayScenePath);
            _battlePreparationDestinationGuideOverlay = _battlePreparationDestinationGuideOverlayScene?.Instantiate<BattlePreparationDestinationGuideOverlay>();
            if (_battlePreparationDestinationGuideOverlay == null)
            {
                GameLog.Error(nameof(WorldSiteRoot), $"BattlePreparationDestinationGuideOverlayMissing scene={BattlePreparationDestinationGuideOverlayScenePath}");
                return;
            }

            parent.AddChild(_battlePreparationDestinationGuideOverlay);
        }
    }

    private void BeginBattlePreparationDestinationTargeting(string groupKey)
    {
        if (!_isBattlePreparationActive || string.IsNullOrWhiteSpace(groupKey))
        {
            return;
        }

        EnsureBattlePreparationDestinationGuideOverlay();
        _battlePreparationDestinationTargetingActive = true;
        _battlePreparationDestinationTargetingGroupKey = groupKey;
        _battlePreparationDestinationPreviousMouseMode = Input.MouseMode;
        Input.MouseMode = Input.MouseModeEnum.Hidden;
        EnterBattleMapOperationHudSuppression(BattleMapOperationHudSuppressionReason.PreparationDestinationBeacon, "preparation_destination_targeting_started");
        UpdateBattlePreparationDestinationTargetingGuide();
        SetSiteNoticeText("请选择部队移动目的地。");
        GameLog.Info(nameof(WorldSiteRoot), $"BattlePreparationDestinationTargetingStarted request={_battlePreparationRequest?.RequestId ?? ""} group={groupKey}");
    }

    private void UpdateBattlePreparationDestinationTargetingGuide()
    {
        if (!_battlePreparationDestinationTargetingActive)
        {
            return;
        }

        bool validTarget = TryGetMouseGridPosition(out GridPosition position) &&
                           TryResolveBattleRuntimeDestinationBeaconTarget(position, out _, out _);
        if (validTarget)
        {
            _highlightOverlay?.SetCells(BattleGridHighlightKind.Hover, new[] { position });
        }
        else
        {
            _highlightOverlay?.ClearCells(BattleGridHighlightKind.Hover);
        }

        if (TryResolveBattlePreparationDestinationGuideOrigin(_battlePreparationDestinationTargetingGroupKey, out Vector2 originGlobal))
        {
            _battlePreparationDestinationGuideOverlay?.SetGuide(originGlobal, GetWorldViewportMousePosition(), validTarget);
        }
    }

    private void ClearBattlePreparationDestinationTargeting(string reason)
    {
        bool wasActive = _battlePreparationDestinationTargetingActive;
        _battlePreparationDestinationTargetingActive = false;
        _battlePreparationDestinationTargetingGroupKey = "";
        _battlePreparationDestinationGuideOverlay?.ClearGuide();
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Hover);
        Input.MouseMode = _battlePreparationDestinationPreviousMouseMode;
        SetBattlePreparationTopPrompt("");
        ExitBattleMapOperationHudSuppression(BattleMapOperationHudSuppressionReason.PreparationDestinationBeacon, reason);
        if (wasActive)
        {
            GameLog.Info(nameof(WorldSiteRoot), $"BattlePreparationDestinationTargetingCleared reason={reason ?? ""}");
        }
    }

    private bool TryResolveBattlePreparationDestinationGuideOrigin(string groupKey, out Vector2 originGlobal)
    {
        originGlobal = default;
        BattleRuntimeCommandGroupView group = FindBattlePreparationCompany(groupKey);
        if (group == null)
        {
            return false;
        }

        List<Vector2> positions = new();
        foreach (BattleForcePlacementRequest placement in (group.Forces ?? System.Array.Empty<BattleForceRequest>())
                     .SelectMany(force => force?.PreferredPlacements ?? Enumerable.Empty<BattleForcePlacementRequest>()))
        {
            var anchor = new GridPosition(placement.CellX, placement.CellY);
            if (TryGetFootprintCenterGlobalPosition(anchor, Vector2I.One, out Vector2 position))
            {
                positions.Add(position);
            }
        }

        if (positions.Count == 0)
        {
            return false;
        }

        originGlobal = positions.Aggregate(Vector2.Zero, (sum, item) => sum + item) / positions.Count;
        return true;
    }
}
