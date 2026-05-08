using Rpg.Presentation.Battle.Entities;

namespace Rpg.Presentation.Battle.UI;

public sealed record BattleTurnQueueEntry(
    string EntityId,
    string DisplayName,
    BattleFaction Faction,
    int Hp,
    int MaxHp,
    int Ap,
    int MaxAp,
    bool IsActive,
    bool IsDefeated);
