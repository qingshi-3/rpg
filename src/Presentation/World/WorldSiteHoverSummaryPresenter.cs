using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.World;
using Rpg.Definitions.World;
using Rpg.Domain.World;

namespace Rpg.Presentation.World;

public static class WorldSiteHoverSummaryPresenter
{
    public const float DefaultAnchorGap = 8.0f;
    public const float DefaultEdgeMargin = 12.0f;

    public static WorldSiteHoverSummaryData Build(
        StrategicWorldDefinitionQueries queries,
        WorldSiteDefinition definition,
        WorldSiteState state)
    {
        ResourceStore resources = state?.LocalResources ?? new ResourceStore();
        return new WorldSiteHoverSummaryData
        {
            Title = StrategicWorldDisplayNames.GetSiteLabel(queries, definition?.Id, definition?.DisplayName ?? ""),
            ResourceText =
                $"{StrategicWorldDisplayNames.GetResourceLabel(queries, StrategicWorldIds.ResourcePopulation)} {resources.GetAvailable(StrategicWorldIds.ResourcePopulation)}/{resources.GetAmount(StrategicWorldIds.ResourcePopulation)}　" +
                $"{StrategicWorldDisplayNames.GetResourceLabel(queries, StrategicWorldIds.ResourceEconomy)} {resources.GetAmount(StrategicWorldIds.ResourceEconomy)}　" +
                $"{StrategicWorldDisplayNames.GetResourceLabel(queries, StrategicWorldIds.ResourceStone)} {resources.GetAmount(StrategicWorldIds.ResourceStone)}",
            ForceText = $"兵团 {GetSiteArmyCount(state)}　英雄 {GetSiteHeroCount(state)}"
        };
    }

    public static WorldSiteHoverSummaryData BuildSnapshot(
        StrategicWorldDefinitionQueries queries,
        WorldSiteDefinition definition,
        WorldSiteIntelSnapshot snapshot)
    {
        ResourceStore resources = snapshot?.KnownLocalResources ?? new ResourceStore();
        return new WorldSiteHoverSummaryData
        {
            Title = $"旧情报：{snapshot?.DisplayName ?? StrategicWorldDisplayNames.GetSiteLabel(queries, definition?.Id, definition?.DisplayName ?? "")}",
            ResourceText =
                $"{StrategicWorldDisplayNames.GetResourceLabel(queries, StrategicWorldIds.ResourcePopulation)} {resources.GetAvailable(StrategicWorldIds.ResourcePopulation)}/{resources.GetAmount(StrategicWorldIds.ResourcePopulation)}　" +
                $"{StrategicWorldDisplayNames.GetResourceLabel(queries, StrategicWorldIds.ResourceEconomy)} {resources.GetAmount(StrategicWorldIds.ResourceEconomy)}　" +
                $"{StrategicWorldDisplayNames.GetResourceLabel(queries, StrategicWorldIds.ResourceStone)} {resources.GetAmount(StrategicWorldIds.ResourceStone)}",
            ForceText = $"兵团 {GetArmyCount(snapshot?.KnownGarrison)}　英雄 {GetHeroCount(snapshot?.KnownGarrison)}"
        };
    }

    public static Vector2 CalculatePanelPosition(
        Rect2 anchorBounds,
        Vector2 panelSize,
        Vector2 viewportSize,
        float anchorGap = DefaultAnchorGap,
        float edgeMargin = DefaultEdgeMargin)
    {
        Vector2 effectiveSize = new(
            Mathf.Max(panelSize.X, 1.0f),
            Mathf.Max(panelSize.Y, 1.0f));
        Vector2 effectiveViewport = new(
            Mathf.Max(viewportSize.X, effectiveSize.X + edgeMargin * 2.0f),
            Mathf.Max(viewportSize.Y, effectiveSize.Y + edgeMargin * 2.0f));

        float x = anchorBounds.GetCenter().X - effectiveSize.X / 2.0f;
        float aboveY = anchorBounds.Position.Y - effectiveSize.Y - anchorGap;
        float belowY = anchorBounds.End.Y + anchorGap;
        float y = aboveY >= edgeMargin ? aboveY : belowY;

        return new Vector2(
            Mathf.Clamp(x, edgeMargin, effectiveViewport.X - effectiveSize.X - edgeMargin),
            Mathf.Clamp(y, edgeMargin, effectiveViewport.Y - effectiveSize.Y - edgeMargin));
    }

    private static int GetSiteHeroCount(WorldSiteState state)
    {
        int tagHeroes = state?.ActiveTags?.Count(tag => tag.StartsWith("hero:")) ?? 0;
        int unitHeroes = GetHeroCount(state?.Garrison);
        return tagHeroes + unitHeroes;
    }

    private static int GetHeroCount(IEnumerable<GarrisonState> garrison)
    {
        return garrison?
            .Where(unit => IsHeroUnitType(unit.UnitTypeId))
            .Sum(unit => System.Math.Max(unit.Count, 0)) ?? 0;
    }

    private static int GetSiteArmyCount(WorldSiteState state)
    {
        return GetArmyCount(state?.Garrison);
    }

    private static int GetArmyCount(IEnumerable<GarrisonState> garrison)
    {
        return garrison == null
            ? 0
            : garrison
                .Where(unit => !IsHeroUnitType(unit.UnitTypeId))
                .Sum(unit => System.Math.Max(unit.Count, 0));
    }

    private static bool IsHeroUnitType(string unitTypeId)
    {
        return unitTypeId == HeroCorpsV0PlayableSliceIds.HeroUnit;
    }
}

public sealed class WorldSiteHoverSummaryData
{
    public string Title { get; set; } = "";
    public string ResourceText { get; set; } = "";
    public string ForceText { get; set; } = "";
}
