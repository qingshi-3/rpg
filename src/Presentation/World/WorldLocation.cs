using Godot;

namespace Rpg.Presentation.World;

public partial class WorldLocation : Node2D
{
    [Signal]
    public delegate void EntranceRequestedEventHandler(string targetLocationId, string targetSpawnId);

    [Signal]
    public delegate void EncounterRequestedEventHandler(string encounterId);

    [Export]
    public string LocationId { get; set; } = string.Empty;

    [Export]
    public NodePath SpawnPointsPath { get; set; } = new("SpawnPoints");

    [Export]
    public NodePath EntrancesPath { get; set; } = new("Entrances");

    [Export]
    public NodePath EncounterTriggersPath { get; set; } = new("EncounterTriggers");

    private Node _spawnPoints;
    private Node _entrances;
    private Node _encounterTriggers;

    public override void _Ready()
    {
        _spawnPoints = GetNodeOrNull<Node>(SpawnPointsPath);
        _entrances = GetNodeOrNull<Node>(EntrancesPath);
        _encounterTriggers = GetNodeOrNull<Node>(EncounterTriggersPath);

        BindEntrances();
        BindEncounterTriggers();
    }

    public WorldSpawnPoint GetSpawnPoint(string spawnId)
    {
        if (_spawnPoints == null)
        {
            return null;
        }

        foreach (Node child in _spawnPoints.GetChildren())
        {
            if (child is WorldSpawnPoint spawnPoint && spawnPoint.SpawnId == spawnId)
            {
                return spawnPoint;
            }
        }

        foreach (Node child in _spawnPoints.GetChildren())
        {
            if (child is WorldSpawnPoint spawnPoint)
            {
                return spawnPoint;
            }
        }

        return null;
    }

    private void BindEntrances()
    {
        if (_entrances == null)
        {
            return;
        }

        foreach (Node child in _entrances.GetChildren())
        {
            if (child is WorldEntrance entrance)
            {
                entrance.EntranceRequested += OnEntranceRequested;
            }
        }
    }

    private void BindEncounterTriggers()
    {
        if (_encounterTriggers == null)
        {
            return;
        }

        foreach (Node child in _encounterTriggers.GetChildren())
        {
            if (child is WorldEncounterTrigger trigger)
            {
                trigger.EncounterRequested += OnEncounterRequested;
            }
        }
    }

    private void OnEntranceRequested(string targetLocationId, string targetSpawnId)
    {
        EmitSignal(SignalName.EntranceRequested, targetLocationId, targetSpawnId);
    }

    private void OnEncounterRequested(string encounterId)
    {
        EmitSignal(SignalName.EncounterRequested, encounterId);
    }
}

