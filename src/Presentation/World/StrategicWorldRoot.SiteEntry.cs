using System.Linq;
using Godot;
using Rpg.Application.Battle;
using Rpg.Application.World;
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
            ReturnScenePath = returnScenePath
        });
        if (transition.Success)
        {
            return;
        }

        StrategicWorldRuntime.LastNotice = "无法进入场域。";
        GameLog.Warn(
            nameof(StrategicWorldRoot),
            $"Cannot enter site detail site={_selectedSiteId} path={SiteScenePath} error={transition.Error} reason={transition.FailureReason}");
        RefreshAll();
    }

    private bool CanEnterSelectedSiteDetail(out string failureReason)
    {
        failureReason = "";
        if (HasAttackingThreat())
        {
            failureReason = "attacking_threat_pending";
            return false;
        }

        if (string.IsNullOrWhiteSpace(_selectedSiteId) ||
            !State.SiteStates.TryGetValue(_selectedSiteId, out WorldSiteState site))
        {
            failureReason = "missing_site";
            return false;
        }

        WorldSiteDefinition definition = new StrategicWorldDefinitionQueries(Definition).GetSite(site.SiteId);
        WorldIntelVisibility visibility = GetSiteIntelVisibility(definition);
        if (visibility != WorldIntelVisibility.Visible)
        {
            failureReason = "site_not_visible";
            return false;
        }

        WorldSiteIntelViewModel intelView = WorldSiteIntelService.BuildCurrentView(
            State,
            Definition,
            site.SiteId,
            WorldIntelVisibility.Visible);

        if (site.OwnerFactionId != State.PlayerFactionId && intelView.CanInspectFullTacticalLayout)
        {
            return true;
        }

        if (site.OwnerFactionId != State.PlayerFactionId ||
            site.ControlState is not (SiteControlState.PlayerHeld or SiteControlState.Damaged))
        {
            failureReason = "site_not_owned";
            return false;
        }

        return true;
    }

    private bool CanShowSelectedSiteDetailEntry(WorldSiteState site)
    {
        if (site == null)
        {
            return false;
        }

        WorldSiteDefinition definition = new StrategicWorldDefinitionQueries(Definition).GetSite(site.SiteId);
        if (GetSiteIntelVisibility(definition) != WorldIntelVisibility.Visible)
        {
            return false;
        }

        if (site.OwnerFactionId == State.PlayerFactionId &&
            site.ControlState is SiteControlState.PlayerHeld or SiteControlState.Damaged)
        {
            return true;
        }

        WorldSiteIntelViewModel intelView = WorldSiteIntelService.BuildCurrentView(
            State,
            Definition,
            site.SiteId,
            WorldIntelVisibility.Visible);
        return intelView.CanInspectFullTacticalLayout;
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
        AddMutedLine(_actionList, $"部队已抵达{ResolveSiteDisplayName(army.TargetSiteId)}，请选择进入方式。");

        Button assaultButton = GameUiSceneFactory.CreateWorldPrimaryActionButton(nameof(StrategicWorldRoot));
        if (assaultButton == null)
        {
            return;
        }

        assaultButton.Text = $"{WorldSiteIntelPresenter.GetDirectAssaultLabel()}\n进入攻占战";
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
            TargetSiteId = viewModel.TargetSiteId,
            TargetSlotId = viewModel.TargetSlotId,
            ThreatId = viewModel.ThreatId
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
        if (HasAttackingThreat())
        {
            _worldClockPaused = true;
        }

        if (result.BattleStartRequest != null)
        {
            TryEnterBattle(result.BattleStartRequest, result.Events);
            return;
        }

        RefreshAll();
    }
}
