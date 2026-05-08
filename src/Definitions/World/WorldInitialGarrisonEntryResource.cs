using Godot;
using Rpg.Definitions.Battle;

namespace Rpg.Definitions.World;

[GlobalClass]
public partial class WorldInitialGarrisonEntryResource : Resource
{
    [Export]
    public BattleUnitDefinition UnitDefinition { get; set; }

    [Export]
    public string UnitTypeIdOverride { get; set; } = "";

    [Export]
    public int Count { get; set; } = 1;

    [Export]
    public int Morale { get; set; } = 50;

    public string ResolveUnitTypeId()
    {
        if (!string.IsNullOrWhiteSpace(UnitTypeIdOverride))
        {
            return UnitTypeIdOverride.Trim();
        }

        return UnitDefinition?.Id?.Trim() ?? "";
    }

    public GarrisonDefinition ToDefinition()
    {
        return new GarrisonDefinition
        {
            UnitTypeId = ResolveUnitTypeId(),
            Count = Count,
            Morale = Morale
        };
    }
}
