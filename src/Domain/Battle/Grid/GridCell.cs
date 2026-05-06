using System.Collections.Generic;

namespace Rpg.Domain.Battle.Grid;

public sealed class GridCell
{
    private readonly List<GridCellLayerData> _layers = new();
    private readonly HashSet<string> _terrainTags = new();

    public GridCell(GridPosition position)
    {
        Position = position;
    }

    public GridPosition Position { get; }
    public int Height { get; private set; }
    public int MoveCost { get; private set; }
    public bool HasFoundation { get; private set; }
    public bool IsWalkable { get; private set; }
    public bool CanStandOn { get; private set; }
    public bool IsObstacle { get; private set; }
    public bool BlocksLineOfSight { get; private set; }
    public bool IsHeightTransition { get; private set; }
    public bool HasFoundationHeightConflict { get; private set; }
    public string TerrainTag { get; private set; } = "";
    public IReadOnlyList<GridCellLayerData> Layers => _layers;
    public IReadOnlyCollection<string> TerrainTags => _terrainTags;

    public void AddLayer(GridCellLayerData layer)
    {
        _layers.Add(layer);
        ApplyLayer(layer);
    }

    private void ApplyLayer(GridCellLayerData layer)
    {
        if (layer.IsVisualOnly || layer.Role == LayerRole.Detail)
        {
            return;
        }

        ApplyCommonTileData(layer);

        if (layer.Role == LayerRole.Foundation)
        {
            HasFoundation = true;
            Height = layer.Height;
            TerrainTag = layer.TerrainTag ?? "";
            ApplyFoundationWalkability(layer);
            return;
        }

        if (layer.Role == LayerRole.Object)
        {
            ApplyNonFoundationWalkability(layer);
            return;
        }

        if (layer.Role == LayerRole.Stair || layer.IsHeightTransitionLayer)
        {
            IsHeightTransition = true;
        }

        ApplyNonFoundationWalkability(layer);
    }

    private void ApplyCommonTileData(GridCellLayerData layer)
    {
        if (layer.IsObstacle)
        {
            IsObstacle = true;
        }

        if (layer.CanStandOn)
        {
            CanStandOn = true;
        }

        if (!string.IsNullOrWhiteSpace(layer.TerrainTag))
        {
            _terrainTags.Add(layer.TerrainTag);
        }

        if (layer.AffectsLineOfSight)
        {
            BlocksLineOfSight = true;
        }
    }

    private void ApplyFoundationWalkability(GridCellLayerData layer)
    {
        if (!layer.AffectsWalkability)
        {
            return;
        }

        if (!layer.Walkable)
        {
            IsWalkable = false;
            MoveCost = 0;
            return;
        }

        IsWalkable = layer.Walkable;
        MoveCost = NormalizeMoveCost(layer.MoveCost);
    }

    private void ApplyNonFoundationWalkability(GridCellLayerData layer)
    {
        if (!layer.AffectsWalkability)
        {
            return;
        }

        if (!layer.Walkable)
        {
            IsWalkable = false;
            MoveCost = 0;
            return;
        }

        if (!IsWalkable)
        {
            return;
        }

        MoveCost = System.Math.Max(MoveCost, NormalizeMoveCost(layer.MoveCost));
    }

    private static int NormalizeMoveCost(int moveCost)
    {
        return System.Math.Max(1, moveCost);
    }
}
