using Rpg.Application.Battle;

namespace Rpg.Runtime.Battle.Tactics;

public sealed class BattleGroupTacticalPolicySettings
{
    // Temporary regions follow moving enemy clusters only at low frequency so
    // non-engaged region movement stays readable instead of re-aiming every few cells.
    public const int DefaultTemporaryRegionRefreshTicks = 50;
    public const int DefaultLocalPerceptionRange = BattlePerceptionPolicy.DefaultLocalPerceptionRange;
    public const int DefaultLocalCombatMaxCells = 64;
    public const int DefaultDisengageGraceTicks = 1;
    // Active runtime actions can briefly outlive direct perception. Keep the
    // combat assignment through that short gap so target locks and zones are not
    // cleared while movement, attacks, or skills are still resolving.
    public const int DefaultActiveCombatDisengageGraceTicks = 3;
    public const int MinimumRegionWidth = 1;
    public const int MinimumRegionHeight = 1;
}
