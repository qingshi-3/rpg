using System.Reflection;
using Godot;
using Rpg.Application.Battle;
using Rpg.Application.StrategicBattleBridge;
using Rpg.Application.StrategicManagement;
using Rpg.Definitions.StrategicManagement;
using Rpg.Domain.StrategicManagement;

internal static partial class StrategicManagementRegressionCases
{
    internal static void StrategicWorldControlProjectionFollowsVictoryAuthority()
    {
        var setup = ApplyStrategicWorldBattleOutcome(BattleOutcome.Victory, 72);
        object projection = BuildStrategicWorldControlProjection(setup.Definitions, setup.State);

        AssertStrategicWorldControlProjection(
            projection,
            new Color(0.52f, 0.84f, 0.68f, 1.0f),
            "玩家控制",
            canAttack: false,
            canReinforce: true,
            StrategicExpeditionIntent.ReinforceLocation);
        AssertNextStrategicWorldCommandSucceeds(setup.Commands, setup.State, projection);
    }

    internal static void StrategicWorldControlProjectionFollowsDefeatAuthority()
    {
        var setup = ApplyStrategicWorldBattleOutcome(BattleOutcome.Defeat, 0);
        object projection = BuildStrategicWorldControlProjection(setup.Definitions, setup.State);

        AssertStrategicWorldControlProjection(
            projection,
            new Color(0.88f, 0.38f, 0.34f, 1.0f),
            "敌方控制",
            canAttack: true,
            canReinforce: false,
            StrategicExpeditionIntent.AssaultLocation);
        AssertNextStrategicWorldCommandSucceeds(setup.Commands, setup.State, projection);
    }

    internal static void StrategicWorldControlProjectionSurvivesPersistedReload()
    {
        var setup = ApplyStrategicWorldBattleOutcome(BattleOutcome.Victory, 72);
        string savePath = Path.Combine(Path.GetTempPath(), $"rpg-a4-strategic-world-{Guid.NewGuid():N}.json");
        try
        {
            StrategicManagementSaveService saveService = new(setup.Definitions);
            saveService.Save(setup.State, savePath);
            StrategicManagementState loaded = saveService.Load(savePath);
            StrategicManagementRules loadedRules = new(setup.Definitions);
            StrategicManagementCommandService loadedCommands = new(setup.Definitions, loadedRules);
            object projection = BuildStrategicWorldControlProjection(setup.Definitions, loaded);

            AssertStrategicWorldControlProjection(
                projection,
                new Color(0.52f, 0.84f, 0.68f, 1.0f),
                "玩家控制",
                canAttack: false,
                canReinforce: true,
                StrategicExpeditionIntent.ReinforceLocation);
            AssertNextStrategicWorldCommandSucceeds(loadedCommands, loaded, projection);
        }
        finally
        {
            DeleteIfExists(savePath);
            DeleteIfExists(savePath + ".previous");
            DeleteIfExists(savePath + ".staging");
        }
    }

    private static (
        StrategicManagementDefinitionSet Definitions,
        StrategicManagementState State,
        StrategicManagementCommandService Commands) ApplyStrategicWorldBattleOutcome(
        BattleOutcome outcome,
        int remainingCorpsStrength)
    {
        var setup = CreateStrategicAssaultExpedition();
        StrategicBattleBridgeService bridge = new(setup.Definitions);
        StrategicBattleSession session = bridge.CreateSession(
            setup.State,
            setup.ExpeditionId,
            "res://return_to_world.tscn",
            "res://scenes/world/sites/WorldSiteRoot.tscn").Session;
        StrategicBattleResultSummary summary = new()
        {
            SessionId = session.SessionId,
            ExpeditionId = setup.ExpeditionId,
            TargetLocationId = StrategicManagementIds.LocationBonefieldOutpost,
            Outcome = outcome,
            ObjectiveSucceeded = outcome == BattleOutcome.Victory
        };
        summary.Participants.Add(new StrategicBattleParticipantResult
        {
            HeroId = StrategicManagementIds.HeroOrdinaryCommander,
            CorpsInstanceId = setup.CorpsInstanceId,
            RemainingCorpsStrength = remainingCorpsStrength
        });
        CompleteDirectBattleResultSummaryForTest(summary, session);

        StrategicCommandResult result = setup.Commands.ApplyBattleResultSummary(setup.State, summary);
        AssertTrue(result.Success, $"strategic battle outcome should apply before map projection, got {result.FailureReason}");
        return (setup.Definitions, setup.State, setup.Commands);
    }

    private static object BuildStrategicWorldControlProjection(
        StrategicManagementDefinitionSet definitions,
        StrategicManagementState state)
    {
        StrategicManagementRules rules = new(definitions);
        StrategicManagementViewModelService viewModels = new(definitions, rules);
        StrategicManagementDashboardViewModel dashboard = viewModels.BuildLocationDashboard(
            state,
            StrategicManagementIds.FactionPlayer,
            StrategicManagementIds.LocationBonefieldOutpost);

        Type presenterType = typeof(StrategicManagementViewModelService).Assembly.GetType(
            "Rpg.Presentation.World.StrategicWorldMapSitePresenter") ??
            throw new InvalidOperationException("strategic world map control presenter is missing");
        MethodInfo buildMethod = presenterType.GetMethod("Build", BindingFlags.Public | BindingFlags.Static) ??
                                 throw new InvalidOperationException("strategic world map control presenter public Build method is missing");
        return buildMethod.Invoke(null, new object[] { dashboard.SelectedLocation }) ??
               throw new InvalidOperationException("strategic world map control presenter returned null");
    }

    private static void AssertStrategicWorldControlProjection(
        object projection,
        Color expectedColor,
        string expectedControlText,
        bool canAttack,
        bool canReinforce,
        StrategicExpeditionIntent expectedNextCommand)
    {
        AssertEqual(StrategicManagementIds.LocationBonefieldOutpost, GetRequiredProperty<string>(projection, "LocationId"), "map projection should preserve the stable strategic location id");
        AssertEqual(StrategicManagementIds.MapSiteBonefield, GetRequiredProperty<string>(projection, "MapSiteId"), "map projection should preserve the stable map-site mapping");
        AssertEqual(expectedColor, GetRequiredProperty<Color>(projection, "ControlColor"), "map color should follow Strategic Management control");
        AssertEqual(expectedControlText, GetRequiredProperty<string>(projection, "ControlText"), "map control text should follow Strategic Management control");
        AssertEqual(canAttack, GetRequiredProperty<bool>(projection, "CanAttack"), "map attackability should follow Strategic Management rules");
        AssertEqual(canReinforce, GetRequiredProperty<bool>(projection, "CanReinforce"), "map reinforcement eligibility should follow Strategic Management rules");
        AssertEqual(expectedNextCommand, GetRequiredProperty<StrategicExpeditionIntent>(projection, "NextCommand"), "map next command should agree with Strategic Management target classification");
    }

    private static void AssertNextStrategicWorldCommandSucceeds(
        StrategicManagementCommandService commands,
        StrategicManagementState state,
        object projection)
    {
        StrategicExpeditionIntent nextCommand = GetRequiredProperty<StrategicExpeditionIntent>(projection, "NextCommand");
        StrategicExpeditionIntent rejectedCommand = nextCommand == StrategicExpeditionIntent.ReinforceLocation
            ? StrategicExpeditionIntent.AssaultLocation
            : StrategicExpeditionIntent.ReinforceLocation;
        StrategicCommandResult rejected = commands.CreateExpedition(
            state,
            StrategicManagementIds.LocationPlainsCity,
            StrategicManagementIds.LocationBonefieldOutpost,
            rejectedCommand,
            StrategicManagementIds.HeroArcherCaptain);
        AssertTrue(!rejected.Success, "the command opposite to the Strategic Management target classification should remain rejected by rules");

        StrategicCommandResult result = commands.CreateExpedition(
            state,
            StrategicManagementIds.LocationPlainsCity,
            StrategicManagementIds.LocationBonefieldOutpost,
            nextCommand,
            StrategicManagementIds.HeroArcherCaptain);
        AssertTrue(result.Success, $"projected next command should remain enforced and accepted by Strategic Management rules, got {result.FailureReason}");
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
