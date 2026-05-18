using System.Collections.Generic;

namespace Rpg.Application.Maps;

public sealed class SemanticMapMarkerExtractionResult
{
    public List<SemanticMapMarkerData> Markers { get; } = new();
    public List<string> Diagnostics { get; } = new();
}
