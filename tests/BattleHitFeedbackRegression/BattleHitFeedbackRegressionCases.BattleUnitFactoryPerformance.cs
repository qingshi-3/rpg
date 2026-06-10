internal static partial class BattleHitFeedbackRegressionCases
{
internal static void BattleUnitFactoryCachesVisualAutoLayout()
{
    string source = File.ReadAllText(Path.Combine("src", "Presentation", "Battle", "Entities", "BattleUnitFactory.cs"))
        .Replace("\r\n", "\n", StringComparison.Ordinal);
    string applyLayoutBody = ExtractMethodBlock(source, "private void ApplySpriteLayout(");

    AssertTrue(
        source.Contains("SharedVisualLayoutCache", StringComparison.Ordinal) &&
        source.Contains("BuildVisualLayoutCacheKey", StringComparison.Ordinal),
        "unit visual auto-layout should cache visible-pixel scans across repeated instances of the same visual definition");
    AssertTrue(
        source.Contains("TryResolveAutoLayout(", StringComparison.Ordinal) &&
        source.Contains("SharedVisualLayoutCache.TryGetValue", StringComparison.Ordinal) &&
        source.Contains("SharedVisualLayoutCache[cacheKey] = layout;", StringComparison.Ordinal),
        "auto-layout cache should be the shared path before falling back to scanning SpriteFrames");
    AssertTrue(
        applyLayoutBody.Contains("TryResolveAutoLayout(visual, out BattleUnitVisualLayout layout)", StringComparison.Ordinal) &&
        !applyLayoutBody.Contains("BattleUnitVisualLayoutCalculator.TryCalculateAutoLayout", StringComparison.Ordinal),
        "sprite layout application should consume the cached layout resolver instead of recalculating every entity");
    AssertTrue(
        !applyLayoutBody.Contains("GameLog.Info", StringComparison.Ordinal) &&
        source.Contains("GameLog.Trace(\n            nameof(BattleUnitFactory)", StringComparison.Ordinal),
        "auto-layout diagnostics should be trace-level and not write an Info log for every created unit");
}
}
