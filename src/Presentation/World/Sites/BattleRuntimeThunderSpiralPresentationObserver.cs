using Rpg.Application.Battle.Commands;
using Rpg.Domain.Battle.Grid;
using Rpg.Presentation.Battle.Entities;
using Rpg.Runtime.Battle.Events;

namespace Rpg.Presentation.World.Sites;

internal static class BattleRuntimeThunderSpiralPresentationObserver
{
    internal static bool IsThunderSpiralSkillUsedEvent(BattleEvent runtimeEvent)
    {
        return string.Equals(
            runtimeEvent?.SourceDefinitionId ?? "",
            HeroSkillCommandIds.ThunderSpiralBreakSkillId,
            System.StringComparison.Ordinal);
    }

    internal static double ObserveRuntimeThunderSpiralSkillUsedEvent(
        BattleEvent runtimeEvent,
        BattleEntity actor,
        BattleUnitRoot unitRoot)
    {
        if (unitRoot == null ||
            actor == null ||
            runtimeEvent == null ||
            !runtimeEvent.HasTargetCells)
        {
            return 0;
        }

        return unitRoot.PlayThunderSpiralBreakPresentation(
            actor,
            new GridSurfacePosition(runtimeEvent.TargetGridX, runtimeEvent.TargetGridY, runtimeEvent.TargetGridHeight),
            runtimeEvent.ActionDurationSeconds);
    }
}
