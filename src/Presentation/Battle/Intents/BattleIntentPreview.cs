using System.Collections.Generic;
using System.Linq;
using Rpg.Domain.Battle.Grid;
using Rpg.Presentation.Battle.Actions;
using Rpg.Presentation.Battle.Entities;

namespace Rpg.Presentation.Battle.Intents;

public sealed class BattleIntentPreview
{
    public BattleIntentPreview(
        BattleIntent intent,
        BattleActionRequest request,
        IReadOnlyList<GridSurfacePosition> pathSurfaces,
        IReadOnlyCollection<GridPosition> affectedCells,
        string detailText)
    {
        Intent = intent;
        Request = request ?? BattleActionRequest.None(intent?.Actor, detailText);
        PathSurfaces = pathSurfaces?.ToArray() ?? System.Array.Empty<GridSurfacePosition>();
        PathCells = PathSurfaces.Select(surface => surface.Position).ToArray();
        AffectedCells = affectedCells?.ToArray() ?? System.Array.Empty<GridPosition>();
        DetailText = detailText ?? "";
    }

    public BattleIntent Intent { get; }
    public BattleActionRequest Request { get; }
    public IReadOnlyList<GridSurfacePosition> PathSurfaces { get; }
    public IReadOnlyList<GridPosition> PathCells { get; }
    public IReadOnlyCollection<GridPosition> AffectedCells { get; }
    public string DetailText { get; }
    public BattleEntity Actor => Intent?.Actor ?? Request?.Actor;
    public BattleEntity Target => Request?.Target;
    public BattleActionKind Kind => Request?.Kind ?? BattleActionKind.None;
    public bool HasAction => Request != null && Request.Kind != BattleActionKind.None;
}
