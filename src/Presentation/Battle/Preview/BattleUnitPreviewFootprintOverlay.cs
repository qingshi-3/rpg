using Godot;

namespace Rpg.Presentation.Battle.Preview;

[Tool]
public partial class BattleUnitPreviewFootprintOverlay : Node2D
{
    [Export(PropertyHint.Range, "1,3,1")]
    public int FootprintWidth { get; set; } = 1;

    [Export(PropertyHint.Range, "1,3,1")]
    public int FootprintHeight { get; set; } = 1;

    [Export(PropertyHint.Range, "16,160,1")]
    public float CellSizePixels { get; set; } = 48f;

    [Export]
    public Color FillColor { get; set; } = new(0.28f, 0.68f, 1.0f, 0.16f);

    [Export]
    public Color BorderColor { get; set; } = new(0.45f, 0.86f, 1.0f, 0.9f);

    [Export]
    public Color AnchorColor { get; set; } = new(1.0f, 0.9f, 0.28f, 0.95f);

    public void Configure(int footprintWidth, int footprintHeight, float cellSizePixels, bool visible)
    {
        FootprintWidth = System.Math.Clamp(footprintWidth, 1, 3);
        FootprintHeight = System.Math.Clamp(footprintHeight, 1, 3);
        CellSizePixels = System.Math.Max(16f, cellSizePixels);
        Visible = visible;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!Visible)
        {
            return;
        }

        float cellSize = System.Math.Max(16f, CellSizePixels);
        int width = System.Math.Clamp(FootprintWidth, 1, 3);
        int height = System.Math.Clamp(FootprintHeight, 1, 3);
        Vector2 origin = new(-width * cellSize * 0.5f, -height * cellSize * 0.5f);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Rect2 rect = new(origin + new Vector2(x * cellSize, y * cellSize), new Vector2(cellSize, cellSize));
                DrawRect(rect, FillColor, filled: true);
                DrawRect(rect, BorderColor, filled: false, width: 1.4f);
            }
        }

        DrawCircle(origin, 4.5f, AnchorColor);
        DrawLine(origin + new Vector2(-8f, 0f), origin + new Vector2(8f, 0f), AnchorColor, 1.5f);
        DrawLine(origin + new Vector2(0f, -8f), origin + new Vector2(0f, 8f), AnchorColor, 1.5f);
    }
}
