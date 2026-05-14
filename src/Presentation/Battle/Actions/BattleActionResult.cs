using System.Collections.Generic;
using System.Linq;
using Rpg.Definitions.Battle.Abilities;
using Rpg.Domain.Battle.Grid;
using Rpg.Presentation.Battle.Entities;

namespace Rpg.Presentation.Battle.Actions;

public sealed class BattleDamageEvent
{
    public BattleDamageEvent(BattleEntity target, int damageApplied, bool targetDefeated)
        : this(target, target?.EntityId ?? "", damageApplied, targetDefeated)
    {
    }

    public BattleDamageEvent(BattleEntity target, string targetId, int damageApplied, bool targetDefeated)
    {
        Target = target;
        TargetId = targetId ?? "";
        DamageApplied = System.Math.Max(0, damageApplied);
        TargetDefeated = targetDefeated;
    }

    public BattleEntity Target { get; }
    public string TargetId { get; }
    public int DamageApplied { get; }
    public bool TargetDefeated { get; }
}

public sealed class BattleActionResult
{
    private BattleActionResult(
        bool success,
        BattleActionKind kind,
        BattleEntity actor,
        BattleEntity target,
        AbilityDefinition ability,
        GridPosition destination,
        IReadOnlyList<GridSurfacePosition> movementPath,
        string message,
        int damageApplied,
        bool targetDefeated,
        IReadOnlyList<BattleDamageEvent> damageEvents)
    {
        Success = success;
        Kind = kind;
        Actor = actor;
        Target = target;
        Ability = ability;
        Destination = destination;
        MovementPath = movementPath ?? System.Array.Empty<GridSurfacePosition>();
        Message = message ?? "";
        DamageApplied = damageApplied;
        TargetDefeated = targetDefeated;
        DamageEvents = damageEvents?.ToArray() ??
                       BuildLegacyDamageEvents(target, damageApplied, targetDefeated);
    }

    public bool Success { get; }
    public BattleActionKind Kind { get; }
    public BattleEntity Actor { get; }
    public BattleEntity Target { get; }
    public AbilityDefinition Ability { get; }
    public GridPosition Destination { get; }
    public IReadOnlyList<GridSurfacePosition> MovementPath { get; }
    public int MovementStepCount => System.Math.Max(0, MovementPath.Count - 1);
    public string Message { get; }
    public int DamageApplied { get; }
    public bool TargetDefeated { get; }
    // Presentation reads this list so single-target and future multi-target hits share one feedback path.
    public IReadOnlyList<BattleDamageEvent> DamageEvents { get; }

    public static BattleActionResult MoveSucceeded(
        BattleEntity actor,
        GridPosition destination,
        IReadOnlyList<GridSurfacePosition> movementPath,
        string message)
    {
        return new BattleActionResult(
            true,
            BattleActionKind.Move,
            actor,
            null,
            null,
            destination,
            movementPath?.ToArray() ?? System.Array.Empty<GridSurfacePosition>(),
            message,
            0,
            false,
            System.Array.Empty<BattleDamageEvent>());
    }

    public static BattleActionResult AttackSucceeded(
        BattleEntity actor,
        BattleEntity target,
        int damageApplied,
        bool targetDefeated,
        string message)
    {
        return new BattleActionResult(true, BattleActionKind.Attack, actor, target, null, default, null, message, damageApplied, targetDefeated, null);
    }

    public static BattleActionResult AbilitySucceeded(
        BattleEntity actor,
        BattleEntity target,
        AbilityDefinition ability,
        int damageApplied,
        bool targetDefeated,
        string message)
    {
        return new BattleActionResult(true, BattleActionKind.Ability, actor, target, ability, default, null, message, damageApplied, targetDefeated, null);
    }

    public static BattleActionResult AbilitySucceeded(
        BattleEntity actor,
        BattleEntity target,
        AbilityDefinition ability,
        IReadOnlyList<BattleDamageEvent> damageEvents,
        string message)
    {
        int totalDamage = damageEvents?.Sum(damage => damage?.DamageApplied ?? 0) ?? 0;
        bool targetDefeated = damageEvents?.Any(damage => damage?.TargetDefeated == true) == true;
        return new BattleActionResult(true, BattleActionKind.Ability, actor, target, ability, default, null, message, totalDamage, targetDefeated, damageEvents);
    }

    public static BattleActionResult Failed(
        BattleActionKind kind,
        BattleEntity actor,
        BattleEntity target,
        GridPosition destination,
        string message)
    {
        return new BattleActionResult(false, kind, actor, target, null, destination, null, message, 0, false, System.Array.Empty<BattleDamageEvent>());
    }

    private static IReadOnlyList<BattleDamageEvent> BuildLegacyDamageEvents(
        BattleEntity target,
        int damageApplied,
        bool targetDefeated)
    {
        return target == null
            ? System.Array.Empty<BattleDamageEvent>()
            : new[] { new BattleDamageEvent(target, damageApplied, targetDefeated) };
    }
}
