using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle.Snapshots;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.World.Sites;

internal sealed class BattleRuntimeHeroFramePresenter
{
    private readonly Control _heroFrame;
    private readonly Label _heroNameLabel;
    private readonly Label _heroStateLabel;
    private readonly ProgressBar _heroHealthBar;
    private readonly ProgressBar _heroManaBar;
    private readonly BattleRuntimeHeroSelectorPresenter _heroSelectorPresenter;
    private readonly HBoxContainer _heroSkillList;
    private readonly Button _regroupButton;
    private readonly Action<string> _skillSlotPressed;
    private readonly Dictionary<string, BattleRuntimeSkillSlot> _skillSlots = new(StringComparer.Ordinal);

    public BattleRuntimeHeroFramePresenter(
        Control heroFrame,
        Label heroNameLabel,
        Label heroStateLabel,
        ProgressBar heroHealthBar,
        ProgressBar heroManaBar,
        BattleRuntimeHeroSelectorPresenter heroSelectorPresenter,
        HBoxContainer heroSkillList,
        Button regroupButton,
        Action<string> skillSlotPressed)
    {
        _heroFrame = heroFrame;
        _heroNameLabel = heroNameLabel;
        _heroStateLabel = heroStateLabel;
        _heroHealthBar = heroHealthBar;
        _heroManaBar = heroManaBar;
        _heroSelectorPresenter = heroSelectorPresenter;
        _heroSkillList = heroSkillList;
        _regroupButton = regroupButton;
        _skillSlotPressed = skillSlotPressed;
    }

    public void Refresh(
        BattleRuntimeCommandGroupView selected,
        IReadOnlyList<BattleRuntimeCommandGroupView> playerGroups,
        bool hasRuntime,
        bool commandPauseActive,
        bool hasReadySkill,
        BattleEntity heroEntity,
        IReadOnlyList<BattleSkillSnapshot> skills,
        Func<BattleRuntimeCommandGroupView, bool> hasReadySkillForGroup,
        Func<string, WorldSiteRoot.BattleRuntimeSkillUsageState> resolveSkillUsageState)
    {
        if (_heroFrame != null)
        {
            _heroFrame.Visible = true;
        }

        if (_heroNameLabel != null)
        {
            _heroNameLabel.Text = selected?.DisplayName ?? "参战英雄";
        }

        if (_heroStateLabel != null)
        {
            _heroStateLabel.Text = BuildHeroStateText(selected, hasRuntime, commandPauseActive, hasReadySkill);
        }

        HealthComponent health = heroEntity?.GetComponent<HealthComponent>();
        BattleRuntimeCommandHudPresentation.SetProgressBar(_heroHealthBar, health?.Hp ?? 1, health?.MaxHp ?? 1);
        // V0 has no Runtime-owned mana yet. Keep the authored mana bar visible as
        // future resource space without inventing a second gameplay resource.
        BattleRuntimeCommandHudPresentation.SetProgressBar(_heroManaBar, 1, 1);

        _heroSelectorPresenter?.Refresh(playerGroups, selected?.GroupKey ?? "", hasRuntime, hasReadySkillForGroup);
        RefreshSkillList(selected, skills, hasRuntime, resolveSkillUsageState);

        if (_regroupButton != null)
        {
            _regroupButton.Disabled = selected == null || !hasRuntime;
            _regroupButton.TooltipText = "重整：先作为战斗中指挥入口，后续接入完整重整命令。";
        }
    }

    private void RefreshSkillList(
        BattleRuntimeCommandGroupView selected,
        IReadOnlyList<BattleSkillSnapshot> skills,
        bool hasRuntime,
        Func<string, WorldSiteRoot.BattleRuntimeSkillUsageState> resolveSkillUsageState)
    {
        if (_heroSkillList == null)
        {
            return;
        }

        HashSet<string> liveSkillIds = (skills ?? Array.Empty<BattleSkillSnapshot>())
            .Where(skill => !string.IsNullOrWhiteSpace(skill?.SkillId))
            .Select(skill => skill.SkillId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (string staleSkillId in _skillSlots.Keys.Where(skillId => !liveSkillIds.Contains(skillId)).ToArray())
        {
            if (_skillSlots.Remove(staleSkillId, out BattleRuntimeSkillSlot staleSlot))
            {
                staleSlot?.QueueFree();
            }
        }

        foreach (BattleSkillSnapshot skill in skills ?? Array.Empty<BattleSkillSnapshot>())
        {
            if (skill == null || string.IsNullOrWhiteSpace(skill.SkillId))
            {
                continue;
            }

            if (!_skillSlots.TryGetValue(skill.SkillId, out BattleRuntimeSkillSlot slot) ||
                slot == null ||
                !GodotObject.IsInstanceValid(slot))
            {
                slot = GameUiSceneFactory.CreateBattleRuntimeSkillSlot(nameof(WorldSiteRoot));
                if (slot == null)
                {
                    continue;
                }

                slot.Pressed += OnSkillSlotPressed;
                _heroSkillList.AddChild(slot);
                _skillSlots[skill.SkillId] = slot;
            }

            WorldSiteRoot.BattleRuntimeSkillUsageState usageState = resolveSkillUsageState?.Invoke(skill.SkillId)
                ?? WorldSiteRoot.BattleRuntimeSkillUsageState.Unavailable;
            bool available = selected != null && hasRuntime && usageState == WorldSiteRoot.BattleRuntimeSkillUsageState.Ready;
            slot.Bind(
                skill.SkillId,
                skill.DisplayName,
                available,
                BattleRuntimeSkillHudText.BuildStatusText(selected, hasRuntime, usageState),
                cooldownRemainingSeconds: 0.0);
        }
    }

    private void OnSkillSlotPressed(string skillId) => _skillSlotPressed?.Invoke(skillId);

    private static string BuildHeroStateText(
        BattleRuntimeCommandGroupView selected,
        bool hasRuntime,
        bool commandPauseActive,
        bool skillReady)
    {
        if (selected == null)
        {
            return "未选择";
        }

        string pauseText = commandPauseActive ? "暂停指挥" : "战斗中";
        string skillText = skillReady ? "技能可用" : "技能锁定";
        return $"{pauseText} / {skillText}";
    }
}
