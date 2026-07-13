using System.Collections;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Reports;
using Rpg.Application.Battle.Settlement;
using Rpg.Application.StrategicBattleBridge;
using Rpg.Application.StrategicManagement;
using Rpg.Definitions.StrategicManagement;
using Rpg.Domain.StrategicManagement;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.Events;

internal static partial class StrategicManagementRegressionCases
{
    internal static void StrategicBattleActiveContextCompilesCompleteConsequenceSummary()
    {
        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
        StrategicManagementCommandService commands = new(definitions, new StrategicManagementRules(definitions));
        string[] heroIds =
        {
            StrategicManagementIds.HeroOrdinaryCommander,
            StrategicManagementIds.HeroArcherCaptain
        };
        StrategicCommandResult created = commands.CreateExpedition(
            state,
            StrategicManagementIds.LocationPlainsCity,
            StrategicManagementIds.LocationBonefieldOutpost,
            StrategicExpeditionIntent.AssaultLocation,
            heroIds);
        AssertTrue(created.Success, $"consequence fixture expedition should be created, got {created.FailureReason}");

        StrategicBattleBridgeService bridge = new(definitions);
        StrategicBattleSession session = bridge.CreateSession(
            state,
            created.CreatedEntityId,
            "res://return_to_world.tscn",
            "res://scenes/world/sites/WorldSiteRoot.tscn").Session;
        BattleStartRequest request = new()
        {
            RequestId = "complete_consequence_summary",
            BattleKind = BattleKind.AssaultSite
        };
        foreach (string heroId in heroIds)
        {
            AddParticipantForces(definitions, state, request, heroId);
        }
        bridge.AttachSessionToLegacyRequest(session, request);
        string reserveParticipantId = session.Participants.Single(item =>
            item.HeroId == StrategicManagementIds.HeroArcherCaptain).ParticipantId;
        string deployedParticipantId = session.Participants.Single(item =>
            item.HeroId == StrategicManagementIds.HeroOrdinaryCommander).ParticipantId;

        StrategicBattleActiveContext context = BuildCompletedActiveContext(
            bridge,
            state,
            session,
            request,
            BattleOutcome.Victory,
            _ => 1,
            configureDraft: draft => draft.PlayerForces.RemoveAll(force =>
                string.Equals(force.StrategicParticipantId, reserveParticipantId, StringComparison.Ordinal)),
            configureEvents: stream =>
            {
                stream.Add(new BattleEvent
                {
                    EventId = $"{session.SessionId}:destination",
                    BattleId = session.SessionId,
                    Kind = BattleEventKind.CommandAccepted,
                    BattleGroupId = deployedParticipantId,
                    SourceCommandId = "command_destination",
                    TargetId = "beacon_front",
                    ReasonCode = "destination_beacon_accepted",
                    HasTargetCells = true,
                    TargetGridX = 12,
                    TargetGridY = 8
                });
                stream.Add(new BattleEvent
                {
                    EventId = $"{session.SessionId}:regroup",
                    BattleId = session.SessionId,
                    Kind = BattleEventKind.CommandCompleted,
                    BattleGroupId = deployedParticipantId,
                    SourceCommandId = "command_regroup",
                    ReasonCode = "regroup_completed"
                });
                stream.Add(new BattleEvent
                {
                    EventId = $"{session.SessionId}:skill_used",
                    BattleId = session.SessionId,
                    Kind = BattleEventKind.SkillUsed,
                    ActorId = $"{deployedParticipantId}:hero",
                    SourceCommandId = "command_skill",
                    SourceDefinitionId = "skill_test",
                    TargetId = "enemy_corps",
                    ReasonCode = "skill_used"
                });
                stream.Add(new BattleEvent
                {
                    EventId = $"{session.SessionId}:skill_effect",
                    BattleId = session.SessionId,
                    Kind = BattleEventKind.EffectApplied,
                    ActorId = $"{deployedParticipantId}:hero",
                    SourceCommandId = "command_skill",
                    SourceActionId = "action_skill",
                    SourceDefinitionId = "skill_test",
                    EffectKind = "damage",
                    TargetId = "enemy_corps",
                    ReasonCode = "effect_applied",
                    CorpsStrengthDelta = -25,
                    RuntimeTick = 4
                });
                stream.Add(new BattleEvent
                {
                    EventId = $"{session.SessionId}:skill_failure",
                    BattleId = session.SessionId,
                    Kind = BattleEventKind.CommandFailed,
                    ActorId = $"{deployedParticipantId}:hero",
                    SourceCommandId = "command_skill_failed",
                    SourceDefinitionId = "skill_test",
                    TargetId = "enemy_hero",
                    ReasonCode = "cost_unavailable",
                    RuntimeTick = 5
                });
            },
            configureSettlementAndReport: (settlement, report) =>
            {
                settlement.Deltas.ChangedLocationIds.Add(StrategicManagementIds.LocationBonefieldOutpost);
                report.FailureCandidates.Add("equipment_level_low");
            });

        StrategicBattleResultSummary summary = bridge.BuildResultSummary(context);

        AssertTrue(summary.HasConsequenceFacts, "accepted envelope should compile consequence facts");
        AssertEqual(session.SessionId, summary.SessionId, "summary should retain session identity");
        AssertEqual(context.Snapshot.SnapshotId, summary.SnapshotId, "summary should retain snapshot identity");
        AssertEqual(created.CreatedEntityId, summary.ExpeditionId, "summary should retain expedition identity");
        AssertEqual(StrategicManagementIds.LocationBonefieldOutpost, summary.TargetLocationId, "summary should retain target identity");
        AssertEqual(BattleTerminationReason.NormalVictory, GetRequiredProperty<BattleTerminationReason>(summary, "TerminationReason"), "summary should retain Runtime termination");
        AssertEqual(session.BattleObjectiveId, GetRequiredProperty<string>(summary, "ObjectiveId"), "summary should retain objective identity");
        AssertEqual(context.ResultEnvelope.Report.ReportId, GetRequiredProperty<string>(summary, "ReportId"), "summary should retain report identity");
        AssertTrue(GetRequiredCollection(summary, "SettlementSourceEventIds").Count >= 7, "summary should retain accepted settlement event lineage");
        AssertTrue(GetRequiredCollection(summary, "ReportSourceEventIds").Count >= 7, "summary should retain report event lineage");
        AssertTrue(GetRequiredCollection(summary, "CommandAndSkillContributions").Count >= 5, "summary should retain command and skill trace facts");
        AssertTrue(GetRequiredCollection(summary, "FailureCandidates").Any(item => item?.ToString() == "equipment_level_low"), "summary should retain report failure attribution");
        AssertTrue(GetRequiredCollection(summary, "ChangedLocationIds").Any(item => item?.ToString() == StrategicManagementIds.LocationBonefieldOutpost), "summary should retain settlement location changes");

        AssertEqual(2, summary.ParticipantDispositions.Count, "every carried participant should have one disposition");
        AssertEqual(1, summary.Participants.Count, "only the deployed participant should receive a Runtime result");
        StrategicBattleParticipantResult participant = summary.Participants.Single();
        AssertEqual(deployedParticipantId, GetRequiredProperty<string>(participant, "ParticipantId"), "participant result should retain stable participant identity");
        AssertEqual("Survived", GetRequiredProperty<object>(participant, "HeroState").ToString(), "surviving hero should retain a supported state");
        AssertTrue(GetRequiredProperty<int>(participant, "StrengthLoss") > 0, "participant result should retain corps loss");
        AssertEqual(session.Participants.Single(item => item.ParticipantId == deployedParticipantId).CorpsEquipmentLevel, GetRequiredProperty<int>(participant, "CorpsEquipmentLevel"), "participant result should retain frozen equipment level");
        AssertTrue(GetRequiredProperty<bool>(participant, "RequiresRecovery"), "a damaged corps should expose recovery need");
        AssertEqual(StrategicBattleParticipantRole.Reserve, summary.ParticipantDispositions.Single(item => item.ParticipantId == reserveParticipantId).Role, "removed participant should remain an unchanged reserve");

        AssertEqual(definitions.BattleRewards.Values.Single().RewardId, summary.RewardClaimId, "victory should compile the target reward claim once");
        AssertTrue(summary.ResourceRewards.Count > 0, "victory should compile target resource rewards");
        AssertEqual(1, summary.RewardEquipmentSampleIds.Count, "victory should compile only the actual equipment reward as a grant");
        AssertEqual(3, GetRequiredCollection(summary, "EquipmentSampleIds").Count, "summary should retain all target equipment samples");
        AssertEqual(summary.RewardLines.Count, summary.RewardLines.Distinct(StringComparer.Ordinal).Count(), "summary reward lines should be compiled once");

        StrategicCommandResult applied = commands.ApplyBattleResultSummary(state, summary);
        AssertTrue(applied.Success, $"complete consequence summary should apply, got {applied.FailureReason}");
        StrategicBattleFeedbackRecord feedback = state.BattleFeedbackRecords[applied.CreatedEntityId];
        AssertEqual(summary.WorldChangeText, feedback.WorldChangeText, "feedback should consume summary world consequence");
        AssertEqual(summary.ProgressionText, feedback.ProgressionText, "feedback should consume summary progression consequence");
        AssertEqual(summary.RewardLines.Count, feedback.RewardLines.Count, "feedback should consume compiled reward lines without re-resolving them");
    }

    private static IReadOnlyList<object?> GetRequiredCollection(object instance, string propertyName)
    {
        object value = GetRequiredProperty<object>(instance, propertyName);
        return value is IEnumerable enumerable
            ? enumerable.Cast<object?>().ToList()
            : throw new InvalidOperationException($"property {propertyName} should be enumerable");
    }
}
