using Godot;
using Rpg.Domain.Battle.Grid;
using Rpg.Infrastructure.Logging;

namespace Rpg.Presentation.Battle;

public partial class BattleMapView : Node2D
{
    [Export]
    public Vector2I Size { get; set; } = new(6, 6);

    [Export]
    public NodePath WaterFoundationLayerPath { get; set; } = new("WaterFoundationLayer");

    [Export]
    public NodePath WaterDetailLayerPath { get; set; } = new("WaterDetailLayer");

    [Export]
    public NodePath WaterObjectLayerPath { get; set; } = new("WaterObjectLayer");

    [Export]
    public NodePath LowFoundationLayerPath { get; set; } = new("LowFoundationLayer");

    [Export]
    public NodePath LowDetailLayerPath { get; set; } = new("LowDetailLayer");

    [Export]
    public NodePath LowObjectLayerPath { get; set; } = new("LowObjectLayer");

    [Export]
    public NodePath HighFoundationLayerPath { get; set; } = new("HighFoundationLayer");

    [Export]
    public NodePath HighDetailLayerPath { get; set; } = new("HighDetailLayer");

    [Export]
    public NodePath HighObjectLayerPath { get; set; } = new("HighObjectLayer");

    [Export]
    public NodePath StairLayerPath { get; set; } = new("StairLayer");

    [Export]
    public NodePath OverlayLayerPath { get; set; } = new("OverlayLayer");

    public TileMapLayer WaterFoundationLayer { get; private set; }
    public TileMapLayer WaterDetailLayer { get; private set; }
    public TileMapLayer WaterObjectLayer { get; private set; }
    public TileMapLayer LowFoundationLayer { get; private set; }
    public TileMapLayer LowDetailLayer { get; private set; }
    public TileMapLayer LowObjectLayer { get; private set; }
    public TileMapLayer HighFoundationLayer { get; private set; }
    public TileMapLayer HighDetailLayer { get; private set; }
    public TileMapLayer HighObjectLayer { get; private set; }
    public TileMapLayer StairLayer { get; private set; }
    public TileMapLayer OverlayLayer { get; private set; }
    public BattleGridMap GridMap { get; private set; }
    public BattleMapRenderSortCache RenderSortCache { get; private set; } = BattleMapRenderSortCache.Empty;
    public BattleMapLayer CoordinateLayer { get; private set; }
    public bool RuntimeDataReady { get; private set; }

    private bool _runtimeDataInitialized;

    public override void _Ready()
    {
        WaterFoundationLayer = GetNode<TileMapLayer>(WaterFoundationLayerPath);
        WaterDetailLayer = GetNode<TileMapLayer>(WaterDetailLayerPath);
        WaterObjectLayer = GetNode<TileMapLayer>(WaterObjectLayerPath);
        LowFoundationLayer = GetNode<TileMapLayer>(LowFoundationLayerPath);
        LowDetailLayer = GetNode<TileMapLayer>(LowDetailLayerPath);
        LowObjectLayer = GetNode<TileMapLayer>(LowObjectLayerPath);
        HighFoundationLayer = GetNode<TileMapLayer>(HighFoundationLayerPath);
        HighDetailLayer = GetNode<TileMapLayer>(HighDetailLayerPath);
        HighObjectLayer = GetNode<TileMapLayer>(HighObjectLayerPath);
        StairLayer = GetNode<TileMapLayer>(StairLayerPath);
        OverlayLayer = GetNode<TileMapLayer>(OverlayLayerPath);

        EnsureRuntimeData();
    }

    public void EnsureRuntimeData()
    {
        if (_runtimeDataInitialized)
        {
            return;
        }

        GridMap = GridMapReader.Read(this);
        RenderSortCache = BattleMapRenderSortCache.Build(this);
        CoordinateLayer = BattleMapLayerQueries.FindCoordinateLayer(this);
        RuntimeDataReady = GridMap != null && CoordinateLayer != null;
        _runtimeDataInitialized = true;

        if (!RuntimeDataReady)
        {
            GameLog.Warn(
                nameof(BattleMapView),
                $"Map runtime missing critical layer gridMap={GridMap != null} coordinateLayer={CoordinateLayer != null} path={GetPath()}");
            return;
        }

        GameLog.Info(
            nameof(BattleMapView),
            $"Map runtime ready path={GetPath()} cells={GridMap.Cells.Count} surfaces={GridMap.Surfaces.Count} coordinateLayer={CoordinateLayer.GetPath()}");
    }
}
