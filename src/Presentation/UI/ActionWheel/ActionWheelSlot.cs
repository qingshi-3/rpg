using System;
using Godot;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.UI.ActionWheel;

public partial class ActionWheelSlot : Control
{
    private Label _iconLabel;
    private Label _label;
    private Label _costLabel;
    private ActionWheelCommandViewModel _command;

    public event Action<ActionWheelSlot> Pressed;
    public event Action<ActionWheelSlot> CancelRequested;
    public event Action<ActionWheelSlot> Hovered;
    public event Action<ActionWheelSlot> Unhovered;

    public ActionWheelCommandViewModel Command => _command;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        _iconLabel = GameUiSceneFactory.GetRequiredNode<Label>(this, "IconLabel", nameof(ActionWheelSlot));
        _label = GameUiSceneFactory.GetRequiredNode<Label>(this, "Label", nameof(ActionWheelSlot));
        _costLabel = GameUiSceneFactory.GetRequiredNode<Label>(this, "CostLabel", nameof(ActionWheelSlot));

        MouseEntered += OnMouseEntered;
        MouseExited += OnMouseExited;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton mouseButton || !mouseButton.Pressed)
        {
            return;
        }

        if (mouseButton.ButtonIndex == MouseButton.Left)
        {
            Pressed?.Invoke(this);
            AcceptEvent();
        }
        else if (mouseButton.ButtonIndex == MouseButton.Right)
        {
            CancelRequested?.Invoke(this);
            AcceptEvent();
        }
    }

    public void SetCommand(ActionWheelCommandViewModel command)
    {
        _command = command;
        if (_iconLabel == null || _label == null || _costLabel == null)
        {
            return;
        }

        _iconLabel.Text = string.IsNullOrWhiteSpace(command.IconText) ? "•" : command.IconText;
        _label.Text = command.Label;
        _costLabel.Text = command.ApCost.HasValue ? $"{command.ApCost.Value} AP" : "";
    }

    public void SetActive(bool active)
    {
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
        {
            LayoutLabels();
        }
    }

    private void OnMouseEntered()
    {
        Hovered?.Invoke(this);
    }

    private void OnMouseExited()
    {
        Unhovered?.Invoke(this);
    }

    private void LayoutLabels()
    {
        if (_iconLabel == null || _label == null || _costLabel == null)
        {
            return;
        }

        float labelWidth = Mathf.Max(Size.X - 8f, 24f);

        _iconLabel.Position = new Vector2(4, 2);
        _iconLabel.Size = new Vector2(labelWidth, 18);
        _label.Position = new Vector2(4, 22);
        _label.Size = new Vector2(labelWidth, 18);
        _costLabel.Position = new Vector2(4, 42);
        _costLabel.Size = new Vector2(labelWidth, 18);
    }

}
