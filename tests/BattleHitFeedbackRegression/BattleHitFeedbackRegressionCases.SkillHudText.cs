using System;
using System.IO;

internal static partial class BattleHitFeedbackRegressionCases
{
    internal static void BattleRuntimeSkillHudMapsQueuedRejection()
    {
        string source = File.ReadAllText(Path.Combine("src", "Presentation", "World", "Sites", "BattleRuntimeSkillHudText.cs"));

        AssertTrue(
            source.Contains("\"hero_skill_already_queued\"", StringComparison.Ordinal) &&
            source.Contains("技能指令正在等待结算", StringComparison.Ordinal),
            "battle runtime skill HUD should map queued skill rejection to the existing pending-command user text instead of falling back to runtime-not-ready");
    }
}
