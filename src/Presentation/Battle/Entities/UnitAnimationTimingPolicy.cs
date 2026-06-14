using Godot;
using Rpg.Application.Battle;
using Rpg.Definitions.Battle.Animation;

namespace Rpg.Presentation.Battle.Entities;

internal static class UnitAnimationTimingPolicy
{
    // Presentation cue pacing is deliberately separate from Runtime action time;
    // Runtime remains the authority for attack legality, impact, and recovery.
    internal static void ApplyAnimatedSpriteLoopMode(SpriteFrames spriteFrames, StringName animationName, string cue)
    {
        if (spriteFrames == null)
        {
            return;
        }

        if (cue is "attack" or "skill_cast" or "hit" or "defeated")
        {
            spriteFrames.SetAnimationLoop(animationName, false);
        }
        else if (cue is "idle" or "move")
        {
            spriteFrames.SetAnimationLoop(animationName, true);
        }
    }

    internal static bool ShouldReturnToIdleAfterCue(string cue)
    {
        return cue is "attack" or "skill_cast" or "hit";
    }

    internal static double ResolveTargetSpriteSeconds(
        string cue,
        BattleUnitAnimationSet animationSet,
        double attackSpeed)
    {
        return ResolveTargetSpriteSeconds(cue, minimumTargetSeconds: 0, animationSet, attackSpeed);
    }

    internal static double ResolveTargetSpriteSeconds(
        string cue,
        double minimumTargetSeconds,
        BattleUnitAnimationSet animationSet,
        double attackSpeed)
    {
        double targetSeconds = cue switch
        {
            "idle" => animationSet?.TargetIdleCycleSeconds ?? 2.0,
            "move" => animationSet?.TargetMoveCycleSeconds ?? 0.55,
            "attack" => animationSet?.TargetAttackSeconds ?? 1.2,
            "skill_cast" => animationSet?.TargetSkillCastSeconds ?? 1.5,
            "hit" => animationSet?.TargetHitSeconds ?? 0.48,
            "defeated" => animationSet?.TargetDefeatedSeconds ?? 0.4,
            _ => 0
        };

        return cue is "hit" or "defeated"
            ? System.Math.Max(targetSeconds, minimumTargetSeconds)
            : cue == "attack"
                ? BattleAttackSpeedPolicy.ScaleTargetSeconds(targetSeconds, attackSpeed)
                : targetSeconds;
    }

    internal static float ResolveAnimationPlayerSpeedScale(string cue, double attackSpeed)
    {
        return cue == "attack"
            ? (float)BattleAttackSpeedPolicy.Normalize(attackSpeed)
            : 1f;
    }

    internal static double ScaleAnimationSecondsByAttackSpeed(double seconds, string cue, double attackSpeed)
    {
        return cue == "attack"
            ? BattleAttackSpeedPolicy.ScaleTargetSeconds(seconds, attackSpeed)
            : seconds;
    }
}
