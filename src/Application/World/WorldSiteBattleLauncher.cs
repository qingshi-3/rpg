using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;

namespace Rpg.Application.World;

public sealed class WorldSiteBattleLaunchRollback
{
    public string SiteId { get; set; } = "";
    public bool HasPreviousSiteMode { get; set; }
    public WorldSiteMode PreviousSiteMode { get; set; } = WorldSiteMode.Peacetime;
}

public sealed class WorldSiteBattleLaunchResult
{
    public bool Success { get; init; }
    public string FailureReason { get; init; } = "";
    public BattleGroupSessionProbeResult ProbeResult { get; init; }
}

public sealed class WorldSiteBattleLauncher
{
    private readonly WorldSiteModeTransitionService _siteModeTransitions;
    private readonly BattleGroupSessionProbeService _battleGroupSessionProbe;

    public WorldSiteBattleLauncher(
        WorldSiteModeTransitionService siteModeTransitions = null,
        BattleGroupSessionProbeService battleGroupSessionProbe = null)
    {
        _siteModeTransitions = siteModeTransitions ?? new WorldSiteModeTransitionService();
        _battleGroupSessionProbe = battleGroupSessionProbe;
    }

    public WorldSiteBattleLaunchRollback CaptureRollback(WorldSiteState site)
    {
        WorldSiteBattleLaunchRollback rollback = new()
        {
            SiteId = site?.SiteId ?? ""
        };

        if (site == null)
        {
            return rollback;
        }

        rollback.HasPreviousSiteMode = true;
        rollback.PreviousSiteMode = site.SiteMode;

        return rollback;
    }

    public void ApplyModeTransitionRollbackEvent(
        WorldSiteBattleLaunchRollback rollback,
        IReadOnlyCollection<GameEvent> transitionEvents)
    {
        if (rollback == null || transitionEvents == null)
        {
            return;
        }

        GameEvent modeEvent = transitionEvents.LastOrDefault(gameEvent =>
            gameEvent.Kind == "SiteModeChanged" &&
            gameEvent.TargetIds.Contains(rollback.SiteId) &&
            gameEvent.Payload.TryGetValue("to", out string toMode) &&
            toMode == WorldSiteMode.Wartime.ToString() &&
            gameEvent.Payload.TryGetValue("from", out _));
        if (modeEvent == null ||
            !modeEvent.Payload.TryGetValue("from", out string fromMode) ||
            !Enum.TryParse(fromMode, out WorldSiteMode previousMode))
        {
            return;
        }

        rollback.HasPreviousSiteMode = true;
        rollback.PreviousSiteMode = previousMode;
    }

    public WorldSiteBattleLaunchResult BeginAndActivate(
        StrategicWorldState state,
        BattleStartRequest request,
        WorldSiteBattleLaunchRollback rollback,
        Action applyBattleStartRequest,
        Func<bool> activateBattleRuntime,
        Func<string> getBlockedReason,
        Action clearBattleEntities,
        Action clearBattleCorpsRuntime,
        Action<bool> setBattleRuntimeEnabled)
    {
        if (request == null)
        {
            return new WorldSiteBattleLaunchResult
            {
                Success = false,
                FailureReason = "missing_battle_request"
            };
        }

        BattleSessionHandoff.BeginBattle(request);
        // Probe is intentionally diagnostic-only while legacy handoff remains the
        // player-facing path; probe failure must not roll back a valid launch.
        BattleGroupSessionProbeResult probeResult = _battleGroupSessionProbe?.Probe(request);
        applyBattleStartRequest?.Invoke();
        if (activateBattleRuntime?.Invoke() == true)
        {
            return new WorldSiteBattleLaunchResult
            {
                Success = true,
                ProbeResult = probeResult
            };
        }

        string reason = getBlockedReason?.Invoke() ?? "";
        if (string.IsNullOrWhiteSpace(reason))
        {
            reason = "battle_activation_failed";
        }

        BattleSessionHandoff.CancelBattle();
        Rollback(
            state,
            rollback,
            request,
            reason,
            clearBattleEntities,
            clearBattleCorpsRuntime,
            setBattleRuntimeEnabled);
        return new WorldSiteBattleLaunchResult
        {
            Success = false,
            FailureReason = reason,
            ProbeResult = probeResult
        };
    }

    private void Rollback(
        StrategicWorldState state,
        WorldSiteBattleLaunchRollback rollback,
        BattleStartRequest request,
        string reason,
        Action clearBattleEntities,
        Action clearBattleCorpsRuntime,
        Action<bool> setBattleRuntimeEnabled)
    {
        string siteId = !string.IsNullOrWhiteSpace(request?.TargetSiteId)
            ? request.TargetSiteId
            : rollback?.SiteId ?? "";
        WorldSiteState site = null;
        state?.SiteStates.TryGetValue(siteId, out site);
        if (site == null)
        {
            return;
        }

        WorldSiteMode currentMode = site.SiteMode;
        if (rollback?.HasPreviousSiteMode == true)
        {
            _siteModeTransitions.RestoreMode(
                site,
                rollback.PreviousSiteMode,
                state?.WorldTick ?? site.LastModeChangedTick,
                "battle_launch_rollback",
                request?.RequestId ?? "");
        }

        clearBattleEntities?.Invoke();
        clearBattleCorpsRuntime?.Invoke();
        setBattleRuntimeEnabled?.Invoke(false);
        GameLog.Warn(
            nameof(WorldSiteBattleLauncher),
            $"Site battle launch rolled back site={site.SiteId} fromMode={currentMode} toMode={site.SiteMode} reason={reason}");
    }
}
