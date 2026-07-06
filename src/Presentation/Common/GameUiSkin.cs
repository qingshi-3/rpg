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
    private const double CursorClickFramesPerSecond = 12.0;
    private const int StaticCursorFrameIndex = -1;

    private static readonly Vector2 CursorHotspot = new(2.0f, 2.0f);
    private static readonly StyleBoxEmpty HiddenFocusStyle = new();
    private static readonly Input.CursorShape[] CommonCursorShapes =
    {
        Input.CursorShape.Arrow,
        Input.CursorShape.Ibeam,
        Input.CursorShape.PointingHand,
        Input.CursorShape.Cross,
        Input.CursorShape.Wait,
        Input.CursorShape.Busy,
        Input.CursorShape.Drag,
        Input.CursorShape.CanDrop,
        Input.CursorShape.Forbidden,
        Input.CursorShape.Vsize,
        Input.CursorShape.Hsize,
        Input.CursorShape.Bdiagsize,
        Input.CursorShape.Fdiagsize,
        Input.CursorShape.Move,
        Input.CursorShape.Vsplit,
        Input.CursorShape.Hsplit,
        Input.CursorShape.Help
    };

    private static Texture2D _cursorHand;
    private static Texture2D[] _cursorClickFrames = System.Array.Empty<Texture2D>();
    private static GameCursorAnimationTimeline _cursorClickTimeline;
    private static int _appliedCursorFrameIndex = int.MinValue;

    public static void ApplyGameCursorTheme()
    {
        _cursorHand = LoadCursorTexture("cursor_hand.png");
        Texture2D clickFrame1 = LoadCursorTexture("cursor_hand_click_1.png");
        Texture2D clickFrame2 = LoadCursorTexture("cursor_hand_click_2.png");
        _cursorClickFrames = clickFrame1 != null && clickFrame2 != null
            ? new[] { clickFrame1, clickFrame2 }
            : System.Array.Empty<Texture2D>();
        _cursorClickTimeline = _cursorClickFrames.Length > 0
            ? new GameCursorAnimationTimeline(_cursorClickFrames.Length, CursorClickFramesPerSecond)
            : null;

        ApplyCursorFrame(StaticCursorFrameIndex);
    }

    public static void HandleCursorInput(InputEvent @event)
    {
        if (_cursorClickTimeline == null ||
            _cursorClickFrames.Length == 0 ||
            @event is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
        {
            return;
        }

        double nowSeconds = GetNowSeconds();
        _cursorClickTimeline.Start(nowSeconds);
        ApplyCursorFrame(_cursorClickTimeline.ResolveFrameIndex(nowSeconds));
    }

    public static void UpdateCursorAnimation()
    {
        if (_cursorClickTimeline == null)
        {
            return;
        }

        int frameIndex = _cursorClickTimeline.ResolveFrameIndex(GetNowSeconds());
        if (frameIndex == _appliedCursorFrameIndex)
        {
            return;
        }

        ApplyCursorFrame(frameIndex);
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
        ApplyHiddenFocusStyle(button);
    }

    public static void ApplyProjectFocusStyle(Node root)
    {
        if (root == null)
        {
            return;
        }

        if (root is Button button)
        {
            ApplyHiddenFocusStyle(button);
        }

        foreach (Node child in root.GetChildren())
        {
            ApplyProjectFocusStyle(child);
        }
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
        // Cursor art is imported as Texture2D so runtime cursor swaps do not
        // create temporary Image/ImageTexture wrappers during scene transitions.
        Texture2D texture = GD.Load<Texture2D>(path);
        if (texture != null)
        {
            return texture;
        }

        GameLog.Warn(nameof(GameUiSkin), $"ProjectCursorAssetMissing path={path}");
        return null;
    }

    private static void ApplyCursorFrame(int frameIndex)
    {
        Texture2D texture = ResolveCursorFrameTexture(frameIndex);
        if (texture == null)
        {
            return;
        }

        // Godot hardware cursors cannot consume SpriteFrames directly. Presentation
        // advances click art by swapping the native cursor texture on frame changes.
        foreach (Input.CursorShape shape in CommonCursorShapes)
        {
            ApplyCursorTexture(texture, shape, CursorHotspot);
        }

        _appliedCursorFrameIndex = frameIndex;
    }

    private static Texture2D ResolveCursorFrameTexture(int frameIndex)
    {
        if (frameIndex >= 0 && frameIndex < _cursorClickFrames.Length)
        {
            return _cursorClickFrames[frameIndex];
        }

        return _cursorHand;
    }

    private static void ApplyCursorTexture(Texture2D texture, Input.CursorShape shape, Vector2 hotspot)
    {
        if (texture == null)
        {
            return;
        }

        Input.SetCustomMouseCursor(texture, shape, hotspot);
    }

    private static void ApplyHiddenFocusStyle(Button button)
    {
        // Focus remains enabled for keyboard/controller navigation; only the
        // Godot overlay style is hidden so themed button states stay readable.
        button.AddThemeStyleboxOverride("focus", HiddenFocusStyle);
    }

    private static double GetNowSeconds()
    {
        return Godot.Time.GetTicksMsec() / 1000.0;
    }
}
