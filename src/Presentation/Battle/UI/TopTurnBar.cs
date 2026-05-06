using Godot;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.Battle.UI;

public partial class TopTurnBar : PanelContainer
{
    private Label _label;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _label = GameUiSceneFactory.GetRequiredNode<Label>(
            this,
            "Margin/Label",
            nameof(TopTurnBar));
    }
}
