using Godot;

namespace Rpg.Presentation.World;

public partial class WorldEncounterTrigger : Area2D
{
    [Signal]
    public delegate void EncounterRequestedEventHandler(string encounterId);

    [Export]
    public string EncounterId { get; set; } = string.Empty;

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
        if (string.IsNullOrWhiteSpace(EncounterId))
        {
            GD.PushWarning($"WorldEncounterTrigger '{Name}' has no encounter id.");
            return;
        }

        EmitSignal(SignalName.EncounterRequested, EncounterId);
    }
}

