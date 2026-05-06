using Godot;

namespace Rpg.Presentation.Common;

public enum GameUiButtonKind
{
    Primary,
    Secondary,
    Compact
}

public enum GameUiPanelKind
{
    TopBar,
    FramedPanel,
    Dialog
}

public static class GameUiSkin
{
    public static void ApplyPanel(PanelContainer panel, GameUiPanelKind kind)
    {
    }

    public static void ApplyDialog(AcceptDialog dialog)
    {
        if (dialog == null)
        {
            return;
        }

        ApplyButton(dialog.GetOkButton(), GameUiButtonKind.Primary);
    }

    public static void ApplyButton(Button button, GameUiButtonKind kind = GameUiButtonKind.Secondary)
    {
        if (button == null)
        {
            return;
        }

        button.FocusMode = Control.FocusModeEnum.All;
        button.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
    }

    public static void ApplySectionTitle(Label label)
    {
    }

    public static void ApplyBodyLabel(Label label)
    {
    }

    public static void ApplyMutedLabel(Label label)
    {
    }
}
