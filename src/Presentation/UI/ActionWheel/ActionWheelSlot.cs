using System;
using Godot;

namespace Rpg.Presentation.UI.ActionWheel;

public partial class ActionWheelSlot : Control
{
    private readonly Label _iconLabel = new();
    private readonly Label _label = new();
    private readonly Label _costLabel = new();

    private ActionWheelCommandViewModel _command;
    private bool _isActive;
    private bool _isHovered;

    public event Action<ActionWheelSlot> Pressed;
    public event Action<ActionWheelSlot> CancelRequested;
    public event Action<ActionWheelSlot> Hovered;
    public event Action<ActionWheelSlot> Unhovered;

    public ActionWheelCommandViewModel Command => _command;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        CustomMinimumSize = new Vector2(96, 62);

        BuildLabel(_iconLabel, 2, HorizontalAlignment.Center);
        BuildLabel(_label, 22, HorizontalAlignment.Center);
        BuildLabel(_costLabel, 42, HorizontalAlignment.Center);

        MouseEntered += OnMouseEntered;
        MouseExited += OnMouseExited;
    }

    public override void _Draw()
    {
        DrawStyleBox(BuildSlotStyle(true), new Rect2(Vector2.Zero, Size));
        DrawStyleBox(BuildSlotStyle(false), new Rect2(Vector2.Zero, Size));
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
        _iconLabel.Text = string.IsNullOrWhiteSpace(command.IconText) ? "•" : command.IconText;
        _label.Text = command.Label;
        _costLabel.Text = command.ApCost.HasValue ? $"{command.ApCost.Value} AP" : "";
        ApplyTextColor();
        QueueRedraw();
    }

    public void SetActive(bool active)
    {
        if (_isActive == active)
        {
            return;
        }

        _isActive = active;
        QueueRedraw();
    }

    private void BuildLabel(Label label, float y, HorizontalAlignment alignment)
    {
        label.MouseFilter = MouseFilterEnum.Ignore;
        label.HorizontalAlignment = alignment;
        label.VerticalAlignment = VerticalAlignment.Center;
        label.Position = new Vector2(4, y);
        label.Size = new Vector2(76, 18);
        AddChild(label);
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
        _isHovered = true;
        Hovered?.Invoke(this);
        QueueRedraw();
    }

    private void OnMouseExited()
    {
        _isHovered = false;
        Unhovered?.Invoke(this);
        QueueRedraw();
    }

    private Color GetBackgroundColor()
    {
        if (_command is { IsEnabled: false })
        {
            return new Color(0.14f, 0.14f, 0.14f, 0.46f);
        }

        if (_isActive)
        {
            return new Color(0.94f, 0.78f, 0.38f, 0.88f);
        }

        if (_isHovered)
        {
            return new Color(0.93f, 0.91f, 0.78f, 0.82f);
        }

        return new Color(0.08f, 0.08f, 0.08f, 0.62f);
    }

    private Color GetBorderColor()
    {
        if (_command is { IsEnabled: false })
        {
            return new Color(1f, 1f, 1f, 0.2f);
        }

        return _isActive || _isHovered
            ? new Color(1f, 0.92f, 0.5f, 0.95f)
            : new Color(1f, 1f, 1f, 0.35f);
    }

    private StyleBoxFlat BuildSlotStyle(bool fill)
    {
        var style = new StyleBoxFlat
        {
            BgColor = fill ? GetBackgroundColor() : Colors.Transparent,
            BorderColor = fill ? Colors.Transparent : GetBorderColor(),
            AntiAliasing = true
        };

        int radius = Mathf.RoundToInt(Mathf.Min(Size.X, Size.Y) * 0.28f);
        style.SetCornerRadiusAll(radius);

        if (!fill)
        {
            style.SetBorderWidthAll(_isActive ? 3 : 1);
        }

        return style;
    }

    private void LayoutLabels()
    {
        float labelWidth = Mathf.Max(Size.X - 8f, 24f);

        _iconLabel.Position = new Vector2(4, 2);
        _iconLabel.Size = new Vector2(labelWidth, 18);
        _label.Position = new Vector2(4, 22);
        _label.Size = new Vector2(labelWidth, 18);
        _costLabel.Position = new Vector2(4, 42);
        _costLabel.Size = new Vector2(labelWidth, 18);
    }

    private void ApplyTextColor()
    {
        Color color = _command is { IsEnabled: false }
            ? new Color(1f, 1f, 1f, 0.42f)
            : Colors.White;

        _iconLabel.AddThemeColorOverride("font_color", color);
        _label.AddThemeColorOverride("font_color", color);
        _costLabel.AddThemeColorOverride("font_color", color);
    }
}
