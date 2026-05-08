using Godot;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.Battle.UI;

public partial class BattleTurnQueueItem : Control
{
    private Label _nameLabel;
    private Label _metaLabel;
    private BattleTurnQueueEntry _entry;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _nameLabel = GameUiSceneFactory.GetRequiredNode<Label>(this, "NameLabel", nameof(BattleTurnQueueItem));
        _metaLabel = GameUiSceneFactory.GetRequiredNode<Label>(this, "MetaLabel", nameof(BattleTurnQueueItem));
        ApplyEntry();
    }

    public override void _Draw()
    {
        BattleFaction faction = _entry?.Faction ?? BattleFaction.Player;
        bool active = _entry?.IsActive == true;
        bool defeated = _entry?.IsDefeated == true;

        Color fill = defeated
            ? new Color(0.06f, 0.06f, 0.06f, 0.56f)
            : faction == BattleFaction.Enemy
                ? new Color(0.44f, 0.11f, 0.08f, active ? 0.94f : 0.62f)
                : new Color(0.08f, 0.25f, 0.46f, active ? 0.96f : 0.66f);
        Color border = active
            ? new Color(1f, 0.86f, 0.35f, 0.96f)
            : new Color(0.9f, 0.78f, 0.5f, 0.22f);

        Rect2 rect = new(Vector2.Zero, Size);
        DrawRect(rect, fill, true);
        DrawRect(rect.Grow(-0.5f), border, false, active ? 2.0f : 1.0f);
    }

    public void SetEntry(BattleTurnQueueEntry entry)
    {
        _entry = entry;
        ApplyEntry();
        QueueRedraw();
    }

    private void ApplyEntry()
    {
        if (_entry == null || _nameLabel == null || _metaLabel == null)
        {
            return;
        }

        _nameLabel.Text = _entry.IsActive ? $"▶ {_entry.DisplayName}" : _entry.DisplayName;
        _metaLabel.Text = $"{GetFactionText(_entry.Faction)}  HP {_entry.Hp}/{_entry.MaxHp}  AP {_entry.Ap}/{_entry.MaxAp}";

        Color textColor = _entry.IsDefeated
            ? new Color(1f, 1f, 1f, 0.45f)
            : Colors.White;
        _nameLabel.Modulate = textColor;
        _metaLabel.Modulate = textColor;
    }

    private static string GetFactionText(BattleFaction faction)
    {
        return faction == BattleFaction.Enemy ? "敌方" : "我方";
    }
}
