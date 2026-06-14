namespace Rpg.Runtime.Battle;

public sealed class BattleRuntimeSpatialMark
{
    public string MarkId { get; set; } = "";
    public string OwnerBattleGroupId { get; set; } = "";
    public string SourceActorId { get; set; } = "";
    public string SourceCommandId { get; set; } = "";
    public string SourceDefinitionId { get; set; } = "";
    public string AttachedActorId { get; set; } = "";
    public bool HasGroundAnchor { get; set; }
    public int GridX { get; set; }
    public int GridY { get; set; }
    public int GridHeight { get; set; }
    public double CreatedAtSeconds { get; set; }
    public double ExpiresAtSeconds { get; set; }
}
