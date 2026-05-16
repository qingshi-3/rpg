using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle;
using Rpg.Definitions.World;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;

namespace Rpg.Application.World;

public static class StrategicWorldV1DefinitionFactory
{
    private const string InitialStateResourcePath = "res://assets/definitions/world/strategic_world_v1_initial_state.tres";

    // Headless regressions can bypass Godot resource loading; runtime default still loads it.
    public static StrategicWorldDefinition Create(bool loadInitialStateResource = true)
    {
        return new StrategicWorldDefinition
        {
            Id = StrategicWorldIds.DefinitionChapter01,
            DisplayName = "第一章：埋骨地攻防",
            StartingSiteId = StrategicWorldIds.SitePlayerCamp,
            PlayerFactionId = StrategicWorldIds.FactionPlayer,
            EnemyFactionIds = new List<string> { StrategicWorldIds.FactionUndead },
            FactionDefinitions = CreateFactions(),
            ResourceDefinitions = CreateResources(),
            FacilityDefinitions = CreateFacilities(),
            SiteDefinitions = CreateSites(loadInitialStateResource),
            OpportunityDefinitions = CreateOpportunities(),
            OpportunitySpawnPoints = CreateOpportunitySpawnPoints(),
            OpportunitySpawnRules = CreateOpportunitySpawnRules(),
            ActionDefinitions = CreateActions(),
            ThreatRules = CreateThreatRules(),
            InitialResources = new List<ResourceAmountDefinition>
            {
                new(StrategicWorldIds.ResourcePopulation, 8),
                new(StrategicWorldIds.ResourceEconomy, 10),
                new(StrategicWorldIds.ResourceStone, 4)
            }
        };
    }

    private static List<ResourceDefinition> CreateResources()
    {
        return new List<ResourceDefinition>
        {
            new()
            {
                Id = StrategicWorldIds.ResourcePopulation,
                DisplayName = "人口",
                Description = "可被建筑占用，也可训练为驻军。",
                Category = ResourceCategory.Workforce,
                IsReservable = true
            },
            new()
            {
                Id = StrategicWorldIds.ResourceEconomy,
                DisplayName = "经济",
                Description = "建造、训练和战略行动的通用消耗。",
                Category = ResourceCategory.Currency
            },
            new()
            {
                Id = StrategicWorldIds.ResourceStone,
                DisplayName = "石材",
                Description = "矿场产出，防御塔和修复设施消耗。",
                Category = ResourceCategory.Material
            }
        };
    }

    private static List<FactionDefinition> CreateFactions()
    {
        return new List<FactionDefinition>
        {
            new()
            {
                Id = StrategicWorldIds.FactionPlayer,
                DisplayName = "玩家营地",
                Description = "能通过出征小队介入世界层战斗，依靠后勤和场域设施稳住防线。",
                Capabilities = new List<FactionCapabilityDefinition>
                {
                    new()
                    {
                        Id = StrategicWorldIds.FactionCapabilityFieldIntervention,
                        DisplayName = "战场介入",
                        Description = "世界层战斗进入任意阶段后，玩家可手动进入战斗并覆盖自动结算结果。",
                        Values = new Dictionary<string, int>
                        {
                            ["world_defense_bonus"] = 1
                        }
                    },
                    new()
                    {
                        Id = StrategicWorldIds.FactionCapabilityCampLogistics,
                        DisplayName = "营地后勤",
                        Description = "己方场域在世界层防守结算中获得少量稳定加成。",
                        Values = new Dictionary<string, int>
                        {
                            ["world_defense_bonus"] = 2
                        }
                    }
                }
            },
            new()
            {
                Id = StrategicWorldIds.FactionUndead,
                DisplayName = "亡灵",
                Description = "依靠持续压迫和坟场增援强化世界层 Raid。",
                Capabilities = new List<FactionCapabilityDefinition>
                {
                    new()
                    {
                        Id = StrategicWorldIds.FactionCapabilityRelentlessRaid,
                        DisplayName = "无休突袭",
                        Description = "亡灵 Raid 会拖入更长的世界层交战过程，给玩家留下介入窗口。",
                        Values = new Dictionary<string, int>
                        {
                            ["world_attack_bonus"] = 2,
                            ["world_battle_duration_bonus"] = 1
                        }
                    },
                    new()
                    {
                        Id = StrategicWorldIds.FactionCapabilityGraveReinforcement,
                        DisplayName = "坟场增援",
                        Description = "世界层投影结算时提高攻击方兵力评分和阶段性消耗。",
                        Values = new Dictionary<string, int>
                        {
                            ["world_attack_bonus"] = 2,
                            ["world_attrition_bonus"] = 1
                        }
                    }
                }
            }
        };
    }

    private static List<FacilityDefinition> CreateFacilities()
    {
        return new List<FacilityDefinition>
        {
            new()
            {
                Id = StrategicWorldIds.FacilityBarracks,
                DisplayName = "兵营",
                Description = "训练和驻军能力。",
                FacilityType = FacilityType.Military,
                Actions = new List<string> { StrategicWorldIds.ActionTrainMilitia }
            },
            new()
            {
                Id = StrategicWorldIds.FacilityMine,
                DisplayName = "矿场",
                Description = "占用人口，每个世界步产出石材。",
                FacilityType = FacilityType.Production,
                BuildCosts = new List<ResourceAmountDefinition> { new(StrategicWorldIds.ResourceEconomy, 2) },
                RequiredSlotTags = new List<string> { "mine" },
                PassiveEffects = new List<string> { "produce_stone_2" }
            },
            new()
            {
                Id = StrategicWorldIds.FacilityDefenseTower,
                DisplayName = "防御塔",
                Description = "提高防守值，防守战提供塔支援。",
                FacilityType = FacilityType.Defense,
                BuildCosts = new List<ResourceAmountDefinition>
                {
                    new(StrategicWorldIds.ResourceStone, 4),
                    new(StrategicWorldIds.ResourceEconomy, 2)
                },
                RequiredSlotTags = new List<string> { "tower" },
                BattleModifiers = new List<BattleModifierDefinition>
                {
                    new()
                    {
                        Id = "tower_support",
                        Type = "tower_support",
                        BattleAnchorId = "bonefield_north_tower",
                        Uses = 1,
                        Values = new Dictionary<string, int> { ["defense_score"] = 3 }
                    }
                }
            }
        };
    }

    private static List<WorldSiteDefinition> CreateSites(bool loadInitialStateResource)
    {
        List<WorldSiteDefinition> sites = new()
        {
            new()
            {
                Id = StrategicWorldIds.SitePlayerCamp,
                DisplayName = "玩家营地",
                SiteKind = WorldSiteKind.Base,
                Description = "玩家初始据点，承担基础资源、建造、训练和出征入口。",
                MapPosition = new Vector2(236, 413),
                InitialOwnerFactionId = StrategicWorldIds.FactionPlayer,
                InitialControlState = SiteControlState.PlayerHeld,
                Intel = new WorldSiteIntelDefinition
                {
                    Policy = WorldSiteIntelPolicy.Transparent
                },
                FacilitySlots = new List<FacilitySlotDefinition>
                {
                    new()
                    {
                        SlotId = "camp_barracks_01",
                        DisplayName = "营地兵营",
                        Tags = new List<string> { "barracks", "military" },
                        AllowedFacilityIds = new List<string> { StrategicWorldIds.FacilityBarracks },
                        InitialFacilityId = StrategicWorldIds.FacilityBarracks
                    }
                },
                DefaultGarrisonZoneId = WorldSiteDeploymentService.DefaultGarrisonZoneId,
                DeploymentZones = new List<SiteDeploymentZoneDefinition>
                {
                    new()
                    {
                        ZoneId = WorldSiteDeploymentService.DefaultGarrisonZoneId,
                        DisplayName = "营地驻军区",
                        ZoneKind = SiteDeploymentZoneKind.DefaultGarrison,
                        Capacity = 6,
                        Cells = new List<Vector2I>
                        {
                            new(17, 16),
                            new(18, 16),
                            new(19, 16),
                            new(17, 17),
                            new(18, 17),
                            new(19, 17)
                        }
                    }
                },
                EntranceDefinitions = new List<BattleEntranceDefinition>
                {
                    new() { EntranceId = "camp_gate", DisplayName = "营地出口", FactionId = StrategicWorldIds.FactionPlayer, BattleAnchorId = "camp_gate" }
                }
            },
            new()
            {
                Id = StrategicWorldIds.SiteBonefield,
                DisplayName = "埋骨地",
                SiteKind = WorldSiteKind.ResourceSite,
                Description = "第一座可争夺资源场域，能启用矿场、建造防御塔并成为墓园 Raid 目标。",
                MapPosition = new Vector2(796, 413),
                InitialOwnerFactionId = StrategicWorldIds.FactionUndead,
                InitialControlState = SiteControlState.Hostile,
                Intel = new WorldSiteIntelDefinition
                {
                    Policy = WorldSiteIntelPolicy.Partial,
                    StrategicSummary = "亡灵控制的资源场域，确认有可利用矿道和外层守军。",
                    TacticalSummary = "正门可进攻，外层有骸骨巡逻。",
                    HiddenTacticalSummary = "内侧营地布阵、侧路和伏兵尚未确认。",
                    PublicEntranceIds = new List<string> { "main_entrance" },
                    ObscurationSources = new List<WorldSiteObscurationDefinition>
                    {
                        new()
                        {
                            Id = "bonefield_outer_watch",
                            DisplayName = "外层哨岗",
                            Description = "哨岗仍在巡逻，内侧布阵只显示为粗略情报。",
                            HidesTacticalLayout = true,
                            DisabledByResolvedPointIds = new List<string> { "bonefield_watch_post" }
                        }
                    }
                },
                FacilitySlots = new List<FacilitySlotDefinition>
                {
                    new()
                    {
                        SlotId = "mine_slot_01",
                        DisplayName = "埋骨地采石点",
                        Tags = new List<string> { "mine", "production" },
                        AllowedFacilityIds = new List<string> { StrategicWorldIds.FacilityMine },
                        BattleAnchorId = "bonefield_core"
                    },
                    new()
                    {
                        SlotId = "tower_slot_01",
                        DisplayName = "北侧塔基",
                        Tags = new List<string> { "tower", "defense" },
                        AllowedFacilityIds = new List<string> { StrategicWorldIds.FacilityDefenseTower },
                        BattleAnchorId = "bonefield_north_tower"
                    }
                },
                DefaultGarrisonZoneId = WorldSiteDeploymentService.DefaultGarrisonZoneId,
                DeploymentZones = new List<SiteDeploymentZoneDefinition>
                {
                    new()
                    {
                        ZoneId = WorldSiteDeploymentService.DefaultGarrisonZoneId,
                        DisplayName = "埋骨地驻军区",
                        ZoneKind = SiteDeploymentZoneKind.DefaultGarrison,
                        Capacity = 4,
                        Cells = new List<Vector2I>
                        {
                            new(18, 17),
                            new(20, 21),
                            new(18, 15),
                            new(19, 15)
                        }
                    }
                },
                EntranceDefinitions = new List<BattleEntranceDefinition>
                {
                    new() { EntranceId = "undead_raid_east", DisplayName = "东侧亡灵进攻点", FactionId = StrategicWorldIds.FactionUndead, Direction = WorldSiteAttackDirection.East, BattleAnchorId = "bonefield_east_raid", Source = "Attacker" },
                    new() { EntranceId = "main_entrance", DisplayName = "埋骨地入口", FactionId = StrategicWorldIds.FactionPlayer, Direction = WorldSiteAttackDirection.West, BattleAnchorId = "bonefield_main_entrance" },
                    new() { EntranceId = "main_entrance_east", DisplayName = "埋骨地东侧入口", FactionId = StrategicWorldIds.FactionPlayer, Direction = WorldSiteAttackDirection.East, BattleAnchorId = "bonefield_east_entrance" },
                    new() { EntranceId = "defense_post", DisplayName = "防守据点", FactionId = StrategicWorldIds.FactionPlayer, Direction = WorldSiteAttackDirection.Any, BattleAnchorId = "bonefield_defense_post", Source = "Garrison" }
                },
                ExplorationPoints = new List<SiteExplorationPointDefinition>
                {
                    new()
                    {
                        Id = "bonefield_broken_cart",
                        DisplayName = "破损矿车",
                        Description = "散落的矿车还留有可用石料。",
                        CellX = 17,
                        CellY = 17,
                        CellHeight = 0,
                        InteractionRange = 1,
                        InitiallyRevealed = true,
                        Actions = new List<SiteExplorationActionDefinition>
                        {
                            new()
                            {
                                Id = "inspect_broken_cart",
                                DisplayName = "调查矿车",
                                Description = "获得少量石材，并确认矿道方向。",
                                ResolvesPoint = true,
                                RevealsPointIds = new[] { "bonefield_collapsed_mine" },
                                AddsKnownTacticalTags = new[] { "mine_trace_confirmed" }
                            }
                        }
                    },
                    new()
                    {
                        Id = "bonefield_watch_post",
                        DisplayName = "外层哨岗",
                        Description = "骸骨巡逻队从这里观察入口。",
                        CellX = 18,
                        CellY = 16,
                        CellHeight = 0,
                        InteractionRange = 1,
                        InitiallyRevealed = true,
                        Actions = new List<SiteExplorationActionDefinition>
                        {
                            new()
                            {
                                Id = "observe_watch_post",
                                DisplayName = "观察哨岗",
                                Description = "揭示内侧入口，降低盲目强攻风险。",
                                ResolvesPoint = true,
                                RevealsEntranceIds = new[] { "main_entrance_east" },
                                AddsKnownTacticalTags = new[] { "watch_post_scouted" },
                                AddsExplorationAdvantageTags = new[] { "outer_watch_scouted" }
                            },
                            new()
                            {
                                Id = "assault_watch_post",
                                DisplayName = "强攻哨岗",
                                Description = "立即触发局部遭遇战。",
                                StartsBattle = true,
                                BattleEncounterId = "bonefield_watch_post",
                                AlertDelta = 2
                            }
                        }
                    },
                    new()
                    {
                        Id = "bonefield_collapsed_mine",
                        DisplayName = "塌方矿道",
                        Description = "清理后可转化为矿场槽位。",
                        CellX = 19,
                        CellY = 17,
                        CellHeight = 0,
                        InteractionRange = 1,
                        InitiallyRevealed = false,
                        Actions = new List<SiteExplorationActionDefinition>
                        {
                            new()
                            {
                                Id = "clear_collapsed_mine",
                                DisplayName = "清理矿道",
                                Description = "消耗一次世界步，解锁埋骨地采石点。",
                                ConsumesWorldTick = true,
                                ResolvesPoint = true,
                                UnlocksFacilitySlotIds = new[] { "mine_slot_01" },
                                ClearsHazardIds = new[] { "collapsed_mine" }
                            }
                        }
                    }
                },
                ExplorationPatrols = new List<SiteExplorationPatrolDefinition>
                {
                    new()
                    {
                        Id = "bonefield_patrol_01",
                        DisplayName = "骸骨巡逻队",
                        UnitTypeId = StrategicWorldIds.UnitSkeletonWarrior,
                        SourcePlacementId = "garrison:skeleton_warrior:1",
                        AlertRadiusCells = 2,
                        ActionPointRegenPerTick = 1,
                        MoveCostPerCell = 2,
                        InitiallyActive = true,
                        RouteCells = new List<SiteExplorationRouteCellDefinition>
                        {
                            new() { CellX = 18, CellY = 17, CellHeight = 0 },
                            new() { CellX = 19, CellY = 17, CellHeight = 0 }
                        }
                    },
                    new()
                    {
                        Id = "bonefield_patrol_02",
                        DisplayName = "骸骨射手巡逻队",
                        UnitTypeId = StrategicWorldIds.UnitSkeletonArcher,
                        SourcePlacementId = "garrison:skeleton_archer:1",
                        AlertRadiusCells = 2,
                        ActionPointRegenPerTick = 1,
                        MoveCostPerCell = 2,
                        InitiallyActive = true,
                        RouteCells = new List<SiteExplorationRouteCellDefinition>
                        {
                            new() { CellX = 18, CellY = 16, CellHeight = 0 },
                            new() { CellX = 19, CellY = 16, CellHeight = 0 }
                        }
                    }
                },
                Tags = new List<string> { "resource_site" }
            },
            new()
            {
                Id = StrategicWorldIds.SiteGraveyard,
                DisplayName = "墓园",
                SiteKind = WorldSiteKind.EnemySource,
                Description = "第一版敌方源头，会定期向埋骨地生成亡灵 Raid。",
                MapPosition = new Vector2(1076, 413),
                InitialOwnerFactionId = StrategicWorldIds.FactionUndead,
                InitialControlState = SiteControlState.Hostile,
                Intel = new WorldSiteIntelDefinition
                {
                    Policy = WorldSiteIntelPolicy.Transparent
                },
                InitialGarrison = new List<GarrisonDefinition>
                {
                    new() { UnitTypeId = StrategicWorldIds.UnitSkeletonWarrior, Count = 1, Morale = 35 },
                    new() { UnitTypeId = StrategicWorldIds.UnitSkeletonArcher, Count = 1, Morale = 35 },
                    new() { UnitTypeId = StrategicWorldIds.UnitGraveShadow, Count = 1, Morale = 40 },
                    new() { UnitTypeId = StrategicWorldIds.UnitGraveMarksman, Count = 1, Morale = 40 },
                    new() { UnitTypeId = StrategicWorldIds.UnitDeathBlighter, Count = 1, Morale = 45 }
                },
                AutoGarrisonProductions = new List<SiteAutoGarrisonProductionDefinition>
                {
                    new()
                    {
                        FactionId = StrategicWorldIds.FactionUndead,
                        IntervalTicks = 3,
                        MaxStoredUnits = 10,
                        BatchUnits = new List<GarrisonDefinition>
                        {
                            new() { UnitTypeId = StrategicWorldIds.UnitSkeletonWarrior, Count = 1, Morale = 35 },
                            new() { UnitTypeId = StrategicWorldIds.UnitSkeletonArcher, Count = 1, Morale = 35 },
                            new() { UnitTypeId = StrategicWorldIds.UnitGraveShadow, Count = 1, Morale = 40 },
                            new() { UnitTypeId = StrategicWorldIds.UnitGraveMarksman, Count = 1, Morale = 40 },
                            new() { UnitTypeId = StrategicWorldIds.UnitDeathBlighter, Count = 1, Morale = 45 }
                        }
                    }
                },
                DefaultGarrisonZoneId = WorldSiteDeploymentService.DefaultGarrisonZoneId,
                DeploymentZones = new List<SiteDeploymentZoneDefinition>
                {
                    new()
                    {
                        ZoneId = WorldSiteDeploymentService.DefaultGarrisonZoneId,
                        DisplayName = "墓园守军区",
                        ZoneKind = SiteDeploymentZoneKind.DefaultGarrison,
                        Capacity = 0
                    }
                },
                Tags = new List<string> { "undead_source" }
            }
        };

        if (loadInitialStateResource)
        {
            ApplyInitialStateResource(sites);
        }

        return sites;
    }

    private static void ApplyInitialStateResource(List<WorldSiteDefinition> sites)
    {
        StrategicWorldInitialStateResource initialState = GD.Load<StrategicWorldInitialStateResource>(InitialStateResourcePath);
        if (initialState == null)
        {
            GameLog.Error(nameof(StrategicWorldV1DefinitionFactory), $"Missing strategic world initial state resource path={InitialStateResourcePath}");
            throw new InvalidOperationException($"Missing strategic world initial state resource: {InitialStateResourcePath}");
        }

        Dictionary<string, WorldSiteDefinition> siteById = sites.ToDictionary(site => site.Id);
        int appliedEntryCount = 0;

        foreach (WorldSiteInitialStateResource siteState in initialState.Sites)
        {
            if (siteState == null)
            {
                continue;
            }

            string siteId = siteState.SiteId?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(siteId))
            {
                GameLog.Warn(nameof(StrategicWorldV1DefinitionFactory), "Initial site state skipped because SiteId is empty.");
                continue;
            }

            if (!siteById.TryGetValue(siteId, out WorldSiteDefinition site))
            {
                GameLog.Warn(nameof(StrategicWorldV1DefinitionFactory), $"Initial site state references unknown site id={siteId}");
                continue;
            }

            site.InitialGarrison.Clear();

            foreach (WorldInitialGarrisonEntryResource entry in siteState.InitialGarrison)
            {
                if (entry == null)
                {
                    continue;
                }

                GarrisonDefinition garrison = entry.ToDefinition();
                if (string.IsNullOrWhiteSpace(garrison.UnitTypeId))
                {
                    GameLog.Warn(nameof(StrategicWorldV1DefinitionFactory), $"Initial garrison skipped site={siteId} reason=empty_unit_definition");
                    continue;
                }

                if (garrison.Count <= 0)
                {
                    GameLog.Warn(nameof(StrategicWorldV1DefinitionFactory), $"Initial garrison skipped site={siteId} unit={garrison.UnitTypeId} reason=non_positive_count count={garrison.Count}");
                    continue;
                }

                site.InitialGarrison.Add(garrison);
                appliedEntryCount++;
            }
        }

        GameLog.Info(nameof(StrategicWorldV1DefinitionFactory), $"StrategicWorldInitialStateApplied path={InitialStateResourcePath} site_count={initialState.Sites.Count} garrison_entries={appliedEntryCount}");
    }

    private static List<WorldActionDefinition> CreateActions()
    {
        return new List<WorldActionDefinition>
        {
            new()
            {
                Id = StrategicWorldIds.ActionBuildMine,
                DisplayName = "启用矿场",
                Description = "消耗经济并占用人口，让埋骨地每个世界步产出石材。",
                Scope = WorldActionScope.Site,
                AdvancesWorldTick = true,
                Costs = new List<ResourceAmountDefinition> { new(StrategicWorldIds.ResourceEconomy, 2) },
                Conditions = new List<WorldConditionDefinition>
                {
                    new() { Kind = WorldConditionKind.SiteOwnerIs, SiteId = StrategicWorldIds.SiteBonefield, FactionId = StrategicWorldIds.FactionPlayer, FailureReasonKey = "site_not_owned" },
                    new() { Kind = WorldConditionKind.SiteControlStateIs, SiteId = StrategicWorldIds.SiteBonefield, ControlStates = new List<SiteControlState> { SiteControlState.PlayerHeld, SiteControlState.Damaged }, FailureReasonKey = "site_not_owned" },
                    new() { Kind = WorldConditionKind.HasEmptyFacilitySlot, SiteId = StrategicWorldIds.SiteBonefield, TargetId = StrategicWorldIds.FacilityMine, SlotTag = "mine", FailureReasonKey = "no_valid_facility_slot" },
                    new() { Kind = WorldConditionKind.HasAvailablePopulation, Amount = 1, FailureReasonKey = "not_enough_population" }
                },
                Effects = new List<WorldEffectDefinition>
                {
                    new() { Kind = WorldEffectKind.AddFacility, SiteId = StrategicWorldIds.SiteBonefield, FacilityId = StrategicWorldIds.FacilityMine },
                    new() { Kind = WorldEffectKind.ReserveResource, ResourceId = StrategicWorldIds.ResourcePopulation, Amount = 1, TargetId = "last_facility" }
                }
            },
            new()
            {
                Id = StrategicWorldIds.ActionBuildDefenseTower,
                DisplayName = "建造防御塔",
                Description = "提高埋骨地防守值，并在防守战提供一次塔支援。",
                Scope = WorldActionScope.Site,
                AdvancesWorldTick = true,
                Costs = new List<ResourceAmountDefinition>
                {
                    new(StrategicWorldIds.ResourceStone, 4),
                    new(StrategicWorldIds.ResourceEconomy, 2)
                },
                Conditions = new List<WorldConditionDefinition>
                {
                    new() { Kind = WorldConditionKind.SiteOwnerIs, SiteId = StrategicWorldIds.SiteBonefield, FactionId = StrategicWorldIds.FactionPlayer, FailureReasonKey = "site_not_owned" },
                    new() { Kind = WorldConditionKind.HasEmptyFacilitySlot, SiteId = StrategicWorldIds.SiteBonefield, TargetId = StrategicWorldIds.FacilityDefenseTower, SlotTag = "tower", FailureReasonKey = "no_valid_facility_slot" }
                },
                Effects = new List<WorldEffectDefinition>
                {
                    new() { Kind = WorldEffectKind.AddFacility, SiteId = StrategicWorldIds.SiteBonefield, FacilityId = StrategicWorldIds.FacilityDefenseTower }
                }
            },
            new()
            {
                Id = StrategicWorldIds.ActionTrainMilitia,
                DisplayName = "训练民兵",
                Description = "消耗人口和经济，在玩家营地增加一队民兵。",
                Scope = WorldActionScope.Site,
                AdvancesWorldTick = true,
                Costs = new List<ResourceAmountDefinition>
                {
                    new(StrategicWorldIds.ResourcePopulation, 1),
                    new(StrategicWorldIds.ResourceEconomy, 2)
                },
                Conditions = new List<WorldConditionDefinition>
                {
                    new() { Kind = WorldConditionKind.SiteOwnerIs, SiteId = StrategicWorldIds.SitePlayerCamp, FactionId = StrategicWorldIds.FactionPlayer, FailureReasonKey = "site_not_owned" },
                    new() { Kind = WorldConditionKind.HasFacility, SiteId = StrategicWorldIds.SitePlayerCamp, TargetId = StrategicWorldIds.FacilityBarracks, FacilityState = FacilityState.Active, FailureReasonKey = "missing_facility" }
                },
                Effects = new List<WorldEffectDefinition>
                {
                    new() { Kind = WorldEffectKind.AddGarrison, SiteId = StrategicWorldIds.SitePlayerCamp, UnitTypeId = StrategicWorldIds.UnitMilitia, Amount = 1 }
                }
            },
            new()
            {
                Id = StrategicWorldIds.ActionDefendRaid,
                DisplayName = "进入防守战",
                Description = "带领埋骨地驻军进入自动防守战。",
                Scope = WorldActionScope.Threat,
                Conditions = new List<WorldConditionDefinition>
                {
                    new() { Kind = WorldConditionKind.ThreatStageIs, ThreatStage = ThreatStage.Attacking, FailureReasonKey = "threat_not_attackable" }
                },
                Effects = new List<WorldEffectDefinition>
                {
                    new() { Kind = WorldEffectKind.StartBattle, SiteId = StrategicWorldIds.SiteBonefield, BattleKind = nameof(BattleKind.DefenseRaid) }
                }
            },
            new()
            {
                Id = StrategicWorldIds.ActionAutoResolveRaid,
                DisplayName = "自动结算",
                Description = "按埋骨地驻军和防御塔自动结算本次 Raid。",
                Scope = WorldActionScope.Threat,
                Conditions = new List<WorldConditionDefinition>
                {
                    new() { Kind = WorldConditionKind.ThreatStageIs, ThreatStage = ThreatStage.Attacking, FailureReasonKey = "threat_not_attackable" }
                }
            },
            new()
            {
                Id = StrategicWorldIds.ActionWaitTick,
                DisplayName = "等待 / 整顿",
                Description = "推进一个世界步，结算产出和敌方威胁。",
                Scope = WorldActionScope.Run,
                AdvancesWorldTick = true
            }
        };
    }

    private static List<WorldOpportunityDefinition> CreateOpportunities()
    {
        return new List<WorldOpportunityDefinition>
        {
            new()
            {
                Id = StrategicWorldIds.OpportunitySpiritHerbPatch,
                DisplayName = "灵草丛",
                Description = "荒野短暂显露的灵草丛。小队可以采集，获得少量经济。",
                PoolId = StrategicWorldIds.OpportunityPoolWildernessV1,
                Weight = 3,
                DurationTicks = 4,
                CompletionText = "采集灵草，换得经济 +2。",
                CompletionRewards = new List<ResourceAmountDefinition>
                {
                    new(StrategicWorldIds.ResourceEconomy, 2)
                },
                Tags = new List<string> { "wilderness", "gather" }
            },
            new()
            {
                Id = StrategicWorldIds.OpportunityLostCaravan,
                DisplayName = "迷路商队",
                Description = "一支避开亡灵的商队迷失在野外。护送他们离开可以获得报酬。",
                PoolId = StrategicWorldIds.OpportunityPoolWildernessV1,
                Weight = 2,
                DurationTicks = 3,
                CompletionText = "护送商队脱险，获得经济 +3。",
                CompletionRewards = new List<ResourceAmountDefinition>
                {
                    new(StrategicWorldIds.ResourceEconomy, 3)
                },
                Tags = new List<string> { "wilderness", "rescue" }
            },
            new()
            {
                Id = StrategicWorldIds.OpportunityLooseStoneVein,
                DisplayName = "裸露石脉",
                Description = "雨后露出的浅层石脉，只会在短时间内可采。",
                PoolId = StrategicWorldIds.OpportunityPoolWildernessV1,
                Weight = 2,
                DurationTicks = 5,
                CompletionText = "开采裸露石脉，石材 +3。",
                CompletionRewards = new List<ResourceAmountDefinition>
                {
                    new(StrategicWorldIds.ResourceStone, 3)
                },
                Tags = new List<string> { "wilderness", "gather" }
            }
        };
    }

    private static List<OpportunitySpawnPointDefinition> CreateOpportunitySpawnPoints()
    {
        return new List<OpportunitySpawnPointDefinition>
        {
            new()
            {
                Id = "wilderness_west_road",
                DisplayName = "西侧荒路",
                MapPosition = new Vector2(484, 319),
                Radius = 58.0f
            },
            new()
            {
                Id = "wilderness_bonefield_outskirts",
                DisplayName = "埋骨地外缘",
                MapPosition = new Vector2(892, 400),
                Radius = 66.0f
            },
            new()
            {
                Id = "wilderness_south_ridge",
                DisplayName = "南侧荒脊",
                MapPosition = new Vector2(689, 515),
                Radius = 54.0f
            }
        };
    }

    private static List<OpportunitySpawnRuleDefinition> CreateOpportunitySpawnRules()
    {
        return new List<OpportunitySpawnRuleDefinition>
        {
            new()
            {
                Id = StrategicWorldIds.OpportunityRuleWildernessV1,
                PoolId = StrategicWorldIds.OpportunityPoolWildernessV1,
                MinWorldTick = 1,
                CheckIntervalTicks = 2,
                SpawnChancePermille = 650,
                CooldownTicks = 3,
                MaxActiveCount = 2,
                PositionJitterRadius = 36.0f,
                SpawnPointIds = new List<string>
                {
                    "wilderness_west_road",
                    "wilderness_bonefield_outskirts",
                    "wilderness_south_ridge"
                }
            }
        };
    }

    private static List<ThreatRuleDefinition> CreateThreatRules()
    {
        return new List<ThreatRuleDefinition>
        {
            new()
            {
                Id = StrategicWorldIds.ThreatRuleGraveyardRaidBonefield,
                SourceSiteId = StrategicWorldIds.SiteGraveyard,
                TargetSiteId = StrategicWorldIds.SiteBonefield,
                ThreatType = ThreatType.Raid,
                InitialCountdownTicks = 3,
                EnemyGroupId = "undead_raid_01",
                EnemyForces = new List<GarrisonDefinition>
                {
                    new() { UnitTypeId = StrategicWorldIds.UnitSkeletonWarrior, Count = 1, Morale = 30 },
                    new() { UnitTypeId = StrategicWorldIds.UnitSkeletonArcher, Count = 1, Morale = 30 },
                    new() { UnitTypeId = StrategicWorldIds.UnitGraveShadow, Count = 1, Morale = 35 },
                    new() { UnitTypeId = StrategicWorldIds.UnitGraveMarksman, Count = 1, Morale = 35 },
                    new() { UnitTypeId = StrategicWorldIds.UnitDeathBlighter, Count = 1, Morale = 40 }
                },
                TriggerConditions = new List<WorldConditionDefinition>
                {
                    new() { Kind = WorldConditionKind.SiteControlStateIs, SiteId = StrategicWorldIds.SiteBonefield, ControlStates = new List<SiteControlState> { SiteControlState.PlayerHeld, SiteControlState.Damaged } },
                    new() { Kind = WorldConditionKind.SiteControlStateIs, SiteId = StrategicWorldIds.SiteGraveyard, ControlState = SiteControlState.Hostile },
                    new() { Kind = WorldConditionKind.NoActiveThreatOfRule, RuleId = StrategicWorldIds.ThreatRuleGraveyardRaidBonefield }
                }
            }
        };
    }
}

