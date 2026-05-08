using Rpg.Definitions.Battle.Abilities;
using Rpg.Domain.Battle.Grid;

namespace Rpg.Presentation.Battle.Threats;

public sealed record BattleThreatSource(
    GridSurfacePosition Origin,
    GridPosition ThreatCell,
    AbilityDefinition Ability);
