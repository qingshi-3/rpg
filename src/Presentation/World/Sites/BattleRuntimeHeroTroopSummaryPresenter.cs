using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.World.Sites;

internal sealed class BattleRuntimeHeroTroopSummaryPresenter
{
    private readonly HBoxContainer _host;
    private readonly Dictionary<string, BattleRuntimeHeroTroopSummaryRow> _rows = new(StringComparer.Ordinal);

    public BattleRuntimeHeroTroopSummaryPresenter(HBoxContainer host)
    {
        _host = host;
    }

    public void Refresh(IReadOnlyList<BattleRuntimeHeroTroopSummaryView> summaries, string selectedGroupKey)
    {
        if (_host == null)
        {
            return;
        }

        IReadOnlyList<BattleRuntimeHeroTroopSummaryView> liveSummaries = summaries ?? Array.Empty<BattleRuntimeHeroTroopSummaryView>();
        HashSet<string> liveGroupKeys = liveSummaries
            .Where(summary => !string.IsNullOrWhiteSpace(summary?.GroupKey))
            .Select(summary => summary.GroupKey)
            .ToHashSet(StringComparer.Ordinal);
        foreach (string staleGroupKey in _rows.Keys.Where(groupKey => !liveGroupKeys.Contains(groupKey)).ToArray())
        {
            if (_rows.Remove(staleGroupKey, out BattleRuntimeHeroTroopSummaryRow staleRow))
            {
                staleRow?.QueueFree();
            }
        }

        foreach (BattleRuntimeHeroTroopSummaryView summary in liveSummaries)
        {
            if (summary == null || string.IsNullOrWhiteSpace(summary.GroupKey))
            {
                continue;
            }

            if (!_rows.TryGetValue(summary.GroupKey, out BattleRuntimeHeroTroopSummaryRow row) ||
                row == null ||
                !GodotObject.IsInstanceValid(row))
            {
                row = GameUiSceneFactory.CreateBattleRuntimeHeroTroopSummaryRow(nameof(WorldSiteRoot));
                if (row == null)
                {
                    continue;
                }

                _host.AddChild(row);
                _rows[summary.GroupKey] = row;
            }

            bool selected = string.Equals(summary.GroupKey, selectedGroupKey ?? "", StringComparison.Ordinal);
            row.Bind(summary, selected);
        }
    }

    private static void BindFallbackRow(Control row, BattleRuntimeHeroTroopSummaryView summary)
    {
        if (row == null || summary == null)
        {
            return;
        }

        string soldierCountText = summary.SoldierCountText;
        int heroHpCurrent = summary.HeroHpCurrent;
        int troopHpCurrent = summary.TroopHpCurrent;
        Label soldierCountLabel = row.GetNodeOrNull<Label>("SoldierCountText");
        if (soldierCountLabel != null)
        {
            soldierCountLabel.Text = soldierCountText;
        }

        BattleRuntimeCommandHudPresentation.SetProgressBar(
            row.GetNodeOrNull<ProgressBar>("HeroHpBar"),
            heroHpCurrent,
            summary.HeroHpMax);
        BattleRuntimeCommandHudPresentation.SetProgressBar(
            row.GetNodeOrNull<ProgressBar>("TroopHpBar"),
            troopHpCurrent,
            summary.TroopHpMax);
    }
}
