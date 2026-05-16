namespace Rpg.Application.Battle.Auto;

public sealed class AutoBattleCombatant
{
    internal AutoBattleCombatant(
        string combatantId,
        string forceId,
        string sourceKind,
        string sourceId,
        string unitDefinitionId,
        string factionId,
        bool isPlayerSide,
        int cellX,
        int cellY,
        int cellHeight,
        int maxHealth)
    {
        CombatantId = combatantId ?? "";
        ForceId = forceId ?? "";
        SourceKind = sourceKind ?? "";
        SourceId = sourceId ?? "";
        UnitDefinitionId = unitDefinitionId ?? "";
        FactionId = factionId ?? "";
        IsPlayerSide = isPlayerSide;
        CellX = cellX;
        CellY = cellY;
        CellHeight = cellHeight;
        MaxHealth = System.Math.Max(1, maxHealth);
        Health = MaxHealth;
    }

    public string CombatantId { get; }
    public string ForceId { get; }
    public string SourceKind { get; }
    public string SourceId { get; }
    public string UnitDefinitionId { get; }
    public string FactionId { get; }
    public bool IsPlayerSide { get; }
    public int CellX { get; internal set; }
    public int CellY { get; internal set; }
    public int CellHeight { get; internal set; }
    public int MaxHealth { get; }
    public int Health { get; internal set; }
    public bool IsDefeated => Health <= 0;
    internal string CurrentTargetId { get; set; } = "";
    internal int AttackCooldownTicksRemaining { get; set; }
}
