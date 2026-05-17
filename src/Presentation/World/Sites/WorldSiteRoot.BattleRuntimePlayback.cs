using System.Collections.Generic;
using System.Threading.Tasks;
using Rpg.Domain.Battle.Grid;
using Rpg.Presentation.Battle.Actions;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Battle.Rules;
using Rpg.Runtime.Battle.Events;

namespace Rpg.Presentation.World.Sites;

public partial class WorldSiteRoot
{
    private async Task PlayRuntimeMovementEventAsync(
        BattleEvent runtimeEvent,
        IReadOnlyDictionary<string, BattleEntity> entitiesByRuntimeActor)
    {
        if (!entitiesByRuntimeActor.TryGetValue(runtimeEvent.ActorId ?? "", out BattleEntity actor) ||
            !runtimeEvent.HasMovementCells)
        {
            return;
        }

        GridOccupantComponent actorGrid = actor.GetComponent<GridOccupantComponent>();
        if (actorGrid == null)
        {
            return;
        }

        GridSurfacePosition nextStep = new(runtimeEvent.ToGridX, runtimeEvent.ToGridY, runtimeEvent.ToGridHeight);
        _unitRoot.MoveEntityTo(actor, new[] { actorGrid.SurfacePosition, nextStep });
        await WaitSiteBattlePresentationSeconds(_unitRoot.UnitMoveDuration);
    }

    private async Task PlayRuntimeDamageEventAsync(
        BattleEvent runtimeEvent,
        IReadOnlyDictionary<string, BattleEntity> entitiesByRuntimeActor)
    {
        if (!entitiesByRuntimeActor.TryGetValue(runtimeEvent.ActorId ?? "", out BattleEntity actor) ||
            !entitiesByRuntimeActor.TryGetValue(runtimeEvent.TargetId ?? "", out BattleEntity target))
        {
            return;
        }

        int damage = System.Math.Max(0, -runtimeEvent.CorpsStrengthDelta);
        HealthComponent health = target.GetComponent<HealthComponent>();
        int applied = health?.ApplyDamage(damage, actor) ?? 0;
        bool defeated = BattleRuleQueries.IsDefeated(target);
        _unitRoot.PlayActionResultAnimation(BattleActionResult.AttackSucceeded(
            actor,
            target,
            applied,
            defeated,
            runtimeEvent.ReasonCode));
        if (defeated)
        {
            _unitRoot.MarkEntityDefeated(target);
        }

        await WaitSiteBattlePresentationSeconds(0.42);
    }
}
