using Godot;

namespace Rpg.Presentation.Battle.Entities;

public partial class BattleThunderLinkFx : Node2D
{
    [Export]
    public NodePath ProjectileRootPath { get; set; } = new("ProjectileRoot");

    [Export]
    public NodePath ChainLightningSpritePath { get; set; } = new("ProjectileRoot/ChainLightningSprite");

    [Export(PropertyHint.Range, "0.08,1,0.02")]
    public double LifetimeSeconds { get; set; } = 0.32;

    [Export(PropertyHint.Range, "0.01,0.24,0.01")]
    public double ExtendSeconds { get; set; } = 0.08;

    [Export(PropertyHint.Range, "8,96,1")]
    public float ChainLightningFrameWidthPixels { get; set; } = 48f;

    [Export(PropertyHint.Range, "0.4,3,0.05")]
    public float ChainLightningHeightScale { get; set; } = 1.35f;

    [Export(PropertyHint.Range, "0.01,0.8,0.01")]
    public float InitialLengthScale { get; set; } = 0.12f;

    private Tween _tween;
    private Node2D _projectileRoot;
    private AnimatedSprite2D _chainLightningSprite;

    public override void _Ready()
    {
        _projectileRoot = GetNodeOrNull<Node2D>(ProjectileRootPath);
        _chainLightningSprite = GetNodeOrNull<AnimatedSprite2D>(ChainLightningSpritePath);
        Visible = false;
    }

    public override void _ExitTree()
    {
        KillTween();
    }

    public void Play(Vector2 endpointLocal)
    {
        KillTween();
        Visible = true;
        Modulate = Colors.White;

        PrepareChainLightning(endpointLocal);

        double lifetime = System.Math.Clamp(LifetimeSeconds, 0.08, 1.0);
        double extendSeconds = System.Math.Clamp(ExtendSeconds, 0.01, lifetime);
        _tween = CreateTween();
        _tween.SetParallel();

        if (_chainLightningSprite != null && GodotObject.IsInstanceValid(_chainLightningSprite))
        {
            Vector2 targetScale = ResolveChainLightningScale(endpointLocal.Length());
            _tween.TweenProperty(_chainLightningSprite, "scale", targetScale, extendSeconds)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out);
            _tween.TweenProperty(_chainLightningSprite, "modulate", Colors.Transparent, System.Math.Max(0.04, lifetime - extendSeconds))
                .SetDelay(extendSeconds);
        }

        _tween.Chain().TweenCallback(Callable.From(QueueFree));
    }

    private void PrepareChainLightning(Vector2 endpointLocal)
    {
        if (_projectileRoot == null || !GodotObject.IsInstanceValid(_projectileRoot))
        {
            return;
        }

        _projectileRoot.Position = Vector2.Zero;
        _projectileRoot.Rotation = endpointLocal.Angle();
        _projectileRoot.Scale = Vector2.One;
        _projectileRoot.Modulate = Colors.White;

        if (_chainLightningSprite == null || !GodotObject.IsInstanceValid(_chainLightningSprite))
        {
            return;
        }

        // The Duelyst chain-lightning frame is 48 px wide; X scale stretches that authored asset
        // from the caster origin toward the target instead of drawing a replacement bolt in code.
        Vector2 targetScale = ResolveChainLightningScale(endpointLocal.Length());
        float initialXScale = Mathf.Min(targetScale.X, Mathf.Max(0.01f, InitialLengthScale));
        _chainLightningSprite.Scale = new Vector2(initialXScale, targetScale.Y);
        _chainLightningSprite.Modulate = new Color(0.72f, 0.94f, 1.35f, 0.98f);
        PlayDefaultAnimation(_chainLightningSprite, speedScale: 1.8f);
    }

    private Vector2 ResolveChainLightningScale(float distancePixels)
    {
        float frameWidth = Mathf.Max(1f, ChainLightningFrameWidthPixels);
        float xScale = Mathf.Max(0.05f, distancePixels / frameWidth);
        return new Vector2(xScale, ChainLightningHeightScale);
    }

    private static void PlayDefaultAnimation(AnimatedSprite2D sprite, float speedScale)
    {
        SpriteFrames frames = sprite.SpriteFrames;
        StringName animation = new("default");
        if (frames == null || !frames.HasAnimation(animation))
        {
            return;
        }

        sprite.Frame = 0;
        sprite.SpeedScale = speedScale;
        sprite.Play(animation);
    }

    private void KillTween()
    {
        if (_tween != null && GodotObject.IsInstanceValid(_tween))
        {
            _tween.Kill();
        }

        _tween = null;
    }
}
