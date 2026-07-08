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
        Action<string> skillSlotPressed)
    {
        _heroFrame = heroFrame;
        _heroNameLabel = heroNameLabel;
        _heroStateLabel = heroStateLabel;
        _heroHealthBar = heroHealthBar;
        _heroManaBar = heroManaBar;
        _heroSelectorPresenter = heroSelectorPresenter;
        _heroSkillList = heroSkillList;
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

        HashSet<string> liveSkillDefinitionIds = (skills ?? Array.Empty<BattleSkillSnapshot>())
            .Where(skill => !string.IsNullOrWhiteSpace(ResolveSkillDefinitionId(skill)))
            .Select(ResolveSkillDefinitionId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (string staleSkillDefinitionId in _skillSlots.Keys.Where(skillDefinitionId => !liveSkillDefinitionIds.Contains(skillDefinitionId)).ToArray())
        {
            if (_skillSlots.Remove(staleSkillDefinitionId, out BattleRuntimeSkillSlot staleSlot))
            {
                staleSlot?.QueueFree();
            }
        }

        foreach (BattleSkillSnapshot skill in skills ?? Array.Empty<BattleSkillSnapshot>())
        {
            string skillDefinitionId = ResolveSkillDefinitionId(skill);
            if (skill == null || string.IsNullOrWhiteSpace(skillDefinitionId))
            {
                continue;
            }

            if (!_skillSlots.TryGetValue(skillDefinitionId, out BattleRuntimeSkillSlot slot) ||
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
                _skillSlots[skillDefinitionId] = slot;
            }

            WorldSiteRoot.BattleRuntimeSkillUsageState usageState = resolveSkillUsageState?.Invoke(skillDefinitionId)
                ?? WorldSiteRoot.BattleRuntimeSkillUsageState.Unavailable;
            bool available = selected != null && hasRuntime && usageState == WorldSiteRoot.BattleRuntimeSkillUsageState.Ready;
            slot.Bind(
                skillDefinitionId,
                skill.DisplayName,
                skill.IconText,
                skill.IconPath,
                available,
                BattleRuntimeSkillHudText.BuildStatusText(selected, hasRuntime, usageState),
                cooldownRemainingSeconds: 0.0);
        }
    }

    private void OnSkillSlotPressed(string skillDefinitionId) => _skillSlotPressed?.Invoke(skillDefinitionId);

    private static string ResolveSkillDefinitionId(BattleSkillSnapshot skill)
    {
        return skill?.SkillDefinitionId?.Trim() ?? "";
    }

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
