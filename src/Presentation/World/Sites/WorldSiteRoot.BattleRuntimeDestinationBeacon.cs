using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle;
using Godot;
using Rpg.Application.Battle.Snapshots;
using Rpg.Application.Battle.Commands;
using Rpg.Application.World;
using Rpg.Domain.Battle.Grid;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Battle;
using Rpg.Presentation.Battle.Entities;
using Rpg.Runtime.Battle;

namespace Rpg.Presentation.World.Sites;

public partial class WorldSiteRoot
{
    private bool TryHandleBattlePreparationDestinationBeaconInput(InputEvent inputEvent)
    {
        if (!_isBattlePreparationActive ||
            !_battlePreparationDestinationTargetingActive ||
            !string.IsNullOrWhiteSpace(_draggedBattlePreparationGroupKey) ||
            inputEvent is not (InputEventMouseMotion or InputEventMouseButton))
        {
            return false;
        }

        if (inputEvent is InputEventMouseMotion)
        {
            UpdateBattlePreparationDestinationTargetingGuide();
            GetViewport()?.SetInputAsHandled();
            return true;
        }

        if (inputEvent is not InputEventMouseButton mouseButton)
        {
            return false;
        }

        bool isLeftClick = mouseButton.ButtonIndex == MouseButton.Left;
        if (!isLeftClick || !mouseButton.Pressed)
        {
            return false;
        }

        IReadOnlyList<string> selectedGroupKeys = BuildSelectedBattlePreparationDestinationBeaconGroupKeys();
        if (selectedGroupKeys.Count == 0)
        {
            SetSiteNoticeText("请先选择已部署部队。");
            GetViewport()?.SetInputAsHandled();
            return true;
        }

        EnterBattleMapOperationHudSuppression(BattleMapOperationHudSuppressionReason.PreparationDestinationBeacon, "preparation_destination_left_click");
        if (!TryGetMouseGridPosition(out GridPosition position) ||
            !TryResolveBattleRuntimeDestinationBeaconTarget(position, out GridPosition target, out int targetHeight))
        {
            SetSiteNoticeText("请选择战场内的有效目的地。");
            GameLog.Info(
                nameof(WorldSiteRoot),
                $"BattlePreparationDestinationRejected request={_battlePreparationRequest?.RequestId ?? ""} group={_battlePreparationDestinationTargetingGroupKey} reason=invalid_target");
            GetViewport()?.SetInputAsHandled();
            return true;
        }

        foreach (string groupKey in selectedGroupKeys)
        {
            ApplyBattlePreparationDestinationBeaconToPlan(groupKey, target, targetHeight);
        }

        SyncSelectedBattlePreparationPlanFallback(_battlePreparationRequest);
        BindBattlePreparationCompanyRoster();
        BindBattlePreparationLaunchControl();
        RefreshBattlePreparationDestinationBeaconOverlays();
        SetBattlePreparationTopPrompt("");
        SetSiteNoticeText($"已为{selectedGroupKeys.Count}支部队设置目的地。");
        ClearBattlePreparationDestinationTargeting("preparation_destination_submitted");
        GameLog.Info(
            nameof(WorldSiteRoot),
            $"BattlePreparationDestinationBeaconStored request={_battlePreparationRequest?.RequestId ?? ""} groups={string.Join("|", selectedGroupKeys)} target=({target.X},{target.Y},{targetHeight})");
        GetViewport()?.SetInputAsHandled();
        return true;
    }

    private IReadOnlyList<string> BuildSelectedBattlePreparationDestinationBeaconGroupKeys()
    {
        if (_battlePreparationDestinationTargetingActive &&
            !string.IsNullOrWhiteSpace(_battlePreparationDestinationTargetingGroupKey))
        {
            BattleRuntimeCommandGroupView targetingGroup = FindBattlePreparationCompany(_battlePreparationDestinationTargetingGroupKey);
            return targetingGroup != null && IsBattlePreparationCompanyPlaced(targetingGroup)
                ? new[] { targetingGroup.GroupKey }
                : System.Array.Empty<string>();
        }

        HashSet<string> selected = new(_selectedBattlePreparationPlanGroupKeys, System.StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(_selectedBattlePreparationPlanGroupKey))
        {
            selected.Add(_selectedBattlePreparationPlanGroupKey);
        }

        HashSet<string> deployed = BuildBattlePreparationPlayerGroups()
            .Where(IsBattlePreparationCompanyPlaced)
            .Select(group => group.GroupKey ?? "")
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .ToHashSet(System.StringComparer.Ordinal);
        selected.RemoveWhere(key => !deployed.Contains(key));
        return selected
            .OrderBy(key => key, System.StringComparer.Ordinal)
            .ToArray();
    }

    private void ApplyBattlePreparationDestinationBeaconToPlan(
        string groupKey,
        GridPosition target,
        int targetHeight)
    {
        BattleGroupPlanSnapshot plan = ResolveBattlePreparationGroupPlan(_battlePreparationRequest, groupKey, create: true);
        if (plan == null)
        {
            return;
        }

        // Battle preparation records command intent only; Runtime later owns the
        // actual beacon object, flow field, action locks, and movement commits.
        plan.HasInitialDestinationBeacon = true;
        plan.InitialDestinationCellX = target.X;
        plan.InitialDestinationCellY = target.Y;
        plan.InitialDestinationCellHeight = targetHeight;
        plan.EngagementRule = BattleEngagementRule.AttackFirst;
    }

    private void RefreshBattlePreparationDestinationBeaconOverlays()
    {
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.DestinationBeacon);
        _battleDestinationBeaconMarkerPresenter.RefreshPreparation(
            _highlightOverlay,
            BuildBattlePreparationPlayerGroups(), string.IsNullOrWhiteSpace(_draggedBattlePreparationGroupKey) ? _selectedBattlePreparationPlanGroupKey : _draggedBattlePreparationGroupKey,
            groupKey => ResolveBattlePreparationGroupPlan(_battlePreparationRequest, groupKey, create: false));
    }

    private bool TryHandleBattleRuntimeCommandSelectionInput(InputEvent inputEvent)
    {
        if (!_battleRuntimeEnabled ||
            _isBattlePreparationActive ||
            inputEvent is not InputEventMouseButton mouseButton)
        {
            return false;
        }

        bool isLeftClick = mouseButton.ButtonIndex == MouseButton.Left;
        if (!isLeftClick || !mouseButton.Pressed)
        {
            return false;
        }

        if (BattleRuntimeCommandHudPointerGate.ContainsPointer(_battleRuntimeCommandBar, mouseButton.Position) ||
            BattleRuntimeCommandHudPointerGate.ContainsPointer(_battleRuntimeSummaryBar, mouseButton.Position) ||
            BattleRuntimeCommandHudPointerGate.ContainsPointer(_battleRuntimePauseDetailPanel, mouseButton.Position))
        {
            return false;
        }

        if (!TryGetMouseGridPosition(out GridPosition position))
        {
            return false;
        }

        if (!TryResolveBattleRuntimeCommandGroupKeyAtPosition(position, out string groupKey))
        {
            return false;
        }

        SelectBattleRuntimeCommandGroup(groupKey, IsBattleRuntimeAdditiveSelectionInput(mouseButton));
        GetViewport()?.SetInputAsHandled();
        return true;
    }

    private bool TryHandleBattleRuntimeDestinationBeaconInput(InputEvent inputEvent)
    {
        if (!_battleRuntimeEnabled ||
            _isBattlePreparationActive ||
            inputEvent is not InputEventMouseButton mouseButton)
        {
            return false;
        }

        bool isRightClick = mouseButton.ButtonIndex == MouseButton.Right;
        if (!isRightClick || !mouseButton.Pressed)
        {
            return false;
        }

        IReadOnlyList<string> selectedGroupKeys = BuildSelectedBattleRuntimeCommandGroupKeys();
        if (selectedGroupKeys.Count == 0)
        {
            SetSiteNoticeText("请选择要下达目的地的部队。");
            GetViewport()?.SetInputAsHandled();
            return true;
        }

        EnterBattleMapOperationHudSuppression(BattleMapOperationHudSuppressionReason.RuntimeDestinationBeacon, "runtime_destination_right_click");
        if (!TryGetMouseGridPosition(out GridPosition position) ||
            !TryResolveBattleRuntimeDestinationBeaconTarget(position, out GridPosition target, out int targetHeight))
        {
            SetSiteNoticeText("请选择战场内的有效目的地。");
            GetViewport()?.SetInputAsHandled();
            return true;
        }

        CommandRequest commandRequest = BuildBattleRuntimeDestinationBeaconCommandRequest(selectedGroupKeys, target, targetHeight);
        BattleCommandSubmissionResult result = new BattleCommandSubmissionService().Submit(
            _activeBattleGroupRuntimeResolution?.Snapshot,
            StrategicWorldRuntime.State?.PlayerFactionId ?? "",
            commandRequest,
            _activeBattleGroupRuntimeResolution?.RuntimeController);
        bool accepted = result?.Accepted == true;
        if (accepted)
        {
            SetSiteNoticeText($"已为{selectedGroupKeys.Count}支部队设置目的地。");
            RefreshBattleRuntimeDestinationBeaconOverlayVisibility();
            ExitBattleMapOperationHudSuppression(BattleMapOperationHudSuppressionReason.RuntimeDestinationBeacon, "runtime_destination_submitted");
        }
        else
        {
            SetSiteNoticeText($"目的地不可用：{BattleRuntimeSkillHudText.BuildUnavailableText(result?.ReasonCode)}");
        }

        GameLog.Info(
            nameof(WorldSiteRoot),
            $"BattleRuntimeDestinationBeaconSubmitted command={commandRequest.CommandId} groups={string.Join("|", selectedGroupKeys)} target=({target.X},{target.Y},{targetHeight}) accepted={accepted} reason={result?.ReasonCode ?? "runtime_missing"}");
        GetViewport()?.SetInputAsHandled();
        return true;
    }

    private void SelectBattleRuntimeCommandGroup(string groupKey) => SelectBattleRuntimeCommandGroup(groupKey, additive: false);

    private void SelectBattleRuntimeCommandGroup(string groupKey, bool additive)
    {
        string normalizedGroupKey = groupKey?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(normalizedGroupKey))
        {
            return;
        }

        bool changedPrimary = !string.Equals(normalizedGroupKey, _selectedBattleRuntimeGroupKey, System.StringComparison.Ordinal);
        if (changedPrimary)
        {
            CancelBattleRuntimeHeroSkillTargetPicking("group_changed");
        }

        if (!additive)
        {
            _selectedBattleRuntimeGroupKeys.Clear();
            _selectedBattleRuntimeGroupKeys.Add(normalizedGroupKey);
            _selectedBattleRuntimeGroupKey = normalizedGroupKey;
        }
        else if (!_selectedBattleRuntimeGroupKeys.Add(normalizedGroupKey) && _selectedBattleRuntimeGroupKeys.Count > 1)
        {
            _selectedBattleRuntimeGroupKeys.Remove(normalizedGroupKey);
            if (string.Equals(_selectedBattleRuntimeGroupKey, normalizedGroupKey, System.StringComparison.Ordinal))
            {
                _selectedBattleRuntimeGroupKey = _selectedBattleRuntimeGroupKeys
                    .OrderBy(key => key, System.StringComparer.Ordinal)
                    .FirstOrDefault() ?? "";
            }
        }
        else
        {
            _selectedBattleRuntimeGroupKey = normalizedGroupKey;
        }

        ApplyBattleRuntimeCommandGroupHighlight(); RefreshBattleRuntimeHeroFrame(); RefreshBattleRuntimeDestinationBeaconOverlayVisibility();
        GameLog.Info(
            nameof(WorldSiteRoot),
            $"BattleRuntimeCommandGroupSelected group={_selectedBattleRuntimeGroupKey} selected={string.Join("|", _selectedBattleRuntimeGroupKeys.OrderBy(key => key, System.StringComparer.Ordinal))}");
    }

    private void EnsureSelectedBattleRuntimeCommandGroup()
    {
        IReadOnlyList<BattleRuntimeCommandGroupView> groups = BuildBattleRuntimePlayerGroups();
        HashSet<string> validKeys = groups
            .Select(group => group?.GroupKey ?? "")
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .ToHashSet(System.StringComparer.Ordinal);
        _selectedBattleRuntimeGroupKeys.RemoveWhere(key => !validKeys.Contains(key));
        if (validKeys.Contains(_selectedBattleRuntimeGroupKey ?? ""))
        {
            _selectedBattleRuntimeGroupKeys.Add(_selectedBattleRuntimeGroupKey);
            ApplyBattleRuntimeCommandGroupHighlight();
            return;
        }

        _selectedBattleRuntimeGroupKey = groups.FirstOrDefault()?.GroupKey ?? "";
        if (!string.IsNullOrWhiteSpace(_selectedBattleRuntimeGroupKey))
        {
            _selectedBattleRuntimeGroupKeys.Add(_selectedBattleRuntimeGroupKey);
        }

        ApplyBattleRuntimeCommandGroupHighlight();
    }

    private void ApplyBattleRuntimeCommandGroupHighlight()
    {
        IReadOnlyList<BattleRuntimeCommandGroupView> selectedGroups = ResolveSelectedBattleRuntimeGroups();
        if (selectedGroups.Count == 0)
        {
            _unitRoot?.ClearCommandSelection();
            return;
        }

        HashSet<string> entityIds = BuildSelectedBattleRuntimeCommandGroupEntityIds(selectedGroups);
        HashSet<string> spotlightEntityIds = BuildSelectedBattleRuntimeCommandGroupSpotlightEntityIds(selectedGroups);
        int highlighted = _unitRoot == null ? 0 : _unitRoot.SetCommandSelectionByEntityIds(entityIds, spotlightEntityIds);
        GameLog.Info(
            nameof(WorldSiteRoot),
            $"BattleRuntimeCommandGroupHighlighted groups={string.Join("|", selectedGroups.Select(group => group.GroupKey))} entities={highlighted} spotlights={spotlightEntityIds.Count}");
    }

    private static HashSet<string> BuildBattleRuntimeCommandGroupEntityIds(BattleRuntimeCommandGroupView selected) => BattleRuntimeHeroSkillTargetPresentation.BuildGroupEntityIds(selected?.Forces);

    private static HashSet<string> BuildSelectedBattleRuntimeCommandGroupEntityIds(IEnumerable<BattleRuntimeCommandGroupView> selectedGroups)
    {
        HashSet<string> entityIds = new(System.StringComparer.Ordinal);
        foreach (BattleRuntimeCommandGroupView selected in selectedGroups ?? System.Array.Empty<BattleRuntimeCommandGroupView>())
        {
            entityIds.UnionWith(BuildBattleRuntimeCommandGroupEntityIds(selected));
        }

        return entityIds;
    }

    private HashSet<string> BuildBattleRuntimeCommandGroupSpotlightEntityIds(BattleRuntimeCommandGroupView selected)
    {
        BattleEntity source = BuildBattleRuntimeHeroSkillSourceEntity(selected);
        return string.IsNullOrWhiteSpace(source?.EntityId)
            ? new HashSet<string>(System.StringComparer.Ordinal)
            : new HashSet<string>(System.StringComparer.Ordinal) { source.EntityId };
    }

    private HashSet<string> BuildSelectedBattleRuntimeCommandGroupSpotlightEntityIds(IEnumerable<BattleRuntimeCommandGroupView> selectedGroups)
    {
        HashSet<string> spotlightEntityIds = new(System.StringComparer.Ordinal);
        foreach (BattleRuntimeCommandGroupView selected in selectedGroups ?? System.Array.Empty<BattleRuntimeCommandGroupView>())
        {
            spotlightEntityIds.UnionWith(BuildBattleRuntimeCommandGroupSpotlightEntityIds(selected));
        }

        return spotlightEntityIds;
    }

    private IReadOnlyList<BattleRuntimeCommandGroupView> ResolveSelectedBattleRuntimeGroups()
    {
        IReadOnlyList<BattleRuntimeCommandGroupView> groups = BuildBattleRuntimePlayerGroups();
        HashSet<string> selectedKeys = BuildSelectedBattleRuntimeCommandGroupKeys().ToHashSet(System.StringComparer.Ordinal);
        return groups
            .Where(group => group != null && selectedKeys.Contains(group.GroupKey ?? ""))
            .OrderBy(group => group.GroupKey, System.StringComparer.Ordinal)
            .ToArray();
    }

    private IReadOnlyList<string> BuildSelectedBattleRuntimeCommandGroupKeys()
    {
        IReadOnlyList<BattleRuntimeCommandGroupView> groups = BuildBattleRuntimePlayerGroups();
        HashSet<string> validKeys = groups
            .Select(group => group?.GroupKey ?? "")
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .ToHashSet(System.StringComparer.Ordinal);
        _selectedBattleRuntimeGroupKeys.RemoveWhere(key => !validKeys.Contains(key));
        if (!string.IsNullOrWhiteSpace(_selectedBattleRuntimeGroupKey) &&
            validKeys.Contains(_selectedBattleRuntimeGroupKey))
        {
            _selectedBattleRuntimeGroupKeys.Add(_selectedBattleRuntimeGroupKey);
        }

        return _selectedBattleRuntimeGroupKeys
            .OrderBy(key => key, System.StringComparer.Ordinal)
            .ToArray();
    }

    private bool TryResolveBattleRuntimeCommandGroupKeyAtPosition(GridPosition position, out string groupKey)
    {
        groupKey = "";
        BattleEntity entity = FindEntityAt(position);
        string entityId = entity?.EntityId ?? "";
        if (string.IsNullOrWhiteSpace(entityId))
        {
            return false;
        }

        foreach (BattleRuntimeCommandGroupView group in BuildBattleRuntimePlayerGroups())
        {
            if (BuildBattleRuntimeCommandGroupEntityIds(group).Contains(entityId))
            {
                groupKey = group.GroupKey ?? "";
                return !string.IsNullOrWhiteSpace(groupKey);
            }
        }

        return false;
    }

    private static bool IsBattleRuntimeAdditiveSelectionInput(InputEventMouseButton mouseButton) => mouseButton?.ShiftPressed == true;

    private bool TryResolveBattleRuntimeDestinationBeaconTarget(
        GridPosition position,
        out GridPosition target,
        out int targetHeight)
    {
        target = position;
        targetHeight = 0;
        if (_activeGridMap == null || !_activeGridMap.TryGetCell(position, out _))
        {
            return false;
        }

        if (_activeGridMap.TryGetTopSurfacePosition(position, out GridSurfacePosition surface))
        {
            targetHeight = surface.Height;
        }

        return true;
    }

    private CommandRequest BuildBattleRuntimeDestinationBeaconCommandRequest(
        IReadOnlyList<string> selectedGroupKeys,
        GridPosition target,
        int targetHeight)
    {
        string battleId = _activeBattleGroupRuntimeResolution?.RuntimeController?.BattleId ?? _battleRuntimeRequest?.RequestId ?? "";
        string primaryGroupKey = selectedGroupKeys?.FirstOrDefault() ?? _selectedBattleRuntimeGroupKey ?? "";
        string commandId = $"{battleId}:destination_beacon:{++_battleRuntimeDestinationBeaconCommandSequence}";
        var request = new CommandRequest
        {
            CommandId = commandId,
            BattleId = battleId,
            BattleGroupId = primaryGroupKey,
            Channel = CommandChannel.Combined,
            Kind = CommandKind.DestinationBeacon,
            HasTargetGrid = true,
            TargetGridX = target.X,
            TargetGridY = target.Y,
            TargetGridHeight = targetHeight
        };
        foreach (string groupKey in selectedGroupKeys ?? System.Array.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(groupKey) &&
                !request.BattleGroupIds.Contains(groupKey, System.StringComparer.Ordinal))
            {
                request.BattleGroupIds.Add(groupKey);
            }
        }

        return request;
    }

    private void RefreshBattleRuntimeDestinationBeaconOverlayVisibility()
    {
        // Destination beacons are live command facts; in-battle overlays are only exposed during tactical pause.
        if (_battleRuntimeCommandPauseActive) { RefreshBattleRuntimeDestinationBeaconOverlays(); return; }
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.DestinationBeacon);
        _battleDestinationBeaconMarkerPresenter.Clear();
    }

    private void RefreshBattleRuntimeDestinationBeaconOverlays()
    {
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.DestinationBeacon);
        _battleDestinationBeaconMarkerPresenter.RefreshRuntime(
            _highlightOverlay,
            _activeBattleGroupRuntimeResolution?.RuntimeController?.State?.DestinationBeacons,
            BuildBattleRuntimePlayerGroups(),
            _selectedBattleRuntimeGroupKey);
    }
}
