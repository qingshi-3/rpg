using Rpg.Definitions.World;
using Rpg.Domain.World;

namespace Rpg.Application.World;

public static class StrategicWorldRuntime
{
    private static readonly StrategicWorldService WorldService = new();

    public static StrategicWorldDefinition Definition { get; private set; }
    public static StrategicWorldState State { get; private set; }
    public static string LastNotice { get; set; } = "";
    private static string PendingSiteVisitSiteId { get; set; } = "";
    private static string PendingSiteVisitReturnScenePath { get; set; } = "";

    public static void EnsureInitialized()
    {
        Definition ??= StrategicWorldV1DefinitionFactory.Create();
        State ??= WorldService.CreateInitialState(Definition);
    }

    public static void Reset()
    {
        Definition = StrategicWorldV1DefinitionFactory.Create();
        State = WorldService.CreateInitialState(Definition);
        ClearPendingSiteVisit();
        LastNotice = "战略世界已重置。";
    }

    public static void ReplaceState(StrategicWorldState state)
    {
        Definition ??= StrategicWorldV1DefinitionFactory.Create();
        State = state ?? WorldService.CreateInitialState(Definition);
    }

    public static void BeginSiteVisit(string siteId, string returnScenePath)
    {
        PendingSiteVisitSiteId = siteId ?? "";
        PendingSiteVisitReturnScenePath = returnScenePath ?? "";
    }

    public static bool TryConsumePendingSiteVisit(out string siteId, out string returnScenePath)
    {
        siteId = PendingSiteVisitSiteId;
        returnScenePath = PendingSiteVisitReturnScenePath;
        ClearPendingSiteVisit();
        return !string.IsNullOrWhiteSpace(siteId);
    }

    public static void ClearPendingSiteVisit()
    {
        PendingSiteVisitSiteId = "";
        PendingSiteVisitReturnScenePath = "";
    }
}
