using System.Collections.Generic;
using System.Diagnostics;
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
    private void RefreshAll()
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        StrategicWorldDefinitionQueries queries = new(Definition);
        RefreshResources();
        RefreshSiteButtons(queries);
        RefreshCurrentStrategicWorldPanel(queries);
        RefreshWorldClockLabel();
        _noticeLabel.Text = StrategicWorldRuntime.LastNotice;
        RefreshStrategicFog();
        QueueStrategicOverlayRedraw();
        stopwatch.Stop();
        if (stopwatch.ElapsedMilliseconds >= 16)
        {
            GameLog.Info(
                nameof(StrategicWorldRoot),
                $"StrategicRefreshAllCost selectedSite={_selectedSiteId} selectedOpportunity={_selectedOpportunityId} elapsedMs={stopwatch.ElapsedMilliseconds}");
        }
    }

    private WorldUiMode ResolveStrategicWorldUiMode()
    {
        return _isExpeditionDrafting || _isExpeditionTargeting
            ? WorldUiMode.ExpeditionDraft
            : WorldUiMode.StrategicSelection;
    }

    private void RefreshCurrentStrategicWorldPanel(StrategicWorldDefinitionQueries queries)
    {
        switch (ResolveStrategicWorldUiMode())
        {
            case WorldUiMode.ExpeditionDraft:
                BindExpeditionDraftPanel(queries);
                break;
            default:
                BindStrategicSelectionPanel(queries);
                break;
        }
    }

    private void BindStrategicSelectionPanel(StrategicWorldDefinitionQueries queries)
    {
        RefreshDetail(queries);
        RefreshActions();
    }

    private void BindExpeditionDraftPanel(StrategicWorldDefinitionQueries queries)
    {
        RefreshDetail(queries);
        RefreshActions();
    }

    private void RefreshDetail(StrategicWorldDefinitionQueries queries)
    {
        if (TryRefreshOpportunityDetail(queries))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_selectedSiteId) || !State.SiteStates.TryGetValue(_selectedSiteId, out WorldSiteState site))
        {
            _selectedSiteId = "";
            HideWorldDetailSections();
            return;
        }

        SetSiteDetailSectionsVisible(true);
        WorldSiteDefinition definition = queries.GetSite(_selectedSiteId);
        List<string> detailLines = new()
        {
            definition.Description,
            ""
        };
        detailLines.AddRange(new[]
        {
            $"状态：{GetControlStateLabel(site.ControlState)}",
            $"模式：{GetSiteModeLabel(site.SiteMode)}",
            $"归属：{StrategicWorldDisplayNames.GetFactionLabel(queries, site.OwnerFactionId)}",
            $"受损：{site.DamageLevel}"
        });

        _siteTitleLabel.Text = $"{definition.DisplayName}  ·  {GetSiteKindLabel(definition.SiteKind)}";
        _siteBodyLabel.Text = string.Join("\n", detailLines);

        ClearChildren(_facilityList);
        if (site.Facilities.Count == 0)
        {
            AddMutedLine(_facilityList, "无");
        }
        else
        {
            foreach (FacilityInstance facility in site.Facilities)
            {
                FacilityDefinition facilityDefinition = queries.GetFacility(facility.FacilityId);
                string extra = facility.FacilityId == StrategicWorldIds.FacilityMine
                    ? $"  占用{StrategicWorldDisplayNames.GetResourceLabel(queries, StrategicWorldIds.ResourcePopulation)} {facility.AssignedPopulation}  产出 {StrategicWorldDisplayNames.GetResourceLabel(queries, StrategicWorldIds.ResourceStone)} +2/世界步"
                    : facility.FacilityId == StrategicWorldIds.FacilityDefenseTower
                        ? "  防守 +3  塔支援 1 次"
                        : "";
                AddMutedLine(_facilityList, $"{facilityDefinition?.DisplayName ?? facility.FacilityId}  {GetFacilityStateLabel(facility.State)}{extra}");
            }
        }

        ClearChildren(_garrisonList);
        AddSiteGarrisonLines(_garrisonList, site);
    }

    private void AddSiteGarrisonLines(VBoxContainer list, WorldSiteState site)
    {
        if (list == null)
        {
            return;
        }

        if (site?.Garrison == null || site.Garrison.Count == 0)
        {
            AddMutedLine(list, "无");
        }
        else
        {
            foreach (GarrisonState garrison in site.Garrison)
            {
                AddMutedLine(list, $"{GetUnitLabel(garrison.UnitTypeId)} x{garrison.Count}");
            }
        }
    }

    private bool TryRefreshStaleSiteDetail(StrategicWorldDefinitionQueries queries, WorldSiteDefinition definition)
    {
        return false;
    }

    private bool TryRefreshOpportunityDetail(StrategicWorldDefinitionQueries queries)
    {
        if (!TryGetSelectedActiveOpportunity(out WorldOpportunityState opportunity))
        {
            return false;
        }

        if (_opportunityDetailPanel == null)
        {
            _selectedOpportunityId = "";
            GameLog.Warn(nameof(StrategicWorldRoot), "Missing WorldOpportunityDetailPanel scene instance.");
            return false;
        }

        WorldOpportunityDefinition definition = queries.GetOpportunity(opportunity.DefinitionId);
        OpportunitySpawnPointDefinition spawnPoint = queries.GetOpportunitySpawnPoint(opportunity.SpawnPointId);
        int remainingTicks = System.Math.Max(0, opportunity.ExpiresTick - State.WorldTick);
        SetSiteDetailSectionsVisible(false);
        _opportunityDetailPanel.Visible = true;
        _opportunityDetailPanel.Bind(new WorldOpportunityDetailPanelData
        {
            Title = $"野外小场域 · {definition?.DisplayName ?? opportunity.DefinitionId}",
            Description = definition?.Description ?? "临时出现的野外机会。",
            StatusText = GetOpportunityStatusLabel(opportunity.Status),
            SpawnPointText = spawnPoint?.DisplayName ?? opportunity.SpawnPointId,
            RemainingText = $"{remainingTicks} 世界步",
            RewardText = BuildOpportunityRewardText(queries, definition)
        });
        return true;
    }

    private void SetSiteDetailSectionsVisible(bool visible)
    {
        if (_siteSummaryCard != null)
        {
            _siteSummaryCard.Visible = visible;
        }

        if (_facilityCard != null)
        {
            _facilityCard.Visible = visible;
        }

        if (_defenseCard != null)
        {
            _defenseCard.Visible = visible;
        }

        if (_actionCard != null)
        {
            _actionCard.Visible = visible;
        }

        if (_opportunityCard != null)
        {
            _opportunityCard.Visible = !visible;
        }

        if (_opportunityDetailPanel != null)
        {
            _opportunityDetailPanel.Visible = !visible;
        }
    }

    private void HideWorldDetailSections()
    {
        SetSiteDetailSectionsVisible(false);
        if (_opportunityDetailPanel != null)
        {
            _opportunityDetailPanel.Visible = false;
        }
    }

    private string BuildArmyArrivalText(WorldArmyState army)
    {
        float remainingDistance = army.WorldPosition.DistanceTo(army.Destination);
        double movementSpeed = System.Math.Max(1.0, army.MoveSpeed * WorldClockSpeedMultipliers[_worldClockSpeedIndex]);
        double etaSeconds = System.Math.Ceiling(remainingDistance / movementSpeed);
        return $"敌军行军中  预计 {etaSeconds:0}s 抵达";
    }

    private void RefreshActions()
    {
        ClearChildren(_actionList);
        if (string.IsNullOrWhiteSpace(_selectedSiteId) &&
            !_isExpeditionDrafting &&
            !TryGetSelectedActiveOpportunity(out _))
        {
            return;
        }

        if (RefreshExpeditionControls())
        {
            return;
        }

        if (TryGetSelectedActiveOpportunity(out _))
        {
            return;
        }

        IReadOnlyList<WorldActionViewModel> actions = string.IsNullOrWhiteSpace(_selectedSiteId)
            ? System.Array.Empty<WorldActionViewModel>()
            : _actionResolver.GetAvailableActions(State, Definition, _selectedSiteId);
        foreach (WorldActionViewModel action in actions)
        {
            if (!ShouldShowStrategicAction(action))
            {
                continue;
            }

            Button button = GameUiSceneFactory.CreateWorldPrimaryActionButton(nameof(StrategicWorldRoot));
            if (button == null)
            {
                continue;
            }

            button.Text = BuildActionButtonText(action);
            button.Disabled = !action.IsEnabled;

            if (action.IsEnabled)
            {
                button.Pressed += () => ExecuteAction(action);
            }

            _actionList.AddChild(button);
        }

        if (_actionList.GetChildCount() == 0)
        {
            AddMutedLine(_actionList, "暂无可执行行动");
        }
    }

    private bool ShouldShowStrategicAction(WorldActionViewModel action)
    {
        if (action == null)
        {
            return false;
        }

        WorldActionDefinition definition = new StrategicWorldDefinitionQueries(Definition).GetAction(action.ActionId);
        return definition == null ||
               definition.Scope is not WorldActionScope.Site and not WorldActionScope.Facility;
    }

    private void CompleteSelectedOpportunity()
    {
        if (!TryGetSelectedActiveOpportunity(out WorldOpportunityState opportunity))
        {
            StrategicWorldRuntime.LastNotice = "野外小场域已经消失。";
            _selectedOpportunityId = "";
            RefreshAll();
            return;
        }

        WorldActionResult result = _opportunityService.CompleteOpportunity(State, Definition, opportunity.OpportunityId);
        StrategicWorldRuntime.LastNotice = result.Message;
        if (result.Success)
        {
            _selectedOpportunityId = "";
        }

        RefreshAll();
    }
}
