using Rpg.Runtime.Battle.AI;
using Rpg.Runtime.Battle.Navigation;

namespace Rpg.Runtime.Battle;

internal sealed class BattleRuntimeActionProposal
{
    public BattleRuntimeAiActionRequest Request { get; init; }
    public BattleRuntimeActor Actor { get; init; }
    public BattleRuntimeActor Target { get; init; }
    public BattleRuntimeAiActionKind Kind => Request?.Kind ?? BattleRuntimeAiActionKind.Hold;
    public BattleGridCoord ActorStart { get; init; }
    public BattleGridCoord TargetStart { get; init; }
    public BattleGridCoord MoveTo { get; init; }
    public bool HasMoveTo { get; init; }
    public string FailureReason { get; init; } = "";
}
