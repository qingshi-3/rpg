namespace Rpg.Domain.Battle.Grid;

public sealed class GridCellLayerData
{
    public GridCellLayerData(
        string layerName,
        LayerRole role,
        int height,
        bool affectsWalkability,
        bool affectsLineOfSight,
        bool isHeightTransitionLayer,
        bool isVisualOnly,
        int sourceId,
        int atlasX,
        int atlasY,
        int alternativeTile)
    {
        LayerName = layerName;
        Role = role;
        Height = height;
        AffectsWalkability = affectsWalkability;
        AffectsLineOfSight = affectsLineOfSight;
        IsHeightTransitionLayer = isHeightTransitionLayer;
        IsVisualOnly = isVisualOnly;
        SourceId = sourceId;
        AtlasX = atlasX;
        AtlasY = atlasY;
        AlternativeTile = alternativeTile;
    }

    public string LayerName { get; }
    public LayerRole Role { get; }
    public int Height { get; }
    public bool AffectsWalkability { get; }
    public bool AffectsLineOfSight { get; }
    public bool IsHeightTransitionLayer { get; }
    public bool IsVisualOnly { get; }
    public int SourceId { get; }
    public int AtlasX { get; }
    public int AtlasY { get; }
    public int AlternativeTile { get; }
}
