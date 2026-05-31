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
				string factionId = string.IsNullOrWhiteSpace(force.FactionId)
					? fallbackFactionId
					: force.FactionId;
				BattleForcePlacementRequest placement = index < force.PreferredPlacements.Count
					? force.PreferredPlacements[index]
					: null;
				string heroId = $"probe_hero_{groupIndex}";
				string corpsId = $"probe_corps_{groupIndex}";
				BattleGroupPlanSnapshot groupPlan = ResolveBattleGroupPlan(request, force, planSide);

				BattleGroupState group = new()
				{
					BattleGroupId = $"probe_group_{forceId}_{index}",
					HeroId = heroId,
					CorpsId = corpsId,
					CurrentLocationId = sourceLocationId,
					Status = BattleGroupStatus.Stationed
				};
				HeroState hero = new()
				{
					HeroId = heroId,
					HeroDefinitionId = $"probe_hero_definition_{groupIndex}",
					OwnerFactionId = factionId,
					Level = 1
				};
				CorpsState corps = new()
				{
					CorpsId = corpsId,
					CorpsDefinitionId = unitDefinitionId,
					Level = 1,
					CorpsStrength = CorpsStrengthPolicy.MaxStrength
				};

				seed.Groups.Add(group);
				seed.GroupMetadata[group.BattleGroupId] = new ProbeGroupMetadata
				{
					FactionId = factionId,
					SourceForceId = forceId,
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
					Plan = CopyPlanForGroup(group.BattleGroupId, groupPlan)
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
		string regionId = $"{group.BattleGroupId}:hold_seed";
		return new BattleTacticalRegionSnapshot
		{
			RegionId = regionId,
			OwnerBattleGroupId = group.BattleGroupId ?? "",
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
		return new BattleTacticalRegionSnapshot
		{
			RegionId = $"{group.BattleGroupId}:fixed:{selected.Zone.ObjectiveZoneId}",
			OwnerBattleGroupId = group.BattleGroupId ?? "",
			Kind = BattleTacticalRegionKind.FixedTarget,
			SourceRegionId = selected.Zone.ObjectiveZoneId ?? "",
			ReasonCode = densityDecided
				? BattleGroupTacticalReasonCode.RegionFixedSelectedPlayerDensity
				: BattleGroupTacticalReasonCode.RegionFixedSelectedPriority,
			CenterCellX = selected.Zone.CellX,
			CenterCellY = selected.Zone.CellY,
			CenterCellHeight = selected.Zone.CellHeight,
			Width = System.Math.Max(1, selected.Zone.Width),
			Height = System.Math.Max(1, selected.Zone.Height)
		};
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
			group.Plan = CopyPlanForGroup(group.BattleGroupId, metadata.Plan);
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
			string groupKey = ResolveBattleGroupPlanKey(force);
			foreach (string key in new[] { groupKey, force?.ForceId ?? "" }.Where(key => !string.IsNullOrWhiteSpace(key)))
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

	private static string ResolveBattleGroupPlanKey(BattleForceRequest force)
	{
		if (force == null)
		{
			return "";
		}

		if (!string.IsNullOrWhiteSpace(force.SourceKind) && !string.IsNullOrWhiteSpace(force.SourceId))
		{
			return $"{force.SourceKind}:{force.SourceId}";
		}

		if (!string.IsNullOrWhiteSpace(force.SourceId))
		{
			return force.SourceId;
		}

		return string.IsNullOrWhiteSpace(force.ForceId) ? force.UnitDefinitionId ?? "" : force.ForceId;
	}

	private static bool HasAuthoredPlan(BattleGroupPlanSnapshot plan)
	{
		return plan != null &&
			   (!string.IsNullOrWhiteSpace(plan.ObjectiveZoneId) ||
				!string.IsNullOrWhiteSpace(plan.InitialFormationId) ||
				plan.HasObjectiveAnchor ||
				plan.EngagementRule != BattleEngagementRule.AttackFirst);
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
			ObjectiveHeight = source.ObjectiveHeight
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
		public BattlePlanSide PlanSide { get; init; }
		public int CellX { get; init; }
		public int CellY { get; init; }
		public int CellHeight { get; init; }
		public int FootprintWidth { get; init; } = 1;
		public int FootprintHeight { get; init; } = 1;
		public int MaxHitPoints { get; init; }
		public int AttackDamage { get; init; }
		public int AttackRange { get; init; } = 1;
		public double AttackSpeed { get; init; } = 1.0;
		public double MoveStepSeconds { get; init; } = BattleActionTimingPolicy.DefaultMoveStepSeconds;
		public double AttackActionSeconds { get; init; }
		public double AttackImpactDelaySeconds { get; init; }
		public string InitialCorpsCommandId { get; init; } = "";
		public BattleGroupPlanSnapshot Plan { get; init; } = new();
	}
}
