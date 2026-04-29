using Godot;

namespace Rpg.Presentation.Battle.Debug;

public partial class BattleGuideGridDebug : BattleDebugComponent
{
    [ExportGroup("辅助网格")]

    [Export]
    public bool VisibleOnStart { get; set; }

    [Export]
    public bool ToggleByKey { get; set; } = true;

    [Export]
    public Key ToggleKey { get; set; } = Key.F4;

    [Export]
    public int GridSpacingPixels { get; set; } = 16;

    [Export]
    public Color GridLineColor { get; set; } = new(0.35f, 0.78f, 1f, 0.24f);

    [Export]
    public float GridLineWidth { get; set; } = 1f;

    private BattleGuideGridDrawer _drawer;
    private bool _gridVisible;

    public override void _Ready()
    {
        _gridVisible = VisibleOnStart;
        _drawer = new BattleGuideGridDrawer();
        AddChild(_drawer);
        ApplyDrawerState();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!DebugEnabled || !ToggleByKey || @event is not InputEventKey keyEvent)
        {
            return;
        }

        if (!keyEvent.Pressed || keyEvent.Echo || keyEvent.Keycode != ToggleKey)
        {
            return;
        }

        _gridVisible = !_gridVisible;
        ApplyDrawerState();
        GetViewport().SetInputAsHandled();
    }

    protected override void OnDebugEnabledChanged(bool enabled)
    {
        ApplyDrawerState();
    }

    private void ApplyDrawerState()
    {
        if (_drawer == null)
        {
            return;
        }

        _drawer.GridSpacingPixels = Mathf.Max(1, GridSpacingPixels);
        _drawer.GridLineColor = GridLineColor;
        _drawer.GridLineWidth = GridLineWidth;
        _drawer.Visible = DebugEnabled && _gridVisible;
        _drawer.SetProcess(_drawer.Visible);
        _drawer.QueueRedraw();
    }
}
