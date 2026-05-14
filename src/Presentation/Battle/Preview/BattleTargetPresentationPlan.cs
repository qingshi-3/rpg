using System.Collections.Generic;
using System.Linq;

namespace Rpg.Presentation.Battle.Preview;

public sealed class BattleTargetPresentationPlan
{
    private BattleTargetPresentationPlan(IReadOnlyList<string> targetEntityIds)
    {
        TargetEntityIds = targetEntityIds ?? System.Array.Empty<string>();
    }

    public IReadOnlyList<string> TargetEntityIds { get; }

    public bool ShowTargetGridCells => false;

    public static BattleTargetPresentationPlan Build(IEnumerable<string> targetEntityIds)
    {
        string[] ids = targetEntityIds?
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToArray() ?? System.Array.Empty<string>();

        return new BattleTargetPresentationPlan(ids);
    }
}
