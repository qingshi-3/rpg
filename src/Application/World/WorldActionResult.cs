using System.Collections.Generic;
using Rpg.Application.Battle;

namespace Rpg.Application.World;

public sealed class WorldActionResult
{
    public bool Success { get; set; }
    public string ActionId { get; set; } = "";
    public string Message { get; set; } = "";
    public string FailureReason { get; set; } = "";
    public BattleStartRequest BattleStartRequest { get; set; }
    public List<GameEvent> Events { get; set; } = new();

    public static WorldActionResult Failed(string actionId, string reason, string message = "")
    {
        return new WorldActionResult
        {
            Success = false,
            ActionId = actionId ?? "",
            FailureReason = reason ?? "",
            Message = string.IsNullOrWhiteSpace(message) ? reason ?? "" : message
        };
    }
}
