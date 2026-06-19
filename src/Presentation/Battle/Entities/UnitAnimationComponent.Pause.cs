using Godot;

namespace Rpg.Presentation.Battle.Entities;

public partial class UnitAnimationComponent
{
    private bool _presentationPaused;
    private bool _pausedAnimatedSpriteWasPlaying;
    private float _pausedAnimatedSpriteSpeedScale = 1f;
    private bool _pausedDefeatedFadeTweenWasRunning;

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
            PauseDefeatedFadeTweenPlayback();
            return;
        }

        ResumeAnimatedSpritePlayback();
        ResumeDefeatedFadeTweenPlayback();
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

    private void PauseDefeatedFadeTweenPlayback()
    {
        if (_defeatedFadeTween == null || !GodotObject.IsInstanceValid(_defeatedFadeTween))
        {
            _pausedDefeatedFadeTweenWasRunning = false;
            return;
        }

        _pausedDefeatedFadeTweenWasRunning = _defeatedFadeTween.IsRunning();
        if (_pausedDefeatedFadeTweenWasRunning)
        {
            _defeatedFadeTween.Pause();
        }
    }

    private void ResumeDefeatedFadeTweenPlayback()
    {
        if (_defeatedFadeTween == null || !GodotObject.IsInstanceValid(_defeatedFadeTween))
        {
            _pausedDefeatedFadeTweenWasRunning = false;
            return;
        }

        if (_pausedDefeatedFadeTweenWasRunning)
        {
            _defeatedFadeTween.Play();
        }

        _pausedDefeatedFadeTweenWasRunning = false;
    }
}
