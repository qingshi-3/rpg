using Rpg.Runtime.Battle.Navigation;

namespace Rpg.Runtime.Battle;

internal readonly record struct BattleRuntimeTickStartActorFact(
    BattleRuntimeActor Actor,
    BattleGridCoord Anchor,
    int HitPoints,
    double AttackCharge,
    string TargetActorId,
    string CommandId);
