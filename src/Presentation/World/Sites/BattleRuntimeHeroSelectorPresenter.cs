using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.World.Sites;

internal sealed class BattleRuntimeHeroSelectorPresenter
{
    private readonly HBoxContainer _host;
    private readonly Action<string> SelectBattleRuntimeCommandGroup;
    private readonly Dictionary<string, BattleRuntimeHeroSwitchButton> _buttons = new(StringComparer.Ordinal);

    internal BattleRuntimeHeroSelectorPresenter(HBoxContainer host, Action<string> selectBattleRuntimeCommandGroup)
    {
        _host = host;
        SelectBattleRuntimeCommandGroup = selectBattleRuntimeCommandGroup;
    }

    internal void Refresh(
        IReadOnlyList<BattleRuntimeCommandGroupView> groups,
        string selectedGroupKey,
        bool hasRuntime,
        Func<BattleRuntimeCommandGroupView, bool> hasReadySkill)
    {
        if (_host == null)
        {
            return;
        }

        IReadOnlyList<BattleRuntimeCommandGroupView> liveGroups = groups ?? Array.Empty<BattleRuntimeCommandGroupView>();
        HashSet<string> liveKeys = liveGroups
            .Where(group => !string.IsNullOrWhiteSpace(group?.GroupKey))
            .Select(group => group.GroupKey)
            .ToHashSet(StringComparer.Ordinal);
        foreach (string staleKey in _buttons.Keys.Where(key => !liveKeys.Contains(key)).ToArray())
        {
            if (_buttons.Remove(staleKey, out BattleRuntimeHeroSwitchButton staleButton))
            {
                staleButton?.QueueFree();
            }
        }

        foreach (BattleRuntimeCommandGroupView group in liveGroups.Where(group => !string.IsNullOrWhiteSpace(group?.GroupKey)))
        {
            if (!_buttons.TryGetValue(group.GroupKey, out BattleRuntimeHeroSwitchButton button) ||
                button == null ||
                !GodotObject.IsInstanceValid(button))
            {
                button = GameUiSceneFactory.CreateBattleRuntimeHeroSwitchButton(nameof(WorldSiteRoot));
                if (button == null)
                {
                    continue;
                }

                button.Selected += OnHeroSelected;
                _host.AddChild(button);
                _buttons[group.GroupKey] = button;
            }

            button.Bind(
                group.GroupKey,
                group.DisplayName,
                string.Equals(group.GroupKey, selectedGroupKey ?? "", StringComparison.Ordinal),
                hasReadySkill?.Invoke(group) == true,
                hasRuntime);
        }

        _host.Visible = liveGroups.Count > 0;
    }

    private void OnHeroSelected(string groupKey)
    {
        SelectBattleRuntimeCommandGroup?.Invoke(groupKey ?? "");
    }
}
