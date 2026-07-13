using Rpg.Definitions.StrategicManagement;
using Rpg.Domain.StrategicManagement;

namespace Rpg.Application.StrategicManagement;

public static class StrategicManagementRuntime
{
    public const string DefaultSavePath = "user://saves/strategic_management_autosave.json";
    private static readonly StrategicManagementStateInvariantService Invariants = new();
    private static readonly StrategicManagementGeographyInvariantService GeographyInvariants = new();

    public static StrategicManagementDefinitionSet Definitions { get; private set; }
    public static StrategicManagementState State { get; private set; }
    public static StrategicManagementRules Rules { get; private set; }
    public static StrategicManagementCommandService Commands { get; private set; }
    public static StrategicWorldTimeflowController Timeflow { get; private set; }
    public static StrategicManagementViewModelService ViewModels { get; private set; }
    public static StrategicManagementSaveService SaveService { get; private set; }

    public static void EnsureInitialized()
    {
        EnsureInitialized(FirstStrategicManagementDefinitions.Create());
    }

    public static void EnsureInitialized(StrategicManagementDefinitionSet expectedDefinitions)
    {
        if (Definitions != null &&
            State != null &&
            Rules != null &&
            Commands != null &&
            Timeflow != null &&
            ViewModels != null &&
            SaveService != null)
        {
            if (Definitions.ContentIdentity != expectedDefinitions.ContentIdentity)
            {
                throw new System.InvalidOperationException(
                    $"Strategic Management content identity mismatch runtime={Definitions.ContentIdentity} selected={expectedDefinitions.ContentIdentity}");
            }
            GeographyInvariants.ThrowIfInvalid(Definitions, State, "runtime-existing");
            Invariants.RepairAll(State);
            return;
        }

        Definitions = expectedDefinitions;
        State = FirstStrategicManagementStateFactory.CreatePlayerStart(Definitions);
        GeographyInvariants.ThrowIfInvalid(Definitions, State, "runtime-initialize");
        Rules = new StrategicManagementRules(Definitions);
        Commands = new StrategicManagementCommandService(Definitions, Rules);
        Timeflow = new StrategicWorldTimeflowController(Commands);
        ViewModels = new StrategicManagementViewModelService(Definitions, Rules);
        SaveService = new StrategicManagementSaveService(Definitions);
        Invariants.RepairAll(State);
    }

    public static void Reset()
    {
        Reset(FirstStrategicManagementDefinitions.Create());
    }

    public static void Reset(StrategicManagementDefinitionSet definitions)
    {
        Definitions = definitions;
        State = FirstStrategicManagementStateFactory.CreatePlayerStart(Definitions);
        GeographyInvariants.ThrowIfInvalid(Definitions, State, "runtime-reset");
        Rules = new StrategicManagementRules(Definitions);
        Commands = new StrategicManagementCommandService(Definitions, Rules);
        Timeflow = new StrategicWorldTimeflowController(Commands);
        ViewModels = new StrategicManagementViewModelService(Definitions, Rules);
        SaveService = new StrategicManagementSaveService(Definitions);
        Invariants.RepairAll(State);
    }

    public static void SaveCurrentState(string path = DefaultSavePath)
    {
        EnsureInitialized();
        Invariants.RepairAll(State);
        SaveService.Save(State, path);
    }

    public static void LoadSavedState(string path = DefaultSavePath)
    {
        EnsureInitialized();
        State = SaveService.Load(path);
        GeographyInvariants.ThrowIfInvalid(Definitions, State, "runtime-load");
        Invariants.RepairAll(State);
    }

    public static StrategicBattleSettlementCommitResult CommitBattleResult(
        Rpg.Application.StrategicBattleBridge.StrategicBattleActiveContext context,
        Rpg.Application.StrategicBattleBridge.StrategicBattleActiveContextToken expectedResultToken,
        Rpg.Application.StrategicBattleBridge.StrategicBattleResultSummary summary,
        string path = DefaultSavePath)
    {
        EnsureInitialized();
        StrategicBattleSettlementCommitService service = new(Definitions, SaveService);
        return service.Commit(State, context, expectedResultToken, summary, path, candidate => State = candidate);
    }

    public static StrategicManagementDashboardViewModel BuildDashboard(string factionId, string cityId)
    {
        EnsureInitialized();
        return ViewModels.BuildDashboard(State, factionId, cityId);
    }

    public static StrategicManagementDashboardViewModel BuildLocationDashboard(string factionId, string locationId)
    {
        EnsureInitialized();
        return ViewModels.BuildLocationDashboard(State, factionId, locationId);
    }

    public static StrategicManagementDashboardViewModel BuildHeroCorpsWorkbenchDashboard(string factionId, string cityId, string heroId)
    {
        EnsureInitialized();
        return ViewModels.BuildHeroCorpsWorkbenchDashboard(State, factionId, cityId, heroId);
    }

    public static void PauseWorldTimeForCityManagement()
    {
        EnsureInitialized();
        Timeflow.PauseForCityManagement();
    }

    public static void ResumeWorldMapTime()
    {
        EnsureInitialized();
        Timeflow.ResumeWorldMapTime();
    }

    public static StrategicCommandResult SettleElapsedWorldTime(int elapsedPulses)
    {
        EnsureInitialized();
        return Timeflow.SettleElapsedWorldTime(State, StrategicManagementIds.FactionPlayer, elapsedPulses);
    }
}
