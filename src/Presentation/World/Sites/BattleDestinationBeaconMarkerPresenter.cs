using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle.Snapshots;
using Rpg.Domain.Battle.Grid;
using Rpg.Presentation.Battle;
using Rpg.Presentation.Common;
using Rpg.Runtime.Battle;

namespace Rpg.Presentation.World.Sites;

internal sealed class BattleDestinationBeaconMarkerPresenter
{
    private const string BattleDestinationBeaconMarkerScenePath = "res://scenes/world/sites/BattleDestinationBeaconMarker.tscn";

    private readonly Dictionary<string, BattleDestinationBeaconMarker> _battleDestinationBeaconMarkers = new(StringComparer.Ordinal);
    private PackedScene _markerScene;

    public void RefreshPreparation(
        BattleGridHighlightOverlay overlay,
        IEnumerable<BattleRuntimeCommandGroupView> playerGroups,
        string selectedGroupKey,
        Func<string, BattleGroupPlanSnapshot> resolvePlan)
    {
        RefreshBattleDestinationBeaconMarkers(
            overlay,
            BuildBattlePreparationDestinationBeaconMarkerModels(playerGroups, selectedGroupKey, resolvePlan));
    }

    public void RefreshRuntime(
        BattleGridHighlightOverlay overlay,
        IEnumerable<BattleRuntimeDestinationBeacon> destinationBeacons,
        IEnumerable<BattleRuntimeCommandGroupView> playerGroups,
        string selectedGroupKey)
    {
        RefreshBattleDestinationBeaconMarkers(
            overlay,
            BuildBattleRuntimeDestinationBeaconMarkerModels(destinationBeacons, playerGroups, selectedGroupKey));
    }

    public void Clear()
    {
        foreach (BattleDestinationBeaconMarker marker in _battleDestinationBeaconMarkers.Values.Where(GodotObject.IsInstanceValid))
        {
            marker.QueueFree();
        }

        _battleDestinationBeaconMarkers.Clear();
    }

    private void RefreshBattleDestinationBeaconMarkers(
        BattleGridHighlightOverlay overlay,
        IEnumerable<BattleDestinationBeaconMarkerModel> models)
    {
        // Markers are display-only: preparation plans and Runtime command facts remain the beacon authority.
        if (overlay == null)
        {
            Clear();
            return;
        }

        BattleDestinationBeaconMarkerModel[] nextModels = models?.ToArray() ?? Array.Empty<BattleDestinationBeaconMarkerModel>();
        HashSet<string> nextKeys = nextModels.Select(model => model.Key).ToHashSet(StringComparer.Ordinal);
        foreach (string staleKey in _battleDestinationBeaconMarkers.Keys.Where(key => !nextKeys.Contains(key)).ToArray())
        {
            if (GodotObject.IsInstanceValid(_battleDestinationBeaconMarkers[staleKey]))
            {
                _battleDestinationBeaconMarkers[staleKey].QueueFree();
            }

            _battleDestinationBeaconMarkers.Remove(staleKey);
        }

        foreach (BattleDestinationBeaconMarkerModel model in nextModels)
        {
            if (!overlay.TryResolveCellCenter(model.Cell, out Vector2 markerPosition))
            {
                continue;
            }

            BattleDestinationBeaconMarker marker = ResolveMarker(overlay, model.Key);
            if (marker == null)
            {
                continue;
            }

            marker.Position = markerPosition;
            // The marker owns the visual, but the overlay owns map-space tile geometry.
            IReadOnlyList<Vector2> targetCellPolygon = overlay.TryResolveCellPolygon(model.Cell, out Vector2[] cellPolygon)
                ? cellPolygon.Select(point => point - markerPosition).ToArray()
                : null;
            marker.Bind(model.HeroPreview, targetCellPolygon, model.IsSelected);
            marker.ApplyViewportAvoidance(
                marker.GetGlobalTransformWithCanvas().Origin,
                overlay.GetViewport()?.GetVisibleRect().Size ?? Vector2.Zero);
        }
    }

    private IEnumerable<BattleDestinationBeaconMarkerModel> BuildBattlePreparationDestinationBeaconMarkerModels(
        IEnumerable<BattleRuntimeCommandGroupView> playerGroups,
        string selectedGroupKey,
        Func<string, BattleGroupPlanSnapshot> resolvePlan)
    {
        string normalizedSelectedGroupKey = selectedGroupKey?.Trim() ?? "";
        var plannedGroups = (playerGroups ?? Array.Empty<BattleRuntimeCommandGroupView>())
            .Select(group => new
            {
                Group = group,
                Plan = resolvePlan?.Invoke(group?.GroupKey ?? "")
            })
            .Where(item => item.Group != null && item.Plan?.HasInitialDestinationBeacon == true)
            .GroupBy(item => (item.Plan.InitialDestinationCellX, item.Plan.InitialDestinationCellY, item.Plan.InitialDestinationCellHeight));

        foreach (var plannedGroup in plannedGroups)
        {
            var primary = plannedGroup
                .OrderBy(item => item.Group.GroupKey, StringComparer.Ordinal)
                .First();
            bool isSelected = !string.IsNullOrWhiteSpace(normalizedSelectedGroupKey) &&
                plannedGroup.Any(item => string.Equals(item.Group.GroupKey ?? "", normalizedSelectedGroupKey, StringComparison.Ordinal));
            yield return new BattleDestinationBeaconMarkerModel(
                Key: $"preparation:{plannedGroup.Key.InitialDestinationCellX}:{plannedGroup.Key.InitialDestinationCellY}:{plannedGroup.Key.InitialDestinationCellHeight}",
                Cell: new GridPosition(plannedGroup.Key.InitialDestinationCellX, plannedGroup.Key.InitialDestinationCellY),
                HeroPreview: BattleUnitPreviewResolver.ResolveAnimatedPreview(primary.Group.HeroBattleUnitId),
                IsSelected: isSelected);
        }
    }

    private IEnumerable<BattleDestinationBeaconMarkerModel> BuildBattleRuntimeDestinationBeaconMarkerModels(
        IEnumerable<BattleRuntimeDestinationBeacon> destinationBeacons,
        IEnumerable<BattleRuntimeCommandGroupView> playerGroups,
        string selectedGroupKey)
    {
        Dictionary<string, BattleRuntimeCommandGroupView> groupsByKey = (playerGroups ?? Array.Empty<BattleRuntimeCommandGroupView>())
            .Where(group => group != null && !string.IsNullOrWhiteSpace(group.GroupKey))
            .ToDictionary(group => group.GroupKey, StringComparer.Ordinal);
        string normalizedSelectedGroupKey = selectedGroupKey?.Trim() ?? "";

        foreach (BattleRuntimeDestinationBeacon beacon in destinationBeacons ?? Array.Empty<BattleRuntimeDestinationBeacon>())
        {
            if (beacon?.IsValid != true ||
                string.IsNullOrWhiteSpace(beacon.BeaconId) ||
                beacon.OwnerBattleGroupIds.Count == 0)
            {
                continue;
            }

            BattleRuntimeCommandGroupView primaryGroup = beacon.OwnerBattleGroupIds
                .Select(groupKey => groupsByKey.TryGetValue(groupKey ?? "", out BattleRuntimeCommandGroupView group) ? group : null)
                .FirstOrDefault(group => group != null);
            bool isSelected = !string.IsNullOrWhiteSpace(normalizedSelectedGroupKey) &&
                beacon.OwnerBattleGroupIds.Any(groupKey => string.Equals(groupKey ?? "", normalizedSelectedGroupKey, StringComparison.Ordinal));
            yield return new BattleDestinationBeaconMarkerModel(
                Key: beacon.BeaconId,
                Cell: new GridPosition(beacon.Anchor.X, beacon.Anchor.Y),
                HeroPreview: BattleUnitPreviewResolver.ResolveAnimatedPreview(primaryGroup?.HeroBattleUnitId),
                IsSelected: isSelected);
        }
    }

    private BattleDestinationBeaconMarker ResolveMarker(BattleGridHighlightOverlay overlay, string key)
    {
        if (_battleDestinationBeaconMarkers.TryGetValue(key, out BattleDestinationBeaconMarker existing) &&
            GodotObject.IsInstanceValid(existing) &&
            existing.GetParent() == overlay)
        {
            return existing;
        }

        if (GodotObject.IsInstanceValid(existing))
        {
            existing.QueueFree();
        }

        _markerScene ??= GD.Load<PackedScene>(BattleDestinationBeaconMarkerScenePath);
        BattleDestinationBeaconMarker marker = _markerScene?.Instantiate<BattleDestinationBeaconMarker>();
        if (marker == null)
        {
            return null;
        }

        overlay.AddChild(marker);
        _battleDestinationBeaconMarkers[key] = marker;
        return marker;
    }

    private sealed record BattleDestinationBeaconMarkerModel(
        string Key,
        GridPosition Cell,
        BattleUnitAnimatedPreviewModel HeroPreview,
        bool IsSelected);
}
