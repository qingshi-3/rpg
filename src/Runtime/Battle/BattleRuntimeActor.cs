using System.Collections.Generic;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle.AI;
using Rpg.Runtime.Battle.Navigation;

namespace Rpg.Runtime.Battle;

public sealed class BattleRuntimeActor
{
    public string ActorId { get; set; } = "";
    public string BattleGroupId { get; set; } = "";
    public string FactionId { get; set; } = "";
    public string UnitDefinitionId { get; set; } = "";
    public string SourceForceId { get; set; } = "";
    public string SourceHeroId { get; set; } = "";
    public string SourceStateId { get; set; } = "";
    public BattleRuntimeActorKind Kind { get; set; }
    public int HitPoints { get; set; } = 1;
    public int Position { get; set; }
    public int GridX { get; set; }
    public int GridY { get; set; }
    public int GridHeight { get; set; }
    public int FootprintWidth { get; set; } = 1;
    public int FootprintHeight { get; set; } = 1;
    public BattleRuntimeActorMotionState MotionState { get; set; } = BattleRuntimeActorMotionState.Anchored;
    // Runtime phase is the decision authority for tick resolution.
    public BattleRuntimeActorPhase Phase { get; set; } = BattleRuntimeActorPhase.AnchoredDecision;
    public string TargetActorId { get; set; } = "";
    public bool HasReservedGridCell { get; set; }
    public int ReservedGridX { get; set; }
    public int ReservedGridY { get; set; }
    public int ReservedGridHeight { get; set; }
    public bool HasMovementTarget { get; set; }
    public int MovementFromGridX { get; set; }
    public int MovementFromGridY { get; set; }
    public int MovementFromGridHeight { get; set; }
    public int MovementToGridX { get; set; }
    public int MovementToGridY { get; set; }
    public int MovementToGridHeight { get; set; }
    // Local-combat movement uses this short memory to avoid three-cell pacing
    // loops while still allowing fresh decisions after a real target change.
    public bool HasMovementBacktrackGuardCell { get; set; }
    public int MovementBacktrackGuardGridX { get; set; }
    public int MovementBacktrackGuardGridY { get; set; }
    public int MovementBacktrackGuardGridHeight { get; set; }
    public bool HasSecondaryMovementBacktrackGuardCell { get; set; }
    public int SecondaryMovementBacktrackGuardGridX { get; set; }
    public int SecondaryMovementBacktrackGuardGridY { get; set; }
    public int SecondaryMovementBacktrackGuardGridHeight { get; set; }
    // Steering memory belongs to the current movement intent. It lets Runtime
    // follow static obstacle edges consistently without owning a full path.
    public BattleLocalSteeringMode MovementSteeringMode { get; set; } = BattleLocalSteeringMode.SeekGoal;
    public int MovementSteeringSide { get; set; }
    public int MovementSteeringBestDistance { get; set; } = int.MaxValue;
    public int MovementSteeringBudgetRemaining { get; set; }
    public string MovementSteeringIntentKey { get; set; } = "";
    public double MovementStartedAtSeconds { get; set; }
    public double MovementDurationSeconds { get; set; }
    public double MovementProgress { get; set; }
    // Runtime-only movement intent captured when a segment is committed. Same-tick
    // continuation may reuse it, but Presentation never reads it as path truth.
    public bool HasMovementIntentSnapshot { get; set; }
    public BattleRuntimeAiActionKind MovementIntentKind { get; set; } = BattleRuntimeAiActionKind.Hold;
    public string MovementIntentTargetActorId { get; set; } = "";
    public string MovementIntentObjectiveZoneId { get; set; } = "";
    public string MovementIntentRegionId { get; set; } = "";
    public string MovementIntentCommandId { get; set; } = "";
    public string MovementIntentReasonCode { get; set; } = "";
    public string MovementIntentLocalCombatSituationId { get; set; } = "";
    public bool HasMovementIntentCombatSlot { get; set; }
    public int MovementIntentCombatSlotX { get; set; }
    public int MovementIntentCombatSlotY { get; set; }
    public int MovementIntentCombatSlotHeight { get; set; }
    internal BattleCombatSlotKind MovementIntentCombatSlotKind { get; set; } = BattleCombatSlotKind.Support;
    public double MovementIntentSegmentDurationSeconds { get; set; }
    public int AttackRange { get; set; } = 1;
    public int AttackDamage { get; set; } = 1;
    public double AttackSpeed { get; set; } = BattleAttackSpeedPolicy.DefaultAttackSpeed;
    public double AttackCharge { get; set; } = 1.0;
    public double MoveStepSeconds { get; set; } = 0.16;
    public double AttackActionSeconds { get; set; } = 1.2;
    public double AttackImpactDelaySeconds { get; set; } = 0.66;
    // Basic attacks lock their target and cells at action start. Runtime impact
    // later reads this payload instead of re-resolving presentation or AI facts.
    public string CurrentBasicAttackTargetActorId { get; set; } = "";
    public int CurrentBasicAttackDamage { get; set; }
    public bool CurrentBasicAttackImpactApplied { get; set; }
    public double CurrentBasicAttackStartedAtSeconds { get; set; }
    public double CurrentBasicAttackImpactAtSeconds { get; set; }
    public double CurrentBasicAttackEndsAtSeconds { get; set; }
    public int CurrentBasicAttackActorGridX { get; set; }
    public int CurrentBasicAttackActorGridY { get; set; }
    public int CurrentBasicAttackActorGridHeight { get; set; }
    public int CurrentBasicAttackTargetGridX { get; set; }
    public int CurrentBasicAttackTargetGridY { get; set; }
    public int CurrentBasicAttackTargetGridHeight { get; set; }
    public string CurrentSkillActionId { get; set; } = "";
    public string CurrentSkillDefinitionId { get; set; } = "";
    public string CurrentSkillGrantedSkillId { get; set; } = "";
    public string CurrentSkillLoadoutSlotId { get; set; } = "";
    public string CurrentSkillOwnerHeroId { get; set; } = "";
    public string CurrentSkillSourceCommandId { get; set; } = "";
    public string CurrentSkillTargetActorId { get; set; } = "";
    // Accepted skill orders wait with the caster, not in a center resolver queue.
    internal List<BattleRuntimePendingHeroSkillCommand> PendingAbilityOrders { get; } = new();
    // Active channels stay with the caster ability runtime. A tick-wide shared
    // commit buffer still applies their damage so opposing channels resolve together.
    internal List<BattleRuntimeActiveChannel> ActiveChannels { get; } = new();
    // Active skill targeting is locked at command acceptance so delayed impacts,
    // pause/resume, and recovery do not need to read back a removed pending command.
    public bool CurrentSkillHasTargetGrid { get; set; }
    public int CurrentSkillTargetGridX { get; set; }
    public int CurrentSkillTargetGridY { get; set; }
    public int CurrentSkillTargetGridHeight { get; set; }
    public string CurrentSkillSelectedSpatialMarkId { get; set; } = "";
    public double CurrentSkillImpactAtSeconds { get; set; }
    public bool CurrentSkillImpactApplied { get; set; }
    // Central runtime time, in seconds, when this actor may submit its next action intent.
    public double ActionReadyAtSeconds { get; set; }
    public int ActionLockTicksRemaining { get; set; }
    public string ActionLockReason { get; set; } = "";
    // Commander fields below are non-authoritative execution caches. The group
    // tactical store overwrites them before decisions so stale actor copies cannot
    // select command, beacon, plan, or objective behavior.
    public string CommandId { get; set; } = "";
    public string ActiveDestinationBeaconId { get; set; } = "";
    public int ActiveDestinationBeaconRevision { get; set; }
    public int ActiveDestinationBeaconGridX { get; set; }
    public int ActiveDestinationBeaconGridY { get; set; }
    public int ActiveDestinationBeaconGridHeight { get; set; }
    public string ActiveDestinationBeaconCommandId { get; set; } = "";
    // Objective caches remain available to existing actor-local movement solvers;
    // their source of truth is the owning BattleGroupTacticalState.
    public BattleEngagementRule EngagementRule { get; set; } = BattleEngagementRule.AttackFirst;
    public BattleGroupPlanRuntimeState PlanState { get; set; } = BattleGroupPlanRuntimeState.SensingContact;
    public bool HasObjectiveAnchor { get; set; }
    public string ObjectiveZoneId { get; set; } = "";
    public int ObjectiveGridX { get; set; }
    public int ObjectiveGridY { get; set; }
    public int ObjectiveGridHeight { get; set; }
    public int ObjectiveWidth { get; set; } = 1;
    public int ObjectiveHeight { get; set; } = 1;
    // Keeps blocked movement diagnosable without letting presentation infer combat truth.
    public int ConsecutiveAdvanceFailures { get; set; }
    public string LastAdvanceFailureReason { get; set; } = "";
    // Retreat completion is an execution fact derived from commander state.
    // It removes the actor from live decisions without treating survival as defeat.
    public bool HasRetreated { get; set; }
}
