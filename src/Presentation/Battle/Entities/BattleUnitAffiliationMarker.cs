using Godot;

namespace Rpg.Presentation.Battle.Entities;

public partial class BattleUnitAffiliationMarker : Node2D
{
    [Export]
    public BattleFaction Faction { get; set; } = BattleFaction.Neutral;

    [Export]
    public bool HideNeutralMarker { get; set; } = false;

    [Export]
    public Color PlayerColor { get; set; } = new(0.22f, 0.72f, 1f, 0.9f);

    [Export]
    public Color EnemyColor { get; set; } = new(1f, 0.24f, 0.16f, 0.92f);

    [Export]
    public Color NeutralColor { get; set; } = new(0.95f, 0.86f, 0.52f, 0.8f);

    [Export(PropertyHint.Range, "4,24,0.5")]
    public float RadiusX { get; set; } = 9.5f;

    [Export(PropertyHint.Range, "1,12,0.5")]
    public float RadiusY { get; set; } = 3.2f;

    [Export(PropertyHint.Range, "0.5,5,0.1")]
    public float Width { get; set; } = 2.2f;

    [Export(PropertyHint.Range, "4,48,1")]
    public int SegmentCount { get; set; } = 16;

    [Export]
    public Vector2 Offset { get; set; } = new(0f, 3f);

    [Export(PropertyHint.Range, "0,360,1")]
    public float StartDegrees { get; set; } = 24f;

    [Export(PropertyHint.Range, "0,360,1")]
    public float EndDegrees { get; set; } = 156f;

    [Export]
    public bool DrawInnerShadow { get; set; } = true;

    public override void _Ready()
    {
        UpdateVisibility();
    }

    public override void _Draw()
    {
        if (!Visible)
        {
            return;
        }

        Vector2[] points = BuildArcPoints();
        if (points.Length < 2)
        {
            return;
        }

        if (DrawInnerShadow)
        {
            DrawPolyline(points, new Color(0f, 0f, 0f, 0.35f), Width + 1.6f, true);
        }

        DrawPolyline(points, ResolveColor(), Width, true);
    }

    public void SetFaction(BattleFaction faction)
    {
        if (Faction == faction)
        {
            return;
        }

        Faction = faction;
        UpdateVisibility();
        QueueRedraw();
    }

    private Vector2[] BuildArcPoints()
    {
        int segmentCount = Mathf.Max(2, SegmentCount);
        float start = Mathf.DegToRad(StartDegrees);
        float end = Mathf.DegToRad(EndDegrees);
        if (Mathf.IsEqualApprox(start, end))
        {
            return System.Array.Empty<Vector2>();
        }

        var points = new Vector2[segmentCount + 1];
        for (int index = 0; index <= segmentCount; index++)
        {
            float t = index / (float)segmentCount;
            float angle = Mathf.Lerp(start, end, t);
            points[index] = Offset + new Vector2(Mathf.Cos(angle) * RadiusX, Mathf.Sin(angle) * RadiusY);
        }

        return points;
    }

    private Color ResolveColor()
    {
        return Faction switch
        {
            BattleFaction.Player => PlayerColor,
            BattleFaction.Enemy => EnemyColor,
            _ => NeutralColor
        };
    }

    private void UpdateVisibility()
    {
        Visible = !(HideNeutralMarker && Faction == BattleFaction.Neutral);
    }
}
