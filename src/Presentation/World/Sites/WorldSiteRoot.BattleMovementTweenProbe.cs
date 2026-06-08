using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle;
using Rpg.Domain.Battle.Grid;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Battle;
using Rpg.Presentation.Battle.Entities;

namespace Rpg.Presentation.World.Sites;

public partial class WorldSiteRoot
{
    // Slow enough for manual comparison against grid-step movement; this probe
    // is not a runtime movement-speed contract.
    private const double BattleMovementTweenProbeSeconds = 10.0;
    private const string BattleMovementTweenProbeEnvironmentVariable = "RPG_BATTLE_MOVEMENT_TWEEN_PROBE";
    private const int BattleMovementTweenProbeZIndex = 4096;
    private BattleEntity _battleMovementTweenProbe;

    private static bool ShouldPlayBattleMovementTweenProbe()
    {
        // This visual probe is manual QA instrumentation; normal battle runtime
        // should not create an extra animated entity or tween by default.
        string value = System.Environment.GetEnvironmentVariable(BattleMovementTweenProbeEnvironmentVariable) ?? "";
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Equals("1", System.StringComparison.OrdinalIgnoreCase) ||
               value.Equals("true", System.StringComparison.OrdinalIgnoreCase) ||
               value.Equals("yes", System.StringComparison.OrdinalIgnoreCase) ||
               value.Equals("on", System.StringComparison.OrdinalIgnoreCase);
    }

    private void PlayBattleMovementTweenProbe()
    {
        ClearBattleMovementTweenProbe();
        if (!_battleRuntimeEnabled || _unitRoot == null)
        {
            return;
        }

        BattleEntity source = _unitRoot
            .EnumerateAliveFaction(BattleFaction.Player)
            .FirstOrDefault(entity => entity?.GetComponent<GridOccupantComponent>() != null);
        if (source == null)
        {
            GameLog.Warn(nameof(WorldSiteRoot), "Battle movement tween probe skipped because no player unit is available.");
            return;
        }

        if (!TryResolveBattleMovementTweenProbeSpan(source, out Vector2 fromGlobal, out Vector2 toGlobal))
        {
            GameLog.Warn(nameof(WorldSiteRoot), "Battle movement tween probe skipped because map span could not be resolved.");
            return;
        }

        BattleEntity probe = source.Duplicate() as BattleEntity;
        if (probe == null)
        {
            GameLog.Warn(nameof(WorldSiteRoot), $"Battle movement tween probe could not duplicate source={source.EntityId}");
            return;
        }

        probe.Name = "BattleMovementTweenProbe";
        probe.EntityId = $"{source.EntityId}:movement_tween_probe";
        probe.DisplayName = $"{source.DisplayName} Tween Probe";
        probe.DrawDebugMarker = false;
        probe.ZIndex = BattleMovementTweenProbeZIndex;
        _unitRoot.GetParent()?.AddChild(probe);
        probe.GlobalPosition = fromGlobal;
        _battleMovementTweenProbe = probe;

        UnitAnimationComponent animation = probe.GetComponent<UnitAnimationComponent>();
        animation?.FaceHorizontalDirection(toGlobal.X - fromGlobal.X);
        animation?.PlayMove(restart: true);

        // The probe is deliberately one Presentation tween, independent from
        // Runtime grid stepping, so manual QA can compare raw tween smoothness.
        Tween tween = CreateTween();
        tween.BindNode(probe);
        tween.SetTrans(Tween.TransitionType.Linear);
        tween.TweenProperty(probe, "global_position", toGlobal, BattleMovementTweenProbeSeconds);
        tween.TweenCallback(Callable.From(() => FinishBattleMovementTweenProbe(probe)));
        GameLog.Info(
            nameof(WorldSiteRoot),
            $"BattleMovementTweenProbeStarted source={source.EntityId} from={fromGlobal} to={toGlobal} seconds={BattleMovementTweenProbeSeconds:0.00}");
    }

    private void ClearBattleMovementTweenProbe()
    {
        if (_battleMovementTweenProbe == null)
        {
            return;
        }

        if (GodotObject.IsInstanceValid(_battleMovementTweenProbe))
        {
            _battleMovementTweenProbe.QueueFree();
        }

        _battleMovementTweenProbe = null;
    }

    private void FinishBattleMovementTweenProbe(BattleEntity probe)
    {
        if (probe == null || !GodotObject.IsInstanceValid(probe))
        {
            return;
        }

        probe.GetComponent<UnitAnimationComponent>()?.PlayIdle();
        if (_battleMovementTweenProbe == probe)
        {
            _battleMovementTweenProbe = null;
        }

        probe.QueueFree();
        GameLog.Info(nameof(WorldSiteRoot), $"BattleMovementTweenProbeFinished id={probe.EntityId}");
    }

    private bool TryResolveBattleMovementTweenProbeSpan(
        BattleEntity source,
        out Vector2 fromGlobal,
        out Vector2 toGlobal)
    {
        fromGlobal = default;
        toGlobal = default;
        if (_activeGridMap == null || source == null)
        {
            return false;
        }

        GridOccupantComponent sourceGrid = source.GetComponent<GridOccupantComponent>();
        int preferredY = sourceGrid?.GridY ?? 0;
        int footprintWidth = BattleFootprintCells.NormalizeSize(sourceGrid?.FootprintWidth ?? 1);
        int footprintHeight = BattleFootprintCells.NormalizeSize(sourceGrid?.FootprintHeight ?? 1);
        Vector2I footprint = new(footprintWidth, footprintHeight);

        var row = EnumerateBattleMovementTweenProbeSurfaces()
            .GroupBy(surface => surface.SurfacePosition.Y)
            .Select(group => new
            {
                Y = group.Key,
                MinX = group.Min(surface => surface.SurfacePosition.X),
                MaxX = group.Max(surface => surface.SurfacePosition.X)
            })
            .Where(candidate => candidate.MaxX > candidate.MinX)
            .OrderByDescending(candidate => candidate.MaxX - candidate.MinX)
            .ThenBy(candidate => System.Math.Abs(candidate.Y - preferredY))
            .FirstOrDefault();
        if (row == null)
        {
            return false;
        }

        return TryGetFootprintCenterGlobalPosition(new GridPosition(row.MinX, row.Y), footprint, out fromGlobal) &&
               TryGetFootprintCenterGlobalPosition(new GridPosition(row.MaxX, row.Y), footprint, out toGlobal) &&
               fromGlobal.DistanceSquaredTo(toGlobal) > 1f;
    }

    private IEnumerable<GridCellSurface> EnumerateBattleMovementTweenProbeSurfaces()
    {
        return _activeGridMap?.Surfaces.Values
            .Where(surface =>
                surface.HasFoundation &&
                _activeGridMap.IsTopSurface(surface.SurfacePosition)) ??
            System.Array.Empty<GridCellSurface>();
    }
}
