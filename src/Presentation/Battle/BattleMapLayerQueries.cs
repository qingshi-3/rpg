using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Domain.Battle.Grid;

namespace Rpg.Presentation.Battle;

public static class BattleMapLayerQueries
{
    public static BattleMapLayer FindCoordinateLayer(BattleMapView battleMapView)
    {
        return EnumerateBattleMapLayers(battleMapView)
            .FirstOrDefault(layer => layer.Role == LayerRole.Foundation)
            ?? EnumerateBattleMapLayers(battleMapView).FirstOrDefault();
    }

    public static BattleMapLayer FindLowestFoundationLayer(BattleMapView battleMapView)
    {
        return EnumerateBattleMapLayers(battleMapView)
            .Where(layer => layer.Role == LayerRole.Foundation)
            .OrderBy(layer => layer.Height)
            .FirstOrDefault();
    }

    public static IEnumerable<BattleMapLayer> EnumerateBattleMapLayers(Node root)
    {
        foreach (Node child in root.GetChildren())
        {
            if (child is BattleMapLayer layer)
            {
                yield return layer;
            }

            foreach (BattleMapLayer descendant in EnumerateBattleMapLayers(child))
            {
                yield return descendant;
            }
        }
    }
}
