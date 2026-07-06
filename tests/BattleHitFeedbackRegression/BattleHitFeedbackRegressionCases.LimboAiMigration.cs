internal static partial class BattleHitFeedbackRegressionCases
{
internal static void LimboAiAuthoredResourcesMirrorPlannerBranches()
{
    string aiResourceRoot = Path.Combine("resource", "battle", "ai");
    string taskRoot = Path.Combine("src", "Runtime", "Battle", "AI", "LimboTasks");
    string enemyTreePath = Path.Combine(aiResourceRoot, "battle_enemy_basic.tres");
    string alliedTreePath = Path.Combine(aiResourceRoot, "battle_corps_commanded.tres");
    string commandTaskPath = Path.Combine(taskRoot, "has_battle_command.gd");
    string rangedTaskPath = Path.Combine(taskRoot, "has_ranged_battle_ability.gd");
    string facadePath = Path.Combine("src", "Presentation", "Battle", "AI", "BattleAiFacade.cs");

    AssertTrue(File.Exists(enemyTreePath), "enemy battle tree should stay authored as a LimboAI resource");
    AssertTrue(File.Exists(alliedTreePath), "allied command battle tree should be authored as a LimboAI resource");
    AssertTrue(File.Exists(commandTaskPath), "allied command tree should use a LimboAI command condition task");
    AssertTrue(File.Exists(rangedTaskPath), "enemy battle tree should use a LimboAI ranged ability condition task");

    string enemyTree = File.ReadAllText(enemyTreePath);
    AssertTrue(enemyTree.Contains("has_ranged_battle_ability.gd", StringComparison.Ordinal), "enemy tree should call a task before emitting ranged pressure");
    AssertTrue(enemyTree.Contains("template_id = &\"ranged_pressure\"", StringComparison.Ordinal), "enemy tree should represent the ranged pressure branch from the C# planner");
    AssertTrue(enemyTree.Contains("template_id = &\"direct_strike\"", StringComparison.Ordinal), "enemy tree should represent the direct strike branch from the C# planner");
    AssertTrue(enemyTree.Contains("template_id = &\"melee_pressure\"", StringComparison.Ordinal), "enemy tree should represent the melee pressure branch from the C# planner");

    string alliedTree = File.ReadAllText(alliedTreePath);
    AssertTrue(alliedTree.Contains("has_battle_command.gd", StringComparison.Ordinal), "allied tree should branch on the C# command context through a task");
    AssertTrue(alliedTree.Contains("expected_command = \"HoldLine\"", StringComparison.Ordinal), "allied tree should include the hold-line command branch");
    AssertTrue(alliedTree.Contains("expected_command = \"FocusFire\"", StringComparison.Ordinal), "allied tree should include the focus-fire command branch");
    AssertTrue(alliedTree.Contains("expected_command = \"Assault\"", StringComparison.Ordinal), "allied tree should include the assault/default advance branch");
    AssertTrue(alliedTree.Contains("mode = \"lowest_health_hostile\"", StringComparison.Ordinal), "focus-fire branch should select the lowest-health hostile target");
    AssertTrue(alliedTree.Contains("template_id = &\"focus_strike\"", StringComparison.Ordinal), "focus-fire tree should emit focus strike when it can attack");
    AssertTrue(alliedTree.Contains("template_id = &\"focus_pressure\"", StringComparison.Ordinal), "focus-fire tree should emit focus pressure when it must advance");
    AssertTrue(alliedTree.Contains("template_id = &\"hold\"", StringComparison.Ordinal), "allied tree should keep an explicit hold fallback");

    string commandTask = File.ReadAllText(commandTaskPath);
    string rangedTask = File.ReadAllText(rangedTaskPath);
    string facade = File.ReadAllText(facadePath);
    AssertTrue(commandTask.Contains("agent.has_battle_command(expected_command, blackboard)", StringComparison.Ordinal), "command task should route command checks through the C# facade");
    AssertTrue(rangedTask.Contains("agent.has_ranged_battle_ability(ability_var, blackboard)", StringComparison.Ordinal), "ranged condition task should route ability shape checks through the C# facade");
    AssertTrue(facade.Contains("bool has_battle_command", StringComparison.Ordinal), "Godot-facing facade should expose the command condition used by allied trees");
    AssertTrue(facade.Contains("bool has_ranged_battle_ability", StringComparison.Ordinal), "Godot-facing facade should expose the ranged ability condition used by enemy trees");
}

internal static void LimboAiGodotFacadeRequiresBlackboardTargetBeforeStrike()
{
    string facadePath = Path.Combine("src", "Presentation", "Battle", "AI", "BattleAiFacade.cs");
    string facade = File.ReadAllText(facadePath);

    AssertTrue(
        facade.Contains("HasBlackboardValue(blackboard, targetVar)", StringComparison.Ordinal),
        "Godot-facing facade should require the selected target to exist in the LimboAI blackboard before allowing strike");
    AssertTrue(
        facade.Contains("HasBlackboardValue(blackboard, abilityVar)", StringComparison.Ordinal),
        "Godot-facing facade should require the selected ability to exist in the LimboAI blackboard before allowing strike");
}
}
