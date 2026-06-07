using System.Collections.Generic;
using Rpg.Runtime.Battle.Events;

namespace Rpg.Runtime.Battle;

public sealed class BattleRuntimeCommandSubmitResult
{
    public bool Accepted { get; init; }
    public string ReasonCode { get; init; } = "";
    public IReadOnlyList<BattleEvent> Events { get; init; } = System.Array.Empty<BattleEvent>();
}
