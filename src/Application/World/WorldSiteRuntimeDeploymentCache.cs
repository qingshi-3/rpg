using System.Collections.Generic;
using System.Linq;
using Rpg.Definitions.Maps;
using Rpg.Domain.Battle.Grid;
using Rpg.Domain.World;

namespace Rpg.Application.World;

public sealed class WorldSiteRuntimeDeploymentCache
{
    public WorldSiteRuntimeDeploymentCache(
        string siteId,
        int candidateSurfaceCount,
        IReadOnlyDictionary<WorldSiteAttackDirection, IReadOnlyList<WorldSiteDeploymentCell>> candidatesByDirection)
        : this(siteId, candidateSurfaceCount, candidatesByDirection, null)
    {
    }

    public WorldSiteRuntimeDeploymentCache(
        string siteId,
        int candidateSurfaceCount,
        IReadOnlyDictionary<WorldSiteAttackDirection, IReadOnlyList<WorldSiteDeploymentCell>> candidatesByDirection,
        IReadOnlyDictionary<string, IReadOnlyDictionary<WorldSiteAttackDirection, IReadOnlyList<WorldSiteDeploymentCell>>> deploymentZoneCandidatesByFaction)
        : this(siteId, candidateSurfaceCount, candidatesByDirection, deploymentZoneCandidatesByFaction, null)
    {
    }

    public WorldSiteRuntimeDeploymentCache(
        string siteId,
        int candidateSurfaceCount,
        IReadOnlyDictionary<WorldSiteAttackDirection, IReadOnlyList<WorldSiteDeploymentCell>> candidatesByDirection,
        IReadOnlyDictionary<string, IReadOnlyDictionary<WorldSiteAttackDirection, IReadOnlyList<WorldSiteDeploymentCell>>> deploymentZoneCandidatesByFaction,
        IReadOnlyDictionary<SemanticDeploymentSide, IReadOnlyDictionary<WorldSiteAttackDirection, IReadOnlyList<WorldSiteDeploymentCell>>> deploymentZoneCandidatesBySide)
    {
        SiteId = siteId ?? "";
        CandidateSurfaceCount = System.Math.Max(0, candidateSurfaceCount);
        CandidatesByDirection = (candidatesByDirection ?? new Dictionary<WorldSiteAttackDirection, IReadOnlyList<WorldSiteDeploymentCell>>())
            .ToDictionary(
                pair => pair.Key,
                pair => pair.Value ?? System.Array.Empty<WorldSiteDeploymentCell>());
        DeploymentZoneCandidatesByFaction = (deploymentZoneCandidatesByFaction ??
                                             new Dictionary<string, IReadOnlyDictionary<WorldSiteAttackDirection, IReadOnlyList<WorldSiteDeploymentCell>>>())
            .ToDictionary(
                pair => NormalizeFactionKey(pair.Key),
                pair => (IReadOnlyDictionary<WorldSiteAttackDirection, IReadOnlyList<WorldSiteDeploymentCell>>)(pair.Value ??
                    new Dictionary<WorldSiteAttackDirection, IReadOnlyList<WorldSiteDeploymentCell>>()),
                System.StringComparer.Ordinal);
        DeploymentZoneCandidatesBySide = (deploymentZoneCandidatesBySide ??
                                          new Dictionary<SemanticDeploymentSide, IReadOnlyDictionary<WorldSiteAttackDirection, IReadOnlyList<WorldSiteDeploymentCell>>>())
            .ToDictionary(
                pair => NormalizeDeploymentSide(pair.Key),
                pair => (IReadOnlyDictionary<WorldSiteAttackDirection, IReadOnlyList<WorldSiteDeploymentCell>>)(pair.Value ??
                    new Dictionary<WorldSiteAttackDirection, IReadOnlyList<WorldSiteDeploymentCell>>()));
        AuthoredDeploymentZoneSurfaceCount = DeploymentZoneCandidatesByFaction.Values
            .Concat(DeploymentZoneCandidatesBySide.Values)
            .SelectMany(directionMap => directionMap.Values)
            .SelectMany(candidates => candidates)
            .Select(candidate => new GridSurfacePosition(candidate.Cell.X, candidate.Cell.Y, candidate.Height))
            .Distinct()
            .Count();
    }

    public string SiteId { get; }
    public int CandidateSurfaceCount { get; }
    public int AuthoredDeploymentZoneSurfaceCount { get; }
    public IReadOnlyDictionary<WorldSiteAttackDirection, IReadOnlyList<WorldSiteDeploymentCell>> CandidatesByDirection { get; }
    public IReadOnlyDictionary<string, IReadOnlyDictionary<WorldSiteAttackDirection, IReadOnlyList<WorldSiteDeploymentCell>>> DeploymentZoneCandidatesByFaction { get; }
    public IReadOnlyDictionary<SemanticDeploymentSide, IReadOnlyDictionary<WorldSiteAttackDirection, IReadOnlyList<WorldSiteDeploymentCell>>> DeploymentZoneCandidatesBySide { get; }

    public IReadOnlyList<WorldSiteDeploymentCell> GetCandidates(WorldSiteAttackDirection direction)
    {
        if (CandidatesByDirection.TryGetValue(direction, out IReadOnlyList<WorldSiteDeploymentCell> candidates) &&
            candidates.Count > 0)
        {
            return candidates;
        }

        return CandidatesByDirection.TryGetValue(WorldSiteAttackDirection.Any, out IReadOnlyList<WorldSiteDeploymentCell> anyCandidates)
            ? anyCandidates
            : System.Array.Empty<WorldSiteDeploymentCell>();
    }

    public IReadOnlyList<WorldSiteDeploymentCell> GetDeploymentZoneCandidates(string factionId, WorldSiteAttackDirection direction)
    {
        // Authored deployment zones are a battle-preparation constraint layered on top of
        // the full placement cache. Older consumers can keep using GetCandidates without
        // being accidentally restricted to the new start zones.
        if (TryGetAuthoredDeploymentZoneCandidates(NormalizeFactionKey(factionId), direction, out IReadOnlyList<WorldSiteDeploymentCell> factionCandidates))
        {
            return factionCandidates;
        }

        if (TryGetAuthoredDeploymentZoneCandidatesForSide(SemanticDeploymentSide.Any, direction, out IReadOnlyList<WorldSiteDeploymentCell> sideCandidates))
        {
            return sideCandidates;
        }

        if (TryGetAuthoredDeploymentZoneCandidates("", direction, out IReadOnlyList<WorldSiteDeploymentCell> sharedCandidates))
        {
            return sharedCandidates;
        }

        return GetCandidates(direction);
    }

    public IReadOnlyList<WorldSiteDeploymentCell> GetDeploymentZoneCandidatesForSide(
        SemanticDeploymentSide deploymentSide,
        string factionId,
        WorldSiteAttackDirection direction)
    {
        // Exact faction buckets are kept as an optional override/compatibility path.
        // Normal map authoring should use DeploymentSide so maps stay reusable across factions.
        if (TryGetAuthoredDeploymentZoneCandidates(NormalizeFactionKey(factionId), direction, out IReadOnlyList<WorldSiteDeploymentCell> factionCandidates))
        {
            return factionCandidates;
        }

        if (TryGetAuthoredDeploymentZoneCandidatesForSide(NormalizeDeploymentSide(deploymentSide), direction, out IReadOnlyList<WorldSiteDeploymentCell> sideCandidates))
        {
            return sideCandidates;
        }

        if (TryGetAuthoredDeploymentZoneCandidatesForSide(SemanticDeploymentSide.Any, direction, out IReadOnlyList<WorldSiteDeploymentCell> sharedSideCandidates))
        {
            return sharedSideCandidates;
        }

        if (TryGetAuthoredDeploymentZoneCandidates("", direction, out IReadOnlyList<WorldSiteDeploymentCell> sharedFactionCandidates))
        {
            return sharedFactionCandidates;
        }

        return GetCandidates(direction);
    }

    public bool HasAuthoredDeploymentZoneForSide(SemanticDeploymentSide deploymentSide, string factionId)
    {
        return HasAnyAuthoredDeploymentZoneCandidatesByFaction(NormalizeFactionKey(factionId)) ||
               HasAnyAuthoredDeploymentZoneCandidatesBySide(NormalizeDeploymentSide(deploymentSide)) ||
               HasAnyAuthoredDeploymentZoneCandidatesBySide(SemanticDeploymentSide.Any) ||
               HasAnyAuthoredDeploymentZoneCandidatesByFaction("");
    }

    private bool TryGetAuthoredDeploymentZoneCandidates(
        string factionId,
        WorldSiteAttackDirection direction,
        out IReadOnlyList<WorldSiteDeploymentCell> candidates)
    {
        candidates = System.Array.Empty<WorldSiteDeploymentCell>();
        if (!DeploymentZoneCandidatesByFaction.TryGetValue(NormalizeFactionKey(factionId), out IReadOnlyDictionary<WorldSiteAttackDirection, IReadOnlyList<WorldSiteDeploymentCell>> directionMap))
        {
            return false;
        }

        if (directionMap.TryGetValue(direction, out candidates) &&
            candidates.Count > 0)
        {
            return true;
        }

        if (directionMap.TryGetValue(WorldSiteAttackDirection.Any, out candidates) &&
            candidates.Count > 0)
        {
            return true;
        }

        candidates = System.Array.Empty<WorldSiteDeploymentCell>();
        return false;
    }

    private bool TryGetAuthoredDeploymentZoneCandidatesForSide(
        SemanticDeploymentSide deploymentSide,
        WorldSiteAttackDirection direction,
        out IReadOnlyList<WorldSiteDeploymentCell> candidates)
    {
        candidates = System.Array.Empty<WorldSiteDeploymentCell>();
        if (!DeploymentZoneCandidatesBySide.TryGetValue(NormalizeDeploymentSide(deploymentSide), out IReadOnlyDictionary<WorldSiteAttackDirection, IReadOnlyList<WorldSiteDeploymentCell>> directionMap))
        {
            return false;
        }

        if (directionMap.TryGetValue(direction, out candidates) &&
            candidates.Count > 0)
        {
            return true;
        }

        if (directionMap.TryGetValue(WorldSiteAttackDirection.Any, out candidates) &&
            candidates.Count > 0)
        {
            return true;
        }

        candidates = System.Array.Empty<WorldSiteDeploymentCell>();
        return false;
    }

    private bool HasAnyAuthoredDeploymentZoneCandidatesByFaction(string factionId)
    {
        return DeploymentZoneCandidatesByFaction.TryGetValue(NormalizeFactionKey(factionId), out IReadOnlyDictionary<WorldSiteAttackDirection, IReadOnlyList<WorldSiteDeploymentCell>> directionMap) &&
               directionMap.Values.Any(candidates => candidates.Count > 0);
    }

    private bool HasAnyAuthoredDeploymentZoneCandidatesBySide(SemanticDeploymentSide deploymentSide)
    {
        return DeploymentZoneCandidatesBySide.TryGetValue(NormalizeDeploymentSide(deploymentSide), out IReadOnlyDictionary<WorldSiteAttackDirection, IReadOnlyList<WorldSiteDeploymentCell>> directionMap) &&
               directionMap.Values.Any(candidates => candidates.Count > 0);
    }

    private static string NormalizeFactionKey(string factionId)
    {
        return factionId?.Trim() ?? "";
    }

    private static SemanticDeploymentSide NormalizeDeploymentSide(SemanticDeploymentSide deploymentSide)
    {
        return deploymentSide switch
        {
            SemanticDeploymentSide.Player => SemanticDeploymentSide.Player,
            SemanticDeploymentSide.Enemy => SemanticDeploymentSide.Enemy,
            _ => SemanticDeploymentSide.Any
        };
    }
}
