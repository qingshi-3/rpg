using System.Collections.Generic;
using Rpg.Definitions.Battle.Abilities;
using Rpg.Domain.Battle.Grid;
using Rpg.Presentation.Battle.Abilities;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Battle.Rules;

namespace Rpg.Presentation.Battle.Actions;

public sealed class BattleActionExecutor
{
    public BattleActionResult Execute(BattleActionExecutionContext context, BattleActionRequest request)
    {
        if (request == null)
        {
            return BattleActionResult.Failed(BattleActionKind.None, null, null, default, "行动请求为空");
        }

        return request.Kind switch
        {
            BattleActionKind.Move => ExecuteMove(context, request),
            BattleActionKind.Ability => ExecuteAbility(context, request),
            BattleActionKind.Attack => ExecuteLegacyAttack(context, request),
            _ => BattleActionResult.Failed(request.Kind, request.Actor, request.Target, request.Destination, request.Reason)
        };
    }

    private static BattleActionResult ExecuteMove(
        BattleActionExecutionContext context,
        BattleActionRequest request)
    {
        BattleEntity actor = request.Actor;
        if (context?.GridMap == null || actor == null)
        {
            return BattleActionResult.Failed(request.Kind, actor, null, request.Destination, "移动上下文无效");
        }

        MovementComponent movement = actor.GetComponent<MovementComponent>();
        GridOccupantComponent gridOccupant = actor.GetComponent<GridOccupantComponent>();
        if (movement == null || gridOccupant == null)
        {
            return BattleActionResult.Failed(request.Kind, actor, null, request.Destination, "该单位不能移动");
        }

        if (!movement.CanUseMove())
        {
            return BattleActionResult.Failed(request.Kind, actor, null, request.Destination, "移动次数不足");
        }

        if (!BattleRuleQueries.CanSpendActionPoints(actor, movement.ApCost))
        {
            return BattleActionResult.Failed(request.Kind, actor, null, request.Destination, "行动点不足");
        }

        MovementRangeResult result = MovementRangeFinder.FindReachableCells(
            context.GridMap,
            gridOccupant.SurfacePosition,
            movement.MoveRange,
            BuildBlockedMovementSurfaces(context, actor),
            surface => BattleRuleQueries.CanEnterSurface(actor, surface));

        if (!result.StartCellExists ||
            !result.TryGetBestDestinationSurface(request.Destination, out GridSurfacePosition destinationSurface))
        {
            return BattleActionResult.Failed(request.Kind, actor, null, request.Destination, "不能移动到这里");
        }

        if (!result.TryBuildPathTo(destinationSurface, out IReadOnlyList<GridSurfacePosition> movementPath))
        {
            return BattleActionResult.Failed(request.Kind, actor, null, request.Destination, "移动路径无效");
        }

        if (!TrySpendActionPoints(actor, movement.ApCost))
        {
            return BattleActionResult.Failed(request.Kind, actor, null, request.Destination, "行动点不足");
        }

        if (!movement.TryUseMove())
        {
            return BattleActionResult.Failed(request.Kind, actor, null, request.Destination, "移动次数不足");
        }

        context.MoveEntityTo?.Invoke(actor, movementPath);
        return BattleActionResult.MoveSucceeded(actor, destinationSurface.Position, movementPath, $"{actor.DisplayName} 移动完成");
    }

    private static BattleActionResult ExecuteLegacyAttack(
        BattleActionExecutionContext context,
        BattleActionRequest request)
    {
        AbilityDefinition ability = BattleAbilityQueries.GetPrimaryAbility(request.Actor);
        return ExecuteAbility(context, BattleActionRequest.UseAbility(request.Actor, request.Target, ability));
    }

    private static BattleActionResult ExecuteAbility(
        BattleActionExecutionContext context,
        BattleActionRequest request)
    {
        BattleEntity actor = request.Actor;
        BattleEntity target = request.Target;
        AbilityDefinition ability = request.Ability;

        if (context?.GridMap == null || actor == null || ability == null)
        {
            return BattleActionResult.Failed(request.Kind, actor, target, default, "能力上下文无效");
        }

        GridPosition targetPosition = target?.GetComponent<GridOccupantComponent>()?.Position ?? default;
        if (!BattleAbilityQueries.IsValidTarget(
                context.GridMap,
                context.Entities,
                actor,
                target,
                targetPosition,
                ability,
                context.MarkEntityDefeated,
                out string reason))
        {
            return BattleActionResult.Failed(request.Kind, actor, target, default, reason);
        }

        if (!TrySpendActionPoints(actor, ability.ApCost))
        {
            return BattleActionResult.Failed(request.Kind, actor, target, default, "行动点不足");
        }

        var abilityContext = new AbilityUseContext(
            context.GridMap,
            context.Entities,
            actor,
            target,
            targetPosition,
            ability,
            context.MarkEntityDefeated);
        AbilityEffectResult effectResult = BattleAbilityQueries.ApplyEffects(abilityContext);
        string abilityName = string.IsNullOrWhiteSpace(ability.DisplayName) ? "能力" : ability.DisplayName;
        string message = effectResult.DamageApplied > 0
            ? $"{actor.DisplayName} 使用 {abilityName} 对 {target.DisplayName} 造成 {effectResult.DamageApplied} 点伤害"
            : $"{actor.DisplayName} 使用 {abilityName}";

        return BattleActionResult.AbilitySucceeded(
            actor,
            target,
            ability,
            effectResult.DamageApplied,
            effectResult.TargetDefeated,
            message);
    }

    private static ISet<GridSurfacePosition> BuildBlockedMovementSurfaces(
        BattleActionExecutionContext context,
        BattleEntity movingEntity)
    {
        var blockedSurfaces = new HashSet<GridSurfacePosition>();

        if (context?.Entities == null)
        {
            return blockedSurfaces;
        }

        foreach (BattleEntity entity in context.Entities)
        {
            if (!BattleRuleQueries.TryGetMovementBlockSurface(
                    movingEntity,
                    entity,
                    out GridSurfacePosition blockSurface))
            {
                continue;
            }

            blockedSurfaces.Add(blockSurface);
        }

        return blockedSurfaces;
    }

    private static bool TrySpendActionPoints(BattleEntity entity, int cost)
    {
        if (cost <= 0)
        {
            return true;
        }

        ActionPointComponent actionPoint = entity?.GetComponent<ActionPointComponent>();
        return actionPoint != null && actionPoint.TrySpend(cost);
    }
}
