namespace Rpg.Domain.BattleGroups;

public sealed class BattleGroupState
{
    public string BattleGroupId { get; set; } = "";
    public string HeroId { get; set; } = "";
    public string CorpsId { get; set; } = "";
    public string CurrentLocationId { get; set; } = "";
    public BattleGroupStatus Status { get; set; } = BattleGroupStatus.Available;
    public string ActiveBattleId { get; set; } = "";

    public bool CanSortie => Status is BattleGroupStatus.Available or BattleGroupStatus.Stationed;
}
