using Rpg.Application.Battle.Commands;
using Rpg.Domain.Battle.Grid;

namespace Rpg.Presentation.World.Sites;

internal static class BattleRuntimeHeroSkillCommandRequestFactory
{
    internal static CommandRequest BuildHeroSkillCommandRequest(
        string groupKey,
        string battleId,
        string skillId,
        string sourceActorId,
        string targetActorId,
        GridPosition? targetGrid,
        string selectedSpatialMarkId = "")
    {
        return new CommandRequest
        {
            CommandId = $"hero_skill:{groupKey ?? ""}:{skillId ?? ""}",
            BattleId = battleId ?? "",
            BattleGroupId = groupKey ?? "",
            SourceActorId = sourceActorId ?? "",
            Channel = CommandChannel.Hero,
            Kind = CommandKind.CastSkill,
            SkillId = skillId ?? "",
            TargetActorId = targetActorId ?? "",
            HasTargetGrid = targetGrid.HasValue,
            TargetGridX = targetGrid?.X ?? 0,
            TargetGridY = targetGrid?.Y ?? 0,
            TargetGridHeight = 0,
            SelectedSpatialMarkId = selectedSpatialMarkId ?? ""
        };
    }
}
