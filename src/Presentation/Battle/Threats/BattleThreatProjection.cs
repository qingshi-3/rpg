using System.Collections.Generic;
using System.Linq;
using Rpg.Domain.Battle.Grid;
using Rpg.Presentation.Battle.Entities;

namespace Rpg.Presentation.Battle.Threats;

public sealed class BattleThreatProjection
{
    public BattleThreatProjection(
        IReadOnlyCollection<GridPosition> movementCells,
        IReadOnlyCollection<GridPosition> threatCells,
        IReadOnlyCollection<GridPosition> targetCells,
        IReadOnlyCollection<BattleEntity> threatenedTargets,
        IReadOnlyCollection<BattleThreatSource> sources)
    {
        MovementCells = movementCells?.ToArray() ?? System.Array.Empty<GridPosition>();
        ThreatCells = threatCells?.ToArray() ?? System.Array.Empty<GridPosition>();
        TargetCells = targetCells?.ToArray() ?? System.Array.Empty<GridPosition>();
        ThreatenedTargets = threatenedTargets?.ToArray() ?? System.Array.Empty<BattleEntity>();
        Sources = sources?.ToArray() ?? System.Array.Empty<BattleThreatSource>();
    }

    public IReadOnlyCollection<GridPosition> MovementCells { get; }
    public IReadOnlyCollection<GridPosition> ThreatCells { get; }
    public IReadOnlyCollection<GridPosition> TargetCells { get; }
    public IReadOnlyCollection<BattleEntity> ThreatenedTargets { get; }
    public IReadOnlyCollection<BattleThreatSource> Sources { get; }
}
