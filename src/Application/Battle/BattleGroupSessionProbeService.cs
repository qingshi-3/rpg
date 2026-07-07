using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle.Adapters;
using Rpg.Application.Battle.Snapshots;
using Rpg.Domain.BattleGroups;
using Rpg.Domain.Corps;
using Rpg.Domain.Heroes;
using Rpg.Infrastructure.Logging;
using Rpg.Runtime.Battle.Tactics;

namespace Rpg.Application.Battle;

public sealed class BattleGroupSessionProbeResult
{
	public bool Success { get; init; }
	public string FailureReason { get; init; } = "";
	public BattleStartSnapshot Snapshot { get; init; } = new();
	public BattleGroupBattleFlowResult FlowResult { get; init; } = new();
}

public sealed class BattleGroupSessionProbeService
{
	private readonly LegacyBattleStartSnapshotAdapter _snapshotAdapter = new();
	private readonly BattleGroupBattleFlowService _flowService = new();

	// Phase-2 migration uses this as a diagnostic side channel: it proves live
	// launch data can enter the target architecture without touching legacy handoff.
	public BattleGroupSessionProbeResult Probe(BattleStartRequest request)
	{
		BattleGroupSessionProbeResult prepared = PrepareSnapshot(request);
		if (!prepared.Success)
		{
			return prepared;
		}

		BattleStartSnapshot snapshot = prepared.Snapshot;
		BattleGroupBattleFlowResult flowResult = _flowService.RunSnapshot(snapshot);
		if (!flowResult.SettlementPlan.Accepted)
		{
			string reason = string.IsNullOrWhiteSpace(flowResult.SettlementPlan.RejectionReason)
				? "battle_group_probe_rejected"
				: flowResult.SettlementPlan.RejectionReason;
			GameLog.Warn(
				nameof(BattleGroupSessionProbeService),
				$"Battle group session probe rejected request={request.RequestId} snapshot={snapshot.SnapshotId} reason={reason}");
			return Reject(reason, snapshot, flowResult);
		}

		GameLog.Info(
			nameof(BattleGroupSessionProbeService),
			$"Battle group session probe succeeded request={request.RequestId} snapshot={snapshot.SnapshotId} groups={snapshot.BattleGroups.Count}");
		return new BattleGroupSessionProbeResult
		{
			Success = true,
			Snapshot = snapshot,
			FlowResult = flowResult
		};
	}

	public BattleGroupSessionProbeResult PrepareSnapshot(BattleStartRequest request)
	{
		if (request == null)
		{
			return Reject("missing_battle_request", null, null);
		}

		ProbeSeed seed = BuildSeed(request);
		BattleStartSnapshot snapshot = _snapshotAdapter.ToSnapshot(
			request,
			seed.Groups,
			seed.Heroes,
			seed.Corps);
		ApplyProbeMetadata(snapshot, seed);
		_snapshotAdapter.RecompileSkillDefinitions(snapshot);
		ApplyBattleEntryTacticalSeeds(snapshot, request, seed);
		return new BattleGroupSessionProbeResult
		{
			Success = true,
			Snapshot = snapshot
		};
	}

	private static BattleGroupSessionProbeResult Reject(
		string reason,
		BattleStartSnapshot snapshot,
		BattleGroupBattleFlowResult flowResult)
	{
		return new BattleGroupSessionProbeResult
		{
			Success = false,
			FailureReason = reason ?? "",
			Snapshot = snapshot ?? new BattleStartSnapshot(),
			FlowResult = flowResult ?? new BattleGroupBattleFlowResult()
		};
	}

	private static ProbeSeed BuildSeed(BattleStartRequest request)
	{
		ProbeSeed seed = new();
		string sourceLocationId = !string.IsNullOrWhiteSpace(request.SourceSiteId)
			? request.SourceSiteId
			: request.TargetSiteId ?? "";
		int groupIndex = 0;
		AddForces(seed, request.PlayerForces, "player", sourceLocationId, request.InitialCorpsCommandId, request, BattlePlanSide.Player, ref groupIndex);
		AddForces(seed, request.EnemyForces, "enemy", request.TargetSiteId ?? sourceLocationId, "", request, BattlePlanSide.Enemy, ref groupIndex);

		return seed;
	}

	private static void AddForces(
		ProbeSeed seed,
		IEnumerable<BattleForceRequest> forces,
		string fallbackFactionId,
		string sourceLocationId,
		string initialCorpsCommandId,
		BattleStartRequest request,
		BattlePlanSide planSide,
		ref int groupIndex)
	{
		foreach (BattleForceRequest force in forces ?? Enumerable.Empty<BattleForceRequest>())
		{
			int count = force?.Count ?? 0;
			for (int index = 0; index < count; index++)
			{
				string unitDefinitionId = string.IsNullOrWhiteSpace(force.UnitDefinitionId)
					? "legacy_unknown_corps"
					: force.UnitDefinitionId;
				string forceId = string.IsNullOrWhiteSpace(force.ForceId)
					? $"force_{groupIndex}"
					: force.ForceId;
				string sourceForceId = string.IsNullOrWhiteSpace(force.StrategicParticipantId)
					? forceId
					: force.StrategicParticipantId;
				string commanderGroupId = string.IsNullOrWhiteSpace(force.StrategicParticipantId)
					? BattleCommanderGroupIdentity.BuildProbeCommanderGroupId(force, forceId)
					: force.StrategicParticipantId;
				string factionId = string.IsNullOrWhiteSpace(force.FactionId)
					? fallbackFactionId
					: force.FactionId;
				BattleForcePlacementRequest placement = index < force.PreferredPlacements.Count
					? force.PreferredPlacements[index]
					: null;
				string heroId = string.IsNullOrWhiteSpace(force.StrategicHeroId)
					? $"probe_hero_{groupIndex}"
					: force.StrategicHeroId;
				string corpsId = string.IsNullOrWhiteSpace(force.StrategicCorpsInstanceId)
					? $"probe_corps_{groupIndex}"
					: force.StrategicCorpsInstanceId;
				string strategicSourceLocationId = string.IsNullOrWhiteSpace(force.StrategicSourceLocationId)
					? sourceLocationId
					: force.StrategicSourceLocationId;
				string heroBattleUnitId = string.IsNullOrWhiteSpace(force.StrategicHeroBattleUnitId)
					? ""
					: force.StrategicHeroBattleUnitId;
				string corpsBattleUnitId = string.IsNullOrWhiteSpace(force.StrategicCorpsBattleUnitId)
					? unitDefinitionId
					: force.StrategicCorpsBattleUnitId;
				BattleGroupPlanSnapshot groupPlan = ResolveBattleGroupPlan(request, force, planSide);
				BattleTacticalIntentPlanSnapshot tacticalIntentPlan = ResolveBattleTacticalIntentPlan(request, force, planSide);

				BattleGroupState group = new()
				{
					BattleGroupId = $"probe_group_{forceId}_{index}",
					HeroId = heroId,
					CorpsId = corpsId,
					CurrentLocationId = strategicSourceLocationId,
					Status = BattleGroupStatus.Stationed
				};
				HeroState hero = new()
				{
					HeroId = heroId,
					HeroDefinitionId = string.IsNullOrWhiteSpace(force.StrategicHeroDefinitionId)
						? $"probe_hero_definition_{groupIndex}"
						: force.StrategicHeroDefinitionId,
					OwnerFactionId = factionId,
					Level = 1
				};
				CorpsState corps = new()
				{
					CorpsId = corpsId,
					CorpsDefinitionId = string.IsNullOrWhiteSpace(force.StrategicCorpsDefinitionId)
						? unitDefinitionId
						: force.StrategicCorpsDefinitionId,
					Level = 1,
					CorpsStrength = force.StrategicPreBattleCorpsStrength > 0
						? force.StrategicPreBattleCorpsStrength
						: CorpsStrengthPolicy.MaxStrength
				};

				seed.Groups.Add(group);
				seed.GroupMetadata[group.BattleGroupId] = new ProbeGroupMetadata
				{
					FactionId = factionId,
					SourceForceId = sourceForceId,
					RuntimeCommanderGroupId = commanderGroupId,
					HeroBattleUnitId = heroBattleUnitId,
					CorpsBattleUnitId = corpsBattleUnitId,
					PlanSide = planSide,
					CellX = placement?.CellX ?? ResolveFallbackCellX(fallbackFactionId),
					CellY = placement?.CellY ?? index,
					CellHeight = placement?.CellHeight ?? 0,
					FootprintWidth = force.FootprintWidth,
					FootprintHeight = force.FootprintHeight,
					MaxHitPoints = force.MaxHitPoints,
					AttackDamage = force.AttackDamage,
					AttackRange = force.AttackRange,
					AttackSpeed = force.AttackSpeed,
					MoveStepSeconds = force.MoveStepSeconds,
					AttackActionSeconds = force.AttackActionSeconds,
					AttackImpactDelaySeconds = force.AttackImpactDelaySeconds,
					InitialCorpsCommandId = initialCorpsCommandId ?? "",
					Plan = CopyPlanForGroup(commanderGroupId, groupPlan),
					TacticalIntentPlan = CopyTacticalIntentPlan(tacticalIntentPlan)
				};
				seed.Heroes[hero.HeroId] = hero;
				seed.Corps[corps.CorpsId] = corps;
				groupIndex++;
			}
		}
	}

	private static void ApplyBattleEntryTacticalSeeds(
		BattleStartSnapshot snapshot,
		BattleStartRequest request,
		ProbeSeed seed)
	{
		if (snapshot?.BattleGroups == null)
		{
			return;
		}

		foreach (BattleGroupSnapshot group in snapshot.BattleGroups)
		{
			if (group == null ||
				!seed.GroupMetadata.TryGetValue(group.BattleGroupId ?? "", out ProbeGroupMetadata metadata))
			{
				continue;
			}

			if (metadata.PlanSide == BattlePlanSide.Player)
			{
				group.TacticalMode = BattleGroupTacticalMode.PlayerCommanded;
				if (group.InitialTacticalRegions.Count == 0)
				{
					BattleTacticalRegionSnapshot playerCommandRegion = BuildPlayerCommandRegionSeed(
						group,
						snapshot.ObjectiveZones);
					if (playerCommandRegion != null)
					{
						group.InitialTacticalRegions.Add(playerCommandRegion);
					}
				}

				continue;
			}

			group.InitialTacticalRegions.Clear();

			// Battle entry only writes immutable seed facts; Runtime owns all later
			// mutation of enemy regions and player-commanded intent remains untouched.
			if (IsHoldPosture(group))
			{
				group.TacticalMode = BattleGroupTacticalMode.EnemyHoldDefense;
				group.InitialTacticalRegions.Add(BuildHoldRegionSeed(group));
				continue;
			}

			if (MatchesFaction(group.FactionId, request?.AttackerFactionId))
			{
				group.TacticalMode = BattleGroupTacticalMode.EnemyOffense;
				BattleTacticalRegionSnapshot fixedRegion = SelectFixedTargetRegionSeed(
					group,
					snapshot.BattleGroups,
					snapshot.ObjectiveZones,
					FixedTargetCandidateRole.Defensive);
				if (fixedRegion != null)
				{
					group.InitialTacticalRegions.Add(fixedRegion);
				}

				continue;
			}

			if (MatchesFaction(group.FactionId, request?.DefenderFactionId))
			{
				group.TacticalMode = BattleGroupTacticalMode.EnemyActiveDefense;
				BattleTacticalRegionSnapshot fixedRegion = SelectFixedTargetRegionSeed(
					group,
					snapshot.BattleGroups,
					snapshot.ObjectiveZones,
					FixedTargetCandidateRole.Offensive);
				if (fixedRegion != null)
				{
					group.InitialTacticalRegions.Add(fixedRegion);
				}
			}
		}
	}

	private static bool MatchesFaction(string actual, string expected)
	{
		return !string.IsNullOrWhiteSpace(expected) &&
			string.Equals(actual ?? "", expected, StringComparison.Ordinal);
	}

	private static bool IsHoldPosture(BattleGroupSnapshot group)
	{
		return group?.Plan?.EngagementRule == BattleEngagementRule.Hold ||
			IsHoldLineCommand(group?.InitialCorpsCommandId);
	}

	private static bool IsHoldLineCommand(string commandId)
	{
		string normalized = commandId ?? "";
		return normalized.Equals("HoldLine", StringComparison.OrdinalIgnoreCase) ||
			normalized.Equals("hold_line", StringComparison.OrdinalIgnoreCase) ||
			normalized.Equals("hold-line", StringComparison.OrdinalIgnoreCase);
	}

	private static BattleTacticalRegionSnapshot BuildHoldRegionSeed(BattleGroupSnapshot group)
	{
		string ownerGroupId = BattleCommanderGroupIdentity.Resolve(group);
		string regionId = $"{ownerGroupId}:hold_seed";
		return new BattleTacticalRegionSnapshot
		{
			RegionId = regionId,
			OwnerBattleGroupId = ownerGroupId,
			Kind = BattleTacticalRegionKind.Hold,
			SourceRegionId = string.IsNullOrWhiteSpace(group.Plan?.ObjectiveZoneId)
				? regionId
				: group.Plan.ObjectiveZoneId,
			ReasonCode = BattleGroupTacticalReasonCode.RegionHoldSeededPosture,
			CenterCellX = group.CellX,
			CenterCellY = group.CellY,
			CenterCellHeight = group.CellHeight,
			Width = System.Math.Max(1, group.FootprintWidth),
			Height = System.Math.Max(1, group.FootprintHeight)
		};
	}

	private static BattleTacticalRegionSnapshot BuildPlayerCommandRegionSeed(
		BattleGroupSnapshot group,
		IReadOnlyList<BattleObjectiveZoneSnapshot> objectiveZones)
	{
		BattleGroupPlanSnapshot plan = group?.Plan;
		if (plan == null)
		{
			return null;
		}

		string ownerGroupId = BattleCommanderGroupIdentity.Resolve(group);
		BattleObjectiveZoneSnapshot zone = (objectiveZones ?? Array.Empty<BattleObjectiveZoneSnapshot>())
			.FirstOrDefault(candidate => string.Equals(
				candidate?.ObjectiveZoneId ?? "",
				plan.ObjectiveZoneId ?? "",
				StringComparison.Ordinal));
		if (zone != null)
		{
			int width = System.Math.Max(1, zone.Width);
			int height = System.Math.Max(1, zone.Height);
			return new BattleTacticalRegionSnapshot
			{
				RegionId = zone.ObjectiveZoneId ?? "",
				OwnerBattleGroupId = ownerGroupId,
				Kind = BattleTacticalRegionKind.FixedTarget,
				SourceRegionId = zone.ObjectiveZoneId ?? "",
				ReasonCode = BattleGroupTacticalReasonCode.RegionAccepted,
				CenterCellX = ResolveCenterCell(zone.CellX, width),
				CenterCellY = ResolveCenterCell(zone.CellY, height),
				CenterCellHeight = zone.CellHeight,
				Width = width,
				Height = height
			};
		}

		if (!plan.HasObjectiveAnchor)
		{
			return null;
		}

		int planWidth = System.Math.Max(1, plan.ObjectiveWidth);
		int planHeight = System.Math.Max(1, plan.ObjectiveHeight);
		string regionId = string.IsNullOrWhiteSpace(plan.ObjectiveZoneId)
			? $"{ownerGroupId}:player_command:{plan.ObjectiveCellX}:{plan.ObjectiveCellY}:{plan.ObjectiveCellHeight}"
			: plan.ObjectiveZoneId;
		// Player-selected objectives must enter the tactical store as command
		// regions; otherwise autonomous fallback treats the group as uncommanded.
		return new BattleTacticalRegionSnapshot
		{
			RegionId = regionId,
			OwnerBattleGroupId = ownerGroupId,
			Kind = BattleTacticalRegionKind.FixedTarget,
			SourceRegionId = plan.ObjectiveZoneId ?? "",
			ReasonCode = BattleGroupTacticalReasonCode.RegionAccepted,
			CenterCellX = ResolveCenterCell(plan.ObjectiveCellX, planWidth),
			CenterCellY = ResolveCenterCell(plan.ObjectiveCellY, planHeight),
			CenterCellHeight = plan.ObjectiveCellHeight,
			Width = planWidth,
			Height = planHeight
		};
	}

	private static BattleTacticalRegionSnapshot SelectFixedTargetRegionSeed(
		BattleGroupSnapshot group,
		IReadOnlyList<BattleGroupSnapshot> battleGroups,
		IReadOnlyList<BattleObjectiveZoneSnapshot> objectiveZones,
		FixedTargetCandidateRole candidateRole)
	{
		List<FixedTargetCandidate> candidates = (objectiveZones ?? Array.Empty<BattleObjectiveZoneSnapshot>())
			.Where(zone => IsPlayerSideFixedTargetCandidate(zone, candidateRole))
			.Select(zone => BuildFixedTargetCandidate(group, battleGroups, zone))
			.OrderByDescending(candidate => candidate.Score)
			.ThenBy(candidate => candidate.Distance)
			.ThenBy(candidate => candidate.Zone.ObjectiveZoneId ?? "", StringComparer.Ordinal)
			.ToList();

		if (candidates.Count == 0)
		{
			return null;
		}

		FixedTargetCandidate selected = candidates[0];
		bool densityDecided = candidates.Skip(1).All(candidate => selected.OpposingAliveActorsInsideRegion > candidate.OpposingAliveActorsInsideRegion);
		int width = System.Math.Max(1, selected.Zone.Width);
		int height = System.Math.Max(1, selected.Zone.Height);
		return new BattleTacticalRegionSnapshot
		{
			RegionId = $"{BattleCommanderGroupIdentity.Resolve(group)}:fixed:{selected.Zone.ObjectiveZoneId}",
			OwnerBattleGroupId = BattleCommanderGroupIdentity.Resolve(group),
			Kind = BattleTacticalRegionKind.FixedTarget,
			SourceRegionId = selected.Zone.ObjectiveZoneId ?? "",
			ReasonCode = densityDecided
				? BattleGroupTacticalReasonCode.RegionFixedSelectedPlayerDensity
				: BattleGroupTacticalReasonCode.RegionFixedSelectedPriority,
			CenterCellX = ResolveCenterCell(selected.Zone.CellX, width),
			CenterCellY = ResolveCenterCell(selected.Zone.CellY, height),
			CenterCellHeight = selected.Zone.CellHeight,
			Width = width,
			Height = height
		};
	}

	private static int ResolveCenterCell(int minCell, int size)
	{
		return minCell + (System.Math.Max(1, size) - 1) / 2;
	}

	private static FixedTargetCandidate BuildFixedTargetCandidate(
		BattleGroupSnapshot group,
		IReadOnlyList<BattleGroupSnapshot> battleGroups,
		BattleObjectiveZoneSnapshot zone)
	{
		int density = (battleGroups ?? Array.Empty<BattleGroupSnapshot>())
			.Count(candidate => candidate != null &&
				!string.Equals(candidate.BattleGroupId, group.BattleGroupId, StringComparison.Ordinal) &&
				candidate.CorpsStrength > 0 &&
				!string.Equals(candidate.FactionId ?? "", group.FactionId ?? "", StringComparison.Ordinal) &&
				IsGroupAnchorInsideZone(candidate, zone));
		double distance = ApproximateDistanceToZoneCenter(group, zone);
		return new FixedTargetCandidate
		{
			Zone = zone,
			OpposingAliveActorsInsideRegion = density,
			Distance = distance,
			Score = density * 1000.0 + zone.Priority * 10.0 - distance
		};
	}

	private static bool IsGroupAnchorInsideZone(BattleGroupSnapshot group, BattleObjectiveZoneSnapshot zone)
	{
		int width = System.Math.Max(1, zone.Width);
		int height = System.Math.Max(1, zone.Height);
		return group.CellHeight == zone.CellHeight &&
			group.CellX >= zone.CellX &&
			group.CellX < zone.CellX + width &&
			group.CellY >= zone.CellY &&
			group.CellY < zone.CellY + height;
	}

	private static double ApproximateDistanceToZoneCenter(BattleGroupSnapshot group, BattleObjectiveZoneSnapshot zone)
	{
		double centerX = zone.CellX + (System.Math.Max(1, zone.Width) - 1) / 2.0;
		double centerY = zone.CellY + (System.Math.Max(1, zone.Height) - 1) / 2.0;
		double deltaX = group.CellX - centerX;
		double deltaY = group.CellY - centerY;
		return System.Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
	}

	private static bool IsPlayerSideFixedTargetCandidate(BattleObjectiveZoneSnapshot zone, FixedTargetCandidateRole candidateRole)
	{
		if (zone == null || string.IsNullOrWhiteSpace(zone.ObjectiveZoneId))
		{
			return false;
		}

		if (!string.Equals(zone.DeploymentSide ?? "", "Player", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		string role = zone.ObjectiveRole ?? "";
		if (role.Equals("player_deployment", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		return candidateRole == FixedTargetCandidateRole.Defensive
			? ContainsAny(role, "defensive", "defense")
			: ContainsAny(role, "offensive", "offense", "assault");
	}

	private static bool ContainsAny(string value, params string[] needles)
	{
		return needles.Any(needle => (value ?? "").IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0);
	}

	private static void ApplyProbeMetadata(BattleStartSnapshot snapshot, ProbeSeed seed)
	{
		if (snapshot?.BattleGroups == null)
		{
			return;
		}

		foreach (BattleGroupSnapshot group in snapshot.BattleGroups)
		{
			if (group == null ||
				!seed.GroupMetadata.TryGetValue(group.BattleGroupId ?? "", out ProbeGroupMetadata metadata))
			{
				continue;
			}

			// Legacy launch requests are copied into the target snapshot once, then
			// runtime and settlement consume only snapshot-owned identities.
			group.FactionId = metadata.FactionId;
			group.SourceForceId = metadata.SourceForceId;
			group.RuntimeCommanderGroupId = metadata.RuntimeCommanderGroupId;
			group.HeroBattleUnitId = metadata.HeroBattleUnitId;
			group.CorpsBattleUnitId = metadata.CorpsBattleUnitId;
			group.CellX = metadata.CellX;
			group.CellY = metadata.CellY;
			group.CellHeight = metadata.CellHeight;
			group.FootprintWidth = metadata.FootprintWidth;
			group.FootprintHeight = metadata.FootprintHeight;
			group.MaxHitPoints = metadata.MaxHitPoints;
			group.AttackDamage = metadata.AttackDamage;
			group.AttackRange = metadata.AttackRange;
			group.AttackSpeed = metadata.AttackSpeed;
			group.MoveStepSeconds = metadata.MoveStepSeconds;
			group.AttackActionSeconds = metadata.AttackActionSeconds;
			group.AttackImpactDelaySeconds = metadata.AttackImpactDelaySeconds;
			group.InitialCorpsCommandId = metadata.InitialCorpsCommandId;
			group.Plan = CopyPlanForGroup(BattleCommanderGroupIdentity.Resolve(group), metadata.Plan);
			group.TacticalIntentPlan = CopyTacticalIntentPlan(metadata.TacticalIntentPlan);
		}
	}

	private static BattleGroupPlanSnapshot ResolveBattleGroupPlan(
		BattleStartRequest request,
		BattleForceRequest force,
		BattlePlanSide side)
	{
		Dictionary<string, BattleGroupPlanSnapshot> sidePlans = side == BattlePlanSide.Enemy
			? request?.EnemyBattleGroupPlans
			: request?.PlayerBattleGroupPlans;
		BattleGroupPlanSnapshot fallbackPlan = side == BattlePlanSide.Enemy
			? request?.EnemyBattleGroupPlan
			: request?.PlayerBattleGroupPlan;

		if (sidePlans != null)
		{
			foreach (string key in ResolveBattleGroupPlanKeys(force))
			{
				if (sidePlans.TryGetValue(key, out BattleGroupPlanSnapshot plan) &&
					HasAuthoredPlan(plan))
				{
					return CopyPlanForGroup("", plan);
				}
			}
		}

		return HasAuthoredPlan(fallbackPlan) ? CopyPlanForGroup("", fallbackPlan) : null;
	}

	private static BattleTacticalIntentPlanSnapshot ResolveBattleTacticalIntentPlan(
		BattleStartRequest request,
		BattleForceRequest force,
		BattlePlanSide side)
	{
		if (side != BattlePlanSide.Enemy)
		{
			return null;
		}

		foreach (string key in ResolveBattleGroupPlanKeys(force))
		{
			if (request?.EnemyTacticalIntentPlans != null &&
				request.EnemyTacticalIntentPlans.TryGetValue(key, out BattleTacticalIntentPlanSnapshot explicitPlan) &&
				HasAuthoredTacticalIntentPlan(explicitPlan))
			{
				return CopyTacticalIntentPlan(explicitPlan, BattleTacticalIntentPlanSources.ExplicitGroup);
			}
		}

		if (HasAuthoredTacticalIntentPlan(force?.TacticalIntentPlan))
		{
			return CopyTacticalIntentPlan(force.TacticalIntentPlan, BattleTacticalIntentPlanSources.ForceDefault);
		}

		if (HasAuthoredTacticalIntentPlan(request?.EnemyTacticalIntentPlan))
		{
			return CopyTacticalIntentPlan(request.EnemyTacticalIntentPlan, BattleTacticalIntentPlanSources.ScenarioDefault);
		}

		return null;
	}

	private static IEnumerable<string> ResolveBattleGroupPlanKeys(BattleForceRequest force)
	{
		if (force == null)
		{
			yield break;
		}

		string fallbackForceId = string.IsNullOrWhiteSpace(force.ForceId)
			? force.UnitDefinitionId ?? ""
			: force.ForceId;
		foreach (string key in new[]
				 {
					 BattleCommanderGroupIdentity.BuildProbeCommanderGroupId(force, fallbackForceId),
					 BattleCommanderGroupIdentity.ResolveForceCommandKey(force, fallbackForceId),
					 force.ForceId ?? "",
					 !string.IsNullOrWhiteSpace(force.SourceKind) && !string.IsNullOrWhiteSpace(force.SourceId)
						 ? $"{force.SourceKind}:{force.SourceId}"
						 : "",
					 force.SourceId ?? "",
					 fallbackForceId
				 })
		{
			if (!string.IsNullOrWhiteSpace(key))
			{
				yield return key;
			}
		}
	}

	private static bool HasAuthoredPlan(BattleGroupPlanSnapshot plan)
	{
		return plan != null &&
			   (!string.IsNullOrWhiteSpace(plan.ObjectiveZoneId) ||
				!string.IsNullOrWhiteSpace(plan.InitialFormationId) ||
				plan.HasObjectiveAnchor ||
				plan.HasInitialDestinationBeacon ||
				plan.EngagementRule != BattleEngagementRule.AttackFirst);
	}

	private static bool HasAuthoredTacticalIntentPlan(BattleTacticalIntentPlanSnapshot plan)
	{
		return plan != null &&
			   (!string.IsNullOrWhiteSpace(plan.IntentId) ||
				!string.IsNullOrWhiteSpace(plan.PrimaryTargetSelector) ||
				(plan.SecondaryTargetSelectors?.Count ?? 0) > 0 ||
				!string.IsNullOrWhiteSpace(plan.StyleProfileId) ||
				!string.IsNullOrWhiteSpace(plan.LeashSelector) ||
				!string.IsNullOrWhiteSpace(plan.RetargetPolicyId) ||
				!string.IsNullOrWhiteSpace(plan.EngagementPolicyId) ||
				!string.IsNullOrWhiteSpace(plan.FallbackIntentId));
	}

	private static BattleGroupPlanSnapshot CopyPlanForGroup(
		string battleGroupId,
		BattleGroupPlanSnapshot source)
	{
		if (source == null)
		{
			return new BattleGroupPlanSnapshot();
		}

		return new BattleGroupPlanSnapshot
		{
			BattleGroupId = battleGroupId ?? "",
			ObjectiveZoneId = source.ObjectiveZoneId ?? "",
			EngagementRule = System.Enum.IsDefined(typeof(BattleEngagementRule), source.EngagementRule)
				? source.EngagementRule
				: BattleEngagementRule.AttackFirst,
			InitialFormationId = source.InitialFormationId ?? "",
			HasObjectiveAnchor = source.HasObjectiveAnchor,
			ObjectiveCellX = source.ObjectiveCellX,
			ObjectiveCellY = source.ObjectiveCellY,
			ObjectiveCellHeight = source.ObjectiveCellHeight,
			ObjectiveWidth = source.ObjectiveWidth,
			ObjectiveHeight = source.ObjectiveHeight,
			HasInitialDestinationBeacon = source.HasInitialDestinationBeacon,
			InitialDestinationCellX = source.InitialDestinationCellX,
			InitialDestinationCellY = source.InitialDestinationCellY,
			InitialDestinationCellHeight = source.InitialDestinationCellHeight
		};
	}

	private static BattleTacticalIntentPlanSnapshot CopyTacticalIntentPlan(
		BattleTacticalIntentPlanSnapshot source,
		string intentSource = "")
	{
		if (source == null)
		{
			return new BattleTacticalIntentPlanSnapshot();
		}

		return new BattleTacticalIntentPlanSnapshot
		{
			IntentId = source.IntentId ?? "",
			PrimaryTargetSelector = source.PrimaryTargetSelector ?? "",
			SecondaryTargetSelectors = (source.SecondaryTargetSelectors ?? new List<string>())
				.Where(item => !string.IsNullOrWhiteSpace(item))
				.ToList(),
			StyleProfileId = source.StyleProfileId ?? "",
			LeashSelector = source.LeashSelector ?? "",
			RetargetPolicyId = source.RetargetPolicyId ?? "",
			EngagementPolicyId = source.EngagementPolicyId ?? "",
			FallbackIntentId = source.FallbackIntentId ?? "",
			IntentSource = string.IsNullOrWhiteSpace(intentSource)
				? source.IntentSource ?? ""
				: intentSource
		};
	}

	private static int ResolveFallbackCellX(string fallbackFactionId)
	{
		return fallbackFactionId == "player" ? 0 : 8;
	}

	private sealed class ProbeSeed
	{
		public List<BattleGroupState> Groups { get; } = new();
		public Dictionary<string, HeroState> Heroes { get; } = new();
		public Dictionary<string, CorpsState> Corps { get; } = new();
		public Dictionary<string, ProbeGroupMetadata> GroupMetadata { get; } = new();
	}

	private enum BattlePlanSide
	{
		Player,
		Enemy
	}

	private enum FixedTargetCandidateRole
	{
		Defensive,
		Offensive
	}

	private sealed class FixedTargetCandidate
	{
		public BattleObjectiveZoneSnapshot Zone { get; init; } = new();
		public int OpposingAliveActorsInsideRegion { get; init; }
		public double Distance { get; init; }
		public double Score { get; init; }
	}

	private sealed class ProbeGroupMetadata
	{
		public string FactionId { get; init; } = "";
		public string SourceForceId { get; init; } = "";
		public string RuntimeCommanderGroupId { get; init; } = "";
		public string HeroBattleUnitId { get; init; } = "";
		public string CorpsBattleUnitId { get; init; } = "";
		public BattlePlanSide PlanSide { get; init; }
		public int CellX { get; init; }
		public int CellY { get; init; }
		public int CellHeight { get; init; }
		public int FootprintWidth { get; init; } = 1;
		public int FootprintHeight { get; init; } = 1;
		public int MaxHitPoints { get; init; }
		public int AttackDamage { get; init; }
		public int AttackRange { get; init; } = 1;
		public double AttackSpeed { get; init; } = BattleAttackSpeedPolicy.DefaultAttackSpeed;
		public double MoveStepSeconds { get; init; } = BattleActionTimingPolicy.DefaultMoveStepSeconds;
		public double AttackActionSeconds { get; init; }
		public double AttackImpactDelaySeconds { get; init; } = double.NaN;
		public string InitialCorpsCommandId { get; init; } = "";
		public BattleGroupPlanSnapshot Plan { get; init; } = new();
		public BattleTacticalIntentPlanSnapshot TacticalIntentPlan { get; init; } = new();
	}
}
