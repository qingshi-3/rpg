using Godot;
using Rpg.Definitions.Battle.Animation;

namespace Rpg.Definitions.Battle;

[GlobalClass]
public partial class BattleUnitVisualDefinition : Resource
{
    [Export]
    public SpriteFrames SpriteFrames { get; set; }

    [Export]
    public BattleUnitAnimationSet AnimationSet { get; set; }

    [ExportGroup("Auto Layout")]

    [Export]
    public bool AutoLayoutFromSpriteFrames { get; set; } = true;

    [Export]
    public float TargetMaxSpriteSizePixels { get; set; } = 40f;

    [Export]
    public float GroundAnchorOffsetPixels { get; set; } = 5f;

    [Export]
    public float VisibleAlphaThreshold { get; set; } = 0.05f;

    [ExportGroup("Manual Layout")]

    [Export]
    public Vector2 Offset { get; set; } = Vector2.Zero;

    [Export]
    public Vector2 Scale { get; set; } = Vector2.One;

    [Export]
    public Color Modulate { get; set; } = Colors.White;
}
