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

    private const string DefaultAnimationName = "default";

    private AnimatedSprite2D _spiralSprite;
    private Tween _fadeTween;
    private int _playVersion;
    private bool _looping;

    public override void _Ready()
    {
        _spiralSprite = GetNodeOrNull<AnimatedSprite2D>(SpiralSpritePath);
        if (_spiralSprite != null && GodotObject.IsInstanceValid(_spiralSprite))
        {
            _spiralSprite.AnimationFinished += OnSpiralAnimationFinished;
        }

        Play();
    }

    public override void _ExitTree()
    {
        _playVersion++;
        _looping = false;
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
        Visible = true;
        Modulate = Colors.White;
        PrepareSpiralSprite();
        PlayDefaultAnimation();
        _ = QueueFreeAfterLifetime(playVersion, ResolveLifetimeSeconds(durationSeconds));
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
}
