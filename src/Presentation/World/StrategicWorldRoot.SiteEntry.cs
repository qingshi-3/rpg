using System.Linq;
using Godot;
using Rpg.Application.StrategicManagement;
using Rpg.Application.World;
using Rpg.Definitions.StrategicManagement;
using Rpg.Definitions.World;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;
using Rpg.Infrastructure.Scenes;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.World;

public partial class StrategicWorldRoot
{
    private void EnterSelectedSiteDetail()
    {
        if (!CanEnterSelectedSiteDetail(out string failureReason))
        {
            StrategicWorldRuntime.LastNotice = WorldActionResolver.FormatFailureReason(failureReason);
            RefreshAll();
            return;
        }

        string returnScenePath = string.IsNullOrWhiteSpace(SceneFilePath)
            ? "res://scenes/world/StrategicWorldRoot.tscn"
            : SceneFilePath;
        StrategicWorldRuntime.LastNotice = $"进入{ResolveSiteDisplayName(_selectedSiteId)}。";
        _worldClockPaused = true;

        SceneTransitionResult transition = _sceneTransitionRouter.EnterSiteDetail(new SceneTransitionSiteVisitRequest
        {
            SiteId = _selectedSiteId,
            TargetScenePath = SiteScenePath,
            ReturnScenePath = returnScenePath,
            // Scene entry, not button press, is the Strategic Management pause boundary.
            OnEntered = StrategicManagementRuntime.PauseWorldTimeForCityManagement
        });
        if (transition.Success)
        {
            return;
        }

        StrategicWorldRuntime.LastNotice = "无法进入场地。";
        GameLog.Warn(nameof(StrategicWorldRoot), $"Cannot enter site detail site={_selectedSiteId} path={SiteScenePath} error={transition.Error} reason={transition.FailureReason}");
        RefreshAll();
    }

    private bool CanEnterSelectedSiteDetail(out string failureReason)
    {
        failureReason = "";
        if (!TryBuildSelectedStrategicLocationDashboard(out StrategicManagementDashboardViewModel dashboard))
        {
            failureReason = "missing_site";
            return false;
        }

        if (!dashboard.SelectedLocation.CanManageCity)
        {
            failureReason = "site_not_owned";
            return false;
        }

        return true;
    }

    private bool CanShowSelectedSiteDetailEntry()
    {
        return TryBuildSelectedStrategicLocationDashboard(out StrategicManagementDashboardViewModel dashboard) &&
               dashboard.SelectedLocation.CanManageCity;
    }

    private bool TryBuildSelectedStrategicLocationDashboard(out StrategicManagementDashboardViewModel dashboard)
    {
        return TryBuildStrategicLocationDashboardForMapSite(_selectedSiteId, out dashboard);
    }

    private static bool TryBuildStrategicLocationDashboardForMapSite(
        string mapSiteId,
        out StrategicManagementDashboardViewModel dashboard)
    {
        dashboard = null;
        if (string.IsNullOrWhiteSpace(mapSiteId))
        {
            return false;
        }

        // Large-map management actions must read Strategic Management ownership.
        // WorldSiteState is only a legacy map/scene cache and may lag after conquest.
        StrategicManagementRuntime.EnsureInitialized();
        if (!TemporaryLegacyStrategicSiteIdentityAdapter.TryResolveLocationId(mapSiteId, out string locationId))
        {
            return false;
        }

        dashboard = StrategicManagementRuntime.BuildLocationDashboard(
            StrategicManagementIds.FactionPlayer,
            locationId);
        return true;
    }

    private static bool TryBuildStrategicWorldMapSitePresentation(
        string mapSiteId,
        out StrategicWorldMapSitePresentation presentation)
    {
        presentation = null;
        if (string.IsNullOrWhiteSpace(mapSiteId))
        {
            return false;
        }

        StrategicManagementRuntime.EnsureInitialized();
        if (!TemporaryLegacyStrategicSiteIdentityAdapter.TryResolveLocationId(mapSiteId, out string locationId))
        {
            return false;
        }

        StrategicLocationDashboardViewModel location = StrategicManagementRuntime.ViewModels.BuildLocationMapView(
            StrategicManagementRuntime.State,
            StrategicManagementIds.FactionPlayer,
            locationId);
        presentation = StrategicWorldMapSitePresenter.Build(mapSiteId, location);
        return string.Equals(presentation.MapSiteId, mapSiteId, System.StringComparison.Ordinal);
    }

    private bool TryGetSelectedArrivedAssaultArmy(out WorldArmyState army)
    {
        army = null;
        if (string.IsNullOrWhiteSpace(_selectedSiteId) || State?.ArmyStates == null)
        {
            return false;
        }

        army = State.ArmyStates.Values.FirstOrDefault(item =>
            item.OwnerFactionId == State.PlayerFactionId &&
            item.TargetSiteId == _selectedSiteId &&
            item.Status == WorldArmyStatus.Attacking &&
            item.Intent == WorldArmyIntent.AssaultSite);
        return army != null;
    }

    private void AddArrivedAssaultChoiceButtons(WorldArmyState army)
    {
        AddMutedLine(_actionList, $"部队已抵达{ResolveSiteDisplayName(army.TargetSiteId)}，可触发战斗。");
        Button assaultButton = GameUiSceneFactory.CreateWorldPrimaryActionButton(nameof(StrategicWorldRoot));
        if (assaultButton == null)
        {
            return;
        }

        assaultButton.Text = "触发战斗";
        assaultButton.Pressed += () => TryEnterBattleForArrivedArmy(army.ArmyId);

        _actionList.AddChild(assaultButton);
    }

    private void ExecuteAction(WorldActionViewModel viewModel)
    {
        WorldActionRequest request = new()
        {
            ActionId = viewModel.ActionId,
            ActorFactionId = State.PlayerFactionId,
            SourceSiteId = _selectedSiteId,
            TargetSiteId = viewModel.TargetSiteId
        };

        string returnScenePath = string.IsNullOrWhiteSpace(SceneFilePath)
            ? "res://scenes/world/StrategicWorldRoot.tscn"
            : SceneFilePath;
        WorldActionResult result = _actionResolver.Apply(State, Definition, request, returnScenePath, SiteScenePath);
        StrategicWorldRuntime.LastNotice = result.Message;

        if (!result.Success)
        {
            RefreshAll();
            return;
        }

        _worldClockAccumulator = 0.0;
        if (result.BattleStartRequest != null)
        {
            TryEnterBattle(result.BattleStartRequest, result.Events);
            return;
        }

        RefreshAll();
    }
}
