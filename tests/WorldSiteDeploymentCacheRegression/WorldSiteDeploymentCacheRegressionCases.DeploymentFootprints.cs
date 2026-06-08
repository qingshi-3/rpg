using Godot;
using Rpg.Application.Battle;
using Rpg.Application.Maps;
using Rpg.Application.World;
using Rpg.Definitions.Maps;
using Rpg.Definitions.World;
using Rpg.Domain.Battle.Grid;
using Rpg.Domain.World;

internal static partial class WorldSiteDeploymentCacheRegressionCases
{
internal static void BattleDeploymentPreparerKeepsMixedResidentEnemyFootprintsSeparate()
{
    BattleGridMap grid = new();
    for (int x = 0; x < 4; x++)
    {
        for (int y = 0; y < 3; y++)
        {
            AddWalkableSurface(grid, x, y, terrainTag: "enemy_zone");
        }
    }

    for (int x = 10; x < 13; x++)
    {
        AddWalkableSurface(grid, x, 0, terrainTag: "legacy_garrison");
    }

    var markers = new[]
    {
        new SemanticMapMarkerData
        {
            MarkerId = "enemy_start",
            MarkerType = SemanticMapMarkerType.DeploymentZone,
            DeploymentSide = SemanticDeploymentSide.Enemy,
            AnchorCell = new Vector2I(0, 0),
            Width = 4,
            Height = 3
        }
    };
    WorldSiteRuntimeDeploymentCache cache = new WorldSiteRuntimeDeploymentCacheBuilder()
        .Build("site_under_test", grid, markers);
    WorldSiteState site = BuildDeploymentSite();
    site.OwnerFactionId = StrategicWorldIds.FactionUndead;
    site.Garrison.Add(new GarrisonState
    {
        UnitTypeId = "wide_enemy",
        Count = 1
    });
    site.Garrison.Add(new GarrisonState
    {
        UnitTypeId = "small_enemy",
        Count = 2
    });
    WorldSiteDefinition definition = BuildDefaultZoneDefinition(
        new Vector2I(10, 0),
        new Vector2I(11, 0),
        new Vector2I(12, 0));
    BattleStartRequest request = new()
    {
        TargetSiteId = site.SiteId,
        BattleKind = BattleKind.AssaultSite,
        AttackDirection = WorldSiteAttackDirection.West,
        AttackerFactionId = StrategicWorldIds.FactionPlayer,
        DefenderFactionId = StrategicWorldIds.FactionUndead
    };
    BattleForceRequest wideEnemyForce = new()
    {
        ForceId = "resident_wide_enemy",
        SourceKind = "DefenderSite",
        SourceId = site.SiteId,
        UnitDefinitionId = "wide_enemy",
        Count = 1,
        FactionId = StrategicWorldIds.FactionUndead,
        FootprintWidth = 2,
        FootprintHeight = 2
    };
    BattleForceRequest smallEnemyForce = new()
    {
        ForceId = "resident_small_enemy",
        SourceKind = "DefenderSite",
        SourceId = site.SiteId,
        UnitDefinitionId = "small_enemy",
        Count = 2,
        FactionId = StrategicWorldIds.FactionUndead,
        FootprintWidth = 1,
        FootprintHeight = 1
    };
    request.EnemyForces.Add(wideEnemyForce);
    request.EnemyForces.Add(smallEnemyForce);

    bool prepared = new WorldSiteBattleDeploymentPreparer().Prepare(
        request,
        site,
        definition,
        cache,
        grid,
        CanForceEnterWater,
        CanPlacementEnterWater,
        out string failureReason);

    AssertTrue(prepared, $"mixed resident enemy deployment should succeed failure={failureReason}");
    AssertEqual(1, wideEnemyForce.PreferredPlacements.Count, "wide resident enemy preferred placement count");
    AssertEqual(2, smallEnemyForce.PreferredPlacements.Count, "small resident enemy preferred placement count");
    AssertDeploymentFootprintsStayInsideCandidates(
        wideEnemyForce.PreferredPlacements,
        wideEnemyForce.FootprintWidth,
        wideEnemyForce.FootprintHeight,
        cache.GetDeploymentZoneCandidatesForSide(SemanticDeploymentSide.Enemy, wideEnemyForce.FactionId, WorldSiteAttackDirection.East),
        "wide resident enemy deployment footprint");
    AssertDeploymentFootprintsStayInsideCandidates(
        smallEnemyForce.PreferredPlacements,
        smallEnemyForce.FootprintWidth,
        smallEnemyForce.FootprintHeight,
        cache.GetDeploymentZoneCandidatesForSide(SemanticDeploymentSide.Enemy, smallEnemyForce.FactionId, WorldSiteAttackDirection.East),
        "small resident enemy deployment footprint");

    HashSet<GridPosition> occupied = new();
    AssertForceFootprintsDoNotOverlap(wideEnemyForce, occupied, "wide resident enemy footprint");
    AssertForceFootprintsDoNotOverlap(smallEnemyForce, occupied, "small resident enemy footprint");
}

private static void AssertForceFootprintsDoNotOverlap(
    BattleForceRequest force,
    HashSet<GridPosition> occupied,
    string message)
{
    if (force == null)
    {
        return;
    }

    foreach (BattleForcePlacementRequest placement in force.PreferredPlacements)
    {
        GridPosition anchor = new(placement.CellX, placement.CellY);
        foreach (GridPosition cell in BattleFootprintCells.Enumerate(anchor, force.FootprintWidth, force.FootprintHeight))
        {
            AssertTrue(
                occupied.Add(cell),
                $"{message} should not overlap anchor={anchor} cell={cell}");
        }
    }
}
}
