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

    internal void ReceiveDamage(
        BattleCommitBuffer commitBuffer,
        BattleEffectExecutionContext context,
        int amount,
        string effectKind)
    {
        if (commitBuffer == null || context == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(context.Actor?.ActorId) ||
            string.IsNullOrWhiteSpace(_health.Actor?.ActorId))
        {
            return;
        }

        _health.RequestEffectDamage(
            commitBuffer,
            context,
            amount,
            string.IsNullOrWhiteSpace(effectKind) ? BattleEffectKindLabels.Damage : effectKind);
    }
}
