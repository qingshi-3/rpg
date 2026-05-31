using Rpg.Application.Battle;

namespace Rpg.Runtime.Battle.Tactics;

public sealed class BattleGroupTacticalPolicySettings
{
    public const int DefaultTemporaryRegionRefreshTicks = 5;
    public const int DefaultLocalPerceptionRange = BattlePerceptionPolicy.DefaultLocalPerceptionRange;
    public const int DefaultLocalCombatMaxCells = 64;
    public const int DefaultDisengageGraceTicks = 1;
    public const int MinimumRegionWidth = 1;
    public const int MinimumRegionHeight = 1;
}
