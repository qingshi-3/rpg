using System.Collections.Generic;
using System.Linq;
using Rpg.Definitions.StrategicManagement;
using Rpg.Domain.StrategicManagement;

namespace Rpg.Application.StrategicManagement;

public sealed class StrategicManagementViewModelService
{
    private readonly StrategicManagementDefinitionSet _definitions;
    private readonly StrategicManagementRules _rules;

    public StrategicManagementViewModelService(
        StrategicManagementDefinitionSet definitions,
        StrategicManagementRules rules)
    {
        _definitions = definitions ?? new StrategicManagementDefinitionSet();
        _rules = rules ?? new StrategicManagementRules(_definitions);
    }

    // Strategic Presentation consumes this read model so Godot UI does not grow its own strategic rule authority.
    public StrategicManagementDashboardViewModel BuildDashboard(
        StrategicManagementState state,
        string factionId,
        string selectedCityId)
    {
        string scopedFactionId = factionId ?? "";
        string scopedCityId = selectedCityId ?? "";
        StrategicManagementDashboardViewModel dashboard = new()
        {
            FactionId = scopedFactionId,
            SelectedLocation = BuildSelectedLocation(state, scopedFactionId, scopedCityId),
            SelectedCity = BuildSelectedCity(state, scopedFactionId, scopedCityId),
            Resources = BuildResources(state, scopedFactionId),
            Heroes = BuildHeroes(state, scopedFactionId),
            LatestBattleFeedback = BuildLatestBattleFeedback(state, scopedCityId)
        };

        return dashboard;
    }

    public StrategicManagementDashboardViewModel BuildLocationDashboard(
        StrategicManagementState state,
        string factionId,
        string selectedLocationId)
    {
        string scopedFactionId = factionId ?? "";
        string scopedLocationId = selectedLocationId ?? "";
        StrategicLocationDashboardViewModel selectedLocation = BuildSelectedLocation(state, scopedFactionId, scopedLocationId);
        StrategicManagementDashboardViewModel dashboard = new()
        {
            FactionId = scopedFactionId,
            SelectedLocation = selectedLocation,
            SelectedCity = selectedLocation.CanManageCity
                ? BuildSelectedCity(state, scopedFactionId, selectedLocation.LocationId)
                : new StrategicCityManagementViewModel(),
            Resources = BuildResources(state, scopedFactionId),
            Heroes = BuildHeroes(state, scopedFactionId),
            LatestBattleFeedback = BuildLatestBattleFeedback(state, scopedLocationId)
        };

        return dashboard;
    }

    private List<StrategicResourceViewModel> BuildResources(
        StrategicManagementState state,
        string factionId)
    {
        return _definitions.Resources.Values
            .OrderBy(item => item.ResourceId)
            .Select(resource => new StrategicResourceViewModel
            {
                ResourceId = resource.ResourceId,
                DisplayName = resource.DisplayName,
                Amount = state?.GetResourceAmount(factionId, resource.ResourceId) ?? 0
            })
            .ToList();
    }

    private static StrategicBattleFeedbackViewModel BuildLatestBattleFeedback(
        StrategicManagementState state,
        string targetLocationId)
    {
        if (state == null)
        {
            return new StrategicBattleFeedbackViewModel();
        }

        StrategicBattleFeedbackRecord feedback = state.BattleFeedbackRecords.Values
            .Where(item => string.Equals(item.TargetLocationId, targetLocationId ?? "", System.StringComparison.Ordinal))
            .OrderByDescending(item => item.AppliedElapsedWorldTimePulses)
            .ThenByDescending(item => item.FeedbackId)
            .FirstOrDefault();
        if (feedback == null)
        {
            return new StrategicBattleFeedbackViewModel();
        }

        return new StrategicBattleFeedbackViewModel
        {
            FeedbackId = feedback.FeedbackId,
            ExpeditionId = feedback.ExpeditionId,
            SessionId = feedback.SessionId,
            TargetLocationId = feedback.TargetLocationId,
            TargetDisplayName = feedback.TargetDisplayName,
            Victory = feedback.Victory,
            OutcomeText = feedback.OutcomeText,
            WorldChangeText = feedback.WorldChangeText,
            FailureReasonText = feedback.FailureReasonText,
            ProgressionText = feedback.ProgressionText,
            RewardLines = new List<string>(feedback.RewardLines),
            ParticipantFeedback = feedback.ParticipantFeedback.Select(item => new StrategicBattleParticipantFeedbackViewModel
            {
                HeroId = item.HeroId,
                HeroDisplayName = item.HeroDisplayName,
                CorpsInstanceId = item.CorpsInstanceId,
                CorpsDisplayName = item.CorpsDisplayName,
                RemainingCorpsStrength = item.RemainingCorpsStrength,
                StrengthLoss = item.StrengthLoss,
                ResultText = item.ResultText
            }).ToList(),
            HeroFeedback = feedback.HeroFeedback.Select(item => new StrategicHeroBattleFeedbackViewModel
            {
                HeroId = item.HeroId,
                HeroDisplayName = item.HeroDisplayName,
                ReactionText = item.ReactionText
            }).ToList(),
            EquipmentSamples = feedback.EquipmentSamples.Select(item => new StrategicEquipmentSampleFeedbackViewModel
            {
                EquipmentSampleId = item.EquipmentSampleId,
                DisplayName = item.DisplayName,
                SlotKind = item.SlotKind,
                Grade = item.Grade,
                RoleText = item.RoleText,
                IsReward = item.IsReward
            }).ToList()
        };
    }

    private StrategicLocationDashboardViewModel BuildSelectedLocation(
        StrategicManagementState state,
        string factionId,
        string locationId)
    {
        string scopedLocationId = locationId ?? "";
        StrategicLocationDashboardViewModel locationView = new()
        {
            LocationId = scopedLocationId,
            DisplayName = scopedLocationId,
            KindDisplayName = FormatLocationKind(StrategicLocationKind.Unknown),
            ControlState = StrategicLocationControlState.Unknown,
            ControlStateDisplayName = FormatLocationControlState(StrategicLocationControlState.Unknown),
            SourcePermissionDisplayText = ""
        };

        if (_definitions.Locations.TryGetValue(scopedLocationId, out StrategicLocationDefinition definition))
        {
            locationView.LocationId = definition.LocationId;
            locationView.MapSiteId = definition.MapSiteId;
            locationView.DisplayName = string.IsNullOrWhiteSpace(definition.DisplayName)
                ? definition.LocationId
                : definition.DisplayName;
            locationView.Kind = definition.Kind;
            locationView.KindDisplayName = FormatLocationKind(definition.Kind);
            locationView.IsCity = definition.Kind == StrategicLocationKind.City;
            locationView.SourcePermissionTags = new List<string>(definition.SourcePermissionTags);
            locationView.SourcePermissionDisplayText = locationView.SourcePermissionTags.Count == 0
                ? ""
                : string.Join(" / ", locationView.SourcePermissionTags.Select(FormatSourcePermissionTag));
            locationView.ProductionPerWorldTimePulse = BuildProduction(definition.ProductionPerWorldTimePulse);
            locationView.ProductionDisplayText = BuildProductionDisplayText(locationView.ProductionPerWorldTimePulse);
        }

        if (state != null && state.Locations.TryGetValue(locationView.LocationId, out StrategicLocationState location))
        {
            locationView.OwnerFactionId = location.OwnerFactionId;
            locationView.ControlState = location.ControlState;
            locationView.ControlStateDisplayName = FormatLocationControlState(location.ControlState);
        }

        // City management is stricter than being a city definition: the requesting
        // faction must currently own and control this managed location.
        locationView.CanManageCity = locationView.IsCity &&
                                     state?.Cities.ContainsKey(locationView.LocationId) == true &&
                                     string.Equals(locationView.OwnerFactionId, factionId ?? "", System.StringComparison.Ordinal) &&
                                     locationView.ControlState == StrategicLocationControlState.PlayerHeld;
        return locationView;
    }

    private StrategicCityManagementViewModel BuildSelectedCity(
        StrategicManagementState state,
        string factionId,
        string cityId)
    {
        StrategicCityManagementViewModel cityView = new()
        {
            LocationId = cityId
        };

        if (state == null || !state.Cities.TryGetValue(cityId, out StrategicCityState city))
        {
            return cityView;
        }

        _definitions.Locations.TryGetValue(city.LocationId, out StrategicLocationDefinition location);
        _definitions.CityIdentities.TryGetValue(city.CityIdentityId, out StrategicCityIdentityDefinition identity);

        cityView.LocationId = city.LocationId;
        cityView.DisplayName = location?.DisplayName ?? city.LocationId;
        cityView.CityIdentityId = city.CityIdentityId;
        cityView.CityIdentityDisplayName = identity?.DisplayName ?? city.CityIdentityId;
        cityView.CityForceCapacity = city.CityForceCapacity;
        cityView.ReserveForces = city.ReserveForces;
        cityView.ActiveForces = _rules.GetActiveForces(state, city.LocationId);
        cityView.RemainingForceCapacity = _rules.GetRemainingCityForceCapacity(state, city.LocationId);
        cityView.Conscription = BuildConscription(state, city);
        cityView.ConstructionRegions = BuildConstructionRegions(city, location);
        cityView.Buildings = BuildBuildings(city, location);
        cityView.BuildingOptions = BuildBuildingOptions(state, city, location);
        cityView.MusterTemplates = BuildMusterTemplates(state, city.LocationId);
        cityView.CorpsInstances = BuildCorpsInstances(state, factionId, city.LocationId);
        cityView.HeroCompanies = BuildHeroCompanies(state, factionId, city.LocationId);
        return cityView;
    }

    private StrategicConscriptionViewModel BuildConscription(
        StrategicManagementState state,
        StrategicCityState city)
    {
        string currentIntensityId = string.IsNullOrWhiteSpace(city.AutoConscriptionIntensityId)
            ? StrategicManagementIds.ConscriptionOff
            : city.AutoConscriptionIntensityId;
        string manualFailure = _rules.GetManualConscriptionFailureReason(state, city.LocationId);
        StrategicConscriptionViewModel viewModel = new()
        {
            CurrentIntensityId = currentIntensityId,
            CityForceCapacity = city.CityForceCapacity,
            ReserveForces = city.ReserveForces,
            ActiveForces = _rules.GetActiveForces(state, city.LocationId),
            RemainingForceCapacity = _rules.GetRemainingCityForceCapacity(state, city.LocationId),
            ManualOption = new StrategicConscriptionManualOptionViewModel
            {
                ReserveGain = _rules.GetManualConscriptionReserveGain(),
                CanConscript = string.IsNullOrWhiteSpace(manualFailure),
                DisabledReason = manualFailure,
                Cost = BuildCosts(_rules.GetManualConscriptionCost())
            }
        };

        viewModel.IntensityOptions = _rules.GetAutoConscriptionIntensityRules()
            .Select(rule =>
            {
                string disabledReason = _rules.GetAutoConscriptionIntensityFailureReason(
                    state,
                    city.LocationId,
                    rule.IntensityId);
                return new StrategicConscriptionIntensityOptionViewModel
                {
                    IntensityId = rule.IntensityId,
                    DisplayName = rule.DisplayName,
                    ReserveGain = rule.ReserveGain,
                    RequiresTrainingGround = rule.RequiresTrainingGround,
                    IsCurrent = string.Equals(currentIntensityId, rule.IntensityId, System.StringComparison.Ordinal),
                    CanSelect = string.IsNullOrWhiteSpace(disabledReason),
                    DisabledReason = disabledReason,
                    Cost = BuildCosts(rule.Cost)
                };
            })
            .ToList();
        return viewModel;
    }

    private static List<StrategicConstructionRegionViewModel> BuildConstructionRegions(
        StrategicCityState city,
        StrategicLocationDefinition location)
    {
        if (city == null || location == null)
        {
            return new List<StrategicConstructionRegionViewModel>();
        }

        return location.ConstructionRegions
            .Where(region => city.ConstructionRegionIds.Contains(region.RegionId))
            .OrderBy(region => region.RegionId)
            .Select(region => new StrategicConstructionRegionViewModel
            {
                RegionId = region.RegionId,
                DisplayName = region.DisplayName,
                OriginX = region.OriginX,
                OriginY = region.OriginY,
                Width = region.Width,
                Height = region.Height
            })
            .ToList();
    }

    private List<StrategicBuildingInstanceViewModel> BuildBuildings(
        StrategicCityState city,
        StrategicLocationDefinition location)
    {
        if (city == null)
        {
            return new List<StrategicBuildingInstanceViewModel>();
        }

        return city.Buildings
            .OrderBy(item => item.BuildingInstanceId)
            .Select(instance =>
            {
                _definitions.Buildings.TryGetValue(instance.BuildingDefinitionId, out StrategicBuildingDefinition building);
                StrategicConstructionRegionDefinition region = location?.ConstructionRegions.FirstOrDefault(item =>
                    string.Equals(item.RegionId, instance.ConstructionRegionId, System.StringComparison.Ordinal));
                return new StrategicBuildingInstanceViewModel
                {
                    BuildingInstanceId = instance.BuildingInstanceId,
                    BuildingDefinitionId = instance.BuildingDefinitionId,
                    ConstructionRegionId = instance.ConstructionRegionId,
                    RegionDisplayName = region?.DisplayName ?? instance.ConstructionRegionId,
                    DisplayName = building?.DisplayName ?? instance.BuildingDefinitionId,
                    CategoryId = building?.CategoryId ?? "",
                    GridX = instance.GridX,
                    GridY = instance.GridY,
                    FootprintWidth = System.Math.Max(1, building?.FootprintWidth ?? 1),
                    FootprintHeight = System.Math.Max(1, building?.FootprintHeight ?? 1),
                    Level = instance.Level,
                    IsConstructed = instance.IsConstructed,
                    BattleAnchorId = instance.BattleAnchorId
                };
            })
            .ToList();
    }

    private List<StrategicBuildingOptionViewModel> BuildBuildingOptions(
        StrategicManagementState state,
        StrategicCityState city,
        StrategicLocationDefinition location)
    {
        return _definitions.Buildings.Values
            .OrderBy(item => item.BuildingDefinitionId)
            .Select(building =>
            {
                (string regionId, int gridX, int gridY, string failureReason) =
                    ResolveDefaultBuildingPlacement(state, city, location, building);
                return new StrategicBuildingOptionViewModel
                {
                    BuildingDefinitionId = building.BuildingDefinitionId,
                    DisplayName = building.DisplayName,
                    IconPath = building.IconPath,
                    CategoryId = building.CategoryId,
                    FootprintWidth = System.Math.Max(1, building.FootprintWidth),
                    FootprintHeight = System.Math.Max(1, building.FootprintHeight),
                    CanBuild = string.IsNullOrWhiteSpace(failureReason),
                    DisabledReason = failureReason,
                    DefaultRegionId = regionId,
                    DefaultGridX = gridX,
                    DefaultGridY = gridY,
                    BuildCost = BuildCosts(building.BuildCost)
                };
            })
            .ToList();
    }

    private (string RegionId, int GridX, int GridY, string FailureReason) ResolveDefaultBuildingPlacement(
        StrategicManagementState state,
        StrategicCityState city,
        StrategicLocationDefinition location,
        StrategicBuildingDefinition building)
    {
        if (city == null || location == null || building == null)
        {
            return ("", 0, 0, StrategicFailureReasons.MissingCity);
        }

        string firstFailure = "";
        string fallbackRegionId = "";
        int fallbackX = 0;
        int fallbackY = 0;
        foreach (StrategicConstructionRegionDefinition region in location.ConstructionRegions
            .Where(region => city.ConstructionRegionIds.Contains(region.RegionId))
            .OrderBy(region => region.RegionId))
        {
            if (string.IsNullOrWhiteSpace(fallbackRegionId))
            {
                fallbackRegionId = region.RegionId;
                fallbackX = region.OriginX;
                fallbackY = region.OriginY;
            }

            for (int y = region.OriginY; y < region.OriginY + region.Height; y++)
            {
                for (int x = region.OriginX; x < region.OriginX + region.Width; x++)
                {
                    string failure = _rules.GetBuildingPlacementFailureReason(
                        state,
                        city.LocationId,
                        building.BuildingDefinitionId,
                        region.RegionId,
                        x,
                        y);
                    if (string.IsNullOrWhiteSpace(failure))
                    {
                        return (region.RegionId, x, y, "");
                    }

                    if (string.IsNullOrWhiteSpace(firstFailure))
                    {
                        firstFailure = failure;
                    }
                }
            }
        }

        return (
            fallbackRegionId,
            fallbackX,
            fallbackY,
            string.IsNullOrWhiteSpace(firstFailure)
                ? StrategicFailureReasons.MissingConstructionRegion
                : firstFailure);
    }

    private List<StrategicMusterTemplateViewModel> BuildMusterTemplates(
        StrategicManagementState state,
        string cityId)
    {
        return _rules.GetMusterTemplates(state, cityId)
            .OrderBy(item => item.CorpsDefinitionId)
            .Select(availability =>
            {
                _definitions.Corps.TryGetValue(availability.CorpsDefinitionId, out StrategicCorpsDefinition corps);
                return new StrategicMusterTemplateViewModel
                {
                    CorpsDefinitionId = availability.CorpsDefinitionId,
                    DisplayName = corps?.DisplayName ?? availability.CorpsDefinitionId,
                    BattleUnitId = corps?.BattleUnitId ?? "",
                    ReserveForceCost = System.Math.Max(0, corps?.SoldierCapacityCost ?? 0),
                    CanCreate = availability.IsAvailable,
                    DisabledReasons = new List<string>(availability.FailureReasons),
                    CreationCost = BuildCosts(corps == null
                        ? System.Array.Empty<StrategicResourceAmount>()
                        : corps.CreationCost)
                };
            })
            .ToList();
    }

    private List<StrategicCorpsInstanceViewModel> BuildCorpsInstances(
        StrategicManagementState state,
        string factionId,
        string cityId)
    {
        if (state == null)
        {
            return new List<StrategicCorpsInstanceViewModel>();
        }

        return state.CorpsInstances.Values
            .Where(corps =>
                string.Equals(corps.FactionId, factionId, System.StringComparison.Ordinal) &&
                string.Equals(corps.HomeCityId, cityId, System.StringComparison.Ordinal) &&
                string.IsNullOrWhiteSpace(corps.CurrentExpeditionId) &&
                corps.Status != StrategicCorpsInstanceStatus.Expedition)
            .OrderBy(corps => corps.CorpsInstanceId)
            .Select(corps =>
            {
                _definitions.Corps.TryGetValue(corps.CorpsDefinitionId, out StrategicCorpsDefinition definition);
                return new StrategicCorpsInstanceViewModel
                {
                    CorpsInstanceId = corps.CorpsInstanceId,
                    CorpsDefinitionId = corps.CorpsDefinitionId,
                    DisplayName = definition?.DisplayName ?? corps.CorpsDefinitionId,
                    BattleUnitId = definition?.BattleUnitId ?? "",
                    HomeCityId = corps.HomeCityId,
                    Strength = corps.Strength,
                    Level = corps.Level,
                    EquipmentLevel = corps.EquipmentLevel,
                    Experience = corps.Experience,
                    Status = corps.Status,
                    AssignedHeroId = corps.AssignedHeroId,
                    CanReplenish = string.IsNullOrWhiteSpace(_rules.GetCorpsReplenishmentFailureReason(state, cityId, corps.CorpsInstanceId, 100)),
                    ReplenishDisabledReason = _rules.GetCorpsReplenishmentFailureReason(state, cityId, corps.CorpsInstanceId, 100),
                    ReplenishReserveCost = _rules.GetCorpsReplenishmentReserveCost(state, corps.CorpsInstanceId, 100),
                    ReplenishCost = BuildCosts(_rules.GetCorpsReplenishmentResourceCost(state, corps.CorpsInstanceId, 100))
                };
            })
            .ToList();
    }

    private List<StrategicHeroAssignmentViewModel> BuildHeroes(
        StrategicManagementState state,
        string factionId)
    {
        if (state == null)
        {
            return new List<StrategicHeroAssignmentViewModel>();
        }

        return state.Heroes.Values
            .Where(hero => string.Equals(hero.FactionId, factionId, System.StringComparison.Ordinal))
            .OrderBy(hero => hero.HeroId)
            .Select(hero => BuildHero(hero, state))
            .ToList();
    }

    private List<StrategicHeroCompanyViewModel> BuildHeroCompanies(
        StrategicManagementState state,
        string factionId,
        string cityId)
    {
        if (state == null)
        {
            return new List<StrategicHeroCompanyViewModel>();
        }

        return state.Heroes.Values
            .Where(hero =>
                string.Equals(hero.FactionId, factionId, System.StringComparison.Ordinal) &&
                string.IsNullOrWhiteSpace(hero.CurrentExpeditionId) &&
                !string.IsNullOrWhiteSpace(hero.AssignedCorpsInstanceId) &&
                state.CorpsInstances.TryGetValue(hero.AssignedCorpsInstanceId, out StrategicCorpsInstanceState corps) &&
                string.Equals(corps.HomeCityId, cityId, System.StringComparison.Ordinal) &&
                string.IsNullOrWhiteSpace(corps.CurrentExpeditionId) &&
                corps.Status == StrategicCorpsInstanceStatus.AssignedToHero)
            .OrderBy(hero => hero.HeroId)
            .Select(hero => BuildHeroCompany(state, cityId, hero))
            .ToList();
    }

    private StrategicHeroCompanyViewModel BuildHeroCompany(
        StrategicManagementState state,
        string cityId,
        StrategicHeroState hero)
    {
        state.CorpsInstances.TryGetValue(hero.AssignedCorpsInstanceId, out StrategicCorpsInstanceState corps);
        _definitions.Heroes.TryGetValue(hero.HeroDefinitionId, out StrategicHeroDefinition heroDefinition);
        _definitions.Corps.TryGetValue(corps?.CorpsDefinitionId ?? "", out StrategicCorpsDefinition corpsDefinition);
        string disabledReason = _rules.GetExpeditionCreationFailureReason(
            state,
            cityId,
            "",
            StrategicExpeditionIntent.MoveToPosition,
            hero.HeroId);

        return new StrategicHeroCompanyViewModel
        {
            HeroId = hero.HeroId,
            HeroDefinitionId = hero.HeroDefinitionId,
            HeroDisplayName = heroDefinition?.DisplayName ?? hero.HeroDefinitionId,
            HeroBattleUnitId = heroDefinition?.BattleUnitId ?? "",
            CorpsInstanceId = corps?.CorpsInstanceId ?? "",
            CorpsDefinitionId = corps?.CorpsDefinitionId ?? "",
            CorpsDisplayName = corpsDefinition?.DisplayName ?? corps?.CorpsDefinitionId ?? "",
            CorpsBattleUnitId = corpsDefinition?.BattleUnitId ?? "",
            SourceCityId = cityId ?? "",
            Strength = corps?.Strength ?? 0,
            Level = corps?.Level ?? 0,
            EquipmentLevel = corps?.EquipmentLevel ?? 0,
            AptitudeGrade = corps == null
                ? StrategicHeroCorpsAptitudeGrade.D
                : _rules.EvaluateHeroCorpsAptitude(state, hero.HeroId, corps.CorpsDefinitionId),
            CanCreateExpedition = string.IsNullOrWhiteSpace(disabledReason),
            DisabledReason = disabledReason
        };
    }

    private StrategicHeroAssignmentViewModel BuildHero(
        StrategicHeroState hero,
        StrategicManagementState state)
    {
        _definitions.Heroes.TryGetValue(hero.HeroDefinitionId, out StrategicHeroDefinition heroDefinition);
        StrategicHeroAssignmentViewModel viewModel = new()
        {
            HeroId = hero.HeroId,
            HeroDefinitionId = hero.HeroDefinitionId,
            DisplayName = heroDefinition?.DisplayName ?? hero.HeroDefinitionId,
            BattleUnitId = heroDefinition?.BattleUnitId ?? "",
            AssignedCorpsInstanceId = hero.AssignedCorpsInstanceId
        };

        if (string.IsNullOrWhiteSpace(hero.AssignedCorpsInstanceId) ||
            !state.CorpsInstances.TryGetValue(hero.AssignedCorpsInstanceId, out StrategicCorpsInstanceState corps))
        {
            return viewModel;
        }

        _definitions.Corps.TryGetValue(corps.CorpsDefinitionId, out StrategicCorpsDefinition corpsDefinition);
        viewModel.HasAssignedCorps = true;
        viewModel.AssignedCorpsDefinitionId = corps.CorpsDefinitionId;
        viewModel.AssignedCorpsDisplayName = corpsDefinition?.DisplayName ?? corps.CorpsDefinitionId;
        viewModel.AptitudeGrade = _rules.EvaluateHeroCorpsAptitude(state, hero.HeroId, corps.CorpsDefinitionId);
        return viewModel;
    }

    private List<StrategicResourceCostViewModel> BuildCosts(IReadOnlyCollection<StrategicResourceAmount> costs)
    {
        return (costs ?? System.Array.Empty<StrategicResourceAmount>())
            .Where(cost => cost.Amount > 0)
            .OrderBy(cost => cost.ResourceId)
            .Select(cost =>
            {
                _definitions.Resources.TryGetValue(cost.ResourceId, out StrategicResourceDefinition resource);
                return new StrategicResourceCostViewModel
                {
                    ResourceId = cost.ResourceId,
                    DisplayName = resource?.DisplayName ?? cost.ResourceId,
                    Amount = cost.Amount
                };
            })
            .ToList();
    }

    private List<StrategicResourceProductionViewModel> BuildProduction(IReadOnlyCollection<StrategicResourceAmount> production)
    {
        return (production ?? System.Array.Empty<StrategicResourceAmount>())
            .Where(item => item.Amount > 0 && !string.IsNullOrWhiteSpace(item.ResourceId))
            .OrderBy(item => item.ResourceId)
            .Select(item =>
            {
                _definitions.Resources.TryGetValue(item.ResourceId, out StrategicResourceDefinition resource);
                return new StrategicResourceProductionViewModel
                {
                    ResourceId = item.ResourceId,
                    DisplayName = resource?.DisplayName ?? item.ResourceId,
                    Amount = item.Amount
                };
            })
            .ToList();
    }

    private static string BuildProductionDisplayText(IReadOnlyList<StrategicResourceProductionViewModel> production)
    {
        return production == null || production.Count == 0
            ? "无"
            : string.Join(" / ", production.Select(item => $"{item.DisplayName} +{item.Amount} / 大地图时间"));
    }

    private static string FormatLocationKind(StrategicLocationKind kind)
    {
        return kind switch
        {
            StrategicLocationKind.City => "城市",
            StrategicLocationKind.ResourceSite => "资源点",
            StrategicLocationKind.Ruin => "遗迹",
            StrategicLocationKind.Dungeon => "地牢",
            StrategicLocationKind.Gate => "关隘",
            _ => "未知地点"
        };
    }

    private static string FormatLocationControlState(StrategicLocationControlState controlState)
    {
        return controlState switch
        {
            StrategicLocationControlState.PlayerHeld => "玩家控制",
            StrategicLocationControlState.EnemyHeld => "敌方控制",
            StrategicLocationControlState.Neutral => "中立",
            _ => "未知"
        };
    }

    private static string FormatSourcePermissionTag(string sourcePermissionTag)
    {
        return sourcePermissionTag ?? "";
    }
}
