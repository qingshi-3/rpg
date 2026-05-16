using System.Collections.Generic;
using System.Linq;
using Rpg.Domain.World;

namespace Rpg.Application.World;

public sealed class WorldSiteRuntimeDeploymentCache
{
    public WorldSiteRuntimeDeploymentCache(
        string siteId,
        int candidateSurfaceCount,
        IReadOnlyDictionary<WorldSiteAttackDirection, IReadOnlyList<WorldSiteDeploymentCell>> candidatesByDirection)
    {
        SiteId = siteId ?? "";
        CandidateSurfaceCount = System.Math.Max(0, candidateSurfaceCount);
        CandidatesByDirection = (candidatesByDirection ?? new Dictionary<WorldSiteAttackDirection, IReadOnlyList<WorldSiteDeploymentCell>>())
            .ToDictionary(
                pair => pair.Key,
                pair => pair.Value ?? System.Array.Empty<WorldSiteDeploymentCell>());
    }

    public string SiteId { get; }
    public int CandidateSurfaceCount { get; }
    public IReadOnlyDictionary<WorldSiteAttackDirection, IReadOnlyList<WorldSiteDeploymentCell>> CandidatesByDirection { get; }

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
}
