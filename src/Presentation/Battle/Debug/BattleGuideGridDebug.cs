using Godot;

namespace Rpg.Presentation.Battle.Debug;

public partial class BattleGuideGridDebug : BattleDebugComponent
{
    private const string BattleGuideGridToggleAction = "battle_guide_grid_toggle";

    [ExportGroup("辅助网格")]

    [Export]
    public bool VisibleOnStart { get; set; }

    [Export]
    public bool ToggleByInputAction { get; set; } = true;

    [Export]
    public string ToggleAction { get; set; } = BattleGuideGridToggleAction;

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
        if (!DebugEnabled || !ToggleByInputAction || string.IsNullOrWhiteSpace(ToggleAction))
        {
            return;
        }

        if (!@event.IsActionPressed(ToggleAction))
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
