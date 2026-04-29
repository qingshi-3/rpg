using Godot;
using Rpg.Domain.Battle.Grid;

namespace Rpg.Presentation.Battle.Debug;

public abstract partial class BattleDebugComponent : Node
{
    protected BattleRoot BattleRoot { get; private set; }
    protected BattleMapView BattleMapView { get; private set; }
    protected BattleGridMap GridMap { get; private set; }
    protected bool DebugEnabled { get; private set; }

    public virtual void Configure(BattleRoot battleRoot, BattleMapView battleMapView, BattleGridMap gridMap)
    {
        BattleRoot = battleRoot;
        BattleMapView = battleMapView;
        GridMap = gridMap;
    }

    public void SetDebugEnabled(bool enabled)
    {
        if (DebugEnabled == enabled)
        {
            return;
        }

        DebugEnabled = enabled;
        OnDebugEnabledChanged(enabled);
    }

    protected virtual void OnDebugEnabledChanged(bool enabled)
    {
    }
}
