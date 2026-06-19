using Godot;
using Rpg.Infrastructure.Logging;

namespace Rpg.Presentation.Battle.Entities;

public partial class UnitAnimationComponent
{
    private bool _heldAnimationActive;
    private bool _heldAnimationWaitingForFrame;
    private string _heldAnimationName = "";
    private string _heldAnimationCue = "";
    private int _heldAnimationFrame;
    private int _heldAnimationFrameCount;

    public int ResolveChanneledSkillCastHoldFrame()
    {
        return System.Math.Max(0, AnimationSet?.ChanneledSkillCastHoldFrame ?? 2);
    }

    public bool PlaySkillCastHoldAtFrame(int holdFrame)
    {
        if (TryPlaySkillCastHoldAnimation(holdFrame))
        {
            return true;
        }

        string attackAnimation = ResolveAnimationName(AnimationSet?.AttackAnimation, "attack");
        return PlayAnimationUntilFrame(attackAnimation, "attack", holdFrame, restart: true);
    }

    public bool PlayAnimationUntilFrame(
        string animationName,
        string cue,
        int holdFrame,
        bool restart = true,
        double minimumTargetSeconds = 0)
    {
        string resolvedAnimationName = ResolveAnimationName(animationName, cue);
        if (_presentationPaused)
        {
            return true;
        }

        if (!TryResolveHeldAnimationFrame(
                resolvedAnimationName,
                cue,
                holdFrame,
                out int clampedFrame,
                out int frameCount))
        {
            return false;
        }

        if (!TryPlayAnimatedSprite(resolvedAnimationName, cue, restart, logMissing: true, minimumTargetSeconds))
        {
            CancelHeldAnimationPlayback();
            return false;
        }

        BeginHeldAnimation(resolvedAnimationName, cue, clampedFrame, frameCount);
        TryPauseHeldAnimationAtCurrentFrame();
        return true;
    }

    public bool ResumeHeldAnimationFromNextFrame()
    {
        if (!_heldAnimationActive && !_heldAnimationWaitingForFrame)
        {
            return false;
        }

        string animationName = _heldAnimationName;
        int frameCount = _heldAnimationFrameCount;
        int resumeFrame = System.Math.Clamp(_heldAnimationFrame + 1, 0, System.Math.Max(0, frameCount - 1));
        CancelHeldAnimationPlayback();

        if (_animatedSprite == null ||
            !GodotObject.IsInstanceValid(_animatedSprite) ||
            frameCount <= 0 ||
            !IsCurrentAnimation(animationName))
        {
            return false;
        }

        _animatedSprite.SetFrameAndProgress(resumeFrame, 0f);
        _animatedSprite.Play();
        GameLog.Trace(nameof(UnitAnimationComponent),
            $"Held sprite animation resumed entity={Entity?.EntityId} animation={animationName} resumeFrame={resumeFrame}");
        return true;
    }

    private bool TryPlaySkillCastHoldAnimation(int holdFrame)
    {
        string skillCastAnimation = ResolveAnimationName(AnimationSet?.SkillCastAnimation, "skill_cast");
        return HasPlayableAnimation(skillCastAnimation, "skill_cast") &&
               PlayAnimationUntilFrame(skillCastAnimation, "skill_cast", holdFrame, restart: true);
    }

    private bool TryResolveHeldAnimationFrame(
        string animationName,
        string cue,
        int holdFrame,
        out int clampedFrame,
        out int frameCount)
    {
        clampedFrame = 0;
        frameCount = 0;
        if (_animatedSprite == null)
        {
            ResolveAnimatedSprite();
            TryConnectSpriteAnimationFinished();
        }

        if (_animatedSprite == null)
        {
            LogMissingOnce($"sprite:{cue}", $"Unit animated sprite missing entity={Entity?.EntityId} cue={cue} path={AnimatedSpritePath}");
            return false;
        }

        SpriteFrames spriteFrames = _animatedSprite.SpriteFrames;
        if (spriteFrames == null)
        {
            LogMissingOnce($"sprite-frames:{cue}", $"Unit animated sprite has no SpriteFrames entity={Entity?.EntityId} cue={cue} path={_animatedSprite.GetPath()}");
            return false;
        }

        var spriteAnimationName = new StringName(animationName);
        if (!spriteFrames.HasAnimation(spriteAnimationName))
        {
            LogMissingOnce($"sprite-animation:{cue}:{animationName}", $"Unit animated sprite missing animation entity={Entity?.EntityId} cue={cue} animation={animationName} path={_animatedSprite.GetPath()}");
            return false;
        }

        frameCount = spriteFrames.GetFrameCount(spriteAnimationName);
        if (frameCount <= 0)
        {
            LogMissingOnce($"sprite-empty:{cue}:{animationName}", $"Unit animated sprite animation has no frames entity={Entity?.EntityId} cue={cue} animation={animationName} path={_animatedSprite.GetPath()}");
            return false;
        }

        clampedFrame = System.Math.Clamp(holdFrame, 0, frameCount - 1);
        return true;
    }

    private void BeginHeldAnimation(string animationName, string cue, int holdFrame, int frameCount)
    {
        _heldAnimationActive = true;
        _heldAnimationWaitingForFrame = true;
        _heldAnimationName = animationName ?? "";
        _heldAnimationCue = cue ?? "";
        _heldAnimationFrame = holdFrame;
        _heldAnimationFrameCount = frameCount;

        // Held casts own their release point. The normal one-shot idle timer
        // must not interrupt the middle frame while the Runtime channel is live.
        InvalidateOneShotReturn();
        GameLog.Trace(nameof(UnitAnimationComponent),
            $"Held sprite animation started entity={Entity?.EntityId} cue={_heldAnimationCue} animation={_heldAnimationName} holdFrame={_heldAnimationFrame} frames={_heldAnimationFrameCount}");
    }

    private void OnAnimatedSpriteFrameChanged()
    {
        if (!_heldAnimationWaitingForFrame)
        {
            return;
        }

        TryPauseHeldAnimationAtCurrentFrame();
    }

    private void TryPauseHeldAnimationAtCurrentFrame()
    {
        if (!_heldAnimationWaitingForFrame ||
            _animatedSprite == null ||
            !GodotObject.IsInstanceValid(_animatedSprite))
        {
            return;
        }

        if (!IsCurrentAnimation(_heldAnimationName))
        {
            CancelHeldAnimationPlayback();
            return;
        }

        if (_animatedSprite.Frame >= _heldAnimationFrame)
        {
            _animatedSprite.SetFrameAndProgress(_heldAnimationFrame, 0f);
            _animatedSprite.Pause();
            _heldAnimationWaitingForFrame = false;
            GameLog.Trace(nameof(UnitAnimationComponent),
                $"Held sprite animation paused entity={Entity?.EntityId} cue={_heldAnimationCue} animation={_heldAnimationName} holdFrame={_heldAnimationFrame}");
        }
    }

    private void CancelHeldAnimationPlayback()
    {
        _heldAnimationActive = false;
        _heldAnimationWaitingForFrame = false;
        _heldAnimationName = "";
        _heldAnimationCue = "";
        _heldAnimationFrame = 0;
        _heldAnimationFrameCount = 0;
    }
}
