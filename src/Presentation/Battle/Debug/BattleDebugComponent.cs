using Godot;
using Rpg.Domain.Battle.Grid;
using Rpg.Presentation.World.Sites;

namespace Rpg.Presentation.Battle.Debug;

public abstract partial class BattleDebugComponent : Node
{
    protected WorldSiteRoot SiteRoot { get; private set; }
    protected BattleMapView BattleMapView { get; private set; }
    protected BattleGridMap GridMap { get; private set; }
    protected bool DebugEnabled { get; private set; }

    public virtual void Configure(WorldSiteRoot siteRoot, BattleMapView battleMapView, BattleGridMap gridMap)
    {
        SiteRoot = siteRoot;
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
