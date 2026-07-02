namespace Rpg.Definitions.Battle.Skills;

public enum BattleSkillCommandChannelDefinition
{
    Hero,
    Corps,
    Combined
}

public enum BattleSkillTypeDefinition
{
    Active,
    Passive,
    Toggle
}

public enum BattleSkillInputFlowDefinition
{
    ImmediateSelf,
    SelectActor,
    SelectCell,
    SelectActorOrCell,
    SelectMarkThenLandingCell,
    SelectDirectionArea
}

public enum BattleSkillTargetKindDefinition
{
    None,
    Actor,
    Cell,
    ActorOrCell,
    Direction,
    Mark
}

public enum BattleSkillRangeMetricDefinition
{
    Manhattan,
    Chebyshev,
    Euclidean
}

public enum BattleSkillAreaShapeDefinition
{
    SingleActor,
    SingleCell,
    Line,
    Cone,
    CircleRadius,
    GridRadius
}

public enum BattleSkillDirectionModeDefinition
{
    None,
    FreeAngle,
    EightWay,
    FourWay,
    ForwardArc
}

public enum BattleSkillCostPayTimingDefinition
{
    CommandAccepted,
    CastStart,
    EffectRelease,
    SuccessfulCompletion
}

public enum BattleSkillRefundPolicyDefinition
{
    Never,
    FailedBeforeRelease,
    InterruptedBeforeRelease,
    InvalidatedBeforeRelease
}

public enum BattleSkillCooldownStartDefinition
{
    CommandAccepted,
    CastStart,
    EffectRelease,
    SuccessfulCompletion
}

public enum BattleSkillDamageTypeDefinition
{
    Physical,
    Lightning,
    Fire,
    Ice,
    Arcane
}

public enum BattleSkillMarkKindDefinition
{
    None,
    ThunderMark
}
