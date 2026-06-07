using Godot;
using Rpg.Infrastructure.Logging;

namespace Rpg.Presentation.Battle.Entities;

public partial class BattleSkillCastFxComponent : BattleEntityComponent
{
    private const string DefaultSkillCastFxScenePath = "res://scenes/battle/entities/fx/BattleSkillCastFx.tscn";

    [Export]
    public PackedScene SkillCastFxScene { get; set; }

    [Export]
    public NodePath FxParentPath { get; set; } = new("../VisualRoot");

    [Export]
    public Vector2 FxOffset { get; set; } = new(0f, -10f);

    public void PlaySkillCastFx(double durationSeconds = 0)
    {
        PackedScene scene = SkillCastFxScene ?? GD.Load<PackedScene>(DefaultSkillCastFxScenePath);
        if (scene == null)
        {
            GameLog.Warn(nameof(BattleSkillCastFxComponent), $"Skill cast FX scene missing path={DefaultSkillCastFxScenePath}");
            return;
        }

        Node2D parent = ResolveFxParent();
        if (parent == null)
        {
            return;
        }

        if (scene.Instantiate() is not Node2D fx)
        {
            GameLog.Warn(nameof(BattleSkillCastFxComponent), $"Skill cast FX scene root is not Node2D path={scene.ResourcePath}");
            return;
        }

        parent.AddChild(fx);
        fx.Position = FxOffset;
        if (fx is BattleSkillCastFx skillCastFx)
        {
            skillCastFx.Play(durationSeconds);
        }
    }

    private Node2D ResolveFxParent()
    {
        string value = FxParentPath?.ToString() ?? "";
        if (!string.IsNullOrWhiteSpace(value) && GetNodeOrNull<Node2D>(FxParentPath) is { } parent)
        {
            return parent;
        }

        return Entity;
    }
}
