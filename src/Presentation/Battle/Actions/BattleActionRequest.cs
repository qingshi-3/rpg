using Rpg.Definitions.Battle.Abilities;
using Rpg.Domain.Battle.Grid;
using Rpg.Presentation.Battle.Entities;

namespace Rpg.Presentation.Battle.Actions;

public sealed class BattleActionRequest
{
    private BattleActionRequest(
        BattleActionKind kind,
        BattleEntity actor,
        BattleEntity target,
        GridPosition destination,
        AbilityDefinition ability,
        string reason)
    {
        Kind = kind;
        Actor = actor;
        Target = target;
        Destination = destination;
        Ability = ability;
        Reason = reason ?? "";
    }

    public BattleActionKind Kind { get; }
    public BattleEntity Actor { get; }
    public BattleEntity Target { get; }
    public GridPosition Destination { get; }
    public AbilityDefinition Ability { get; }
    public string Reason { get; }

    public static BattleActionRequest Move(BattleEntity actor, GridPosition destination)
    {
        return new BattleActionRequest(BattleActionKind.Move, actor, null, destination, null, "");
    }

    public static BattleActionRequest Attack(BattleEntity actor, BattleEntity target)
    {
        return new BattleActionRequest(BattleActionKind.Attack, actor, target, default, null, "");
    }

    public static BattleActionRequest UseAbility(BattleEntity actor, BattleEntity target, AbilityDefinition ability)
    {
        return new BattleActionRequest(BattleActionKind.Ability, actor, target, default, ability, "");
    }

    public static BattleActionRequest None(BattleEntity actor, string reason)
    {
        return new BattleActionRequest(BattleActionKind.None, actor, null, default, null, reason);
    }
}
