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
        string failureReason = GetBattleResultSummaryFailureReason(state, summary);
        if (!string.IsNullOrWhiteSpace(failureReason))
        {
            return Reject("ApplyBattleResultSummary", summary?.ExpeditionId, failureReason);
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
        ApplyParticipantBattleResults(state, expedition, summary);
        StrategicBattleFeedbackRecord feedback = BuildBattleFeedbackRecord(
            state,
            expedition,
            targetLocation,
            summary,
            victory,
            preBattleStrengthByCorps);
        ApplyBattleFeedbackRewards(state, expedition.FactionId, feedback, victory);
        state.BattleFeedbackRecords[feedback.FeedbackId] = feedback;
        state.BattleFeedbackRecordIdsByExpedition[expedition.ExpeditionId] = feedback.FeedbackId;

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

        if (state.BattleFeedbackRecordIdsByExpedition.ContainsKey(summary.ExpeditionId ?? ""))
        {
            return StrategicFailureReasons.BattleResultAlreadyApplied;
        }

        if (!state.Locations.ContainsKey(summary.TargetLocationId ?? "") ||
            !string.Equals(expedition.TargetLocationId, summary.TargetLocationId ?? "", System.StringComparison.Ordinal))
        {
            return StrategicFailureReasons.BattleResultMismatch;
        }

        foreach (StrategicExpeditionParticipantState participant in EnumerateExpeditionParticipants(expedition))
        {
            if (!summary.Participants.Any(item =>
                    string.Equals(item?.CorpsInstanceId ?? "", participant.CorpsInstanceId ?? "", System.StringComparison.Ordinal)))
            {
                return StrategicFailureReasons.MissingBattleParticipantResult;
            }
        }

        return "";
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
        StrategicLocationState targetLocation,
        StrategicBattleResultSummary summary,
        bool victory,
        System.Collections.Generic.IReadOnlyDictionary<string, int> preBattleStrengthByCorps)
    {
        _definitions.Locations.TryGetValue(summary.TargetLocationId ?? "", out StrategicLocationDefinition targetDefinition);
        StrategicBattleRewardDefinition reward = ResolveBattleReward(summary.TargetLocationId);
        bool rewardAlreadyClaimed = reward != null &&
                                    state.ClaimedBattleRewardIds.Contains(reward.RewardId);
        StrategicBattleFeedbackRecord feedback = new()
        {
            FeedbackId = state.AllocateBattleFeedbackId(),
            ExpeditionId = expedition.ExpeditionId,
            SessionId = summary.SessionId ?? "",
            TargetLocationId = summary.TargetLocationId ?? "",
            TargetDisplayName = targetDefinition?.DisplayName ?? summary.TargetLocationId ?? "",
            Victory = victory,
            ObjectiveSucceeded = summary.ObjectiveSucceeded,
            OutcomeText = victory ? "胜利" : "失败",
            WorldChangeText = BuildWorldChangeText(targetLocation, targetDefinition, reward, victory),
            FailureReasonText = victory ? "" : "阵线被守军压散，重整编制后再进攻。",
            ProgressionText = victory
                ? reward?.VictoryProgressionText ?? "进展：目标已被控制。"
                : reward?.DefeatProgressionText ?? "进展：本次未取得战略收益。",
            AppliedElapsedWorldTimePulses = state.ElapsedWorldTimePulses
        };

        BuildParticipantFeedback(state, expedition, summary, preBattleStrengthByCorps, feedback);
        BuildHeroFeedback(state, expedition, feedback, victory);
        BuildEquipmentFeedback(reward, feedback, victory && !rewardAlreadyClaimed);
        BuildRewardLines(reward, feedback, victory, rewardAlreadyClaimed);
        return feedback;
    }

    private StrategicBattleRewardDefinition ResolveBattleReward(string targetLocationId)
    {
        return _definitions.BattleRewards.Values.FirstOrDefault(reward =>
            string.Equals(reward.TargetLocationId, targetLocationId ?? "", System.StringComparison.Ordinal));
    }

    private static string BuildWorldChangeText(
        StrategicLocationState targetLocation,
        StrategicLocationDefinition targetDefinition,
        StrategicBattleRewardDefinition reward,
        bool victory)
    {
        if (reward != null)
        {
            return victory ? reward.VictorySummaryText : reward.DefeatSummaryText;
        }

        string name = targetDefinition?.DisplayName ?? targetLocation?.LocationId ?? "目标地点";
        return victory
            ? $"{name}已转入我方控制。"
            : $"{name}仍未被我方控制。";
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
        StrategicBattleFeedbackRecord feedback,
        bool victory)
    {
        foreach (StrategicExpeditionParticipantState participant in EnumerateExpeditionParticipants(expedition))
        {
            state.Heroes.TryGetValue(participant.HeroId ?? "", out StrategicHeroState hero);
            _definitions.Heroes.TryGetValue(hero?.HeroDefinitionId ?? "", out StrategicHeroDefinition heroDefinition);
            string heroDisplayName = heroDefinition?.DisplayName ?? participant.HeroId ?? "";
            feedback.HeroFeedback.Add(new StrategicHeroBattleFeedbackRecord
            {
                HeroId = participant.HeroId ?? "",
                HeroDisplayName = heroDisplayName,
                ReactionText = BuildHeroReactionText(hero?.HeroDefinitionId ?? participant.HeroId ?? "", heroDisplayName, victory)
            });
        }
    }

    private static string BuildHeroReactionText(
        string heroDefinitionId,
        string heroDisplayName,
        bool victory)
    {
        if (!victory)
        {
            return $"{heroDisplayName}：阵线被拖散了。先重整编制，再压住敌军侧翼。";
        }

        if (string.Equals(heroDefinitionId, StrategicManagementIds.HeroCavalryCaptain, System.StringComparison.Ordinal))
        {
            return $"{heroDisplayName}：侧翼已经打开，下一次出征可以更快压进。";
        }

        return $"{heroDisplayName}：白骨岗哨的突破口已经打开。";
    }

    private void BuildEquipmentFeedback(
        StrategicBattleRewardDefinition reward,
        StrategicBattleFeedbackRecord feedback,
        bool victory)
    {
        foreach (string equipmentSampleId in reward?.EquipmentSampleIds ?? Enumerable.Empty<string>())
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
                IsReward = victory &&
                           string.Equals(equipment.EquipmentSampleId, reward.RewardEquipmentSampleId, System.StringComparison.Ordinal)
            });
        }
    }

    private void BuildRewardLines(
        StrategicBattleRewardDefinition reward,
        StrategicBattleFeedbackRecord feedback,
        bool victory,
        bool rewardAlreadyClaimed)
    {
        if (reward == null)
        {
            feedback.RewardLines.Add(victory ? "奖励：目标已控制。" : "未获得：目标奖励。");
            return;
        }

        if (!victory)
        {
            feedback.RewardLines.Add($"未获得：{reward.DisplayName}");
            return;
        }

        if (rewardAlreadyClaimed)
        {
            feedback.RewardLines.Add($"奖励已领取：{reward.DisplayName}");
            return;
        }

        if (!string.IsNullOrWhiteSpace(reward.UnlockText))
        {
            feedback.RewardLines.Add(reward.UnlockText);
        }

        foreach (StrategicResourceAmount amount in reward.VictoryResourceRewards)
        {
            if (amount.Amount <= 0 || string.IsNullOrWhiteSpace(amount.ResourceId))
            {
                continue;
            }

            _definitions.Resources.TryGetValue(amount.ResourceId, out StrategicResourceDefinition resource);
            feedback.RewardLines.Add($"获得：{resource?.DisplayName ?? amount.ResourceId} +{amount.Amount}");
        }

        StrategicEquipmentSampleFeedbackRecord rewardEquipment = feedback.EquipmentSamples.FirstOrDefault(item => item.IsReward);
        if (rewardEquipment != null)
        {
            feedback.RewardLines.Add($"获得装备：{rewardEquipment.DisplayName}（{FormatEquipmentSlot(rewardEquipment.SlotKind)}）");
        }
    }

    private void ApplyBattleFeedbackRewards(
        StrategicManagementState state,
        string factionId,
        StrategicBattleFeedbackRecord feedback,
        bool victory)
    {
        if (!victory)
        {
            return;
        }

        StrategicBattleRewardDefinition reward = ResolveBattleReward(feedback.TargetLocationId);
        if (reward == null)
        {
            return;
        }

        if (state.ClaimedBattleRewardIds.Contains(reward.RewardId))
        {
            return;
        }

        foreach (StrategicResourceAmount amount in reward.VictoryResourceRewards)
        {
            state.AddResourceAmount(factionId, amount.ResourceId, amount.Amount);
        }

        AddUnique(state.ClaimedBattleRewardIds, reward.RewardId);
        foreach (StrategicEquipmentSampleFeedbackRecord equipment in feedback.EquipmentSamples.Where(item => item.IsReward))
        {
            AddUnique(state.UnlockedEquipmentSampleIds, equipment.EquipmentSampleId);
        }
    }

    private static string FormatEquipmentSlot(string slotKind)
    {
        return slotKind switch
        {
            "weapon" => "武器",
            "armor" => "护甲",
            "token" => "号令道具",
            _ => slotKind ?? ""
        };
    }

    private static void ApplyParticipantBattleResults(
        StrategicManagementState state,
        StrategicExpeditionState expedition,
        StrategicBattleResultSummary summary)
    {
        foreach (StrategicExpeditionParticipantState participant in EnumerateExpeditionParticipants(expedition))
        {
            StrategicBattleParticipantResult result = summary.Participants.First(item =>
                string.Equals(item.CorpsInstanceId ?? "", participant.CorpsInstanceId ?? "", System.StringComparison.Ordinal));
            UnlockExpeditionParticipant(state, expedition, participant, result);
        }
    }
}
