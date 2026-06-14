using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle;
using Rpg.Domain.Battle.Grid;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Battle.Rules;
using Rpg.Runtime.Battle;

namespace Rpg.Presentation.World.Sites;

internal static class BattleRuntimeThunderFoldTargetingPresentation
{
    internal const int LandingRadius = 3;

    internal static bool TrySelectMark(
        IReadOnlyList<BattleRuntimeSpatialMark> marks,
        string ownerBattleGroupId,
        double runtimeTimeSeconds,
        GridPosition position,
        BattleEntity target,
        out BattleRuntimeSpatialMark mark,
        out GridSurfacePosition surface)
    {
        mark = null;
        surface = default;
        foreach (BattleRuntimeSpatialMark candidate in marks ?? System.Array.Empty<BattleRuntimeSpatialMark>())
        {
            if (!IsLiveOwned(candidate, ownerBattleGroupId, runtimeTimeSeconds))
            {
                continue;
            }

            if (candidate.HasGroundAnchor &&
                candidate.GridX == position.X &&
                candidate.GridY == position.Y)
            {
                mark = candidate;
                surface = new GridSurfacePosition(candidate.GridX, candidate.GridY, candidate.GridHeight);
                return true;
            }

            if (!string.IsNullOrWhiteSpace(candidate.AttachedActorId) &&
                string.Equals(candidate.AttachedActorId, target?.EntityId ?? "", System.StringComparison.Ordinal) &&
                target.GetComponent<GridOccupantComponent>() is GridOccupantComponent grid &&
                !BattleRuleQueries.IsDefeated(target))
            {
                mark = candidate;
                surface = grid.SurfacePosition;
                return true;
            }
        }

        return false;
    }

    internal static IReadOnlyList<GridPosition> BuildLandingCells(
        BattleGridMap gridMap,
        BattleUnitRoot unitRoot,
        BattleEntity source,
        GridSurfacePosition markSurface)
    {
        GridOccupantComponent sourceGrid = source?.GetComponent<GridOccupantComponent>();
        if (gridMap == null || sourceGrid == null || BattleRuleQueries.IsDefeated(source))
        {
            return System.Array.Empty<GridPosition>();
        }

        var cells = new List<GridPosition>();
        for (int y = markSurface.Y - LandingRadius; y <= markSurface.Y + LandingRadius; y++)
        {
            for (int x = markSurface.X - LandingRadius; x <= markSurface.X + LandingRadius; x++)
            {
                var anchor = new GridPosition(x, y);
                if (IsLegalLandingAnchor(gridMap, unitRoot, source, sourceGrid, anchor, markSurface.Height))
                {
                    cells.Add(anchor);
                }
            }
        }

        return cells;
    }

    internal static IReadOnlyList<GridPosition> BuildMarkCells(
        IReadOnlyList<BattleRuntimeSpatialMark> marks,
        string ownerBattleGroupId,
        double runtimeTimeSeconds,
        BattleUnitRoot unitRoot)
    {
        var cells = new HashSet<GridPosition>();
        foreach (BattleRuntimeSpatialMark mark in marks ?? System.Array.Empty<BattleRuntimeSpatialMark>())
        {
            if (!IsLiveOwned(mark, ownerBattleGroupId, runtimeTimeSeconds))
            {
                continue;
            }

            if (mark.HasGroundAnchor)
            {
                cells.Add(new GridPosition(mark.GridX, mark.GridY));
                continue;
            }

            BattleEntity attached = ResolveAttachedEntity(unitRoot, mark.AttachedActorId);
            foreach (GridPosition cell in BattleRuntimeHeroSkillTargetPresentation.BuildFootprintCells(attached))
            {
                cells.Add(cell);
            }
        }

        return cells.ToArray();
    }

    private static bool IsLiveOwned(BattleRuntimeSpatialMark mark, string ownerBattleGroupId, double runtimeTimeSeconds) =>
        mark != null &&
        mark.ExpiresAtSeconds > runtimeTimeSeconds &&
        string.Equals(mark.OwnerBattleGroupId ?? "", ownerBattleGroupId ?? "", System.StringComparison.Ordinal);

    private static BattleEntity ResolveAttachedEntity(BattleUnitRoot unitRoot, string actorId)
    {
        if (unitRoot == null || string.IsNullOrWhiteSpace(actorId))
        {
            return null;
        }

        return unitRoot.GetEntitiesSnapshot()
            .FirstOrDefault(entity =>
                entity != null &&
                !BattleRuleQueries.IsDefeated(entity) &&
                string.Equals(entity.EntityId ?? "", actorId, System.StringComparison.Ordinal));
    }

    private static bool IsLegalLandingAnchor(
        BattleGridMap gridMap,
        BattleUnitRoot unitRoot,
        BattleEntity source,
        GridOccupantComponent sourceGrid,
        GridPosition anchor,
        int height)
    {
        foreach (GridPosition cell in BattleFootprintCells.Enumerate(anchor, sourceGrid.FootprintWidth, sourceGrid.FootprintHeight))
        {
            if (gridMap.TryGetTopSurfacePosition(cell, out GridSurfacePosition topSurface) != true ||
                topSurface.Height != height ||
                gridMap.TryGetSurface(topSurface, out GridCellSurface surface) != true ||
                surface.IsWalkable != true ||
                surface.MoveCost <= 0 ||
                BattleRuleQueries.CanEnterSurface(source, surface) != true ||
                unitRoot?.FindEntityAt(cell) != null)
            {
                return false;
            }
        }

        return true;
    }
}
