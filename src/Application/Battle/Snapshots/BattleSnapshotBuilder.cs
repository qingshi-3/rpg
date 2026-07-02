using System.Collections.Generic;
using Rpg.Application.Config;
using Rpg.Application.World;
using Rpg.Domain.BattleGroups;
using Rpg.Domain.Corps;
using Rpg.Domain.Heroes;

namespace Rpg.Application.Battle.Snapshots;

public sealed class BattleSnapshotBuilder
{
    private readonly FirstSliceBattleGroupSkillGrantProvider _skillGrantProvider;
    private readonly BattleSkillSnapshotCompiler _skillSnapshotCompiler;
    private readonly System.Func<BattleSkillDefinitionCatalog> _loadSkillCatalog;

    public BattleSnapshotBuilder()
        : this(
            new FirstSliceBattleGroupSkillGrantProvider(),
            new BattleSkillSnapshotCompiler(),
            () => BattleSkillDefinitionCatalog.Load(BattleSkillDefinitionIndexLoader.LoadDefaultIndex()))
    {
    }

    internal BattleSnapshotBuilder(
        FirstSliceBattleGroupSkillGrantProvider skillGrantProvider,
        BattleSkillSnapshotCompiler skillSnapshotCompiler,
        System.Func<BattleSkillDefinitionCatalog> loadSkillCatalog)
    {
        _skillGrantProvider = skillGrantProvider ?? throw new System.ArgumentNullException(nameof(skillGrantProvider));
        _skillSnapshotCompiler = skillSnapshotCompiler ?? throw new System.ArgumentNullException(nameof(skillSnapshotCompiler));
        _loadSkillCatalog = loadSkillCatalog ?? throw new System.ArgumentNullException(nameof(loadSkillCatalog));
    }

    public BattleStartSnapshot Build(
        string snapshotId,
        string battleId,
        string targetLocationId,
        IEnumerable<BattleGroupState> battleGroups,
        IReadOnlyDictionary<string, HeroState> heroes,
        IReadOnlyDictionary<string, CorpsState> corps)
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = snapshotId ?? "",
            BattleId = battleId ?? "",
            TargetLocationId = targetLocationId ?? "",
            LocationContext = new LocationBattleContext { LocationId = targetLocationId ?? "" }
        };

        if (battleGroups == null)
        {
            return snapshot;
        }

        foreach (BattleGroupState group in battleGroups)
        {
            if (group == null)
            {
                // Preserve invalid requested entries so Runtime rejects the handoff instead of letting Application narrow it.
                snapshot.BattleGroups.Add(new BattleGroupSnapshot());
                continue;
            }

            HeroState hero = null;
            CorpsState corpsState = null;
            heroes?.TryGetValue(group.HeroId ?? "", out hero);
            corps?.TryGetValue(group.CorpsId ?? "", out corpsState);

            snapshot.BattleGroups.Add(new BattleGroupSnapshot
            {
                BattleGroupId = group.BattleGroupId ?? "",
                FactionId = hero?.OwnerFactionId ?? "",
                HeroId = hero?.HeroId ?? group.HeroId ?? "",
                HeroDefinitionId = hero?.HeroDefinitionId ?? "",
                HeroLevel = hero?.Level ?? 0,
                CorpsId = corpsState?.CorpsId ?? group.CorpsId ?? "",
                CorpsDefinitionId = corpsState?.CorpsDefinitionId ?? "",
                CorpsLevel = corpsState?.Level ?? 0,
                CorpsEquipmentLevel = corpsState?.EquipmentLevel ?? 0,
                CorpsStrength = corpsState == null ? 0 : CorpsStrengthPolicy.Clamp(corpsState.CorpsStrength),
                SourceLocationId = group.CurrentLocationId ?? ""
            });
        }

        RecompileSkillDefinitions(snapshot);
        return snapshot;
    }

    public void RecompileSkillDefinitions(BattleStartSnapshot snapshot)
    {
        snapshot?.SkillDefinitions?.Clear();
        IReadOnlyList<BattleSkillGrantSnapshot> grants = _skillGrantProvider.CreateGrants(snapshot?.BattleGroups);
        if (grants.Count == 0)
        {
            return;
        }

        BattleSkillDefinitionCatalog catalog = _loadSkillCatalog();
        snapshot.SkillDefinitions.AddRange(_skillSnapshotCompiler.CompileTemplates(
            catalog.SnapshotTemplatesById,
            snapshot.BattleGroups,
            grants));
    }
}
