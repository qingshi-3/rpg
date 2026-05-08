using Godot;

namespace Rpg.Definitions.Battle.Animation;

[GlobalClass]
public partial class BattleUnitAnimationSet : Resource
{
    [ExportGroup("动画名称")]

    [Export]
    public string IdleAnimation { get; set; } = "idle";

    [Export]
    public string MoveAnimation { get; set; } = "move";

    [Export]
    public string AttackAnimation { get; set; } = "attack";

    [Export]
    public string HitAnimation { get; set; } = "hit";

    [Export]
    public string DefeatedAnimation { get; set; } = "defeated";

    [ExportGroup("AnimatedSprite2D")]

    [Export]
    public bool PreferAnimatedSprite { get; set; } = true;

    [ExportGroup("AnimatedSprite2D Frame Balancing")]

    [Export]
    public bool BalanceSpriteSpeedByFrameCount { get; set; } = true;

    [Export]
    public float MinBalancedSpeedScale { get; set; } = 1f;

    [Export]
    public float MaxBalancedSpeedScale { get; set; } = 4.5f;

    [Export]
    public double TargetIdleCycleSeconds { get; set; } = 1.2;

    [Export]
    public double TargetMoveCycleSeconds { get; set; } = 0.5;

    [Export]
    public double TargetAttackSeconds { get; set; } = 1.2;

    [Export]
    public double TargetHitSeconds { get; set; } = 0.48;

    [Export]
    public double TargetDefeatedSeconds { get; set; } = 0.8;

    [ExportGroup("Combat Timing")]

    [Export(PropertyHint.Range, "0,1,0.05")]
    public double AttackImpactNormalizedTime { get; set; } = 0.55;

    [Export(PropertyHint.Range, "0,1,0.05")]
    public double HitMinimumAttackDurationRatio { get; set; } = 0.4;

    [Export(PropertyHint.Range, "0,1,0.05")]
    public double DefeatedMinimumAttackDurationRatio { get; set; } = 0.4;

    [ExportGroup("播放规则")]

    [Export]
    public bool ReturnToIdleAfterOneShot { get; set; } = true;

    [Export]
    public bool HideAfterDefeatedAnimation { get; set; } = true;

    [Export]
    public double DefeatedFallbackSeconds { get; set; } = 0.65;
}
