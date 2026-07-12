using Rpg.Application.Battle;
using Rpg.Application.Battle.Reports;
using Rpg.Application.Battle.Settlement;
using Rpg.Application.StrategicBattleBridge;
using Rpg.Application.StrategicManagement;
using Rpg.Application.World;
using Rpg.Definitions.StrategicManagement;
using Rpg.Domain.StrategicManagement;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Results;

internal static partial class StrategicManagementRegressionCases
{
    internal static void StrategicBattleGroupCardinalityCoversDefaultGroupsAndCountVariations()
    {
        string[] defaultHeroIds =
        {
            StrategicManagementIds.HeroOrdinaryCommander,
            StrategicManagementIds.HeroArcherCaptain,
            StrategicManagementIds.HeroCavalryCaptain
        };

        for (int mask = 1; mask < 1 << defaultHeroIds.Length; mask++)
        {
            string[] selectedHeroIds = defaultHeroIds
                .Where((_, index) => (mask & 1 << index) != 0)
                .ToArray();
            var setup = CreateCardinalityBattle(selectedHeroIds, mask);

            bool started = new WorldSiteBattleGroupRuntimeAdapter().TryStartActiveBattle(
                setup.Context,
                out WorldSiteBattleGroupRuntimeResolveResult launch);

            AssertTrue(started, $"cardinality launch should start for mask={mask}, got {launch.FailureReason}");
            HashSet<string> participantIds = setup.Session.Participants
                .Select(item => item.ParticipantId)
                .ToHashSet(StringComparer.Ordinal);
            var playerGroups = launch.Snapshot.BattleGroups
                .Where(group => participantIds.Contains(group.SourceForceId))
                .ToArray();
            AssertEqual(selectedHeroIds.Length, playerGroups.Length, $"mask={mask} should compile one snapshot per participant regardless of Count");
            AssertEqual(
                selectedHeroIds.Length,
                playerGroups.Select(group => group.BattleGroupId).Distinct(StringComparer.Ordinal).Count(),
                $"mask={mask} should preserve one stable group identity per participant");

            BattleRuntimeActor[] playerActors = launch.RuntimeController.State.Actors
                .Where(actor => participantIds.Contains(actor.SourceForceId))
                .ToArray();
            AssertEqual(
                selectedHeroIds.Length,
                playerActors.Count(actor => actor.Kind == BattleRuntimeActorKind.Hero),
                $"mask={mask} should create one hero actor per participant");
            AssertEqual(
                selectedHeroIds.Length,
                playerActors.Select(actor => actor.BattleGroupId).Distinct(StringComparer.Ordinal).Count(),
                $"mask={mask} should create one commander state per participant");
            foreach (StrategicBattleParticipantReference participant in setup.Session.Participants)
            {
                BattleRuntimeActor[] actors = playerActors
                    .Where(actor => string.Equals(actor.SourceForceId, participant.ParticipantId, StringComparison.Ordinal))
                    .ToArray();
                AssertEqual(1, actors.Count(actor => actor.Kind == BattleRuntimeActorKind.Hero), $"{participant.HeroId} should own exactly one hero actor");
                AssertTrue(
                    actors.All(actor => string.Equals(actor.BattleGroupId, participant.ParticipantId, StringComparison.Ordinal)),
                    $"{participant.HeroId} corps and presentation actors should share the participant commander identity");
            }
        }
    }

    internal static void StrategicBattleCasualtyUsesFrozenCorpsStrengthAndRuntimeCorpsSurvival()
    {
        foreach ((int remainingHitPoints, int expectedStrength) in new[] { (0, 0), (12, 50), (24, 100) })
        {
            var setup = CreateCardinalityBattle(
                new[] { StrategicManagementIds.HeroOrdinaryCommander },
                remainingHitPoints + 17);
            bool started = new WorldSiteBattleGroupRuntimeAdapter().TryStartActiveBattle(
                setup.Context,
                out WorldSiteBattleGroupRuntimeResolveResult launch);
            AssertTrue(started, $"casualty launch should start, got {launch.FailureReason}");

            StrategicBattleParticipantReference participant = setup.Session.Participants.Single();
            BattleRuntimeActor corpsActor = launch.RuntimeController.State.Actors.Single(actor =>
                actor.Kind == BattleRuntimeActorKind.Corps &&
                string.Equals(actor.SourceForceId, participant.ParticipantId, StringComparison.Ordinal));
            BattleOutcomeResult outcome = BuildCardinalityOutcome(
                launch,
                participant.ParticipantId,
                corpsActor.ActorId,
                remainingHitPoints);
            AssertEqual("", CompleteCardinalityContext(setup.Context, outcome), "valid cardinality result should publish");

            StrategicBattleResultSummary summary = setup.Bridge.BuildResultSummary(setup.Context);
            StrategicBattleParticipantResult participantResult = summary.Participants.Single();
            AssertEqual(expectedStrength, participantResult.RemainingCorpsStrength, $"remaining HP={remainingHitPoints} should scale the frozen 0-100 corps basis");

            StrategicCommandResult applied = setup.Commands.ApplyBattleResultSummary(setup.State, summary);
            AssertTrue(applied.Success, $"casualty summary should apply, got {applied.FailureReason}");
            AssertEqual(
                expectedStrength,
                setup.State.CorpsInstances[participant.CorpsInstanceId].Strength,
                "strategic writeback should use only the deployed corps survival fraction");
        }
    }

    internal static void StrategicBattleDuplicateMappingsRejectWithoutMutation()
    {
        var launchSetup = CreateCardinalityBattle(
            new[] { StrategicManagementIds.HeroOrdinaryCommander },
            91);
        StrategicBattleParticipantReference launchParticipant = launchSetup.Session.Participants.Single();
        StrategicHeroState launchHero = launchSetup.State.Heroes[launchParticipant.HeroId];
        launchSetup.Request.PlayerForces.Add(new BattleForceRequest
        {
            ForceId = "duplicate_hero_row",
            UnitDefinitionId = launchSetup.Definitions.Heroes[launchHero.HeroDefinitionId].BattleUnitId,
            StrategicParticipantId = launchParticipant.ParticipantId,
            Count = 7,
            FactionId = launchParticipant.FactionId,
            MaxHitPoints = 30,
            AttackDamage = 6,
            AttackRange = 1,
            AttackSpeed = 1.0,
            MoveStepSeconds = 0.16,
            AttackActionSeconds = 1.0,
            AttackImpactDelaySeconds = 0.45
        });
        int originalSnapshotGroupCount = launchSetup.Context.Snapshot.BattleGroups.Count;
        StrategicBattleParticipantRole originalRole = launchParticipant.Role;

        bool duplicateStarted = new WorldSiteBattleGroupRuntimeAdapter().TryStartActiveBattle(
            launchSetup.Context,
            out WorldSiteBattleGroupRuntimeResolveResult duplicateLaunch);

        AssertTrue(!duplicateStarted, "duplicate participant hero rows must reject before Runtime starts");
        AssertEqual(
            "strategic_battle_launch_participant_role_duplicate",
            duplicateLaunch.FailureReason,
            "duplicate launch mapping should expose a named failure");
        AssertEqual(originalSnapshotGroupCount, launchSetup.Context.Snapshot.BattleGroups.Count, "duplicate launch must not replace the active snapshot");
        AssertEqual(originalRole, launchParticipant.Role, "duplicate launch must not mutate the frozen participant role");

        var resultSetup = CreateCardinalityBattle(
            new[] { StrategicManagementIds.HeroOrdinaryCommander },
            92);
        bool resultStarted = new WorldSiteBattleGroupRuntimeAdapter().TryStartActiveBattle(
            resultSetup.Context,
            out WorldSiteBattleGroupRuntimeResolveResult resultLaunch);
        AssertTrue(resultStarted, $"result mapping launch should start, got {resultLaunch.FailureReason}");
        StrategicBattleParticipantReference resultParticipant = resultSetup.Session.Participants.Single();
        BattleRuntimeActor resultCorps = resultLaunch.RuntimeController.State.Actors.Single(actor =>
            actor.Kind == BattleRuntimeActorKind.Corps &&
            string.Equals(actor.SourceForceId, resultParticipant.ParticipantId, StringComparison.Ordinal));
        BattleOutcomeResult ambiguousOutcome = BuildCardinalityOutcome(
            resultLaunch,
            resultParticipant.ParticipantId,
            resultCorps.ActorId,
            24);
        BattleActorOutcome duplicateCorps = ambiguousOutcome.ActorOutcomes.Single(actor => actor.ActorId == resultCorps.ActorId);
        ambiguousOutcome.ActorOutcomes.Add(new BattleActorOutcome
        {
            ActorId = $"{duplicateCorps.ActorId}:duplicate",
            BattleGroupId = duplicateCorps.BattleGroupId,
            FactionId = duplicateCorps.FactionId,
            SourceForceId = duplicateCorps.SourceForceId,
            SourceStateId = duplicateCorps.SourceStateId,
            Kind = duplicateCorps.Kind,
            Survived = duplicateCorps.Survived,
            RemainingHitPoints = duplicateCorps.RemainingHitPoints
        });
        string publicationFailure = CompleteCardinalityContext(resultSetup.Context, ambiguousOutcome);
        int originalStrength = resultSetup.State.CorpsInstances[resultParticipant.CorpsInstanceId].Strength;

        AssertEqual(
            "strategic_battle_participant_actor_mapping_ambiguous",
            publicationFailure,
            "ambiguous participant actor mapping should reject envelope publication with a named failure");
        AssertTrue(resultSetup.Context.ResultEnvelope == null, "ambiguous result rejection must leave the active context unchanged");
        StrategicBattleResultSummary rejectedSummary = resultSetup.Bridge.BuildResultSummary(resultSetup.Context);
        StrategicCommandResult rejected = resultSetup.Commands.ApplyBattleResultSummary(resultSetup.State, rejectedSummary);
        AssertTrue(!rejected.Success, "ambiguous participant actor mapping must not apply strategic consequences");
        AssertEqual(
            originalStrength,
            resultSetup.State.CorpsInstances[resultParticipant.CorpsInstanceId].Strength,
            "ambiguous result rejection must leave strategic corps strength unchanged");
    }

    private static (
        StrategicManagementDefinitionSet Definitions,
        StrategicManagementState State,
        StrategicManagementCommandService Commands,
        StrategicBattleBridgeService Bridge,
        StrategicBattleSession Session,
        BattleStartRequest Request,
        StrategicBattleActiveContext Context) CreateCardinalityBattle(
            IReadOnlyList<string> heroIds,
            int countSeed)
    {
        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
        StrategicManagementCommandService commands = new(definitions, new StrategicManagementRules(definitions));
        StrategicCommandResult expedition = commands.CreateExpedition(
            state,
            StrategicManagementIds.LocationPlainsCity,
            StrategicManagementIds.LocationBonefieldOutpost,
            StrategicExpeditionIntent.AssaultLocation,
            heroIds.ToArray());
        AssertTrue(expedition.Success, $"cardinality expedition should be created, got {expedition.FailureReason}");

        StrategicBattleBridgeService bridge = new(definitions);
        StrategicBattleSession session = bridge.CreateSession(
            state,
            expedition.CreatedEntityId,
            "res://return_to_world.tscn",
            "res://scenes/world/sites/WorldSiteRoot.tscn").Session;
        BattleStartRequest request = new()
        {
            RequestId = $"request_cardinality_{countSeed}",
            BattleKind = BattleKind.AssaultSite,
            AttackerFactionId = StrategicManagementIds.FactionPlayer,
            DefenderFactionId = "enemy"
        };

        for (int index = 0; index < session.Participants.Count; index++)
        {
            StrategicBattleParticipantReference participant = session.Participants[index];
            StrategicHeroState hero = state.Heroes[participant.HeroId];
            StrategicCorpsInstanceState corps = state.CorpsInstances[participant.CorpsInstanceId];
            int x = 4 + index * 5;
            request.PlayerForces.Add(new BattleForceRequest
            {
                ForceId = $"{participant.ParticipantId}:hero_row",
                UnitDefinitionId = definitions.Heroes[hero.HeroDefinitionId].BattleUnitId,
                StrategicParticipantId = participant.ParticipantId,
                Count = 1 + (countSeed + index) % 3,
                FactionId = participant.FactionId,
                MaxHitPoints = 30,
                AttackDamage = 6,
                AttackRange = 1,
                AttackSpeed = 1.0,
                MoveStepSeconds = 0.16,
                AttackActionSeconds = 1.0,
                AttackImpactDelaySeconds = 0.45,
                PreferredPlacements =
                {
                    new BattleForcePlacementRequest { CellX = x, CellY = 6, CellHeight = 0 }
                }
            });
            request.PlayerForces.Add(new BattleForceRequest
            {
                ForceId = $"{participant.ParticipantId}:corps_row",
                UnitDefinitionId = definitions.Corps[corps.CorpsDefinitionId].BattleUnitId,
                StrategicParticipantId = participant.ParticipantId,
                Count = 3 + (countSeed + index) % 6,
                FactionId = participant.FactionId,
                MaxHitPoints = 24,
                AttackDamage = 5,
                AttackRange = 1,
                AttackSpeed = 1.0,
                MoveStepSeconds = 0.16,
                AttackActionSeconds = 1.0,
                AttackImpactDelaySeconds = 0.45,
                PreferredPlacements =
                {
                    new BattleForcePlacementRequest { CellX = x, CellY = 8, CellHeight = 0 }
                }
            });
        }

        request.EnemyForces.Add(new BattleForceRequest
        {
            ForceId = $"enemy_cardinality_{countSeed}",
            SourceKind = "Garrison",
            SourceId = $"enemy_cardinality_{countSeed}",
            UnitDefinitionId = "enemy_runtime_unit",
            Count = 1,
            FactionId = "enemy",
            MaxHitPoints = 24,
            AttackDamage = 4,
            AttackRange = 1,
            AttackSpeed = 1.0,
            MoveStepSeconds = 0.16,
            AttackActionSeconds = 1.0,
            AttackImpactDelaySeconds = 0.45,
            PreferredPlacements =
            {
                new BattleForcePlacementRequest { CellX = 28, CellY = 8, CellHeight = 0 }
            }
        });
        bridge.AttachSessionToLegacyRequest(session, request);
        AttachStrategicLaunchFlatTopology(request);
        StrategicBattleActiveContextResult context = bridge.CreateActiveContext(state, session, request);
        AssertTrue(context.Success, $"cardinality active context should be created, got {context.FailureReason}");
        return (definitions, state, commands, bridge, session, context.Context.PreparationDraft, context.Context);
    }

    private static BattleOutcomeResult BuildCardinalityOutcome(
        WorldSiteBattleGroupRuntimeResolveResult launch,
        string participantId,
        string participantCorpsActorId,
        int remainingCorpsHitPoints)
    {
        BattleOutcomeResult outcome = BattleOutcomeResult.Completed(
            launch.Snapshot.SnapshotId,
            launch.Snapshot.BattleId,
            BattleTerminationReason.NormalVictory);
        foreach (BattleRuntimeActor actor in launch.RuntimeController.State.Actors)
        {
            bool isParticipantCorps = string.Equals(actor.ActorId, participantCorpsActorId, StringComparison.Ordinal);
            int remainingHitPoints = isParticipantCorps
                ? remainingCorpsHitPoints
                : actor.Kind == BattleRuntimeActorKind.Hero || string.Equals(actor.SourceForceId, participantId, StringComparison.Ordinal)
                    ? Math.Max(1, actor.HitPoints)
                    : 0;
            outcome.ActorOutcomes.Add(new BattleActorOutcome
            {
                ActorId = actor.ActorId,
                BattleGroupId = actor.BattleGroupId,
                FactionId = actor.FactionId,
                SourceForceId = actor.SourceForceId,
                SourceStateId = actor.SourceStateId,
                Kind = actor.Kind,
                Survived = remainingHitPoints > 0,
                RemainingHitPoints = remainingHitPoints
            });
        }

        return outcome;
    }

    private static string CompleteCardinalityContext(
        StrategicBattleActiveContext context,
        BattleOutcomeResult outcome)
    {
        BattleEventStream eventStream = BuildEndedStream(outcome.BattleId);
        SettlementPlan settlement = new BattleSettlementService().BuildPlan(
            outcome.SnapshotId,
            outcome,
            eventStream);
        BattleRuntimeSessionResult runtimeResult = new()
        {
            Outcome = outcome,
            EventStream = eventStream
        };
        BattleReportRecord report = new BattleReportBuilder().Build(outcome, eventStream, settlement);
        return context.TryPublishResultEnvelope(runtimeResult, settlement, report, out string envelopeFailureReason)
            ? ""
            : envelopeFailureReason;
    }
}
