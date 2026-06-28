using Godot;

namespace Rpg.Presentation.World;

public enum WorldSiteNamePresentationMode
{
    Full,
    Compact,
    Hidden
}

public partial class WorldSiteNameBadge : Control
{
    private const float Padding = 4.0f;
    private const float BorderWidth = 2.0f;
    private const int FontSize = 15;

    private string _displayName = "";
    private bool _selected;
    private bool _hovered;
    private WorldSiteNamePresentationMode _presentationMode = WorldSiteNamePresentationMode.Full;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
    }

    public void Bind(string displayName, bool selected, bool hovered, WorldSiteNamePresentationMode presentationMode)
    {
        _displayName = displayName ?? "";
        _selected = selected;
        _hovered = hovered;
        _presentationMode = presentationMode;
        Visible = presentationMode != WorldSiteNamePresentationMode.Hidden;
        QueueRedraw();
    }

    public void SetScreenRect(Rect2 screenRect)
    {
        Position = screenRect.Position;
        Size = screenRect.Size;
        QueueRedraw();
    }

    public void SetPresentationMode(WorldSiteNamePresentationMode presentationMode)
    {
        if (_presentationMode == presentationMode)
        {
            return;
        }

        _presentationMode = presentationMode;
        Visible = presentationMode != WorldSiteNamePresentationMode.Hidden;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!Visible || _presentationMode == WorldSiteNamePresentationMode.Hidden)
        {
            return;
        }

        Rect2 bounds = new(Vector2.Zero, Size);
        Color fill = _hovered
            ? new Color(0.12f, 0.14f, 0.16f, 0.82f)
            : new Color(0.06f, 0.07f, 0.08f, 0.72f);
        Color border = _selected
            ? new Color(1.0f, 0.86f, 0.32f, 0.95f)
            : new Color(0.28f, 0.32f, 0.36f, 0.7f);

        // This badge is a presentation overlay. It should keep a fixed screen size
        // and later can swap modes when zoom crosses a threshold.
        DrawRect(bounds.Grow(Padding), fill, filled: true);
        DrawRect(bounds.Grow(Padding), border, filled: false, width: BorderWidth);
        DrawString(
            ThemeDB.FallbackFont,
            new Vector2(0.0f, Size.Y * 0.76f),
            _displayName,
            HorizontalAlignment.Center,
            Size.X,
            FontSize,
            _selected ? new Color(1.0f, 0.94f, 0.72f, 1.0f) : new Color(0.96f, 0.91f, 0.78f, 0.96f));
    }
}
