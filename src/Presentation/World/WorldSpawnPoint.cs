using Godot;

namespace Rpg.Presentation.World;

public partial class WorldSpawnPoint : Marker2D
{
    [Export]
    public string SpawnId { get; set; } = "default";
}

