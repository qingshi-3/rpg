using Godot;

namespace Rpg.Presentation.World;

public partial class WorldEntrance : Area2D
{
    [Signal]
    public delegate void EntranceRequestedEventHandler(string targetLocationId, string targetSpawnId);

    [Export]
    public string TargetLocationId { get; set; } = string.Empty;

    [Export]
    public string TargetSpawnId { get; set; } = "default";

    public override void _Ready()
    {
        InputPickable = true;
    }

    public override void _InputEvent(Viewport viewport, InputEvent @event, int shapeIdx)
    {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            Activate();
        }
    }

    public void Activate()
    {
        if (string.IsNullOrWhiteSpace(TargetLocationId))
        {
            GD.PushWarning($"WorldEntrance '{Name}' has no target location id.");
            return;
        }

        EmitSignal(SignalName.EntranceRequested, TargetLocationId, TargetSpawnId);
    }
}

