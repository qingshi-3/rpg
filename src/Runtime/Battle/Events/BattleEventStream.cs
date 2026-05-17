using System.Collections.Generic;
using System.Linq;

namespace Rpg.Runtime.Battle.Events;

public sealed class BattleEventStream
{
    private readonly List<BattleEvent> _events = new();

    public static BattleEventStream Empty => new();
    public IReadOnlyList<BattleEvent> Events => _events;
    public IReadOnlyList<string> EventIds => _events.Select(item => item.EventId).ToList();

    public void Add(BattleEvent battleEvent)
    {
        if (battleEvent != null)
        {
            _events.Add(battleEvent);
        }
    }
}
