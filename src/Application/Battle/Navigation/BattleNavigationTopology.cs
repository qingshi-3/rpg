using System.Collections.Generic;
using System.Linq;

namespace Rpg.Application.Battle.Navigation;

public sealed class BattleNavigationTopology
{
    public List<BattleNavigationNode> Nodes { get; set; } = new();
    public List<BattleNavigationEdge> Edges { get; set; } = new();
    public bool HasNodes => Nodes?.Count > 0;

    public BattleNavigationTopology Clone()
    {
        return new BattleNavigationTopology
        {
            Nodes = (Nodes ?? Enumerable.Empty<BattleNavigationNode>())
                .Where(node => node != null)
                .Select(node => new BattleNavigationNode
                {
                    X = node.X,
                    Y = node.Y,
                    Height = node.Height,
                    MoveCost = node.MoveCost
                })
                .ToList(),
            Edges = (Edges ?? Enumerable.Empty<BattleNavigationEdge>())
                .Where(edge => edge != null)
                .Select(edge => new BattleNavigationEdge
                {
                    FromX = edge.FromX,
                    FromY = edge.FromY,
                    FromHeight = edge.FromHeight,
                    ToX = edge.ToX,
                    ToY = edge.ToY,
                    ToHeight = edge.ToHeight,
                    MoveCost = edge.MoveCost,
                    Kind = edge.Kind
                })
                .ToList()
        };
    }
}
