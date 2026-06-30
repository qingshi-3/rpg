using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.StrategicManagement;
using Rpg.Definitions.StrategicManagement;
using Rpg.Definitions.World;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.World;

public partial class StrategicWorldRoot
{
    private const double ResourceFloatStaggerSeconds = 0.08;
    private Control _worldResourceFloatOverlay;

    private void BindWorldResourceFloatOverlay(Control hud)
    {
        _worldResourceFloatOverlay = GameUiSceneFactory.GetRequiredNode<Control>(
            hud,
            "OverlayHost/WorldResourceFloatOverlay",
            nameof(StrategicWorldRoot));
    }

    private void ShowStrategicProductionFeedback(StrategicCommandResult settlement)
    {
        if (settlement?.Success != true || _worldResourceFloatOverlay == null || Definition == null)
        {
            return;
        }

        foreach (StrategicEvent strategicEvent in settlement.Events)
        {
            if (strategicEvent == null ||
                strategicEvent.Kind is not ("StrategicLocationProductionSettled" or "StrategicCityBuildingProductionSettled") ||
                strategicEvent.TargetIds.Count == 0)
            {
                continue;
            }

            string locationId = strategicEvent.TargetIds[0];
            if (!StrategicManagementRuntime.Definitions.Locations.TryGetValue(locationId, out StrategicLocationDefinition location) ||
                string.IsNullOrWhiteSpace(location.MapSiteId))
            {
                continue;
            }

            WorldSiteDefinition site = Definition.SiteDefinitions.FirstOrDefault(item =>
                string.Equals(item.Id, location.MapSiteId, StringComparison.Ordinal));
            if (site == null ||
                !strategicEvent.Payload.TryGetValue("resources", out string resourcePayload) ||
                !TryParseStrategicResourceAmounts(resourcePayload, out IReadOnlyList<StrategicResourceAmount> resources))
            {
                continue;
            }

            List<StrategicResourceAmount> positiveResources = resources
                .Where(item => item.Amount > 0)
                .ToList();
            if (positiveResources.Count == 0)
            {
                continue;
            }

            Rect2 labelRect = GetSiteLabelRect(site);
            Vector2 anchor = new(labelRect.GetCenter().X, labelRect.Position.Y - 8.0f);
            for (int index = 0; index < positiveResources.Count; index++)
            {
                StrategicResourceAmount resource = positiveResources[index];
                WorldResourceFloatText floatText = GameUiSceneFactory.CreateWorldResourceFloatText(nameof(StrategicWorldRoot));
                if (floatText == null)
                {
                    continue;
                }

                string displayName = ResolveStrategicResourceDisplayName(resource.ResourceId);
                floatText.Bind(
                    ResolveStrategicResourceSymbol(resource.ResourceId, displayName),
                    displayName,
                    resource.Amount,
                    ResolveStrategicResourceColor(resource.ResourceId));
                _worldResourceFloatOverlay.AddChild(floatText);
                float xOffset = (index - (positiveResources.Count - 1) * 0.5f) * 72.0f;
                floatText.Play(anchor + new Vector2(xOffset, 0.0f), index * ResourceFloatStaggerSeconds);
            }
        }
    }

    private static bool TryParseStrategicResourceAmounts(
        string payload,
        out IReadOnlyList<StrategicResourceAmount> amounts)
    {
        List<StrategicResourceAmount> parsed = new();
        foreach (string entry in (payload ?? "").Split(
                     ',',
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] parts = entry.Split(':', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 ||
                string.IsNullOrWhiteSpace(parts[0]) ||
                !int.TryParse(parts[1], out int amount))
            {
                continue;
            }

            parsed.Add(new StrategicResourceAmount(parts[0], amount));
        }

        amounts = parsed;
        return parsed.Count > 0;
    }

    private static string ResolveStrategicResourceDisplayName(string resourceId)
    {
        return StrategicManagementRuntime.Definitions.Resources.TryGetValue(resourceId ?? "", out StrategicResourceDefinition resource)
            ? resource.DisplayName
            : resourceId ?? "";
    }

    private static string ResolveStrategicResourceSymbol(string resourceId, string displayName)
    {
        return resourceId switch
        {
            StrategicManagementIds.ResourceMoney => "金",
            StrategicManagementIds.ResourceFood => "粮",
            StrategicManagementIds.ResourceWood => "木",
            StrategicManagementIds.ResourceOre => "矿",
            _ => string.IsNullOrWhiteSpace(displayName) ? "资" : displayName.Substring(0, 1)
        };
    }

    private static Color ResolveStrategicResourceColor(string resourceId)
    {
        return resourceId switch
        {
            StrategicManagementIds.ResourceMoney => new Color(1.0f, 0.82f, 0.32f, 1.0f),
            StrategicManagementIds.ResourceFood => new Color(0.65f, 0.95f, 0.55f, 1.0f),
            StrategicManagementIds.ResourceWood => new Color(0.74f, 0.56f, 0.34f, 1.0f),
            StrategicManagementIds.ResourceOre => new Color(0.70f, 0.82f, 1.0f, 1.0f),
            _ => new Color(0.88f, 0.94f, 0.88f, 1.0f)
        };
    }
}
