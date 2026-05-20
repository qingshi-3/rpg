using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Rpg.Application.Battle.Snapshots;

namespace Rpg.Application.Battle.Navigation;

public static class BattleNavigationTopologyDiagnostics
{
    private const int SampleLimit = 96;

    public static string DescribeRequestTopology(BattleStartRequest request, string stage)
    {
        return Describe(
            request?.RequestId ?? "",
            request?.ContextId ?? "",
            stage,
            request?.NavigationSurfaces?.Count ?? 0,
            request?.NavigationConnections?.Count ?? 0,
            request?.NavigationTopology,
            EnumerateRequestPlacements(request));
    }

    public static string DescribeSnapshotTopology(BattleStartSnapshot snapshot, string stage)
    {
        return Describe(
            snapshot?.SnapshotId ?? "",
            snapshot?.BattleId ?? "",
            stage,
            snapshot?.LocationContext?.NavigationSurfaces?.Count ?? 0,
            snapshot?.LocationContext?.NavigationConnections?.Count ?? 0,
            snapshot?.LocationContext?.NavigationTopology,
            EnumerateSnapshotPlacements(snapshot));
    }

    private static string Describe(
        string requestOrSnapshotId,
        string battleId,
        string stage,
        int surfaceCount,
        int connectionCount,
        BattleNavigationTopology topology,
        IEnumerable<PlacementDiagnostic> placements)
    {
        BattleNavigationNode[] nodes = (topology?.Nodes ?? Enumerable.Empty<BattleNavigationNode>())
            .Where(node => node != null)
            .OrderBy(node => node.Height)
            .ThenBy(node => node.Y)
            .ThenBy(node => node.X)
            .ToArray();
        BattleNavigationEdge[] edges = (topology?.Edges ?? Enumerable.Empty<BattleNavigationEdge>())
            .Where(edge => edge != null)
            .OrderBy(edge => edge.FromHeight)
            .ThenBy(edge => edge.FromY)
            .ThenBy(edge => edge.FromX)
            .ThenBy(edge => edge.ToHeight)
            .ThenBy(edge => edge.ToY)
            .ThenBy(edge => edge.ToX)
            .ThenBy(edge => edge.Kind)
            .ToArray();
        var nodeSet = nodes
            .Select(node => (node.X, node.Y, node.Height))
            .ToHashSet();

        string nodeSample = string.Join(";", nodes
            .Take(SampleLimit)
            .Select(node => FormatNode(node.X, node.Y, node.Height)));
        string nodeHeightSummary = string.Join(";", nodes
            .GroupBy(node => node.Height)
            .OrderBy(group => group.Key)
            .Select(group => $"h={group.Key}:{group.Count()}"));
        string edgeSample = string.Join(";", edges
            .Take(SampleLimit)
            .Select(edge => $"{FormatNode(edge.FromX, edge.FromY, edge.FromHeight)}->{FormatNode(edge.ToX, edge.ToY, edge.ToHeight)}:{edge.Kind}"));
        string placementSample = string.Join(";", (placements ?? Enumerable.Empty<PlacementDiagnostic>())
            .Take(SampleLimit)
            .Select(placement =>
            {
                bool inTopology = nodeSet.Contains((placement.X, placement.Y, placement.Height));
                return $"placement={placement.ForceId}/{placement.PlacementId}@{FormatNode(placement.X, placement.Y, placement.Height)}:inTopology={inTopology}";
            }));

        return string.Create(
            CultureInfo.InvariantCulture,
            $"BattleNavigationTopologyDump stage={stage ?? ""} id={requestOrSnapshotId ?? ""} battle={battleId ?? ""} surfaces={surfaceCount} connections={connectionCount} nodes={nodes.Length} edges={edges.Length} nodeHeights={nodeHeightSummary} nodeSample={nodeSample} edgeSample={edgeSample} placementSample={placementSample}");
    }

    private static IEnumerable<PlacementDiagnostic> EnumerateRequestPlacements(BattleStartRequest request)
    {
        foreach (BattleForceRequest force in request?.PlayerForces ?? Enumerable.Empty<BattleForceRequest>())
        {
            foreach (PlacementDiagnostic placement in EnumerateForcePlacements(force))
            {
                yield return placement;
            }
        }

        foreach (BattleForceRequest force in request?.EnemyForces ?? Enumerable.Empty<BattleForceRequest>())
        {
            foreach (PlacementDiagnostic placement in EnumerateForcePlacements(force))
            {
                yield return placement;
            }
        }
    }

    private static IEnumerable<PlacementDiagnostic> EnumerateForcePlacements(BattleForceRequest force)
    {
        foreach (BattleForcePlacementRequest placement in force?.PreferredPlacements ?? Enumerable.Empty<BattleForcePlacementRequest>())
        {
            if (placement == null)
            {
                continue;
            }

            yield return new PlacementDiagnostic(
                force?.ForceId ?? "",
                placement.PlacementId ?? "",
                placement.CellX,
                placement.CellY,
                placement.CellHeight);
        }
    }

    private static IEnumerable<PlacementDiagnostic> EnumerateSnapshotPlacements(BattleStartSnapshot snapshot)
    {
        foreach (BattleGroupSnapshot group in snapshot?.BattleGroups ?? Enumerable.Empty<BattleGroupSnapshot>())
        {
            if (group == null)
            {
                continue;
            }

            yield return new PlacementDiagnostic(
                string.IsNullOrWhiteSpace(group.SourceForceId) ? group.BattleGroupId ?? "" : group.SourceForceId,
                group.BattleGroupId ?? "",
                group.CellX,
                group.CellY,
                group.CellHeight);
        }
    }

    private static string FormatNode(int x, int y, int height)
    {
        return string.Create(CultureInfo.InvariantCulture, $"({x},{y},h={height})");
    }

    private readonly record struct PlacementDiagnostic(string ForceId, string PlacementId, int X, int Y, int Height);
}
