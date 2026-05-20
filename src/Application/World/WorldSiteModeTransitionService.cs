using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;

namespace Rpg.Application.World;

public sealed class WorldSiteModeTransitionService
{
    public GameEvent EnterAlert(WorldSiteState site, int tick, string reason, string triggerId = "")
    {
        return SetMode(site, WorldSiteMode.Alert, tick, reason, triggerId);
    }

    public GameEvent EnterWartime(WorldSiteState site, int tick, string reason, string triggerId = "")
    {
        return SetMode(site, WorldSiteMode.Wartime, tick, reason, triggerId);
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
