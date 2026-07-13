using Godot;

namespace Rpg.Presentation.Battle.Entities;

public partial class BattleThunderTeleportFx : Node2D
{
    [Export] public NodePath OriginBurstPath { get; set; } = new("OriginBurst");
    [Export] public NodePath DestinationBurstPath { get; set; } = new("DestinationBurst");

    [Export(PropertyHint.Range, "0.1,0.22,0.01")]
    public double FoldDurationSeconds { get; set; } = 0.15;

    [Export(PropertyHint.Range, "0,0.05,0.002")]
    public double StrandStaggerSeconds { get; set; } = 0.018;

    [Export(PropertyHint.Range, "1,10,0.5")]
    public float StrandOffsetPixels { get; set; } = 4.5f;

    [Export(PropertyHint.Range, "0.05,0.3,0.01")]
    public double LingerSeconds { get; set; } = 0.12;

    private readonly Strand[] _strands = new Strand[3];
    private Node2D _originBurst;
    private Node2D _destinationBurst;
    private CanvasItem _actorVisual;
    private Tween _burstTween;
    private Vector2 _endpointLocal;
    private double _elapsedSeconds;
    private double _resolvedDurationSeconds;
    private int _playVersion;
    private bool _playing;

    public override void _Ready()
    {
        _originBurst = GetNodeOrNull<Node2D>(OriginBurstPath);
        _destinationBurst = GetNodeOrNull<Node2D>(DestinationBurstPath);
        _strands[0] = ResolveStrand("Strands/StrandA");
        _strands[1] = ResolveStrand("Strands/StrandB");
        _strands[2] = ResolveStrand("Strands/StrandC");
        Visible = false;
        SetProcess(false);
    }

    public override void _Process(double delta)
    {
        if (!_playing || delta <= 0)
        {
            return;
        }

        _elapsedSeconds += delta;
        for (int index = 0; index < _strands.Length; index++)
        {
            double delay = StrandStaggerSeconds * index;
            float progress = (float)Mathf.Clamp(
                (_elapsedSeconds - delay) / System.Math.Max(0.01, _resolvedDurationSeconds - delay),
                0,
                1);
            UpdateStrand(_strands[index], index, progress);
        }

        if (_elapsedSeconds >= _resolvedDurationSeconds)
        {
            CompleteFold();
        }
    }

    public override void _ExitTree()
    {
        _playVersion++;
        _playing = false;
        RevealActorVisual();
        KillBurstTween();
    }

    public double Play(Vector2 endpointLocal, CanvasItem actorVisual)
    {
        KillBurstTween();
        int playVersion = ++_playVersion;
        _endpointLocal = endpointLocal.IsFinite() ? endpointLocal : Vector2.Zero;
        _actorVisual = actorVisual;
        _resolvedDurationSeconds = System.Math.Clamp(FoldDurationSeconds, 0.12, 0.18);
        _elapsedSeconds = 0;
        _playing = true;
        Visible = true;
        Modulate = Colors.White;

        if (IsValid(_actorVisual))
        {
            // Runtime is already at the destination. Only the authored actor visual is
            // withheld until the fold converges; the entity itself is never tweened.
            _actorVisual.Visible = false;
        }

        PrepareBurst(_originBurst, Vector2.Zero, visible: true);
        PrepareBurst(_destinationBurst, _endpointLocal, visible: false);
        AnimateBurst(_originBurst, playVersion);
        for (int index = 0; index < _strands.Length; index++)
        {
            SetStrandVisible(_strands[index], false);
        }

        SetProcess(true);
        return _resolvedDurationSeconds;
    }

    private void CompleteFold()
    {
        _playing = false;
        SetProcess(false);
        for (int index = 0; index < _strands.Length; index++)
        {
            UpdateStrand(_strands[index], index, 1f);
        }

        RevealActorVisual();
        PrepareBurst(_destinationBurst, _endpointLocal, visible: true);
        AnimateBurst(_destinationBurst, _playVersion);
        _ = QueueFreeAfterLingerAsync(_playVersion);
    }

    private async System.Threading.Tasks.Task QueueFreeAfterLingerAsync(int playVersion)
    {
        await ToSignal(
            GetTree().CreateTimer(System.Math.Clamp(LingerSeconds, 0.05, 0.3), processAlways: false),
            SceneTreeTimer.SignalName.Timeout);
        if (IsCurrent(playVersion))
        {
            QueueFree();
        }
    }

    private void UpdateStrand(Strand strand, int strandIndex, float progress)
    {
        if (strand == null)
        {
            return;
        }

        if (progress <= 0)
        {
            SetStrandVisible(strand, false);
            return;
        }

        SetStrandVisible(strand, true);
        float easedProgress = 1f - Mathf.Pow(1f - progress, 2f);
        Vector2 end = _endpointLocal * easedProgress;
        Vector2[] points = BuildStrandPoints(end, strandIndex);
        ApplyPoints(strand.Glow, points);
        ApplyPoints(strand.Body, points);
        ApplyPoints(strand.Core, points);
    }

    private Vector2[] BuildStrandPoints(Vector2 end, int strandIndex)
    {
        const int segmentCount = 8;
        var points = new Vector2[segmentCount + 1];
        Vector2 perpendicular = _endpointLocal.LengthSquared() > 0.01f
            ? _endpointLocal.Normalized().Orthogonal()
            : Vector2.Up;
        float strandSign = strandIndex - 1;
        for (int index = 0; index <= segmentCount; index++)
        {
            float t = index / (float)segmentCount;
            float envelope = Mathf.Sin(Mathf.Pi * t);
            float offset = strandSign * StrandOffsetPixels * envelope;
            float jitter = Mathf.Sin((index + 1) * (4.17f + strandIndex * 0.37f)) * 2.2f * envelope;
            points[index] = end * t + perpendicular * (offset + jitter);
        }

        return points;
    }

    private Strand ResolveStrand(string path)
    {
        Node2D root = GetNodeOrNull<Node2D>(path);
        return root == null
            ? null
            : new Strand(
                root,
                root.GetNodeOrNull<Line2D>("Glow"),
                root.GetNodeOrNull<Line2D>("Body"),
                root.GetNodeOrNull<Line2D>("Core"));
    }

    private void PrepareBurst(Node2D burst, Vector2 position, bool visible)
    {
        if (!IsValid(burst))
        {
            return;
        }

        burst.Position = position;
        burst.Visible = visible;
        burst.Scale = Vector2.One;
        burst.Modulate = Colors.White;
        CpuParticles2D particles = burst.GetNodeOrNull<CpuParticles2D>("Sparks");
        if (IsValid(particles))
        {
            particles.Emitting = false;
        }
    }

    private void AnimateBurst(Node2D burst, int playVersion)
    {
        if (!IsValid(burst) || !IsCurrent(playVersion))
        {
            return;
        }

        CpuParticles2D particles = burst.GetNodeOrNull<CpuParticles2D>("Sparks");
        if (IsValid(particles))
        {
            particles.Restart();
            particles.Emitting = true;
        }

        Line2D ring = burst.GetNodeOrNull<Line2D>("Ring");
        if (IsValid(ring))
        {
            ring.Scale = new Vector2(0.35f, 0.15f);
            ring.Modulate = Colors.White;
            _burstTween = CreateTween().BindNode(this).SetParallel();
            _burstTween.TweenProperty(ring, "scale", new Vector2(1.2f, 0.5f), 0.12)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out);
            _burstTween.TweenProperty(ring, "modulate", Colors.Transparent, 0.12);
        }
    }

    private void RevealActorVisual()
    {
        if (IsValid(_actorVisual))
        {
            _actorVisual.Visible = true;
        }

        _actorVisual = null;
    }

    private bool IsCurrent(int playVersion)
    {
        return GodotObject.IsInstanceValid(this) && IsInsideTree() && playVersion == _playVersion;
    }

    private static void SetStrandVisible(Strand strand, bool visible)
    {
        if (strand != null && IsValid(strand.Root))
        {
            strand.Root.Visible = visible;
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

    private void KillBurstTween()
    {
        if (IsValid(_burstTween))
        {
            _burstTween.Kill();
        }

        _burstTween = null;
    }

    private sealed record Strand(Node2D Root, Line2D Glow, Line2D Body, Line2D Core);
}
