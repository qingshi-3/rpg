using Godot;

namespace Rpg.Presentation.Battle;

internal readonly record struct BattleGridHighlightStyle(
    Color Fill,
    Color Border,
    float BorderWidth,
    BattleGridHighlightTileShape Shape = BattleGridHighlightTileShape.Diamond);

internal enum BattleGridHighlightTileShape
{
    Diamond,
    Square,
    SoftAura
}
