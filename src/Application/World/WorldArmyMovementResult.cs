using System.Collections.Generic;

namespace Rpg.Application.World;

public sealed class WorldArmyMovementResult
{
    public List<GameEvent> Events { get; set; } = new();
    public List<string> Messages { get; set; } = new();
    public List<string> ArrivedArmyIds { get; set; } = new();
    public List<string> BattleReadyArmyIds { get; set; } = new();
    public List<WorldArmyInterceptResult> FieldIntercepts { get; set; } = new();
    public List<string> NavigationBlockedArmyIds { get; set; } = new();
    public List<string> GarrisonRejectedArmyIds { get; set; } = new();
}
