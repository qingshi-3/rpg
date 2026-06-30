using System.Collections.Generic;
using Rpg.Definitions.StrategicManagement;

namespace Rpg.Domain.StrategicManagement;

public sealed class StrategicCityState
{
    public string LocationId { get; set; } = "";
    public string CityIdentityId { get; set; } = "";
    public int CityForceCapacity { get; set; }
    public int ReserveForces { get; set; }
    public string AutoConscriptionIntensityId { get; set; } = StrategicManagementIds.ConscriptionOff;
    public List<string> ConstructionRegionIds { get; set; } = new();
    public List<StrategicBuildingInstanceState> Buildings { get; set; } = new();
}

public sealed class StrategicBuildingInstanceState
{
    public string BuildingInstanceId { get; set; } = "";
    public string BuildingDefinitionId { get; set; } = "";
    public string ConstructionRegionId { get; set; } = "";
    public int GridX { get; set; }
    public int GridY { get; set; }
    public int Level { get; set; } = 1;
    public bool IsConstructed { get; set; } = true;
    public string BattleAnchorId { get; set; } = "";
}
