using System.Collections.Generic;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Snapshots;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Battle.Entities;

namespace Rpg.Presentation.World.Sites;

public partial class WorldSiteRoot
{
    private void AlignBattlePresentationEntityIdsToRuntime(BattleStartRequest request)
    {
        if (_unitRoot == null || request == null)
        {
            return;
        }

        IReadOnlyDictionary<string, string> runtimeActorIdsByPresentationEntity =
            BattleRuntimeActorIdentity.BuildPresentationEntityToRuntimeActorMap(request);
        AlignBattlePresentationEntityIdsToRuntime(request, runtimeActorIdsByPresentationEntity, "");
    }

    private void AlignBattlePresentationEntityIdsToRuntime(BattleStartRequest request, BattleStartSnapshot launchedSnapshot)
    {
        if (_unitRoot == null || request == null || launchedSnapshot == null)
        {
            return;
        }

        // Strategic launch sync may normalize Runtime SourceForceId away from the
        // visual force id. The launched snapshot is the authority for live event IDs.
        IReadOnlyDictionary<string, string> runtimeActorIdsByPresentationEntity =
            BattleRuntimeActorIdentity.BuildPresentationEntityToRuntimeActorMap(request, launchedSnapshot);
        AlignBattlePresentationEntityIdsToRuntime(
            request,
            runtimeActorIdsByPresentationEntity,
            launchedSnapshot.SnapshotId ?? "");
    }

    private void AlignBattlePresentationEntityIdsToRuntime(
        BattleStartRequest request,
        IReadOnlyDictionary<string, string> runtimeActorIdsByPresentationEntity,
        string snapshotId)
    {
        if (_unitRoot == null || request == null || runtimeActorIdsByPresentationEntity == null)
        {
            return;
        }

        if (runtimeActorIdsByPresentationEntity.Count == 0)
        {
            return;
        }

        int aligned = 0;
        foreach (BattleEntity entity in _unitRoot.GetEntitiesSnapshot())
        {
            string currentEntityId = entity?.EntityId ?? "";
            if (string.IsNullOrWhiteSpace(currentEntityId) ||
                !runtimeActorIdsByPresentationEntity.TryGetValue(currentEntityId, out string runtimeActorId) ||
                string.IsNullOrWhiteSpace(runtimeActorId) ||
                string.Equals(currentEntityId, runtimeActorId, System.StringComparison.Ordinal))
            {
                continue;
            }

            // Runtime events address corps actors, not legacy request force ids. Presentation
            // entities must switch identity at the launch boundary before live events arrive.
            entity.EntityId = runtimeActorId;
            aligned++;
        }

        if (aligned > 0)
        {
            GameLog.Info(
                nameof(WorldSiteRoot),
                $"BattlePresentationEntityIdsAligned request={request.RequestId} snapshot={snapshotId ?? ""} count={aligned}");
        }
    }
}
