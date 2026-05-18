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
		AddForces(seed, request.PlayerForces, "player", sourceLocationId, ref groupIndex);
		AddForces(seed, request.EnemyForces, "enemy", request.TargetSiteId ?? sourceLocationId, ref groupIndex);

		return seed;
	}

	private static void AddForces(
		ProbeSeed seed,
		IEnumerable<BattleForceRequest> forces,
		string fallbackFactionId,
		string sourceLocationId,
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
					AttackSpeed = force.AttackSpeed
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
			group.AttackSpeed = metadata.AttackSpeed;
		}
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

	private sealed class ProbeGroupMetadata
	{
		public string FactionId { get; init; } = "";
		public string SourceForceId { get; init; } = "";
		public int CellX { get; init; }
		public int CellY { get; init; }
		public int CellHeight { get; init; }
		public int FootprintWidth { get; init; } = 1;
		public int FootprintHeight { get; init; } = 1;
		public double AttackSpeed { get; init; } = 1.0;
	}
}
