using Godot;
using Rpg.Presentation.Battle.Entities;

namespace Rpg.Presentation.World.Sites;

public partial class WorldSiteDeploymentDragComponent : BattleEntityComponent
{
	[Export]
	public string PlacementId { get; set; } = "";

	[Export]
	public bool DragEnabled { get; set; }

	public void Configure(string placementId, bool dragEnabled)
	{
		PlacementId = placementId ?? "";
		DragEnabled = dragEnabled;
	}

	public void SetDragEnabled(bool enabled)
	{
		DragEnabled = enabled;
	}

	public bool CanDragPlacement(string placementId)
	{
		return DragEnabled &&
			   !string.IsNullOrWhiteSpace(PlacementId) &&
			   PlacementId == placementId;
	}
}
