namespace Rpg.Runtime.Battle.AI;

public sealed class BattleRuntimeAiTargetCandidateFacts
{
    public string ActorId { get; init; } = "";
    public int SelectionTier { get; init; } = int.MaxValue;
    public int OrthogonalAttackGap { get; init; } = int.MaxValue;
    public int GridGap { get; init; } = int.MaxValue;
    public int CenterManhattanDistance { get; init; } = int.MaxValue;
    public int HitPoints { get; init; } = int.MaxValue;
    public int TravelCost { get; init; } = int.MaxValue;
    public bool IsImmediateAttackOpportunity { get; init; }
    public bool IsRetainedTarget { get; init; }
    public bool IsRouteBlockingObjective { get; init; }
}

public static class BattleRuntimeAiTargetSelectionPolicy
{
    public const string Default = "default";
    public const string FocusFire = "focus_fire";
    public const string HoldLine = "hold_line";
    public const string PlanScoped = "plan_scoped";
    public const string MoveFirstPlanScoped = "move_first_plan_scoped";
    public const string RegionScoped = "region_scoped";
    public const string CombatZoneScoped = "combat_zone_scoped";
}
