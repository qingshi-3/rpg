using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle;
using Rpg.Definitions.World;
using Rpg.Domain.Battle.Grid;
using Rpg.Domain.World;

namespace Rpg.Application.World;

public static class WorldSiteExplorationService
{
    private const int ExplorationPathMoveBudget = 10000;

    public static bool TryMoveParty(
        WorldSiteExplorationState exploration,
        BattleGridMap gridMap,
        GridPosition destination,
        out IReadOnlyList<GridSurfacePosition> path,
        out string failureReason)
    {
        path = Array.Empty<GridSurfacePosition>();
        failureReason = "";
        if (!TryBuildPath(exploration, null, gridMap, destination, out path, out failureReason))
        {
            return false;
        }

        GridSurfacePosition final = path[^1];
        exploration.CurrentCellX = final.X;
        exploration.CurrentCellY = final.Y;
        exploration.CurrentCellHeight = final.Height;
        MarkVisited(exploration, final);
        return true;
    }

    public static bool TrySetPartyMoveIntent(
        WorldSiteExplorationState exploration,
        WorldSiteDefinition siteDefinition,
        BattleGridMap gridMap,
        GridPosition destination,
        out IReadOnlyList<GridSurfacePosition> path,
        out string failureReason)
    {
        path = Array.Empty<GridSurfacePosition>();
        failureReason = "";
        if (!TryBuildPath(exploration, siteDefinition, gridMap, destination, out path, out failureReason))
        {
            return false;
        }

        exploration.PendingPathCellKeys.Clear();
        foreach (GridSurfacePosition cell in path.Skip(1))
        {
            exploration.PendingPathCellKeys.Add(ToCellKey(cell));
        }

        exploration.IsSimulationPaused = exploration.PendingPathCellKeys.Count == 0;
        exploration.PauseReason = exploration.IsSimulationPaused ? "exploration_arrived" : "";
        exploration.ActiveAlertPatrolId = "";
        return true;
    }

    public static bool TryBuildPartyPath(
        WorldSiteExplorationState exploration,
        WorldSiteDefinition siteDefinition,
        BattleGridMap gridMap,
        GridPosition destination,
        out IReadOnlyList<GridSurfacePosition> path,
        out string failureReason)
    {
        return TryBuildPath(exploration, siteDefinition, gridMap, destination, out path, out failureReason);
    }

    public static bool TrySetPartyMoveIntent(
        WorldSiteExplorationState exploration,
        BattleGridMap gridMap,
        GridPosition destination,
        out IReadOnlyList<GridSurfacePosition> path,
        out string failureReason)
    {
        return TrySetPartyMoveIntent(exploration, null, gridMap, destination, out path, out failureReason);
    }

    public static SiteExplorationTickResult AdvanceTick(
        WorldSiteExplorationState exploration,
        WorldSiteDefinition siteDefinition,
        BattleGridMap gridMap,
        int partyActionPointRegenPerTick = 1,
        int partyMoveCostPerCell = 1,
        bool waitAction = false)
    {
        SiteExplorationTickResult result = new();
        if (exploration == null || siteDefinition == null || gridMap == null)
        {
            result.Paused = true;
            result.PauseReason = "exploration_missing";
            return result;
        }

        if (exploration.IsSimulationPaused)
        {
            result.Paused = true;
            result.PauseReason = exploration.PauseReason;
            result.AlertPatrolId = exploration.ActiveAlertPatrolId;
            return result;
        }

        EnsurePatrolStates(exploration, siteDefinition);
        exploration.PartyActionPoints += Math.Max(0, partyActionPointRegenPerTick);

        GridSurfacePosition? reservedPartyDestination = null;
        if (exploration.PendingPathCellKeys.Count > 0 && exploration.PartyActionPoints >= Math.Max(1, partyMoveCostPerCell))
        {
            exploration.PartyActionPoints -= Math.Max(1, partyMoveCostPerCell);
            if (!TryParseCellKey(exploration.PendingPathCellKeys[0], out GridSurfacePosition next) ||
                !IsValidWalkableTopSurface(gridMap, next))
            {
                Pause(exploration, result, "exploration_path_invalid", "");
                return result;
            }

            GridSurfacePosition previous = new(exploration.CurrentCellX, exploration.CurrentCellY, exploration.CurrentCellHeight);
            exploration.CurrentCellX = next.X;
            exploration.CurrentCellY = next.Y;
            exploration.CurrentCellHeight = next.Height;
            exploration.PendingPathCellKeys.RemoveAt(0);
            MarkVisited(exploration, next);
            // Player action has priority in discrete realtime: patrol AI treats the player's next cell as occupied for this tick.
            reservedPartyDestination = next;
            result.PartyMoved = true;
            result.PartyPathStep.Add(previous);
            result.PartyPathStep.Add(next);
        }

        if (!result.PartyMoved && !waitAction)
        {
            return result;
        }

        foreach (SiteExplorationPatrolState patrol in exploration.PatrolUnits)
        {
            if (patrol == null || patrol.IsRemoved)
            {
                continue;
            }

            SiteExplorationPatrolDefinition patrolDefinition = siteDefinition.ExplorationPatrols.FirstOrDefault(definition => definition.Id == patrol.PatrolId);
            if (patrolDefinition == null)
            {
                Pause(exploration, result, "exploration_patrol_definition_missing", patrol.PatrolId);
                return result;
            }

            if (patrolDefinition.RouteCells.Count < 2)
            {
                continue;
            }

            patrol.ActionPoints += Math.Max(0, patrolDefinition.ActionPointRegenPerTick);
            int moveCost = Math.Max(1, patrolDefinition.MoveCostPerCell);
            if (patrol.ActionPoints < moveCost)
            {
                continue;
            }

            int nextIndex = (patrol.RouteIndex + 1) % patrolDefinition.RouteCells.Count;
            GridSurfacePosition next = ToSurface(patrolDefinition.RouteCells[nextIndex]);
            if (!CanPatrolEnterCell(gridMap, next, reservedPartyDestination))
            {
                continue;
            }

            GridSurfacePosition previous = new(patrol.CellX, patrol.CellY, patrol.CellHeight);
            patrol.ActionPoints -= moveCost;
            patrol.CellX = next.X;
            patrol.CellY = next.Y;
            patrol.CellHeight = next.Height;
            patrol.RouteIndex = nextIndex;
            result.PatrolMoved = true;
            result.PatrolMoves.Add(new SiteExplorationPatrolMove { PatrolId = patrol.PatrolId, From = previous, To = next });
        }

        if (TryFindAlertPatrol(exploration, siteDefinition, out string alertPatrolId))
        {
            Pause(exploration, result, "exploration_alert_radius", alertPatrolId);
            result.AlertPatrolId = alertPatrolId;
            return result;
        }

        if (exploration.PendingPathCellKeys.Count == 0)
        {
            Pause(exploration, result, "exploration_arrived", "");
        }

        return result;
    }

    public static void EnsurePatrolStates(WorldSiteExplorationState exploration, WorldSiteDefinition definition)
    {
        if (exploration == null || definition?.ExplorationPatrols == null)
        {
            return;
        }

        foreach (SiteExplorationPatrolDefinition patrolDefinition in definition.ExplorationPatrols)
        {
            if (patrolDefinition == null ||
                !patrolDefinition.InitiallyActive ||
                string.IsNullOrWhiteSpace(patrolDefinition.Id) ||
                patrolDefinition.RouteCells.Count == 0 ||
                exploration.PatrolUnits.Any(patrol => patrol.PatrolId == patrolDefinition.Id))
            {
                continue;
            }

            SiteExplorationRouteCellDefinition start = patrolDefinition.RouteCells[0];
            exploration.PatrolUnits.Add(new SiteExplorationPatrolState
            {
                PatrolId = patrolDefinition.Id,
                CellX = start.CellX,
                CellY = start.CellY,
                CellHeight = start.CellHeight,
                RouteIndex = 0,
                ActionPoints = 0,
                IsRemoved = false
            });
        }
    }

    public static void ReconcilePatrolStates(WorldSiteState site, WorldSiteDefinition definition)
    {
        if (site?.Exploration == null || definition?.ExplorationPatrols == null)
        {
            return;
        }

        foreach (SiteExplorationPatrolState patrol in site.Exploration.PatrolUnits)
        {
            SiteExplorationPatrolDefinition patrolDefinition = definition.ExplorationPatrols.FirstOrDefault(item => item.Id == patrol.PatrolId);
            if (patrolDefinition == null || !HasAlivePatrolPlacement(site, patrolDefinition))
            {
                patrol.IsRemoved = true;
            }
        }

        foreach (SiteExplorationPatrolDefinition patrolDefinition in definition.ExplorationPatrols)
        {
            if (patrolDefinition == null ||
                !patrolDefinition.InitiallyActive ||
                string.IsNullOrWhiteSpace(patrolDefinition.Id) ||
                patrolDefinition.RouteCells.Count == 0 ||
                !HasAlivePatrolPlacement(site, patrolDefinition) ||
                site.Exploration.PatrolUnits.Any(patrol => patrol.PatrolId == patrolDefinition.Id))
            {
                continue;
            }

            SiteExplorationRouteCellDefinition start = patrolDefinition.RouteCells[0];
            site.Exploration.PatrolUnits.Add(new SiteExplorationPatrolState
            {
                PatrolId = patrolDefinition.Id,
                CellX = start.CellX,
                CellY = start.CellY,
                CellHeight = start.CellHeight,
                RouteIndex = 0,
                ActionPoints = 0,
                IsRemoved = false
            });
        }
    }

    public static bool HasAlivePatrolPlacement(WorldSiteState site, SiteExplorationPatrolDefinition patrolDefinition)
    {
        if (site == null || patrolDefinition == null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(patrolDefinition.SourcePlacementId))
        {
            return site.UnitPlacements.Any(placement =>
                placement.PlacementId == patrolDefinition.SourcePlacementId &&
                placement.UnitTypeId == patrolDefinition.UnitTypeId);
        }

        return site.UnitPlacements.Any(placement => placement.UnitTypeId == patrolDefinition.UnitTypeId);
    }

    public static BattleStartRequest BuildExplorationBattleRequest(
        string siteId,
        string pointId,
        string triggerPatrolId,
        WorldArmyState playerArmy,
        IEnumerable<SiteExplorationPatrolDefinition> encounterPatrols,
        GridSurfacePosition entryCell,
        int alertLevel,
        string returnScenePath,
        string siteScenePath)
    {
        BattleStartRequest request = new()
        {
            ContextId = siteId ?? "",
            BattleKind = BattleKind.AssaultSite,
            EncounterId = $"site_exploration:{pointId ?? "unknown"}",
            SourceArmyId = playerArmy?.ArmyId ?? "",
            SourceSiteId = playerArmy?.SourceSiteId ?? "",
            TargetSiteId = siteId ?? "",
            AttackerFactionId = StrategicWorldIds.FactionPlayer,
            DefenderFactionId = StrategicWorldIds.FactionUndead,
            AttackDirection = playerArmy?.TargetApproachDirection ?? WorldSiteAttackDirection.Any,
            MapDefinitionId = string.IsNullOrWhiteSpace(siteId) ? "site_exploration_v1" : $"{siteId}_exploration_v1",
            ReturnScenePath = returnScenePath ?? "",
            SiteScenePath = string.IsNullOrWhiteSpace(siteScenePath) ? "res://scenes/world/sites/WorldSiteRoot.tscn" : siteScenePath
        };
        request.ExplorationPointId = pointId ?? "";
        request.ExplorationTriggerPatrolId = triggerPatrolId ?? "";
        request.ExplorationEntryCellX = entryCell.X;
        request.ExplorationEntryCellY = entryCell.Y;
        request.ExplorationEntryCellHeight = entryCell.Height;
        request.ExplorationAlertLevel = Math.Max(0, alertLevel);
        request.ObjectiveIds.Add($"exploration_cell={entryCell.X}:{entryCell.Y}:{entryCell.Height}");
        request.ObjectiveIds.Add($"exploration_alert={Math.Max(0, alertLevel)}");
        if (!string.IsNullOrWhiteSpace(triggerPatrolId))
        {
            request.ObjectiveIds.Add($"exploration_patrol={triggerPatrolId}");
        }

        AddExplorationArmyForces(request.PlayerForces, playerArmy);
        AddExplorationPatrolForces(request.EnemyForces, siteId, encounterPatrols);
        return request;
    }

    private static void AddExplorationArmyForces(ICollection<BattleForceRequest> target, WorldArmyState army)
    {
        if (target == null || army == null)
        {
            return;
        }

        foreach (GarrisonState unit in army.GarrisonUnits.Where(item => item.Count > 0 && !string.IsNullOrWhiteSpace(item.UnitTypeId)))
        {
            target.Add(new BattleForceRequest
            {
                ForceId = $"{army.ArmyId}:{unit.UnitTypeId}",
                SourceKind = "PlayerArmy",
                SourceId = army.ArmyId,
                UnitDefinitionId = unit.UnitTypeId,
                Count = unit.Count,
                FactionId = string.IsNullOrWhiteSpace(army.OwnerFactionId) ? StrategicWorldIds.FactionPlayer : army.OwnerFactionId
            });
        }
    }

    private static void AddExplorationPatrolForces(
        ICollection<BattleForceRequest> target,
        string siteId,
        IEnumerable<SiteExplorationPatrolDefinition> patrols)
    {
        if (target == null || patrols == null)
        {
            return;
        }

        foreach (SiteExplorationPatrolDefinition patrol in patrols.Where(item => item != null))
        {
            AddExplorationPatrolForce(target, siteId, patrol);
        }
    }

    private static void AddExplorationPatrolForce(
        ICollection<BattleForceRequest> target,
        string siteId,
        SiteExplorationPatrolDefinition patrol)
    {
        if (target == null || patrol == null || string.IsNullOrWhiteSpace(patrol.UnitTypeId))
        {
            return;
        }

        // Exploration patrol combat references the existing WorldSite placement as the authority.
        // The deployment layer must bind this force to that resident unit instead of spawning a duplicate.
        string placementId = patrol.SourcePlacementId ?? "";
        target.Add(new BattleForceRequest
        {
            ForceId = string.IsNullOrWhiteSpace(patrol.Id)
                ? $"site_exploration:{siteId}:{patrol.UnitTypeId}"
                : $"site_exploration:{siteId}:{patrol.Id}:{patrol.UnitTypeId}",
            SourceKind = string.IsNullOrWhiteSpace(placementId) ? "DefenderSite" : "SitePlacement",
            SourceId = string.IsNullOrWhiteSpace(placementId) ? siteId ?? "" : placementId,
            UnitDefinitionId = patrol.UnitTypeId,
            Count = 1,
            FactionId = StrategicWorldIds.FactionUndead
        });
    }

    public static void ApplyAction(
        WorldSiteExplorationState exploration,
        string pointId,
        string[] revealsPointIds,
        int alertDelta,
        bool resolvesPoint)
    {
        if (exploration == null)
        {
            return;
        }

        exploration.AlertLevel = Math.Clamp(exploration.AlertLevel + alertDelta, 0, 5);
        if (revealsPointIds != null)
        {
            foreach (string id in revealsPointIds)
            {
                AddUnique(exploration.RevealedPointIds, id);
            }
        }

        if (resolvesPoint)
        {
            AddUnique(exploration.ResolvedPointIds, pointId);
        }
    }

    public static string ToCellKey(GridSurfacePosition cell)
    {
        return $"{cell.X}:{cell.Y}:{cell.Height}";
    }

    private static bool TryBuildPath(
        WorldSiteExplorationState exploration,
        WorldSiteDefinition siteDefinition,
        BattleGridMap gridMap,
        GridPosition destination,
        out IReadOnlyList<GridSurfacePosition> path,
        out string failureReason)
    {
        path = Array.Empty<GridSurfacePosition>();
        failureReason = "";
        if (exploration == null || gridMap == null)
        {
            failureReason = "exploration_missing";
            return false;
        }

        GridSurfacePosition start = new(exploration.CurrentCellX, exploration.CurrentCellY, exploration.CurrentCellHeight);
        if (!IsValidWalkableTopSurface(gridMap, start))
        {
            if (!TryUseNearestWalkableStart(exploration, gridMap, out start))
            {
                failureReason = "exploration_start_blocked";
                return false;
            }
        }

        if (!gridMap.TryGetTopSurfacePosition(destination, out GridSurfacePosition target))
        {
            failureReason = "exploration_destination_missing";
            return false;
        }

        // Exploration units are hard blockers for pathfinding, not just post-path validation.
        // If blockers are checked only after shortest-path selection, a valid route around a patrol can be rejected
        // because the first cheapest path happened to pass through the occupied patrol cell.
        MovementRangeResult movement = MovementRangeFinder.FindReachableCells(
            gridMap,
            start,
            ExplorationPathMoveBudget,
            BuildOccupiedExplorationSurfaces(exploration, siteDefinition, start));
        if (!movement.TryBuildPathTo(target, out path) || path.Count == 0)
        {
            failureReason = "exploration_destination_unreachable";
            return false;
        }

        if (PathCrossesOccupiedExplorationUnit(exploration, siteDefinition, path))
        {
            failureReason = "exploration_path_occupied";
            return false;
        }

        return true;
    }

    private static bool TryUseNearestWalkableStart(
        WorldSiteExplorationState exploration,
        BattleGridMap gridMap,
        out GridSurfacePosition start)
    {
        foreach (GridSurfacePosition candidate in gridMap.TopSurfacePositions.Values)
        {
            if (gridMap.TryGetSurface(candidate, out GridCellSurface surface) && surface.IsWalkable)
            {
                start = candidate;
                exploration.CurrentCellX = candidate.X;
                exploration.CurrentCellY = candidate.Y;
                exploration.CurrentCellHeight = candidate.Height;
                MarkVisited(exploration, candidate);
                return true;
            }
        }

        start = default;
        return false;
    }

    private static void MarkVisited(WorldSiteExplorationState exploration, GridSurfacePosition cell)
    {
        string key = ToCellKey(cell);
        AddUnique(exploration.VisitedCellKeys, key);
        AddUnique(exploration.RevealedCellKeys, key);
    }

    private static void AddUnique(List<string> values, string value)
    {
        if (values == null || string.IsNullOrWhiteSpace(value) || values.Contains(value))
        {
            return;
        }

        values.Add(value);
    }

    private static bool IsValidWalkableTopSurface(BattleGridMap gridMap, GridSurfacePosition cell)
    {
        return gridMap.TryGetSurface(cell, out GridCellSurface surface) &&
            surface.IsWalkable &&
            gridMap.IsTopSurface(cell);
    }

    private static bool CanPatrolEnterCell(
        BattleGridMap gridMap,
        GridSurfacePosition cell,
        GridSurfacePosition? reservedPartyDestination)
    {
        if (reservedPartyDestination.HasValue &&
            SameCell(cell, reservedPartyDestination.Value))
        {
            return false;
        }

        return IsValidWalkableTopSurface(gridMap, cell);
    }

    private static bool IsOccupiedByExplorationUnit(
        WorldSiteExplorationState exploration,
        WorldSiteDefinition siteDefinition,
        GridSurfacePosition target)
    {
        if (exploration == null)
        {
            return false;
        }

        GridSurfacePosition party = new(exploration.CurrentCellX, exploration.CurrentCellY, exploration.CurrentCellHeight);
        if (SameCell(party, target))
        {
            return true;
        }

        foreach (SiteExplorationPatrolState patrol in exploration.PatrolUnits)
        {
            if (patrol == null || patrol.IsRemoved)
            {
                continue;
            }

            if (siteDefinition?.ExplorationPatrols?.Any(definition => definition.Id == patrol.PatrolId) == false)
            {
                continue;
            }

            if (SameCell(new GridSurfacePosition(patrol.CellX, patrol.CellY, patrol.CellHeight), target))
            {
                return true;
            }
        }

        return false;
    }

    private static bool PathCrossesOccupiedExplorationUnit(
        WorldSiteExplorationState exploration,
        WorldSiteDefinition siteDefinition,
        IReadOnlyList<GridSurfacePosition> path)
    {
        if (path == null || path.Count <= 1)
        {
            return false;
        }

        for (int index = 1; index < path.Count; index++)
        {
            if (IsOccupiedByExplorationUnit(exploration, siteDefinition, path[index]))
            {
                return true;
            }
        }

        return false;
    }

    private static ISet<GridSurfacePosition> BuildOccupiedExplorationSurfaces(
        WorldSiteExplorationState exploration,
        WorldSiteDefinition siteDefinition,
        GridSurfacePosition start)
    {
        var blocked = new HashSet<GridSurfacePosition>();
        if (exploration == null)
        {
            return blocked;
        }

        foreach (SiteExplorationPatrolState patrol in exploration.PatrolUnits)
        {
            if (patrol == null || patrol.IsRemoved)
            {
                continue;
            }

            if (siteDefinition?.ExplorationPatrols?.Any(definition => definition.Id == patrol.PatrolId) == false)
            {
                continue;
            }

            GridSurfacePosition patrolCell = new(patrol.CellX, patrol.CellY, patrol.CellHeight);
            if (!SameCell(patrolCell, start))
            {
                blocked.Add(patrolCell);
            }
        }

        return blocked;
    }

    private static bool SameCell(GridSurfacePosition a, GridSurfacePosition b)
    {
        return a.X == b.X && a.Y == b.Y && a.Height == b.Height;
    }

    private static bool TryParseCellKey(string key, out GridSurfacePosition cell)
    {
        cell = default;
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        string[] parts = key.Split(':');
        if (parts.Length != 3 ||
            !int.TryParse(parts[0], out int x) ||
            !int.TryParse(parts[1], out int y) ||
            !int.TryParse(parts[2], out int height))
        {
            return false;
        }

        cell = new GridSurfacePosition(x, y, height);
        return true;
    }

    private static GridSurfacePosition ToSurface(SiteExplorationRouteCellDefinition cell)
    {
        return new GridSurfacePosition(cell.CellX, cell.CellY, cell.CellHeight);
    }

    private static bool TryFindAlertPatrol(WorldSiteExplorationState exploration, WorldSiteDefinition definition, out string alertPatrolId)
    {
        alertPatrolId = "";
        GridSurfacePosition party = new(exploration.CurrentCellX, exploration.CurrentCellY, exploration.CurrentCellHeight);
        foreach (SiteExplorationPatrolState patrol in exploration.PatrolUnits)
        {
            if (patrol == null || patrol.IsRemoved)
            {
                continue;
            }

            SiteExplorationPatrolDefinition patrolDefinition = definition.ExplorationPatrols.FirstOrDefault(item => item.Id == patrol.PatrolId);
            if (patrolDefinition == null)
            {
                continue;
            }

            GridSurfacePosition patrolCell = new(patrol.CellX, patrol.CellY, patrol.CellHeight);
            if (GetManhattanDistance(party, patrolCell) <= Math.Max(0, patrolDefinition.AlertRadiusCells))
            {
                alertPatrolId = patrol.PatrolId;
                return true;
            }
        }

        return false;
    }

    private static int GetManhattanDistance(GridSurfacePosition a, GridSurfacePosition b)
    {
        return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) + Math.Abs(a.Height - b.Height);
    }

    private static void Pause(WorldSiteExplorationState exploration, SiteExplorationTickResult result, string reason, string patrolId)
    {
        exploration.IsSimulationPaused = true;
        exploration.PauseReason = reason ?? "";
        exploration.ActiveAlertPatrolId = patrolId ?? "";
        result.Paused = true;
        result.PauseReason = exploration.PauseReason;
        result.AlertPatrolId = exploration.ActiveAlertPatrolId;
    }
}

public sealed class SiteExplorationTickResult
{
    public bool PartyMoved { get; set; }
    public bool PatrolMoved { get; set; }
    public bool Paused { get; set; }
    public string PauseReason { get; set; } = "";
    public string AlertPatrolId { get; set; } = "";
    public List<GridSurfacePosition> PartyPathStep { get; } = new();
    public List<SiteExplorationPatrolMove> PatrolMoves { get; } = new();
}

public sealed class SiteExplorationPatrolMove
{
    public string PatrolId { get; set; } = "";
    public GridSurfacePosition From { get; set; }
    public GridSurfacePosition To { get; set; }
}
