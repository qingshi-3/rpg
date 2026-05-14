using System.Collections.Generic;
using System.Linq;
using Rpg.Presentation.Battle.Actions;

namespace Rpg.Presentation.Battle.Feedback;

public sealed class BattleHitFeedbackPlan
{
    public BattleHitFeedbackPlan(
        IReadOnlyList<string> outlinedTargetIds,
        IReadOnlyList<BattleDamageNumberSpec> damageNumbers)
    {
        OutlinedTargetIds = outlinedTargetIds ?? System.Array.Empty<string>();
        DamageNumbers = damageNumbers ?? System.Array.Empty<BattleDamageNumberSpec>();
    }

    public IReadOnlyList<string> OutlinedTargetIds { get; }
    public IReadOnlyList<BattleDamageNumberSpec> DamageNumbers { get; }
}

public readonly record struct BattleDamageNumberSpec(string TargetId, string Text);

public static class BattleHitFeedbackPlanner
{
    public static BattleHitFeedbackPlan Build(IEnumerable<BattleDamageEvent> damageEvents)
    {
        BattleDamageEvent[] events = damageEvents?
            .Where(damage => damage != null && !string.IsNullOrWhiteSpace(damage.TargetId))
            .ToArray() ?? System.Array.Empty<BattleDamageEvent>();

        string[] outlinedTargetIds = events
            .Select(damage => damage.TargetId)
            .Distinct()
            .ToArray();

        BattleDamageNumberSpec[] damageNumbers = events
            .Where(damage => damage.DamageApplied > 0)
            .Select(damage => new BattleDamageNumberSpec(damage.TargetId, $"-{damage.DamageApplied}"))
            .ToArray();

        return new BattleHitFeedbackPlan(outlinedTargetIds, damageNumbers);
    }
}
