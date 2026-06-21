namespace Rpg.Runtime.Battle;

internal sealed class BattleActorRuntime
{
    internal BattleActorRuntime(BattleRuntimeActor actor)
    {
        Actor = actor;
        ActionController = new BattleActionController(actor);
        MovementController = new BattleMovementController(actor);
        AbilityController = new BattleAbilityController(actor);
        HealthComponent = new BattleHealthComponent(actor);
        EffectReceiver = new BattleEffectReceiver(HealthComponent);
    }

    internal BattleRuntimeActor Actor { get; }

    internal BattleActionController ActionController { get; }

    internal BattleMovementController MovementController { get; }

    internal BattleAbilityController AbilityController { get; }

    internal BattleHealthComponent HealthComponent { get; }

    internal BattleEffectReceiver EffectReceiver { get; }
}
