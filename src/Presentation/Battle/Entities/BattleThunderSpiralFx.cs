using Godot;

namespace Rpg.Presentation.Battle.Entities;

public partial class BattleThunderSpiralFx : Node2D
{
    public const int DefaultThunderSpiralAreaCells = 3;
    public const float DefaultThunderSpiralVisualScaleMultiplier = 1.5f;
    public const float DefaultVortexCoreDiameterPixels = 128f;
    public static readonly Vector2 DefaultBattleTileSizePixels = new(16f, 16f);

    [Export]
    public NodePath SpiralSpritePath { get; set; } = new("SpiralSprite");

    [Export]
    public NodePath StrikeSparksPath { get; set; } = new("StormLayer/StrikeSparks");

    [Export]
    public NodePath GroundPulsePath { get; set; } = new("StormLayer/GroundPulse");

    [Export(PropertyHint.Range, "0.4,3,0.05")]
    public double LifetimeSeconds { get; set; } = 1.6;

    [Export(PropertyHint.Range, "0.04,0.4,0.01")]
    public double FadeSeconds { get; set; } = 0.18;

    [Export(PropertyHint.Range, "0.5,3,0.05")]
    public float PlaybackSpeedScale { get; set; } = 1.35f;

    [Export]
    public Vector2 AreaPixelSize { get; set; } = ResolveDefaultAreaPixelSize();

    [Export(PropertyHint.Range, "32,170,1")]
    public float VortexCoreDiameterPixels { get; set; } = DefaultVortexCoreDiameterPixels;

    [Export]
    public Color StartModulate { get; set; } = new(0.48f, 0.88f, 1.35f, 0.86f);

    [Export(PropertyHint.Range, "0.12,0.45,0.01")]
    public double StrikeIntervalSeconds { get; set; } = 0.22;

    [Export(PropertyHint.Range, "0.04,0.18,0.01")]
    public double StrikeVisibleSeconds { get; set; } = 0.09;

    [Export(PropertyHint.Range, "32,96,1")]
    public float StrikeHeightPixels { get; set; } = 58f;

    [Export(PropertyHint.Range, "0.1,0.35,0.01")]
    public double FinalConvergenceSeconds { get; set; } = 0.2;

    private const string DefaultAnimationName = "default";

    private AnimatedSprite2D _spiralSprite;
    private CpuParticles2D _strikeSparks;
    private Line2D _groundPulse;
    private readonly StormStrike[] _stormStrikes = new StormStrike[3];
    private Tween _fadeTween;
    private int _playVersion;
    private bool _looping;
    private bool _finalConvergenceStarted;
    private double _elapsedSeconds;
    private double _resolvedLifetimeSeconds;
    private double _nextStrikeSeconds;
    private int _strikeSequence;

    public override void _Ready()
    {
        _spiralSprite = GetNodeOrNull<AnimatedSprite2D>(SpiralSpritePath);
        _strikeSparks = GetNodeOrNull<CpuParticles2D>(StrikeSparksPath);
        _groundPulse = GetNodeOrNull<Line2D>(GroundPulsePath);
        _stormStrikes[0] = ResolveStormStrike("StormLayer/StrikeA");
        _stormStrikes[1] = ResolveStormStrike("StormLayer/StrikeB");
        _stormStrikes[2] = ResolveStormStrike("StormLayer/StrikeC");
        if (_spiralSprite != null && GodotObject.IsInstanceValid(_spiralSprite))
        {
            _spiralSprite.AnimationFinished += OnSpiralAnimationFinished;
        }

        Play();
    }

    public override void _Process(double delta)
    {
        if (!_looping || delta <= 0)
        {
            return;
        }

        _elapsedSeconds += delta;
        double remainingSeconds = _resolvedLifetimeSeconds - _elapsedSeconds;
        if (!_finalConvergenceStarted && remainingSeconds <= FinalConvergenceSeconds)
        {
            ActivateFinalConvergence();
        }

        if (!_finalConvergenceStarted && _elapsedSeconds >= _nextStrikeSeconds)
        {
            ActivateNextStrike();
            _nextStrikeSeconds += System.Math.Clamp(StrikeIntervalSeconds, 0.12, 0.45);
        }

        UpdateStrikeVisibility();
        UpdateGroundPulse();
    }

    public override void _ExitTree()
    {
        _playVersion++;
        _looping = false;
        SetProcess(false);
        if (_spiralSprite != null && GodotObject.IsInstanceValid(_spiralSprite))
        {
            _spiralSprite.AnimationFinished -= OnSpiralAnimationFinished;
        }

        KillTween();
    }

    public void Play(double durationSeconds = 0)
    {
        KillTween();
        int playVersion = ++_playVersion;
        _looping = true;
        _finalConvergenceStarted = false;
        _elapsedSeconds = 0;
        _resolvedLifetimeSeconds = ResolveLifetimeSeconds(durationSeconds);
        _nextStrikeSeconds = 0.04;
        _strikeSequence = 0;
        Visible = true;
        Modulate = Colors.White;
        PrepareSpiralSprite();
        PrepareStormLayer();
        PlayDefaultAnimation();
        SetProcess(true);
        _ = QueueFreeAfterLifetime(playVersion, _resolvedLifetimeSeconds);
    }

    public void ConfigureAreaCoreSize(Vector2 areaPixelSize)
    {
        AreaPixelSize = SanitizeAreaPixelSize(areaPixelSize);
        ApplyAreaScaleIfReady();
    }

    public static Vector2 ResolveDefaultAreaPixelSize()
    {
        return DefaultBattleTileSizePixels *
               DefaultThunderSpiralAreaCells *
               DefaultThunderSpiralVisualScaleMultiplier;
    }

    public static Vector2 ResolveAreaScale(Vector2 areaPixelSize, float coreDiameterPixels)
    {
        Vector2 sanitizedAreaPixelSize = SanitizeAreaPixelSize(areaPixelSize);
        float coreDiameter = float.IsFinite(coreDiameterPixels)
            ? Mathf.Max(1f, coreDiameterPixels)
            : DefaultVortexCoreDiameterPixels;
        return new Vector2(
            sanitizedAreaPixelSize.X / coreDiameter,
            sanitizedAreaPixelSize.Y / coreDiameter);
    }

    private async System.Threading.Tasks.Task QueueFreeAfterLifetime(int playVersion, double lifetimeSeconds)
    {
        if (!IsLifetimeVersionCurrent(playVersion))
        {
            return;
        }

        await ToSignal(
            GetTree().CreateTimer(System.Math.Max(0.2, lifetimeSeconds), processAlways: false),
            SceneTreeTimer.SignalName.Timeout);
        if (!IsLifetimeVersionCurrent(playVersion))
        {
            return;
        }

        _looping = false;
        SetProcess(false);
        double fadeSeconds = System.Math.Clamp(FadeSeconds, 0.04, 0.4);
        _fadeTween = CreateTween().BindNode(this);
        _fadeTween.TweenProperty(this, "modulate", Colors.Transparent, fadeSeconds)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.In);
        _fadeTween.Finished += () =>
        {
            if (IsLifetimeVersionCurrent(playVersion))
            {
                QueueFree();
            }
        };
    }

    private bool IsLifetimeVersionCurrent(int playVersion)
    {
        return GodotObject.IsInstanceValid(this) &&
               IsInsideTree() &&
               playVersion == _playVersion;
    }

    private void PrepareSpiralSprite()
    {
        if (_spiralSprite == null || !GodotObject.IsInstanceValid(_spiralSprite))
        {
            return;
        }

        // This is a coarse area FX placed on the Runtime target cell center; it is
        // intentionally not tied to a hand/palm socket or sub-character bone.
        _spiralSprite.Visible = true;
        _spiralSprite.Frame = 0;
        _spiralSprite.SpeedScale = Mathf.Max(0.05f, PlaybackSpeedScale);
        ApplyAreaScaleIfReady();
        _spiralSprite.Modulate = StartModulate;
    }

    private void PrepareStormLayer()
    {
        foreach (StormStrike strike in _stormStrikes)
        {
            if (strike?.Root != null && GodotObject.IsInstanceValid(strike.Root))
            {
                strike.Root.Visible = false;
                strike.VisibleUntilSeconds = 0;
            }
        }

        if (_strikeSparks != null && GodotObject.IsInstanceValid(_strikeSparks))
        {
            _strikeSparks.Emitting = false;
        }

        if (_groundPulse != null && GodotObject.IsInstanceValid(_groundPulse))
        {
            _groundPulse.Visible = true;
            _groundPulse.Scale = new Vector2(0.45f, 0.19f);
            _groundPulse.Modulate = new Color(0.5f, 0.88f, 1f, 0.42f);
        }
    }

    private void ActivateNextStrike()
    {
        int strikeIndex = _strikeSequence % _stormStrikes.Length;
        float halfWidth = Mathf.Max(18f, AreaPixelSize.X * 0.36f);
        Vector2[] endpoints =
        {
            new(-halfWidth, 5f),
            new(halfWidth * 0.18f, -2f),
            new(halfWidth, 7f)
        };
        ActivateStrike(_stormStrikes[strikeIndex], strikeIndex, endpoints[strikeIndex], converging: false);
        _strikeSequence++;
    }

    private void ActivateFinalConvergence()
    {
        _finalConvergenceStarted = true;
        for (int index = 0; index < _stormStrikes.Length; index++)
        {
            ActivateStrike(_stormStrikes[index], index, Vector2.Zero, converging: true);
        }

        if (_groundPulse != null && GodotObject.IsInstanceValid(_groundPulse))
        {
            _groundPulse.Width = 5.2f;
            _groundPulse.Modulate = new Color(0.82f, 0.98f, 1f, 0.9f);
        }
    }

    private void ActivateStrike(StormStrike strike, int strikeIndex, Vector2 endpoint, bool converging)
    {
        if (strike?.Root == null || !GodotObject.IsInstanceValid(strike.Root))
        {
            return;
        }

        float startOffset = (strikeIndex - 1) * (converging ? 25f : 9f);
        Vector2 start = new(endpoint.X + startOffset, endpoint.Y - Mathf.Max(32f, StrikeHeightPixels));
        Vector2[] points = BuildStrikePoints(start, endpoint, strikeIndex, converging);
        ApplyStrikePoints(strike.Glow, points);
        ApplyStrikePoints(strike.Body, points);
        ApplyStrikePoints(strike.Core, points);
        strike.Root.Visible = true;
        strike.VisibleUntilSeconds = converging
            ? _resolvedLifetimeSeconds + 0.1
            : _elapsedSeconds + System.Math.Clamp(StrikeVisibleSeconds, 0.04, 0.18);

        if (_strikeSparks != null && GodotObject.IsInstanceValid(_strikeSparks))
        {
            _strikeSparks.Position = endpoint;
            _strikeSparks.Restart();
            _strikeSparks.Emitting = true;
        }
    }

    private static Vector2[] BuildStrikePoints(Vector2 start, Vector2 end, int strikeIndex, bool converging)
    {
        const int segmentCount = 7;
        var points = new Vector2[segmentCount + 1];
        Vector2 direction = end - start;
        Vector2 perpendicular = direction.LengthSquared() > 0.01f
            ? direction.Normalized().Orthogonal()
            : Vector2.Right;
        float amplitude = converging ? 5.5f : 4.2f;
        for (int index = 0; index <= segmentCount; index++)
        {
            float t = index / (float)segmentCount;
            float jitter = Mathf.Sin((index + 1) * (4.31f + strikeIndex * 0.43f));
            points[index] = start.Lerp(end, t) + perpendicular * jitter * amplitude * Mathf.Sin(Mathf.Pi * t);
        }

        return points;
    }

    private void UpdateStrikeVisibility()
    {
        foreach (StormStrike strike in _stormStrikes)
        {
            if (strike?.Root != null &&
                GodotObject.IsInstanceValid(strike.Root) &&
                strike.Root.Visible &&
                _elapsedSeconds > strike.VisibleUntilSeconds)
            {
                strike.Root.Visible = false;
            }
        }
    }

    private void UpdateGroundPulse()
    {
        if (_groundPulse == null || !GodotObject.IsInstanceValid(_groundPulse))
        {
            return;
        }

        float pulse = 0.5f + 0.5f * Mathf.Sin((float)_elapsedSeconds * 18f);
        float scale = Mathf.Lerp(0.72f, 1.08f, pulse);
        _groundPulse.Scale = new Vector2(scale, scale * 0.42f);
    }

    private StormStrike ResolveStormStrike(string path)
    {
        Node2D root = GetNodeOrNull<Node2D>(path);
        return root == null
            ? null
            : new StormStrike(
                root,
                root.GetNodeOrNull<Line2D>("Glow"),
                root.GetNodeOrNull<Line2D>("Body"),
                root.GetNodeOrNull<Line2D>("Core"));
    }

    private static void ApplyStrikePoints(Line2D line, Vector2[] points)
    {
        if (line != null && GodotObject.IsInstanceValid(line))
        {
            line.Points = points;
        }
    }

    private void ApplyAreaScaleIfReady()
    {
        if (_spiralSprite == null || !GodotObject.IsInstanceValid(_spiralSprite))
        {
            return;
        }

        // Runtime damage stays a 3x3 logical-tile area. The authored vortex
        // reads small at exact footprint size, so its core is tuned 1.5x larger
        // while remaining anchored to the submitted area center.
        _spiralSprite.Scale = ResolveAreaScale(AreaPixelSize, VortexCoreDiameterPixels);
    }

    private void OnSpiralAnimationFinished()
    {
        if (!_looping || !IsLifetimeVersionCurrent(_playVersion))
        {
            return;
        }

        PlayDefaultAnimation();
    }

    private void PlayDefaultAnimation()
    {
        if (_spiralSprite == null ||
            !GodotObject.IsInstanceValid(_spiralSprite) ||
            _spiralSprite.SpriteFrames?.HasAnimation(DefaultAnimationName) != true)
        {
            return;
        }

        _spiralSprite.Frame = 0;
        _spiralSprite.Play(DefaultAnimationName);
    }

    private double ResolveLifetimeSeconds(double durationSeconds)
    {
        return durationSeconds > 0
            ? System.Math.Clamp(durationSeconds, 0.4, 3.0)
            : System.Math.Clamp(LifetimeSeconds, 0.4, 3.0);
    }

    private static Vector2 SanitizeAreaPixelSize(Vector2 areaPixelSize)
    {
        float x = float.IsFinite(areaPixelSize.X)
            ? Mathf.Max(1f, Mathf.Abs(areaPixelSize.X))
            : ResolveDefaultAreaPixelSize().X;
        float y = float.IsFinite(areaPixelSize.Y)
            ? Mathf.Max(1f, Mathf.Abs(areaPixelSize.Y))
            : ResolveDefaultAreaPixelSize().Y;
        return new Vector2(x, y);
    }

    private void KillTween()
    {
        if (_fadeTween != null && GodotObject.IsInstanceValid(_fadeTween))
        {
            _fadeTween.Kill();
        }

        _fadeTween = null;
    }

    private sealed class StormStrike
    {
        public StormStrike(Node2D root, Line2D glow, Line2D body, Line2D core)
        {
            Root = root;
            Glow = glow;
            Body = body;
            Core = core;
        }

        public Node2D Root { get; }
        public Line2D Glow { get; }
        public Line2D Body { get; }
        public Line2D Core { get; }
        public double VisibleUntilSeconds { get; set; }
    }
}
