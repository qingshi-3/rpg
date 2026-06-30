using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.StrategicManagement;
using Rpg.Definitions.StrategicManagement;
using Rpg.Definitions.World;

namespace Rpg.Presentation.World;

public static class WorldSiteHoverSummaryPresenter
{
    public const float DefaultAnchorGap = 8.0f;
    public const float DefaultEdgeMargin = 12.0f;

    public static WorldSiteHoverSummaryData Build(
        WorldSiteDefinition definition,
        StrategicManagementDashboardViewModel dashboard)
    {
        StrategicLocationDashboardViewModel location = dashboard?.SelectedLocation;
        StrategicCityManagementViewModel city = dashboard?.SelectedCity;
        string title = string.IsNullOrWhiteSpace(location?.DisplayName)
            ? definition?.DisplayName ?? definition?.Id ?? ""
            : location.DisplayName;

        return new WorldSiteHoverSummaryData
        {
            Title = title,
            ResourceText = BuildAssetText(dashboard?.Resources),
            ForceText = BuildCityForceText(city, location)
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

    private static string BuildAssetText(IEnumerable<StrategicResourceViewModel> resources)
    {
        string text = string.Join(
            "  ",
            (resources ?? Enumerable.Empty<StrategicResourceViewModel>())
            .Where(resource => resource != null && !string.IsNullOrWhiteSpace(resource.DisplayName))
            .OrderBy(resource => resource.ResourceId)
            .Select(resource => $"{resource.DisplayName} {resource.Amount}"));
        return string.IsNullOrWhiteSpace(text) ? "资产 无" : text;
    }

    private static string BuildCityForceText(
        StrategicCityManagementViewModel city,
        StrategicLocationDashboardViewModel location)
    {
        if (location?.CanManageCity != true || city == null)
        {
            string control = string.IsNullOrWhiteSpace(location?.ControlStateDisplayName)
                ? "未知"
                : location.ControlStateDisplayName;
            string production = string.IsNullOrWhiteSpace(location?.ProductionDisplayText)
                ? "无"
                : location.ProductionDisplayText;
            return $"控制 {control}  产出 {production}";
        }

        int reserveForces = System.Math.Max(0, city.ReserveForces);
        int capacity = System.Math.Max(0, city.CityForceCapacity);
        int currentHeroCompanies = city.HeroCompanies?.Count ?? 0;
        return $"预备兵 {reserveForces}/{capacity}  英雄 {currentHeroCompanies}";
    }
}

public sealed class WorldSiteHoverSummaryData
{
    public string Title { get; set; } = "";
    public string ResourceText { get; set; } = "";
    public string ForceText { get; set; } = "";
}
