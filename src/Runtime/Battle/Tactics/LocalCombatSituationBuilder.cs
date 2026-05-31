using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle.Navigation;

namespace Rpg.Runtime.Battle.Tactics;

internal static class LocalCombatSituationBuilder
{
    private const int HoldLeashRange = 2;

    public static LocalCombatSituation Build(
        BattleRuntimeActor actor,
        BattleRuntimeActor target,
        IReadOnlyCollection<BattleRuntimeActor> livingCorps,
        BattleNavigationGraph graph,
        BattleDynamicOccupancy occupancy,
        double currentTimeSeconds,
        BattleTacticalRegionSnapshot localCombatRegion = null)
    {
        if (actor == null || target == null || graph == null || occupancy == null)
        {
            return null;
        }

        BattleGridCoord targetAnchor = new(target.GridX, target.GridY, target.GridHeight);
        BattleTacticalRegionSnapshot ownedLocalRegion = ResolveOwnedLocalCombatRegion(actor, localCombatRegion);
        bool blocksObjectiveRoute = actor.EngagementRule == BattleEngagementRule.MoveFirst &&
                                    BlocksObjectiveRoute(actor, target);
        bool insideLeash = IsInsideLeash(actor, target);
        if (!insideLeash)
        {
            return BuildRejectedOutsideLeashSituation(actor, target, livingCorps, currentTimeSeconds, ownedLocalRegion);
        }

        bool isLocalFight = IsLocalFight(actor, target, livingCorps);
        bool isLocalRegionTarget = IsActorInsideLocalCombatRegion(target, ownedLocalRegion);
        if (!blocksObjectiveRoute && !isLocalFight && !isLocalRegionTarget)
        {
            return null;
        }

        BattleGridCoord situationCenter = ResolveSituationCenter(targetAnchor, ownedLocalRegion);
        BattleCombatSlot[] slots = BattleCombatSlotAllocator.FindSlots(
            actor,
            target,
            graph,
            localCombatRegion: ownedLocalRegion).ToArray();
        BattleCombatSlot[] attackSlots = slots
            .Where(item => item.Kind == BattleCombatSlotKind.Attack)
            .ToArray();
        BattleCombatSlot[] openAttackSlots = attackSlots
            .Where(item => occupancy.CanPlaceFootprint(actor, item.Anchor))
            .ToArray();
        BattleCombatSlot[] supportSlots = slots
            .Where(item => item.Kind == BattleCombatSlotKind.Support && occupancy.CanPlaceFootprint(actor, item.Anchor))
            .ToArray();
        BattleCombatSlot? preferredSupport = supportSlots.Cast<BattleCombatSlot?>().FirstOrDefault();
        int nearbyFriendlyCount = CountNearby(livingCorps, actor, targetAnchor, sameFaction: true);
        int nearbyHostileCount = CountNearby(livingCorps, actor, targetAnchor, sameFaction: false);
        return new LocalCombatSituation
        {
            SituationId = $"local:{target.ActorId}",
            OwnerBattleGroupId = ownedLocalRegion?.OwnerBattleGroupId ?? actor.BattleGroupId ?? "",
            RegionId = ownedLocalRegion?.RegionId ?? "",
            Center = situationCenter,
            TargetActorId = target.ActorId ?? "",
            DirtyReason = "decision_boundary",
            ReasonCode = ownedLocalRegion?.ReasonCode ?? "decision_boundary",
            Version = ownedLocalRegion?.Version ?? 1,
            Width = System.Math.Max(1, ownedLocalRegion?.Width ?? 1),
            Height = System.Math.Max(1, ownedLocalRegion?.Height ?? 1),
            LastBuiltRuntimeTimeSeconds = currentTimeSeconds,
            NearbyFriendlyCount = nearbyFriendlyCount,
            NearbyHostileCount = nearbyHostileCount,
            OpenAttackSlotCount = openAttackSlots.Length,
            OccupiedAttackSlotCount = System.Math.Max(0, attackSlots.Length - openAttackSlots.Length),
            BlocksObjectiveRoute = blocksObjectiveRoute,
            InsideLeash = insideLeash,
            HasReachableAttackSlot = openAttackSlots.Length > 0,
            HasReachableSupportSlot = preferredSupport.HasValue,
            PreferredSupportRole = preferredSupport?.SupportRole ?? LocalCombatSupportSlotRole.None,
            PreferredSupportAnchor = preferredSupport?.Anchor ?? default,
            ParticipantActorIds = (livingCorps ?? System.Array.Empty<BattleRuntimeActor>())
                .Where(item => item?.HitPoints > 0 && IsNearTarget(item, targetAnchor, BattlePerceptionPolicy.DefaultLocalPerceptionRange))
                .Select(item => item.ActorId ?? "")
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .OrderBy(item => item, System.StringComparer.Ordinal)
                .ToArray(),
            NearbyCandidateActorIds = new[] { actor.ActorId ?? "" }
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToArray()
        };
    }

    private static LocalCombatSituation BuildRejectedOutsideLeashSituation(
        BattleRuntimeActor actor,
        BattleRuntimeActor target,
        IReadOnlyCollection<BattleRuntimeActor> livingCorps,
        double currentTimeSeconds,
        BattleTacticalRegionSnapshot localCombatRegion)
    {
        BattleGridCoord targetAnchor = new(target.GridX, target.GridY, target.GridHeight);
        BattleGridCoord situationCenter = ResolveSituationCenter(targetAnchor, localCombatRegion);
        return new LocalCombatSituation
        {
            SituationId = $"local:{target.ActorId}",
            OwnerBattleGroupId = localCombatRegion?.OwnerBattleGroupId ?? actor.BattleGroupId ?? "",
            RegionId = localCombatRegion?.RegionId ?? "",
            Center = situationCenter,
            TargetActorId = target.ActorId ?? "",
            DirtyReason = LocalCombatDecisionReason.RejectOutsideLeash,
            ReasonCode = localCombatRegion?.ReasonCode ?? LocalCombatDecisionReason.RejectOutsideLeash,
            Version = localCombatRegion?.Version ?? 1,
            Width = System.Math.Max(1, localCombatRegion?.Width ?? 1),
            Height = System.Math.Max(1, localCombatRegion?.Height ?? 1),
            LastBuiltRuntimeTimeSeconds = currentTimeSeconds,
            NearbyFriendlyCount = CountNearby(livingCorps, actor, targetAnchor, sameFaction: true),
            NearbyHostileCount = CountNearby(livingCorps, actor, targetAnchor, sameFaction: false),
            BlocksObjectiveRoute = false,
            InsideLeash = false,
            ParticipantActorIds = (livingCorps ?? System.Array.Empty<BattleRuntimeActor>())
                .Where(item => item?.HitPoints > 0 && IsNearTarget(item, targetAnchor, BattlePerceptionPolicy.DefaultLocalPerceptionRange))
                .Select(item => item.ActorId ?? "")
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .OrderBy(item => item, System.StringComparer.Ordinal)
                .ToArray(),
            NearbyCandidateActorIds = new[] { actor.ActorId ?? "" }
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToArray()
        };
    }

    private static int CountNearby(
        IReadOnlyCollection<BattleRuntimeActor> livingCorps,
        BattleRuntimeActor actor,
        BattleGridCoord targetAnchor,
        bool sameFaction)
    {
        return (livingCorps ?? System.Array.Empty<BattleRuntimeActor>())
            .Count(item =>
                item?.HitPoints > 0 &&
                string.Equals(item.FactionId, actor.FactionId, System.StringComparison.Ordinal) == sameFaction &&
                IsNearTarget(item, targetAnchor, BattlePerceptionPolicy.DefaultLocalPerceptionRange));
    }

    private static BattleTacticalRegionSnapshot ResolveOwnedLocalCombatRegion(
        BattleRuntimeActor actor,
        BattleTacticalRegionSnapshot localCombatRegion)
    {
        if (actor == null ||
            localCombatRegion == null ||
            localCombatRegion.Kind != BattleTacticalRegionKind.LocalCombat ||
            !string.Equals(localCombatRegion.OwnerBattleGroupId ?? "", actor.BattleGroupId ?? "", System.StringComparison.Ordinal))
        {
            return null;
        }

        return localCombatRegion;
    }

    private static BattleGridCoord ResolveSituationCenter(
        BattleGridCoord fallback,
        BattleTacticalRegionSnapshot localCombatRegion)
    {
        return localCombatRegion == null
            ? fallback
            : new BattleGridCoord(
                localCombatRegion.CenterCellX,
                localCombatRegion.CenterCellY,
                localCombatRegion.CenterCellHeight);
    }

    private static bool IsNearTarget(BattleRuntimeActor actor, BattleGridCoord targetAnchor, int range)
    {
        return System.Math.Abs((actor?.GridX ?? 0) - targetAnchor.X) +
               System.Math.Abs((actor?.GridY ?? 0) - targetAnchor.Y) +
               System.Math.Abs((actor?.GridHeight ?? 0) - targetAnchor.Height) <= range;
    }

    private static bool IsActorInsideLocalCombatRegion(
        BattleRuntimeActor actor,
        BattleTacticalRegionSnapshot localCombatRegion)
    {
        if (actor == null ||
            localCombatRegion == null ||
            actor.GridHeight != localCombatRegion.CenterCellHeight)
        {
            return false;
        }

        int width = System.Math.Max(1, localCombatRegion.Width);
        int height = System.Math.Max(1, localCombatRegion.Height);
        int minX = localCombatRegion.CenterCellX - (width - 1) / 2;
        int minY = localCombatRegion.CenterCellY - (height - 1) / 2;
        return actor.GridX >= minX &&
               actor.GridX < minX + width &&
               actor.GridY >= minY &&
               actor.GridY < minY + height;
    }

    private static bool BlocksObjectiveRoute(BattleRuntimeActor actor, BattleRuntimeActor target)
    {
        if (actor?.HasObjectiveAnchor != true || target == null)
        {
            return false;
        }

        bool horizontalCorridor = actor.GridY == actor.ObjectiveGridY && target.GridY == actor.GridY;
        bool verticalCorridor = actor.GridX == actor.ObjectiveGridX && target.GridX == actor.GridX;
        return horizontalCorridor && IsBetween(actor.GridX, actor.ObjectiveGridX, target.GridX) ||
               verticalCorridor && IsBetween(actor.GridY, actor.ObjectiveGridY, target.GridY);
    }

    private static bool IsLocalFight(
        BattleRuntimeActor actor,
        BattleRuntimeActor target,
        IReadOnlyCollection<BattleRuntimeActor> livingCorps)
    {
        if (actor == null || target == null)
        {
            return false;
        }

        BattleGridCoord targetAnchor = new(target.GridX, target.GridY, target.GridHeight);
        return IsNearTarget(actor, targetAnchor, BattlePerceptionPolicy.DefaultLocalPerceptionRange) &&
               (IsEngaged(actor, target) ||
                (livingCorps ?? System.Array.Empty<BattleRuntimeActor>())
                .Any(item =>
                    item?.HitPoints > 0 &&
                    !string.Equals(item.ActorId, actor.ActorId, System.StringComparison.Ordinal) &&
                    string.Equals(item.FactionId, actor.FactionId, System.StringComparison.Ordinal) &&
                    IsEngaged(item, target)));
    }

    private static bool IsEngaged(BattleRuntimeActor actor, BattleRuntimeActor target)
    {
        int attackRange = System.Math.Max(1, actor?.AttackRange ?? 1);
        return BattleActorFootprint.GetOrthogonalGap(
            actor,
            new BattleGridCoord(actor?.GridX ?? 0, actor?.GridY ?? 0, actor?.GridHeight ?? 0),
            target,
            new BattleGridCoord(target?.GridX ?? 0, target?.GridY ?? 0, target?.GridHeight ?? 0)) <= attackRange;
    }

    private static bool IsBetween(int first, int second, int value)
    {
        return value >= System.Math.Min(first, second) && value <= System.Math.Max(first, second);
    }

    private static bool IsInsideLeash(BattleRuntimeActor actor, BattleRuntimeActor target)
    {
        if (actor?.EngagementRule != BattleEngagementRule.Hold || actor.HasObjectiveAnchor == false || target == null)
        {
            return true;
        }

        int gap = System.Math.Abs(target.GridX - actor.ObjectiveGridX) +
                  System.Math.Abs(target.GridY - actor.ObjectiveGridY) +
                  System.Math.Abs(target.GridHeight - actor.ObjectiveGridHeight);
        int leash = System.Math.Max(HoldLeashRange, System.Math.Max(actor.ObjectiveWidth, actor.ObjectiveHeight));
        return gap <= leash;
    }
}
