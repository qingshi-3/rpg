using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Snapshots;
using Rpg.Presentation.Battle.Entities;

namespace Rpg.Presentation.World.Sites;

internal static class BattlePreparationCommandSelectionPresenter
{
    public static int Apply(
        BattleUnitRoot unitRoot,
        BattleRuntimeCommandGroupView selectedGroup,
        string selectedGroupKey)
    {
        if (unitRoot == null)
        {
            return 0;
        }

        if (selectedGroup == null || string.IsNullOrWhiteSpace(selectedGroupKey))
        {
            unitRoot.ClearCommandSelection();
            return 0;
        }

        HashSet<string> entityIds = BuildPresentationEntityIds(selectedGroup.Forces);
        return unitRoot.SetCommandSelectionByEntityIds(entityIds, new HashSet<string>(StringComparer.Ordinal));
    }

    private static HashSet<string> BuildPresentationEntityIds(IEnumerable<BattleForceRequest> forces)
    {
        return BattleRuntimeActorIdentity.BuildPresentationEntityToRuntimeActorMap(forces, Enumerable.Empty<BattleForceRequest>())
            .Keys
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);
    }
}
