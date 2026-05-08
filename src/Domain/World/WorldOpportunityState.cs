using System.Collections.Generic;
using System.Text.Json.Serialization;
using Godot;

namespace Rpg.Domain.World;

public sealed class WorldOpportunityState
{
    public string OpportunityId { get; set; } = "";
    public string DefinitionId { get; set; } = "";
    public string SpawnRuleId { get; set; } = "";
    public string SpawnPointId { get; set; } = "";
    public float WorldX { get; set; }
    public float WorldY { get; set; }
    public int CreatedTick { get; set; }
    public int ExpiresTick { get; set; }
    public WorldOpportunityStatus Status { get; set; } = WorldOpportunityStatus.Active;
    public List<string> Tags { get; set; } = new();

    [JsonIgnore]
    public Vector2 WorldPosition
    {
        get => new(WorldX, WorldY);
        set
        {
            WorldX = value.X;
            WorldY = value.Y;
        }
    }
}
