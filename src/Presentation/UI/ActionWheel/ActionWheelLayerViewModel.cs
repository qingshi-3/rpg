using System.Collections.Generic;

namespace Rpg.Presentation.UI.ActionWheel;

public sealed record ActionWheelLayerViewModel(
    string Id,
    string ParentLayerId,
    IReadOnlyList<ActionWheelCommandViewModel> Commands);
