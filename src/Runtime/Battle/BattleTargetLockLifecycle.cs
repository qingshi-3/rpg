using System.Collections.Generic;
using System.Linq;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Tactics;

namespace Rpg.Runtime.Battle;

internal static class BattleTargetLockLifecycle
{
    // This narrow boundary owns engagement-exit target lock clears only.
    internal static void ClearForEngagementExits(
        BattleRuntimeActor[] livingCorps,
        IReadOnlyList<BattleEvent> engagementEvents)
    {
        string[] exitedGroups = (engagementEvents ?? System.Array.Empty<BattleEvent>())
            .Where(item => item.Kind == BattleEventKind.BattleGroupEngagementStateChanged &&
                           item.ReasonCode == BattleGroupTacticalReasonCode.EngagementExitNoGroupPerception)
            .Select(item => item.BattleGroupId ?? "")
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(System.StringComparer.Ordinal)
            .ToArray();
        if (exitedGroups.Length == 0)
        {
            return;
        }

        foreach (BattleRuntimeActor actor in livingCorps.Where(actor => exitedGroups.Contains(actor.BattleGroupId ?? "")))
        {
            actor.TargetActorId = "";
        }
    }
}
