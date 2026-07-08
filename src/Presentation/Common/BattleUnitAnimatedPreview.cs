using Godot;
using Rpg.Definitions.Battle;
using Rpg.Presentation.Battle.Entities;

namespace Rpg.Presentation.Common;

// World overlays need ground-anchored visual bounds; UI cards need the old
// TextureRect-style whole-frame slot fit so animation does not change scale.
public enum BattleUnitAnimatedPreviewLayoutMode
{
    VisualBounds = 0,
    FrameRect = 1
}

public partial class BattleUnitAnimatedPreview : Node2D
{
    [Export]
    public SpriteFrames SpriteFrames { get; set; }

    [Export]
    public string AnimationName { get; set; } = "idle";

    [Export]
    public BattleUnitVisualDefinition Visual { get; set; }

    [Export]
    public Vector2 MaxSize { get; set; } = new(72f, 72f);

    [Export]
    public BattleUnitAnimatedPreviewLayoutMode PreviewLayoutMode { get; set; } = BattleUnitAnimatedPreviewLayoutMode.VisualBounds;

    [Export]
    public NodePath AnimatedSpritePath { get; set; } = new("AnimatedSprite2D");

    private AnimatedSprite2D _sprite;

    public override void _Ready()
    {
        ApplyBinding();
    }

    public void Bind(BattleUnitAnimatedPreviewModel preview)
    {
        Bind(preview?.SpriteFrames, preview?.AnimationName, preview?.Visual);
    }

    public void Bind(SpriteFrames spriteFrames, string animationName, BattleUnitVisualDefinition visual)
    {
        SpriteFrames = spriteFrames;
        AnimationName = string.IsNullOrWhiteSpace(animationName) ? "idle" : animationName.Trim();
        Visual = visual;
        ApplyBinding();
    }

    private void ApplyBinding()
    {
        _sprite ??= GetNodeOrNull<AnimatedSprite2D>(AnimatedSpritePath);
        if (_sprite == null)
        {
            return;
        }

        _sprite.SpriteFrames = SpriteFrames;
        _sprite.Centered = true;
        _sprite.Visible = SpriteFrames != null;
        if (SpriteFrames == null)
        {
            _sprite.Stop();
            return;
        }

        ApplyLayout();
        string animationName = ResolveAnimationName();
        if (string.IsNullOrWhiteSpace(animationName))
        {
            _sprite.Stop();
            return;
        }

        _sprite.Play(animationName);
    }

    private void ApplyLayout()
    {
        if (PreviewLayoutMode == BattleUnitAnimatedPreviewLayoutMode.FrameRect)
        {
            ApplyFrameRectLayout();
            return;
        }

        if (Visual?.AutoLayoutFromSpriteFrames == true &&
            BattleUnitVisualLayoutCalculator.TryCalculateAutoLayout(
                SpriteFrames,
                ResolveTargetMaxSize(),
                Visual.GroundAnchorOffsetPixels,
                Visual.VisibleAlphaThreshold,
                out BattleUnitVisualLayout layout))
        {
            _sprite.Position = layout.Position;
            _sprite.Offset = Vector2.Zero;
            _sprite.Scale = layout.Scale;
            _sprite.Modulate = Visual.Modulate;
            return;
        }

        _sprite.Position = Vector2.Zero;
        _sprite.Offset = Vector2.Zero;
        _sprite.Scale = ResolveFallbackScale();
        _sprite.Modulate = Visual?.Modulate ?? Colors.White;
    }

    private void ApplyFrameRectLayout()
    {
        _sprite.Position = Vector2.Zero;
        _sprite.Offset = Vector2.Zero;
        _sprite.Scale = ResolveFrameRectScale();
        _sprite.Modulate = Visual?.Modulate ?? Colors.White;
    }

    private string ResolveAnimationName()
    {
        if (SpriteFrames == null)
        {
            return "";
        }

        string configured = string.IsNullOrWhiteSpace(AnimationName) ? "idle" : AnimationName.Trim();
        if (SpriteFrames.HasAnimation(configured))
        {
            return configured;
        }

        if (SpriteFrames.HasAnimation("idle"))
        {
            return "idle";
        }

        foreach (StringName animationName in SpriteFrames.GetAnimationNames())
        {
            return animationName.ToString();
        }

        return "";
    }

    private Vector2 ResolveFallbackScale()
    {
        string animationName = ResolveAnimationName();
        Texture2D texture = string.IsNullOrWhiteSpace(animationName)
            ? null
            : SpriteFrames?.GetFrameTexture(animationName, 0);
        Vector2 textureSize = texture?.GetSize() ?? Vector2.Zero;
        if (textureSize.X <= 0f || textureSize.Y <= 0f)
        {
            return Visual?.Scale ?? Vector2.One;
        }

        float scale = Mathf.Min(
            Mathf.Max(1f, MaxSize.X) / textureSize.X,
            Mathf.Max(1f, MaxSize.Y) / textureSize.Y);
        return (Visual?.Scale ?? Vector2.One) * scale;
    }

    private Vector2 ResolveFrameRectScale()
    {
        string animationName = ResolveAnimationName();
        Texture2D texture = string.IsNullOrWhiteSpace(animationName)
            ? null
            : SpriteFrames?.GetFrameTexture(animationName, 0);
        Vector2 textureSize = texture?.GetSize() ?? Vector2.Zero;
        if (textureSize.X <= 0f || textureSize.Y <= 0f)
        {
            return Vector2.One;
        }

        float scale = Mathf.Min(
            Mathf.Max(1f, MaxSize.X) / textureSize.X,
            Mathf.Max(1f, MaxSize.Y) / textureSize.Y);
        return new Vector2(scale, scale);
    }

    private float ResolveTargetMaxSize()
    {
        return Mathf.Max(1f, Mathf.Min(MaxSize.X, MaxSize.Y));
    }

}
