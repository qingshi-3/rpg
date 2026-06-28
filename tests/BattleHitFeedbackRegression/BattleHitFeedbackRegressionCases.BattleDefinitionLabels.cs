internal static partial class BattleHitFeedbackRegressionCases
{
internal static void StarterAudioMigrationUsesSourceVisuals()
{
    string report = File.ReadAllText(Path.Combine("assets", "audio", "sfx", "duelyst_audio_migration_a.json"));

    AssertTrue(
        report.Contains("actual SpriteFrames source visual", StringComparison.Ordinal),
        "migration report should document source-visual mapping rule");
    AssertTrue(
        report.Contains("f1_shieldforger.png / RSX.f1Surgeforger*", StringComparison.Ordinal),
        "shieldforger-backed units should map from f1Surgeforger visual identity");
    AssertTrue(
        report.Contains("f1_scintilla.png / RSX.f1Scintilla*", StringComparison.Ordinal),
        "scintilla-backed units should map from f1Scintilla visual identity");
    AssertTrue(
        report.Contains("app/sdk/cards/factory/wartech/faction1.coffee:291-307", StringComparison.Ordinal),
        "surgeforger mapping should cite original Duelyst card factory sound block");
    AssertTrue(
        report.Contains("app/sdk/cards/factory/bloodstorm/faction1.coffee:100-116", StringComparison.Ordinal),
        "scintilla mapping should cite original Duelyst card factory sound block");
}

internal static void WorldUnitLabelsResolveThroughBattleDefinitions()
{
    string strategicRoot = ReadStrategicWorldRootSource();
    string siteRoot = ReadWorldSiteRootSource();

    AssertTrue(
        strategicRoot.Contains("_battleUnitFactory.ResolveUnitDisplayName(unitTypeId)", StringComparison.Ordinal),
        "strategic world UI should resolve unit group labels from battle unit definitions");
    AssertTrue(
        siteRoot.Contains("_battleUnitFactory.ResolveUnitDisplayName(unitTypeId)", StringComparison.Ordinal),
        "site detail UI should resolve garrison labels from battle unit definitions");
    AssertTrue(
        siteRoot.Contains("_battleUnitFactory.ResolveUnitInstanceDisplayName(placement.UnitTypeId", StringComparison.Ordinal),
        "site placement labels should use the same indexed instance names as battle units");
}
}
