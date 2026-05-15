using System;
using System.Collections.Generic;
using Godot;
using Rpg.Infrastructure.Logging;

namespace Rpg.Presentation.World;

public partial class StrategicWorldFogOverlay : Control
{
    private const string FogShaderPath = "res://assets/world/shaders/strategic_fog_of_war.gdshader";
    private const int MaxVisibleCircles = 32;
    private const int MinExploredMaskResolution = 256;
    private const int MaxExploredMaskResolution = 1024;
    private static readonly StringName UnknownColorParameter = "unknown_color";
    private static readonly StringName RevealedColorParameter = "revealed_color";
    private static readonly StringName ExploredMaskParameter = "explored_mask";
    private static readonly StringName OverlaySizeParameter = "overlay_size";
    private static readonly StringName MapRectParameter = "map_rect";
    private static readonly StringName VisibleCircleCountParameter = "visible_circle_count";
    private static readonly StringName VisibleCirclesParameter = "visible_circles";
    private static readonly StringName EdgeSoftnessParameter = "edge_softness";
    private static readonly StringName ExploredMaskTextureSizeParameter = "explored_mask_texture_size";

    private readonly ColorRect _shaderRect = new()
    {
        Name = "StrategicWorldFogShaderRect",
        Color = Colors.Transparent,
        Visible = false,
        MouseFilter = MouseFilterEnum.Ignore
    };

    private ShaderMaterial _material;
    private ImageTexture _emptyExploredMask;

    public StrategicWorldFogOverlay()
    {
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        EnsureShaderRect();
        UpdateShaderRectBounds();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
        {
            UpdateShaderRectBounds();
            UpdateOverlaySizeParameter();
        }
    }

    public void SetFog(
        Rect2 screenBounds,
        IEnumerable<StrategicWorldFogOverlayRect> revealedRects,
        IEnumerable<StrategicWorldFogOverlayCircle> visibleCircles,
        float fogTexelScreenSize,
        Color unknownColor)
    {
        EnsureShaderRect();
        if (_material == null)
        {
            return;
        }

        Rect2 bounds = NormalizeRect(screenBounds);
        Color revealedColor = ResolveRevealedColor(revealedRects);
        Vector2I exploredMaskSize = EstimateExploredMaskSize(bounds, MathF.Max(1.0f, fogTexelScreenSize));
        ImageTexture exploredMask = BuildExploredMask(
            bounds,
            revealedRects,
            exploredMaskSize);
        Vector4[] circleParameters = BuildCircleParameters(visibleCircles, out int circleCount);

        _material.SetShaderParameter(UnknownColorParameter, unknownColor);
        _material.SetShaderParameter(RevealedColorParameter, revealedColor);
        _material.SetShaderParameter(ExploredMaskParameter, exploredMask ?? GetEmptyExploredMask());
        _material.SetShaderParameter(
            ExploredMaskTextureSizeParameter,
            new Vector2(exploredMaskSize.X, exploredMaskSize.Y));
        _material.SetShaderParameter(MapRectParameter, new Vector4(bounds.Position.X, bounds.Position.Y, bounds.Size.X, bounds.Size.Y));
        _material.SetShaderParameter(VisibleCircleCountParameter, circleCount);
        _material.SetShaderParameter(VisibleCirclesParameter, circleParameters);
        _material.SetShaderParameter(EdgeSoftnessParameter, 5.0f);
        UpdateOverlaySizeParameter();
        _shaderRect.Visible = true;
    }

    public void SetVisibleCircles(IEnumerable<StrategicWorldFogOverlayCircle> visibleCircles)
    {
        EnsureShaderRect();
        if (_material == null)
        {
            return;
        }

        Vector4[] circleParameters = BuildCircleParameters(visibleCircles, out int circleCount);
        _material.SetShaderParameter(VisibleCircleCountParameter, circleCount);
        _material.SetShaderParameter(VisibleCirclesParameter, circleParameters);
        UpdateOverlaySizeParameter();
        _shaderRect.Visible = true;
    }

    public void ClearFog()
    {
        EnsureShaderRect();
        if (_material != null)
        {
            _material.SetShaderParameter(VisibleCircleCountParameter, 0);
            _material.SetShaderParameter(ExploredMaskParameter, GetEmptyExploredMask());
            _material.SetShaderParameter(ExploredMaskTextureSizeParameter, new Vector2(1.0f, 1.0f));
        }

        _shaderRect.Visible = false;
    }

    private void EnsureShaderRect()
    {
        if (_shaderRect.GetParent() == null)
        {
            AddChild(_shaderRect);
        }

        if (_material != null)
        {
            return;
        }

        Shader shader = GD.Load<Shader>(FogShaderPath);
        if (shader == null)
        {
            GameLog.Warn(nameof(StrategicWorldFogOverlay), $"Strategic fog shader missing path={FogShaderPath}");
            return;
        }

        _material = new ShaderMaterial
        {
            Shader = shader
        };
        _shaderRect.Material = _material;
    }

    private void UpdateShaderRectBounds()
    {
        _shaderRect.Position = Vector2.Zero;
        _shaderRect.Size = Size;
    }

    private void UpdateOverlaySizeParameter()
    {
        _material?.SetShaderParameter(OverlaySizeParameter, Size);
    }

    private static ImageTexture BuildExploredMask(
        Rect2 bounds,
        IEnumerable<StrategicWorldFogOverlayRect> revealedRects,
        Vector2I maskSize)
    {
        if (bounds.Size.X <= 0.0f || bounds.Size.Y <= 0.0f)
        {
            return null;
        }

        int width = Mathf.Max(1, maskSize.X);
        int height = Mathf.Max(1, maskSize.Y);
        Image image = Image.CreateEmpty(width, height, false, Image.Format.R8);
        image.Fill(Colors.Black);
        if (revealedRects != null)
        {
            foreach (StrategicWorldFogOverlayRect rect in revealedRects)
            {
                FillMaskSoftCircle(image, bounds, NormalizeRect(rect.ScreenRect));
            }
        }

        return ImageTexture.CreateFromImage(image);
    }

    private static void FillMaskSoftCircle(Image image, Rect2 bounds, Rect2 rect)
    {
        int resolutionX = image.GetWidth();
        int resolutionY = image.GetHeight();
        Rect2 clipped = bounds.Intersection(rect);
        if (clipped.Size.X <= 0.0f || clipped.Size.Y <= 0.0f)
        {
            return;
        }

        Vector2 rectCenter = rect.GetCenter();
        float radius = MathF.Max(rect.Size.X, rect.Size.Y) * 0.5f;
        if (radius <= 0.0f)
        {
            return;
        }

        float centerX = (rectCenter.X - bounds.Position.X) / bounds.Size.X * resolutionX;
        float centerY = (rectCenter.Y - bounds.Position.Y) / bounds.Size.Y * resolutionY;
        float radiusX = radius / bounds.Size.X * resolutionX;
        float radiusY = radius / bounds.Size.Y * resolutionY;
        const float featherPixels = 1.5f;
        int startX = Mathf.Clamp(Mathf.FloorToInt(centerX - radiusX - featherPixels), 0, resolutionX - 1);
        int endX = Mathf.Clamp(Mathf.CeilToInt(centerX + radiusX + featherPixels), 0, resolutionX);
        int startY = Mathf.Clamp(Mathf.FloorToInt(centerY - radiusY - featherPixels), 0, resolutionY - 1);
        int endY = Mathf.Clamp(Mathf.CeilToInt(centerY + radiusY + featherPixels), 0, resolutionY);

        for (int y = startY; y < endY; y++)
        {
            for (int x = startX; x < endX; x++)
            {
                float px = x + 0.5f;
                float py = y + 0.5f;
                float normalizedX = radiusX <= 0.0f ? 0.0f : (px - centerX) / radiusX;
                float normalizedY = radiusY <= 0.0f ? 0.0f : (py - centerY) / radiusY;
                float distance = (Mathf.Sqrt(normalizedX * normalizedX + normalizedY * normalizedY) - 1.0f) * MathF.Max(radiusX, radiusY);
                distance = MathF.Max(0.0f, distance);
                if (distance > featherPixels)
                {
                    continue;
                }

                float amount = 1.0f - SmoothStep(0.0f, featherPixels, distance);
                float current = image.GetPixel(x, y).R;
                if (amount > current)
                {
                    image.SetPixel(x, y, new Color(amount, 0.0f, 0.0f));
                }
            }
        }
    }

    private static float SmoothStep(float edge0, float edge1, float value)
    {
        float t = Mathf.Clamp((value - edge0) / (edge1 - edge0), 0.0f, 1.0f);
        return t * t * (3.0f - 2.0f * t);
    }

    private static int ComputeExploredMaskResolution(
        float axisLength,
        float fogTexelScreenSize)
    {
        float clampedCellSize = Mathf.Max(1.0f, fogTexelScreenSize);
        float target = axisLength / clampedCellSize;
        if (!float.IsFinite(target) || target <= 0.0f)
        {
            return MinExploredMaskResolution;
        }

        return Mathf.Clamp(
            Mathf.CeilToInt(target),
            MinExploredMaskResolution,
            MaxExploredMaskResolution);
    }

    private static Vector2I EstimateExploredMaskSize(
        Rect2 bounds,
        float fogTexelScreenSize)
    {
        return new Vector2I(
            ComputeExploredMaskResolution(bounds.Size.X, fogTexelScreenSize),
            ComputeExploredMaskResolution(bounds.Size.Y, fogTexelScreenSize));
    }

    private static Vector4[] BuildCircleParameters(
        IEnumerable<StrategicWorldFogOverlayCircle> visibleCircles,
        out int circleCount)
    {
        Vector4[] parameters = new Vector4[MaxVisibleCircles];
        circleCount = 0;
        if (visibleCircles == null)
        {
            return parameters;
        }

        foreach (StrategicWorldFogOverlayCircle circle in visibleCircles)
        {
            if (circleCount >= MaxVisibleCircles)
            {
                break;
            }

            if (circle.ScreenRadius <= 0.0f)
            {
                continue;
            }

            parameters[circleCount++] = new Vector4(circle.ScreenCenter.X, circle.ScreenCenter.Y, circle.ScreenRadius, 0.0f);
        }

        return parameters;
    }

    private static Color ResolveRevealedColor(IEnumerable<StrategicWorldFogOverlayRect> revealedRects)
    {
        if (revealedRects == null)
        {
            return new Color(0.025f, 0.03f, 0.035f, 0.42f);
        }

        foreach (StrategicWorldFogOverlayRect rect in revealedRects)
        {
            return rect.Color;
        }

        return new Color(0.025f, 0.03f, 0.035f, 0.42f);
    }

    private ImageTexture GetEmptyExploredMask()
    {
        if (_emptyExploredMask != null)
        {
            return _emptyExploredMask;
        }

        Image image = Image.CreateEmpty(1, 1, false, Image.Format.R8);
        image.Fill(Colors.Black);
        _emptyExploredMask = ImageTexture.CreateFromImage(image);
        return _emptyExploredMask;
    }

    private static Rect2 NormalizeRect(Rect2 rect)
    {
        Vector2 start = new(
            Mathf.Min(rect.Position.X, rect.Position.X + rect.Size.X),
            Mathf.Min(rect.Position.Y, rect.Position.Y + rect.Size.Y));
        Vector2 end = new(
            Mathf.Max(rect.Position.X, rect.Position.X + rect.Size.X),
            Mathf.Max(rect.Position.Y, rect.Position.Y + rect.Size.Y));
        return new Rect2(start, end - start);
    }
}

public readonly record struct StrategicWorldFogOverlayRect(Rect2 ScreenRect, Color Color);

public readonly record struct StrategicWorldFogOverlayCircle(Vector2 ScreenCenter, float ScreenRadius);
