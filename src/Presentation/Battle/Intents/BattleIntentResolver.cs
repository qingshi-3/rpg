using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Rpg.Definitions.Battle.Abilities;
using Rpg.Domain.Battle.Grid;
using Rpg.Presentation.Battle.Abilities;
using Rpg.Presentation.Battle.Actions;
using Rpg.Presentation.Battle.AI;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Battle.Rules;

namespace Rpg.Presentation.Battle.Intents;

public sealed class BattleIntentResolver
{
    private const int ApproachSearchMaxCost = 100000;

    public BattleIntentPreview Preview(BattleAiContext context, BattleIntent intent)
    {
        return BuildPreview(context, intent);
    }

    public BattleActionRequest Resolve(BattleAiContext context, BattleIntent intent)
    {
        return BuildPreview(context, intent).Request;
    }

    private static BattleIntentPreview BuildPreview(BattleAiContext context, BattleIntent intent)
    {
        BattleEntity actor = intent?.Actor;
        if (context?.GridMap == null || actor == null || BattleRuleQueries.IsDefeated(actor))
        {
            return CreateNone(intent, actor, "敌方意图上下文无效");
        }

        if (!intent.CanResolveAction)
        {
            string reason = string.IsNullOrWhiteSpace(intent.Reason) ? "暂不行动" : intent.Reason;
            return CreateNone(intent, actor, $"{actor.DisplayName}：{reason}");
        }

        if (!TryChooseTarget(context, actor, intent.TargetPolicy, out BattleEntity target))
        {
            return CreateNone(intent, actor, $"{actor.DisplayName} 暂无可用目标");
        }

        AbilityDefinition ability = intent.PreferredAbility ?? BattleAbilityQueries.GetPrimaryAbility(actor);
        if (ability != null &&
            TryBuildAbilityPreview(context, intent, actor, target, ability, out BattleIntentPreview abilityPreview))
        {
            return abilityPreview;
        }

        string moveRejectReason = "";
        if (intent.Type != BattleIntentType.Guard &&
            TryBuildApproachPreview(context, intent, actor, target, ability, out BattleIntentPreview movePreview, out moveRejectReason))
        {
            return movePreview;
        }

        string reasonText = string.IsNullOrWhiteSpace(moveRejectReason) ? "当前无法执行" : moveRejectReason;
        return CreateNone(intent, actor, $"{actor.DisplayName} 的{intent.DisplayName}无法执行：{reasonText}");
    }

    private static bool TryBuildAbilityPreview(
        BattleAiContext context,
        BattleIntent intent,
        BattleEntity actor,
        BattleEntity target,
        AbilityDefinition ability,
        out BattleIntentPreview preview)
    {
        preview = null;
        GridOccupantComponent targetGrid = target.GetComponent<GridOccupantComponent>();
        if (targetGrid == null ||
            !BattleAbilityQueries.IsValidTarget(
                context.GridMap,
                context.Entities,
                actor,
                target,
                targetGrid.Position,
                ability,
                null,
                out _))
        {
            return false;
        }

        BattleActionRequest request = BattleActionRequest.UseAbility(actor, target, ability);
        GridPosition[] affectedCells = { targetGrid.Position };
        string abilityName = GetAbilityName(ability);
        int damage = GetDamageValue(ability);
        string damageText = damage > 0
            ? $"，预计造成 {damage.ToString(CultureInfo.InvariantCulture)} 点伤害"
            : "";
        string detail = $"{actor.DisplayName}：{intent.DisplayName}。若现在结束回合，将对 {target.DisplayName} 使用 {abilityName}{damageText}。";
        preview = new BattleIntentPreview(
            intent,
            request,
            System.Array.Empty<GridSurfacePosition>(),
            affectedCells,
            detail);
        return true;
    }

    private static bool TryBuildApproachPreview(
        BattleAiContext context,
        BattleIntent intent,
        BattleEntity actor,
        BattleEntity target,
        AbilityDefinition ability,
        out BattleIntentPreview preview,
        out string rejectReason)
    {
        preview = null;
        rejectReason = "";

        MovementComponent movement = actor.GetComponent<MovementComponent>();
        GridOccupantComponent actorGrid = actor.GetComponent<GridOccupantComponent>();
        GridOccupantComponent targetGrid = target.GetComponent<GridOccupantComponent>();
        if (movement == null ||
            actorGrid == null ||
            targetGrid == null ||
            movement.MoveRange <= 0 ||
            !movement.CanUseMove() ||
            !BattleRuleQueries.CanSpendActionPoints(actor, movement.ApCost))
        {
            rejectReason = "移动能力、移动次数或行动点不足";
            return false;
        }

        ISet<GridSurfacePosition> blockedSurfaces = BuildBlockedMovementSurfaces(context, actor);
        MovementRangeResult result = MovementRangeFinder.FindReachableCells(
            context.GridMap,
            actorGrid.SurfacePosition,
            movement.MoveRange,
            blockedSurfaces,
            surface => BattleRuleQueries.CanEnterSurface(actor, surface));

        if (!result.StartCellExists || result.DestinationSurfaces.Count == 0)
        {
            rejectReason = result.StartCellExists ? "没有可移动目标格" : "当前位置不在可走地图面中";
            return false;
        }

        int desiredRange = System.Math.Max(ability?.Range ?? 1, 1);
        HashSet<GridSurfacePosition> attackSurfaces = BuildAttackSurfaces(
            context,
            actor,
            targetGrid.SurfacePosition,
            desiredRange,
            blockedSurfaces);
        if (attackSurfaces.Count == 0)
        {
            rejectReason = "没有可用接近位置";
            return false;
        }

        int currentApproachCost = FindNearestApproachCost(
            context,
            actor,
            actorGrid.SurfacePosition,
            attackSurfaces,
            blockedSurfaces);
        int currentDistance = BattleRuleQueries.GetManhattanDistance(actorGrid.Position, targetGrid.Position);

        GridSurfacePosition bestDestination = result.DestinationSurfaces
            .OrderBy(surface => FindNearestApproachCost(context, actor, surface, attackSurfaces, blockedSurfaces))
            .ThenBy(surface => BattleRuleQueries.GetManhattanDistance(surface.Position, targetGrid.Position))
            .ThenBy(surface => result.ReachableSurfaceCosts.TryGetValue(surface, out int cost) ? cost : int.MaxValue)
            .FirstOrDefault();

        int bestApproachCost = FindNearestApproachCost(
            context,
            actor,
            bestDestination,
            attackSurfaces,
            blockedSurfaces);
        int bestDistance = BattleRuleQueries.GetManhattanDistance(bestDestination.Position, targetGrid.Position);

        if (bestApproachCost == int.MaxValue)
        {
            rejectReason = "没有通往攻击位置的路径";
            return false;
        }

        if (bestApproachCost > currentApproachCost ||
            bestApproachCost == currentApproachCost && bestDistance >= currentDistance)
        {
            rejectReason = "可达格不能更接近目标";
            return false;
        }

        if (!result.TryBuildPathTo(bestDestination, out IReadOnlyList<GridSurfacePosition> path))
        {
            rejectReason = "无法构建移动路径";
            return false;
        }

        BattleActionRequest request = BattleActionRequest.Move(actor, bestDestination.Position);
        GridPosition[] affectedCells = { bestDestination.Position };
        int steps = System.Math.Max(0, path.Count - 1);
        string detail = $"{actor.DisplayName}：{intent.DisplayName}。若现在结束回合，将向 {target.DisplayName} 靠近，移动 {steps.ToString(CultureInfo.InvariantCulture)} 格到 {bestDestination.Position}。";
        preview = new BattleIntentPreview(intent, request, path, affectedCells, detail);
        return true;
    }

    private static bool TryChooseTarget(
        BattleAiContext context,
        BattleEntity actor,
        BattleIntentTargetPolicy policy,
        out BattleEntity target)
    {
        target = null;
        GridOccupantComponent actorGrid = actor.GetComponent<GridOccupantComponent>();
        if (actorGrid == null)
        {
            return false;
        }

        IEnumerable<BattleEntity> candidates = context.Entities
            .Where(entity => !BattleRuleQueries.IsDefeated(entity) && BattleRuleQueries.AreHostile(actor, entity))
            .Where(entity => entity.GetComponent<GridOccupantComponent>() != null);

        target = policy switch
        {
            BattleIntentTargetPolicy.LowestHealthHostile => candidates
                .OrderBy(entity => entity.GetComponent<HealthComponent>()?.Hp ?? int.MaxValue)
                .ThenBy(entity => GetDistance(actorGrid, entity))
                .FirstOrDefault(),
            BattleIntentTargetPolicy.NearestHostile => candidates
                .OrderBy(entity => GetDistance(actorGrid, entity))
                .FirstOrDefault(),
            _ => null
        };

        return target != null;
    }

    private static int GetDistance(GridOccupantComponent actorGrid, BattleEntity entity)
    {
        GridOccupantComponent entityGrid = entity.GetComponent<GridOccupantComponent>();
        return entityGrid == null
            ? int.MaxValue
            : BattleRuleQueries.GetManhattanDistance(actorGrid.Position, entityGrid.Position);
    }

    private static HashSet<GridSurfacePosition> BuildAttackSurfaces(
        BattleAiContext context,
        BattleEntity actor,
        GridSurfacePosition targetSurface,
        int attackRange,
        ISet<GridSurfacePosition> blockedSurfaces)
    {
        return context.GridMap.Surfaces.Values
            .Where(surface => surface.SurfacePosition != targetSurface)
            .Where(surface => !blockedSurfaces.Contains(surface.SurfacePosition))
            .Where(surface => context.GridMap.IsTopSurface(surface.SurfacePosition))
            .Where(surface => surface.IsWalkable && surface.MoveCost > 0)
            .Where(surface => BattleRuleQueries.CanEnterSurface(actor, surface))
            .Where(surface => BattleRuleQueries.GetManhattanDistance(surface.Position, targetSurface.Position) <= attackRange)
            .Select(surface => surface.SurfacePosition)
            .ToHashSet();
    }

    private static int FindNearestApproachCost(
        BattleAiContext context,
        BattleEntity actor,
        GridSurfacePosition start,
        ISet<GridSurfacePosition> attackSurfaces,
        ISet<GridSurfacePosition> blockedSurfaces)
    {
        if (attackSurfaces.Contains(start))
        {
            return 0;
        }

        MovementRangeResult reachable = MovementRangeFinder.FindReachableCells(
            context.GridMap,
            start,
            ApproachSearchMaxCost,
            blockedSurfaces,
            surface => BattleRuleQueries.CanEnterSurface(actor, surface));

        int bestCost = int.MaxValue;
        foreach ((GridSurfacePosition surface, int cost) in reachable.ReachableSurfaceCosts)
        {
            if (attackSurfaces.Contains(surface) && cost < bestCost)
            {
                bestCost = cost;
            }
        }

        return bestCost;
    }

    private static ISet<GridSurfacePosition> BuildBlockedMovementSurfaces(
        BattleAiContext context,
        BattleEntity movingEntity)
    {
        var blockedSurfaces = new HashSet<GridSurfacePosition>();
        if (context?.Entities == null)
        {
            return blockedSurfaces;
        }

        foreach (BattleEntity entity in context.Entities)
        {
            if (entity == movingEntity || BattleRuleQueries.IsDefeated(entity))
            {
                continue;
            }

            GridOccupantComponent gridOccupant = entity.GetComponent<GridOccupantComponent>();
            if (gridOccupant is { BlocksMovement: true })
            {
                blockedSurfaces.Add(gridOccupant.SurfacePosition);
            }
        }

        return blockedSurfaces;
    }

    private static BattleIntentPreview CreateNone(BattleIntent intent, BattleEntity actor, string reason)
    {
        return new BattleIntentPreview(
            intent,
            BattleActionRequest.None(actor, reason),
            System.Array.Empty<GridSurfacePosition>(),
            System.Array.Empty<GridPosition>(),
            reason);
    }

    private static int GetDamageValue(AbilityDefinition ability)
    {
        if (ability?.Effects == null)
        {
            return 0;
        }

        return ability.Effects
            .OfType<DamageAbilityEffect>()
            .Sum(effect => System.Math.Max(0, effect.Damage));
    }

    private static string GetAbilityName(AbilityDefinition ability)
    {
        return string.IsNullOrWhiteSpace(ability?.DisplayName) ? "能力" : ability.DisplayName;
    }

}
