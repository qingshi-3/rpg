using Godot;

namespace Rpg.Presentation.World.Sites;

public partial class StrategicCityBuildingMapEntity : Node2D
{
    private string _buildingInstanceId = "";
    private string _displayName = "";
    private Texture2D _texture;
    private Rect2 _drawRect;
    private bool _hasDrawRect;

    [Export]
    public Color BuildingTint { get; set; } = new(1f, 1f, 1f, 0.96f);

    public override void _Ready()
    {
        ZAsRelative = false;
        YSortEnabled = false;
    }

    public void SetBuilding(
        string buildingInstanceId,
        string displayName,
        Texture2D texture,
        Rect2 drawRect)
    {
        _buildingInstanceId = buildingInstanceId ?? "";
        _displayName = displayName ?? "";
        _texture = texture;
        _drawRect = drawRect;
        _hasDrawRect = drawRect.Size.X > 0.001f && drawRect.Size.Y > 0.001f;
        Visible = _hasDrawRect;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!_hasDrawRect)
        {
            return;
        }

        if (_texture != null)
        {
            DrawTexture(_texture, ResolveNativeDrawPosition(), BuildingTint);
        }
    }

    private Vector2 ResolveNativeDrawPosition()
    {
        // The footprint bounds own placement and occupancy. The sprite stays at
        // native pixel size so confirmed buildings do not pick up scaling blur.
        float textureWidth = System.Math.Max(1, _texture.GetWidth());
        float textureHeight = System.Math.Max(1, _texture.GetHeight());
        return new Vector2(
            Mathf.Round(_drawRect.Position.X + ((_drawRect.Size.X - textureWidth) * 0.5f)),
            Mathf.Round(_drawRect.Position.Y + _drawRect.Size.Y - textureHeight));
    }
}
