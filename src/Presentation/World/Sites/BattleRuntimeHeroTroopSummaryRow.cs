using Godot;

namespace Rpg.Presentation.World.Sites;

public partial class BattleRuntimeHeroTroopSummaryRow : PanelContainer
{
    private Label _nameLabel;
    private Label _soldierCountLabel;
    private Label _heroHpLabel;
    private Label _troopHpLabel;
    private ProgressBar _heroHpBar;
    private ProgressBar _troopHpBar;
    private BattleRuntimeHeroTroopSummaryView _pendingView;
    private bool _pendingSelected;

    public override void _Ready()
    {
        // Summary rows are live status only; they must not reserve map clicks.
        MouseFilter = MouseFilterEnum.Ignore;
        _nameLabel = GetNodeOrNull<Label>("Margin/Stack/Header/HeroName");
        _soldierCountLabel = GetNodeOrNull<Label>("Margin/Stack/Header/SoldierCountText");
        _heroHpLabel = GetNodeOrNull<Label>("Margin/Stack/HeroHpRow/HeroHpText");
        _troopHpLabel = GetNodeOrNull<Label>("Margin/Stack/TroopHpRow/TroopHpText");
        _heroHpBar = GetNodeOrNull<ProgressBar>("Margin/Stack/HeroHpRow/HeroHpBar");
        _troopHpBar = GetNodeOrNull<ProgressBar>("Margin/Stack/TroopHpRow/TroopHpBar");
        ApplyBinding();
    }

    internal void Bind(BattleRuntimeHeroTroopSummaryView view, bool selected)
    {
        _pendingView = view;
        _pendingSelected = selected;
        ApplyBinding();
    }

    private void ApplyBinding()
    {
        if (_pendingView == null)
        {
            return;
        }

        if (_nameLabel != null)
        {
            _nameLabel.Text = string.IsNullOrWhiteSpace(_pendingView.DisplayName)
                ? _pendingView.GroupKey
                : _pendingView.DisplayName;
        }

        if (_soldierCountLabel != null)
        {
            _soldierCountLabel.Text = _pendingView.SoldierCountText;
        }

        if (_heroHpLabel != null)
        {
            _heroHpLabel.Text = $"英雄 {_pendingView.HeroHpCurrent}/{_pendingView.HeroHpMax}";
        }

        if (_troopHpLabel != null)
        {
            _troopHpLabel.Text = $"兵力 {_pendingView.TroopHpCurrent}/{_pendingView.TroopHpMax}";
        }

        BattleRuntimeCommandHudPresentation.SetProgressBar(
            _heroHpBar,
            _pendingView.HeroHpCurrent,
            _pendingView.HeroHpMax);
        BattleRuntimeCommandHudPresentation.SetProgressBar(
            _troopHpBar,
            _pendingView.TroopHpCurrent,
            _pendingView.TroopHpMax);

        SelfModulate = _pendingSelected
            ? Colors.White
            : new Color(1f, 1f, 1f, 0.76f);
    }
}
