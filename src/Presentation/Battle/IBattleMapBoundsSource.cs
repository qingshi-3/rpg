using System;
using Godot;

namespace Rpg.Presentation.Battle;

public interface IBattleMapBoundsSource
{
    Node ActiveBattleMap { get; }
    event Action<Node> BattleMapLoaded;
}
