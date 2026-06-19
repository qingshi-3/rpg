using System.Collections.Generic;
using Godot;
using Rpg.Domain.Battle.Grid;
using Rpg.Presentation.World.Sites;

namespace Rpg.Presentation.Battle.Debug;

public partial class BattleDebugController : Node
{
    private const string BattleDebugToggleAction = "battle_debug_toggle";

    [ExportGroup("Debug总开关")]

    [Export]
    public bool DebugEnabled { get; set; } = true;

    [Export]
    public bool ToggleByInputAction { get; set; } = true;

    [Export]
    public string ToggleAction { get; set; } = BattleDebugToggleAction;

    private WorldSiteRoot _siteRoot;
    private BattleMapView _battleMapView;
    private BattleGridMap _gridMap;

    public override void _Ready()
    {
        _siteRoot = GetParentOrNull<WorldSiteRoot>();

        if (_siteRoot == null)
        {
            GD.PushWarning("BattleDebugController must be a direct child of WorldSiteRoot.");
            return;
        }

        _siteRoot.SiteMapLoaded += OnSiteMapLoaded;
        ApplyDebugEnabled();
    }

    public override void _ExitTree()
    {
        if (_siteRoot != null)
        {
            _siteRoot.SiteMapLoaded -= OnSiteMapLoaded;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!ToggleByInputAction || string.IsNullOrWhiteSpace(ToggleAction))
        {
            return;
        }

        if (!@event.IsActionPressed(ToggleAction))
        {
            return;
        }

        SetDebugEnabled(!DebugEnabled);
        GetViewport().SetInputAsHandled();
    }

    public void SetDebugEnabled(bool enabled)
    {
        if (DebugEnabled == enabled)
        {
            return;
        }

        DebugEnabled = enabled;
        ApplyDebugEnabled();
    }

    private void OnSiteMapLoaded(Node activeSiteMap)
    {
        _battleMapView = activeSiteMap as BattleMapView;
        _battleMapView?.EnsureRuntimeData();
        _gridMap = _siteRoot.ActiveGridMap ?? _battleMapView?.GridMap;

        if (_battleMapView == null)
        {
            GD.PushWarning("Loaded site map is not a BattleMapView; debug grid data is unavailable.");
        }

        foreach (BattleDebugComponent component in EnumerateDebugComponents(this))
        {
            component.Configure(_siteRoot, _battleMapView, _gridMap);
        }

        ApplyDebugEnabled();
    }

    private void ApplyDebugEnabled()
    {
        foreach (BattleDebugComponent component in EnumerateDebugComponents(this))
        {
            component.SetDebugEnabled(DebugEnabled);
        }
    }

    private static IEnumerable<BattleDebugComponent> EnumerateDebugComponents(Node root)
    {
        foreach (Node child in root.GetChildren())
        {
            if (child is BattleDebugComponent component)
            {
                yield return component;
            }

            foreach (BattleDebugComponent descendant in EnumerateDebugComponents(child))
            {
                yield return descendant;
            }
        }
    }
}
