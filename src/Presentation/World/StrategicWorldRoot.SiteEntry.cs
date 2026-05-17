using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle;
using Rpg.Application.World;
using Rpg.Definitions.World;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Battle.Entities;
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
        StrategicWorldRuntime.BeginSiteVisit(_selectedSiteId, returnScenePath);
        StrategicWorldRuntime.LastNotice = $"进入{ResolveSiteDisplayName(_selectedSiteId)}。";
        _worldClockPaused = true;

        Error error = GetTree().ChangeSceneToFile(SiteScenePath);
        if (error == Error.Ok)
        {
            return;
        }

        StrategicWorldRuntime.ClearPendingSiteVisit();
        StrategicWorldRuntime.LastNotice = "无法进入场域。";
        GameLog.Warn(nameof(StrategicWorldRoot), $"Cannot enter site detail site={_selectedSiteId} path={SiteScenePath} error={error}");
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

    private bool CanExploreSelectedSite(WorldSiteState site)
    {
        if (site == null)
        {
            return false;
        }

        WorldSiteDefinition definition = new StrategicWorldDefinitionQueries(Definition).GetSite(site.SiteId);
        return definition?.ExplorationPatrols?.Count > 0;
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
        AddMutedLine(_actionList, $"部队已抵达{ResolveSiteDisplayName(army.TargetSiteId)}，选择进入方式。");

        Button assaultButton = GameUiSceneFactory.CreateWorldPrimaryActionButton(nameof(StrategicWorldRoot));
        if (assaultButton != null)
        {
            assaultButton.Text = $"{WorldSiteIntelPresenter.GetDirectAssaultLabel()}\n进入攻占战";
            assaultButton.Pressed += () => TryEnterBattleForArrivedArmy(army.ArmyId);
            _actionList.AddChild(assaultButton);
        }

        int arrivedAssaultArmyCount = CountArrivedAssaultArmiesAtSite(army.TargetSiteId);
        bool canInfiltrate = arrivedAssaultArmyCount == 1 &&
                             GetArmyUnitCount(army) == 1 &&
                             State.SiteStates.TryGetValue(army.TargetSiteId, out WorldSiteState site) &&
                             CanExploreSelectedSite(site);
        Button infiltrateButton = GameUiSceneFactory.CreateWorldPrimaryActionButton(nameof(StrategicWorldRoot));
        if (infiltrateButton != null)
        {
            infiltrateButton.Text = canInfiltrate ? "进入探索\n进入场域探索" : "进入探索\n该场域没有探索配置";
            infiltrateButton.Disabled = !canInfiltrate;
            if (canInfiltrate)
            {
                infiltrateButton.Pressed += () => EnterSelectedSiteInfiltration(army.ArmyId);
            }

            _actionList.AddChild(infiltrateButton);
        }
    }

    private static int GetArmyUnitCount(WorldArmyState army)
    {
        return army?.GarrisonUnits?.Sum(unit => System.Math.Max(0, unit.Count)) ?? 0;
    }

    private void EnterSelectedSiteInfiltration(string armyId)
    {
        if (string.IsNullOrWhiteSpace(armyId) ||
            !State.ArmyStates.TryGetValue(armyId, out WorldArmyState army) ||
            army.Status != WorldArmyStatus.Attacking ||
            army.Intent != WorldArmyIntent.AssaultSite)
        {
            StrategicWorldRuntime.LastNotice = "潜入部队状态已失效。";
            RefreshAll();
            return;
        }

        string returnScenePath = string.IsNullOrWhiteSpace(SceneFilePath)
            ? "res://scenes/world/StrategicWorldRoot.tscn"
            : SceneFilePath;
        // Infiltration is a site visit with an army context; it must not enter battle or wartime mode here.
        GameLog.Info(
            nameof(StrategicWorldRoot),
            $"SiteInfiltrationSelected army={army.ArmyId} target={army.TargetSiteId} status={army.Status} intent={army.Intent} units={FormatArmyUnitsForLog(army)} returnScene={returnScenePath}");
        StrategicWorldRuntime.BeginSiteVisit(army.TargetSiteId, returnScenePath, army.ArmyId);
        StrategicWorldRuntime.LastNotice = $"派出{BuildArmyUnitSummary(army)}潜入{ResolveSiteDisplayName(army.TargetSiteId)}。";
        _worldClockPaused = true;

        Error error = GetTree().ChangeSceneToFile(SiteScenePath);
        if (error == Error.Ok)
        {
            return;
        }

        StrategicWorldRuntime.ClearPendingSiteVisit();
        StrategicWorldRuntime.LastNotice = "无法进入潜入场域。";
        GameLog.Warn(nameof(StrategicWorldRoot), $"Cannot enter site infiltration army={army.ArmyId} site={army.TargetSiteId} path={SiteScenePath} error={error}");
        RefreshAll();
    }

    private int CountArrivedAssaultArmiesAtSite(string siteId)
    {
        if (string.IsNullOrWhiteSpace(siteId) || State?.ArmyStates == null)
        {
            return 0;
        }

        return State.ArmyStates.Values.Count(item =>
            item.OwnerFactionId == State.PlayerFactionId &&
            item.TargetSiteId == siteId &&
            item.Status == WorldArmyStatus.Attacking &&
            item.Intent == WorldArmyIntent.AssaultSite);
    }

    private string BuildArmyUnitSummary(WorldArmyState army)
    {
        if (army?.GarrisonUnits == null || army.GarrisonUnits.Count == 0)
        {
            return "部队";
        }

        return string.Join("、", army.GarrisonUnits.Select(unit => $"{GetUnitLabel(unit.UnitTypeId)}x{unit.Count}"));
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
