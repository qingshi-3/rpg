using Godot;

namespace Rpg.Presentation.Battle.Entities;

public partial class BattleThunderLinkFx : Node2D
{
    [Export] public NodePath BoltGlowPath { get; set; } = new("BoltGlow");
    [Export] public NodePath BoltBodyPath { get; set; } = new("BoltBody");
    [Export] public NodePath BoltCorePath { get; set; } = new("BoltCore");
    [Export] public NodePath ProjectileHeadPath { get; set; } = new("ProjectileHead");
    [Export] public NodePath ChainLightningAccentPath { get; set; } = new("ProjectileHead/ChainLightningAccent");
    [Export] public NodePath TrailSparksPath { get; set; } = new("ProjectileHead/TrailSparks");
    [Export] public NodePath ImpactSparksPath { get; set; } = new("ImpactSparks");

    [Export(PropertyHint.Range, "0.1,0.3,0.01")]
    public double MinTravelSeconds { get; set; } = 0.15;

    [Export(PropertyHint.Range, "0.15,0.4,0.01")]
    public double MaxTravelSeconds { get; set; } = 0.28;

    [Export(PropertyHint.Range, "32,320,1")]
    public float DistanceForMaxDurationPixels { get; set; } = 176f;

    [Export(PropertyHint.Range, "20,120,1")]
    public float TrailLengthPixels { get; set; } = 58f;

    [Export(PropertyHint.Range, "1,12,0.5")]
    public float ZigZagAmplitudePixels { get; set; } = 5f;

    [Export(PropertyHint.Range, "4,12,1")]
    public int BoltSegmentCount { get; set; } = 7;

    [Export(PropertyHint.Range, "0.05,0.25,0.01")]
    public double ImpactHoldSeconds { get; set; } = 0.12;

    private Line2D _boltGlow;
    private Line2D _boltBody;
    private Line2D _boltCore;
    private Node2D _projectileHead;
    private AnimatedSprite2D _chainLightningAccent;
    private CpuParticles2D _trailSparks;
    private CpuParticles2D _impactSparks;
    private Line2D[] _branches = System.Array.Empty<Line2D>();
    private Tween _finishTween;
    private Vector2 _endpointLocal;
    private double _travelDurationSeconds;
    private double _elapsedSeconds;
    private bool _travelling;

    public override void _Ready()
    {
        _boltGlow = GetNodeOrNull<Line2D>(BoltGlowPath);
        _boltBody = GetNodeOrNull<Line2D>(BoltBodyPath);
        _boltCore = GetNodeOrNull<Line2D>(BoltCorePath);
        _projectileHead = GetNodeOrNull<Node2D>(ProjectileHeadPath);
        _chainLightningAccent = GetNodeOrNull<AnimatedSprite2D>(ChainLightningAccentPath);
        _trailSparks = GetNodeOrNull<CpuParticles2D>(TrailSparksPath);
        _impactSparks = GetNodeOrNull<CpuParticles2D>(ImpactSparksPath);
        _branches = new[]
        {
            GetNodeOrNull<Line2D>("Branches/BranchA"),
            GetNodeOrNull<Line2D>("Branches/BranchB"),
            GetNodeOrNull<Line2D>("Branches/BranchC")
        };
        Visible = false;
        SetProcess(false);
    }

    public override void _Process(double delta)
    {
        if (!_travelling || delta <= 0)
        {
            return;
        }

        _elapsedSeconds += delta;
        float progress = (float)Mathf.Clamp(_elapsedSeconds / _travelDurationSeconds, 0, 1);
        UpdateTravellingBolt(progress);
        if (progress >= 1f)
        {
            FinishTravel();
        }
    }

    public override void _ExitTree()
    {
        _travelling = false;
        KillFinishTween();
    }

    public double Play(Vector2 endpointLocal)
    {
        KillFinishTween();
        _endpointLocal = endpointLocal.IsFinite() ? endpointLocal : Vector2.Zero;
        _travelDurationSeconds = ResolveTravelDurationSeconds(_endpointLocal.Length());
        _elapsedSeconds = 0;
        _travelling = true;
        Visible = true;
        Modulate = Colors.White;

        PrepareProjectileHead();
        SetLineVisibility(true);
        if (IsValid(_trailSparks))
        {
            _trailSparks.Emitting = true;
        }

        if (IsValid(_impactSparks))
        {
            _impactSparks.Emitting = false;
        }

        UpdateTravellingBolt(0);
        SetProcess(true);
        return _travelDurationSeconds;
    }

    public double ResolveTravelDurationSeconds(float distancePixels)
    {
        double minimum = System.Math.Clamp(MinTravelSeconds, 0.1, 0.3);
        double maximum = System.Math.Clamp(MaxTravelSeconds, minimum, 0.4);
        float distanceRatio = Mathf.Clamp(
            Mathf.Max(0, distancePixels) / Mathf.Max(1f, DistanceForMaxDurationPixels),
            0,
            1);
        return Mathf.Lerp((float)minimum, (float)maximum, Mathf.Sqrt(distanceRatio));
    }

    private void UpdateTravellingBolt(float progress)
    {
        float easedProgress = 1f - Mathf.Pow(1f - progress, 1.65f);
        Vector2 head = _endpointLocal * easedProgress;
        Vector2 direction = _endpointLocal.LengthSquared() > 0.01f
            ? _endpointLocal.Normalized()
            : Vector2.Right;
        float travelledDistance = head.Length();
        Vector2 tail = direction * Mathf.Max(0, travelledDistance - Mathf.Max(4f, TrailLengthPixels));
        Vector2[] points = BuildBoltPoints(tail, head, BoltSegmentCount, ZigZagAmplitudePixels, phase: 0);

        ApplyPoints(_boltGlow, points);
        ApplyPoints(_boltBody, points);
        ApplyPoints(_boltCore, points);
        UpdateBranches(points, direction);

        if (IsValid(_projectileHead))
        {
            _projectileHead.Position = head;
            _projectileHead.Rotation = direction.Angle();
        }
    }

    private void UpdateBranches(Vector2[] boltPoints, Vector2 direction)
    {
        if (boltPoints.Length < 4)
        {
            return;
        }

        Vector2 perpendicular = direction.Orthogonal();
        for (int index = 0; index < _branches.Length; index++)
        {
            Line2D branch = _branches[index];
            if (!IsValid(branch))
            {
                continue;
            }

            int anchorIndex = System.Math.Clamp(boltPoints.Length - 2 - index, 1, boltPoints.Length - 2);
            Vector2 anchor = boltPoints[anchorIndex];
            float side = index % 2 == 0 ? 1f : -1f;
            float length = 7f + index * 2f;
            branch.Points = new[]
            {
                anchor,
                anchor - direction * (length * 0.45f) + perpendicular * side * (length * 0.35f),
                anchor - direction * length + perpendicular * side * length
            };
            branch.Visible = true;
        }
    }

    private static Vector2[] BuildBoltPoints(
        Vector2 start,
        Vector2 end,
        int segmentCount,
        float amplitude,
        int phase)
    {
        int segments = System.Math.Clamp(segmentCount, 4, 12);
        var points = new Vector2[segments + 1];
        Vector2 direction = end - start;
        Vector2 perpendicular = direction.LengthSquared() > 0.01f
            ? direction.Normalized().Orthogonal()
            : Vector2.Up;
        for (int index = 0; index <= segments; index++)
        {
            float t = index / (float)segments;
            float envelope = Mathf.Sin(Mathf.Pi * t);
            float deterministicJitter = Mathf.Sin((index + 1) * 4.73f + phase * 1.91f);
            points[index] = start.Lerp(end, t) + perpendicular * deterministicJitter * amplitude * envelope;
        }

        return points;
    }

    private void PrepareProjectileHead()
    {
        if (IsValid(_projectileHead))
        {
            _projectileHead.Visible = true;
            _projectileHead.Position = Vector2.Zero;
        }

        if (IsValid(_chainLightningAccent))
        {
            // The source sheet remains a compact head accent; directional readability
            // comes from the authored layered trail and never from stretching the frame.
            _chainLightningAccent.Visible = true;
            _chainLightningAccent.Scale = new Vector2(0.42f, 0.9f);
            _chainLightningAccent.Frame = 0;
            _chainLightningAccent.SpeedScale = 2.2f;
            _chainLightningAccent.Play("default");
        }
    }

    private void FinishTravel()
    {
        _travelling = false;
        SetProcess(false);
        if (IsValid(_trailSparks))
        {
            _trailSparks.Emitting = false;
        }

        if (IsValid(_impactSparks))
        {
            _impactSparks.Position = _endpointLocal;
            _impactSparks.Restart();
            _impactSparks.Emitting = true;
        }

        if (IsValid(_projectileHead))
        {
            _projectileHead.Visible = false;
        }

        _finishTween = CreateTween().BindNode(this);
        _finishTween.TweenProperty(this, "modulate", Colors.Transparent, System.Math.Clamp(ImpactHoldSeconds, 0.05, 0.25));
        _finishTween.Finished += QueueFree;
    }

    private void SetLineVisibility(bool visible)
    {
        if (IsValid(_boltGlow)) _boltGlow.Visible = visible;
        if (IsValid(_boltBody)) _boltBody.Visible = visible;
        if (IsValid(_boltCore)) _boltCore.Visible = visible;
        foreach (Line2D branch in _branches)
        {
            if (IsValid(branch)) branch.Visible = visible;
        }
    }

    private static void ApplyPoints(Line2D line, Vector2[] points)
    {
        if (IsValid(line))
        {
            line.Points = points;
        }
    }

    private static bool IsValid(GodotObject value)
    {
        return value != null && GodotObject.IsInstanceValid(value);
    }

    private void KillFinishTween()
    {
        if (IsValid(_finishTween))
        {
            _finishTween.Kill();
        }

        _finishTween = null;
    }
}
