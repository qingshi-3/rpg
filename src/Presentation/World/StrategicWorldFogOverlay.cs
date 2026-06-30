using System;
using System.Collections.Generic;
using System.Diagnostics;
using Godot;
using Rpg.Application.World;
using Rpg.Infrastructure.Logging;

namespace Rpg.Presentation.World;

public partial class StrategicWorldFogOverlay : Control
{
	private const string FogShaderPath = "res://assets/world/shaders/strategic_fog_of_war.gdshader";
	private const int MaxVisibleCircles = 32;
	private const int MinExploredMaskResolution = 256;
	private const int MaxExploredMaskResolution = 1024;
	private const long FullMaskRebuildWarningMilliseconds = 12;
	private const int IncrementalUpdateLargeCellWarningThreshold = 128;
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
	// Legacy explored-history fields remain only for compatibility with older
	// callers; the active strategic fog path no longer uses them.
	private ImageTexture _emptyExploredMask;
	private Image _exploredMaskImage;
	private ImageTexture _exploredMaskTexture;
	private Rect2 _exploredMaskMapBounds;
	private Vector2I _exploredMaskOriginCell;
	private Vector2I _exploredMaskTextureSize;
	private float _exploredMaskFogTexelWorldSize;
	private Rect2 _fogMapBounds;
	private bool _hasFogMapBounds;

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
		IEnumerable<StrategicWorldFogOverlayCircle> visibleCircles,
		Color unknownColor)
	{
		EnsureShaderRect();
		if (_material == null)
		{
			return;
		}

		Rect2 bounds = NormalizeRect(screenBounds);
		ApplyFogSurfaceBounds(bounds);
		Vector4[] circleParameters = BuildCircleParameters(visibleCircles, -bounds.Position, out int circleCount);
		ApplyBinaryFogShaderParameters(new Rect2(Vector2.Zero, bounds.Size), unknownColor, circleCount, circleParameters);
		_shaderRect.Visible = true;
	}

	public void SetFog(
		Rect2 screenBounds,
		Rect2 mapBounds,
		IEnumerable<string> exploredCellKeys,
		IEnumerable<StrategicWorldFogOverlayCircle> visibleCircles,
		StrategicFogOfWarSettings settings,
		Color unknownColor)
	{
		EnsureShaderRect();
		if (_material == null)
		{
			return;
		}

		Stopwatch stopwatch = Stopwatch.StartNew();
		Rect2 bounds = NormalizeRect(screenBounds);
		Rect2 normalizedMapBounds = NormalizeRect(mapBounds);
		EnsureMapSpaceMask(normalizedMapBounds, settings, forceRebuild: true);
		RebuildExploredMaskTexture(exploredCellKeys);
		Vector4[] circleParameters = BuildCircleParameters(visibleCircles, out int circleCount);
		ApplyFogShaderParameters(bounds, unknownColor, ResolveRevealedColor(), GetExploredMaskTexture(), _exploredMaskTextureSize, circleCount, circleParameters);
		_shaderRect.Visible = true;
		stopwatch.Stop();
		if (stopwatch.ElapsedMilliseconds >= FullMaskRebuildWarningMilliseconds)
		{
			GameLog.Info(
				nameof(StrategicWorldFogOverlay),
				$"StrategicFogFullMaskRebuildCost elapsedMs={stopwatch.ElapsedMilliseconds} exploredCells={CountItems(exploredCellKeys)} maskSize={_exploredMaskTextureSize}");
		}
	}

	public bool UpdateExploredMaskIncremental(
		Rect2 screenBounds,
		Rect2 mapBounds,
		IEnumerable<string> newlyExploredCells,
		IEnumerable<StrategicWorldFogOverlayCircle> visibleCircles,
		StrategicFogOfWarSettings settings,
		Color unknownColor)
	{
		EnsureShaderRect();
		if (_material == null)
		{
			return false;
		}

		Rect2 bounds = NormalizeRect(screenBounds);
		Rect2 normalizedMapBounds = NormalizeRect(mapBounds);
		if (!CanReuseMapSpaceMask(normalizedMapBounds, settings))
		{
			return false;
		}

		int newCellCount = UpdateExploredMaskCells(newlyExploredCells);
		if (newCellCount <= 0)
		{
			Vector4[] visibleCircleParameters = BuildCircleParameters(visibleCircles, out int visibleCircleCount);
			ApplyFogShaderParameters(bounds, unknownColor, ResolveRevealedColor(), GetExploredMaskTexture(), _exploredMaskTextureSize, visibleCircleCount, visibleCircleParameters);
			_shaderRect.Visible = true;
			return true;
		}

		CommitExploredMaskTexture();
		Vector4[] committedVisibleCircleParameters = BuildCircleParameters(visibleCircles, out int committedVisibleCircleCount);
		ApplyFogShaderParameters(bounds, unknownColor, ResolveRevealedColor(), GetExploredMaskTexture(), _exploredMaskTextureSize, committedVisibleCircleCount, committedVisibleCircleParameters);
		_shaderRect.Visible = true;
		if (newCellCount >= IncrementalUpdateLargeCellWarningThreshold)
		{
			GameLog.Info(
				nameof(StrategicWorldFogOverlay),
				$"StrategicFogIncrementalMaskLargeUpdate newCells={newCellCount} maskSize={_exploredMaskTextureSize}");
		}

		return true;
	}

	public void SetVisibleCircles(IEnumerable<StrategicWorldFogOverlayCircle> visibleCircles)
	{
		EnsureShaderRect();
		if (_material == null)
		{
			return;
		}

		Vector4[] circleParameters = BuildCircleParameters(visibleCircles, GetFogCircleOffset(), out int circleCount);
		_material.SetShaderParameter(VisibleCircleCountParameter, circleCount);
		_material.SetShaderParameter(VisibleCirclesParameter, circleParameters);
		UpdateOverlaySizeParameter();
		_shaderRect.Visible = true;
	}

	public void UpdateFogScreenTransform(
		Rect2 screenBounds,
		IEnumerable<StrategicWorldFogOverlayCircle> visibleCircles)
	{
		EnsureShaderRect();
		if (_material == null)
		{
			return;
		}

		Rect2 bounds = NormalizeRect(screenBounds);
		ApplyFogSurfaceBounds(bounds);
		Vector4[] circleParameters = BuildCircleParameters(visibleCircles, -bounds.Position, out int circleCount);
		_material.SetShaderParameter(MapRectParameter, new Vector4(0.0f, 0.0f, bounds.Size.X, bounds.Size.Y));
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

		_exploredMaskImage = null;
		_exploredMaskTexture = null;
		_exploredMaskMapBounds = default;
		_exploredMaskOriginCell = default;
		_exploredMaskTextureSize = default;
		_exploredMaskFogTexelWorldSize = 0.0f;
		_fogMapBounds = default;
		_hasFogMapBounds = false;
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

	private void ApplyFogSurfaceBounds(Rect2 bounds)
	{
		// The fog overlay is a map-space surface under the transformed world overlay.
		// Size it to the strategic map, not the camera viewport; SubViewport clipping
		// still keeps shader raster cost bounded to visible screen pixels.
		_fogMapBounds = bounds;
		_hasFogMapBounds = bounds.Size.X > 0.0f && bounds.Size.Y > 0.0f;
		SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
		Position = bounds.Position;
		Size = new Vector2(Mathf.Max(1.0f, bounds.Size.X), Mathf.Max(1.0f, bounds.Size.Y));
		UpdateShaderRectBounds();
		UpdateOverlaySizeParameter();
	}

	private Vector2 GetFogCircleOffset()
	{
		return _hasFogMapBounds ? -_fogMapBounds.Position : Vector2.Zero;
	}

	private void EnsureMapSpaceMask(
		Rect2 mapBounds,
		StrategicFogOfWarSettings settings,
		bool forceRebuild)
	{
		float fogTexelWorldSize = ResolveFogTexelWorldSize(settings);
		Vector2I maskSize = EstimateExploredMaskSize(mapBounds, fogTexelWorldSize);
		if (!forceRebuild && CanReuseMapSpaceMask(mapBounds, settings))
		{
			return;
		}

		EnsureExploredMaskTexture(maskSize);
		_exploredMaskMapBounds = mapBounds;
		_exploredMaskFogTexelWorldSize = fogTexelWorldSize;
		_exploredMaskOriginCell = new Vector2I(
			Mathf.FloorToInt(mapBounds.Position.X / fogTexelWorldSize),
			Mathf.FloorToInt(mapBounds.Position.Y / fogTexelWorldSize));
		_exploredMaskTextureSize = maskSize;
	}

	private void EnsureExploredMaskTexture(Vector2I maskSize)
	{
		Vector2I safeSize = new(Mathf.Max(2, maskSize.X), Mathf.Max(2, maskSize.Y));
		if (_exploredMaskImage != null && _exploredMaskTexture != null && _exploredMaskTextureSize == safeSize)
		{
			return;
		}

		_exploredMaskImage = Image.CreateEmpty(safeSize.X, safeSize.Y, false, Image.Format.R8);
		_exploredMaskImage.Fill(Colors.Black);
		_exploredMaskTexture = ImageTexture.CreateFromImage(_exploredMaskImage);
	}

	private bool CanReuseMapSpaceMask(
		Rect2 mapBounds,
		StrategicFogOfWarSettings settings)
	{
		return _exploredMaskImage != null &&
			   _exploredMaskTexture != null &&
			   IsSameRect(_exploredMaskMapBounds, mapBounds) &&
			   MathF.Abs(_exploredMaskFogTexelWorldSize - ResolveFogTexelWorldSize(settings)) <= 0.001f;
	}

	private Texture2D GetExploredMaskTexture()
	{
		if (_exploredMaskTexture != null)
		{
			return _exploredMaskTexture;
		}

		return GetEmptyExploredMask();
	}

	private void RebuildExploredMaskTexture(
		IEnumerable<string> exploredCellKeys)
	{
		if (_exploredMaskImage == null)
		{
			return;
		}

		_exploredMaskImage.Fill(Colors.Black);
		UpdateExploredMaskCells(exploredCellKeys);
		CommitExploredMaskTexture();
	}

	private int UpdateExploredMaskCells(
		IEnumerable<string> exploredCellKeys)
    {
		int updatedCells = 0;
		if (_exploredMaskImage == null || exploredCellKeys == null)
		{
			return updatedCells;
		}

		foreach (string cellKey in exploredCellKeys)
		{
			if (SetExploredCell(cellKey))
			{
				updatedCells++;
			}
		}

		return updatedCells;
	}

	private bool SetExploredCell(
		string cellKey)
	{
		if (_exploredMaskImage == null || string.IsNullOrWhiteSpace(cellKey))
		{
			return false;
		}

		Vector2I pixel = CellKeyToMaskPixel(cellKey);
		if (pixel.X < 0 || pixel.Y < 0 || pixel.X >= _exploredMaskImage.GetWidth() || pixel.Y >= _exploredMaskImage.GetHeight())
		{
			return false;
		}

		_exploredMaskImage.SetPixel(pixel.X, pixel.Y, Colors.White);
		return true;
	}

	private Vector2I CellKeyToMaskPixel(
		string cellKey)
	{
		Vector2I cell = ParseCellKey(cellKey);
		int pixelX = cell.X - _exploredMaskOriginCell.X;
		int pixelY = cell.Y - _exploredMaskOriginCell.Y;
		return new Vector2I(pixelX, pixelY);
	}

	private void CommitExploredMaskTexture()
	{
		if (_exploredMaskImage == null)
		{
			return;
		}

		if (_exploredMaskTexture == null)
		{
			_exploredMaskTexture = ImageTexture.CreateFromImage(_exploredMaskImage);
			return;
		}

		_exploredMaskTexture.Update(_exploredMaskImage);
	}

	private static int ComputeExploredMaskResolution(
		float axisLength,
		float fogTexelWorldSize)
	{
		float clampedCellSize = Mathf.Max(1.0f, fogTexelWorldSize);
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
		float fogTexelWorldSize)
	{
		return new Vector2I(
			ComputeExploredMaskResolution(bounds.Size.X, fogTexelWorldSize),
			ComputeExploredMaskResolution(bounds.Size.Y, fogTexelWorldSize));
	}

	private static Vector4[] BuildCircleParameters(
		IEnumerable<StrategicWorldFogOverlayCircle> visibleCircles,
		out int circleCount)
	{
		return BuildCircleParameters(visibleCircles, Vector2.Zero, out circleCount);
	}

	private static Vector4[] BuildCircleParameters(
		IEnumerable<StrategicWorldFogOverlayCircle> visibleCircles,
		Vector2 circleOffset,
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

			Vector2 center = circle.ScreenCenter + circleOffset;
			parameters[circleCount++] = new Vector4(center.X, center.Y, circle.ScreenRadius, 0.0f);
		}

		return parameters;
	}

	private void ApplyFogShaderParameters(
		Rect2 bounds,
		Color unknownColor,
		Color revealedColor,
		Texture2D exploredMask,
		Vector2I exploredMaskSize,
		int circleCount,
		Vector4[] circleParameters)
	{
		_material.SetShaderParameter(UnknownColorParameter, unknownColor);
		_material.SetShaderParameter(RevealedColorParameter, revealedColor);
		_material.SetShaderParameter(ExploredMaskParameter, exploredMask);
		_material.SetShaderParameter(
			ExploredMaskTextureSizeParameter,
			new Vector2(Mathf.Max(1, exploredMaskSize.X), Mathf.Max(1, exploredMaskSize.Y)));
		_material.SetShaderParameter(MapRectParameter, new Vector4(bounds.Position.X, bounds.Position.Y, bounds.Size.X, bounds.Size.Y));
		_material.SetShaderParameter(VisibleCircleCountParameter, circleCount);
		_material.SetShaderParameter(VisibleCirclesParameter, circleParameters);
		_material.SetShaderParameter(EdgeSoftnessParameter, 5.0f);
		UpdateOverlaySizeParameter();
	}

	private void ApplyBinaryFogShaderParameters(
		Rect2 bounds,
		Color unknownColor,
		int circleCount,
		Vector4[] circleParameters)
	{
		_material.SetShaderParameter(UnknownColorParameter, unknownColor);
		_material.SetShaderParameter(MapRectParameter, new Vector4(bounds.Position.X, bounds.Position.Y, bounds.Size.X, bounds.Size.Y));
		_material.SetShaderParameter(VisibleCircleCountParameter, circleCount);
		_material.SetShaderParameter(VisibleCirclesParameter, circleParameters);
		_material.SetShaderParameter(EdgeSoftnessParameter, 5.0f);
		UpdateOverlaySizeParameter();
	}

	private static Color ResolveRevealedColor()
	{
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

	private static bool IsSameRect(Rect2 left, Rect2 right)
	{
		return left.Position.DistanceSquaredTo(right.Position) <= 0.001f &&
			   left.Size.DistanceSquaredTo(right.Size) <= 0.001f;
	}

	private static Vector2I ParseCellKey(string cellKey)
	{
		string[] parts = (cellKey ?? "").Split(':');
		return parts.Length == 2 &&
		       int.TryParse(parts[0], out int x) &&
		       int.TryParse(parts[1], out int y)
			? new Vector2I(x, y)
			: Vector2I.Zero;
	}

	private static float ResolveFogTexelWorldSize(StrategicFogOfWarSettings settings)
	{
		return Mathf.Max(settings?.FogTexelWorldSize ?? StrategicFogOfWarService.DefaultFogTexelWorldSize, 1.0f);
	}

	private static int CountItems<T>(IEnumerable<T> items)
	{
		if (items == null)
		{
			return 0;
		}

		if (items is ICollection<T> collection)
		{
			return collection.Count;
		}

		int count = 0;
		foreach (T _ in items)
		{
			count++;
		}

		return count;
	}
}

public readonly record struct StrategicWorldFogOverlayRect(Rect2 ScreenRect, Color Color);

public readonly record struct StrategicWorldFogOverlayCircle(Vector2 ScreenCenter, float ScreenRadius);
