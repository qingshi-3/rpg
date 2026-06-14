using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Commands;
using Rpg.Application.Battle.Snapshots;
using Rpg.Domain.Battle.Grid;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Battle;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Battle.Flow;
using Rpg.Presentation.Battle.Rules;
using Rpg.Presentation.Common;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.Events;
namespace Rpg.Presentation.World.Sites;
public partial class WorldSiteRoot
{
    internal enum BattleRuntimeSkillUsageState
    {
        Unavailable,
        Ready,
        Pending,
        Used
    }
    private enum ThunderFoldTargetingStage { None, MarkSelection, LandingSelection }
    private bool TryHandleBattleRuntimePauseInput(InputEvent inputEvent)
    {
        if (!_battleRuntimeEnabled ||
            _isBattlePreparationActive ||
            inputEvent is not InputEventKey { Pressed: true, Echo: false } key ||
            key.Keycode != Key.Space)
        {
            return false;
        }
        ToggleBattleRuntimeCommandPause();
        GetViewport()?.SetInputAsHandled();
        return true;
    }
    private void ToggleBattleRuntimeCommandPause()
    {
        SetBattleRuntimeCommandPauseActive(!_battleRuntimeCommandPauseActive, "space_toggle");
    }
    private void SubmitBattleRuntimeCommand(BattleCorpsCommand command)
    {
        CommandRequest commandRequest = BuildBattleRuntimeCommandRequest(command);
        if (_battleRuntimeEnabled && !_isBattlePreparationActive)
        {
            SetSiteNoticeText("战斗已经开始，当前姿态由战前计划决定；运行时请使用重整或英雄技能。");
            RefreshBattleRuntimeHeroFrame();
            GameLog.Info(
                nameof(WorldSiteRoot),
                $"BattleRuntimeCommandRejected command={commandRequest.CommandId} reason=runtime_locked request={commandRequest.BattleId}");
            return;
        }
        _selectedBattleCorpsCommand = command;
        BattleRuntimeCommandHudModel.ApplyRuntimeCommandToRequest(_battlePreparationRequest, commandRequest);
        if (_isBattlePreparationActive)
        {
            RefreshBattlePreparationPlanUi($"战斗姿态已设为：{BattleCorpsCommandLabels.ToDisplayText(command)}。", "battle_preparation_command_selected");
        }
        RefreshBattleRuntimeHeroFrame();
        GameLog.Info(
            nameof(WorldSiteRoot),
            $"BattleRuntimeCommandSelected command={commandRequest.CommandId} kind={commandRequest.Kind} request={commandRequest.BattleId}");
    }
    private CommandRequest BuildBattleRuntimeCommandRequest(BattleCorpsCommand command)
    {
        BattleStartRequest request = _battlePreparationRequest;
        if (request == null && TryResolveActiveBattleRequest(out BattleStartRequest activeRequest)) { request = activeRequest; }
        return new CommandRequest
        {
            CommandId = BattleRuntimeCommandHudModel.NormalizeCorpsCommandId(command),
            BattleId = request?.RequestId ?? "",
            BattleGroupId = "player_corps_initial_posture",
            Channel = CommandChannel.Corps,
            Kind = BattleRuntimeCommandHudModel.ToCommandKind(command)
        };
    }
    private void RefreshBattleRuntimeCommandControls(bool runtimeLocked)
    {
        RefreshBattleRuntimeHeroFrame();
    }
    private void RefreshBattleRuntimeCommandPausePresentation()
    {
        if (!_battleRuntimeCommandPauseActive)
        {
            if (_siteBottomCommandHost != null) { _siteBottomCommandHost.Visible = false; }
            if (_battleRuntimeCommandBar != null) { _battleRuntimeCommandBar.Visible = false; }
            UpdateMainWorldViewportLayout("battle_runtime_command_resume");
            return;
        }
        if (_siteHudRoot != null)
        {
            _siteHudRoot.Visible = true;
            ApplySiteHudFullRect("battle_runtime_command_pause");
        }
        if (_siteHudTopBar != null)
        {
            _siteHudTopBar.Visible = false;
        }
        if (_sitePeacetimePanel != null)
        {
            _sitePeacetimePanel.Visible = false;
        }
        if (_siteBottomCommandHost != null)
        {
            _siteBottomCommandHost.Visible = true;
        }
        if (_battleRuntimeCommandBar != null)
        {
            _battleRuntimeCommandBar.Visible = true;
        }
        RefreshBattleRuntimeHeroFrame();
        UpdateMainWorldViewportLayout("battle_runtime_command_pause");
    }
    private void SelectBattleRuntimeCommandGroup(string groupKey)
    {
        if (!string.Equals(groupKey ?? "", _selectedBattleRuntimeGroupKey, System.StringComparison.Ordinal))
        {
            CancelBattleRuntimeHeroSkillTargetPicking("group_changed");
        }
        _selectedBattleRuntimeGroupKey = groupKey ?? "";
        ApplyBattleRuntimeCommandGroupHighlight();
        RefreshBattleRuntimeHeroFrame();
        GameLog.Info(nameof(WorldSiteRoot), $"BattleRuntimeCommandGroupSelected group={_selectedBattleRuntimeGroupKey}");
    }
    private void EnsureSelectedBattleRuntimeCommandGroup()
    {
        IReadOnlyList<BattleRuntimeCommandGroupView> groups = BuildBattleRuntimePlayerGroups();
        if (groups.Any(group => string.Equals(group.GroupKey, _selectedBattleRuntimeGroupKey, System.StringComparison.Ordinal)))
        {
            ApplyBattleRuntimeCommandGroupHighlight();
            return;
        }
        _selectedBattleRuntimeGroupKey = groups.FirstOrDefault()?.GroupKey ?? "";
        ApplyBattleRuntimeCommandGroupHighlight();
    }
    private void ApplyBattleRuntimeCommandGroupHighlight()
    {
        BattleRuntimeCommandGroupView selected = ResolveSelectedBattleRuntimeGroup();
        if (selected == null)
        {
            _unitRoot?.ClearCommandSelection();
            return;
        }
        HashSet<string> entityIds = BuildBattleRuntimeCommandGroupEntityIds(selected);
        int highlighted = _unitRoot == null ? 0 : _unitRoot.SetCommandSelectionByEntityIds(entityIds);
        GameLog.Info(
            nameof(WorldSiteRoot),
            $"BattleRuntimeCommandGroupHighlighted group={selected.GroupKey} entities={highlighted}");
    }
    private static HashSet<string> BuildBattleRuntimeCommandGroupEntityIds(BattleRuntimeCommandGroupView selected)
    {
        return BattleRuntimeHeroSkillTargetPresentation.BuildGroupEntityIds(selected?.Forces);
    }
    private void RefreshBattleRuntimeHeroFrame()
    {
        BattleRuntimeCommandGroupView selected = ResolveSelectedBattleRuntimeGroup();
        bool hasRuntime = _activeBattleGroupRuntimeResolution?.RuntimeController != null &&
                          _activeBattleGroupRuntimeResolution.RuntimeController.IsComplete == false;
        bool hasReadySkill = HasReadyBattleRuntimeSkill(selected, hasRuntime);
        BattleEntity heroEntity = BuildBattleRuntimeHeroSkillSourceEntity(selected);
        IReadOnlyList<BattleSkillSnapshot> skills = BuildBattleRuntimeSkillSnapshots(selected);
        _battleRuntimeHeroFramePresenter.Refresh(
            selected,
            BuildBattleRuntimePlayerGroups(),
            hasRuntime,
            _battleRuntimeCommandPauseActive,
            hasReadySkill,
            heroEntity,
            skills,
            group => HasReadyBattleRuntimeSkill(group, hasRuntime),
            skillId => ResolveSelectedHeroSkillUsageState(selected, skillId));
        GameLog.Info(
            nameof(WorldSiteRoot),
            $"BattleRuntimeHeroFrameRefreshed group={selected?.GroupKey ?? ""} skillReady={hasReadySkill}");
    }
    private IReadOnlyList<BattleSkillSnapshot> BuildBattleRuntimeSkillSnapshots(BattleRuntimeCommandGroupView selected)
    {
        IReadOnlyList<BattleSkillSnapshot> runtimeSkills = _activeBattleGroupRuntimeResolution?.RuntimeController?.State?.SkillDefinitions;
        IReadOnlyList<BattleSkillSnapshot> skills = runtimeSkills != null && runtimeSkills.Count > 0
            ? runtimeSkills
            : BattleSkillSnapshotFactory.CreateSelectedHeroSkillSnapshots();
        return BattleRuntimeSkillFilter.FilterForGroup(skills, selected);
    }
    private bool HasReadyBattleRuntimeSkill(BattleRuntimeCommandGroupView selected, bool hasRuntime) =>
        selected != null &&
        hasRuntime &&
        BuildBattleRuntimeSkillSnapshots(selected).Any(skill =>
            skill != null &&
            ResolveSelectedHeroSkillUsageState(selected, skill.SkillId) == BattleRuntimeSkillUsageState.Ready);
    private bool IsSelectedHeroSkillUsedOrPending(BattleRuntimeCommandGroupView selected, string skillId)
    {
        BattleRuntimeSkillUsageState state = ResolveSelectedHeroSkillUsageState(selected, skillId);
        return state is BattleRuntimeSkillUsageState.Pending or BattleRuntimeSkillUsageState.Used;
    }
    private BattleRuntimeSkillUsageState ResolveSelectedHeroSkillUsageState(
        BattleRuntimeCommandGroupView selected,
        string skillId)
    {
        return BattleRuntimeSkillUsageResolver.Resolve(
            selected,
            skillId,
            _activeBattleGroupRuntimeResolution?.RuntimeController?.EventStream?.Events,
            _activeBattleGroupRuntimeResolution?.RuntimeController?.State?.SpatialMarks,
            _activeBattleGroupRuntimeResolution?.RuntimeController?.CurrentTimeSeconds ?? 0);
    }
    private void OnBattleRuntimeHeroSkillPressed()
    {
        BattleRuntimeCommandGroupView selected = ResolveSelectedBattleRuntimeGroup();
        string skillId = BuildBattleRuntimeSkillSnapshots(selected).FirstOrDefault()?.SkillId ?? HeroSkillCommandIds.FirstSliceHeroSkillId;
        BeginBattleRuntimeSkillPress(selected, skillId);
    }
    private void OnBattleRuntimeSkillSlotPressed(string skillId) =>
        BeginBattleRuntimeSkillPress(ResolveSelectedBattleRuntimeGroup(), skillId);
    private void BeginBattleRuntimeSkillPress(BattleRuntimeCommandGroupView selected, string skillId)
    {
        if (selected == null)
        {
            SetSiteNoticeText("请选择参战英雄。");
            return;
        }
        string normalizedSkillId = string.IsNullOrWhiteSpace(skillId)
            ? HeroSkillCommandIds.FirstSliceHeroSkillId
            : skillId.Trim();
        IReadOnlyList<BattleSkillSnapshot> availableSkills = BuildBattleRuntimeSkillSnapshots(selected);
        BattleSkillSnapshot pressedSkill = availableSkills.FirstOrDefault(skill => string.Equals(skill.SkillId, normalizedSkillId, System.StringComparison.Ordinal));
        if (pressedSkill == null)
        {
            SetSiteNoticeText($"技能暂不可用：{BattleRuntimeSkillHudText.BuildUnavailableText("skill_caster_not_allowed")}");
            RefreshBattleRuntimeHeroFrame();
            return;
        }
        if (ResolveSelectedHeroSkillUsageState(selected, normalizedSkillId) != BattleRuntimeSkillUsageState.Ready)
        {
            SetSiteNoticeText($"技能暂不可用：{BattleRuntimeSkillHudText.BuildUnavailableText(IsSelectedHeroSkillUsedOrPending(selected, normalizedSkillId) ? "hero_skill_already_used" : "hero_actor_unavailable")}");
            RefreshBattleRuntimeHeroFrame();
            return;
        }
        if (!_battleRuntimeCommandPauseActive)
        {
            SetBattleRuntimeCommandPauseActive(true, "hero_skill_button");
        }
        GameLog.Info(
            nameof(WorldSiteRoot),
            $"BattleRuntimeHeroSkillPressed group={selected.GroupKey} skill={normalizedSkillId} pause={_battleRuntimeCommandPauseActive}");
        if (pressedSkill.TargetingMode == BattleSkillTargetingMode.None)
        {
            CancelBattleRuntimeHeroSkillTargetPicking("self_skill_submit");
            _battleRuntimeHeroSkillTargetPickingGroup = selected;
            _battleRuntimeHeroSkillTargetPickingSkillId = normalizedSkillId;
            BattleEntity source = BuildBattleRuntimeHeroSkillSourceEntity(selected);
            if (!TryResolveBattleRuntimeHeroSkillSourceActorId(source, out string sourceActorId)) { SetSiteNoticeText("技能暂不可用：当前英雄无法行动。"); RefreshBattleRuntimeHeroFrame(); return; }
            SubmitBattleRuntimeHeroSkillCommand(selected, sourceActorId, "");
            return;
        }
        BeginBattleRuntimeHeroSkillTargetPicking(selected, normalizedSkillId);
    }
    private void OnBattleRuntimeRegroupPressed()
    {
        BattleRuntimeCommandGroupView selected = ResolveSelectedBattleRuntimeGroup();
        if (selected == null)
        {
            SetSiteNoticeText("请选择参战英雄。");
            return;
        }
        if (!_battleRuntimeCommandPauseActive)
        {
            SetBattleRuntimeCommandPauseActive(true, "regroup_button");
        }
        SetSiteNoticeText($"{selected.DisplayName}：重整指令已选中，后续会接入完整运行时执行。");
        GameLog.Info(nameof(WorldSiteRoot), $"BattleRuntimeRegroupPressed group={selected.GroupKey}");
    }
    private void BeginBattleRuntimeHeroSkillTargetPicking(BattleRuntimeCommandGroupView selected, string skillId)
    {
        if (selected == null ||
            _activeBattleGroupRuntimeResolution?.RuntimeController == null ||
            _activeBattleGroupRuntimeResolution.RuntimeController.IsComplete)
        {
            SetSiteNoticeText("技能暂不可用：战斗运行时尚未准备好。");
            RefreshBattleRuntimeHeroFrame();
            return;
        }
        _battleRuntimeHeroSkillTargetPickingActive = true;
        _battleRuntimeHeroSkillTargetPickingGroup = selected;
        _battleRuntimeHeroSkillTargetPickingSkillId = string.IsNullOrWhiteSpace(skillId)
            ? HeroSkillCommandIds.FirstSliceHeroSkillId
            : skillId.Trim();
        _battleRuntimeHeroSkillPreviewTargetActorId = "";
        _battleRuntimeThunderFoldTargetingStage = string.Equals(_battleRuntimeHeroSkillTargetPickingSkillId, HeroSkillCommandIds.ThunderMarkFoldSkillId, System.StringComparison.Ordinal)
            ? ThunderFoldTargetingStage.MarkSelection
            : ThunderFoldTargetingStage.None;
        _battleRuntimeThunderFoldSelectedMarkId = "";
        _battleRuntimeThunderFoldSelectedMarkSurface = default;
        RefreshBattleRuntimeHeroSkillTargetPreview();
        SetSiteNoticeText(_battleRuntimeThunderFoldTargetingStage == ThunderFoldTargetingStage.MarkSelection
            ? $"{selected.DisplayName}：选择一个已有雷印。"
            : $"{selected.DisplayName}：选择一个敌方目标释放技能。");
        GameLog.Info(
            nameof(WorldSiteRoot),
            $"BattleRuntimeHeroSkillTargetPickingStarted group={selected.GroupKey} skill={_battleRuntimeHeroSkillTargetPickingSkillId}");
    }
    private bool TryHandleBattleRuntimeHeroSkillTargetInput(InputEvent inputEvent)
    {
        if (!_battleRuntimeHeroSkillTargetPickingActive)
        {
            return false;
        }
        if (inputEvent is InputEventMouseMotion)
        {
            RefreshBattleRuntimeHeroSkillTargetPreview();
            return false;
        }
        if (inputEvent is not InputEventMouseButton { Pressed: true } mouseButton)
        {
            return false;
        }
        if (BattleRuntimeCommandHudPointerGate.ContainsPointer(_battleRuntimeCommandBar, mouseButton.Position))
        {
            return false;
        }
        if (mouseButton.ButtonIndex == MouseButton.Right)
        {
            CancelBattleRuntimeHeroSkillTargetPicking("cancel_click");
            GetViewport()?.SetInputAsHandled();
            return true;
        }
        if (mouseButton.ButtonIndex != MouseButton.Left)
        {
            return false;
        }
        RefreshBattleRuntimeHeroSkillTargetPreview();
        if (!TryGetMouseGridPosition(out GridPosition position))
        {
            SetSiteNoticeText("请选择战场内的敌方目标。");
            GetViewport()?.SetInputAsHandled();
            return true;
        }
        BattleEntity source = BuildBattleRuntimeHeroSkillSourceEntity(_battleRuntimeHeroSkillTargetPickingGroup);
        if (!TryResolveBattleRuntimeHeroSkillSourceActorId(source, out string sourceActorId))
        {
            SetSiteNoticeText("技能暂不可用：当前英雄无法行动。");
            GetViewport()?.SetInputAsHandled();
            return true;
        }
        BattleSkillSnapshot pickedSkill = BuildBattleRuntimeSkillSnapshots(_battleRuntimeHeroSkillTargetPickingGroup)
            .FirstOrDefault(item => string.Equals(item?.SkillId, _battleRuntimeHeroSkillTargetPickingSkillId, System.StringComparison.Ordinal))
            ?? _activeBattleGroupRuntimeResolution?.RuntimeController?.State?.SkillDefinitions?
                .FirstOrDefault(item => string.Equals(item?.SkillId, _battleRuntimeHeroSkillTargetPickingSkillId, System.StringComparison.Ordinal));
        BattleEntity target = FindEntityAt(position);
        if (string.Equals(pickedSkill?.SkillId, HeroSkillCommandIds.ThunderMarkFoldSkillId, System.StringComparison.Ordinal))
        {
            if (_battleRuntimeThunderFoldTargetingStage == ThunderFoldTargetingStage.LandingSelection)
            {
                TrySubmitBattleRuntimeThunderFoldLanding(position, sourceActorId);
            }
            else if (TrySelectBattleRuntimeThunderFoldMark(position, target, out BattleRuntimeSpatialMark mark, out GridSurfacePosition markSurface))
            {
                BeginBattleRuntimeThunderFoldLandingSelection(mark, markSurface);
            }
            else
            {
                SetSiteNoticeText("雷印折跃需要先选择已有雷印。");
            }
            GetViewport()?.SetInputAsHandled();
            return true;
        }
        if (pickedSkill?.TargetingMode == BattleSkillTargetingMode.TargetedActorOrCell)
        {
            if (TryResolveBattleRuntimeHeroSkillTargetActorId(source, target, out string targetActorOrCellId) &&
                IsBattleRuntimeHeroSkillTargetInRange(source, target, pickedSkill.Range))
            {
                SubmitBattleRuntimeHeroSkillCommand(_battleRuntimeHeroSkillTargetPickingGroup, sourceActorId, targetActorOrCellId);
            }
            else if (IsBattleRuntimeHeroSkillCellInRange(source, position, pickedSkill.Range))
            {
                SubmitBattleRuntimeHeroSkillCommand(_battleRuntimeHeroSkillTargetPickingGroup, sourceActorId, "", position);
            }
            else
            {
                SetSiteNoticeText("请选择技能范围内的目标。");
                RefreshBattleRuntimeHeroSkillTargetPreview();
            }
            GetViewport()?.SetInputAsHandled();
            return true;
        }
        if (pickedSkill?.TargetingMode == BattleSkillTargetingMode.TargetedCell)
        {
            if (!IsBattleRuntimeHeroSkillCellInRange(source, position, pickedSkill.Range))
            {
                SetSiteNoticeText("请选择技能范围内的地块。");
                RefreshBattleRuntimeHeroSkillTargetPreview();
                GetViewport()?.SetInputAsHandled();
                return true;
            }

            SubmitBattleRuntimeHeroSkillCommand(_battleRuntimeHeroSkillTargetPickingGroup, sourceActorId, "", position);
            GetViewport()?.SetInputAsHandled();
            return true;
        }
        if (!TryResolveBattleRuntimeHeroSkillTargetActorId(source, target, out string targetActorId) ||
            !IsBattleRuntimeHeroSkillTargetInRange(source, target, pickedSkill?.Range ?? 0))
        {
            SetSiteNoticeText("请选择可被技能影响的敌方单位。");
            GetViewport()?.SetInputAsHandled();
            return true;
        }
        SubmitBattleRuntimeHeroSkillCommand(_battleRuntimeHeroSkillTargetPickingGroup, sourceActorId, targetActorId);
        GetViewport()?.SetInputAsHandled();
        return true;
    }
    private void SubmitBattleRuntimeHeroSkillCommand(BattleRuntimeCommandGroupView selected, string sourceActorId, string targetActorId, GridPosition? targetGrid = null, string selectedSpatialMarkId = "")
    {
        CommandRequest commandRequest = targetGrid.HasValue
            ? string.IsNullOrWhiteSpace(selectedSpatialMarkId)
                ? BuildBattleRuntimeHeroSkillCommandRequest(selected, sourceActorId, targetActorId, targetGrid)
                : BuildBattleRuntimeHeroSkillCommandRequest(selected, sourceActorId, targetActorId, targetGrid, selectedSpatialMarkId)
            : string.IsNullOrWhiteSpace(selectedSpatialMarkId)
                ? BuildBattleRuntimeHeroSkillCommandRequest(selected, sourceActorId, targetActorId)
                : BuildBattleRuntimeHeroSkillCommandRequest(selected, sourceActorId, targetActorId, null, selectedSpatialMarkId);
        Rpg.Runtime.Battle.BattleRuntimeCommandSubmitResult result =
            _activeBattleGroupRuntimeResolution?.RuntimeController?.SubmitCommand(commandRequest);
        bool accepted = result?.Accepted == true;
        if (accepted)
        {
            SetSiteNoticeText($"{selected?.DisplayName ?? "参战英雄"}：英雄技能已下达，恢复战斗后生效。");
        }
        else
        {
            SetSiteNoticeText($"技能暂不可用：{BattleRuntimeSkillHudText.BuildUnavailableText(result?.ReasonCode)}");
        }
        if (accepted)
        {
            CancelBattleRuntimeHeroSkillTargetPicking("submitted");
        }
        else
        {
            RefreshBattleRuntimeHeroSkillTargetPreview();
        }
        RefreshBattleRuntimeHeroFrame();
        GameLog.Info(
            nameof(WorldSiteRoot),
            $"BattleRuntimeHeroSkillSubmitted group={commandRequest.BattleGroupId} source={commandRequest.SourceActorId} skill={commandRequest.SkillId} target={commandRequest.TargetActorId} accepted={result?.Accepted == true} reason={result?.ReasonCode ?? "runtime_missing"} events={result?.Events.Count ?? 0}");
    }
    private CommandRequest BuildBattleRuntimeHeroSkillCommandRequest(BattleRuntimeCommandGroupView selected, string sourceActorId, string targetActorId, GridPosition? targetGrid = null, string selectedSpatialMarkId = "")
    {
        string groupKey = selected?.GroupKey ?? _selectedBattleRuntimeGroupKey ?? "";
        string battleId = _activeBattleGroupRuntimeResolution?.RuntimeController?.BattleId ?? _battleRuntimeRequest?.ContextId ?? "";
        string skillId = string.IsNullOrWhiteSpace(_battleRuntimeHeroSkillTargetPickingSkillId)
            ? HeroSkillCommandIds.FirstSliceHeroSkillId
            : _battleRuntimeHeroSkillTargetPickingSkillId;
        return BattleRuntimeHeroSkillCommandRequestFactory.BuildHeroSkillCommandRequest(groupKey, battleId, skillId, sourceActorId, targetActorId, targetGrid, selectedSpatialMarkId);
    }
    private void RefreshBattleRuntimeHeroSkillTargetPreview()
    {
        if (!_battleRuntimeHeroSkillTargetPickingActive)
        {
            return;
        }
        BattleEntity source = BuildBattleRuntimeHeroSkillSourceEntity(_battleRuntimeHeroSkillTargetPickingGroup);
        IReadOnlyList<GridPosition> rangeCells = BuildBattleRuntimeHeroSkillRangeCells(source);
        IReadOnlyList<GridPosition> targetCells = System.Array.Empty<GridPosition>();
        string targetActorId = "";
        bool isThunderFold = string.Equals(_battleRuntimeHeroSkillTargetPickingSkillId, HeroSkillCommandIds.ThunderMarkFoldSkillId, System.StringComparison.Ordinal);
        if (isThunderFold && _battleRuntimeThunderFoldTargetingStage == ThunderFoldTargetingStage.MarkSelection) { rangeCells = BuildBattleRuntimeThunderFoldMarkCells(); }
        else if (isThunderFold && _battleRuntimeThunderFoldTargetingStage == ThunderFoldTargetingStage.LandingSelection) { rangeCells = BuildBattleRuntimeThunderFoldLandingCells(source); }
        if (TryGetMouseGridPosition(out GridPosition position))
        {
            if (isThunderFold && _battleRuntimeThunderFoldTargetingStage == ThunderFoldTargetingStage.LandingSelection)
            {
                targetCells = rangeCells.Contains(position) ? new[] { position } : System.Array.Empty<GridPosition>();
            }
            else
            {
                BattleEntity target = FindEntityAt(position);
                if (isThunderFold && _battleRuntimeThunderFoldTargetingStage == ThunderFoldTargetingStage.MarkSelection)
                {
                    if (TrySelectBattleRuntimeThunderFoldMark(position, target, out BattleRuntimeSpatialMark mark, out GridSurfacePosition markSurface))
                    {
                        targetActorId = string.IsNullOrWhiteSpace(mark?.AttachedActorId) ? "" : mark.AttachedActorId;
                        targetCells = string.IsNullOrWhiteSpace(targetActorId)
                            ? new[] { new GridPosition(markSurface.X, markSurface.Y) }
                            : BattleRuntimeHeroSkillTargetPresentation.BuildFootprintCells(target);
                    }
                }
                else if (TryResolveBattleRuntimeHeroSkillTargetActorId(source, target, out targetActorId) &&
                         IsBattleRuntimeHeroSkillTargetInRange(source, target, ResolveBattleRuntimeHeroSkillRange()))
                {
                    targetCells = BattleRuntimeHeroSkillTargetPresentation.BuildFootprintCells(target);
                }
            }
        }
        _battleRuntimeHeroSkillPreviewTargetActorId = targetActorId;
        _highlightOverlay?.SetCellsBatch(
            (BattleGridHighlightKind.Skill, rangeCells),
            (BattleGridHighlightKind.Target, targetCells));
        _unitRoot?.SetAttackTargetPreviewByEntityId(targetActorId);
    }
    private void CancelBattleRuntimeHeroSkillTargetPicking(string reason)
    {
        if (!_battleRuntimeHeroSkillTargetPickingActive &&
            string.IsNullOrWhiteSpace(_battleRuntimeHeroSkillPreviewTargetActorId))
        {
            return;
        }
        _battleRuntimeHeroSkillTargetPickingActive = false;
        _battleRuntimeHeroSkillTargetPickingGroup = null;
        _battleRuntimeHeroSkillTargetPickingSkillId = "";
        _battleRuntimeHeroSkillPreviewTargetActorId = "";
        _battleRuntimeThunderFoldTargetingStage = ThunderFoldTargetingStage.None;
        _battleRuntimeThunderFoldSelectedMarkId = "";
        _battleRuntimeThunderFoldSelectedMarkSurface = default;
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Skill);
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Target);
        _unitRoot?.ClearAttackTargetPreview();
        GameLog.Info(nameof(WorldSiteRoot), $"BattleRuntimeHeroSkillTargetPickingCancelled reason={reason ?? ""}");
    }
    private bool TrySelectBattleRuntimeThunderFoldMark(GridPosition position, BattleEntity target, out BattleRuntimeSpatialMark mark, out GridSurfacePosition surface) => BattleRuntimeThunderFoldTargetingPresentation.TrySelectMark(_activeBattleGroupRuntimeResolution?.RuntimeController?.State?.SpatialMarks, _battleRuntimeHeroSkillTargetPickingGroup?.GroupKey ?? _selectedBattleRuntimeGroupKey, _activeBattleGroupRuntimeResolution?.RuntimeController?.CurrentTimeSeconds ?? 0, position, target, out mark, out surface);
    private void BeginBattleRuntimeThunderFoldLandingSelection(BattleRuntimeSpatialMark mark, GridSurfacePosition surface)
    {
        _battleRuntimeThunderFoldTargetingStage = ThunderFoldTargetingStage.LandingSelection; _battleRuntimeThunderFoldSelectedMarkId = mark?.MarkId ?? ""; _battleRuntimeThunderFoldSelectedMarkSurface = surface; _battleRuntimeHeroSkillPreviewTargetActorId = ""; RefreshBattleRuntimeHeroSkillTargetPreview();
        SetSiteNoticeText("雷印已选定：请选择雷印周围3格内的空地。");
    }
    private IReadOnlyList<GridPosition> BuildBattleRuntimeThunderFoldMarkCells() => BattleRuntimeThunderFoldTargetingPresentation.BuildMarkCells(_activeBattleGroupRuntimeResolution?.RuntimeController?.State?.SpatialMarks, _battleRuntimeHeroSkillTargetPickingGroup?.GroupKey ?? _selectedBattleRuntimeGroupKey, _activeBattleGroupRuntimeResolution?.RuntimeController?.CurrentTimeSeconds ?? 0, _unitRoot);
    private IReadOnlyList<GridPosition> BuildBattleRuntimeThunderFoldLandingCells(BattleEntity source) =>
        BattleRuntimeThunderFoldTargetingPresentation.BuildLandingCells(_activeGridMap, _unitRoot, source, _battleRuntimeThunderFoldSelectedMarkSurface);
    private void TrySubmitBattleRuntimeThunderFoldLanding(GridPosition position, string sourceActorId)
    {
        BattleEntity source = BuildBattleRuntimeHeroSkillSourceEntity(_battleRuntimeHeroSkillTargetPickingGroup);
        if (!BuildBattleRuntimeThunderFoldLandingCells(source).Contains(position))
        {
            SetSiteNoticeText("请选择雷印周围3格内的空地。");
            RefreshBattleRuntimeHeroSkillTargetPreview();
            return;
        }
        // The first click selects the Runtime mark; only this second legal landing click submits command intent.
        SubmitBattleRuntimeHeroSkillCommand(_battleRuntimeHeroSkillTargetPickingGroup, sourceActorId, "", position, _battleRuntimeThunderFoldSelectedMarkId);
    }
    private bool TryResolveBattleRuntimeHeroSkillTargetActorId(BattleEntity source, BattleEntity target, out string targetActorId) => BattleRuntimeHeroSkillTargetPresentation.TryResolveTargetActorId(source, target, out targetActorId);
    private bool TryResolveBattleRuntimeHeroSkillSourceActorId(BattleEntity source, out string sourceActorId) => BattleRuntimeHeroSkillTargetPresentation.TryResolveSourceActorId(source, out sourceActorId);
    // Local picking mirrors the drawn preview; Runtime remains the command authority.
    private bool IsBattleRuntimeHeroSkillTargetInRange(BattleEntity source, BattleEntity target, int range) => BattleRuntimeHeroSkillTargetPresentation.IsTargetInRange(source, target, range);
    private bool IsBattleRuntimeHeroSkillCellInRange(BattleEntity source, GridPosition position, int range) => BattleRuntimeHeroSkillTargetPresentation.IsCellInRange(source, position, range);
    private BattleEntity BuildBattleRuntimeHeroSkillSourceEntity(BattleRuntimeCommandGroupView selected) => BattleRuntimeHeroSkillTargetPresentation.ResolveSourceEntity(_unitRoot, selected?.Forces);
    private IReadOnlyList<GridPosition> BuildBattleRuntimeHeroSkillRangeCells(BattleEntity source) => BattleRuntimeHeroSkillTargetPresentation.BuildRangeCells(source, _activeGridMap, ResolveBattleRuntimeHeroSkillRange());
    private int ResolveBattleRuntimeHeroSkillRange()
    {
        string skillId = string.IsNullOrWhiteSpace(_battleRuntimeHeroSkillTargetPickingSkillId) ? HeroSkillCommandIds.FirstSliceHeroSkillId : _battleRuntimeHeroSkillTargetPickingSkillId;
        return (BuildBattleRuntimeSkillSnapshots(_battleRuntimeHeroSkillTargetPickingGroup).FirstOrDefault(item => string.Equals(item?.SkillId, skillId, System.StringComparison.Ordinal))
            ?? _activeBattleGroupRuntimeResolution?.RuntimeController?.State?.SkillDefinitions?.FirstOrDefault(item => string.Equals(item?.SkillId, skillId, System.StringComparison.Ordinal)))?.Range ?? 0;
    }
    private IReadOnlyList<BattleRuntimeCommandGroupView> BuildBattleRuntimePlayerGroups()
    {
        BattleStartRequest request = _battleRuntimeRequest;
        if (request == null && TryResolveActiveBattleRequest(out BattleStartRequest activeRequest))
        {
            request = activeRequest;
        }
        return BattleRuntimeCommandHudModel.BuildPlayerGroups(request, _battleUnitFactory.ResolveUnitDisplayName);
    }
    private BattleRuntimeCommandGroupView ResolveSelectedBattleRuntimeGroup()
    {
        IReadOnlyList<BattleRuntimeCommandGroupView> groups = BuildBattleRuntimePlayerGroups();
        BattleRuntimeCommandGroupView selected = groups
            .FirstOrDefault(group => string.Equals(group.GroupKey, _selectedBattleRuntimeGroupKey, System.StringComparison.Ordinal));
        if (selected != null)
        {
            return selected;
        }
        selected = groups.FirstOrDefault();
        _selectedBattleRuntimeGroupKey = selected?.GroupKey ?? "";
        return selected;
    }
}
