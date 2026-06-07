using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle;
using Rpg.Definitions.Maps;
using Rpg.Domain.Battle.Grid;
using Rpg.Domain.World;

namespace Rpg.Application.World;

public sealed class BattlePreparationCompanyFormationPlanner
{
    private const int LateralLaneCount = 2;

    public BattlePreparationCompanyFormationDraft BuildDraft(
        IReadOnlyList<BattleForceRequest> companyForces,
        string formationId,
        GridPosition formationAnchor,
        SemanticDeploymentSide deploymentSide,
        string factionId,
        WorldSiteAttackDirection direction,
        BattleGridMap gridMap,
        WorldSiteRuntimeDeploymentCache deploymentCache,
        IEnumerable<BattleForceRequest> occupancyForces,
        Func<BattleForceRequest, bool> canEnterWater)
    {
        List<BattlePreparationCompanyMember> members = FlattenMembers(companyForces).ToList();
        if (members.Count == 0)
        {
            return BattlePreparationCompanyFormationDraft.Invalid("battle_force_missing");
        }

        if (gridMap == null || deploymentCache == null)
        {
            return BattlePreparationCompanyFormationDraft.Invalid("placement_cell_invalid");
        }

        Vector2I depthAxis = ResolveDepthAxis(direction);
        Vector2I lateralAxis = ResolveLateralAxis(depthAxis);
        HashSet<GridPosition> occupiedCells = BuildOccupiedCells(occupancyForces);
        WorldSiteDeploymentCell[] deployableCells = deploymentCache
            .GetDeploymentZoneCandidatesForSide(deploymentSide, factionId, direction)
            .ToArray();
        IReadOnlyList<GridPosition> candidateAnchors = ResolveCandidateAnchors(formationAnchor, deployableCells);

        BattlePreparationCompanyFormationDraft fallbackInvalid = null;
        foreach (int lateralLaneCount in ResolveFormationLaneAttempts(formationId))
        {
            foreach (GridPosition candidateAnchor in candidateAnchors)
            {
                BattlePreparationCompanyFormationDraft attempt = BuildDraftAttempt(
                    members,
                    candidateAnchor,
                    depthAxis,
                    lateralAxis,
                    lateralLaneCount,
                    gridMap,
                    deployableCells,
                    occupiedCells,
                    factionId,
                    canEnterWater);
                if (attempt.IsValid)
                {
                    return attempt;
                }

                fallbackInvalid ??= attempt;
            }
        }

        return fallbackInvalid ?? BattlePreparationCompanyFormationDraft.Invalid("placement_cell_invalid");
    }

    private static BattlePreparationCompanyFormationDraft BuildDraftAttempt(
        IReadOnlyList<BattlePreparationCompanyMember> members,
        GridPosition formationAnchor,
        Vector2I depthAxis,
        Vector2I lateralAxis,
        int lateralLaneCount,
        BattleGridMap gridMap,
        IReadOnlyList<WorldSiteDeploymentCell> deployableCells,
        HashSet<GridPosition> occupiedCells,
        string factionId,
        Func<BattleForceRequest, bool> canEnterWater)
    {
        int depthStep = ResolveDepthStep(members, depthAxis);
        int lateralStep = ResolveLateralStep(members, depthAxis);
        int laneCount = Math.Max(1, lateralLaneCount);
        List<BattlePreparationCompanyPlacementDraft> placements = new(members.Count);
        HashSet<GridPosition> coveredCells = new();
        string failureReason = "";

        for (int order = 0; order < members.Count; order++)
        {
            BattlePreparationCompanyMember member = members[order];
            int lane = order % laneCount;
            int rank = order / laneCount;
            GridPosition anchor = Offset(
                formationAnchor,
                depthAxis,
                rank * depthStep,
                lateralAxis,
                lane * lateralStep);

            if (!gridMap.TryGetTopSurfacePosition(anchor, out GridSurfacePosition surfacePosition) &&
                string.IsNullOrWhiteSpace(failureReason))
            {
                failureReason = "placement_cell_invalid";
            }

            IReadOnlyList<GridPosition> memberCells = BattleFootprintCells.Enumerate(
                anchor,
                member.FootprintSize.X,
                member.FootprintSize.Y);
            bool memberCanEnterWater = canEnterWater?.Invoke(member.Force) == true;
            string memberFailureReason = ValidateMemberCells(
                memberCells,
                gridMap,
                deployableCells,
                occupiedCells,
                coveredCells,
                memberCanEnterWater);
            if (!string.IsNullOrWhiteSpace(memberFailureReason))
            {
                failureReason = string.IsNullOrWhiteSpace(failureReason)
                    ? memberFailureReason
                    : failureReason;
            }

            foreach (GridPosition cell in memberCells)
            {
                coveredCells.Add(cell);
            }

            placements.Add(new BattlePreparationCompanyPlacementDraft
            {
                Force = member.Force,
                ForceId = member.Force?.ForceId ?? "",
                ForceIndex = member.ForceIndex,
                UnitDefinitionId = member.Force?.UnitDefinitionId ?? "",
                FactionId = member.Force?.FactionId ?? factionId ?? "",
                PlacementId = BuildPlacementId(member.Force, member.ForceIndex),
                Anchor = anchor,
                CellHeight = surfacePosition.Height,
                FootprintSize = member.FootprintSize,
                CoveredCells = memberCells
            });
        }

        return string.IsNullOrWhiteSpace(failureReason)
            ? BattlePreparationCompanyFormationDraft.Valid(placements, coveredCells)
            : BattlePreparationCompanyFormationDraft.Invalid(failureReason, placements, coveredCells);
    }

    private static IReadOnlyList<GridPosition> ResolveCandidateAnchors(
        GridPosition preferredAnchor,
        IReadOnlyList<WorldSiteDeploymentCell> deployableCells)
    {
        // The pointer is player intent, not a hard requirement. When it moves outside
        // the start zone, project the whole formation to the nearest legal anchor.
        return new[] { preferredAnchor }
            .Concat((deployableCells ?? Array.Empty<WorldSiteDeploymentCell>())
                .Select(cell => new GridPosition(cell.Cell.X, cell.Cell.Y))
                .OrderBy(anchor => DistanceSquared(anchor, preferredAnchor))
                .ThenBy(anchor => anchor.Y)
                .ThenBy(anchor => anchor.X))
            .Distinct()
            .ToArray();
    }

    private static int DistanceSquared(GridPosition lhs, GridPosition rhs)
    {
        int dx = lhs.X - rhs.X;
        int dy = lhs.Y - rhs.Y;
        return dx * dx + dy * dy;
    }

    private static string ValidateMemberCells(
        IReadOnlyList<GridPosition> memberCells,
        BattleGridMap gridMap,
        IReadOnlyList<WorldSiteDeploymentCell> deployableCells,
        HashSet<GridPosition> occupiedCells,
        HashSet<GridPosition> coveredCells,
        bool memberCanEnterWater)
    {
        foreach (GridPosition cell in memberCells ?? Array.Empty<GridPosition>())
        {
            if (!gridMap.TryGetTopSurfacePosition(cell, out _))
            {
                return "placement_cell_invalid";
            }

            WorldSiteDeploymentCell? deploymentCell = deployableCells
                .Where(candidate => candidate.Cell.X == cell.X && candidate.Cell.Y == cell.Y)
                .Select(candidate => (WorldSiteDeploymentCell?)candidate)
                .FirstOrDefault();
            if (!deploymentCell.HasValue)
            {
                return "placement_cell_not_deployable";
            }

            if (!memberCanEnterWater && deploymentCell.Value.IsWater)
            {
                return "placement_cell_water";
            }

            if (occupiedCells.Contains(cell) || coveredCells.Contains(cell))
            {
                return "placement_cell_occupied";
            }
        }

        return "";
    }

    private static int ResolveDepthStep(
        IReadOnlyList<BattlePreparationCompanyMember> members,
        Vector2I depthAxis)
    {
        return Math.Max(1, members.Max(member => depthAxis.X != 0 ? member.FootprintSize.X : member.FootprintSize.Y));
    }

    private static int ResolveLateralStep(
        IReadOnlyList<BattlePreparationCompanyMember> members,
        Vector2I depthAxis)
    {
        return Math.Max(1, members.Max(member => depthAxis.X != 0 ? member.FootprintSize.Y : member.FootprintSize.X));
    }

    private static IReadOnlyList<int> ResolveFormationLaneAttempts(string formationId)
    {
        string normalized = formationId?.Trim() ?? "";
        if (string.Equals(normalized, "formation_column", StringComparison.Ordinal) ||
            string.Equals(normalized, "column", StringComparison.Ordinal))
        {
            return new[] { 1, LateralLaneCount };
        }

        // Standard starts readable and wide, then falls back to a no-overlap column for narrow zones.
        return new[] { LateralLaneCount, 1 };
    }

    public void ApplyDraft(BattlePreparationCompanyFormationDraft draft)
    {
        if (draft?.IsValid != true)
        {
            return;
        }

        foreach (BattlePreparationCompanyPlacementDraft placement in draft.Placements)
        {
            if (placement?.Force == null || placement.ForceIndex < 0)
            {
                continue;
            }

            while (placement.Force.PreferredPlacements.Count <= placement.ForceIndex)
            {
                placement.Force.PreferredPlacements.Add(null);
            }

            placement.Force.PreferredPlacements[placement.ForceIndex] = new BattleForcePlacementRequest
            {
                PlacementId = placement.PlacementId,
                CellX = placement.Anchor.X,
                CellY = placement.Anchor.Y,
                CellHeight = placement.CellHeight
            };
        }
    }

    private static IEnumerable<BattlePreparationCompanyMember> FlattenMembers(
        IEnumerable<BattleForceRequest> forces)
    {
        foreach (BattleForceRequest force in (forces ?? Array.Empty<BattleForceRequest>())
                     .Where(force => force != null && force.Count > 0)
                     .OrderBy(force => IsLikelyHeroForce(force) ? 0 : 1)
                     .ThenBy(force => force.ForceId ?? "", StringComparer.Ordinal)
                     .ThenBy(force => force.UnitDefinitionId ?? "", StringComparer.Ordinal))
        {
            Vector2I footprintSize = new(
                BattleFootprintCells.NormalizeSize(force.FootprintWidth),
                BattleFootprintCells.NormalizeSize(force.FootprintHeight));
            for (int index = 0; index < force.Count; index++)
            {
                yield return new BattlePreparationCompanyMember(force, index, footprintSize);
            }
        }
    }

    private static HashSet<GridPosition> BuildOccupiedCells(IEnumerable<BattleForceRequest> forces)
    {
        HashSet<GridPosition> occupied = new();
        foreach (BattleForceRequest force in forces ?? Array.Empty<BattleForceRequest>())
        {
            for (int index = 0; index < (force?.PreferredPlacements?.Count ?? 0); index++)
            {
                BattleForcePlacementRequest placement = force.PreferredPlacements[index];
                if (placement == null)
                {
                    continue;
                }

                foreach (GridPosition cell in BattleFootprintCells.Enumerate(
                             new GridPosition(placement.CellX, placement.CellY),
                             force.FootprintWidth,
                             force.FootprintHeight))
                {
                    occupied.Add(cell);
                }
            }
        }

        return occupied;
    }

    private static bool IsLikelyHeroForce(BattleForceRequest force)
    {
        return force != null &&
               (force.UnitDefinitionId?.Contains("hero", StringComparison.OrdinalIgnoreCase) == true ||
                force.SourceKind?.Contains("Hero", StringComparison.OrdinalIgnoreCase) == true ||
                string.Equals(force.UnitDefinitionId, HeroCorpsV0PlayableSliceIds.HeroUnit, StringComparison.Ordinal));
    }

    private static Vector2I ResolveDepthAxis(WorldSiteAttackDirection direction)
    {
        return direction switch
        {
            WorldSiteAttackDirection.West => new Vector2I(-1, 0),
            WorldSiteAttackDirection.North => new Vector2I(0, -1),
            WorldSiteAttackDirection.South => new Vector2I(0, 1),
            _ => new Vector2I(1, 0)
        };
    }

    private static Vector2I ResolveLateralAxis(Vector2I depthAxis)
    {
        return depthAxis.X != 0 ? new Vector2I(0, 1) : new Vector2I(1, 0);
    }

    private static GridPosition Offset(
        GridPosition anchor,
        Vector2I depthAxis,
        int depth,
        Vector2I lateralAxis,
        int lateral)
    {
        return new GridPosition(
            anchor.X + depthAxis.X * depth + lateralAxis.X * lateral,
            anchor.Y + depthAxis.Y * depth + lateralAxis.Y * lateral);
    }

    private static string BuildPlacementId(BattleForceRequest force, int forceIndex)
    {
        string forceId = string.IsNullOrWhiteSpace(force?.ForceId)
            ? force?.UnitDefinitionId ?? "force"
            : force.ForceId;
        return $"battle_deploy:{forceId}:{forceIndex + 1}";
    }

    private readonly record struct BattlePreparationCompanyMember(
        BattleForceRequest Force,
        int ForceIndex,
        Vector2I FootprintSize);
}

public sealed class BattlePreparationCompanyFormationDraft
{
    public bool IsValid { get; init; }
    public string FailureReason { get; init; } = "";
    public IReadOnlyList<BattlePreparationCompanyPlacementDraft> Placements { get; init; } =
        Array.Empty<BattlePreparationCompanyPlacementDraft>();
    public IReadOnlyList<GridPosition> CoveredCells { get; init; } = Array.Empty<GridPosition>();

    public static BattlePreparationCompanyFormationDraft Valid(
        IReadOnlyList<BattlePreparationCompanyPlacementDraft> placements,
        IEnumerable<GridPosition> coveredCells)
    {
        return new BattlePreparationCompanyFormationDraft
        {
            IsValid = true,
            Placements = placements ?? Array.Empty<BattlePreparationCompanyPlacementDraft>(),
            CoveredCells = coveredCells?.Distinct().ToArray() ?? Array.Empty<GridPosition>()
        };
    }

    public static BattlePreparationCompanyFormationDraft Invalid(
        string failureReason,
        IReadOnlyList<BattlePreparationCompanyPlacementDraft> placements = null,
        IEnumerable<GridPosition> coveredCells = null)
    {
        return new BattlePreparationCompanyFormationDraft
        {
            IsValid = false,
            FailureReason = string.IsNullOrWhiteSpace(failureReason) ? "placement_cell_invalid" : failureReason,
            Placements = placements ?? Array.Empty<BattlePreparationCompanyPlacementDraft>(),
            CoveredCells = coveredCells?.Distinct().ToArray() ?? Array.Empty<GridPosition>()
        };
    }
}

public sealed class BattlePreparationCompanyPlacementDraft
{
    public BattleForceRequest Force { get; init; }
    public string ForceId { get; init; } = "";
    public int ForceIndex { get; init; } = -1;
    public string UnitDefinitionId { get; init; } = "";
    public string FactionId { get; init; } = "";
    public string PlacementId { get; init; } = "";
    public GridPosition Anchor { get; init; }
    public int CellHeight { get; init; }
    public Vector2I FootprintSize { get; init; } = Vector2I.One;
    public IReadOnlyList<GridPosition> CoveredCells { get; init; } = Array.Empty<GridPosition>();
}
