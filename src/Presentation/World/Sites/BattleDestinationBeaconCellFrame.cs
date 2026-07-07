using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Rpg.Presentation.World.Sites;

public partial class BattleDestinationBeaconCellFrame : Node2D
{
    [Export]
    public Color BorderColor { get; set; } = new(1f, 0.86f, 0.22f, 0.96f);

    [Export]
    public Color GlowColor { get; set; } = new(0.1f, 0.82f, 1f, 0.18f);

    [Export(PropertyHint.Range, "1,8,0.25")]
    public float BorderWidth { get; set; } = 2.25f;

    [Export(PropertyHint.Range, "2,18,0.5")]
    public float GlowWidth { get; set; } = 6f;

    [Export(PropertyHint.Range, "0.05,0.45,0.01")]
    public float CornerLengthRatio { get; set; } = 0.26f;

    [Export]
    public Vector2 CellStepX { get; set; } = new(32f, 16f);

    [Export]
    public Vector2 CellStepY { get; set; } = new(-32f, 16f);

    private Vector2[] _cellPolygon;

    public void SetCellPolygon(IEnumerable<Vector2> localPolygon)
    {
        Vector2[] nextPolygon = localPolygon?.ToArray();
        _cellPolygon = nextPolygon?.Length >= 4 ? nextPolygon : null;
        QueueRedraw();
    }

    public override void _Draw()
    {
        Vector2[] polygon = ResolveCellPolygon();
        if (polygon.Length < 4)
        {
            return;
        }

        float cornerLengthRatio = Mathf.Clamp(CornerLengthRatio, 0.05f, 0.45f);
        for (int index = 0; index < polygon.Length; index++)
        {
            Vector2 corner = polygon[index];
            Vector2 previous = polygon[(index - 1 + polygon.Length) % polygon.Length];
            Vector2 next = polygon[(index + 1) % polygon.Length];

            DrawCornerSegment(corner, corner.Lerp(previous, cornerLengthRatio));
            DrawCornerSegment(corner, corner.Lerp(next, cornerLengthRatio));
        }
    }

    private void DrawCornerSegment(Vector2 start, Vector2 end)
    {
        if (GlowColor.A > 0f && GlowWidth > 0f)
        {
            DrawLine(start, end, GlowColor, GlowWidth, true);
        }

        DrawLine(start, end, BorderColor, BorderWidth, true);
    }

    private Vector2[] ResolveCellPolygon()
    {
        if (_cellPolygon is { Length: >= 4 })
        {
            return _cellPolygon;
        }

        // The authored scene needs an editor-visible default; runtime marker binding replaces this with map geometry.
        return new[]
        {
            -(CellStepX + CellStepY) * 0.5f,
            (CellStepX - CellStepY) * 0.5f,
            (CellStepX + CellStepY) * 0.5f,
            (-CellStepX + CellStepY) * 0.5f
        };
    }
}
