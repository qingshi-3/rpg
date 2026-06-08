using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle;
using Rpg.Application.World;
using Rpg.Domain.Battle.Grid;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Battle.Rules;

namespace Rpg.Presentation.World.Sites;

internal static class BattleRuntimeHeroSkillTargetPresentation
{
    internal static HashSet<string> BuildGroupEntityIds(IEnumerable<BattleForceRequest> forces)
    {
        return (forces ?? System.Array.Empty<BattleForceRequest>())
            .Where(force => !string.IsNullOrWhiteSpace(force?.ForceId))
            .SelectMany(force => Enumerable.Range(1, System.Math.Max(0, force.Count))
                .Select(index => $"{force.ForceId}:{index}"))
            .ToHashSet(System.StringComparer.Ordinal);
    }

    internal static BattleEntity ResolveSourceEntity(
        BattleUnitRoot unitRoot,
        IEnumerable<BattleForceRequest> forces)
    {
        HashSet<string> entityIds = BuildGroupEntityIds(forces);
        HashSet<string> preferredEntityIds = BuildPreferredSourceEntityIds(forces);
        return unitRoot?.GetEntitiesSnapshot()
            .Where(entity => entity != null &&
                             entityIds.Contains(entity.EntityId ?? "") &&
                             !BattleRuleQueries.IsDefeated(entity))
            .OrderBy(entity => preferredEntityIds.Contains(entity.EntityId ?? "") ? 0 : 1)
            .ThenBy(entity => entity.EntityId, System.StringComparer.Ordinal)
            .FirstOrDefault();
    }

    internal static bool TryResolveSourceActorId(BattleEntity source, out string sourceActorId)
    {
        sourceActorId = "";
        if (source == null ||
            BattleRuleQueries.IsDefeated(source) ||
            source.GetComponent<GridOccupantComponent>() == null ||
            string.IsNullOrWhiteSpace(source.EntityId))
        {
            return false;
        }

        sourceActorId = source.EntityId;
        return true;
    }

    internal static bool TryResolveTargetActorId(
        BattleEntity source,
        BattleEntity target,
        out string targetActorId)
    {
        targetActorId = "";
        if (source == null ||
            target == null ||
            BattleRuleQueries.IsDefeated(target) ||
            target.GetComponent<TargetableComponent>() is { IsTargetable: false } ||
            target.GetComponent<GridOccupantComponent>() == null ||
            !BattleRuleQueries.AreHostile(source, target))
        {
            return false;
        }

        return TryResolveCorpsActorId(target, out targetActorId);
    }

    internal static IReadOnlyList<GridPosition> BuildRangeCells(
        BattleUnitRoot unitRoot,
        IEnumerable<BattleForceRequest> forces,
        BattleGridMap gridMap,
        int range)
    {
        return BuildRangeCells(ResolveSourceEntity(unitRoot, forces), gridMap, range);
    }

    internal static IReadOnlyList<GridPosition> BuildRangeCells(
        BattleEntity source,
        BattleGridMap gridMap,
        int range)
    {
        var cells = new HashSet<GridPosition>();
        GridOccupantComponent grid = source?.GetComponent<GridOccupantComponent>();
        if (grid == null || BattleRuleQueries.IsDefeated(source))
        {
            return cells.ToArray();
        }

        foreach (GridPosition cell in EnumerateRangeCells(grid, range))
        {
            if (gridMap == null || gridMap.TryGetCell(cell, out _))
            {
                cells.Add(cell);
            }
        }

        return cells.ToArray();
    }

    internal static IReadOnlyList<GridPosition> BuildFootprintCells(BattleEntity entity)
    {
        GridOccupantComponent grid = entity?.GetComponent<GridOccupantComponent>();
        return grid == null
            ? System.Array.Empty<GridPosition>()
            : BattleFootprintCells.Enumerate(grid.Position, grid.FootprintWidth, grid.FootprintHeight);
    }

    private static bool TryResolveCorpsActorId(BattleEntity entity, out string actorId)
    {
        actorId = "";
        string entityId = entity?.EntityId ?? "";
        int separatorIndex = entityId.LastIndexOf(':');
        if (separatorIndex <= 0 ||
            separatorIndex >= entityId.Length - 1 ||
            !int.TryParse(entityId[(separatorIndex + 1)..], out int oneBasedIndex) ||
            oneBasedIndex <= 0)
        {
            return false;
        }

        // Runtime corps actor ids share the presentation entity id format; target picking
        // must not synthesize probe-only group ids or Runtime validation will reject it.
        actorId = entityId;
        return true;
    }

    private static HashSet<string> BuildPreferredSourceEntityIds(IEnumerable<BattleForceRequest> forces)
    {
        return (forces ?? System.Array.Empty<BattleForceRequest>())
            .Where(IsLikelyHeroForce)
            .Where(force => !string.IsNullOrWhiteSpace(force.ForceId))
            .SelectMany(force => Enumerable.Range(1, System.Math.Max(0, force.Count))
                .Select(index => $"{force.ForceId}:{index}"))
            .ToHashSet(System.StringComparer.Ordinal);
    }

    private static bool IsLikelyHeroForce(BattleForceRequest force)
    {
        return force != null &&
               (FirstSliceHeroCompanyIds.IsHeroUnit(force.UnitDefinitionId) ||
                force.UnitDefinitionId?.Contains("hero", System.StringComparison.OrdinalIgnoreCase) == true ||
                force.SourceKind?.Contains("Hero", System.StringComparison.OrdinalIgnoreCase) == true);
    }

    private static IEnumerable<GridPosition> EnumerateRangeCells(GridOccupantComponent grid, int range)
    {
        int normalizedRange = System.Math.Max(0, range);
        int width = BattleFootprintCells.NormalizeSize(grid?.FootprintWidth ?? 1);
        int height = BattleFootprintCells.NormalizeSize(grid?.FootprintHeight ?? 1);
        int minX = (grid?.GridX ?? 0) - normalizedRange;
        int maxX = (grid?.GridX ?? 0) + width - 1 + normalizedRange;
        int minY = (grid?.GridY ?? 0) - normalizedRange;
        int maxY = (grid?.GridY ?? 0) + height - 1 + normalizedRange;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                int gapX = GetAxisGap(grid.GridX, width, x, 1);
                int gapY = GetAxisGap(grid.GridY, height, y, 1);
                if (gapX + gapY <= normalizedRange)
                {
                    yield return new GridPosition(x, y);
                }
            }
        }
    }

    private static int GetAxisGap(int firstStart, int firstSize, int secondStart, int secondSize)
    {
        int firstEnd = firstStart + BattleFootprintCells.NormalizeSize(firstSize) - 1;
        int secondEnd = secondStart + BattleFootprintCells.NormalizeSize(secondSize) - 1;
        if (firstStart > secondEnd)
        {
            return firstStart - secondEnd;
        }

        return secondStart > firstEnd ? secondStart - firstEnd : 0;
    }
}
