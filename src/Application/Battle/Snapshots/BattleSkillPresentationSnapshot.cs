namespace Rpg.Application.Battle.Snapshots;

public sealed class BattleSkillPresentationSnapshot
{
    public string ProfileId { get; set; } = "";
    public string CastFxProfileId { get; set; } = "";
    public string ImpactFxProfileId { get; set; } = "";
    public string MarkFxProfileId { get; set; } = "";
    public string AreaFxProfileId { get; set; } = "";
    public bool SuppressActorCastFx { get; set; }
    public bool HoldCastAnimationDuringAction { get; set; }
}
