using System.Reflection;
using Rpg.Application.Battle.Commands;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.Events;

internal static class TargetBattleSkillConfigurationAuthorityRegressionCases
{
    internal static void Register(Action<string, Action> run)
    {
        run("skill command uses skill definition id not skill id", SkillCommandUsesSkillDefinitionIdNotSkillId);
        run("runtime requires owned grant or loadout", RuntimeRequiresOwnedGrantOrLoadout);
        run("runtime rejects duplicate grant availability keys", RuntimeRejectsDuplicateGrantAvailabilityKeys);
        run("runtime rejects use limit exhausted through availability state", RuntimeRejectsUseLimitExhaustedThroughAvailabilityState);
        run("typed damage payload has base damage not amount", TypedDamagePayloadHasBaseDamageNotAmount);
        run("typed executor registry rejects unsupported payload", TypedExecutorRegistryRejectsUnsupportedPayload);
    }

    private static void SkillCommandUsesSkillDefinitionIdNotSkillId()
    {
        Type requestType = typeof(CommandRequest);

        AssertTrue(
            requestType.GetProperty("SkillDefinitionId", BindingFlags.Public | BindingFlags.Instance) != null,
            "CommandRequest should expose SkillDefinitionId as the content lookup id");
        AssertTrue(
            requestType.GetProperty("SkillId", BindingFlags.Public | BindingFlags.Instance) == null,
            "CommandRequest must not keep SkillId as a parallel command authority");
    }

    private static void RuntimeRequiresOwnedGrantOrLoadout()
    {
        Type snapshotType = typeof(BattleSkillSnapshot);

        AssertStringProperty(snapshotType, "SkillDefinitionId");
        AssertStringProperty(snapshotType, "GrantedSkillId");
        AssertStringProperty(snapshotType, "LoadoutSlotId");
        AssertStringProperty(snapshotType, "OwnerBattleGroupId");
        AssertStringProperty(snapshotType, "RuntimeCommanderGroupId");

        string resolverSource = ReadRuntimeBattleSource("BattleRuntimeHeroSkillCommandResolver*.cs");
        AssertTrue(
            resolverSource.Contains("GrantedSkillId", StringComparison.Ordinal) ||
            resolverSource.Contains("LoadoutSlotId", StringComparison.Ordinal),
            "runtime command validation should require owned grant or loadout facts");
        AssertTrue(
            !resolverSource.Contains("CasterUnitIds", StringComparison.Ordinal),
            "runtime command validation should not use CasterUnitIds as the skill ownership authority");
    }

    private static void RuntimeRejectsDuplicateGrantAvailabilityKeys()
    {
        string sessionSource = ReadRuntimeBattleSource("BattleRuntimeSession*.cs");

        AssertTrue(
            sessionSource.Contains("battle_skill_grant_duplicate", StringComparison.Ordinal),
            "runtime launch validation should reject duplicate GrantedSkillId values before battle starts");
        AssertTrue(
            sessionSource.Contains("battle_skill_loadout_slot_duplicate", StringComparison.Ordinal),
            "runtime launch validation should reject duplicate owner loadout slots before battle starts");
    }

    private static void RuntimeRejectsUseLimitExhaustedThroughAvailabilityState()
    {
        Type stateType = Type.GetType("Rpg.Runtime.Battle.BattleSkillAvailabilityState, rpg");
        AssertTrue(stateType != null, "Runtime should expose BattleSkillAvailabilityState");

        string runtimeSource = ReadRuntimeBattleSource("*.cs");
        AssertTrue(
            !runtimeSource.Contains("UsedHeroSkillKeys", StringComparison.Ordinal),
            "runtime must remove hidden UsedHeroSkillKeys one-use tracking");
        AssertTrue(
            runtimeSource.Contains("skill_use_limit_exhausted", StringComparison.Ordinal),
            "runtime availability should reject exhausted limited-use skills with a structured reason");
    }

    private static void TypedDamagePayloadHasBaseDamageNotAmount()
    {
        Type? payloadType = Type.GetType("Rpg.Application.Battle.Snapshots.DamageSkillEffectSnapshot, rpg");
        AssertTrue(payloadType != null, "DamageSkillEffectSnapshot should exist");
        AssertTrue(
            payloadType!.GetProperty("BaseDamage", BindingFlags.Public | BindingFlags.Instance) != null,
            "damage payload should expose BaseDamage");
        AssertTrue(
            typeof(BattleSkillEffectSnapshot).GetProperty("Amount", BindingFlags.Public | BindingFlags.Instance) == null,
            "base BattleSkillEffectSnapshot must not expose generic Amount");
        AssertTrue(
            typeof(BattleSkillEffectSnapshot).GetProperty("Kind", BindingFlags.Public | BindingFlags.Instance) == null,
            "base BattleSkillEffectSnapshot must not expose the deleted legacy effect kind alias");
    }

    private static void TypedExecutorRegistryRejectsUnsupportedPayload()
    {
        Type? registryType = Type.GetType("Rpg.Runtime.Battle.Effects.BattleSkillEffectExecutorRegistry, rpg");
        AssertTrue(registryType != null, "typed effect executor registry should exist");

        string runtimeSource = ReadRuntimeBattleSource(Path.Combine("Effects", "*.cs"));
        AssertTrue(
            runtimeSource.Contains("battle_skill_effect_executor_missing", StringComparison.Ordinal),
            "typed executor registry should expose a structured missing-executor reason");
        AssertTrue(
            !runtimeSource.Contains("switch (payload.EffectKind)", StringComparison.Ordinal),
            "effect execution must not dispatch through the old generic EffectKind switch");

        string runtimeBattleSource = ReadRuntimeBattleSource("*.cs") + "\n" + runtimeSource;
        AssertTrue(
            !runtimeBattleSource.Contains("BattleEffectPayload", StringComparison.Ordinal),
            "runtime effect execution must not keep the deleted generic BattleEffectPayload wrapper");
        AssertTrue(
            !runtimeBattleSource.Contains("BattleSkillEffectKind", StringComparison.Ordinal),
            "runtime effect execution must not depend on the deleted legacy BattleSkillEffectKind enum");
    }

    private static void AssertStringProperty(Type type, string propertyName)
    {
        PropertyInfo? property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        AssertTrue(property != null, $"{type.Name} should expose {propertyName}");
        AssertTrue(property!.PropertyType == typeof(string), $"{type.Name}.{propertyName} should be a string");
    }

    private static string ReadRuntimeBattleSource(string searchPattern)
    {
        string root = ProjectRoot();
        string runtimeRoot = Path.Combine(root, "src", "Runtime", "Battle");
        string directory = runtimeRoot;
        string pattern = searchPattern;
        string nestedDirectory = Path.GetDirectoryName(searchPattern) ?? "";
        if (!string.IsNullOrWhiteSpace(nestedDirectory))
        {
            directory = Path.Combine(runtimeRoot, nestedDirectory);
            pattern = Path.GetFileName(searchPattern);
        }

        return string.Join("\n", Directory
            .GetFiles(directory, pattern, SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(File.ReadAllText));
    }

    private static string ProjectRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "rpg.csproj")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate project root from test output directory.");
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
