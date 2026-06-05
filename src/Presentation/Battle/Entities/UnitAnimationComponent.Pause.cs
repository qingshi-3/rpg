using Godot;

namespace Rpg.Presentation.Battle.Entities;

public partial class UnitAnimationComponent
{
    private bool _presentationPaused;
    private bool _pausedAnimatedSpriteWasPlaying;
    private float _pausedAnimatedSpriteSpeedScale = 1f;
    private bool _pausedAnimationPlayerWasPlaying;
    private float _pausedAnimationPlayerSpeedScale = 1f;
    private bool _pausedProceduralTweenWasRunning;

    public void SetPresentationPaused(bool paused)
    {
        if (_presentationPaused == paused)
        {
            return;
        }

        _presentationPaused = paused;
        if (paused)
        {
            PauseAnimatedSpritePlayback();
            PauseAnimationPlayerPlayback();
            PauseProceduralTweenPlayback();
            return;
        }

        ResumeAnimatedSpritePlayback();
        ResumeAnimationPlayerPlayback();
        ResumeProceduralTweenPlayback();
    }

    private void PauseAnimatedSpritePlayback()
    {
        if (_animatedSprite == null)
        {
            ResolveAnimatedSprite();
        }

        if (_animatedSprite == null || !GodotObject.IsInstanceValid(_animatedSprite))
        {
            _pausedAnimatedSpriteWasPlaying = false;
            return;
        }

        _pausedAnimatedSpriteSpeedScale = _animatedSprite.SpeedScale;
        _pausedAnimatedSpriteWasPlaying = _animatedSprite.IsPlaying();
        if (_pausedAnimatedSpriteWasPlaying)
        {
            _animatedSprite.Pause();
        }
    }

    private void ResumeAnimatedSpritePlayback()
    {
        if (_animatedSprite == null || !GodotObject.IsInstanceValid(_animatedSprite))
        {
            _pausedAnimatedSpriteWasPlaying = false;
            return;
        }

        _animatedSprite.SpeedScale = _pausedAnimatedSpriteSpeedScale;
        if (_pausedAnimatedSpriteWasPlaying)
        {
            _animatedSprite.Play();
        }

        _pausedAnimatedSpriteWasPlaying = false;
    }

    private void PauseAnimationPlayerPlayback()
    {
        if (_animationPlayer == null)
        {
            ResolveAnimationPlayer();
        }

        if (_animationPlayer == null || !GodotObject.IsInstanceValid(_animationPlayer))
        {
            _pausedAnimationPlayerWasPlaying = false;
            return;
        }

        _pausedAnimationPlayerSpeedScale = _animationPlayer.SpeedScale;
        _pausedAnimationPlayerWasPlaying = _animationPlayer.IsPlaying();
        if (_pausedAnimationPlayerWasPlaying)
        {
            _animationPlayer.Pause();
        }
    }

    private void ResumeAnimationPlayerPlayback()
    {
        if (_animationPlayer == null || !GodotObject.IsInstanceValid(_animationPlayer))
        {
            _pausedAnimationPlayerWasPlaying = false;
            return;
        }

        _animationPlayer.SpeedScale = _pausedAnimationPlayerSpeedScale;
        if (_pausedAnimationPlayerWasPlaying)
        {
            _animationPlayer.Play();
        }

        _pausedAnimationPlayerWasPlaying = false;
    }

    private void PauseProceduralTweenPlayback()
    {
        if (_proceduralTween == null || !GodotObject.IsInstanceValid(_proceduralTween))
        {
            _pausedProceduralTweenWasRunning = false;
            return;
        }

        _pausedProceduralTweenWasRunning = _proceduralTween.IsRunning();
        if (_pausedProceduralTweenWasRunning)
        {
            _proceduralTween.Pause();
        }
    }

    private void ResumeProceduralTweenPlayback()
    {
        if (_proceduralTween == null || !GodotObject.IsInstanceValid(_proceduralTween))
        {
            _pausedProceduralTweenWasRunning = false;
            return;
        }

        if (_pausedProceduralTweenWasRunning)
        {
            _proceduralTween.Play();
        }

        _pausedProceduralTweenWasRunning = false;
    }
}
