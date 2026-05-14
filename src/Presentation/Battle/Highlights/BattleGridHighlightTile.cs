using Godot;

namespace Rpg.Presentation.Battle;

internal readonly record struct BattleGridHighlightTile(int SourceId, Vector2I AtlasCoords, int AlternativeTile = 0);

