using Godot;

namespace Rpg.Presentation.Battle.Entities;

public partial class BattleUnitOverlayAnchorComponent : BattleEntityComponent
{
    [Export]
    public NodePath GridOccupantPath { get; set; } = new("../GridOccupantComponent");

    [Export]
    public NodePath VisualRootPath { get; set; } = new("../VisualRoot");

    [Export]
    public NodePath SpritePath { get; set; } = new("../VisualRoot/AnimatedSprite2D");

    [Export]
    public Vector2 FootprintCellProjection { get; set; } = new(36f, 20f);

    [Export(PropertyHint.Range, "0,24,1")]
    public float HeadPaddingPixels { get; set; } = 5f;

    [Export]
    public float FallbackTopY { get; set; } = -34f;

    private GridOccupantComponent _gridOccupant;
    private Node2D _visualRoot;
    private AnimatedSprite2D _sprite;

    protected override void OnAttached()
    {
        ResolveNodes();
    }

    public Vector2 ResolveHeadOverlayPosition(Vector2 overlaySize)
    {
        ResolveNodes();

        float topY = System.Math.Min(ResolveFootprintTopY(), ResolveVisualTopY());
        return new Vector2(
            -overlaySize.X * 0.5f,
            topY - Mathf.Max(0f, HeadPaddingPixels) - overlaySize.Y);
    }

    private void ResolveNodes()
    {
        _gridOccupant ??= ResolveSibling<GridOccupantComponent>(GridOccupantPath);
        _visualRoot ??= ResolveSibling<Node2D>(VisualRootPath);
        _sprite ??= ResolveSibling<AnimatedSprite2D>(SpritePath);
    }

    private float ResolveFootprintTopY()
    {
        int width = System.Math.Clamp(_gridOccupant?.FootprintWidth ?? 1, 1, 5);
        int height = System.Math.Clamp(_gridOccupant?.FootprintHeight ?? 1, 1, 5);
        float cellProjectionY = Mathf.Max(1f, FootprintCellProjection.Y);

        // Entity positions are already snapped to footprint center; this turns
        // the occupied footprint into a reusable local head-space bound.
        return -((width + height) * cellProjectionY * 0.5f);
    }

    private float ResolveVisualTopY()
    {
        Texture2D texture = ResolveCurrentFrameTexture();
        if (_sprite == null || texture == null)
        {
            return FallbackTopY;
        }

        float rootScaleY = _visualRoot == null ? 1f : _visualRoot.Scale.Y;
        float spriteScaleY = _sprite.Scale.Y;
        float scaleY = Mathf.Abs(rootScaleY * spriteScaleY);
        float rootY = _visualRoot?.Position.Y ?? 0f;
        float spriteY = _sprite.Position.Y * rootScaleY;
        float topY = rootY + spriteY + (_sprite.Offset.Y * scaleY);
        if (_sprite.Centered)
        {
            topY -= texture.GetSize().Y * scaleY * 0.5f;
        }

        return topY;
    }

    private Texture2D ResolveCurrentFrameTexture()
    {
        if (_sprite?.SpriteFrames == null)
        {
            return null;
        }

        StringName animationName = _sprite.Animation;
        if (!_sprite.SpriteFrames.HasAnimation(animationName))
        {
            string[] names = _sprite.SpriteFrames.GetAnimationNames();
            if (names.Length == 0)
            {
                return null;
            }

            animationName = names[0];
        }

        int frameCount = _sprite.SpriteFrames.GetFrameCount(animationName);
        if (frameCount <= 0)
        {
            return null;
        }

        int frameIndex = System.Math.Clamp(_sprite.Frame, 0, frameCount - 1);
        return _sprite.SpriteFrames.GetFrameTexture(animationName, frameIndex);
    }

    private T ResolveSibling<T>(NodePath path) where T : Node
    {
        string value = path?.ToString() ?? "";
        return string.IsNullOrWhiteSpace(value)
            ? null
            : GetNodeOrNull<T>(path);
    }
}
