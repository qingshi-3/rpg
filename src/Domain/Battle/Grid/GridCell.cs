using System.Collections.Generic;

namespace Rpg.Domain.Battle.Grid;

public sealed class GridCell
{
    private readonly List<GridCellLayerData> _layers = new();

    public GridCell(GridPosition position)
    {
        Position = position;
    }

    public GridPosition Position { get; }
    public int Height { get; private set; }
    public bool HasFoundation { get; private set; }
    public bool IsWalkable { get; private set; }
    public bool BlocksLineOfSight { get; private set; }
    public bool IsHeightTransition { get; private set; }
    public bool HasFoundationHeightConflict { get; private set; }
    public IReadOnlyList<GridCellLayerData> Layers => _layers;

    public void AddLayer(GridCellLayerData layer)
    {
        _layers.Add(layer);
        ApplyLayer(layer);
    }

    private void ApplyLayer(GridCellLayerData layer)
    {
        if (layer.IsVisualOnly)
        {
            return;
        }

        if (layer.Role == LayerRole.Foundation)
        {
            if (HasFoundation && Height != layer.Height)
            {
                HasFoundationHeightConflict = true;
            }

            HasFoundation = true;
            Height = layer.Height;
            IsWalkable = layer.AffectsWalkability;
            return;
        }

        if (layer.Role == LayerRole.Object)
        {
            if (layer.AffectsWalkability)
            {
                IsWalkable = false;
            }

            if (layer.AffectsLineOfSight)
            {
                BlocksLineOfSight = true;
            }

            return;
        }

        if (layer.Role == LayerRole.Stair || layer.IsHeightTransitionLayer)
        {
            IsHeightTransition = true;
        }
    }
}
