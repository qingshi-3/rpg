using System.Linq;
using Rpg.Application.Battle.Commands;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.Events;

internal static partial class TargetBattleThunderMarkSkillRegressionCases
{
    internal static void RuntimeThunderSpiralDamagesSelectedDirectionalArea()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot(
            "battle_thunder_spiral_directional_area",
            enemyStrength: 100,
            enemyCellX: 2,
            enemyCellY: 1);
        AddThunderSpiralSkill(snapshot);
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);
        AddEnemyBehindCaster(controller);
        FreezeAutonomousCorps(controller);

        CommandRequest spiralRequest = new()
        {
            CommandId = "cmd_thunder_spiral_directional_area",
            BattleId = "battle_thunder_spiral_directional_area",
            BattleGroupId = "group_player",
            SourceActorId = "group_player:hero",
            Channel = CommandChannel.Hero,
            Kind = CommandKind.CastSkill,
            SkillDefinitionId = ThunderSpiralBreakSkillId
        };
        SetCommandTargetGrid(spiralRequest, x: 2, y: 0, height: 0);

        BattleRuntimeCommandSubmitResult spiral = controller.SubmitCommand(spiralRequest);
        AssertTrue(spiral.Accepted, $"thunder spiral should accept the selected direction center reason={spiral.ReasonCode}");

        BattleRuntimeAdvanceResult advance = controller.AdvanceFixedTick();

        AssertDamageEvent(
            advance,
            "cmd_thunder_spiral_directional_area",
            EnemyActorId,
            expectedActorGridX: 0,
            expectedDamage: 9,
            $"right-direction spiral should hit the selected front 3x3 area events={DescribeEvents(advance)}");
        AssertEqual(91, EnemyCorps(controller).HitPoints, "front-right enemy HP after thunder spiral");
        AssertEqual(80, NearEnemy(controller).HitPoints, "enemy behind the caster should not be hit by the right-direction 3x3 area");
        AssertTrue(
            advance.Events.All(item =>
                item.Kind != BattleEventKind.DamageApplied ||
                item.TargetId != NearEnemyActorId ||
                item.SourceCommandId != "cmd_thunder_spiral_directional_area"),
            "right-direction thunder spiral should not emit damage on a behind-caster target");
    }

    private static void AddEnemyBehindCaster(BattleRuntimeSessionController controller)
    {
        controller.State.Actors.Add(new BattleRuntimeActor
        {
            ActorId = NearEnemyActorId,
            BattleGroupId = "group_enemy",
            FactionId = "enemy",
            SourceForceId = "force_enemy_behind",
            SourceStateId = "corps_enemy_behind",
            Kind = BattleRuntimeActorKind.Corps,
            HitPoints = 80,
            GridX = -1,
            GridY = 0,
            GridHeight = 0,
            Position = -1
        });
    }
}
