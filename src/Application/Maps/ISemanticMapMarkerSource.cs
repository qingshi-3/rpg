namespace Rpg.Application.Maps;

public interface ISemanticMapMarkerSource
{
    bool TryResolveSemanticMarkerData(string mapId, out SemanticMapMarkerData data, out string failureReason);
}
