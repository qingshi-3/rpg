using Godot;
using Rpg.Definitions.Battle;

namespace Rpg.Presentation.Common;

public partial class BattleUnitPlinthPreview : Node2D
{
    private static readonly Vector2 PlinthSize = new(176f, 80f);
    private static readonly Vector2 HeroOffset = new(0f, -39f);
    private static readonly Vector2 HeroMaxSize = new(188f, 130f);
    private const BattleUnitAnimatedPreviewLayoutMode HeroPreviewLayoutMode = BattleUnitAnimatedPreviewLayoutMode.FrameRect;

    [Export]
    public SpriteFrames SpriteFrames { get; set; }

    [Export]
    public string AnimationName { get; set; } = "idle";

    [Export]
    public BattleUnitVisualDefinition Visual { get; set; }

    [Export]
    public Texture2D PlinthTexture { get; set; }

    [Export]
    public NodePath PlinthPath { get; set; } = new("Plinth");

    [Export]
    public NodePath HeroPreviewPath { get; set; } = new("HeroPreview");

    private Sprite2D _plinth;
    private BattleUnitAnimatedPreview _heroPreview;

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
        ResolveNodes();
        ApplyPlinthLayout();
        ApplyHeroLayout();
    }

    private void ResolveNodes()
    {
        _plinth ??= GetNodeOrNull<Sprite2D>(PlinthPath);
        _heroPreview ??= GetNodeOrNull<BattleUnitAnimatedPreview>(HeroPreviewPath);
    }

    private void ApplyPlinthLayout()
    {
        if (_plinth == null)
        {
            return;
        }

        if (PlinthTexture != null)
        {
            _plinth.Texture = PlinthTexture;
        }

        _plinth.Position = Vector2.Zero;
        _plinth.Scale = ResolvePlinthScale(_plinth.Texture);
    }

    private void ApplyHeroLayout()
    {
        if (_heroPreview == null)
        {
            return;
        }

        // Parent scenes place this component at the plinth center; this offset
        // is the reusable tuning point for keeping the hero grounded on it.
        _heroPreview.Position = HeroOffset;
        _heroPreview.MaxSize = HeroMaxSize;
        _heroPreview.PreviewLayoutMode = HeroPreviewLayoutMode;
        _heroPreview.Bind(SpriteFrames, AnimationName, Visual);
    }

    private Vector2 ResolvePlinthScale(Texture2D texture)
    {
        Vector2 textureSize = texture?.GetSize() ?? Vector2.Zero;
        if (textureSize.X <= 0f || textureSize.Y <= 0f)
        {
            return Vector2.One;
        }

        return new Vector2(
            Mathf.Max(1f, PlinthSize.X) / textureSize.X,
            Mathf.Max(1f, PlinthSize.Y) / textureSize.Y);
    }
}
