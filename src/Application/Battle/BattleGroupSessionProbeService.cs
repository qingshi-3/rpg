using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle.Adapters;
using Rpg.Application.Battle.Snapshots;
using Rpg.Domain.BattleGroups;
using Rpg.Domain.Corps;
using Rpg.Domain.Heroes;
using Rpg.Infrastructure.Logging;

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

	private sealed class ProbeGroupMetadata
	{
		public string FactionId { get; init; } = "";
		public string SourceForceId { get; init; } = "";
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
