using Godot;

namespace Rpg.Presentation.Battle.Entities;

public partial class BattleUnitSelectionSpotlight : Node2D
{
    [Export]
    public Color CoreColor { get; set; } = new(1f, 0.96f, 0.62f, 0.22f);

    [Export]
    public Color OuterColor { get; set; } = new(1f, 0.76f, 0.18f, 0.07f);

    [Export]
    public Vector2 CenterOffset { get; set; } = new(0f, -11f);

    [Export(PropertyHint.Range, "8,64,0.5")]
    public float RadiusX { get; set; } = 30f;

    [Export(PropertyHint.Range, "8,64,0.5")]
    public float RadiusY { get; set; } = 25f;

    [Export(PropertyHint.Range, "4,14,1")]
    public int LayerCount { get; set; } = 8;

    [Export(PropertyHint.Range, "16,72,1")]
    public int SegmentCount { get; set; } = 48;

    [Export]
    public bool PulseEnabled { get; set; } = true;

    [Export(PropertyHint.Range, "0.4,3,0.05")]
    public float PulseSeconds { get; set; } = 1.25f;

    [Export(PropertyHint.Range, "0.1,1,0.05")]
    public float PulseAlphaMin { get; set; } = 0.7f;

    [Export(PropertyHint.Range, "0.5,1.5,0.05")]
    public float PulseAlphaMax { get; set; } = 1.08f;

    [Export(PropertyHint.Range, "0,0.25,0.01")]
    public float PulseScaleAmplitude { get; set; } = 0.07f;

    private bool _selected;
    private float _phase;

    public override void _Ready()
    {
        SetSelected(false);
    }

    public override void _Process(double delta)
    {
        if (!_selected || !PulseEnabled)
        {
            return;
        }

        float safeSeconds = Mathf.Max(0.4f, PulseSeconds);
        _phase = Mathf.PosMod(_phase + (float)delta * Mathf.Tau / safeSeconds, Mathf.Tau);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!_selected)
        {
            return;
        }

        int layerCount = Mathf.Max(4, LayerCount);
        float pulseAlpha = ResolvePulseAlpha();
        float pulseScale = ResolvePulseScale();

        for (int index = layerCount; index >= 1; index--)
        {
            float t = index / (float)layerCount;
            float eased = t * t;
            Color color = OuterColor.Lerp(CoreColor, 1f - eased);
            color.A *= pulseAlpha * (1f - t * 0.58f);

            DrawColoredPolygon(
                BuildEllipsePoints(RadiusX * t * pulseScale, RadiusY * t * pulseScale),
                color);
        }
    }

    public void SetSelected(bool selected)
    {
        if (_selected == selected)
        {
            return;
        }

        _selected = selected;
        Visible = selected;
        SetProcess(selected && PulseEnabled);
        QueueRedraw();
    }

    private Vector2[] BuildEllipsePoints(float radiusX, float radiusY)
    {
        int segmentCount = Mathf.Max(16, SegmentCount);
        var points = new Vector2[segmentCount];
        for (int index = 0; index < segmentCount; index++)
        {
            float angle = Mathf.Tau * index / segmentCount;
            points[index] = CenterOffset + new Vector2(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY);
        }

        return points;
    }

    private float ResolvePulseAlpha()
    {
        if (!PulseEnabled)
        {
            return 1f;
        }

        float alphaMin = Mathf.Clamp(PulseAlphaMin, 0.1f, 1f);
        float alphaMax = Mathf.Clamp(PulseAlphaMax, alphaMin, 1.5f);
        float t = (Mathf.Sin(_phase) + 1f) * 0.5f;
        return Mathf.Lerp(alphaMin, alphaMax, t);
    }

    private float ResolvePulseScale()
    {
        if (!PulseEnabled)
        {
            return 1f;
        }

        float amplitude = Mathf.Clamp(PulseScaleAmplitude, 0f, 0.25f);
        float t = (Mathf.Sin(_phase) + 1f) * 0.5f;
        return 1f + Mathf.Lerp(-amplitude, amplitude, t);
    }
}
