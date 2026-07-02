using Rpg.Application.Battle;
using Rpg.Application.Battle.Snapshots;
using Rpg.Application.StrategicBattleBridge;
using Rpg.Application.StrategicManagement;
using Rpg.Application.World;
using Rpg.Definitions.StrategicManagement;
using Rpg.Definitions.World;
using Rpg.Domain.StrategicManagement;

internal static partial class WorldSiteDeploymentCacheRegressionCases
{
internal static void BattleSkillAuthorityDeletesLegacyAbilityReferencesFromGodotResources()
{
    string root = ProjectRoot();
    string[] forbiddenFragments =
    {
        "AbilityComponent",
        "AbilityDefinition.cs",
        "BattleAbilityQueries.cs",
        "basic_attack.tres"
    };

    foreach (string filePath in EnumerateGodotResourceFiles(root))
    {
        string relativePath = Path.GetRelativePath(root, filePath);
        string source = File.ReadAllText(filePath);
        foreach (string fragment in forbiddenFragments)
        {
            AssertTrue(
                !source.Contains(fragment, StringComparison.Ordinal),
                $"Godot scene/resource should not reference deleted legacy ability authority file={relativePath} fragment={fragment}");
        }
    }
}

internal static void StrategicBattleLaunchSnapshotPreservesStartingHeroSkillGrants()
{
    foreach ((string heroId, string heroUnitId, string corpsUnitId, int corpsCount) in EnumerateStartingHeroSkillGrantCases())
    {
        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
        StrategicManagementCommandService commands = new(definitions, new StrategicManagementRules(definitions));

        StrategicCommandResult expeditionResult = commands.CreateExpedition(
            state,
            StrategicManagementIds.LocationPlainsCity,
            StrategicManagementIds.LocationBonefieldOutpost,
            StrategicExpeditionIntent.AssaultLocation,
            new[] { heroId });
        AssertTrue(
            expeditionResult.Success,
            $"assault expedition should be created through Strategic Management hero={heroId} reason={expeditionResult.FailureReason}");

        StrategicBattleBridgeService bridge = new(definitions);
        StrategicBattleSessionResult sessionResult = bridge.CreateSession(
            state,
            expeditionResult.CreatedEntityId,
            "res://scenes/world/StrategicWorldRoot.tscn",
            "res://scenes/world/sites/WorldSiteRoot.tscn");
        AssertTrue(
            sessionResult.Success,
            $"strategic battle session should be created hero={heroId} reason={sessionResult.FailureReason}");
        StrategicBattleParticipantReference participant = sessionResult.Session.Participants.Single();

        BattleStartRequest compatibilityRequest = BuildAssaultLaunchCompatibilityRequest(
            participant,
            heroUnitId,
            corpsUnitId,
            corpsCount);
        StrategicBattleActiveContextResult activeContextResult = bridge.CreateActiveContext(
            state,
            sessionResult.Session,
            compatibilityRequest);
        AssertTrue(
            activeContextResult.Success,
            $"strategic active context should compile a snapshot hero={heroId} reason={activeContextResult.FailureReason}");
        AssertThunderKitSkillGrants(
            activeContextResult.Context.Snapshot.SkillDefinitions,
            participant.ParticipantId,
            participant.HeroId,
            $"active bridge snapshot hero={heroId}");

        StrategicBattleLaunchSnapshotSyncResult syncResult = new StrategicBattleLaunchSnapshotSyncService().Sync(
            activeContextResult.Context,
            compatibilityRequest);
        AssertTrue(
            syncResult.Success,
            $"strategic launch snapshot sync should succeed hero={heroId} reason={syncResult.FailureReason}");
        AssertThunderKitSkillGrants(
            syncResult.Snapshot.SkillDefinitions,
            participant.ParticipantId,
            participant.HeroId,
            $"launch snapshot hero={heroId}");
        AssertTrue(
            syncResult.Snapshot.BattleGroups.Any(group =>
                string.Equals(group.RuntimeCommanderGroupId, participant.ParticipantId, StringComparison.Ordinal) &&
                string.Equals(group.SourceForceId, participant.ParticipantId, StringComparison.Ordinal)),
            $"launch snapshot should keep strategic participant id as runtime commander/source force for HUD skill ownership hero={heroId}");
    }
}

private static IEnumerable<string> EnumerateGodotResourceFiles(string root)
{
    foreach (string directory in new[] { "scenes", "assets" })
    {
        string absoluteDirectory = Path.Combine(root, directory);
        if (!Directory.Exists(absoluteDirectory))
        {
            continue;
        }

        foreach (string filePath in Directory.EnumerateFiles(absoluteDirectory, "*.*", SearchOption.AllDirectories))
        {
            string extension = Path.GetExtension(filePath);
            if (string.Equals(extension, ".tscn", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".tres", StringComparison.OrdinalIgnoreCase))
            {
                yield return filePath;
            }
        }
    }
}

private static IEnumerable<(string HeroId, string HeroUnitId, string CorpsUnitId, int CorpsCount)> EnumerateStartingHeroSkillGrantCases()
{
    yield return (
        StrategicManagementIds.HeroOrdinaryCommander,
        FirstSliceHeroCompanyIds.ShieldHeroUnit,
        FirstSliceHeroCompanyIds.ShieldCorpsUnit,
        FirstSliceHeroCompanyIds.ShieldCorpsCount);
    yield return (
        StrategicManagementIds.HeroArcherCaptain,
        FirstSliceHeroCompanyIds.ArcherHeroUnit,
        FirstSliceHeroCompanyIds.ArcherCorpsUnit,
        FirstSliceHeroCompanyIds.ArcherCorpsCount);
    yield return (
        StrategicManagementIds.HeroCavalryCaptain,
        FirstSliceHeroCompanyIds.AssaultHeroUnit,
        FirstSliceHeroCompanyIds.AssaultCorpsUnit,
        FirstSliceHeroCompanyIds.AssaultCorpsCount);
}

private static BattleStartRequest BuildAssaultLaunchCompatibilityRequest(
    StrategicBattleParticipantReference participant,
    string heroUnitId,
    string corpsUnitId,
    int corpsCount)
{
    BattleStartRequest request = new()
    {
        RequestId = "battle_skill_runtime_repair_assault",
        ContextId = "battle_skill_runtime_repair_assault",
        BattleKind = BattleKind.AssaultSite,
        SourceSiteId = StrategicWorldIds.SitePlayerCamp,
        TargetSiteId = StrategicWorldIds.SiteBonefield,
        AttackerFactionId = StrategicWorldIds.FactionPlayer,
        DefenderFactionId = StrategicWorldIds.FactionUndead,
        MapDefinitionId = "bonefield_assault_v1"
    };
    request.PlayerForces.Add(BuildStrategicLaunchForce(
        participant,
        heroUnitId,
        count: 1,
        forceId: "assault_hero",
        heroUnitId,
        corpsUnitId,
        x: 0,
        y: 0));
    request.PlayerForces.Add(BuildStrategicLaunchForce(
        participant,
        corpsUnitId,
        corpsCount,
        "assault_corps",
        heroUnitId,
        corpsUnitId,
        x: 0,
        y: 1));
    request.EnemyForces.Add(BuildEnemyLaunchForce());
    return request;
}

private static BattleForceRequest BuildStrategicLaunchForce(
    StrategicBattleParticipantReference participant,
    string unitDefinitionId,
    int count,
    string forceId,
    string heroBattleUnitId,
    string corpsBattleUnitId,
    int x,
    int y)
{
    BattleForceRequest force = BuildLaunchForce(
        forceId,
        unitDefinitionId,
        count,
        StrategicWorldIds.FactionPlayer,
        x,
        y);
    force.CommandGroupId = participant.ParticipantId;
    force.SourceKind = "StrategicExpeditionParticipant";
    force.SourceId = participant.ParticipantId;
    force.StrategicParticipantId = participant.ParticipantId;
    force.StrategicHeroId = participant.HeroId;
    force.StrategicHeroDefinitionId = participant.HeroDefinitionId;
    force.StrategicHeroBattleUnitId = heroBattleUnitId;
    force.StrategicCorpsInstanceId = participant.CorpsInstanceId;
    force.StrategicCorpsDefinitionId = participant.CorpsDefinitionId;
    force.StrategicCorpsBattleUnitId = corpsBattleUnitId;
    force.StrategicSourceLocationId = participant.SourceLocationId;
    force.StrategicPreBattleCorpsStrength = participant.PreBattleCorpsStrength;
    return force;
}

private static BattleForceRequest BuildEnemyLaunchForce()
{
    return BuildLaunchForce(
        "bonefield_leader",
        FirstSliceHeroCompanyIds.BonefieldLeaderUnit,
        1,
        StrategicWorldIds.FactionUndead,
        x: 4,
        y: 0);
}

private static BattleForceRequest BuildLaunchForce(
    string forceId,
    string unitDefinitionId,
    int count,
    string factionId,
    int x,
    int y)
{
    return new BattleForceRequest
    {
        ForceId = forceId,
        UnitDefinitionId = unitDefinitionId,
        Count = count,
        FactionId = factionId,
        FootprintWidth = 1,
        FootprintHeight = 1,
        MaxHitPoints = 100,
        AttackDamage = 10,
        AttackRange = 1,
        AttackSpeed = 1,
        MoveStepSeconds = 0.2,
        AttackActionSeconds = 0.4,
        AttackImpactDelaySeconds = 0.2,
        PreferredPlacements =
        {
            new BattleForcePlacementRequest
            {
                PlacementId = $"{forceId}:preferred",
                CellX = x,
                CellY = y,
                CellHeight = 0
            }
        }
    };
}

private static void AssertThunderKitSkillGrants(
    IReadOnlyList<BattleSkillSnapshot> skills,
    string participantId,
    string heroId,
    string context)
{
    string[] expectedSkillIds =
    {
        "skill_thunder_tag_throw",
        "skill_thunder_mark_fold",
        "skill_thunder_spiral_break"
    };

    foreach (string skillDefinitionId in expectedSkillIds)
    {
        BattleSkillSnapshot skill = skills.FirstOrDefault(item =>
            string.Equals(item.SkillDefinitionId, skillDefinitionId, StringComparison.Ordinal));
        AssertTrue(skill != null, $"{context} should include assault skill {skillDefinitionId}");
        AssertEqual(heroId, skill.OwnerHeroId, $"{context} {skillDefinitionId} owner hero");
        AssertEqual(participantId, skill.OwnerBattleGroupId, $"{context} {skillDefinitionId} owner battle group");
        AssertEqual(participantId, skill.RuntimeCommanderGroupId, $"{context} {skillDefinitionId} runtime commander group");
        AssertTrue(
            !string.IsNullOrWhiteSpace(skill.GrantedSkillId),
            $"{context} {skillDefinitionId} should have a stable granted skill id");
        AssertTrue(
            skill.GrantedSkillId.StartsWith($"default_hero:{heroId}:grant:", StringComparison.Ordinal) &&
            !skill.GrantedSkillId.Contains("battle_group", StringComparison.Ordinal) &&
            !skill.GrantedSkillId.Contains("strategic_participant:", StringComparison.Ordinal),
            $"{context} {skillDefinitionId} grant id should be hero-owned and independent from battle-group/runtime participant ids");
    }
}
}
