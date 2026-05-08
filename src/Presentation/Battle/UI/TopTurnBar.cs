using System.Collections.Generic;
using Godot;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.Battle.UI;

public partial class TopTurnBar : PanelContainer
{
    private readonly List<BattleTurnQueueItem> _queueItems = new();
    private Label _statusLabel;
    private HBoxContainer _queueRow;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _statusLabel = GameUiSceneFactory.GetRequiredNode<Label>(
            this,
            "Margin/Content/StatusLabel",
            nameof(TopTurnBar));
        _queueRow = GameUiSceneFactory.GetRequiredNode<HBoxContainer>(
            this,
            "Margin/Content/QueueScroll/QueueRow",
            nameof(TopTurnBar));
        SetTurnState(1, BattleFaction.Player, null);
    }

    public void SetTurnState(int roundNumber, BattleFaction activeFaction, IReadOnlyList<BattleTurnQueueEntry> entries)
    {
        if (_statusLabel != null)
        {
            _statusLabel.Text = $"第 {roundNumber} 回合 · {GetPhaseText(activeFaction)}";
            _statusLabel.Modulate = activeFaction == BattleFaction.Enemy
                ? new Color(1f, 0.62f, 0.54f, 1f)
                : new Color(1f, 0.88f, 0.48f, 1f);
        }

        if (_queueRow == null)
        {
            return;
        }

        int entryCount = entries?.Count ?? 0;
        for (int index = 0; index < entryCount; index++)
        {
            BattleTurnQueueItem item = GetOrCreateQueueItem(index);
            if (item == null)
            {
                return;
            }

            item.SetEntry(entries[index]);
            item.Visible = true;
        }

        for (int index = entryCount; index < _queueItems.Count; index++)
        {
            _queueItems[index].Visible = false;
        }
    }

    private BattleTurnQueueItem GetOrCreateQueueItem(int index)
    {
        while (_queueItems.Count <= index)
        {
            BattleTurnQueueItem item = GameUiSceneFactory.CreateBattleTurnQueueItem(nameof(TopTurnBar));
            if (item == null)
            {
                return null;
            }

            _queueItems.Add(item);
            _queueRow.AddChild(item);
        }

        return _queueItems[index];
    }

    private static string GetPhaseText(BattleFaction activeFaction)
    {
        return activeFaction == BattleFaction.Enemy ? "敌方行动" : "我方行动";
    }
}
