internal static partial class BattleHitFeedbackRegressionCases
{
internal static void BattleUnitRootDelegatesHitFeedbackPresentation()
{
    string presenterPath = Path.Combine("src", "Presentation", "Battle", "Entities", "BattleUnitHitFeedbackPresenter.cs");
    string unitRoot = ReadBattleUnitRootSource();
    string unitRootMain = File.ReadAllText(Path.Combine("src", "Presentation", "Battle", "Entities", "BattleUnitRoot.cs"));

    AssertTrue(File.Exists(presenterPath), "battle unit hit feedback should live in a focused presenter instead of the root scene shell");
    string presenter = File.ReadAllText(presenterPath);

    AssertTrue(
        presenter.Contains("internal sealed class BattleUnitHitFeedbackPresenter", StringComparison.Ordinal) &&
        presenter.Contains("PlayAsync(", StringComparison.Ordinal) &&
        presenter.Contains("ResolveHitFeedbackTargets", StringComparison.Ordinal) &&
        presenter.Contains("PlayHitOutlinePulses", StringComparison.Ordinal) &&
        presenter.Contains("SpawnDamageNumbers", StringComparison.Ordinal) &&
        presenter.Contains("BattleSkillImpactFeedbackPlayer.PlaySkillImpacts", StringComparison.Ordinal),
        "hit feedback presenter should own target resolution, impact FX, outline pulses, and damage-number spawning");
    AssertTrue(
        unitRoot.Contains("_hitFeedbackPresenter", StringComparison.Ordinal) &&
        unitRoot.Contains("HitFeedbackPresenter.PlayAsync", StringComparison.Ordinal) &&
        unitRoot.Contains("ClearHitOutlines", StringComparison.Ordinal),
        "battle unit root should delegate hit feedback playback and cleanup to the presenter");
    AssertTrue(
        !unitRootMain.Contains("private async Task PlayHitFeedbackAsync", StringComparison.Ordinal) &&
        !unitRootMain.Contains("private IEnumerable<BattleEntity> ResolveHitFeedbackTargets", StringComparison.Ordinal) &&
        !unitRootMain.Contains("private void SetHitOutlines", StringComparison.Ordinal) &&
        !unitRootMain.Contains("private static void PlayHitOutlinePulses", StringComparison.Ordinal) &&
        !unitRootMain.Contains("private void SpawnDamageNumbers", StringComparison.Ordinal),
        "battle unit root should not keep the extracted hit feedback implementation methods");
}
}
