using Godot;
using Rpg.Infrastructure.Logging;

namespace Rpg.Presentation.Battle.Entities;

public partial class BattleSkillImpactFxComponent : BattleEntityComponent
{
    private const string DefaultSkillImpactFxScenePath = "res://scenes/battle/entities/fx/BattleSkillImpactFx.tscn";

    [Export]
    public PackedScene SkillImpactFxScene { get; set; }

    [Export]
    public NodePath FxParentPath { get; set; } = new("../VisualRoot");

    [Export]
    public Vector2 FxOffset { get; set; } = new(0f, -18f);

    public void PlaySkillImpactFx()
    {
        PackedScene scene = SkillImpactFxScene ?? GD.Load<PackedScene>(DefaultSkillImpactFxScenePath);
        if (scene == null)
        {
            GameLog.Warn(nameof(BattleSkillImpactFxComponent), $"Skill impact FX scene missing path={DefaultSkillImpactFxScenePath}");
            return;
        }

        Node2D parent = ResolveFxParent();
        if (parent == null)
        {
            return;
        }

        if (scene.Instantiate() is not Node2D fx)
        {
            GameLog.Warn(nameof(BattleSkillImpactFxComponent), $"Skill impact FX scene root is not Node2D path={scene.ResourcePath}");
            return;
        }

        parent.AddChild(fx);
        fx.Position = FxOffset;
        if (fx is BattleSkillImpactFx skillImpactFx)
        {
            skillImpactFx.Play();
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
