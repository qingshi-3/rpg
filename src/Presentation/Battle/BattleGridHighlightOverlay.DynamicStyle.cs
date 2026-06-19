using Godot;
using Rpg.Application.Battle;

namespace Rpg.Presentation.Battle;

public partial class BattleGridHighlightOverlay
{
    private void ApplyDynamicRangeStyle(CanvasItem item, BattleGridHighlightKind kind)
    {
        if (!ShouldAnimateOverlay(kind) || item == null || !IsInsideTree())
        {
            return;
        }

        float minAlpha = Mathf.Clamp(DynamicPulseMinAlphaMultiplier, 0.1f, 1f);
        double pulseSeconds = System.Math.Max(0.2, DynamicPulseSeconds);
        item.Modulate = new Color(1f, 1f, 1f, 1f);

        Tween tween = CreateTween();
        tween.BindNode(item);
        tween.SetLoops();
        tween.SetTrans(Tween.TransitionType.Sine);
        tween.SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(item, "modulate", new Color(1f, 1f, 1f, minAlpha), pulseSeconds);
        tween.TweenProperty(item, "modulate", Colors.White, pulseSeconds);
    }

    private bool ShouldPulse(BattleGridHighlightKind kind)
    {
        return EnableDynamicRangeStyle &&
               ((kind == BattleGridHighlightKind.Threat && PulseThreatHighlights) ||
                (kind == BattleGridHighlightKind.Attack && PulseAttackHighlights) ||
                (kind == BattleGridHighlightKind.FriendlyAttack && PulseAttackHighlights) ||
                (kind == BattleGridHighlightKind.Target && PulseTargetHighlights) ||
                (kind == BattleGridHighlightKind.Skill && PulseSkillHighlights));
    }

    private bool ShouldAnimateOverlay(BattleGridHighlightKind kind)
    {
        // Tactical pause keeps overlay data responsive to player input but makes
        // every battlefield-space hint static; pause readability belongs to UI
        // state or a later pause filter, not battle-time animation.
        return !_tacticalPauseVisualsStatic && ShouldPulse(kind);
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        return new Color(color.R, color.G, color.B, alpha);
    }
}
