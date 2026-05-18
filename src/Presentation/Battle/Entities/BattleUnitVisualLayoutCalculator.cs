using Godot;
using System.Collections.Generic;

namespace Rpg.Presentation.Battle.Entities;

public readonly struct BattleUnitVisualLayout
{
    public BattleUnitVisualLayout(
        Vector2 scale,
        Vector2 position,
        Vector2 visibleSize,
        Vector2 visibleCenterOffset,
        float scaledHeight)
    {
        Scale = scale;
        Position = position;
        VisibleSize = visibleSize;
        VisibleCenterOffset = visibleCenterOffset;
        ScaledHeight = scaledHeight;
    }

    public Vector2 Scale { get; }

    public Vector2 Position { get; }

    public Vector2 VisibleSize { get; }

    public Vector2 VisibleCenterOffset { get; }

    public float ScaledHeight { get; }
}

public static class BattleUnitVisualLayoutCalculator
{
    public static bool TryCalculateAutoLayout(
        SpriteFrames spriteFrames,
        float targetMaxSpriteSizePixels,
        float groundAnchorOffsetPixels,
        float visibleAlphaThreshold,
        out BattleUnitVisualLayout layout)
    {
        layout = default;
        if (spriteFrames == null || targetMaxSpriteSizePixels <= 0)
        {
            return false;
        }

        bool hasVisibleBounds = false;
        Rect2 visibleBounds = default;
        Vector2 frameSize = Vector2.Zero;
        foreach (StringName animationName in EnumerateLayoutAnimationNames(spriteFrames))
        {
            int frameCount = spriteFrames.GetFrameCount(animationName);
            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                Texture2D texture = spriteFrames.GetFrameTexture(animationName, frameIndex);
                if (texture == null)
                {
                    continue;
                }

                if (!TryGetVisibleTextureBounds(
                        texture,
                        visibleAlphaThreshold,
                        out Rect2 frameVisibleBounds,
                        out Vector2 currentFrameSize))
                {
                    continue;
                }

                frameSize.X = System.Math.Max(frameSize.X, currentFrameSize.X);
                frameSize.Y = System.Math.Max(frameSize.Y, currentFrameSize.Y);
                visibleBounds = hasVisibleBounds
                    ? MergeBounds(visibleBounds, frameVisibleBounds)
                    : frameVisibleBounds;
                hasVisibleBounds = true;
            }
        }

        if (!hasVisibleBounds || frameSize.X <= 0f || frameSize.Y <= 0f)
        {
            return false;
        }

        Vector2 visibleSize = visibleBounds.Size;
        float sourceMax = System.Math.Max(visibleSize.X, visibleSize.Y);
        if (sourceMax <= 0f || visibleSize.Y <= 0f)
        {
            return false;
        }

        Vector2 visibleCenter = visibleBounds.Position + visibleBounds.Size / 2f;
        Vector2 frameCenter = frameSize / 2f;
        Vector2 visibleCenterOffset = visibleCenter - frameCenter;
        float uniformScale = targetMaxSpriteSizePixels / sourceMax;
        float scaledHeight = visibleSize.Y * uniformScale;
        float upwardOffset = System.Math.Max(0f, scaledHeight / 2f - groundAnchorOffsetPixels);
        Vector2 scale = new(uniformScale, uniformScale);
        Vector2 position = new(
            -visibleCenterOffset.X * uniformScale,
            -upwardOffset - visibleCenterOffset.Y * uniformScale);

        layout = new BattleUnitVisualLayout(
            scale,
            position,
            visibleSize,
            visibleCenterOffset,
            scaledHeight);
        return true;
    }

    private static IEnumerable<StringName> EnumerateLayoutAnimationNames(SpriteFrames spriteFrames)
    {
        foreach (string preferredName in new[] { "idle", "breathing", "move" })
        {
            var animationName = new StringName(preferredName);
            if (spriteFrames.HasAnimation(animationName))
            {
                yield return animationName;
                yield break;
            }
        }

        foreach (StringName animationName in spriteFrames.GetAnimationNames())
        {
            yield return animationName;
        }
    }

    private static Rect2 MergeBounds(Rect2 a, Rect2 b)
    {
        float left = System.Math.Min(a.Position.X, b.Position.X);
        float top = System.Math.Min(a.Position.Y, b.Position.Y);
        float right = System.Math.Max(a.Position.X + a.Size.X, b.Position.X + b.Size.X);
        float bottom = System.Math.Max(a.Position.Y + a.Size.Y, b.Position.Y + b.Size.Y);
        return new Rect2(left, top, right - left, bottom - top);
    }

    private static bool TryGetVisibleTextureBounds(
        Texture2D texture,
        float alphaThreshold,
        out Rect2 visibleBounds,
        out Vector2 frameSize)
    {
        visibleBounds = default;
        frameSize = Vector2.Zero;
        if (texture == null)
        {
            return false;
        }

        Image image;
        int startX;
        int startY;
        int width;
        int height;
        if (texture is AtlasTexture atlasTexture && atlasTexture.Atlas != null)
        {
            image = atlasTexture.Atlas.GetImage();
            Rect2 region = atlasTexture.Region;
            startX = System.Math.Max(0, (int)System.Math.Floor(region.Position.X));
            startY = System.Math.Max(0, (int)System.Math.Floor(region.Position.Y));
            width = System.Math.Max(0, (int)System.Math.Ceiling(region.Size.X));
            height = System.Math.Max(0, (int)System.Math.Ceiling(region.Size.Y));
        }
        else
        {
            image = texture.GetImage();
            startX = 0;
            startY = 0;
            Vector2 size = texture.GetSize();
            width = System.Math.Max(0, (int)System.Math.Ceiling(size.X));
            height = System.Math.Max(0, (int)System.Math.Ceiling(size.Y));
        }

        if (image == null || width <= 0 || height <= 0)
        {
            return false;
        }

        int imageWidth = image.GetWidth();
        int imageHeight = image.GetHeight();
        int endX = System.Math.Min(imageWidth, startX + width);
        int endY = System.Math.Min(imageHeight, startY + height);
        if (endX <= startX || endY <= startY)
        {
            return false;
        }

        int minX = width;
        int minY = height;
        int maxX = -1;
        int maxY = -1;
        for (int y = startY; y < endY; y++)
        {
            for (int x = startX; x < endX; x++)
            {
                if (image.GetPixel(x, y).A <= alphaThreshold)
                {
                    continue;
                }

                int localX = x - startX;
                int localY = y - startY;
                minX = System.Math.Min(minX, localX);
                minY = System.Math.Min(minY, localY);
                maxX = System.Math.Max(maxX, localX);
                maxY = System.Math.Max(maxY, localY);
            }
        }

        frameSize = new Vector2(width, height);
        if (maxX < minX || maxY < minY)
        {
            return false;
        }

        visibleBounds = new Rect2(minX, minY, maxX - minX + 1, maxY - minY + 1);
        return true;
    }
}
