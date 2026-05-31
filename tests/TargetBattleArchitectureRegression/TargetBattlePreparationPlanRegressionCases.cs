using System.Reflection;
using Rpg.Application.Battle.Snapshots;
using Rpg.Presentation.World.Sites;

internal static class TargetBattlePreparationPlanRegressionCases
{
    public static void ExplicitAttackFirstSelectionSurvivesPreparationDefaultRefresh()
    {
        BattleGroupPlanSnapshot plan = new()
        {
            EngagementRule = BattleEngagementRule.AttackFirst
        };

        bool unselectedDefaultShouldReset = ShouldDefaultEngagementRule(plan, explicitRuleSelected: false);
        bool explicitSelectionShouldPersist = ShouldDefaultEngagementRule(plan, explicitRuleSelected: true);

        AssertTrue(unselectedDefaultShouldReset, "unselected AttackFirst remains the blank snapshot sentinel and should be normalized");
        AssertTrue(!explicitSelectionShouldPersist, "clicking AttackFirst before an objective is selected should survive UI refresh");
    }

    private static bool ShouldDefaultEngagementRule(BattleGroupPlanSnapshot plan, bool explicitRuleSelected)
    {
        MethodInfo? method = typeof(WorldSiteRoot).GetMethod(
            "ShouldDefaultBattlePreparationEngagementRule",
            BindingFlags.NonPublic | BindingFlags.Static);
        if (method == null)
        {
            throw new MissingMethodException(nameof(WorldSiteRoot), "ShouldDefaultBattlePreparationEngagementRule");
        }

        return (bool)method.Invoke(null, new object[] { plan, explicitRuleSelected })!;
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
