namespace Rpg.Runtime.Battle.Effects;

using Rpg.Runtime.Battle.Navigation;

internal sealed class BattleEffectExecutionContext
{
    public string BattleId { get; set; } = "";
    public int RuntimeTick { get; set; } = -1;
    public double RuntimeTimeSeconds { get; set; }
    public string SourceActionId { get; set; } = "";
    public string SourceCommandId { get; set; } = "";
    public string SourceDefinitionId { get; set; } = "";
    public string PresentationProfileId { get; set; } = "";
    public string CastFxProfileId { get; set; } = "";
    public string ImpactFxProfileId { get; set; } = "";
    public string MarkFxProfileId { get; set; } = "";
    public string AreaFxProfileId { get; set; } = "";
    public bool SuppressActorCastFx { get; set; }
    public bool HoldCastAnimationDuringAction { get; set; }
    internal BattleCommitBuffer CommitBuffer { get; set; }
    internal bool DeferEffectDamageCommit { get; set; }
    internal BattleGridCoord? ActorAnchorOverride { get; set; }
    internal BattleGridCoord? TargetAnchorOverride { get; set; }
    public BattleRuntimeState State { get; set; } = new();
    public BattleNavigationGraph NavigationGraph { get; set; }
    public BattleRuntimeActor Actor { get; set; } = new();
    public BattleRuntimeActor Target { get; set; } = new();
    public bool HasTargetGrid { get; set; }
    public int TargetGridX { get; set; }
    public int TargetGridY { get; set; }
    public int TargetGridHeight { get; set; }
    public string SelectedSpatialMarkId { get; set; } = "";
}
