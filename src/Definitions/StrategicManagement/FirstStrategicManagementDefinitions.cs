using System.Collections.Generic;
using Rpg.Application.Config;
using Rpg.Application.World;

namespace Rpg.Definitions.StrategicManagement;

public static class FirstStrategicManagementDefinitions
{
    public static StrategicManagementDefinitionSet Create()
    {
        StrategicManagementDefinitionSet definitions = new();
        AddResources(definitions);
        AddLocations(definitions);
        AddBattleRewards(definitions);
        AddEquipmentSamples(definitions);
        AddCityIdentities(definitions);
        AddBuildings(definitions);
        AddCorps(definitions);
        AddHeroes(definitions);
        return definitions;
    }

    private static void AddResources(StrategicManagementDefinitionSet definitions)
    {
        Add(definitions.Resources, new StrategicResourceDefinition { ResourceId = StrategicManagementIds.ResourceMoney, DisplayName = "资金" });
        Add(definitions.Resources, new StrategicResourceDefinition { ResourceId = StrategicManagementIds.ResourceFood, DisplayName = "粮食" });
        Add(definitions.Resources, new StrategicResourceDefinition { ResourceId = StrategicManagementIds.ResourceWood, DisplayName = "木材" });
        Add(definitions.Resources, new StrategicResourceDefinition { ResourceId = StrategicManagementIds.ResourceOre, DisplayName = "矿石" });
    }

    private static void AddLocations(StrategicManagementDefinitionSet definitions)
    {
        Add(definitions.Locations, new StrategicLocationDefinition
        {
            LocationId = StrategicManagementIds.LocationPlainsCity,
            MapSiteId = StrategicManagementIds.MapSitePlayerCamp,
            DisplayName = "苍原城",
            Kind = StrategicLocationKind.City,
            CityIdentityId = StrategicManagementIds.CityIdentityPlainsHuman,
            ConstructionRegions =
            {
                new StrategicConstructionRegionDefinition
                {
                    RegionId = StrategicManagementIds.RegionPlainsEconomy,
                    DisplayName = "西侧农商区",
                    OriginX = 0,
                    OriginY = 0,
                    Width = 8,
                    Height = 6,
                    AllowedCategoryIds =
                    {
                        StrategicManagementIds.BuildingCategoryEconomy
                    }
                },
                new StrategicConstructionRegionDefinition
                {
                    RegionId = StrategicManagementIds.RegionPlainsMilitary,
                    DisplayName = "东侧军备区",
                    OriginX = 10,
                    OriginY = 0,
                    Width = 7,
                    Height = 5,
                    AllowedCategoryIds =
                    {
                        StrategicManagementIds.BuildingCategoryMilitary,
                        StrategicManagementIds.BuildingCategoryDefense,
                        StrategicManagementIds.BuildingCategorySupport
                    }
                },
                new StrategicConstructionRegionDefinition
                {
                    RegionId = StrategicManagementIds.RegionPlainsCivic,
                    DisplayName = "内城事务区",
                    OriginX = 0,
                    OriginY = 8,
                    Width = 6,
                    Height = 4,
                    AllowedCategoryIds =
                    {
                        StrategicManagementIds.BuildingCategoryHero,
                        StrategicManagementIds.BuildingCategorySupport,
                        StrategicManagementIds.BuildingCategorySpecial
                    }
                }
            }
        });
        Add(definitions.Locations, new StrategicLocationDefinition
        {
            LocationId = StrategicManagementIds.LocationTimberSite,
            DisplayName = "旧林伐场",
            Kind = StrategicLocationKind.ResourceSite,
            ProductionPerWorldTimePulse =
            {
                new StrategicResourceAmount(StrategicManagementIds.ResourceWood, 12)
            }
        });
        Add(definitions.Locations, new StrategicLocationDefinition
        {
            LocationId = StrategicManagementIds.LocationBonefieldOutpost,
            MapSiteId = StrategicManagementIds.MapSiteBonefield,
            DisplayName = "白骨岗哨",
            Kind = StrategicLocationKind.Ruin,
            BattleEncounterId = "assault_bonefield",
            BattleMapDefinitionId = "bonefield_assault_v1",
            BattleScenePath = "res://scenes/world/sites/WorldSiteRoot.tscn",
            BattleObjectiveId = "occupy_bonefield"
        });
    }

    private static void AddBattleRewards(StrategicManagementDefinitionSet definitions)
    {
        Add(definitions.BattleRewards, new StrategicBattleRewardDefinition
        {
            RewardId = StrategicManagementIds.RewardBonefieldVictory,
            TargetLocationId = StrategicManagementIds.LocationBonefieldOutpost,
            DisplayName = "白骨岗哨占领奖励",
            VictorySummaryText = "白骨岗哨已转入我方控制，周边通路和基础物资点被打开。",
            DefeatSummaryText = "白骨岗哨仍由敌方控制，出征部队需要重整后再战。",
            VictoryProgressionText = "进展：新区域已被占领，可作为后续资源开发和战线推进节点。",
            DefeatProgressionText = "进展：本次未能夺取白骨岗哨，请先重整编制、补员后重新出征。",
            UnlockText = "占领：白骨岗哨",
            VictoryResourceRewards =
            {
                new StrategicResourceAmount(StrategicManagementIds.ResourceOre, 25),
                new StrategicResourceAmount(StrategicManagementIds.ResourceWood, 20)
            },
            EquipmentSampleIds =
            {
                StrategicManagementIds.EquipmentDawnshieldSpear,
                StrategicManagementIds.EquipmentSunLionArmor,
                StrategicManagementIds.EquipmentBonefieldCommandHorn
            },
            RewardEquipmentSampleId = StrategicManagementIds.EquipmentBonefieldCommandHorn
        });
    }

    private static void AddEquipmentSamples(StrategicManagementDefinitionSet definitions)
    {
        Add(definitions.EquipmentSamples, new StrategicEquipmentSampleDefinition
        {
            EquipmentSampleId = StrategicManagementIds.EquipmentDawnshieldSpear,
            DisplayName = "晨盾破阵枪",
            SlotKind = "weapon",
            Grade = "fine",
            RoleText = "武器样本：适合盾线英雄突破近身压力。"
        });
        Add(definitions.EquipmentSamples, new StrategicEquipmentSampleDefinition
        {
            EquipmentSampleId = StrategicManagementIds.EquipmentSunLionArmor,
            DisplayName = "日狮纹甲",
            SlotKind = "armor",
            Grade = "fine",
            RoleText = "护甲样本：强化前排英雄在攻占战中的承压身份。"
        });
        Add(definitions.EquipmentSamples, new StrategicEquipmentSampleDefinition
        {
            EquipmentSampleId = StrategicManagementIds.EquipmentBonefieldCommandHorn,
            DisplayName = "白骨号角",
            SlotKind = "token",
            Grade = "rare",
            RoleText = "号令道具：记录白骨岗哨战利品，可作为后续编队指挥物。"
        });
    }

    private static void AddCityIdentities(StrategicManagementDefinitionSet definitions)
    {
        Add(definitions.CityIdentities, new StrategicCityIdentityDefinition
        {
            CityIdentityId = StrategicManagementIds.CityIdentityPlainsHuman,
            DisplayName = "平原人类城池",
            NaturalCorpsDefinitionIds =
            {
                StrategicManagementIds.CorpsShieldLine,
                StrategicManagementIds.CorpsArcherLine,
                StrategicManagementIds.CorpsCavalryLine
            }
        });
    }

    private static void AddBuildings(StrategicManagementDefinitionSet definitions)
    {
        foreach (StrategicBuildingDefinition building in StrategicManagementBuildingDefinitionConfigLoader.LoadDefaultBuildings())
        {
            Add(definitions.Buildings, building);
        }
    }

    private static void AddCorps(StrategicManagementDefinitionSet definitions)
    {
        Add(definitions.Corps, CommonCorps(
            StrategicManagementIds.CorpsShieldLine,
            "天蓝石狮卫",
            30,
            20,
            30,
            FirstSliceHeroCompanyIds.ShieldCorpsUnit,
            FirstSliceHeroCompanyIds.ShieldCorpsCount));
        Add(definitions.Corps, CommonCorps(
            StrategicManagementIds.CorpsArcherLine,
            "穿阳弓手",
            35,
            20,
            30,
            FirstSliceHeroCompanyIds.ArcherCorpsUnit,
            FirstSliceHeroCompanyIds.ArcherCorpsCount));
        Add(definitions.Corps, CommonCorps(
            StrategicManagementIds.CorpsCavalryLine,
            "辉光龙骑",
            45,
            30,
            40,
            FirstSliceHeroCompanyIds.AssaultCorpsUnit,
            FirstSliceHeroCompanyIds.AssaultCorpsCount));
    }

    private static StrategicCorpsDefinition CommonCorps(
        string id,
        string displayName,
        int moneyCost,
        int foodCost,
        int soldiers,
        string battleUnitId,
        int battleUnitCount)
    {
        return new StrategicCorpsDefinition
        {
            CorpsDefinitionId = id,
            DisplayName = displayName,
            BattleUnitId = battleUnitId ?? "",
            BattleUnitCount = System.Math.Max(1, battleUnitCount),
            SoldierCapacityCost = soldiers,
            RequiredCityIdentityIds = { StrategicManagementIds.CityIdentityPlainsHuman },
            CreationCost =
            {
                new StrategicResourceAmount(StrategicManagementIds.ResourceMoney, moneyCost),
                new StrategicResourceAmount(StrategicManagementIds.ResourceFood, foodCost)
            },
            ReplenishFullCost =
            {
                new StrategicResourceAmount(StrategicManagementIds.ResourceMoney, moneyCost),
                new StrategicResourceAmount(StrategicManagementIds.ResourceFood, foodCost)
            }
        };
    }

    private static void AddHeroes(StrategicManagementDefinitionSet definitions)
    {
        Add(definitions.Heroes, new StrategicHeroDefinition
        {
            HeroDefinitionId = StrategicManagementIds.HeroOrdinaryCommander,
            DisplayName = "曦盾执旗者",
            BattleUnitId = FirstSliceHeroCompanyIds.ShieldHeroUnit
        });
        Add(definitions.Heroes, new StrategicHeroDefinition
        {
            HeroDefinitionId = StrategicManagementIds.HeroArcherCaptain,
            DisplayName = "逐日号令官",
            BattleUnitId = FirstSliceHeroCompanyIds.ArcherHeroUnit
        });
        Add(definitions.Heroes, new StrategicHeroDefinition
        {
            HeroDefinitionId = StrategicManagementIds.HeroCavalryCaptain,
            DisplayName = "裂光剑卫",
            BattleUnitId = FirstSliceHeroCompanyIds.AssaultHeroUnit
        });
    }

    private static void Add(Dictionary<string, StrategicResourceDefinition> target, StrategicResourceDefinition definition)
    {
        target[definition.ResourceId] = definition;
    }

    private static void Add(Dictionary<string, StrategicLocationDefinition> target, StrategicLocationDefinition definition)
    {
        target[definition.LocationId] = definition;
    }

    private static void Add(Dictionary<string, StrategicBattleRewardDefinition> target, StrategicBattleRewardDefinition definition)
    {
        target[definition.RewardId] = definition;
    }

    private static void Add(Dictionary<string, StrategicEquipmentSampleDefinition> target, StrategicEquipmentSampleDefinition definition)
    {
        target[definition.EquipmentSampleId] = definition;
    }

    private static void Add(Dictionary<string, StrategicCityIdentityDefinition> target, StrategicCityIdentityDefinition definition)
    {
        target[definition.CityIdentityId] = definition;
    }

    private static void Add(Dictionary<string, StrategicBuildingDefinition> target, StrategicBuildingDefinition definition)
    {
        target[definition.BuildingDefinitionId] = definition;
    }

    private static void Add(Dictionary<string, StrategicCorpsDefinition> target, StrategicCorpsDefinition definition)
    {
        target[definition.CorpsDefinitionId] = definition;
    }

    private static void Add(Dictionary<string, StrategicHeroDefinition> target, StrategicHeroDefinition definition)
    {
        target[definition.HeroDefinitionId] = definition;
    }
}
