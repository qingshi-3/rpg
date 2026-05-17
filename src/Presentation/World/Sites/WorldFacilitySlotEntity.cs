using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Rpg.Presentation.World.Sites;

public partial class WorldFacilitySlotEntity : Node2D
{
	private static readonly Color BuildableFill = new(0.42f, 0.86f, 0.52f, 0.94f);
	private static readonly Color OccupiedFill = new(0.86f, 0.69f, 0.32f, 0.96f);
	private static readonly Color DisabledFill = new(0.35f, 0.37f, 0.40f, 0.82f);
	private static readonly Color ErrorFill = new(1.0f, 0.16f, 0.12f, 0.94f);
	private static readonly Color SelectedRing = new(1.0f, 1.0f, 1.0f, 0.96f);
	private static readonly Color ShadowFill = new(0.0f, 0.0f, 0.0f, 0.48f);

	private Node2D _emptyVisual;
	private Node2D _buildingVisual;
	private Node2D _builtVisualRoot;
	private Label _hintLabel;
	private readonly List<Vector2[]> _footprintPolygonsGlobal = new();

	[Export]
	public string SlotId { get; set; } = "";

	[Export(PropertyHint.Range, "1,12,1")]
	public int FootprintWidth { get; set; } = 1;

	[Export(PropertyHint.Range, "1,12,1")]
	public int FootprintHeight { get; set; } = 1;

	[Export]
	public NodePath EmptyVisualPath { get; set; } = new("EmptyVisual");

	[Export]
	public NodePath BuildingVisualPath { get; set; } = new("BuildingVisual");

	[Export]
	public NodePath BuiltVisualRootPath { get; set; } = new("BuiltVisualRoot");

	[Export]
	public NodePath HintLabelPath { get; set; } = new("HintLabel");

	[Export]
	public bool UseLowestFootprintCellAsSortAnchor { get; set; } = true;

	[Export]
	public bool DrawSlotMarker { get; set; } = true;

	public bool IsOccupied { get; private set; }
	public bool IsBuilding { get; private set; }
	public bool CanBuild { get; private set; }
	public bool CanInteract { get; private set; } = true;
	public bool IsSelected { get; private set; }
	public bool HasConfigurationError { get; private set; }
	public string ConfigurationError { get; private set; } = "";
	public string HintText { get; private set; } = "";

	public override void _Ready()
	{
		_emptyVisual = GetNodeOrNull<Node2D>(EmptyVisualPath);
		_buildingVisual = GetNodeOrNull<Node2D>(BuildingVisualPath);
		_builtVisualRoot = GetNodeOrNull<Node2D>(BuiltVisualRootPath);
		_hintLabel = GetNodeOrNull<Label>(HintLabelPath);

		RefreshVisualVisibility();
	}

	public override void _Draw()
	{
		if (!DrawSlotMarker)
		{
			return;
		}

		Color fill = HasConfigurationError
			? ErrorFill
			: IsOccupied
				? OccupiedFill
				: CanBuild
					? BuildableFill
					: DisabledFill;

		IReadOnlyList<Vector2[]> polygons = ResolveFootprintPolygons();
		for (int index = 0; index < polygons.Count; index++)
		{
			DrawSlotCell(polygons[index], fill);
			if (index == 0)
			{
				DrawSlotIcon(GetPolygonCenter(polygons[index]), fill);
			}
		}
	}

	public void ApplySnappedLayout(Vector2 rootGlobalPosition)
	{
		GlobalPosition = rootGlobalPosition;
	}

	public void SetFootprintPolygons(IReadOnlyList<Vector2[]> globalPolygons)
	{
		_footprintPolygonsGlobal.Clear();
		if (globalPolygons != null)
		{
			foreach (Vector2[] polygon in globalPolygons)
			{
				if (polygon is { Length: >= 3 })
				{
					_footprintPolygonsGlobal.Add(polygon.ToArray());
				}
			}
		}

		QueueRedraw();
	}

	public void BindState(
		string slotId,
		bool isOccupied,
		bool isBuilding,
		bool canBuild,
		bool canInteract,
		bool isSelected,
		string configurationError = "",
		string hintText = "")
	{
		SlotId = string.IsNullOrWhiteSpace(slotId) ? Name : slotId;
		IsOccupied = isOccupied;
		IsBuilding = isBuilding;
		CanBuild = canBuild;
		CanInteract = canInteract;
		IsSelected = isSelected;
		ConfigurationError = configurationError ?? "";
		HasConfigurationError = !string.IsNullOrWhiteSpace(ConfigurationError);
		HintText = hintText ?? "";

		RefreshVisualVisibility();
		QueueRedraw();
	}

	private void RefreshVisualVisibility()
	{
		if (_emptyVisual != null)
		{
			_emptyVisual.Visible = !IsOccupied && !IsBuilding;
		}

		if (_buildingVisual != null)
		{
			_buildingVisual.Visible = IsBuilding;
		}

		if (_builtVisualRoot != null)
		{
			_builtVisualRoot.Visible = IsOccupied && !IsBuilding;
		}

		if (_hintLabel != null)
		{
			_hintLabel.Text = HintText;
			_hintLabel.Visible = !string.IsNullOrWhiteSpace(HintText);
		}
	}

	private IReadOnlyList<Vector2[]> ResolveFootprintPolygons()
	{
		if (_footprintPolygonsGlobal.Count > 0)
		{
			return _footprintPolygonsGlobal
				.Select(polygon => polygon.Select(ToLocal).ToArray())
				.ToList();
		}

		return System.Array.Empty<Vector2[]>();
	}

	private void DrawSlotCell(Vector2[] polygon, Color fill)
	{
		if (polygon == null || polygon.Length < 3)
		{
			return;
		}

		Vector2 center = GetPolygonCenter(polygon);
		Vector2[] shadow = polygon
			.Select(point => point + new Vector2(0.0f, 2.0f))
			.ToArray();
		Vector2[] closed = ClosePolygon(polygon);

		Color body = new(fill.R, fill.G, fill.B, HasConfigurationError ? 0.34f : 0.24f);
		Color border = HasConfigurationError ? ErrorFill : fill;
		DrawColoredPolygon(shadow, ShadowFill);
		DrawColoredPolygon(polygon, body);
		DrawPolyline(closed, border, IsOccupied ? 2.0f : 1.25f, true);

		if (IsSelected)
		{
			Vector2[] selected = polygon
				.Select(point => center + (point - center) * 1.18f)
				.ToArray();
			DrawPolyline(ClosePolygon(selected), SelectedRing, 1.75f, true);
		}
	}

	private static Vector2[] ClosePolygon(Vector2[] polygon)
	{
		Vector2[] closed = new Vector2[polygon.Length + 1];
		polygon.CopyTo(closed, 0);
		closed[^1] = polygon[0];
		return closed;
	}

	private static Vector2 GetPolygonCenter(Vector2[] polygon)
	{
		if (polygon == null || polygon.Length == 0)
		{
			return Vector2.Zero;
		}

		Vector2 sum = Vector2.Zero;
		foreach (Vector2 point in polygon)
		{
			sum += point;
		}

		return sum / polygon.Length;
	}

	private void DrawSlotIcon(Vector2 center, Color fill)
	{
		Color iconColor = fill.Lightened(0.28f);
		Vector2 topLeft = center + new Vector2(-9.0f, -6.0f);
		Vector2 topRight = center + new Vector2(9.0f, -6.0f);
		Vector2 bottomRight = center + new Vector2(9.0f, 6.0f);
		Vector2 bottomLeft = center + new Vector2(-9.0f, 6.0f);
		DrawLine(topLeft, topRight, iconColor, 2.25f, true);
		DrawLine(topRight, bottomRight, iconColor, 2.25f, true);
		DrawLine(bottomRight, bottomLeft, iconColor, 2.25f, true);
		DrawLine(bottomLeft, topLeft, iconColor, 2.25f, true);
		DrawLine(center + new Vector2(-5.0f, 0.0f), center + new Vector2(5.0f, 0.0f), iconColor, 2.25f, true);
		DrawLine(center + new Vector2(0.0f, -4.0f), center + new Vector2(0.0f, 4.0f), iconColor, 2.25f, true);
	}
}
