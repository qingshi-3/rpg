namespace Rpg.Application.Battle.Snapshots;

public enum BattleSkillCommandChannel
{
    Hero,
    Corps,
    Combined
}

public enum BattleSkillType
{
    Active,
    Passive,
    Toggle
}

public enum BattleSkillInputFlow
{
    ImmediateSelf,
    SelectActor,
    SelectCell,
    SelectActorOrCell,
    SelectMarkThenLandingCell,
    SelectDirectionArea
}

public enum BattleSkillTargetKind
{
    None,
    Actor,
    Cell,
    ActorOrCell,
    Direction,
    Mark
}

public enum BattleSkillRangeMetric
{
    Manhattan,
    Chebyshev,
    Euclidean
}

public enum BattleSkillAreaShape
{
    SingleActor,
    SingleCell,
    Line,
    Cone,
    CircleRadius,
    GridRadius
}

public enum BattleSkillDirectionMode
{
    None,
    FreeAngle,
    EightWay,
    FourWay,
    ForwardArc
}

public enum BattleSkillCostPayTiming
{
    CommandAccepted,
    CastStart,
    EffectRelease,
    SuccessfulCompletion
}

public enum BattleSkillRefundPolicy
{
    Never,
    FailedBeforeRelease,
    InterruptedBeforeRelease,
    InvalidatedBeforeRelease
}

public enum BattleSkillCooldownStart
{
    CommandAccepted,
    CastStart,
    EffectRelease,
    SuccessfulCompletion
}

public enum BattleSkillDamageType
{
    Physical,
    Lightning,
    Fire,
    Ice,
    Arcane
}

public enum BattleSkillMarkKind
{
    None,
    ThunderMark
}
