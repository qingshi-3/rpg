using System.Collections.Generic;
using Godot;

namespace Rpg.Presentation.Battle;

internal sealed class BattleGridHighlightTileSetSpec
{
    public BattleGridHighlightTileSetSpec(
        TileSet tileSet,
        IReadOnlyDictionary<BattleGridHighlightKind, BattleGridHighlightTile> tilesByKind)
    {
        TileSet = tileSet;
        TilesByKind = tilesByKind;
    }

    public TileSet TileSet { get; }
    public IReadOnlyDictionary<BattleGridHighlightKind, BattleGridHighlightTile> TilesByKind { get; }
}

