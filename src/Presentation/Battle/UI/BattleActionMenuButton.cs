using System;
using Godot;

namespace Rpg.Presentation.Battle.UI;

public partial class BattleActionMenuButton : Button
{
    private BattleActionMenuCommandViewModel _command;
    private Label _iconLabel;
    private Label _nameLabel;
    private Label _costLabel;
    private PanelContainer _costBadge;

    public event Action<BattleActionMenuCommandViewModel> CommandHovered;
    public event Action<BattleActionMenuCommandViewModel> CommandSelected;
    public event Action<BattleActionMenuCommandViewModel> InvalidCommandSelected;

    public BattleActionMenuCommandViewModel Command => _command;

    public override void _Ready()
    {
        FocusMode = FocusModeEnum.None;
        ToggleMode = true;
        MouseFilter = MouseFilterEnum.Stop;
        Text = "";
        _iconLabel = GetNodeOrNull<Label>("Content/IconLabel");
        _nameLabel = GetNodeOrNull<Label>("Content/NameLabel");
        _costBadge = GetNodeOrNull<PanelContainer>("Content/CostBadge");
        _costLabel = GetNodeOrNull<Label>("Content/CostBadge/CostLabel");
        Pressed += OnPressed;
        MouseEntered += OnMouseEntered;
        ApplyCommandVisuals();
    }

    public void SetCommand(BattleActionMenuCommandViewModel command)
    {
        _command = command;
        TooltipText = command?.IsEnabled == false ? command.DisabledReason : "";
        ApplyCommandVisuals();
    }

    public void SetActive(bool active)
    {
        ButtonPressed = active;
        ApplyCommandVisuals();
    }

    private void OnPressed()
    {
        if (_command == null)
        {
            ButtonPressed = false;
            return;
        }

        if (!_command.IsEnabled)
        {
            ButtonPressed = false;
            InvalidCommandSelected?.Invoke(_command);
            return;
        }

        CommandSelected?.Invoke(_command);
    }

    private void OnMouseEntered()
    {
        if (_command != null)
        {
            CommandHovered?.Invoke(_command);
        }
    }

    private void ApplyCommandVisuals()
    {
        if (_command == null)
        {
            Text = "";
            if (_iconLabel != null)
            {
                _iconLabel.Text = "";
            }

            if (_nameLabel != null)
            {
                _nameLabel.Text = "";
            }

            if (_costBadge != null)
            {
                _costBadge.Visible = false;
            }

            return;
        }

        string iconText = string.IsNullOrWhiteSpace(_command.IconText) ? "令" : _command.IconText;
        string costText = _command.ApCost.HasValue ? $"{_command.ApCost.Value}AP" : "";

        if (_iconLabel != null && _nameLabel != null)
        {
            Text = "";
            _iconLabel.Text = iconText;
            _nameLabel.Text = _command.Label;

            if (_costBadge != null)
            {
                _costBadge.Visible = !string.IsNullOrWhiteSpace(costText);
            }

            if (_costLabel != null)
            {
                _costLabel.Text = costText;
            }
        }
        else
        {
            string prefix = string.IsNullOrWhiteSpace(iconText) ? "" : $"{iconText}  ";
            string cost = string.IsNullOrWhiteSpace(costText) ? "" : $"  {costText}";
            Text = $"{prefix}{_command.Label}{cost}";
        }

        float alpha = _command.IsEnabled ? 1f : 0.46f;
        SelfModulate = ButtonPressed
            ? new Color(1.08f, 1.03f, 0.86f, alpha)
            : new Color(1f, 1f, 1f, alpha);
    }
}
