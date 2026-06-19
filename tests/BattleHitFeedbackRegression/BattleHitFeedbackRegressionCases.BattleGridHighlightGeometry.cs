internal static partial class BattleHitFeedbackRegressionCases
{
internal static void BattleGridHighlightOverlayDelegatesGeometry()
{
    string geometryPath = Path.Combine("src", "Presentation", "Battle", "BattleGridHighlightGeometry.cs");
    string overlay = ReadBattleGridHighlightOverlaySource();
    string vectorRenderer = File.ReadAllText(Path.Combine("src", "Presentation", "Battle", "BattleGridVectorHighlightRenderer.cs"));

    AssertTrue(File.Exists(geometryPath), "battle grid highlight geometry should live in a focused presentation helper");
    string geometry = File.ReadAllText(geometryPath);

    AssertTrue(
        geometry.Contains("internal sealed class BattleGridHighlightGeometry", StringComparison.Ordinal) &&
        geometry.Contains("BuildCellPolygon", StringComparison.Ordinal) &&
        geometry.Contains("BuildBoundarySegments", StringComparison.Ordinal) &&
        geometry.Contains("BuildHoverFramePolygon", StringComparison.Ordinal) &&
        geometry.Contains("BuildTargetLockFramePolygon", StringComparison.Ordinal),
        "highlight geometry helper should own cell, boundary, hover-frame, and target-lock polygon calculations");
    AssertTrue(
        overlay.Contains("_highlightGeometry", StringComparison.Ordinal) &&
        vectorRenderer.Contains("geometry.BuildCellPolygon", StringComparison.Ordinal) &&
        vectorRenderer.Contains("geometry.BuildBoundarySegments", StringComparison.Ordinal) &&
        vectorRenderer.Contains("geometry.BuildTargetLockFramePolygon", StringComparison.Ordinal),
        "highlight overlay should delegate geometry calculations to the helper");
    AssertTrue(
        !overlay.Contains("private Vector2[] BuildCellPolygon", StringComparison.Ordinal) &&
        !overlay.Contains("private IEnumerable<(Vector2 Start, Vector2 End)> BuildBoundarySegments", StringComparison.Ordinal) &&
        !overlay.Contains("private Vector2[] BuildTargetLockFramePolygon", StringComparison.Ordinal),
        "highlight overlay should not keep the extracted geometry implementation methods");
}
}
