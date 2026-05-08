namespace Rpg.Presentation.Battle.UI;

public sealed record BattleActionMenuCommandViewModel(
    string Id,
    string Label,
    int? ApCost = null,
    bool IsEnabled = true,
    string DisabledReason = "",
    string IconText = "");
