namespace Rpg.Presentation.UI.ActionWheel;

public sealed record ActionWheelCommandViewModel(
    string Id,
    string Label,
    int? ApCost = null,
    bool IsEnabled = true,
    string DisabledReason = "",
    string IconText = "",
    string TargetLayerId = "",
    bool IsBackCommand = false);
