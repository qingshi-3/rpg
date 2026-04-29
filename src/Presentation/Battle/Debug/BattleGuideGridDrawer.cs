using Godot;

namespace Rpg.Presentation.Battle.Debug;

public partial class BattleGuideGridDrawer : Node2D
{
    private const float ViewPaddingPixels = 64f;

    public int GridSpacingPixels { get; set; } = 16;
    public Color GridLineColor { get; set; } = new(0.35f, 0.78f, 1f, 0.24f);
    public float GridLineWidth { get; set; } = 1f;

    public override void _Ready()
    {
        ZIndex = 1000;
        SetProcess(Visible);
    }

    public override void _Process(double delta)
    {
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!Visible)
        {
            return;
        }

        Rect2 visibleWorldRect = GetVisibleWorldRect();
        float spacing = Mathf.Max(1, GridSpacingPixels);
        float left = Mathf.Floor((visibleWorldRect.Position.X - ViewPaddingPixels) / spacing) * spacing;
        float top = Mathf.Floor((visibleWorldRect.Position.Y - ViewPaddingPixels) / spacing) * spacing;
        float right = visibleWorldRect.End.X + ViewPaddingPixels;
        float bottom = visibleWorldRect.End.Y + ViewPaddingPixels;

        for (float x = left; x <= right; x += spacing)
        {
            DrawLine(new Vector2(x, top), new Vector2(x, bottom), GridLineColor, GridLineWidth);
        }

        for (float y = top; y <= bottom; y += spacing)
        {
            DrawLine(new Vector2(left, y), new Vector2(right, y), GridLineColor, GridLineWidth);
        }
    }

    private Rect2 GetVisibleWorldRect()
    {
        Camera2D camera = GetViewport().GetCamera2D();
        Vector2 viewportSize = GetViewportRect().Size;

        if (camera == null)
        {
            return new Rect2(Vector2.Zero, viewportSize);
        }

        Vector2 zoom = camera.Zoom;
        Vector2 worldSize = new(
            viewportSize.X / Mathf.Max(zoom.X, 0.001f),
            viewportSize.Y / Mathf.Max(zoom.Y, 0.001f));
        Vector2 center = camera.GetScreenCenterPosition();

        return new Rect2(center - worldSize * 0.5f, worldSize);
    }
}
