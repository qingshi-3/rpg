using Godot;
using Rpg.Definitions.World;
using System.Collections.Generic;

namespace Rpg.Presentation.World;

public partial class WorldRoot : Node2D
{
    [Export]
    public WorldLocationRegistry LocationRegistry { get; set; }

    [Export]
    public string InitialLocationId { get; set; } = "town_village_01";

    [Export]
    public string InitialSpawnId { get; set; } = "main";

    [Export]
    public NodePath LocationRootPath { get; set; } = new("LocationRoot");

    [Export]
    public NodePath PlayerPath { get; set; } = new("Player");

    private readonly Dictionary<string, WorldLocationDefinition> _locations = new();
    private Node _locationRoot;
    private Node2D _player;
    private WorldLocation _activeLocation;

    public override void _Ready()
    {
        _locationRoot = GetNode<Node>(LocationRootPath);
        _player = GetNodeOrNull<Node2D>(PlayerPath);

        RegisterLocations();
        LoadLocation(InitialLocationId, InitialSpawnId);
    }

    public void LoadLocation(string locationId, string spawnId = "")
    {
        if (!_locations.TryGetValue(locationId, out WorldLocationDefinition definition))
        {
            GD.PushWarning($"WorldRoot cannot find location '{locationId}'.");
            return;
        }

        if (definition.LocationScene == null)
        {
            GD.PushWarning($"WorldLocationDefinition '{definition.Id}' has no scene configured.");
            return;
        }

        _activeLocation?.QueueFree();

        Node instance = definition.LocationScene.Instantiate();
        _locationRoot.AddChild(instance);

        if (instance is not WorldLocation location)
        {
            GD.PushWarning($"Location scene '{definition.Id}' does not use WorldLocation as its root script.");
            return;
        }

        _activeLocation = location;
        _activeLocation.EntranceRequested += LoadLocation;
        _activeLocation.EncounterRequested += OnEncounterRequested;

        string resolvedSpawnId = string.IsNullOrWhiteSpace(spawnId) ? definition.DefaultSpawnId : spawnId;
        MovePlayerToSpawn(resolvedSpawnId);
    }

    private void RegisterLocations()
    {
        _locations.Clear();

        if (LocationRegistry == null)
        {
            GD.PushWarning("WorldRoot has no location registry configured.");
            return;
        }

        foreach (WorldLocationDefinition definition in LocationRegistry.Locations)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
            {
                continue;
            }

            _locations[definition.Id] = definition;
        }
    }

    private void MovePlayerToSpawn(string spawnId)
    {
        if (_player == null || _activeLocation == null)
        {
            return;
        }

        WorldSpawnPoint spawnPoint = _activeLocation.GetSpawnPoint(spawnId);
        if (spawnPoint == null)
        {
            GD.PushWarning($"Location '{_activeLocation.LocationId}' has no spawn point '{spawnId}'.");
            return;
        }

        _player.GlobalPosition = spawnPoint.GlobalPosition;
    }

    private void OnEncounterRequested(string encounterId)
    {
        GD.Print($"WorldRoot received encounter request '{encounterId}'. Battle handoff is not implemented yet.");
    }
}
