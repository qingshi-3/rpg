using System.Linq;
using Rpg.Application.Battle.Commands;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.Events;

internal static partial class TargetBattleHeroSkillRegressionCases
{
    internal static void TargetedHeroSkillRequiresExplicitSkillIdAtSubmission()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_hero_skill_requires_skill_id", enemyStrength: 40);
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);

        BattleRuntimeCommandSubmitResult submit = controller.SubmitCommand(new CommandRequest
        {
            CommandId = "cmd_hero_skill_requires_skill_id",
            BattleId = "battle_hero_skill_requires_skill_id",
            BattleGroupId = "group_player",
            Channel = CommandChannel.Hero,
            Kind = CommandKind.CastSkill,
            SkillId = "",
            TargetActorId = EnemyActorId
        });

        AssertTrue(!submit.Accepted, "hero skill command without explicit skill id should be rejected");
        AssertTrue(
            submit.Events.Any(item =>
                item.Kind == BattleEventKind.CommandRejected &&
                item.SourceCommandId == "cmd_hero_skill_requires_skill_id" &&
                item.ReasonCode == "skill_id_required"),
            "missing skill id rejection should enter event stream without defaulting to first-slice skill");
        AssertTrue(
            submit.Events.All(item => item.SourceDefinitionId != FirstSliceSkillId),
            "missing skill id must not be normalized to the first-slice skill definition");
    }
}
