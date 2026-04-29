using System.Collections.Generic;

namespace Rpg.Presentation.UI.ActionWheel;

public sealed record ActionWheelViewModel(
    string ActiveLayerId,
    string ActiveCommandId,
    IReadOnlyDictionary<string, ActionWheelLayerViewModel> Layers);
