using System;
using Rpg.Runtime.Battle.Effects;

namespace Rpg.Runtime.Battle;

internal sealed class BattleEffectReceiver
{
    private readonly BattleHealthComponent _health;

    internal BattleEffectReceiver(BattleHealthComponent health)
    {
        _health = health ?? throw new ArgumentNullException(nameof(health));
    }

    internal void ReceiveEffect(
        BattleCommitBuffer commitBuffer,
        BattleEffectExecutionContext context,
        BattleEffectPayload payload)
    {
        if (payload == null)
        {
            return;
        }

        if (payload.EffectKind == Rpg.Application.Battle.Snapshots.BattleSkillEffectKind.Damage)
        {
            ReceiveDamage(commitBuffer, context, payload);
        }
    }

    internal void ReceiveDamage(
        BattleCommitBuffer commitBuffer,
        BattleEffectExecutionContext context,
        BattleEffectPayload payload)
    {
        if (commitBuffer == null || context == null || payload == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(context.Actor?.ActorId) ||
            string.IsNullOrWhiteSpace(_health.Actor?.ActorId))
        {
            return;
        }

        _health.RequestEffectDamage(commitBuffer, context, payload.Amount, payload.EffectKind.ToString());
    }
}
