using Godot;

namespace Rpg.Application.World;

public readonly record struct WorldSiteDeploymentCell(Vector2I Cell, int Height, string TerrainTag, bool IsWater);
