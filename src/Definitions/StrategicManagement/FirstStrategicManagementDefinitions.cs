using System.Collections.Generic;
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
        AddFacilities(definitions);
        AddCorps(definitions);
        AddHeroes(definitions);
        return definitions;
    }

    private static void AddResources(StrategicManagementDefinitionSet definitions)
    {
        Add(definitions.Resources, new StrategicResourceDefinition { ResourceId = StrategicManagementIds.ResourceFood, DisplayName = "粮食" });
        Add(definitions.Resources, new StrategicResourceDefinition { ResourceId = StrategicManagementIds.ResourceMoney, DisplayName = "资金" });
        Add(definitions.Resources, new StrategicResourceDefinition { ResourceId = StrategicManagementIds.ResourceBuildingMaterials, DisplayName = "建材" });
        Add(definitions.Resources, new StrategicResourceDefinition { ResourceId = StrategicManagementIds.ResourceBeastMaterials, DisplayName = "野兽素材" });
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
            FacilitySlotCount = 3
        });
        Add(definitions.Locations, new StrategicLocationDefinition
        {
            LocationId = StrategicManagementIds.LocationTimberSite,
            DisplayName = "旧林伐场",
            Kind = StrategicLocationKind.ResourceSite,
            ProductionPerWorldTimePulse =
            {
                new StrategicResourceAmount(StrategicManagementIds.ResourceBuildingMaterials, 12)
            }
        });
        Add(definitions.Locations, new StrategicLocationDefinition
        {
            LocationId = StrategicManagementIds.LocationBeastDen,
            MapSiteId = StrategicManagementIds.MapSiteBonefield,
            DisplayName = "白骨原",
            Kind = StrategicLocationKind.BeastMinorSite,
            BattleEncounterId = "assault_bonefield",
            BattleMapDefinitionId = "bonefield_assault_v1",
            BattleScenePath = "res://scenes/world/sites/WorldSiteRoot.tscn",
            BattleObjectiveId = "occupy_bonefield",
            SourcePermissionTags = { StrategicManagementIds.SourceTagBeast }
        });
    }

    private static void AddBattleRewards(StrategicManagementDefinitionSet definitions)
    {
        Add(definitions.BattleRewards, new StrategicBattleRewardDefinition
        {
            RewardId = StrategicManagementIds.RewardBonefieldVictory,
            TargetLocationId = StrategicManagementIds.LocationBeastDen,
            DisplayName = "白骨原攻占奖励",
            VictorySummaryText = "白骨原控制权已转入我方，野兽来源与后续驯养路线开放。",
            DefeatSummaryText = "白骨原仍由敌方控制，出征部队需要重整后再战。",
            VictoryProgressionText = "进展：获得野兽来源，可在城市配合兽栏推进野兽军团路线。",
            DefeatProgressionText = "进展：本次未夺取白骨原，请先重整溃散编制，再选择准备重新出征。",
            UnlockText = "解锁：白骨原野兽来源",
            VictoryResourceRewards =
            {
                new StrategicResourceAmount(StrategicManagementIds.ResourceBeastMaterials, 25),
                new StrategicResourceAmount(StrategicManagementIds.ResourceBuildingMaterials, 20)
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
            RoleText = "号令道具：记录白骨原战利品，可作为后续野兽军团指挥物。"
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

    private static void AddFacilities(StrategicManagementDefinitionSet definitions)
    {
        Add(definitions.Facilities, new StrategicFacilityDefinition
        {
            FacilityDefinitionId = StrategicManagementIds.FacilityTrainingGround,
            DisplayName = "训练场",
            ProvidedTags = { StrategicManagementIds.FacilityTagCommonTraining },
            BuildCost =
            {
                new StrategicResourceAmount(StrategicManagementIds.ResourceMoney, 20),
                new StrategicResourceAmount(StrategicManagementIds.ResourceBuildingMaterials, 40)
            }
        });
        Add(definitions.Facilities, new StrategicFacilityDefinition
        {
            FacilityDefinitionId = StrategicManagementIds.FacilityBeastPen,
            DisplayName = "兽栏",
            ProvidedTags = { StrategicManagementIds.FacilityTagBeastPen },
            BuildCost =
            {
                new StrategicResourceAmount(StrategicManagementIds.ResourceMoney, 40),
                new StrategicResourceAmount(StrategicManagementIds.ResourceBuildingMaterials, 60)
            }
        });
    }

    private static void AddCorps(StrategicManagementDefinitionSet definitions)
    {
        Add(definitions.Corps, CommonCorps(
            StrategicManagementIds.CorpsShieldLine,
            "天蓝石狮卫",
            30,
            FirstSliceHeroCompanyIds.ShieldCorpsUnit,
            FirstSliceHeroCompanyIds.ShieldCorpsCount));
        Add(definitions.Corps, CommonCorps(
            StrategicManagementIds.CorpsArcherLine,
            "穿阳弓手",
            35,
            FirstSliceHeroCompanyIds.ArcherCorpsUnit,
            FirstSliceHeroCompanyIds.ArcherCorpsCount));
        Add(definitions.Corps, CommonCorps(
            StrategicManagementIds.CorpsCavalryLine,
            "辉光龙骑",
            45,
            FirstSliceHeroCompanyIds.AssaultCorpsUnit,
            FirstSliceHeroCompanyIds.AssaultCorpsCount));
        Add(definitions.Corps, new StrategicCorpsDefinition
        {
            CorpsDefinitionId = StrategicManagementIds.CorpsWolfPack,
            DisplayName = "霜魂狼群",
            BattleUnitId = FirstSliceHeroCompanyIds.AssaultCorpsUnit,
            BattleUnitCount = FirstSliceHeroCompanyIds.AssaultCorpsCount,
            RequiredFacilityTags = { StrategicManagementIds.FacilityTagBeastPen },
            RequiredSourcePermissionTags = { StrategicManagementIds.SourceTagBeast },
            CreationCost =
            {
                new StrategicResourceAmount(StrategicManagementIds.ResourceMoney, 70),
                new StrategicResourceAmount(StrategicManagementIds.ResourceBeastMaterials, 30)
            },
            AptitudeTag = StrategicManagementIds.AptitudeTagBeast
        });
        Add(definitions.Corps, new StrategicCorpsDefinition
        {
            CorpsDefinitionId = StrategicManagementIds.CorpsGreatBeast,
            DisplayName = "巨兽冲锋队",
            BattleUnitId = FirstSliceHeroCompanyIds.AssaultCorpsUnit,
            BattleUnitCount = FirstSliceHeroCompanyIds.AssaultCorpsCount,
            RequiredFacilityTags = { StrategicManagementIds.FacilityTagBeastPen },
            RequiredSourcePermissionTags = { StrategicManagementIds.SourceTagBeast },
            CreationCost =
            {
                new StrategicResourceAmount(StrategicManagementIds.ResourceMoney, 120),
                new StrategicResourceAmount(StrategicManagementIds.ResourceBeastMaterials, 80)
            },
            AptitudeTag = StrategicManagementIds.AptitudeTagBeast
        });
    }

    private static StrategicCorpsDefinition CommonCorps(
        string id,
        string displayName,
        int moneyCost,
        string battleUnitId,
        int battleUnitCount)
    {
        return new StrategicCorpsDefinition
        {
            CorpsDefinitionId = id,
            DisplayName = displayName,
            BattleUnitId = battleUnitId ?? "",
            BattleUnitCount = System.Math.Max(1, battleUnitCount),
            RequiredCityIdentityIds = { StrategicManagementIds.CityIdentityPlainsHuman },
            CreationCost = { new StrategicResourceAmount(StrategicManagementIds.ResourceMoney, moneyCost) }
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
            HeroDefinitionId = StrategicManagementIds.HeroBeastTamer,
            DisplayName = "裂光剑卫",
            BattleUnitId = FirstSliceHeroCompanyIds.AssaultHeroUnit,
            AptitudeTags = { StrategicManagementIds.AptitudeTagBeast }
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

    private static void Add(Dictionary<string, StrategicFacilityDefinition> target, StrategicFacilityDefinition definition)
    {
        target[definition.FacilityDefinitionId] = definition;
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
