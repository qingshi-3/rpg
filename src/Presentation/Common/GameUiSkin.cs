using Godot;
using Rpg.Infrastructure.Logging;

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
    private const string CursorAssetRoot = "res://assets/textures/ui/cursors/";

    private static readonly Vector2 HandCursorHotspot = new(3.0f, 2.0f);

    public static void ApplyGameCursorTheme()
    {
        Texture2D hand = LoadCursorTexture("cursor_hand.png");

        // Cursor art is Presentation-owned. We intentionally map all common native
        // shapes to one compact hand so transient disabled states do not flash icons.
        ApplyCursorTexture(hand, Input.CursorShape.Arrow, HandCursorHotspot);
        ApplyCursorTexture(hand, Input.CursorShape.Ibeam, HandCursorHotspot);
        ApplyCursorTexture(hand, Input.CursorShape.PointingHand, HandCursorHotspot);
        ApplyCursorTexture(hand, Input.CursorShape.Cross, HandCursorHotspot);
        ApplyCursorTexture(hand, Input.CursorShape.Wait, HandCursorHotspot);
        ApplyCursorTexture(hand, Input.CursorShape.Busy, HandCursorHotspot);
        ApplyCursorTexture(hand, Input.CursorShape.Drag, HandCursorHotspot);
        ApplyCursorTexture(hand, Input.CursorShape.CanDrop, HandCursorHotspot);
        ApplyCursorTexture(hand, Input.CursorShape.Forbidden, HandCursorHotspot);
        ApplyCursorTexture(hand, Input.CursorShape.Vsize, HandCursorHotspot);
        ApplyCursorTexture(hand, Input.CursorShape.Hsize, HandCursorHotspot);
        ApplyCursorTexture(hand, Input.CursorShape.Bdiagsize, HandCursorHotspot);
        ApplyCursorTexture(hand, Input.CursorShape.Fdiagsize, HandCursorHotspot);
        ApplyCursorTexture(hand, Input.CursorShape.Move, HandCursorHotspot);
        ApplyCursorTexture(hand, Input.CursorShape.Vsplit, HandCursorHotspot);
        ApplyCursorTexture(hand, Input.CursorShape.Hsplit, HandCursorHotspot);
        ApplyCursorTexture(hand, Input.CursorShape.Help, HandCursorHotspot);
    }

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

    private static Texture2D LoadCursorTexture(string fileName)
    {
        string path = CursorAssetRoot + fileName;
        Texture2D texture = GD.Load<Texture2D>(path);
        if (texture != null)
        {
            return texture;
        }

        Image image = Image.LoadFromFile(path);
        if (image == null || image.IsEmpty())
        {
            GameLog.Warn(nameof(GameUiSkin), $"ProjectCursorAssetMissing path={path}");
            return null;
        }

        return ImageTexture.CreateFromImage(image);
    }

    private static void ApplyCursorTexture(Texture2D texture, Input.CursorShape shape, Vector2 hotspot)
    {
        if (texture == null)
        {
            return;
        }

        Input.SetCustomMouseCursor(texture, shape, hotspot);
    }
}
