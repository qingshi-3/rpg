using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Definitions.World;
using Rpg.Domain.World;

namespace Rpg.Application.World;

public sealed class StrategicFogOfWarSettings
{
    public float FogTexelWorldSize { get; set; } = StrategicFogOfWarService.DefaultFogTexelWorldSize;
    public float SiteVisionRadius { get; set; } = 480.0f;
    public float ArmyVisionRadius { get; set; } = 260.0f;
}

public readonly record struct StrategicFogVisionSource(Vector2 Position, float Radius);

public static class StrategicFogOfWarService
{
    // Intel cells are storage/query granularity only; presentation draws circular masks from vision sources.
    public const float DefaultFogTexelWorldSize = 16.0f;

    public static bool RefreshVisibility(
        StrategicWorldState state,
        StrategicWorldDefinition definition,
        StrategicFogOfWarSettings settings)
    {
        if (state == null || definition == null)
        {
            return false;
        }

        state.Intel ??= new StrategicWorldIntelState();
        HashSet<string> previousVisible = state.Intel.VisibleCells.ToHashSet(StringComparer.Ordinal);
        HashSet<string> previousExplored = state.Intel.ExploredCells.ToHashSet(StringComparer.Ordinal);

        StrategicFogOfWarSettings effectiveSettings = NormalizeSettings(settings);
        HashSet<string> visible = BuildVisibleCellKeys(BuildVisionSources(state, definition, effectiveSettings), effectiveSettings);
        HashSet<string> explored = new(previousExplored, StringComparer.Ordinal);
        explored.UnionWith(visible);

        state.Intel.VisibleCells = visible.OrderBy(key => key, StringComparer.Ordinal).ToList();
        state.Intel.ExploredCells = explored.OrderBy(key => key, StringComparer.Ordinal).ToList();
        state.Intel.LastUpdatedWorldTick = state.WorldTick;

        RefreshVisibleSiteSnapshots(state, definition, settings);

        return !previousVisible.SetEquals(visible) || !previousExplored.SetEquals(explored);
    }

    public static HashSet<string> BuildVisibleCellKeys(
        IEnumerable<StrategicFogVisionSource> sources,
        StrategicFogOfWarSettings settings)
    {
        StrategicFogOfWarSettings effectiveSettings = NormalizeSettings(settings);
        HashSet<string> visible = new(StringComparer.Ordinal);
        if (sources == null)
        {
            return visible;
        }

        foreach (StrategicFogVisionSource source in sources)
        {
            if (!IsFinite(source.Position) || source.Radius <= 0.0f)
            {
                continue;
            }

            Vector2I center = WorldToCell(source.Position, effectiveSettings.FogTexelWorldSize);
            int cellRadius = Mathf.CeilToInt(source.Radius / effectiveSettings.FogTexelWorldSize);
            int radiusSquared = cellRadius * cellRadius;

            for (int y = -cellRadius; y <= cellRadius; y++)
            {
                for (int x = -cellRadius; x <= cellRadius; x++)
                {
                    if (x * x + y * y <= radiusSquared)
                    {
                        visible.Add(ToCellKey(center + new Vector2I(x, y)));
                    }
                }
            }
        }

        return visible;
    }

    public static WorldIntelVisibility GetSiteVisibility(
        StrategicWorldIntelState intel,
        WorldSiteDefinition site,
        StrategicFogOfWarSettings settings)
    {
        if (intel == null || site == null)
        {
            return WorldIntelVisibility.Unknown;
        }

        string cellKey = ToCellKey(WorldToCell(site.MapPosition, NormalizeSettings(settings).FogTexelWorldSize));
        if (intel.VisibleCells.Contains(cellKey))
        {
            return WorldIntelVisibility.Visible;
        }

        return intel.KnownSites.ContainsKey(site.Id)
            ? WorldIntelVisibility.Revealed
            : WorldIntelVisibility.Unknown;
    }

    public static WorldIntelVisibility GetPositionVisibility(
        StrategicWorldIntelState intel,
        Vector2 position,
        StrategicFogOfWarSettings settings)
    {
        if (intel == null || !IsFinite(position))
        {
            return WorldIntelVisibility.Unknown;
        }

        string cellKey = ToCellKey(WorldToCell(position, NormalizeSettings(settings).FogTexelWorldSize));
        if (intel.VisibleCells.Contains(cellKey))
        {
            return WorldIntelVisibility.Visible;
        }

        return intel.ExploredCells.Contains(cellKey)
            ? WorldIntelVisibility.Revealed
            : WorldIntelVisibility.Unknown;
    }

    public static IEnumerable<string> EnumerateCellKeysForBounds(Rect2 bounds, StrategicFogOfWarSettings settings)
    {
        StrategicFogOfWarSettings effectiveSettings = NormalizeSettings(settings);
        Vector2I start = WorldToCell(bounds.Position, effectiveSettings.FogTexelWorldSize);
        Vector2I end = WorldToCell(bounds.End, effectiveSettings.FogTexelWorldSize);

        for (int y = Math.Min(start.Y, end.Y); y <= Math.Max(start.Y, end.Y); y++)
        {
            for (int x = Math.Min(start.X, end.X); x <= Math.Max(start.X, end.X); x++)
            {
                yield return ToCellKey(new Vector2I(x, y));
            }
        }
    }

    public static Rect2 CellKeyToWorldRect(string cellKey, StrategicFogOfWarSettings settings)
    {
        Vector2I cell = ParseCellKey(cellKey);
        float size = NormalizeSettings(settings).FogTexelWorldSize;
        return new Rect2(new Vector2(cell.X * size, cell.Y * size), new Vector2(size, size));
    }

    public static IEnumerable<StrategicFogVisionSource> BuildVisionSources(
        StrategicWorldState state,
        StrategicWorldDefinition definition,
        StrategicFogOfWarSettings settings)
    {
        foreach (WorldSiteDefinition siteDefinition in definition.SiteDefinitions.Where(site => site != null))
        {
            if (!state.SiteStates.TryGetValue(siteDefinition.Id, out WorldSiteState siteState) ||
                siteState.OwnerFactionId != state.PlayerFactionId ||
                siteState.ControlState is SiteControlState.Lost or SiteControlState.Unknown)
            {
                continue;
            }

            yield return new StrategicFogVisionSource(siteDefinition.MapPosition, settings.SiteVisionRadius);
        }

        foreach (WorldArmyState army in state.ArmyStates.Values)
        {
            if (army.OwnerFactionId != state.PlayerFactionId ||
                army.Status is WorldArmyStatus.Defeated or WorldArmyStatus.Garrisoned)
            {
                continue;
            }

            yield return new StrategicFogVisionSource(army.WorldPosition, settings.ArmyVisionRadius);
        }
    }

    private static void RefreshVisibleSiteSnapshots(
        StrategicWorldState state,
        StrategicWorldDefinition definition,
        StrategicFogOfWarSettings settings)
    {
        foreach (WorldSiteDefinition siteDefinition in definition.SiteDefinitions.Where(site => site != null))
        {
            if (!state.SiteStates.TryGetValue(siteDefinition.Id, out WorldSiteState siteState) ||
                GetSiteVisibility(state.Intel, siteDefinition, settings) != WorldIntelVisibility.Visible)
            {
                continue;
            }

            state.Intel.KnownSites[siteDefinition.Id] = WorldSiteIntelService.BuildSnapshot(siteDefinition, siteState, state.WorldTick);
        }
    }

    private static StrategicFogOfWarSettings NormalizeSettings(StrategicFogOfWarSettings settings)
    {
        return new StrategicFogOfWarSettings
        {
            FogTexelWorldSize = Mathf.Max(settings?.FogTexelWorldSize ?? DefaultFogTexelWorldSize, 1.0f),
            SiteVisionRadius = Mathf.Max(settings?.SiteVisionRadius ?? 480.0f, 1.0f),
            ArmyVisionRadius = Mathf.Max(settings?.ArmyVisionRadius ?? 260.0f, 1.0f)
        };
    }

    private static Vector2I WorldToCell(Vector2 position, float texelWorldSize)
    {
        return new Vector2I(
            Mathf.FloorToInt(position.X / texelWorldSize),
            Mathf.FloorToInt(position.Y / texelWorldSize));
    }

    private static Vector2 CellToWorldCenter(Vector2I cell, float texelWorldSize)
    {
        return new Vector2(
            (cell.X + 0.5f) * texelWorldSize,
            (cell.Y + 0.5f) * texelWorldSize);
    }

    private static string ToCellKey(Vector2I cell)
    {
        return $"{cell.X}:{cell.Y}";
    }

    private static Vector2I ParseCellKey(string cellKey)
    {
        string[] parts = (cellKey ?? "").Split(':');
        return parts.Length == 2 &&
               int.TryParse(parts[0], out int x) &&
               int.TryParse(parts[1], out int y)
            ? new Vector2I(x, y)
            : Vector2I.Zero;
    }

    private static bool IsFinite(Vector2 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y);
    }
}
