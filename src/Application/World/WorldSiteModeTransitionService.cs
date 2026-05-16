using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;

namespace Rpg.Application.World;

public sealed class WorldSiteModeTransitionService
{
    private const string ExplorationReadyPauseReason = "exploration_ready";
    private const string ExplorationBattlePauseReason = "exploration_battle";
    private const string ExplorationEncounterResolvedPauseReason = "exploration_encounter_resolved";
    private const string ExplorationEncounterFailedPauseReason = "exploration_encounter_failed";
    private const string ExplorationSiteClearedPauseReason = "exploration_site_cleared";
    private const string ExplorationRetreatPauseReason = "exploration_retreat";

    public GameEvent EnterAlert(WorldSiteState site, int tick, string reason, string triggerId = "")
    {
        return SetMode(site, WorldSiteMode.Alert, tick, reason, triggerId);
    }

    public GameEvent EnterExploration(WorldSiteState site, int tick, string reason, string triggerId = "")
    {
        PrepareExplorationPause(site, ExplorationReadyPauseReason, clearAlert: true, clearPath: false);
        return SetModeOrExplorationStateChanged(site, WorldSiteMode.Alert, tick, reason, triggerId);
    }

    public GameEvent EnterWartime(WorldSiteState site, int tick, string reason, string triggerId = "")
    {
        return SetMode(site, WorldSiteMode.Wartime, tick, reason, triggerId);
    }

    public GameEvent EnterBattleFromExploration(WorldSiteState site, int tick, string reason, string triggerId = "")
    {
        PrepareExplorationPause(site, ExplorationBattlePauseReason, clearAlert: false, clearPath: true);
        return SetModeOrExplorationStateChanged(site, WorldSiteMode.Wartime, tick, reason, triggerId);
    }

    public GameEvent EnterAftermath(WorldSiteState site, int tick, string reason, string triggerId = "")
    {
        return SetMode(site, WorldSiteMode.Aftermath, tick, reason, triggerId);
    }

    public GameEvent EnterPeacetime(WorldSiteState site, int tick, string reason, string triggerId = "")
    {
        return SetMode(site, WorldSiteMode.Peacetime, tick, reason, triggerId);
    }

    public GameEvent RestoreMode(WorldSiteState site, WorldSiteMode mode, int tick, string reason, string triggerId = "")
    {
        return SetMode(site, mode, tick, reason, triggerId);
    }

    public GameEvent ReturnToExplorationAfterEncounter(WorldSiteState site, int tick, bool victory, string triggerId = "")
    {
        PrepareExplorationPause(
            site,
            victory ? ExplorationEncounterResolvedPauseReason : ExplorationEncounterFailedPauseReason,
            clearAlert: victory,
            clearPath: true);
        return SetModeOrExplorationStateChanged(site, WorldSiteMode.Alert, tick, victory ? "exploration_encounter_resolved" : "exploration_encounter_failed", triggerId);
    }

    public GameEvent CaptureFromExploration(WorldSiteState site, int tick, string triggerId = "")
    {
        PrepareExplorationPause(site, ExplorationSiteClearedPauseReason, clearAlert: true, clearPath: true);
        site?.Exploration?.PatrolUnits?.Clear();
        return SetModeOrExplorationStateChanged(site, WorldSiteMode.Aftermath, tick, ExplorationSiteClearedPauseReason, triggerId);
    }

    public GameEvent RetreatFromExplorationAlert(WorldSiteState site, int tick, string triggerId = "")
    {
        PrepareExplorationPause(site, ExplorationRetreatPauseReason, clearAlert: true, clearPath: true);
        return BuildExplorationStateChangedEvent(site, tick, ExplorationRetreatPauseReason, triggerId);
    }

    public void ClearAftermathSites(StrategicWorldState state, WorldTickResult result)
    {
        if (state == null || result == null)
        {
            return;
        }

        foreach (WorldSiteState site in state.SiteStates.Values)
        {
            if (site.SiteMode != WorldSiteMode.Aftermath)
            {
                continue;
            }

            if (site.LastModeChangedTick >= state.WorldTick - 1)
            {
                continue;
            }

            GameEvent gameEvent = site.PendingThreatIds.Count > 0
                ? EnterAlert(site, state.WorldTick, "aftermath_cleared")
                : EnterPeacetime(site, state.WorldTick, "aftermath_cleared");
            AddEvent(result, gameEvent);
        }
    }

    public static void AddEvent(WorldActionResult result, GameEvent gameEvent)
    {
        if (result != null && gameEvent != null)
        {
            result.Events.Add(gameEvent);
        }
    }

    public static void AddEvent(WorldTickResult result, GameEvent gameEvent)
    {
        if (result != null && gameEvent != null)
        {
            result.Events.Add(gameEvent);
        }
    }

    private static void PrepareExplorationPause(WorldSiteState site, string reason, bool clearAlert, bool clearPath)
    {
        if (site?.Exploration == null)
        {
            return;
        }

        site.Exploration.IsSimulationPaused = true;
        site.Exploration.PauseReason = reason ?? "";
        if (clearAlert)
        {
            site.Exploration.ActiveAlertPatrolId = "";
        }

        if (clearPath)
        {
            site.Exploration.PendingPathCellKeys.Clear();
        }
    }

    private static GameEvent SetModeOrExplorationStateChanged(WorldSiteState site, WorldSiteMode mode, int tick, string reason, string triggerId)
    {
        return SetMode(site, mode, tick, reason, triggerId) ??
               BuildExplorationStateChangedEvent(site, tick, reason, triggerId);
    }

    private static GameEvent BuildExplorationStateChangedEvent(WorldSiteState site, int tick, string reason, string triggerId)
    {
        if (site == null)
        {
            return null;
        }

        GameLog.Info(nameof(WorldSiteModeTransitionService), $"SiteExplorationStateChanged site={site.SiteId} mode={site.SiteMode} reason={reason} trigger={triggerId}");
        GameEvent gameEvent = new()
        {
            Kind = "SiteExplorationStateChanged",
            Tick = tick,
            TargetIds = { site.SiteId },
            Payload =
            {
                ["mode"] = site.SiteMode.ToString(),
                ["reason"] = reason ?? "",
                ["pauseReason"] = site.Exploration?.PauseReason ?? ""
            }
        };

        if (!string.IsNullOrWhiteSpace(triggerId))
        {
            gameEvent.Payload["trigger"] = triggerId;
        }

        return gameEvent;
    }

    private static GameEvent SetMode(WorldSiteState site, WorldSiteMode mode, int tick, string reason, string triggerId)
    {
        if (site == null || site.SiteMode == mode)
        {
            return null;
        }

        WorldSiteMode previousMode = site.SiteMode;
        site.SiteMode = mode;
        site.LastModeChangedTick = tick;
        GameLog.Info(nameof(WorldSiteModeTransitionService), $"SiteModeChanged site={site.SiteId} from={previousMode} to={mode} reason={reason} trigger={triggerId}");

        GameEvent gameEvent = new()
        {
            Kind = "SiteModeChanged",
            Tick = tick,
            TargetIds = { site.SiteId },
            Payload =
            {
                ["from"] = previousMode.ToString(),
                ["to"] = mode.ToString(),
                ["reason"] = reason ?? ""
            }
        };

        if (!string.IsNullOrWhiteSpace(triggerId))
        {
            gameEvent.Payload["trigger"] = triggerId;
        }

        return gameEvent;
    }
}
