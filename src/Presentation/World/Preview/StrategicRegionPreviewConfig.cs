using Godot;

namespace Rpg.Presentation.World.Preview;

[GlobalClass]
public partial class StrategicRegionPreviewConfig : Resource
{
    public static Rect2 DefaultPreviewBounds => new(2048f, 1024f, 3072f, 2048f);

    [ExportGroup("Content")]
    [Export]
    public Rect2 PreviewBounds { get; set; } = DefaultPreviewBounds;

    [Export]
    public string PlayerCityId { get; set; } = "city_qinghe";

    [Export]
    public string HostileCityId { get; set; } = "city_chiyan";

    [ExportGroup("Camera")]
    [Export(PropertyHint.Range, "0.35,2.0,0.01")]
    public float InitialZoom { get; set; } = 0.48f;

    [ExportGroup("Reference Map")]
    [Export(PropertyHint.Range, "0,1,0.01")]
    public float ReferenceOpacity { get; set; } = 0.78f;

    [ExportGroup("Territory Presentation")]
    [Export]
    public string TerritoryMaskPath { get; set; } = "res://assets/textures/world/masks/territory/territory_mask.png";

    [Export(PropertyHint.Range, "0.05,1.0,0.01")]
    public float TerritoryMaskScale { get; set; } = 0.25f;

    [Export]
    public Color PlayerColor { get; set; } = new("4faee8");

    [Export]
    public Color HostileColor { get; set; } = new("e65f45");

    [Export]
    public Color NeutralColor { get; set; } = new("d1bd78");

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float IdleRegionAlpha { get; set; } = 0f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float ContextRegionAlpha { get; set; } = 0.015f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float HoverRegionAlpha { get; set; } = 0.12f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float SelectedRegionAlpha { get; set; } = 0.18f;

    [Export(PropertyHint.Range, "1,12,0.25")]
    public float RegionOutlineWidth { get; set; } = 3f;

    [Export(PropertyHint.Range, "1,16,0.25")]
    public float CityOutlineWidth { get; set; } = 7f;

    public Color ResolveCityColor(string cityId)
    {
        if (cityId == PlayerCityId)
        {
            return PlayerColor;
        }

        if (cityId == HostileCityId)
        {
            return HostileColor;
        }

        return NeutralColor;
    }
}
