using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle;
using Rpg.Application.World;
using Rpg.Definitions.World;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.World;

public partial class StrategicWorldRoot
{
    private void SaveWorld()
    {
        _saveService.Save(State, out string message);
        StrategicWorldRuntime.LastNotice = message;
        RefreshAll();
    }

    private void LoadWorld()
    {
        if (_saveService.TryLoad(out StrategicWorldState state, out string message))
        {
            StrategicWorldRuntime.ReplaceState(state);
            _selectedSiteId = "";
            _selectedThreatId = "";
            _selectedOpportunityId = "";
            _worldClockPaused = HasAttackingThreat();
            _worldClockAccumulator = 0.0;
            ResetStrategicRuntimeGate();
        }

        StrategicWorldRuntime.LastNotice = message;
        RefreshAll();
    }

    private void ResetWorld()
    {
        StrategicWorldRuntime.Reset();
        SyncDefinitionMapPositionsFromAnchors();
        RebuildSiteVisualFootprints();
        _selectedSiteId = "";
        _selectedThreatId = "";
        _selectedOpportunityId = "";
        _worldClockPaused = false;
        _worldClockAccumulator = 0.0;
        ResetStrategicRuntimeGate();
        RefreshAll();
    }

    private void ResetStrategicRuntimeGate()
    {
        _runtimeStage = StrategicRuntimeStage.WaitingForNavigation;
        _reportedStrategicNavigationNotSynchronized = false;
    }
}
