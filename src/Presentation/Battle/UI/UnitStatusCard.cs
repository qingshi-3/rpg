using Godot;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.Battle.UI;

public partial class UnitStatusCard : PanelContainer
{
    private Label _portrait;
    private Label _name;
    private Label _hp;
    private ProgressBar _hpBar;
    private Label _ap;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;

        _portrait = GameUiSceneFactory.GetRequiredNode<Label>(
            this,
            "Margin/Root/PortraitPanel/Portrait",
            nameof(UnitStatusCard));
        _name = GameUiSceneFactory.GetRequiredNode<Label>(
            this,
            "Margin/Root/Info/NameLabel",
            nameof(UnitStatusCard));
        _hp = GameUiSceneFactory.GetRequiredNode<Label>(
            this,
            "Margin/Root/Info/HpRow/HpLabel",
            nameof(UnitStatusCard));
        _hpBar = GameUiSceneFactory.GetRequiredNode<ProgressBar>(
            this,
            "Margin/Root/Info/HpRow/HpBar",
            nameof(UnitStatusCard));
        _ap = GameUiSceneFactory.GetRequiredNode<Label>(
            this,
            "Margin/Root/Info/ApLabel",
            nameof(UnitStatusCard));

        SetUnit("骑士", 24, 24, 3, 3);
    }

    public void SetUnit(string unitName, int hp, int maxHp, int ap, int maxAp)
    {
        if (_name == null || _hp == null || _ap == null)
        {
            return;
        }

        string displayName = string.IsNullOrWhiteSpace(unitName) ? "未选择" : unitName;
        _name.Text = displayName;
        _hp.Text = $"生命 {hp}/{maxHp}";
        _ap.Text = $"行动点 {BuildApPips(ap, maxAp)}";

        if (_portrait != null)
        {
            _portrait.Text = displayName == "未选择" ? "-" : displayName[..1];
        }

        if (_hpBar != null)
        {
            _hpBar.MaxValue = Mathf.Max(maxHp, 1);
            _hpBar.Value = Mathf.Clamp(hp, 0, Mathf.Max(maxHp, 1));
        }
    }

    private static string BuildApPips(int ap, int maxAp)
    {
        if (maxAp <= 0)
        {
            return "-";
        }

        string pips = "";
        for (int index = 0; index < maxAp; index++)
        {
            pips += index < ap ? "●" : "○";
        }

        return pips;
    }
}
