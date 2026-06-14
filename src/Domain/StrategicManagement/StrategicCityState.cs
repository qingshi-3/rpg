using System.Collections.Generic;

namespace Rpg.Domain.StrategicManagement;

public sealed class StrategicCityState
{
    public string LocationId { get; set; } = "";
    public string CityIdentityId { get; set; } = "";
    public int FacilitySlotCount { get; set; }
    public List<StrategicFacilityInstanceState> Facilities { get; set; } = new();
}

public sealed class StrategicFacilityInstanceState
{
    public string FacilityInstanceId { get; set; } = "";
    public string FacilityDefinitionId { get; set; } = "";
}
