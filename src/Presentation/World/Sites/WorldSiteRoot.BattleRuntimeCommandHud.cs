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
using Rpg.Runtime.Battle.Events;

namespace Rpg.Presentation.World.Sites;

public partial class WorldSiteRoot
{
    private enum BattleRuntimeSkillUsageState
    {
        Unavailable,
        Ready,
        Pending,
        Used
    }

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
        ApplyBattleRuntimeCommandToRequest(_battlePreparationRequest, commandRequest);
        if (_isBattlePreparationActive)
        {
            RefreshBattlePreparationUi($"战斗姿态已设为：{BattleCorpsCommandLabels.ToDisplayText(command)}。");
        }

        RefreshBattleRuntimeHeroFrame();
        GameLog.Info(
            nameof(WorldSiteRoot),
            $"BattleRuntimeCommandSelected command={commandRequest.CommandId} kind={commandRequest.Kind} request={commandRequest.BattleId}");
    }

    private CommandRequest BuildBattleRuntimeCommandRequest(BattleCorpsCommand command)
    {
        BattleStartRequest request = _battlePreparationRequest;
        if (request == null && BattleSessionHandoff.TryPeekActiveRequest(out BattleStartRequest activeRequest))
        {
            request = activeRequest;
        }

        return new CommandRequest
        {
            CommandId = NormalizeBattleCorpsCommandId(command),
            BattleId = request?.RequestId ?? "",
            BattleGroupId = "player_corps_initial_posture",
            Channel = CommandChannel.Corps,
            Kind = ToCommandKind(command)
        };
    }

    private static void ApplyBattleRuntimeCommandToRequest(
        BattleStartRequest request,
        CommandRequest commandRequest)
    {
        if (request == null || commandRequest == null)
        {
            return;
        }

        request.InitialCorpsCommandId = commandRequest.CommandId ?? "";
    }

    private void RefreshBattleRuntimeCommandControls(bool runtimeLocked)
    {
        RefreshBattleRuntimeHeroFrame();
    }

    private void RefreshBattleRuntimeCommandPausePresentation()
    {
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

        if (_battleRuntimeHeroFrame != null)
        {
            _battleRuntimeHeroFrame.Visible = true;
        }

        if (_battleRuntimeHeroNameLabel != null)
        {
            _battleRuntimeHeroNameLabel.Text = selected?.DisplayName ?? "参战英雄";
        }

        if (_battleRuntimeHeroStateLabel != null)
        {
            _battleRuntimeHeroStateLabel.Text = BuildBattleRuntimeHeroStateText(selected, hasRuntime, hasReadySkill);
        }

        BattleEntity heroEntity = BuildBattleRuntimeHeroSkillSourceEntity(selected);
        HealthComponent health = heroEntity?.GetComponent<HealthComponent>();
        SetProgressBar(_battleRuntimeHeroHealthBar, health?.Hp ?? 1, health?.MaxHp ?? 1);
        // V0 has no Runtime-owned mana yet. Keep the authored mana bar visible as
        // future resource space without inventing a second gameplay resource.
        SetProgressBar(_battleRuntimeHeroManaBar, 1, 1);
        RefreshBattleRuntimeSkillList(selected, hasRuntime);

        if (_battleRuntimeRegroupButton != null)
        {
            _battleRuntimeRegroupButton.Disabled = selected == null || !hasRuntime;
            _battleRuntimeRegroupButton.TooltipText = "重整：先作为战斗中指挥入口，后续接入完整重整命令。";
        }

        GameLog.Info(
            nameof(WorldSiteRoot),
            $"BattleRuntimeHeroFrameRefreshed group={selected?.GroupKey ?? ""} skillReady={hasReadySkill}");
    }
    private static void SetProgressBar(ProgressBar progressBar, int value, int maxValue)
    {
        if (progressBar == null)
        {
            return;
        }

        int safeMax = System.Math.Max(1, maxValue);
        progressBar.MinValue = 0;
        progressBar.MaxValue = safeMax;
        progressBar.Value = System.Math.Clamp(value, 0, safeMax);
    }

    private void RefreshBattleRuntimeSkillList(BattleRuntimeCommandGroupView selected, bool hasRuntime)
    {
        if (_battleRuntimeHeroSkillList == null)
        {
            return;
        }

        IReadOnlyList<BattleSkillSnapshot> skills = BuildBattleRuntimeSkillSnapshots();
        HashSet<string> liveSkillIds = skills
            .Where(skill => !string.IsNullOrWhiteSpace(skill?.SkillId))
            .Select(skill => skill.SkillId)
            .ToHashSet(System.StringComparer.Ordinal);
        foreach (string staleSkillId in _battleRuntimeSkillSlots.Keys.Where(skillId => !liveSkillIds.Contains(skillId)).ToArray())
        {
            if (_battleRuntimeSkillSlots.Remove(staleSkillId, out BattleRuntimeSkillSlot staleSlot))
            {
                staleSlot?.QueueFree();
            }
        }

        foreach (BattleSkillSnapshot skill in skills)
        {
            if (skill == null || string.IsNullOrWhiteSpace(skill.SkillId))
            {
                continue;
            }

            if (!_battleRuntimeSkillSlots.TryGetValue(skill.SkillId, out BattleRuntimeSkillSlot slot) ||
                slot == null ||
                !GodotObject.IsInstanceValid(slot))
            {
                slot = GameUiSceneFactory.CreateBattleRuntimeSkillSlot(nameof(WorldSiteRoot));
                if (slot == null)
                {
                    continue;
                }

                slot.Pressed += OnBattleRuntimeSkillSlotPressed;
                _battleRuntimeHeroSkillList.AddChild(slot);
                _battleRuntimeSkillSlots[skill.SkillId] = slot;
            }

            BattleRuntimeSkillUsageState usageState = ResolveSelectedHeroSkillUsageState(selected, skill.SkillId);
            bool available = selected != null && hasRuntime && usageState == BattleRuntimeSkillUsageState.Ready;
            slot.Bind(
                skill.SkillId,
                skill.DisplayName,
                available,
                BuildBattleRuntimeSkillStatusText(selected, hasRuntime, usageState),
                cooldownRemainingSeconds: 0.0);
        }
    }

    private IReadOnlyList<BattleSkillSnapshot> BuildBattleRuntimeSkillSnapshots()
    {
        IReadOnlyList<BattleSkillSnapshot> runtimeSkills = _activeBattleGroupRuntimeResolution?.RuntimeController?.State?.SkillDefinitions;
        return runtimeSkills != null && runtimeSkills.Count > 0
            ? runtimeSkills
            : BattleSkillSnapshotFactory.CreateSelectedHeroSkillSnapshots();
    }

    private bool HasReadyBattleRuntimeSkill(BattleRuntimeCommandGroupView selected, bool hasRuntime) =>
        selected != null &&
        hasRuntime &&
        BuildBattleRuntimeSkillSnapshots().Any(skill =>
            skill != null &&
            ResolveSelectedHeroSkillUsageState(selected, skill.SkillId) == BattleRuntimeSkillUsageState.Ready);

    private static string BuildBattleRuntimeSkillStatusText(
        BattleRuntimeCommandGroupView selected,
        bool hasRuntime,
        BattleRuntimeSkillUsageState usageState)
    {
        if (selected == null)
        {
            return "未选";
        }

        if (!hasRuntime)
        {
            return "未就绪";
        }

        return usageState switch
        {
            BattleRuntimeSkillUsageState.Pending => "待",
            BattleRuntimeSkillUsageState.Used => "已用",
            _ => ""
        };
    }

    private string BuildBattleRuntimeHeroStateText(
        BattleRuntimeCommandGroupView selected,
        bool hasRuntime,
        bool skillReady)
    {
        if (selected == null)
        {
            return "未选择";
        }

        string pauseText = _battleRuntimeCommandPauseActive ? "暂停指挥" : "战斗中";
        string skillText = skillReady ? "技能可用" : "技能锁定";
        return $"{pauseText} / {skillText}";
    }
    private bool IsSelectedHeroSkillUsedOrPending(BattleRuntimeCommandGroupView selected) =>
        IsSelectedHeroSkillUsedOrPending(selected, HeroSkillCommandIds.FirstSliceHeroSkillId);

    private bool IsSelectedHeroSkillUsedOrPending(BattleRuntimeCommandGroupView selected, string skillId)
    {
        BattleRuntimeSkillUsageState state = ResolveSelectedHeroSkillUsageState(selected, skillId);
        return state is BattleRuntimeSkillUsageState.Pending or BattleRuntimeSkillUsageState.Used;
    }

    private BattleRuntimeSkillUsageState ResolveSelectedHeroSkillUsageState(
        BattleRuntimeCommandGroupView selected,
        string skillId)
    {
        if (selected == null)
        {
            return BattleRuntimeSkillUsageState.Unavailable;
        }

        string normalizedSkillId = string.IsNullOrWhiteSpace(skillId)
            ? HeroSkillCommandIds.FirstSliceHeroSkillId
            : skillId.Trim();
        IReadOnlyList<BattleEvent> events = _activeBattleGroupRuntimeResolution?.RuntimeController?.EventStream?.Events;
        if (events == null)
        {
            return BattleRuntimeSkillUsageState.Ready;
        }

        bool used = events.Any(item =>
            item != null &&
            item.Kind == BattleEventKind.SkillUsed &&
            string.Equals(item.BattleGroupId ?? "", selected.GroupKey, System.StringComparison.Ordinal) &&
            string.Equals(item.SourceDefinitionId ?? "", normalizedSkillId, System.StringComparison.Ordinal));
        if (used)
        {
            return BattleRuntimeSkillUsageState.Used;
        }

        HashSet<string> failedCommandIds = events
            .Where(item =>
                item != null &&
                item.Kind == BattleEventKind.CommandFailed &&
                string.Equals(item.BattleGroupId ?? "", selected.GroupKey, System.StringComparison.Ordinal) &&
                string.Equals(item.SourceDefinitionId ?? "", normalizedSkillId, System.StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(item.SourceCommandId))
            .Select(item => item.SourceCommandId)
            .ToHashSet(System.StringComparer.Ordinal);

        bool pending = events.Any(item =>
            item != null &&
            string.Equals(item.BattleGroupId ?? "", selected.GroupKey, System.StringComparison.Ordinal) &&
            string.Equals(item.SourceDefinitionId ?? "", normalizedSkillId, System.StringComparison.Ordinal) &&
            item.Kind == BattleEventKind.CommandAccepted &&
            !failedCommandIds.Contains(item.SourceCommandId ?? ""));
        return pending
            ? BattleRuntimeSkillUsageState.Pending
            : BattleRuntimeSkillUsageState.Ready;
    }
    private void OnBattleRuntimeHeroSkillPressed() =>
        BeginBattleRuntimeSkillPress(ResolveSelectedBattleRuntimeGroup(), HeroSkillCommandIds.FirstSliceHeroSkillId);

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
        if (ResolveSelectedHeroSkillUsageState(selected, normalizedSkillId) != BattleRuntimeSkillUsageState.Ready)
        {
            SetSiteNoticeText($"技能暂不可用：{BuildBattleRuntimeHeroSkillUnavailableText(IsSelectedHeroSkillUsedOrPending(selected, normalizedSkillId) ? "hero_skill_already_used" : "hero_actor_unavailable")}");
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

    private void BeginBattleRuntimeHeroSkillTargetPicking(BattleRuntimeCommandGroupView selected)
    {
        BeginBattleRuntimeHeroSkillTargetPicking(selected, HeroSkillCommandIds.FirstSliceHeroSkillId);
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
        RefreshBattleRuntimeHeroSkillTargetPreview();
        SetSiteNoticeText($"{selected.DisplayName}：选择一个敌方目标释放技能。");
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

        BattleEntity target = FindEntityAt(position);
        if (!TryResolveBattleRuntimeHeroSkillTargetActorId(source, target, out string targetActorId))
        {
            SetSiteNoticeText("请选择可被技能影响的敌方单位。");
            GetViewport()?.SetInputAsHandled();
            return true;
        }

        SubmitBattleRuntimeHeroSkillCommand(_battleRuntimeHeroSkillTargetPickingGroup, sourceActorId, targetActorId);
        GetViewport()?.SetInputAsHandled();
        return true;
    }

    private void SubmitBattleRuntimeHeroSkillCommand(BattleRuntimeCommandGroupView selected, string sourceActorId, string targetActorId)
    {
        CommandRequest commandRequest = BuildBattleRuntimeHeroSkillCommandRequest(selected, sourceActorId, targetActorId);
        Rpg.Runtime.Battle.BattleRuntimeCommandSubmitResult result =
            _activeBattleGroupRuntimeResolution?.RuntimeController?.SubmitCommand(commandRequest);
        bool accepted = result?.Accepted == true;
        if (accepted)
        {
            SetSiteNoticeText($"{selected?.DisplayName ?? "参战英雄"}：英雄技能已下达，恢复战斗后生效。");
        }
        else
        {
            SetSiteNoticeText($"技能暂不可用：{BuildBattleRuntimeHeroSkillUnavailableText(result?.ReasonCode)}");
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

    private CommandRequest BuildBattleRuntimeHeroSkillCommandRequest(BattleRuntimeCommandGroupView selected, string sourceActorId, string targetActorId)
    {
        string groupKey = selected?.GroupKey ?? _selectedBattleRuntimeGroupKey ?? "";
        string battleId = _activeBattleGroupRuntimeResolution?.RuntimeController?.BattleId ?? _battleRuntimeRequest?.ContextId ?? "";
        string skillId = string.IsNullOrWhiteSpace(_battleRuntimeHeroSkillTargetPickingSkillId)
            ? HeroSkillCommandIds.FirstSliceHeroSkillId
            : _battleRuntimeHeroSkillTargetPickingSkillId;
        return new CommandRequest
        {
            CommandId = $"hero_skill:{groupKey}:{skillId}",
            BattleId = battleId,
            BattleGroupId = groupKey,
            SourceActorId = sourceActorId ?? "",
            Channel = CommandChannel.Hero,
            Kind = CommandKind.CastSkill,
            SkillId = skillId,
            TargetActorId = targetActorId
        };
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
        if (TryGetMouseGridPosition(out GridPosition position))
        {
            BattleEntity target = FindEntityAt(position);
            if (TryResolveBattleRuntimeHeroSkillTargetActorId(source, target, out targetActorId))
            {
                targetCells = BattleRuntimeHeroSkillTargetPresentation.BuildFootprintCells(target);
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
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Skill);
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Target);
        _unitRoot?.ClearAttackTargetPreview();
        GameLog.Info(nameof(WorldSiteRoot), $"BattleRuntimeHeroSkillTargetPickingCancelled reason={reason ?? ""}");
    }

    private bool TryResolveBattleRuntimeHeroSkillTargetActorId(BattleEntity source, BattleEntity target, out string targetActorId)
    {
        return BattleRuntimeHeroSkillTargetPresentation.TryResolveTargetActorId(
            source,
            target,
            out targetActorId);
    }

    private bool TryResolveBattleRuntimeHeroSkillSourceActorId(BattleEntity source, out string sourceActorId) => BattleRuntimeHeroSkillTargetPresentation.TryResolveSourceActorId(source, out sourceActorId);

    private BattleEntity BuildBattleRuntimeHeroSkillSourceEntity(BattleRuntimeCommandGroupView selected) => BattleRuntimeHeroSkillTargetPresentation.ResolveSourceEntity(_unitRoot, selected?.Forces);

    private IReadOnlyList<GridPosition> BuildBattleRuntimeHeroSkillRangeCells(BattleEntity source) => BattleRuntimeHeroSkillTargetPresentation.BuildRangeCells(source, _activeGridMap, ResolveBattleRuntimeHeroSkillRange());

    private int ResolveBattleRuntimeHeroSkillRange()
    {
        string skillId = string.IsNullOrWhiteSpace(_battleRuntimeHeroSkillTargetPickingSkillId)
            ? HeroSkillCommandIds.FirstSliceHeroSkillId
            : _battleRuntimeHeroSkillTargetPickingSkillId;
        return _activeBattleGroupRuntimeResolution?.RuntimeController?.State?.SkillDefinitions?
            .FirstOrDefault(item => string.Equals(item?.SkillId, skillId, System.StringComparison.Ordinal))
            ?.Range ?? 0;
    }

    private static string BuildBattleRuntimeHeroSkillUnavailableText(string reasonCode)
    {
        return reasonCode switch
        {
            "hero_skill_already_pending" => "技能指令正在等待结算",
            "hero_skill_already_used" => "本场战斗已经使用过",
            "hero_actor_unavailable" => "当前英雄无法行动",
            "hero_skill_target_missing" => "没有可影响的敌方目标",
            "battle_already_complete" => "战斗已经结束",
            "battle_id_mismatch" => "战斗上下文不匹配",
            _ => "战斗运行时尚未准备好"
        };
    }
    private IReadOnlyList<BattleRuntimeCommandGroupView> BuildBattleRuntimePlayerGroups()
    {
        BattleStartRequest request = _battleRuntimeRequest;
        if (request == null && BattleSessionHandoff.TryPeekActiveRequest(out BattleStartRequest activeRequest))
        {
            request = activeRequest;
        }

        List<BattleForceRequest> forces = (request?.PlayerForces ?? new List<BattleForceRequest>())
            .Where(force => force != null && force.Count > 0)
            .ToList();
        return forces
            .GroupBy(ResolveBattleRuntimeGroupKey, System.StringComparer.Ordinal)
            .Select(group => BuildBattleRuntimeCommandGroup(group.Key, group.ToArray()))
            .Where(group => !string.IsNullOrWhiteSpace(group.GroupKey))
            .ToArray();
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

    private BattleRuntimeCommandGroupView BuildBattleRuntimeCommandGroup(
        string groupKey,
        IReadOnlyList<BattleForceRequest> forces)
    {
        BattleForceRequest heroForce = forces?.FirstOrDefault(IsLikelyHeroForce) ?? forces?.FirstOrDefault();
        string heroName = _battleUnitFactory.ResolveUnitDisplayName(heroForce?.UnitDefinitionId ?? "");
        string corpsSummary = BuildBattleRuntimeCorpsSummary(forces, heroForce);
        return new BattleRuntimeCommandGroupView
        {
            GroupKey = groupKey ?? "",
            DisplayName = string.IsNullOrWhiteSpace(heroName) ? groupKey ?? "参战英雄" : heroName,
            HeroName = string.IsNullOrWhiteSpace(heroName) ? "英雄：参战英雄" : $"英雄：{heroName}",
            CorpsSummary = corpsSummary,
            DefaultFormationId = BattlePreparationPlanUiModel.ResolveDefaultFormationId(forces),
            Forces = forces?.ToArray() ?? System.Array.Empty<BattleForceRequest>()
        };
    }

    private string BuildBattleRuntimeCorpsSummary(
        IReadOnlyList<BattleForceRequest> forces,
        BattleForceRequest heroForce)
    {
        List<string> corps = (forces ?? System.Array.Empty<BattleForceRequest>())
            .Where(force => force != null && !ReferenceEquals(force, heroForce))
            .Select(force => $"{_battleUnitFactory.ResolveUnitDisplayName(force.UnitDefinitionId)} x{force.Count}")
            .ToList();
        return corps.Count == 0
            ? "部队：无附属部队"
            : $"部队：{string.Join("，", corps)}";
    }

    private static bool IsLikelyHeroForce(BattleForceRequest force)
    {
        return force != null &&
               (string.Equals(force.UnitDefinitionId, Rpg.Application.World.HeroCorpsV0PlayableSliceIds.HeroUnit, System.StringComparison.Ordinal) ||
                force.UnitDefinitionId?.Contains("hero", System.StringComparison.OrdinalIgnoreCase) == true ||
                force.SourceKind?.Contains("Hero", System.StringComparison.OrdinalIgnoreCase) == true);
    }

    private static string ResolveBattleRuntimeGroupKey(BattleForceRequest force)
    {
        if (force == null)
        {
            return "";
        }

        string runtimeCommanderGroupId = BattleCommanderGroupIdentity.BuildProbeCommanderGroupId(
            force,
            string.IsNullOrWhiteSpace(force.ForceId) ? force.UnitDefinitionId ?? "" : force.ForceId);
        if (!string.IsNullOrWhiteSpace(runtimeCommanderGroupId))
        {
            return runtimeCommanderGroupId;
        }

        if (!string.IsNullOrWhiteSpace(force.SourceKind) && !string.IsNullOrWhiteSpace(force.SourceId))
        {
            return $"{force.SourceKind}:{force.SourceId}";
        }

        if (!string.IsNullOrWhiteSpace(force.SourceId))
        {
            return force.SourceId;
        }

        return string.IsNullOrWhiteSpace(force.ForceId) ? force.UnitDefinitionId ?? "" : force.ForceId;
    }

    private static CommandKind ToCommandKind(BattleCorpsCommand command)
    {
        return command == BattleCorpsCommand.HoldLine
            ? CommandKind.Hold
            : CommandKind.Attack;
    }

    private static string NormalizeBattleCorpsCommandId(BattleCorpsCommand command)
    {
        return command switch
        {
            BattleCorpsCommand.FocusFire => nameof(BattleCorpsCommand.FocusFire),
            BattleCorpsCommand.HoldLine => nameof(BattleCorpsCommand.HoldLine),
            _ => nameof(BattleCorpsCommand.Assault)
        };
    }

    private static BattleCorpsCommand ResolveBattleCorpsCommand(string commandId)
    {
        if (System.Enum.TryParse(commandId?.Trim() ?? "", ignoreCase: true, out BattleCorpsCommand command))
        {
            return command;
        }

        return BattleCorpsCommand.Assault;
    }
}
