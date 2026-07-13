using Godot;
using Rpg.Application.StrategicManagement;
using Rpg.Domain.StrategicManagement;

namespace Rpg.Presentation.World;

public sealed class StrategicWorldMapSitePresentation
{
    public string LocationId { get; init; } = "";
    public string MapSiteId { get; init; } = "";
    public string OwnerFactionId { get; init; } = "";
    public string ControlText { get; init; } = "";
    public Color ControlColor { get; init; } = Colors.White;
    public bool CanAttack { get; init; }
    public bool CanReinforce { get; init; }
    public StrategicExpeditionIntent NextCommand { get; init; } = StrategicExpeditionIntent.Unknown;
    public string CommandDisabledReason { get; init; } = "";
}

public static class StrategicWorldMapSitePresenter
{
    public static StrategicWorldMapSitePresentation BuildUnavailable(string mapSiteId)
    {
        return new StrategicWorldMapSitePresentation
        {
            MapSiteId = mapSiteId ?? "",
            ControlText = "未接入战略管理",
            ControlColor = Colors.White,
            CommandDisabledReason = StrategicFailureReasons.MissingLocation
        };
    }

    public static StrategicWorldMapSitePresentation Build(
        string legacyMapSiteId,
        StrategicLocationDashboardViewModel location)
    {
        System.ArgumentNullException.ThrowIfNull(location);
        return new StrategicWorldMapSitePresentation
        {
            LocationId = location.LocationId,
            MapSiteId = legacyMapSiteId ?? "",
            OwnerFactionId = location.OwnerFactionId,
            ControlText = location.ControlStateDisplayName,
            ControlColor = ResolveControlColor(location.ControlState),
            CanAttack = location.CanAssault,
            CanReinforce = location.CanReinforce,
            NextCommand = location.PreferredExpeditionIntent,
            CommandDisabledReason = location.CommandDisabledReason
        };
    }

    private static Color ResolveControlColor(StrategicLocationControlState controlState)
    {
        return controlState switch
        {
            StrategicLocationControlState.PlayerHeld => new Color(0.52f, 0.84f, 0.68f, 1.0f),
            StrategicLocationControlState.EnemyHeld => new Color(0.88f, 0.38f, 0.34f, 1.0f),
            StrategicLocationControlState.Neutral => new Color(0.66f, 0.72f, 0.78f, 1.0f),
            _ => Colors.White
        };
    }
}
