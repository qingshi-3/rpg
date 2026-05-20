using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Definitions.Maps;

namespace Rpg.Application.Maps;

public sealed class SemanticMapMarkerExtractor
{
    public SemanticMapMarkerExtractionResult Extract(Node root, string mapId)
    {
        var result = new SemanticMapMarkerExtractionResult();
        if (root == null)
        {
            return result;
        }

        var usedIds = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (ISemanticMapMarkerSource source in EnumerateSources(root))
        {
            if (!source.TryResolveSemanticMarkerData(mapId, out SemanticMapMarkerData marker, out string failureReason))
            {
                result.Diagnostics.Add(string.IsNullOrWhiteSpace(failureReason)
                    ? "semantic_marker_invalid"
                    : failureReason);
                continue;
            }

            if (marker == null)
            {
                result.Diagnostics.Add("semantic_marker_missing_data");
                continue;
            }

            if (string.IsNullOrWhiteSpace(marker.MarkerId))
            {
                result.Diagnostics.Add($"semantic_marker_missing_id source={marker.SourcePath}");
                continue;
            }

            if (!usedIds.Add(marker.MarkerId))
            {
                result.Diagnostics.Add($"semantic_marker_duplicate id={marker.MarkerId} source={marker.SourcePath}");
                // Deployment routing is side/footprint based; copied deployment
                // labels should warn authors without deleting valid zone cells.
                if (marker.MarkerType != SemanticMapMarkerType.DeploymentZone)
                {
                    continue;
                }
            }

            result.Markers.Add(marker);
        }

        return result;
    }

    private static IEnumerable<ISemanticMapMarkerSource> EnumerateSources(Node root)
    {
        if (root is ISemanticMapMarkerSource source)
        {
            yield return source;
        }

        foreach (Node child in root.GetChildren().Cast<Node>())
        {
            foreach (ISemanticMapMarkerSource childSource in EnumerateSources(child))
            {
                yield return childSource;
            }
        }
    }
}
