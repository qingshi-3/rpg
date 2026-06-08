using Godot;

namespace Rpg.Presentation.Battle.Entities;

public partial class BattleSkillImpactFx : Node2D
{
    [Export]
    public NodePath AnimatedSpritePath { get; set; } = new("AnimatedSprite2D");

    [Export]
    public string AnimationName { get; set; } = "fx_impactbrightwhitemedium";

    [Export(PropertyHint.Range, "0.05,1.5,0.05")]
    public double FallbackLifetimeSeconds { get; set; } = 0.6;

    [Export(PropertyHint.Range, "0.1,4,0.05")]
    public float SpeedScale { get; set; } = 1.15f;

    private AnimatedSprite2D _sprite;
    private bool _animationFinishedConnected;

    public override void _Ready()
    {
        _sprite = GetNodeOrNull<AnimatedSprite2D>(AnimatedSpritePath);
        Play();
    }

    public override void _ExitTree()
    {
        if (_animationFinishedConnected && _sprite != null && GodotObject.IsInstanceValid(_sprite))
        {
            _sprite.AnimationFinished -= OnAnimationFinished;
        }

        _animationFinishedConnected = false;
        _sprite = null;
    }

    public void Play()
    {
        if (_sprite == null || !GodotObject.IsInstanceValid(_sprite))
        {
            _ = QueueFreeAfterFallback();
            return;
        }

        if (!_animationFinishedConnected)
        {
            _sprite.AnimationFinished += OnAnimationFinished;
            _animationFinishedConnected = true;
        }

        SpriteFrames frames = _sprite.SpriteFrames;
        StringName animation = ResolveAnimationName(frames);
        if (frames == null || string.IsNullOrWhiteSpace(animation.ToString()))
        {
            _ = QueueFreeAfterFallback();
            return;
        }

        _sprite.SpeedScale = SpeedScale;
        _sprite.Play(animation);
    }

    private StringName ResolveAnimationName(SpriteFrames frames)
    {
        if (frames == null)
        {
            return default;
        }

        var configured = new StringName(AnimationName);
        if (!string.IsNullOrWhiteSpace(AnimationName) && frames.HasAnimation(configured))
        {
            return configured;
        }

        var fallback = new StringName("default");
        return frames.HasAnimation(fallback) ? fallback : default;
    }

    private void OnAnimationFinished()
    {
        QueueFree();
    }

    private async System.Threading.Tasks.Task QueueFreeAfterFallback()
    {
        if (!IsInsideTree())
        {
            QueueFree();
            return;
        }

        await ToSignal(GetTree().CreateTimer(System.Math.Max(0.05, FallbackLifetimeSeconds)), SceneTreeTimer.SignalName.Timeout);
        QueueFree();
    }
}
