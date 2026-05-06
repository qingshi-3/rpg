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

    [ExportGroup("播放规则")]

    [Export]
    public bool ReturnToIdleAfterOneShot { get; set; } = true;

    [Export]
    public bool HideAfterDefeatedAnimation { get; set; } = true;

    [Export]
    public double DefeatedFallbackSeconds { get; set; } = 0.65;
}
