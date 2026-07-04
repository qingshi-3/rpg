using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Snapshots;
using Rpg.Application.World;
using Rpg.Domain.Battle.Grid;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Battle.Rules;

namespace Rpg.Presentation.World.Sites;

internal static class BattleRuntimeHeroSkillTargetPresentation
{
    internal static HashSet<string> BuildGroupEntityIds(IEnumerable<BattleForceRequest> forces)
    {
        return BattleRuntimeActorIdentity.BuildRuntimeCorpsActorIdSet(forces);
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
        GridOccupantComponent grid = source?.GetComponent<GridOccupantComponent>();
        if (grid == null || BattleRuleQueries.IsDefeated(source))
        {
            return System.Array.Empty<GridPosition>();
        }

        return BuildRangeCells(source, grid.Position, gridMap, range);
    }

    internal static IReadOnlyList<GridPosition> BuildRangeCells(
        BattleEntity source,
        GridPosition sourceAnchor,
        BattleGridMap gridMap,
        int range)
    {
        var cells = new HashSet<GridPosition>();
        GridOccupantComponent grid = source?.GetComponent<GridOccupantComponent>();
        if (grid == null || BattleRuleQueries.IsDefeated(source))
        {
            return cells.ToArray();
        }

        foreach (GridPosition cell in EnumerateRangeCells(sourceAnchor, grid.FootprintWidth, grid.FootprintHeight, range))
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

    internal static bool IsTargetInRange(BattleEntity source, BattleEntity target, int range)
    {
        GridOccupantComponent sourceGrid = source?.GetComponent<GridOccupantComponent>();
        if (sourceGrid == null)
        {
            return false;
        }

        return IsTargetInRange(source, sourceGrid.Position, target, range);
    }

    internal static bool IsTargetInRange(BattleEntity source, GridPosition sourceAnchor, BattleEntity target, int range)
    {
        GridOccupantComponent sourceGrid = source?.GetComponent<GridOccupantComponent>();
        GridOccupantComponent targetGrid = target?.GetComponent<GridOccupantComponent>();
        if (sourceGrid == null ||
            targetGrid == null ||
            BattleRuleQueries.IsDefeated(source) ||
            BattleRuleQueries.IsDefeated(target))
        {
            return false;
        }

        int normalizedRange = System.Math.Max(0, range);
        int sourceWidth = BattleFootprintCells.NormalizeSize(sourceGrid.FootprintWidth);
        int sourceHeight = BattleFootprintCells.NormalizeSize(sourceGrid.FootprintHeight);
        int targetWidth = BattleFootprintCells.NormalizeSize(targetGrid.FootprintWidth);
        int targetHeight = BattleFootprintCells.NormalizeSize(targetGrid.FootprintHeight);
        int gapX = GetAxisGap(sourceAnchor.X, sourceWidth, targetGrid.GridX, targetWidth);
        int gapY = GetAxisGap(sourceAnchor.Y, sourceHeight, targetGrid.GridY, targetHeight);
        return gapX + gapY <= normalizedRange;
    }

    internal static bool IsCellInRange(BattleEntity source, GridPosition cell, int range)
    {
        GridOccupantComponent sourceGrid = source?.GetComponent<GridOccupantComponent>();
        if (sourceGrid == null)
        {
            return false;
        }

        return IsCellInRange(source, sourceGrid.Position, cell, range);
    }

    internal static bool IsCellInRange(BattleEntity source, GridPosition sourceAnchor, GridPosition cell, int range)
    {
        GridOccupantComponent sourceGrid = source?.GetComponent<GridOccupantComponent>();
        if (sourceGrid == null || BattleRuleQueries.IsDefeated(source))
        {
            return false;
        }

        int normalizedRange = System.Math.Max(0, range);
        int sourceWidth = BattleFootprintCells.NormalizeSize(sourceGrid.FootprintWidth);
        int sourceHeight = BattleFootprintCells.NormalizeSize(sourceGrid.FootprintHeight);
        int gapX = GetAxisGap(sourceAnchor.X, sourceWidth, cell.X, 1);
        int gapY = GetAxisGap(sourceAnchor.Y, sourceHeight, cell.Y, 1);
        return gapX + gapY <= normalizedRange;
    }

    internal static bool TryResolveDirectionalAreaCenter(
        BattleSkillTargetingSnapshot targeting,
        BattleEntity source,
        GridPosition mouseGrid,
        out GridPosition center)
    {
        center = default;
        GridOccupantComponent grid = source?.GetComponent<GridOccupantComponent>();
        if (grid == null || BattleRuleQueries.IsDefeated(source))
        {
            return false;
        }

        return TryResolveDirectionalAreaCenter(targeting, source, grid.Position, mouseGrid, out center);
    }

    internal static bool TryResolveDirectionalAreaCenter(
        BattleSkillTargetingSnapshot targeting,
        BattleEntity source,
        GridPosition sourceAnchor,
        GridPosition mouseGrid,
        out GridPosition center)
    {
        center = default;
        GridOccupantComponent grid = source?.GetComponent<GridOccupantComponent>();
        if (grid == null || BattleRuleQueries.IsDefeated(source))
        {
            return false;
        }

        int width = BattleFootprintCells.NormalizeSize(grid.FootprintWidth);
        int height = BattleFootprintCells.NormalizeSize(grid.FootprintHeight);
        int sourceCenterX2 = sourceAnchor.X * 2 + width - 1;
        int sourceCenterY2 = sourceAnchor.Y * 2 + height - 1;
        int mouseCenterX2 = mouseGrid.X * 2;
        int mouseCenterY2 = mouseGrid.Y * 2;
        int dx = mouseCenterX2 - sourceCenterX2;
        int dy = mouseCenterY2 - sourceCenterY2;

        if (System.Math.Abs(dx) >= System.Math.Abs(dy))
        {
            center = dx >= 0
                ? new GridPosition(sourceAnchor.X + width + 1, sourceAnchor.Y)
                : new GridPosition(sourceAnchor.X - 2, sourceAnchor.Y);
        }
        else
        {
            center = dy >= 0
                ? new GridPosition(sourceAnchor.X, sourceAnchor.Y + height + 1)
                : new GridPosition(sourceAnchor.X, sourceAnchor.Y - 2);
        }

        return true;
    }

    internal static IReadOnlyList<GridPosition> BuildAreaPreviewCells(
        BattleSkillTargetingSnapshot targeting,
        BattleEntity source,
        GridPosition mouseGrid,
        BattleGridMap gridMap)
    {
        return TryResolveDirectionalAreaCenter(targeting, source, mouseGrid, out GridPosition center)
            ? BuildGridRadiusCells(center, ResolveAreaRadius(targeting), gridMap)
            : System.Array.Empty<GridPosition>();
    }

    internal static IReadOnlyList<GridPosition> BuildAreaPreviewCells(
        BattleSkillTargetingSnapshot targeting,
        BattleEntity source,
        GridPosition sourceAnchor,
        GridPosition mouseGrid,
        BattleGridMap gridMap)
    {
        return TryResolveDirectionalAreaCenter(targeting, source, sourceAnchor, mouseGrid, out GridPosition center)
            ? BuildGridRadiusCells(center, ResolveAreaRadius(targeting), gridMap)
            : System.Array.Empty<GridPosition>();
    }

    internal static IReadOnlyList<GridPosition> BuildGridRadiusCells(
        GridPosition center,
        int radius,
        BattleGridMap gridMap)
    {
        int normalizedRadius = System.Math.Max(0, radius);
        var cells = new List<GridPosition>();
        for (int y = center.Y - normalizedRadius; y <= center.Y + normalizedRadius; y++)
        {
            for (int x = center.X - normalizedRadius; x <= center.X + normalizedRadius; x++)
            {
                GridPosition cell = new(x, y);
                if (gridMap == null || gridMap.TryGetCell(cell, out _))
                {
                    cells.Add(cell);
                }
            }
        }

        return cells;
    }

    private static int ResolveAreaRadius(BattleSkillTargetingSnapshot targeting)
    {
        return targeting?.AreaRadius > 0 ? targeting.AreaRadius : 1;
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
        BattleForceRequest[] likelyHeroForces = (forces ?? System.Array.Empty<BattleForceRequest>())
            .Where(IsLikelyHeroForce)
            .ToArray();
        return BattleRuntimeActorIdentity.BuildRuntimeCorpsActorIdSet(likelyHeroForces);
    }

    private static bool IsLikelyHeroForce(BattleForceRequest force)
    {
        return force != null &&
               (FirstSliceHeroCompanyIds.IsHeroUnit(force.UnitDefinitionId) ||
                force.UnitDefinitionId?.Contains("hero", System.StringComparison.OrdinalIgnoreCase) == true ||
                force.SourceKind?.Contains("Hero", System.StringComparison.OrdinalIgnoreCase) == true);
    }

    private static IEnumerable<GridPosition> EnumerateRangeCells(
        GridPosition anchor,
        int footprintWidth,
        int footprintHeight,
        int range)
    {
        int normalizedRange = System.Math.Max(0, range);
        int width = BattleFootprintCells.NormalizeSize(footprintWidth);
        int height = BattleFootprintCells.NormalizeSize(footprintHeight);
        int minX = anchor.X - normalizedRange;
        int maxX = anchor.X + width - 1 + normalizedRange;
        int minY = anchor.Y - normalizedRange;
        int maxY = anchor.Y + height - 1 + normalizedRange;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                int gapX = GetAxisGap(anchor.X, width, x, 1);
                int gapY = GetAxisGap(anchor.Y, height, y, 1);
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
