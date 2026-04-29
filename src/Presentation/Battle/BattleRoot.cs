using Godot;

namespace Rpg.Presentation.Battle;

public partial class BattleRoot : Node2D
{
    [Signal]
    public delegate void BattleMapLoadedEventHandler(Node activeMap);

    [Export]
    public NodePath MapRootPath { get; set; } = new("MapRoot");

    [Export]
    public PackedScene BattleMapScene { get; set; }

    private Node _mapRoot;
    private Node _activeMap;

    public Node ActiveMap => _activeMap;

    public override void _Ready()
    {
        _mapRoot = GetNode<Node>(MapRootPath);
        LoadConfiguredMap();
    }

    public void LoadConfiguredMap()
    {
        if (BattleMapScene == null)
        {
            GD.PushWarning("BattleRoot has no battle map scene configured.");
            return;
        }

        LoadMap(BattleMapScene);
    }

    public void LoadMap(PackedScene mapScene)
    {
        _activeMap?.QueueFree();

        _activeMap = mapScene.Instantiate<Node>();
        _mapRoot.AddChild(_activeMap);
        EmitSignal(SignalName.BattleMapLoaded, _activeMap);
    }
}
