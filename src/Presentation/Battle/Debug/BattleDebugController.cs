using System.Collections.Generic;
using Godot;
using Rpg.Domain.Battle.Grid;

namespace Rpg.Presentation.Battle.Debug;

public partial class BattleDebugController : Node
{
    [ExportGroup("Debug总开关")]

    [Export]
    public bool DebugEnabled { get; set; } = true;

    [Export]
    public bool ToggleByKey { get; set; } = true;

    [Export]
    public Key ToggleKey { get; set; } = Key.F3;

    private BattleRoot _battleRoot;
    private BattleMapView _battleMapView;
    private BattleGridMap _gridMap;

    public override void _Ready()
    {
        _battleRoot = GetParentOrNull<BattleRoot>();

        if (_battleRoot == null)
        {
            GD.PushWarning("BattleDebugController must be a direct child of BattleRoot.");
            return;
        }

        _battleRoot.BattleMapLoaded += OnBattleMapLoaded;
        ApplyDebugEnabled();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!ToggleByKey || @event is not InputEventKey keyEvent)
        {
            return;
        }

        if (!keyEvent.Pressed || keyEvent.Echo || keyEvent.Keycode != ToggleKey)
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

    private void OnBattleMapLoaded(Node activeMap)
    {
        _battleMapView = activeMap as BattleMapView;
        _gridMap = _battleMapView == null ? null : GridMapReader.Read(_battleMapView);

        if (_battleMapView == null)
        {
            GD.PushWarning("Loaded battle map is not a BattleMapView; debug grid data is unavailable.");
        }

        foreach (BattleDebugComponent component in EnumerateDebugComponents(this))
        {
            component.Configure(_battleRoot, _battleMapView, _gridMap);
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
