using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle;
using Rpg.Domain.Battle.Grid;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Battle;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Battle.Rules;

namespace Rpg.Presentation.World.Sites;

public partial class WorldSiteRoot
{
    private bool _battlePerceptionOverlayVisible;

    private void EnableBattlePerceptionOverlayForRuntime()
    {
        // Local sensing is visible by default during runtime tuning; E remains
        // the escape hatch when the overlay obscures combat readability.
        _battlePerceptionOverlayVisible = true;
        RefreshBattlePerceptionOverlay();
    }

    private bool TryHandleBattlePerceptionOverlayInput(InputEvent @event)
    {
        if (!_battleRuntimeEnabled ||
            @event is not InputEventKey { Pressed: true, Echo: false } keyEvent ||
            keyEvent.Keycode != Key.E)
        {
            return false;
        }

        _battlePerceptionOverlayVisible = !_battlePerceptionOverlayVisible;
        RefreshBattlePerceptionOverlay();
        GameLog.Info(
            nameof(WorldSiteRoot),
            $"BattlePerceptionOverlayToggled visible={_battlePerceptionOverlayVisible} range={BattlePerceptionPolicy.DefaultLocalPerceptionRange}");
        GetViewport()?.SetInputAsHandled();
        return true;
    }

    private void RefreshBattlePerceptionOverlay()
    {
        if (_highlightOverlay == null)
        {
            return;
        }

        if (!_battleRuntimeEnabled || !_battlePerceptionOverlayVisible)
        {
            ClearBattlePerceptionOverlay();
            return;
        }

        _highlightOverlay.SetCellsBatch(
            (BattleGridHighlightKind.FriendlyPerception, BuildBattlePerceptionCells(BattleFaction.Player)),
            (BattleGridHighlightKind.EnemyPerception, BuildBattlePerceptionCells(BattleFaction.Enemy)));
    }

    private void ClearBattlePerceptionOverlay()
    {
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.FriendlyPerception);
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.EnemyPerception);
    }

    private IReadOnlyList<GridPosition> BuildBattlePerceptionCells(BattleFaction faction)
    {
        var cells = new HashSet<GridPosition>();
        if (_unitRoot == null)
        {
            return cells.ToArray();
        }

        foreach (BattleEntity entity in _unitRoot.GetEntitiesSnapshot())
        {
            if (entity == null ||
                !GodotObject.IsInstanceValid(entity) ||
                BattleRuleQueries.IsDefeated(entity) ||
                entity.GetComponent<FactionComponent>()?.Faction != faction)
            {
                continue;
            }

            GridOccupantComponent grid = entity.GetComponent<GridOccupantComponent>();
            if (grid == null)
            {
                continue;
            }

            foreach (GridPosition cell in EnumerateBattlePerceptionCells(grid))
            {
                if (CanShowBattlePerceptionCell(cell))
                {
                    cells.Add(cell);
                }
            }
        }

        return cells.ToArray();
    }

    private static IEnumerable<GridPosition> EnumerateBattlePerceptionCells(GridOccupantComponent grid)
    {
        int range = BattlePerceptionPolicy.DefaultLocalPerceptionRange;
        int width = BattleFootprintCells.NormalizeSize(grid?.FootprintWidth ?? 1);
        int height = BattleFootprintCells.NormalizeSize(grid?.FootprintHeight ?? 1);
        int minX = (grid?.GridX ?? 0) - range;
        int maxX = (grid?.GridX ?? 0) + width - 1 + range;
        int minY = (grid?.GridY ?? 0) - range;
        int maxY = (grid?.GridY ?? 0) + height - 1 + range;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                if (GetAxisGap(grid.GridX, width, x, 1) <= range &&
                    GetAxisGap(grid.GridY, height, y, 1) <= range)
                {
                    yield return new GridPosition(x, y);
                }
            }
        }
    }

    private bool CanShowBattlePerceptionCell(GridPosition cell)
    {
        return _activeGridMap == null ||
               _activeGridMap.TryGetTopSurface(cell, out GridCellSurface surface) &&
               surface?.HasFoundation == true;
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
