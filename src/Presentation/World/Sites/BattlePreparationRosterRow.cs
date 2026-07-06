using Godot;

namespace Rpg.Presentation.World.Sites;

public enum BattlePreparationCompanyPlanStatus
{
    Missing,
    Partial,
    Complete
}

public partial class BattlePreparationRosterRow : PanelContainer
{
    private const float DragThresholdPixels = 5.0f;

    [Signal]
    public delegate void SelectedEventHandler(string groupKey);

    [Signal]
    public delegate void DragStartedEventHandler(string groupKey);

    private TextureRect _avatar;
    private Label _nameLabel;
    private Label _statusLabel;
    private string _groupKey = "";
    private string _pendingGroupKey = "";
    private string _pendingDisplayName = "";
    private Texture2D _previewTexture;
    private BattlePreparationCompanyPlanStatus _pendingStatus = BattlePreparationCompanyPlanStatus.Missing;
    private bool _pendingSelected;
    private bool _pressed;
    private bool _dragStarted;
    private Vector2 _pressPosition;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        _avatar = GetNodeOrNull<TextureRect>("Row/Avatar");
        _nameLabel = GetNodeOrNull<Label>("Row/Name");
        _statusLabel = GetNodeOrNull<Label>("Row/Status");
        ApplyBinding();
    }

    public void Bind(
        string groupKey,
        string displayName,
        Texture2D previewTexture,
        BattlePreparationCompanyPlanStatus status,
        bool selected)
    {
        _pendingGroupKey = groupKey ?? "";
        _pendingDisplayName = displayName ?? "";
        _previewTexture = previewTexture;
        _pendingStatus = status;
        _pendingSelected = selected;
        _groupKey = _pendingGroupKey;
        ApplyBinding();
    }

    private void ApplyBinding()
    {
        if (_nameLabel != null)
        {
            _nameLabel.Text = string.IsNullOrWhiteSpace(_pendingDisplayName)
                ? "未命名部队"
                : _pendingDisplayName.Trim();
        }

        if (_statusLabel != null)
        {
            _statusLabel.Text = _pendingStatus switch
            {
                BattlePreparationCompanyPlanStatus.Complete => "✓",
                BattlePreparationCompanyPlanStatus.Partial => "-",
                _ => "×"
            };
        }

        if (_avatar != null)
        {
            _avatar.Texture = _previewTexture;
            _avatar.SelfModulate = _pendingSelected
                ? Colors.White
                : new Color(1.0f, 1.0f, 1.0f, 0.82f);
        }

        SelfModulate = _pendingSelected ? Colors.White : new Color(1.0f, 1.0f, 1.0f, 0.86f);
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left } mouseButton)
        {
            if (mouseButton.Pressed)
            {
                _pressed = true;
                _dragStarted = false;
                _pressPosition = mouseButton.Position;
                AcceptEvent();
                return;
            }

            if (!mouseButton.Pressed && _pressed && !_dragStarted)
            {
                EmitSignal(SignalName.Selected, _groupKey);
            }

            _pressed = false;
            _dragStarted = false;
            AcceptEvent();
            return;
        }

        if (@event is InputEventMouseMotion motion &&
            _pressed &&
            !_dragStarted &&
            motion.Position.DistanceTo(_pressPosition) >= DragThresholdPixels)
        {
            _dragStarted = true;
            EmitSignal(SignalName.DragStarted, _groupKey);
            AcceptEvent();
        }
    }
}
