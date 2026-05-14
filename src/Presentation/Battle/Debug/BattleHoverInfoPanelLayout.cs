using Godot;

namespace Rpg.Presentation.Battle.Debug;

public static class BattleHoverInfoPanelLayout
{
    public static Vector2 CalculateRightDockedPosition(Vector2 viewportSize, Vector2 panelSize, Vector2 margin)
    {
        float edgePadding = Mathf.Max(0f, margin.X);
        float preferredTop = Mathf.Max(0f, margin.Y);
        float rightX = viewportSize.X - panelSize.X - edgePadding;
        float maxX = Mathf.Max(edgePadding, viewportSize.X - panelSize.X - edgePadding);
        float maxY = Mathf.Max(edgePadding, viewportSize.Y - panelSize.Y - edgePadding);

        return new Vector2(
            Mathf.Clamp(rightX, edgePadding, maxX),
            Mathf.Clamp(preferredTop, edgePadding, maxY));
    }
}
