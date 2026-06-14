using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Rpg.Application.World;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Battle.Entities;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.Events;

namespace Rpg.Presentation.World.Sites;

public partial class WorldSiteRoot
{
    private async Task AdvanceBattleGroupRuntimeOnLiveClockAsync(WorldSiteBattleGroupRuntimeResolveResult resolution)
    {
        BattleRuntimeSessionController controller = resolution?.RuntimeController;
        if (controller == null)
        {
            return;
        }

        BattleRuntimeLivePresentationState presentationState = new(_battleRuntimeLivePresentationObserver.BuildRuntimePlaybackEntityMap());
        while (!controller.IsComplete && IsInsideTree())
        {
            double tickSeconds = ResolveRuntimePlaybackTickSeconds();
            await WaitForBattleRuntimeAdvanceGateAsync();
            BattleRuntimeAdvanceResult advance = controller.AdvanceFixedTick(tickSeconds);
            _ = _battleRuntimeLivePresentationObserver.ObserveAsync(advance.Events, presentationState);
            await WaitSiteBattlePresentationSeconds(tickSeconds);
        }

        int pendingBeforeDrain = presentationState.PendingPresentationTaskCount;
        if (pendingBeforeDrain > 0)
        {
            GameLog.Info(
                nameof(WorldSiteRoot),
                $"BattleRuntimePresentationDrainStarted request={resolution?.Request?.RequestId ?? ""} pendingTasks={pendingBeforeDrain}");
        }

        await presentationState.WaitForAllAsync();

        if (pendingBeforeDrain > 0)
        {
            GameLog.Info(
                nameof(WorldSiteRoot),
                $"BattleRuntimePresentationDrainCompleted request={resolution?.Request?.RequestId ?? ""}");
        }

        _unitRoot?.PlayIdleForActiveEntities();
        if (_unitRoot?.HasPendingDefeatedPresentations == true)
        {
            await _unitRoot.WaitForDefeatedPresentationsAsync();
        }
    }
}
