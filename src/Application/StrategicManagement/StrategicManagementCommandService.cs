using System.Linq;
using Rpg.Application.Battle;
using Rpg.Application.StrategicBattleBridge;
using Rpg.Definitions.StrategicManagement;
using Rpg.Domain.StrategicManagement;
using Rpg.Infrastructure.Logging;

namespace Rpg.Application.StrategicManagement;

public sealed class StrategicManagementCommandService
{
    private readonly StrategicManagementDefinitionSet _definitions;
    private readonly StrategicManagementRules _rules;

    public StrategicManagementCommandService(
        StrategicManagementDefinitionSet definitions,
        StrategicManagementRules rules)
    {
        _definitions = definitions ?? new StrategicManagementDefinitionSet();
        _rules = rules ?? new StrategicManagementRules(_definitions);
    }

    public StrategicCommandResult AddResource(
        StrategicManagementState state,
        string factionId,
        string resourceId,
        int amount)
    {
        if (state == null || string.IsNullOrWhiteSpace(factionId) || string.IsNullOrWhiteSpace(resourceId))
        {
            return Reject("AddResource", factionId, StrategicFailureReasons.MissingDefinitions);
        }

        state.AddResourceAmount(factionId, resourceId, amount);
        StrategicCommandResult result = StrategicCommandResult.Ok($"{factionId}:{resourceId}");
        result.Events.Add(Event("StrategicResourceChanged", factionId, ("resource", resourceId), ("amount", amount.ToString())));
        Accept("AddResource", factionId, result);
        return result;
    }

    public StrategicCommandResult OccupyLocation(
        StrategicManagementState state,
        string locationId,
        string factionId)
    {
        if (state == null || !state.Locations.TryGetValue(locationId ?? "", out StrategicLocationState location))
        {
            return Reject("OccupyLocation", locationId, StrategicFailureReasons.MissingLocation);
        }

        location.OwnerFactionId = factionId ?? "";
        location.ControlState = string.Equals(factionId, StrategicManagementIds.FactionPlayer, System.StringComparison.Ordinal)
            ? StrategicLocationControlState.PlayerHeld
            : StrategicLocationControlState.EnemyHeld;
        StrategicCommandResult result = StrategicCommandResult.Ok(location.LocationId);
        result.Events.Add(Event("StrategicLocationOccupied", location.LocationId, ("owner", location.OwnerFactionId)));
        Accept("OccupyLocation", location.LocationId, result);
        return result;
    }

    public StrategicCommandResult LoseLocation(
        StrategicManagementState state,
        string locationId,
        string newOwnerFactionId)
    {
        return OccupyLocation(state, locationId, newOwnerFactionId);
    }

    public StrategicCommandResult SettleLocationProduction(
        StrategicManagementState state,
        string locationId,
        string factionId,
        int elapsedPulses)
    {
        string failureReason = _rules.GetLocationProductionFailureReason(state, locationId, factionId, elapsedPulses);
        if (!string.IsNullOrWhiteSpace(failureReason))
        {
            return Reject("SettleLocationProduction", locationId, failureReason);
        }

        StrategicLocationState location = state.Locations[locationId];
        System.Collections.Generic.IReadOnlyList<StrategicResourceAmount> production =
            _rules.GetLocationProduction(state, location.LocationId, factionId, elapsedPulses);
        foreach (StrategicResourceAmount amount in production)
        {
            state.AddResourceAmount(factionId, amount.ResourceId, amount.Amount);
        }

        StrategicCommandResult result = StrategicCommandResult.Ok(location.LocationId);
        foreach (StrategicResourceAmount amount in production)
        {
            result.ChangedFactIds.Add($"{factionId}:{amount.ResourceId}");
        }

        result.Events.Add(Event(
            "StrategicLocationProductionSettled",
            location.LocationId,
            ("faction", factionId ?? ""),
            ("elapsedPulses", elapsedPulses.ToString()),
            ("resources", FormatResourceAmounts(production))));
        Accept("SettleLocationProduction", location.LocationId, result);
        return result;
    }

    public StrategicCommandResult SettleElapsedWorldTime(
        StrategicManagementState state,
        string factionId,
        int elapsedPulses)
    {
        if (state == null || string.IsNullOrWhiteSpace(factionId))
        {
            return Reject("SettleElapsedWorldTime", factionId, StrategicFailureReasons.MissingDefinitions);
        }

        if (elapsedPulses <= 0)
        {
            return Reject("SettleElapsedWorldTime", factionId, StrategicFailureReasons.InvalidElapsedWorldTimePulses);
        }

        int previousPulses = state.ElapsedWorldTimePulses;
        StrategicCommandResult result = StrategicCommandResult.Ok("world_time");
        result.Events.Add(Event(
            "StrategicWorldTimeSettled",
            factionId,
            ("faction", factionId),
            ("previousPulses", previousPulses.ToString()),
            ("newPulses", (previousPulses + elapsedPulses).ToString()),
            ("elapsedPulses", elapsedPulses.ToString())));

        // Elapsed world-map time is command-driven: Godot process callbacks,
        // management UI refreshes, and legacy world ticks must not mutate this state.
        foreach (StrategicLocationState location in GetControlledProducingLocations(state, factionId))
        {
            System.Collections.Generic.IReadOnlyList<StrategicResourceAmount> production =
                _rules.GetLocationProduction(state, location.LocationId, factionId, elapsedPulses);
            if (production.Count == 0)
            {
                continue;
            }

            foreach (StrategicResourceAmount amount in production)
            {
                state.AddResourceAmount(factionId, amount.ResourceId, amount.Amount);
                AddUnique(result.ChangedFactIds, $"{factionId}:{amount.ResourceId}");
            }

            result.Events.Add(Event(
                "StrategicLocationProductionSettled",
                location.LocationId,
                ("faction", factionId),
                ("elapsedPulses", elapsedPulses.ToString()),
                ("resources", FormatResourceAmounts(production))));
        }

        state.ElapsedWorldTimePulses = previousPulses + elapsedPulses;
        Accept("SettleElapsedWorldTime", factionId, result);
        return result;
    }

    public StrategicCommandResult BuildFacility(
        StrategicManagementState state,
        string cityId,
        string facilityDefinitionId)
    {
        string failureReason = _rules.GetFacilityBuildFailureReason(state, cityId, facilityDefinitionId);
        if (!string.IsNullOrWhiteSpace(failureReason))
        {
            return Reject("BuildFacility", cityId, failureReason);
        }

        StrategicCityState city = state.Cities[cityId];
        StrategicLocationState location = state.Locations[city.LocationId];
        StrategicFacilityDefinition facility = _definitions.Facilities[facilityDefinitionId];
        state.Spend(location.OwnerFactionId, facility.BuildCost);
        string instanceId = $"{city.LocationId}:facility:{city.Facilities.Count + 1:00}:{facility.FacilityDefinitionId}";
        city.Facilities.Add(new StrategicFacilityInstanceState
        {
            FacilityInstanceId = instanceId,
            FacilityDefinitionId = facility.FacilityDefinitionId
        });

        StrategicCommandResult result = StrategicCommandResult.Ok(city.LocationId, instanceId);
        result.CreatedEntityId = instanceId;
        result.Events.Add(Event("StrategicFacilityBuilt", city.LocationId, ("facility", facility.FacilityDefinitionId)));
        Accept("BuildFacility", city.LocationId, result);
        return result;
    }

    public StrategicCommandResult CreateCorps(
        StrategicManagementState state,
        string cityId,
        string corpsDefinitionId)
    {
        string failureReason = _rules.GetCorpsCreationFailureReason(state, cityId, corpsDefinitionId);
        if (!string.IsNullOrWhiteSpace(failureReason))
        {
            return Reject("CreateCorps", cityId, failureReason);
        }

        StrategicCityState city = state.Cities[cityId];
        StrategicLocationState location = state.Locations[city.LocationId];
        StrategicCorpsDefinition definition = _definitions.Corps[corpsDefinitionId];
        state.Spend(location.OwnerFactionId, definition.CreationCost);
        string corpsInstanceId = state.AllocateCorpsInstanceId();
        state.CorpsInstances[corpsInstanceId] = new StrategicCorpsInstanceState
        {
            CorpsInstanceId = corpsInstanceId,
            CorpsDefinitionId = definition.CorpsDefinitionId,
            HomeCityId = city.LocationId,
            FactionId = location.OwnerFactionId,
            Strength = 100,
            Level = 1,
            EquipmentLevel = 0,
            Experience = 0,
            Status = StrategicCorpsInstanceStatus.Garrisoned
        };

        StrategicCommandResult result = StrategicCommandResult.Ok(city.LocationId, corpsInstanceId);
        result.CreatedEntityId = corpsInstanceId;
        result.Events.Add(Event("StrategicCorpsCreated", corpsInstanceId, ("city", city.LocationId), ("corps", definition.CorpsDefinitionId)));
        Accept("CreateCorps", corpsInstanceId, result);
        return result;
    }

    public StrategicCommandResult AssignCorpsToHero(
        StrategicManagementState state,
        string heroId,
        string corpsInstanceId)
    {
        string failureReason = GetAssignmentFailureReason(state, heroId, corpsInstanceId);
        if (!string.IsNullOrWhiteSpace(failureReason))
        {
            return Reject("AssignCorpsToHero", heroId, failureReason);
        }

        StrategicHeroState hero = state.Heroes[heroId];
        StrategicCorpsInstanceState corps = state.CorpsInstances[corpsInstanceId];
        hero.AssignedCorpsInstanceId = corps.CorpsInstanceId;
        corps.AssignedHeroId = hero.HeroId;
        corps.Status = StrategicCorpsInstanceStatus.AssignedToHero;
        StrategicHeroCorpsAptitudeGrade aptitude = _rules.EvaluateHeroCorpsAptitude(state, hero.HeroId, corps.CorpsDefinitionId);

        StrategicCommandResult result = StrategicCommandResult.Ok(hero.HeroId, corps.CorpsInstanceId);
        result.AptitudeGrade = aptitude;
        result.Events.Add(Event("StrategicCorpsAssignedToHero", hero.HeroId, ("corps", corps.CorpsInstanceId), ("aptitude", aptitude.ToString())));
        Accept("AssignCorpsToHero", hero.HeroId, result);
        return result;
    }

    public StrategicCommandResult UnassignCorpsFromHero(
        StrategicManagementState state,
        string heroId)
    {
        if (state == null || !state.Heroes.TryGetValue(heroId ?? "", out StrategicHeroState hero))
        {
            return Reject("UnassignCorpsFromHero", heroId, StrategicFailureReasons.MissingHero);
        }

        if (string.IsNullOrWhiteSpace(hero.AssignedCorpsInstanceId))
        {
            return StrategicCommandResult.Ok(hero.HeroId);
        }

        string corpsInstanceId = hero.AssignedCorpsInstanceId;
        hero.AssignedCorpsInstanceId = "";
        if (state.CorpsInstances.TryGetValue(corpsInstanceId, out StrategicCorpsInstanceState corps))
        {
            corps.AssignedHeroId = "";
            corps.Status = StrategicCorpsInstanceStatus.Garrisoned;
        }

        StrategicCommandResult result = StrategicCommandResult.Ok(hero.HeroId, corpsInstanceId);
        result.Events.Add(Event("StrategicCorpsUnassignedFromHero", hero.HeroId, ("corps", corpsInstanceId)));
        Accept("UnassignCorpsFromHero", hero.HeroId, result);
        return result;
    }

    public StrategicCommandResult CreateExpedition(
        StrategicManagementState state,
        string sourceLocationId,
        string targetLocationId,
        StrategicExpeditionIntent intent,
        string heroId)
    {
        return CreateExpedition(
            state,
            sourceLocationId,
            targetLocationId,
            intent,
            string.IsNullOrWhiteSpace(heroId) ? System.Array.Empty<string>() : new[] { heroId });
    }

    public StrategicCommandResult CreateExpedition(
        StrategicManagementState state,
        string sourceLocationId,
        string targetLocationId,
        StrategicExpeditionIntent intent,
        System.Collections.Generic.IReadOnlyCollection<string> heroIds)
    {
        string failureReason = _rules.GetExpeditionCreationFailureReason(
            state,
            sourceLocationId,
            targetLocationId,
            intent,
            heroIds);
        if (!string.IsNullOrWhiteSpace(failureReason))
        {
            return Reject("CreateExpedition", string.Join(",", heroIds ?? System.Array.Empty<string>()), failureReason);
        }

        System.Collections.Generic.List<(StrategicHeroState Hero, StrategicCorpsInstanceState Corps)> participants =
            BuildExpeditionParticipants(state, heroIds);
        StrategicHeroState leadHero = participants[0].Hero;
        StrategicCorpsInstanceState leadCorps = participants[0].Corps;
        string expeditionId = state.AllocateExpeditionId();
        StrategicExpeditionState expedition = new()
        {
            ExpeditionId = expeditionId,
            FactionId = leadHero.FactionId,
            SourceLocationId = sourceLocationId ?? "",
            TargetLocationId = targetLocationId ?? "",
            Intent = intent,
            HeroId = leadHero.HeroId,
            CorpsInstanceId = leadCorps.CorpsInstanceId,
            Status = StrategicExpeditionStatus.Moving,
            CreatedElapsedWorldTimePulses = state.ElapsedWorldTimePulses
        };
        foreach ((StrategicHeroState hero, StrategicCorpsInstanceState corps) in participants)
        {
            expedition.Participants.Add(new StrategicExpeditionParticipantState
            {
                HeroId = hero.HeroId,
                CorpsInstanceId = corps.CorpsInstanceId
            });
        }

        state.Expeditions[expedition.ExpeditionId] = expedition;
        foreach ((StrategicHeroState hero, StrategicCorpsInstanceState corps) in participants)
        {
            hero.CurrentExpeditionId = expedition.ExpeditionId;
            corps.CurrentExpeditionId = expedition.ExpeditionId;
            corps.Status = StrategicCorpsInstanceStatus.Expedition;
        }

        StrategicCommandResult result = StrategicCommandResult.Ok(
            participants.SelectMany(item => new[] { item.Hero.HeroId, item.Corps.CorpsInstanceId })
                .Append(expedition.ExpeditionId)
                .ToArray());
        result.CreatedEntityId = expedition.ExpeditionId;
        result.Events.Add(Event(
            "StrategicExpeditionCreated",
            expedition.ExpeditionId,
            ("participants", FormatExpeditionParticipants(expedition)),
            ("source", expedition.SourceLocationId),
            ("target", expedition.TargetLocationId),
            ("intent", expedition.Intent.ToString())));
        Accept("CreateExpedition", expedition.ExpeditionId, result);
        return result;
    }

    public StrategicCommandResult CancelExpedition(
        StrategicManagementState state,
        string expeditionId,
        string reason = "")
    {
        if (state == null || !state.Expeditions.TryGetValue(expeditionId ?? "", out StrategicExpeditionState expedition))
        {
            return Reject("CancelExpedition", expeditionId, StrategicFailureReasons.MissingExpedition);
        }

        expedition.Status = StrategicExpeditionStatus.Cancelled;
        foreach (StrategicExpeditionParticipantState participant in EnumerateExpeditionParticipants(expedition))
        {
            UnlockExpeditionParticipant(state, expedition, participant, null);
        }

        StrategicCommandResult result = StrategicCommandResult.Ok(expedition.ExpeditionId);
        result.Events.Add(Event(
            "StrategicExpeditionCancelled",
            expedition.ExpeditionId,
            ("reason", reason ?? "")));
        Accept("CancelExpedition", expedition.ExpeditionId, result);
        return result;
    }

    public StrategicCommandResult RetargetExpedition(
        StrategicManagementState state,
        string expeditionId,
        string targetLocationId,
        StrategicExpeditionIntent intent)
    {
        string failureReason = _rules.GetExpeditionRetargetFailureReason(
            state,
            expeditionId,
            targetLocationId,
            intent);
        if (!string.IsNullOrWhiteSpace(failureReason))
        {
            return Reject("RetargetExpedition", expeditionId, failureReason);
        }

        StrategicExpeditionState expedition = state.Expeditions[expeditionId ?? ""];
        expedition.TargetLocationId = intent == StrategicExpeditionIntent.MoveToPosition
            ? ""
            : targetLocationId ?? "";
        expedition.Intent = intent;
        expedition.Status = StrategicExpeditionStatus.Moving;

        StrategicCommandResult result = StrategicCommandResult.Ok(
            expedition.ExpeditionId,
            expedition.TargetLocationId,
            expedition.Intent.ToString());
        result.Events.Add(Event(
            "StrategicExpeditionRetargeted",
            expedition.ExpeditionId,
            ("target", expedition.TargetLocationId),
            ("intent", expedition.Intent.ToString())));
        Accept("RetargetExpedition", expedition.ExpeditionId, result);
        return result;
    }

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

    private static string GetAssignmentFailureReason(
        StrategicManagementState state,
        string heroId,
        string corpsInstanceId)
    {
        if (state == null || !state.Heroes.TryGetValue(heroId ?? "", out StrategicHeroState hero))
        {
            return StrategicFailureReasons.MissingHero;
        }

        if (!state.CorpsInstances.TryGetValue(corpsInstanceId ?? "", out StrategicCorpsInstanceState corps))
        {
            return StrategicFailureReasons.MissingCorpsInstance;
        }

        if (!string.Equals(hero.FactionId, corps.FactionId, System.StringComparison.Ordinal))
        {
            return StrategicFailureReasons.FactionMismatch;
        }

        if (!string.IsNullOrWhiteSpace(hero.AssignedCorpsInstanceId))
        {
            return StrategicFailureReasons.HeroAlreadyAssigned;
        }

        return string.IsNullOrWhiteSpace(corps.AssignedHeroId)
            ? ""
            : StrategicFailureReasons.CorpsAlreadyAssigned;
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
            FailureReasonText = victory ? "" : "阵线被白骨原守军压散，重整编制后再进攻。",
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
            return $"{heroDisplayName}：阵线被拖散了。先重整编制，再压住白骨原侧翼。";
        }

        if (string.Equals(heroDefinitionId, StrategicManagementIds.HeroBeastTamer, System.StringComparison.Ordinal))
        {
            return $"{heroDisplayName}：这里的兽痕还热，给我时间就能驯出第一批战兽。";
        }

        return $"{heroDisplayName}：白骨原的突破口已经打开。";
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

    private static void UnlockExpeditionParticipant(
        StrategicManagementState state,
        StrategicExpeditionState expedition,
        StrategicExpeditionParticipantState expeditionParticipant,
        StrategicBattleParticipantResult participant)
    {
        if (state.Heroes.TryGetValue(expeditionParticipant?.HeroId ?? "", out StrategicHeroState hero) &&
            string.Equals(hero.CurrentExpeditionId, expedition.ExpeditionId, System.StringComparison.Ordinal))
        {
            hero.CurrentExpeditionId = "";
        }

        if (!state.CorpsInstances.TryGetValue(expeditionParticipant?.CorpsInstanceId ?? "", out StrategicCorpsInstanceState corps))
        {
            return;
        }

        int remainingStrength = participant == null
            ? corps.Strength
            : System.Math.Clamp(participant.RemainingCorpsStrength, 0, 100);
        corps.Strength = remainingStrength;
        if (string.Equals(corps.CurrentExpeditionId, expedition.ExpeditionId, System.StringComparison.Ordinal))
        {
            corps.CurrentExpeditionId = "";
        }

        corps.Status = remainingStrength <= 0
            ? StrategicCorpsInstanceStatus.Routed
            : string.IsNullOrWhiteSpace(corps.AssignedHeroId)
                ? StrategicCorpsInstanceStatus.Garrisoned
                : StrategicCorpsInstanceStatus.AssignedToHero;
    }

    private static System.Collections.Generic.List<(StrategicHeroState Hero, StrategicCorpsInstanceState Corps)> BuildExpeditionParticipants(
        StrategicManagementState state,
        System.Collections.Generic.IReadOnlyCollection<string> heroIds)
    {
        System.Collections.Generic.List<(StrategicHeroState Hero, StrategicCorpsInstanceState Corps)> participants = new();
        foreach (string heroId in NormalizeHeroIds(heroIds))
        {
            StrategicHeroState hero = state.Heroes[heroId];
            StrategicCorpsInstanceState corps = state.CorpsInstances[hero.AssignedCorpsInstanceId];
            participants.Add((hero, corps));
        }

        return participants;
    }

    private static System.Collections.Generic.IReadOnlyList<StrategicExpeditionParticipantState> EnumerateExpeditionParticipants(
        StrategicExpeditionState expedition)
    {
        if (expedition?.Participants?.Count > 0)
        {
            return expedition.Participants;
        }

        if (expedition == null ||
            string.IsNullOrWhiteSpace(expedition.HeroId) ||
            string.IsNullOrWhiteSpace(expedition.CorpsInstanceId))
        {
            return System.Array.Empty<StrategicExpeditionParticipantState>();
        }

        return new[]
        {
            new StrategicExpeditionParticipantState
            {
                HeroId = expedition.HeroId,
                CorpsInstanceId = expedition.CorpsInstanceId
            }
        };
    }

    private static string[] NormalizeHeroIds(System.Collections.Generic.IReadOnlyCollection<string> heroIds)
    {
        return (heroIds ?? System.Array.Empty<string>())
            .Where(heroId => !string.IsNullOrWhiteSpace(heroId))
            .Distinct(System.StringComparer.Ordinal)
            .ToArray();
    }

    private static string FormatExpeditionParticipants(StrategicExpeditionState expedition)
    {
        return string.Join(
            ",",
            EnumerateExpeditionParticipants(expedition)
                .Select(item => $"{item.HeroId}:{item.CorpsInstanceId}"));
    }

    private System.Collections.Generic.IReadOnlyList<StrategicLocationState> GetControlledProducingLocations(
        StrategicManagementState state,
        string factionId)
    {
        if (state == null || string.IsNullOrWhiteSpace(factionId))
        {
            return System.Array.Empty<StrategicLocationState>();
        }

        return state.Locations.Values
            .Where(location =>
                string.Equals(location.OwnerFactionId, factionId, System.StringComparison.Ordinal) &&
                location.ControlState == StrategicLocationControlState.PlayerHeld &&
                _definitions.Locations.TryGetValue(location.LocationId, out StrategicLocationDefinition definition) &&
                definition.ProductionPerWorldTimePulse.Any(item => item.Amount > 0 && !string.IsNullOrWhiteSpace(item.ResourceId)))
            .OrderBy(location => location.LocationId)
            .ToList();
    }

    private static StrategicEvent Event(
        string kind,
        string targetId,
        params (string Key, string Value)[] payload)
    {
        StrategicEvent strategicEvent = new()
        {
            Kind = kind
        };
        if (!string.IsNullOrWhiteSpace(targetId))
        {
            strategicEvent.TargetIds.Add(targetId);
        }

        foreach ((string key, string value) in payload ?? System.Array.Empty<(string, string)>())
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                strategicEvent.Payload[key] = value ?? "";
            }
        }

        return strategicEvent;
    }

    private static string FormatResourceAmounts(System.Collections.Generic.IReadOnlyList<StrategicResourceAmount> amounts)
    {
        return string.Join(
            ",",
            amounts.Select(item => $"{item.ResourceId}:{item.Amount}"));
    }

    private static void AddUnique(System.Collections.Generic.ICollection<string> values, string value)
    {
        if (!string.IsNullOrWhiteSpace(value) && !values.Contains(value))
        {
            values.Add(value);
        }
    }

    private static StrategicCommandResult Reject(string commandKind, string targetId, string failureReason)
    {
        GameLog.Warn(
            nameof(StrategicManagementCommandService),
            $"StrategicCommandRejected command={commandKind} target={targetId ?? ""} reason={failureReason}");
        return StrategicCommandResult.Failed(failureReason);
    }

    private static void Accept(string commandKind, string targetId, StrategicCommandResult result)
    {
        GameLog.Info(
            nameof(StrategicManagementCommandService),
            $"StrategicCommandAccepted command={commandKind} target={targetId ?? ""} changed={string.Join(",", result.ChangedFactIds)}");
    }
}
