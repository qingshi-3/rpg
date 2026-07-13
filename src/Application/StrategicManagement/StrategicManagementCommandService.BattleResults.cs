using System.Linq;
using Rpg.Application.Battle;
using Rpg.Application.StrategicBattleBridge;
using Rpg.Definitions.StrategicManagement;
using Rpg.Domain.StrategicManagement;

namespace Rpg.Application.StrategicManagement;

public sealed partial class StrategicManagementCommandService
{
    public StrategicCommandResult ApplyBattleResultSummary(
        StrategicManagementState state,
        StrategicBattleResultSummary summary)
    {
        if (!HasCompiledSummaryBoundary(summary))
        {
            return Reject("ApplyBattleResultSummary", summary?.ExpeditionId, StrategicFailureReasons.BattleResultMismatch);
        }

        StrategicCommandResult replayResult = ResolveCommittedReplay(state, summary, out string replayFailureReason);
        if (replayResult != null)
        {
            return replayResult;
        }

        if (!string.IsNullOrWhiteSpace(replayFailureReason))
        {
            return Reject("ApplyBattleResultSummary", summary?.ExpeditionId, replayFailureReason);
        }

        string failureReason = GetBattleResultSummaryFailureReason(state, summary);
        if (!string.IsNullOrWhiteSpace(failureReason))
        {
            return Reject("ApplyBattleResultSummary", summary?.ExpeditionId, failureReason);
        }

        if (state.BattleFeedbackRecordIdsByExpedition.ContainsKey(summary.ExpeditionId ?? ""))
        {
            return Reject("ApplyBattleResultSummary", summary.ExpeditionId, StrategicFailureReasons.BattleResultAlreadyApplied);
        }

        StrategicExpeditionState expedition = state.Expeditions[summary.ExpeditionId];
        StrategicLocationState targetLocation = state.Locations[summary.TargetLocationId];
        System.Collections.Generic.Dictionary<string, int> preBattleStrengthByCorps = CapturePreBattleStrengths(state, expedition);
        bool victory = summary.Outcome == BattleOutcome.Victory && summary.ObjectiveSucceeded;
        if (victory)
        {
            targetLocation.OwnerFactionId = expedition.FactionId;
            targetLocation.ControlState = string.Equals(expedition.FactionId, StrategicManagementIds.FactionPlayer, System.StringComparison.Ordinal)
                ? StrategicLocationControlState.PlayerHeld
                : StrategicLocationControlState.EnemyHeld;
        }

        expedition.Status = StrategicExpeditionStatus.Resolved;
        string capturedCityId = ResolveCapturedCityForSurvivingParticipants(state, expedition, targetLocation, victory);
        ApplyParticipantBattleResults(state, expedition, summary, capturedCityId);
        StrategicBattleFeedbackRecord feedback = BuildBattleFeedbackRecord(
            state,
            expedition,
            summary,
            victory,
            preBattleStrengthByCorps);
        ApplyBattleFeedbackRewards(state, expedition.FactionId, summary, victory);
        state.BattleFeedbackRecords[feedback.FeedbackId] = feedback;
        state.BattleFeedbackRecordIdsByExpedition[expedition.ExpeditionId] = feedback.FeedbackId;
        if (!string.IsNullOrWhiteSpace(summary.SessionId) && !string.IsNullOrWhiteSpace(summary.SnapshotId))
        {
            state.BattleSettlementRecordsByExpedition[expedition.ExpeditionId] = new StrategicBattleSettlementRecord
            {
                ExpeditionId = expedition.ExpeditionId,
                SessionId = summary.SessionId,
                SnapshotId = summary.SnapshotId,
                FeedbackId = feedback.FeedbackId,
                ResultFingerprint = ComputeBattleResultFingerprint(summary)
            };
        }

        StrategicCommandResult result = StrategicCommandResult.Ok(summary.ExpeditionId, summary.TargetLocationId);
        result.CreatedEntityId = feedback.FeedbackId;
        result.ChangedFactIds.Add(feedback.FeedbackId);
        result.Events.Add(Event(
            "StrategicBattleResultApplied",
            summary.ExpeditionId,
            ("session", summary.SessionId ?? ""),
            ("target", summary.TargetLocationId ?? ""),
            ("outcome", summary.Outcome.ToString()),
            ("objectiveSucceeded", summary.ObjectiveSucceeded.ToString())));
        if (victory)
        {
            result.Events.Add(Event(
                "StrategicLocationOccupied",
                targetLocation.LocationId,
                ("owner", targetLocation.OwnerFactionId)));
        }

        result.Events.Add(Event(
            "StrategicBattleFeedbackRecorded",
            feedback.FeedbackId,
            ("target", feedback.TargetLocationId),
            ("victory", feedback.Victory.ToString())));
        if (victory && feedback.RewardLines.Count > 0)
        {
            result.Events.Add(Event(
                "StrategicBattleRewardApplied",
                feedback.FeedbackId,
                ("lines", string.Join("|", feedback.RewardLines))));
        }

        Accept("ApplyBattleResultSummary", summary.ExpeditionId, result);
        return result;
    }

    private static bool HasCompiledSummaryBoundary(StrategicBattleResultSummary summary)
    {
        return summary != null &&
               summary.HasConsequenceFacts &&
               !string.IsNullOrWhiteSpace(summary.SessionId) &&
               !string.IsNullOrWhiteSpace(summary.SnapshotId) &&
               summary.Outcome != BattleOutcome.None &&
               summary.TerminationReason != Rpg.Runtime.Battle.BattleTerminationReason.None &&
               !string.IsNullOrWhiteSpace(summary.ReportId) &&
               summary.ParticipantDispositions?.Count > 0;
    }

    private static StrategicCommandResult ResolveCommittedReplay(
        StrategicManagementState state,
        StrategicBattleResultSummary summary,
        out string failureReason)
    {
        failureReason = "";
        if (state == null || summary == null ||
            !state.BattleSettlementRecordsByExpedition.TryGetValue(summary.ExpeditionId ?? "", out StrategicBattleSettlementRecord record))
        {
            return null;
        }

        bool exact = !string.IsNullOrWhiteSpace(summary.SessionId) &&
                     !string.IsNullOrWhiteSpace(summary.SnapshotId) &&
                     string.Equals(record.ExpeditionId, summary.ExpeditionId, System.StringComparison.Ordinal) &&
                     string.Equals(record.SessionId, summary.SessionId, System.StringComparison.Ordinal) &&
                     string.Equals(record.SnapshotId, summary.SnapshotId, System.StringComparison.Ordinal) &&
                     string.Equals(record.ResultFingerprint, ComputeBattleResultFingerprint(summary), System.StringComparison.Ordinal);
        if (!exact)
        {
            failureReason = StrategicFailureReasons.BattleResultConflict;
            return null;
        }

        // Exact replay returns the original identity and never reapplies consequences.
        StrategicCommandResult replay = StrategicCommandResult.Ok(record.ExpeditionId, record.FeedbackId);
        replay.CreatedEntityId = record.FeedbackId;
        return replay;
    }

    private static string GetBattleResultSummaryFailureReason(
        StrategicManagementState state,
        StrategicBattleResultSummary summary)
    {
        if (state == null || summary == null)
        {
            return StrategicFailureReasons.MissingBattleResultSummary;
        }

        if (!state.Expeditions.TryGetValue(summary.ExpeditionId ?? "", out StrategicExpeditionState expedition))
        {
            return StrategicFailureReasons.MissingExpedition;
        }

        if (summary.Participants == null || summary.Participants.Any(item => item == null))
        {
            return StrategicFailureReasons.MissingBattleParticipantResult;
        }

        if (!state.Locations.ContainsKey(summary.TargetLocationId ?? "") ||
            !string.Equals(expedition.TargetLocationId, summary.TargetLocationId ?? "", System.StringComparison.Ordinal))
        {
            return StrategicFailureReasons.BattleResultMismatch;
        }

        // Every strategic writeback crosses the compiled Bridge contract; an empty
        // disposition set must never reactivate the retired direct-summary path.
        if (summary.ParticipantDispositions == null ||
            !summary.HasConsequenceFacts ||
            string.IsNullOrWhiteSpace(summary.SessionId) ||
            string.IsNullOrWhiteSpace(summary.SnapshotId) ||
            summary.Outcome == BattleOutcome.None ||
            summary.TerminationReason == Rpg.Runtime.Battle.BattleTerminationReason.None ||
            string.IsNullOrWhiteSpace(summary.ReportId) ||
            summary.ParticipantDispositions.Any(item => item == null) ||
            summary.ParticipantDispositions.Count != EnumerateExpeditionParticipants(expedition).Count ||
            summary.ParticipantDispositions.Any(item =>
                string.IsNullOrWhiteSpace(item.ParticipantId)) ||
            summary.ParticipantDispositions.Select(item => item.ParticipantId)
                .Distinct(System.StringComparer.Ordinal).Count() != summary.ParticipantDispositions.Count ||
            summary.ParticipantDispositions
                .GroupBy(item => $"{item.HeroId}\u001f{item.CorpsInstanceId}", System.StringComparer.Ordinal)
                .Any(group => group.Count() != 1))
        {
            return StrategicFailureReasons.BattleResultMismatch;
        }

        System.Collections.Generic.HashSet<string> deployedKeys = summary.ParticipantDispositions
            .Where(item => item.Role == StrategicBattleParticipantRole.Deployed)
            .Select(item => $"{item.HeroId}\u001f{item.CorpsInstanceId}")
            .ToHashSet(System.StringComparer.Ordinal);
        string[] resultKeys = summary.Participants
            .Select(item => $"{item.HeroId}\u001f{item.CorpsInstanceId}")
            .ToArray();
        if (resultKeys.Length != deployedKeys.Count ||
            deployedKeys.Count == 0 ||
            resultKeys.Distinct(System.StringComparer.Ordinal).Count() != resultKeys.Length ||
            resultKeys.Any(key => !deployedKeys.Contains(key)))
        {
            return StrategicFailureReasons.BattleResultMismatch;
        }

        foreach (StrategicExpeditionParticipantState participant in EnumerateExpeditionParticipants(expedition))
        {
            StrategicBattleParticipantDisposition disposition = summary.ParticipantDispositions.SingleOrDefault(item =>
                string.Equals(item?.HeroId ?? "", participant.HeroId ?? "", System.StringComparison.Ordinal) &&
                string.Equals(item?.CorpsInstanceId ?? "", participant.CorpsInstanceId ?? "", System.StringComparison.Ordinal));
            if (disposition == null ||
                disposition.Role == StrategicBattleParticipantRole.Unknown ||
                !string.Equals(disposition.RollbackStationLocationId ?? "", participant.RollbackStationLocationId ?? "", System.StringComparison.Ordinal))
            {
                return StrategicFailureReasons.BattleResultMismatch;
            }

            bool hasRuntimeResult = summary.Participants.Any(item =>
                string.Equals(item?.HeroId ?? "", participant.HeroId ?? "", System.StringComparison.Ordinal) &&
                string.Equals(item?.CorpsInstanceId ?? "", participant.CorpsInstanceId ?? "", System.StringComparison.Ordinal));
            if (disposition.Role == StrategicBattleParticipantRole.Deployed && !hasRuntimeResult)
            {
                return StrategicFailureReasons.MissingBattleParticipantResult;
            }

            StrategicBattleParticipantResult participantResult = summary.Participants.SingleOrDefault(item =>
                string.Equals(item?.HeroId ?? "", participant.HeroId ?? "", System.StringComparison.Ordinal) &&
                string.Equals(item?.CorpsInstanceId ?? "", participant.CorpsInstanceId ?? "", System.StringComparison.Ordinal));
            if (disposition.Role == StrategicBattleParticipantRole.Deployed &&
                (participantResult == null ||
                 !string.Equals(participantResult.ParticipantId ?? "", disposition.ParticipantId ?? "", System.StringComparison.Ordinal) ||
                 participantResult.HeroState == StrategicHeroBattleState.Unknown ||
                 participantResult.PreBattleCorpsStrength < 0 ||
                 participantResult.PreBattleCorpsStrength > 100 ||
                 participantResult.RemainingCorpsStrength < 0 ||
                 participantResult.RemainingCorpsStrength > participantResult.PreBattleCorpsStrength ||
                 participantResult.StrengthLoss != participantResult.PreBattleCorpsStrength - participantResult.RemainingCorpsStrength ||
                 participantResult.Routed != (participantResult.RemainingCorpsStrength == 0)))
            {
                return StrategicFailureReasons.BattleResultMismatch;
            }

            if (disposition.Role == StrategicBattleParticipantRole.Reserve && hasRuntimeResult)
            {
                return StrategicFailureReasons.BattleResultMismatch;
            }
        }

        bool victory = summary.Outcome == BattleOutcome.Victory && summary.ObjectiveSucceeded;
        if (!HasValidSummaryConsequenceFacts(summary, victory))
        {
            return StrategicFailureReasons.BattleResultMismatch;
        }

        return "";
    }

    private static bool HasValidSummaryConsequenceFacts(
        StrategicBattleResultSummary summary,
        bool victory)
    {
        if (summary.ResourceRewards == null ||
            summary.EquipmentSampleIds == null ||
            summary.RewardEquipmentSampleIds == null ||
            summary.RewardLines == null ||
            summary.FailureCandidates == null ||
            summary.ResourceRewards.Any(item =>
                item == null || item.Amount <= 0 || string.IsNullOrWhiteSpace(item.ResourceId)) ||
            summary.ResourceRewards.Select(item => item.ResourceId).Distinct(System.StringComparer.Ordinal).Count() != summary.ResourceRewards.Count ||
            summary.EquipmentSampleIds.Any(string.IsNullOrWhiteSpace) ||
            summary.EquipmentSampleIds.Distinct(System.StringComparer.Ordinal).Count() != summary.EquipmentSampleIds.Count ||
            summary.RewardEquipmentSampleIds.Any(string.IsNullOrWhiteSpace) ||
            summary.RewardEquipmentSampleIds.Distinct(System.StringComparer.Ordinal).Count() != summary.RewardEquipmentSampleIds.Count ||
            summary.RewardEquipmentSampleIds.Any(id => !summary.EquipmentSampleIds.Contains(id, System.StringComparer.Ordinal)) ||
            summary.RewardLines.Any(item => item == null) ||
            summary.RewardLines.Distinct(System.StringComparer.Ordinal).Count() != summary.RewardLines.Count)
        {
            return false;
        }

        bool carriesReward = summary.ResourceRewards.Count > 0 || summary.RewardEquipmentSampleIds.Count > 0;
        return victory
            ? !carriesReward || !string.IsNullOrWhiteSpace(summary.RewardClaimId)
            : string.IsNullOrWhiteSpace(summary.RewardClaimId) &&
              summary.ResourceRewards.Count == 0 &&
              summary.RewardEquipmentSampleIds.Count == 0;
    }

    private System.Collections.Generic.Dictionary<string, int> CapturePreBattleStrengths(
        StrategicManagementState state,
        StrategicExpeditionState expedition)
    {
        System.Collections.Generic.Dictionary<string, int> strengths = new(System.StringComparer.Ordinal);
        foreach (StrategicExpeditionParticipantState participant in EnumerateExpeditionParticipants(expedition))
        {
            if (state.CorpsInstances.TryGetValue(participant.CorpsInstanceId ?? "", out StrategicCorpsInstanceState corps))
            {
                strengths[corps.CorpsInstanceId] = System.Math.Clamp(corps.Strength, 0, 100);
            }
        }

        return strengths;
    }

    private StrategicBattleFeedbackRecord BuildBattleFeedbackRecord(
        StrategicManagementState state,
        StrategicExpeditionState expedition,
        StrategicBattleResultSummary summary,
        bool victory,
        System.Collections.Generic.IReadOnlyDictionary<string, int> preBattleStrengthByCorps)
    {
        StrategicBattleFeedbackRecord feedback = new()
        {
            FeedbackId = state.AllocateBattleFeedbackId(),
            ExpeditionId = expedition.ExpeditionId,
            SessionId = summary.SessionId ?? "",
            SnapshotId = summary.SnapshotId ?? "",
            TargetLocationId = summary.TargetLocationId ?? "",
            TargetDisplayName = string.IsNullOrWhiteSpace(summary.TargetDisplayName)
                ? summary.TargetLocationId ?? ""
                : summary.TargetDisplayName,
            Victory = victory,
            ObjectiveSucceeded = summary.ObjectiveSucceeded,
            OutcomeText = summary.OutcomeText ?? "",
            WorldChangeText = summary.WorldChangeText ?? "",
            FailureReasonText = summary.FailureReasonText ?? "",
            ProgressionText = summary.ProgressionText ?? "",
            AppliedElapsedWorldTimePulses = state.ElapsedWorldTimePulses
        };

        BuildParticipantFeedback(state, expedition, summary, preBattleStrengthByCorps, feedback);
        BuildHeroFeedback(state, expedition, summary, feedback);
        bool rewardAlreadyClaimed = !string.IsNullOrWhiteSpace(summary.RewardClaimId) &&
                                    state.ClaimedBattleRewardIds.Contains(summary.RewardClaimId);
        BuildEquipmentFeedback(summary, feedback, victory && !rewardAlreadyClaimed);
        BuildRewardLines(summary, feedback);
        if (rewardAlreadyClaimed)
        {
            feedback.RewardLines.Clear();
            feedback.RewardLines.Add($"奖励已领取：{summary.RewardClaimId}");
        }
        return feedback;
    }

    private void BuildParticipantFeedback(
        StrategicManagementState state,
        StrategicExpeditionState expedition,
        StrategicBattleResultSummary summary,
        System.Collections.Generic.IReadOnlyDictionary<string, int> preBattleStrengthByCorps,
        StrategicBattleFeedbackRecord feedback)
    {
        foreach (StrategicExpeditionParticipantState participant in EnumerateExpeditionParticipants(expedition))
        {
            if (ResolveParticipantRole(summary, participant) == StrategicBattleParticipantRole.Reserve)
            {
                continue;
            }

            StrategicBattleParticipantResult result = summary.Participants.First(item =>
                string.Equals(item.CorpsInstanceId ?? "", participant.CorpsInstanceId ?? "", System.StringComparison.Ordinal));
            state.Heroes.TryGetValue(participant.HeroId ?? "", out StrategicHeroState hero);
            state.CorpsInstances.TryGetValue(participant.CorpsInstanceId ?? "", out StrategicCorpsInstanceState corps);
            _definitions.Heroes.TryGetValue(hero?.HeroDefinitionId ?? "", out StrategicHeroDefinition heroDefinition);
            _definitions.Corps.TryGetValue(corps?.CorpsDefinitionId ?? "", out StrategicCorpsDefinition corpsDefinition);
            int remainingStrength = System.Math.Clamp(result.RemainingCorpsStrength, 0, 100);
            preBattleStrengthByCorps.TryGetValue(participant.CorpsInstanceId ?? "", out int preBattleStrength);
            int strengthLoss = System.Math.Max(0, preBattleStrength - remainingStrength);
            string heroDisplayName = heroDefinition?.DisplayName ?? participant.HeroId ?? "";
            string corpsDisplayName = corpsDefinition?.DisplayName ?? corps?.CorpsDefinitionId ?? "";
            feedback.ParticipantFeedback.Add(new StrategicBattleParticipantFeedbackRecord
            {
                HeroId = participant.HeroId ?? "",
                HeroDisplayName = heroDisplayName,
                CorpsInstanceId = participant.CorpsInstanceId ?? "",
                CorpsDisplayName = corpsDisplayName,
                RemainingCorpsStrength = remainingStrength,
                StrengthLoss = strengthLoss,
                ResultText = $"{heroDisplayName}率领的{corpsDisplayName}剩余强度{remainingStrength}，损失{strengthLoss}。"
            });
        }
    }

    private void BuildHeroFeedback(
        StrategicManagementState state,
        StrategicExpeditionState expedition,
        StrategicBattleResultSummary summary,
        StrategicBattleFeedbackRecord feedback)
    {
        foreach (StrategicExpeditionParticipantState participant in EnumerateExpeditionParticipants(expedition))
        {
            if (ResolveParticipantRole(summary, participant) == StrategicBattleParticipantRole.Reserve)
            {
                continue;
            }

            state.Heroes.TryGetValue(participant.HeroId ?? "", out StrategicHeroState hero);
            _definitions.Heroes.TryGetValue(hero?.HeroDefinitionId ?? "", out StrategicHeroDefinition heroDefinition);
            string heroDisplayName = heroDefinition?.DisplayName ?? participant.HeroId ?? "";
            StrategicBattleParticipantResult participantResult = summary.Participants.Single(item =>
                string.Equals(item.HeroId ?? "", participant.HeroId ?? "", System.StringComparison.Ordinal) &&
                string.Equals(item.CorpsInstanceId ?? "", participant.CorpsInstanceId ?? "", System.StringComparison.Ordinal));
            feedback.HeroFeedback.Add(new StrategicHeroBattleFeedbackRecord
            {
                HeroId = participant.HeroId ?? "",
                HeroDisplayName = heroDisplayName,
                ReactionText = BuildHeroResultText(heroDisplayName, participantResult.HeroState)
            });
        }
    }

    private static string BuildHeroResultText(
        string heroDisplayName,
        StrategicHeroBattleState heroState)
    {
        string stateText = heroState switch
        {
            StrategicHeroBattleState.Survived => "存活",
            StrategicHeroBattleState.Defeated => "战败",
            StrategicHeroBattleState.Retreated => "已撤退",
            StrategicHeroBattleState.Unavailable => "战报状态不可用",
            _ => "状态未知"
        };
        return $"{heroDisplayName}：{stateText}。";
    }

    private void BuildEquipmentFeedback(
        StrategicBattleResultSummary summary,
        StrategicBattleFeedbackRecord feedback,
        bool victory)
    {
        foreach (string equipmentSampleId in summary?.EquipmentSampleIds ?? Enumerable.Empty<string>())
        {
            if (!_definitions.EquipmentSamples.TryGetValue(equipmentSampleId ?? "", out StrategicEquipmentSampleDefinition equipment))
            {
                continue;
            }

            feedback.EquipmentSamples.Add(new StrategicEquipmentSampleFeedbackRecord
            {
                EquipmentSampleId = equipment.EquipmentSampleId,
                DisplayName = equipment.DisplayName,
                SlotKind = equipment.SlotKind,
                Grade = equipment.Grade,
                RoleText = equipment.RoleText,
                IsReward = victory && summary.RewardEquipmentSampleIds.Contains(
                    equipment.EquipmentSampleId,
                    System.StringComparer.Ordinal)
            });
        }
    }

    private static void BuildRewardLines(
        StrategicBattleResultSummary summary,
        StrategicBattleFeedbackRecord feedback)
    {
        foreach (string line in summary?.RewardLines ?? Enumerable.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                feedback.RewardLines.Add(line);
            }
        }
    }

    private void ApplyBattleFeedbackRewards(
        StrategicManagementState state,
        string factionId,
        StrategicBattleResultSummary summary,
        bool victory)
    {
        if (!victory)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(summary.RewardClaimId) &&
            state.ClaimedBattleRewardIds.Contains(summary.RewardClaimId))
        {
            return;
        }

        foreach (StrategicResourceAmount amount in summary.ResourceRewards ?? Enumerable.Empty<StrategicResourceAmount>())
        {
            state.AddResourceAmount(factionId, amount.ResourceId, amount.Amount);
        }

        AddUnique(state.ClaimedBattleRewardIds, summary.RewardClaimId);
        foreach (string equipmentSampleId in summary.RewardEquipmentSampleIds ?? Enumerable.Empty<string>())
        {
            AddUnique(state.UnlockedEquipmentSampleIds, equipmentSampleId);
        }
    }

    private static string ResolveCapturedCityForSurvivingParticipants(
        StrategicManagementState state,
        StrategicExpeditionState expedition,
        StrategicLocationState targetLocation,
        bool victory)
    {
        if (!victory ||
            state == null ||
            expedition == null ||
            targetLocation == null ||
            !state.Cities.ContainsKey(targetLocation.LocationId) ||
            !string.Equals(targetLocation.OwnerFactionId, expedition.FactionId, System.StringComparison.Ordinal))
        {
            return "";
        }

        return targetLocation.LocationId;
    }

    private static void ApplyParticipantBattleResults(
        StrategicManagementState state,
        StrategicExpeditionState expedition,
        StrategicBattleResultSummary summary,
        string capturedCityId)
    {
        foreach (StrategicExpeditionParticipantState participant in EnumerateExpeditionParticipants(expedition))
        {
            StrategicBattleParticipantRole role = ResolveParticipantRole(summary, participant);
            participant.BattleRole = role;
            if (role == StrategicBattleParticipantRole.Reserve)
            {
                UnlockExpeditionParticipant(state, expedition, participant, null, participant.RollbackStationLocationId);
                continue;
            }

            StrategicBattleParticipantResult result = summary.Participants.First(item =>
                string.Equals(item.HeroId ?? "", participant.HeroId ?? "", System.StringComparison.Ordinal) &&
                string.Equals(item.CorpsInstanceId ?? "", participant.CorpsInstanceId ?? "", System.StringComparison.Ordinal));
            UnlockExpeditionParticipant(state, expedition, participant, result, capturedCityId);
        }
    }

    private static StrategicBattleParticipantRole ResolveParticipantRole(
        StrategicBattleResultSummary summary,
        StrategicExpeditionParticipantState participant)
    {
        StrategicBattleParticipantDisposition disposition = summary?.ParticipantDispositions?.FirstOrDefault(item =>
            string.Equals(item?.HeroId ?? "", participant?.HeroId ?? "", System.StringComparison.Ordinal) &&
            string.Equals(item?.CorpsInstanceId ?? "", participant?.CorpsInstanceId ?? "", System.StringComparison.Ordinal));
        return disposition?.Role ?? StrategicBattleParticipantRole.Deployed;
    }

    private static string ComputeBattleResultFingerprint(StrategicBattleResultSummary summary)
    {
        byte[] payload = System.Text.Encoding.UTF8.GetBytes(
            System.Text.Json.JsonSerializer.Serialize(summary ?? new StrategicBattleResultSummary()));
        return System.Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(payload));
    }
}
