using Godot;
using Rpg.Definitions.Battle.Abilities;

namespace Rpg.Definitions.Battle;

[GlobalClass]
public partial class BattleUnitDefinition : Resource
{
    [Export]
    public string Id { get; set; } = "";

    [Export]
    public string DisplayName { get; set; } = "战斗单位";

    [Export]
    public BattleUnitVisualDefinition Visual { get; set; }

    [Export]
    public Godot.Collections.Array<AbilityDefinition> Abilities { get; set; } = new();

    [Export]
    public BattleUnitControlMode ControlMode { get; set; } = BattleUnitControlMode.AutoByFaction;

    [Export]
    public Color DebugMarkerColor { get; set; } = new(0.35f, 0.9f, 0.75f, 0.9f);

    [ExportGroup("生命")]

    [Export]
    public int MaxHp { get; set; } = 1;

    [ExportGroup("行动")]

    [Export]
    public int MaxActionPoints { get; set; } = 0;

    [Export]
    public int MoveRange { get; set; } = 4;

    [Export]
    public int MoveActionPointCost { get; set; } = 1;

    [Export]
    public int MaxMoveUsesPerTurn { get; set; } = 1;

    [Export]
    public bool CanEnterWater { get; set; }

    [ExportGroup("攻击")]

    [Export]
    public int AttackDamage { get; set; } = 4;

    [Export]
    public int AttackRange { get; set; } = 1;

    [Export]
    public int AttackActionPointCost { get; set; } = 1;

    [ExportGroup("战棋占位")]

    [Export]
    public bool BlocksMovement { get; set; } = true;

    [Export]
    public bool BlocksLineOfSight { get; set; }

    [Export]
    public bool IsTargetable { get; set; } = true;
}
