using Godot;

namespace Rpg.Presentation.World.Sites;

internal static class BattleRuntimeCommandHudPointerGate
{
    internal static bool ContainsPointer(Control control, Vector2 globalPosition) =>
        control != null &&
        control.Visible &&
        control.IsInsideTree() &&
        control.GetGlobalRect().HasPoint(globalPosition);
}
