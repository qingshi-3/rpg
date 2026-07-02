namespace Rpg.Application.Battle.Snapshots;

public sealed class BattleSkillGrantSnapshot
{
    public string GrantedSkillId { get; set; } = "";
    public string LoadoutSlotId { get; set; } = "";
    public string OwnerHeroId { get; set; } = "";
    public string OwnerBattleGroupId { get; set; } = "";
    public string RuntimeCommanderGroupId { get; set; } = "";
    public string SkillDefinitionId { get; set; } = "";
    public string SourceKind { get; set; } = "";
    public string SourceId { get; set; } = "";
    public int SkillLevel { get; set; } = 1;
}
