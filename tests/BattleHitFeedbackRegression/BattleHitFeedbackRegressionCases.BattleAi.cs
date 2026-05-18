using Rpg.Presentation.Battle.AI;
using Rpg.Presentation.Battle.Flow;

internal static partial class BattleHitFeedbackRegressionCases
{
internal static void LimboAiBattleDecisionTreeMirrorsEnemyGreedyBranches()
{
    BattleAiDecisionTreeRunner runner = new();

    AssertEqual(
        "ranged_pressure",
        runner.ChooseEnemyIntent(new BattleAiDecisionFacts
        {
            HasValidContext = true,
            ActorCanAct = true,
            HasTarget = true,
            HasPrimaryAbility = true,
            PrimaryAbilityRange = 3,
            PrimaryAbilityPower = 7,
            CanStrikeNow = false,
            MoveRange = 4
        }).TemplateId,
        "enemy ranged ability should prefer ranged pressure before direct strike");

    BattleAiDecisionResult directStrike = runner.ChooseEnemyIntent(new BattleAiDecisionFacts
    {
        HasValidContext = true,
        ActorCanAct = true,
        HasTarget = true,
        HasPrimaryAbility = true,
        PrimaryAbilityRange = 1,
        PrimaryAbilityPower = 5,
        CanStrikeNow = true,
        MoveRange = 4
    });

    AssertEqual("direct_strike", directStrike.TemplateId, "enemy melee ability in range should strike directly");
    AssertEqual(5, directStrike.Power, "direct strike should keep ability damage as the displayed power");

    BattleAiDecisionResult pressure = runner.ChooseEnemyIntent(new BattleAiDecisionFacts
    {
        HasValidContext = true,
        ActorCanAct = true,
        HasTarget = true,
        HasPrimaryAbility = true,
        PrimaryAbilityRange = 1,
        PrimaryAbilityPower = 5,
        CanStrikeNow = false,
        MoveRange = 3
    });

    AssertEqual("melee_pressure", pressure.TemplateId, "enemy out of melee range should pressure nearest target");
    AssertEqual(3, pressure.Power, "melee pressure should use movement range when the actor can move");
}

internal static void LimboAiBattleDecisionTreeMirrorsAlliedCommandBranches()
{
    BattleAiDecisionTreeRunner runner = new();

    BattleAiDecisionResult holdLineNoStrike = runner.ChooseAlliedIntent(
        new BattleAiDecisionFacts
        {
            HasValidContext = true,
            ActorCanAct = true,
            HasTarget = true,
            HasPrimaryAbility = true,
            PrimaryAbilityRange = 1,
            PrimaryAbilityPower = 6,
            CanStrikeNow = false,
            MoveRange = 4
        },
        BattleCorpsCommand.HoldLine);

    AssertEqual("hold", holdLineNoStrike.TemplateId, "hold-line command should not advance when it cannot strike");
    AssertEqual("执行坚守，保持阵线", holdLineNoStrike.Reason, "hold-line fallback should preserve the existing Chinese reason");

    AssertEqual(
        "focus_pressure",
        runner.ChooseAlliedIntent(
            new BattleAiDecisionFacts
            {
                HasValidContext = true,
                ActorCanAct = true,
                HasTarget = true,
                HasPrimaryAbility = true,
                PrimaryAbilityRange = 1,
                PrimaryAbilityPower = 6,
                CanStrikeNow = false,
                MoveRange = 4
            },
            BattleCorpsCommand.FocusFire).TemplateId,
        "focus-fire command should pressure the lowest-health target when not in range");

    AssertEqual(
        "focus_strike",
        runner.ChooseAlliedIntent(
            new BattleAiDecisionFacts
            {
                HasValidContext = true,
                ActorCanAct = true,
                HasTarget = true,
                HasPrimaryAbility = true,
                PrimaryAbilityRange = 1,
                PrimaryAbilityPower = 6,
                CanStrikeNow = true,
                MoveRange = 4
            },
            BattleCorpsCommand.FocusFire).TemplateId,
        "focus-fire command should strike the focused target when in range");
}

internal static void LimboAiBattleResourceBoundaryIsAuthored()
{
    string treePath = Path.Combine("assets", "ai", "battle", "battle_enemy_basic.tres");
    string hostPath = Path.Combine("scenes", "ai", "battle", "BattleAiAgentHost.tscn");
    string selectTaskPath = Path.Combine("scripts", "ai", "limbo_tasks", "battle", "select_battle_target.gd");
    string strikeConditionPath = Path.Combine("scripts", "ai", "limbo_tasks", "battle", "can_strike_battle_target.gd");
    string emitTaskPath = Path.Combine("scripts", "ai", "limbo_tasks", "battle", "emit_battle_intent.gd");

    AssertTrue(File.Exists(treePath), "LimboAI battle enemy tree resource should be authored under assets/ai/battle");
    AssertTrue(File.Exists(hostPath), "LimboAI battle host scene should be authored under scenes/ai/battle");
    AssertTrue(File.Exists(selectTaskPath), "LimboAI target selection task should exist");
    AssertTrue(File.Exists(strikeConditionPath), "LimboAI strike condition task should exist");
    AssertTrue(File.Exists(emitTaskPath), "LimboAI intent emission task should exist");

    string tree = File.ReadAllText(treePath);
    AssertTrue(tree.Contains("type=\"BehaviorTree\"", StringComparison.Ordinal), "battle tree should be a LimboAI BehaviorTree resource");
    AssertTrue(tree.Contains("select_battle_target.gd", StringComparison.Ordinal), "battle tree should use the target selection task");
    AssertTrue(tree.Contains("can_strike_battle_target.gd", StringComparison.Ordinal), "battle tree should use the strike condition task");
    AssertTrue(tree.Contains("emit_battle_intent.gd", StringComparison.Ordinal), "battle tree should emit an intent through a task");

    string selectTask = File.ReadAllText(selectTaskPath);
    string strikeCondition = File.ReadAllText(strikeConditionPath);
    string emitTask = File.ReadAllText(emitTaskPath);
    AssertTrue(selectTask.Contains("extends BTAction", StringComparison.Ordinal), "target selection should be a LimboAI action task");
    AssertTrue(strikeCondition.Contains("extends BTCondition", StringComparison.Ordinal), "strike range check should be a LimboAI condition task");
    AssertTrue(emitTask.Contains("extends BTAction", StringComparison.Ordinal), "intent emission should be a LimboAI action task");
    AssertTrue(
        selectTask.Contains("select_battle_target", StringComparison.Ordinal) &&
        strikeCondition.Contains("can_strike_battle_target", StringComparison.Ordinal) &&
        emitTask.Contains("emit_battle_intent", StringComparison.Ordinal),
        "LimboAI tasks should call the narrow C# facade methods instead of mutating runtime state directly");
}

internal static void LimboAiBattleFacadeWritesBlackboardAndEmitsIntent()
{
    Dictionary<string, object> blackboard = new(StringComparer.Ordinal);
    BattleAiFacadeCore facade = new();
    BattleAiDecisionFacts facts = new()
    {
        HasValidContext = true,
        ActorCanAct = true,
        HasTarget = true,
        HasPrimaryAbility = true,
        PrimaryAbilityId = "basic_attack",
        PrimaryAbilityRange = 1,
        PrimaryAbilityPower = 5,
        CanStrikeNow = true,
        MoveRange = 3,
        NearestHostileTargetId = "enemy_a",
        LowestHealthHostileTargetId = "enemy_b"
    };

    AssertTrue(
        facade.SelectBattleTarget(facts, "nearest_hostile", blackboard, "target_id"),
        "facade should select a nearest hostile target when facts expose one");
    AssertEqual("enemy_a", blackboard["target_id"], "nearest target should be written into the blackboard");
    AssertEqual("basic_attack", blackboard["ability_id"], "primary ability id should be written with target selection");
    AssertEqual(5, blackboard["intent_power"], "ability power should be written for emit tasks");

    AssertTrue(
        facade.CanStrikeBattleTarget(facts, blackboard, "target_id", "ability_id"),
        "facade should confirm strike legality from C# facts instead of GDScript rules");

    BattleAiDecisionResult result = facade.EmitBattleIntent(facts, "direct_strike", blackboard, "intent_power", "");

    AssertEqual("direct_strike", result.TemplateId, "facade should emit the requested intent template");
    AssertEqual(5, result.Power, "facade should preserve blackboard power when emitting intent");
}

internal static void LimboAiBattleTasksPassBlackboardThroughFacade()
{
    string host = File.ReadAllText(Path.Combine("scenes", "ai", "battle", "BattleAiAgentHost.tscn"));
    string selectTask = File.ReadAllText(Path.Combine("scripts", "ai", "limbo_tasks", "battle", "select_battle_target.gd"));
    string strikeCondition = File.ReadAllText(Path.Combine("scripts", "ai", "limbo_tasks", "battle", "can_strike_battle_target.gd"));
    string emitTask = File.ReadAllText(Path.Combine("scripts", "ai", "limbo_tasks", "battle", "emit_battle_intent.gd"));

    AssertTrue(
        host.Contains("BattleAiFacade.cs", StringComparison.Ordinal),
        "battle AI host root should use the C# facade script as the LimboAI agent");
    AssertTrue(
        selectTask.Contains("agent.select_battle_target(mode, target_var, blackboard)", StringComparison.Ordinal),
        "target task should pass the LimboAI blackboard into the C# facade");
    AssertTrue(
        strikeCondition.Contains("agent.can_strike_battle_target(target_var, ability_var, blackboard)", StringComparison.Ordinal),
        "strike condition should pass the LimboAI blackboard into the C# facade");
    AssertTrue(
        emitTask.Contains("agent.emit_battle_intent(template_id, power_var, reason, blackboard)", StringComparison.Ordinal),
        "emit task should pass the LimboAI blackboard into the C# facade");
}
}
