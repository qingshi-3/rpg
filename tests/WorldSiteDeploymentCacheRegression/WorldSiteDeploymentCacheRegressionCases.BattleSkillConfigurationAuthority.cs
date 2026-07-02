using Rpg.Application.World;

internal static partial class WorldSiteDeploymentCacheRegressionCases
{
internal static void BattleSkillAuthorityDeletesHardcodedFirstSliceSkillPath()
{
    string root = ProjectRoot();

    AssertFileMissing(root, "src", "Definitions", "Battle", "Skills", "FirstSliceBattleSkillDefinitions.cs");
    AssertFileMissing(root, "src", "Application", "Battle", "Snapshots", "BattleSkillSnapshotFactory.cs");
    AssertFileMissing(root, "src", "Application", "Battle", "Commands", "HeroSkillCommandIds.cs");
}

internal static void BattleSkillAuthorityDeletesLegacyAbilityDefinitionPath()
{
    string root = ProjectRoot();

    AssertFileMissing(root, "src", "Definitions", "Battle", "Abilities", "AbilityDefinition.cs");
    AssertFileMissing(root, "src", "Definitions", "Battle", "Abilities", "AbilityEffect.cs");
    AssertFileMissing(root, "src", "Definitions", "Battle", "Abilities", "DamageAbilityEffect.cs");
    AssertFileMissing(root, "src", "Presentation", "Battle", "Entities", "AbilityComponent.cs");
    AssertFileMissing(root, "src", "Presentation", "Battle", "Abilities", "BattleAbilityQueries.cs");
}

internal static void BattleSkillAuthorityDeletesOldBasicAttackAbilityResources()
{
    string root = ProjectRoot();

    AssertFileMissing(root, "assets", "battle", "abilities", "militia_basic_attack.tres");
    AssertFileMissing(root, "assets", "battle", "abilities", "player_knight_basic_attack.tres");
    AssertFileMissing(root, "assets", "battle", "abilities", "skeleton_archer_basic_attack.tres");
    AssertFileMissing(root, "assets", "battle", "abilities", "skeleton_warrior_basic_attack.tres");
}

internal static void BattleSkillAuthorityRejectsHudFallbackFactory()
{
    string root = ProjectRoot();
    string hudSource = ReadWorldSiteRootSource();
    string runtimeResolverSource = string.Join("\n", Directory
        .GetFiles(Path.Combine(root, "src", "Runtime", "Battle"), "BattleRuntimeHeroSkillCommandResolver*.cs")
        .OrderBy(path => path, StringComparer.Ordinal)
        .Select(File.ReadAllText));
    string effectSnapshotSource = File.ReadAllText(Path.Combine(
        root,
        "src",
        "Application",
        "Battle",
        "Snapshots",
        "BattleSkillEffectSnapshot.cs"));

    AssertTrue(
        !hudSource.Contains("CreateSelectedHeroSkillSnapshots", StringComparison.Ordinal),
        "HUD must not recreate hardcoded first-slice skill snapshots when Runtime has no compiled skills");
    AssertTrue(
        !runtimeResolverSource.Contains("HeroSkillCommandIds", StringComparison.Ordinal) &&
        !runtimeResolverSource.Contains("ThunderMarkFoldSkillId", StringComparison.Ordinal) &&
        !runtimeResolverSource.Contains("TeleportToThunderMark", StringComparison.Ordinal),
        "Runtime skill command validation must not branch on concrete thunder skill ids or effect enum names");
    AssertTrue(
        !effectSnapshotSource.Contains("public int Amount", StringComparison.Ordinal) &&
        !effectSnapshotSource.Contains("public double DurationSeconds", StringComparison.Ordinal) &&
        !effectSnapshotSource.Contains("public double TickIntervalSeconds", StringComparison.Ordinal) &&
        !effectSnapshotSource.Contains("public int Radius", StringComparison.Ordinal),
        "effect snapshots must not expose shared semantic payload fields reused by unrelated effects");
}

internal static void BattleSkillAuthorityUsesResourceIndexAndGrantArrays()
{
    string root = ProjectRoot();
    string indexPath = Path.Combine(root, "config", "battle", "battle_skill_definitions.json");
    string companyConfigPath = Path.Combine(root, "config", "battle", "first_slice_hero_companies.json");
    string resourceDir = Path.Combine(root, "assets", "battle", "skills");

    AssertTrue(File.Exists(indexPath), "battle skill definitions should be indexed from config/battle");
    string indexSource = File.ReadAllText(indexPath);
    foreach (string skillDefinitionId in new[]
    {
        "skill_shield_barrier",
        "skill_sun_piercer",
        "skill_thunder_tag_throw",
        "skill_thunder_mark_fold",
        "skill_thunder_spiral_break"
    })
    {
        AssertTrue(
            indexSource.Contains(skillDefinitionId, StringComparison.Ordinal),
            $"skill index should include canonical skill definition id={skillDefinitionId}");
        AssertTrue(
            File.Exists(Path.Combine(resourceDir, $"{skillDefinitionId}.tres")),
            $"authored skill Resource should exist for id={skillDefinitionId}");
    }

    AssertTrue(
        indexSource.Contains("first_slice_skill_thunder_tag_throw", StringComparison.Ordinal),
        "skill index should keep first-slice aliases only at the compiler input boundary");

    string companyConfig = File.ReadAllText(companyConfigPath);
    AssertTrue(
        companyConfig.Contains("\"skillDefinitionIds\"", StringComparison.Ordinal) &&
        !companyConfig.Contains("\"skillDefinitionId\"", StringComparison.Ordinal),
        "first-slice battle-group config should use skillDefinitionIds arrays, not one skillDefinitionId field");
    AssertTrue(
        companyConfig.Contains("\"skill_thunder_tag_throw\"", StringComparison.Ordinal) &&
        companyConfig.Contains("\"skill_thunder_mark_fold\"", StringComparison.Ordinal) &&
        companyConfig.Contains("\"skill_thunder_spiral_break\"", StringComparison.Ordinal),
        "assault battle group should grant all three migrated thunder skills through config");

    string runtimeSource = string.Join("\n", Directory
        .GetFiles(Path.Combine(root, "src", "Runtime", "Battle"), "*.cs", SearchOption.AllDirectories)
        .OrderBy(path => path, StringComparer.Ordinal)
        .Select(File.ReadAllText));
    AssertTrue(
        !runtimeSource.Contains("BattleSkillDefinitionResource", StringComparison.Ordinal) &&
        !runtimeSource.Contains("battle_skill_definitions.json", StringComparison.Ordinal),
        "Runtime must not load skill Resources or config indexes directly");
}

internal static void BattleSkillAuthorityReusesThunderKitAcrossStartingHeroes()
{
    FirstSliceHeroCompanyConfig config = FirstSliceHeroCompanyConfigLoader.LoadDefault();
    string[] expectedSkillDefinitionIds =
    {
        "skill_thunder_tag_throw",
        "skill_thunder_mark_fold",
        "skill_thunder_spiral_break"
    };

    foreach (string roleId in new[] { "shield", "archer", "assault" })
    {
        FirstSliceHeroCompanyDefinition? company = config.Companies.FirstOrDefault(item =>
            string.Equals(item.RoleId, roleId, StringComparison.Ordinal));
        AssertTrue(company != null, $"first-slice battle-group config should include role={roleId}");
        AssertEqual(
            string.Join("|", expectedSkillDefinitionIds),
            string.Join("|", company!.SkillDefinitionIds),
            $"role={roleId} should reuse the shared three-skill thunder kit through config");
        AssertEqual(
            expectedSkillDefinitionIds.Length,
            company.SkillDefinitionIds.Distinct(StringComparer.Ordinal).Count(),
            $"role={roleId} should not duplicate skill grants inside its configured kit");
    }
}

private static void AssertFileMissing(string root, params string[] pathParts)
{
    string path = Path.Combine(new[] { root }.Concat(pathParts).ToArray());
    AssertTrue(!File.Exists(path), $"legacy skill authority file should be deleted: {Path.GetRelativePath(root, path)}");
}
}
