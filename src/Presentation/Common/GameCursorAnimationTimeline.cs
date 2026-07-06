using System;

namespace Rpg.Presentation.Common;

public sealed class GameCursorAnimationTimeline
{
    private readonly int _frameCount;
    private readonly double _frameDurationSeconds;
    private double _startedAtSeconds = double.NaN;

    public GameCursorAnimationTimeline(int frameCount, double framesPerSecond)
    {
        if (frameCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frameCount), "Cursor animation needs at least one frame.");
        }

        if (framesPerSecond <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(framesPerSecond), "Cursor animation speed must be positive.");
        }

        _frameCount = frameCount;
        _frameDurationSeconds = 1.0 / framesPerSecond;
    }

    public void Start(double nowSeconds)
    {
        _startedAtSeconds = nowSeconds;
    }

    public bool IsActive(double nowSeconds)
    {
        return ResolveFrameIndex(nowSeconds) >= 0;
    }

    public int ResolveFrameIndex(double nowSeconds)
    {
        if (double.IsNaN(_startedAtSeconds) || nowSeconds < _startedAtSeconds)
        {
            return -1;
        }

        double elapsedSeconds = nowSeconds - _startedAtSeconds;
        int frameIndex = (int)Math.Floor(elapsedSeconds / _frameDurationSeconds);
        return frameIndex >= 0 && frameIndex < _frameCount
            ? frameIndex
            : -1;
    }
}
