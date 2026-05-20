using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Commands;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Battle.Flow;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.World.Sites;

public partial class WorldSiteRoot
{
    private sealed class BattleRuntimeCommandGroupView
    {
        public string GroupKey { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public string HeroName { get; init; } = "";
        public string CorpsSummary { get; init; } = "";
        public IReadOnlyList<BattleForceRequest> Forces { get; init; } = System.Array.Empty<BattleForceRequest>();
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
        _battleRuntimeCommandPauseActive = !_battleRuntimeCommandPauseActive;
        if (_battleRuntimeCommandPauseActive)
        {
            EnsureSelectedBattleRuntimeCommandGroup();
        }
        else
        {
            _unitRoot?.ClearCommandSelection();
        }

        RefreshBattleRuntimeCommandPausePresentation();
        GameLog.Info(
            nameof(WorldSiteRoot),
            $"BattleRuntimeCommandPauseToggled paused={_battleRuntimeCommandPauseActive} selectedGroup={_selectedBattleRuntimeGroupKey}");
    }

    private void SubmitBattleRuntimeCommand(BattleCorpsCommand command)
    {
        CommandRequest commandRequest = BuildBattleRuntimeCommandRequest(command);
        if (_battleRuntimeEnabled && !_isBattlePreparationActive)
        {
            string lockedNotice = "V0 开战后指令已锁定，请在战前部署阶段选择突击、集火或坚守。";
            SetSiteNoticeText(lockedNotice);
            RefreshBattleRuntimeCommandControls(runtimeLocked: true);
            GameLog.Info(
                nameof(WorldSiteRoot),
                $"BattleRuntimeCommandRejected command={commandRequest.CommandId} reason=runtime_locked request={commandRequest.BattleId}");
            return;
        }

        _selectedBattleCorpsCommand = command;
        ApplyBattleRuntimeCommandToRequest(_battlePreparationRequest, commandRequest);
        string notice = $"战斗指令已设为：{BattleCorpsCommandLabels.ToDisplayText(command)}。";
        if (_isBattlePreparationActive)
        {
            RefreshBattlePreparationUi(notice);
        }

        RefreshBattleRuntimeCommandControls(runtimeLocked: false);
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
        string statusText = BuildBattleRuntimeCommandStatusText(runtimeLocked);
        if (_battleRuntimeCommandLabel != null)
        {
            _battleRuntimeCommandLabel.Text = statusText;
        }

        if (_battleRuntimePauseHintLabel != null)
        {
            _battleRuntimePauseHintLabel.Text = _battleRuntimeCommandPauseActive
                ? "战斗已暂停：选择下方英雄后，在左侧查看英雄与部队指令"
                : "空格：暂停并选择参战英雄";
        }

        if (_battleRuntimeCommandButtonRow != null)
        {
            _battleRuntimeCommandButtonRow.Visible = !runtimeLocked;
        }

        ConfigureBattleRuntimeCommandButton(_battleRuntimeAssaultButton, BattleCorpsCommand.Assault, runtimeLocked);
        ConfigureBattleRuntimeCommandButton(_battleRuntimeFocusFireButton, BattleCorpsCommand.FocusFire, runtimeLocked);
        ConfigureBattleRuntimeCommandButton(_battleRuntimeHoldLineButton, BattleCorpsCommand.HoldLine, runtimeLocked);
        RefreshBattleRuntimeHeroBar();
        RefreshBattleRuntimeSelectedCommandPanel();
    }

    private void RefreshBattleRuntimeCommandPausePresentation()
    {
        if (_battleRuntimeCommandPauseActive)
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
        }

        RefreshBattleRuntimeCommandControls(runtimeLocked: _battleRuntimeEnabled && !_isBattlePreparationActive);
        UpdateSitePeacetimePanelVisibility("battle_runtime_command_pause");
        UpdateMainWorldViewportLayout("battle_runtime_command_pause");
    }

    private void RefreshBattleRuntimeHeroBar()
    {
        if (_battleRuntimeHeroButtonRow == null)
        {
            return;
        }

        ClearChildren(_battleRuntimeHeroButtonRow);
        _battleRuntimeHeroButtonRow.Visible = _battleRuntimeCommandPauseActive;
        if (!_battleRuntimeCommandPauseActive)
        {
            return;
        }

        IReadOnlyList<BattleRuntimeCommandGroupView> groups = BuildBattleRuntimePlayerGroups();
        foreach (BattleRuntimeCommandGroupView group in groups)
        {
            Button button = GameUiSceneFactory.CreateWorldSecondaryActionButton(nameof(WorldSiteRoot));
            if (button == null)
            {
                continue;
            }

            bool selected = string.Equals(group.GroupKey, _selectedBattleRuntimeGroupKey, System.StringComparison.Ordinal);
            button.Text = selected ? $"已选 {group.DisplayName}" : group.DisplayName;
            string capturedGroupKey = group.GroupKey;
            button.Pressed += () => SelectBattleRuntimeCommandGroup(capturedGroupKey);
            _battleRuntimeHeroButtonRow.AddChild(button);
        }

        if (groups.Count == 0)
        {
            AddMutedLine(_battleRuntimeHeroButtonRow, "无参战英雄");
        }
    }

    private void SelectBattleRuntimeCommandGroup(string groupKey)
    {
        _selectedBattleRuntimeGroupKey = groupKey ?? "";
        ApplyBattleRuntimeCommandGroupHighlight();
        RefreshBattleRuntimeHeroBar();
        RefreshBattleRuntimeSelectedCommandPanel();
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
        BattleRuntimeCommandGroupView selected = BuildBattleRuntimePlayerGroups()
            .FirstOrDefault(group => string.Equals(group.GroupKey, _selectedBattleRuntimeGroupKey, System.StringComparison.Ordinal));
        if (selected == null)
        {
            _unitRoot?.ClearCommandSelection();
            return;
        }

        HashSet<string> entityIds = selected.Forces
            .Where(force => !string.IsNullOrWhiteSpace(force?.ForceId))
            .SelectMany(force => Enumerable.Range(1, System.Math.Max(0, force.Count))
                .Select(index => $"{force.ForceId}:{index}"))
            .ToHashSet(System.StringComparer.Ordinal);
        int highlighted = _unitRoot == null ? 0 : _unitRoot.SetCommandSelectionByEntityIds(entityIds);
        GameLog.Info(
            nameof(WorldSiteRoot),
            $"BattleRuntimeCommandGroupHighlighted group={selected.GroupKey} entities={highlighted}");
    }

    private void RefreshBattleRuntimeSelectedCommandPanel()
    {
        bool visible = _battleRuntimeCommandPauseActive;
        SetBattleRuntimeCommandPanelVisible(visible);
        if (!visible)
        {
            return;
        }

        BattleRuntimeCommandGroupView selected = BuildBattleRuntimePlayerGroups()
            .FirstOrDefault(group => string.Equals(group.GroupKey, _selectedBattleRuntimeGroupKey, System.StringComparison.Ordinal));
        if (selected == null)
        {
            if (_battleRuntimeSelectedHeroLabel != null)
            {
                _battleRuntimeSelectedHeroLabel.Text = "未选择参战英雄";
            }

            ClearChildren(_battleRuntimeHeroCommandList);
            ClearChildren(_battleRuntimeCorpsCommandList);
            ClearChildren(_battleRuntimeCombinedCommandList);
            return;
        }

        if (_battleRuntimeSelectedHeroLabel != null)
        {
            _battleRuntimeSelectedHeroLabel.Text = $"{selected.HeroName}\n{selected.CorpsSummary}";
        }

        if (_battleRuntimeCorpsLabel != null)
        {
            _battleRuntimeCorpsLabel.Text = "部队指令";
        }

        if (_battleRuntimeCombinedLabel != null)
        {
            _battleRuntimeCombinedLabel.Text = "编组指令";
        }

        BindBattleRuntimeCommandDrafts(selected);
    }

    private void BindBattleRuntimeCommandDrafts(BattleRuntimeCommandGroupView selected)
    {
        ClearChildren(_battleRuntimeHeroCommandList);
        ClearChildren(_battleRuntimeCorpsCommandList);
        ClearChildren(_battleRuntimeCombinedCommandList);

        AddBattleRuntimeCommandDraftButton(_battleRuntimeHeroCommandList, selected, "hero_move", "移动", "英雄移动到指定位置");
        AddBattleRuntimeCommandDraftButton(_battleRuntimeHeroCommandList, selected, "hero_hold", "坚守", "英雄留在当前位置接敌");
        AddBattleRuntimeCommandDraftButton(_battleRuntimeHeroCommandList, selected, "hero_attack", "攻击", "英雄优先攻击目标");
        AddBattleRuntimeCommandDraftButton(_battleRuntimeHeroCommandList, selected, "hero_retreat", "撤退", "英雄向安全区域脱离");

        AddBattleRuntimeCommandDraftButton(_battleRuntimeCorpsCommandList, selected, "corps_advance", "推进", "部队向当前战线推进");
        AddBattleRuntimeCommandDraftButton(_battleRuntimeCorpsCommandList, selected, "corps_guard", "护卫", "部队回到英雄附近");
        AddBattleRuntimeCommandDraftButton(_battleRuntimeCorpsCommandList, selected, "corps_hold", "固守", "部队守住当前区域");
        AddBattleRuntimeCommandDraftButton(_battleRuntimeCorpsCommandList, selected, "corps_attack", "攻击", "部队压向目标或区域");
        AddBattleRuntimeCommandDraftButton(_battleRuntimeCorpsCommandList, selected, "corps_retreat", "撤退", "部队脱离战线");

        AddBattleRuntimeCommandDraftButton(_battleRuntimeCombinedCommandList, selected, "company_move", "编组移动", "英雄与部队一起移动");
        AddBattleRuntimeCommandDraftButton(_battleRuntimeCombinedCommandList, selected, "company_attack", "编组进攻", "英雄与部队一起压上");
        AddBattleRuntimeCommandDraftButton(_battleRuntimeCombinedCommandList, selected, "company_retreat", "编组撤退", "英雄与部队一起脱离");
        AddBattleRuntimeCommandDraftButton(_battleRuntimeCombinedCommandList, selected, "company_regroup", "重新集结", "部队向英雄重新靠拢");
    }

    private void AddBattleRuntimeCommandDraftButton(
        Container targetList,
        BattleRuntimeCommandGroupView selected,
        string commandId,
        string label,
        string detail)
    {
        if (targetList == null)
        {
            return;
        }

        Button button = GameUiSceneFactory.CreateWorldSecondaryActionButton(nameof(WorldSiteRoot));
        if (button == null)
        {
            return;
        }

        button.Text = $"{label}\n{detail}";
        button.TooltipText = "本切片只验证暂停、选择和指挥面板，不改写本次战斗结算。";
        button.Pressed += () => SelectBattleRuntimeCommandDraft(selected, commandId, label);
        targetList.AddChild(button);
    }

    private void SelectBattleRuntimeCommandDraft(
        BattleRuntimeCommandGroupView selected,
        string commandId,
        string label)
    {
        string notice = $"{selected?.DisplayName ?? "参战英雄"}：{label} 指令已选中，实时执行接入下一步。";
        SetSiteNoticeText(notice);
        GameLog.Info(
            nameof(WorldSiteRoot),
            $"BattleRuntimeCommandDraftSelected group={selected?.GroupKey ?? ""} command={commandId}");
    }

    private void SetBattleRuntimeCommandPanelVisible(bool visible)
    {
        if (_battleRuntimeCommandPanel != null)
        {
            _battleRuntimeCommandPanel.Visible = visible;
        }

        if (_siteOverviewCard != null)
        {
            _siteOverviewCard.Visible = !visible;
        }

        if (visible)
        {
            SetBattlePreparationContentVisible(false);
            if (_siteFacilityBuildCard != null)
            {
                _siteFacilityBuildCard.Visible = false;
            }

            if (_siteFacilityCard != null)
            {
                _siteFacilityCard.Visible = false;
            }

            if (_siteDefenseCard != null)
            {
                _siteDefenseCard.Visible = false;
            }

            if (_siteActionCard != null)
            {
                _siteActionCard.Visible = false;
            }
        }
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
            HeroName = string.IsNullOrWhiteSpace(heroName) ? "参战英雄" : $"英雄：{heroName}",
            CorpsSummary = corpsSummary,
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

    private void RefreshBattleRuntimeCommandControls(Container targetList, bool runtimeLocked)
    {
        if (targetList == null)
        {
            return;
        }

        AddMutedLine(targetList, BuildBattleRuntimeCommandStatusText(runtimeLocked));
        AddBattleRuntimeCommandButton(targetList, BattleCorpsCommand.Assault, runtimeLocked);
        AddBattleRuntimeCommandButton(targetList, BattleCorpsCommand.FocusFire, runtimeLocked);
        AddBattleRuntimeCommandButton(targetList, BattleCorpsCommand.HoldLine, runtimeLocked);
    }

    private void AddBattleRuntimeCommandButton(
        Container targetList,
        BattleCorpsCommand command,
        bool runtimeLocked)
    {
        Button button = GameUiSceneFactory.CreateWorldSecondaryActionButton(nameof(WorldSiteRoot));
        if (button == null)
        {
            return;
        }

        bool selected = _selectedBattleCorpsCommand == command;
        button.Text = selected
            ? $"已选 {BattleCorpsCommandLabels.ToDisplayText(command)}\n{BuildBattleRuntimeCommandButtonDetail(command)}"
            : $"{BattleCorpsCommandLabels.ToDisplayText(command)}\n{BuildBattleRuntimeCommandButtonDetail(command)}";
        button.Disabled = runtimeLocked;
        button.TooltipText = BuildBattleRuntimeCommandTooltip(command);
        if (!runtimeLocked)
        {
            button.Pressed += () => SubmitBattleRuntimeCommand(command);
        }

        targetList.AddChild(button);
    }

    private void ConfigureBattleRuntimeCommandButton(
        Button button,
        BattleCorpsCommand command,
        bool runtimeLocked)
    {
        if (button == null)
        {
            return;
        }

        bool selected = _selectedBattleCorpsCommand == command;
        button.Text = selected
            ? $"已选 {BattleCorpsCommandLabels.ToDisplayText(command)}"
            : BattleCorpsCommandLabels.ToDisplayText(command);
        button.Disabled = runtimeLocked;
        button.TooltipText = runtimeLocked
            ? "V0 运行时已按战前指令结算，实时改令留到下一步。"
            : BuildBattleRuntimeCommandTooltip(command);
    }

    private static string BuildBattleRuntimeCommandTooltip(BattleCorpsCommand command)
    {
        return command switch
        {
            BattleCorpsCommand.FocusFire => "优先压低血量目标，用来验证集火是否改变接触顺序。",
            BattleCorpsCommand.HoldLine => "不主动追远处目标，等待敌人进入接触距离。",
            _ => "主动接敌并推进，用来验证默认自动战斗压力。"
        };
    }

    private static string BuildBattleRuntimeCommandButtonDetail(BattleCorpsCommand command)
    {
        return command switch
        {
            BattleCorpsCommand.FocusFire => "优先压低血量目标",
            BattleCorpsCommand.HoldLine => "原地接敌，不追远处目标",
            _ => "主动推进并接敌"
        };
    }

    private string BuildBattleRuntimeCommandStatusText(bool runtimeLocked)
    {
        string label = BattleCorpsCommandLabels.ToDisplayText(_selectedBattleCorpsCommand);
        return runtimeLocked
            ? $"战斗指令：{label}（本次 V0 开战后锁定）"
            : $"战斗指令：{label}";
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
