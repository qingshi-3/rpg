using Godot;

namespace Rpg.Presentation.World.Preview;

public partial class StrategicCityAnchorVisual : Area2D
{
    [Signal]
    public delegate void CityHoverChangedEventHandler(string cityId, bool hovered);

    [Signal]
    public delegate void CitySelectedEventHandler(string cityId);

    private Polygon2D _shadow = null!;
    private Polygon2D _body = null!;
    private Polygon2D _battlements = null!;
    private Line2D _flagPole = null!;
    private Polygon2D _flag = null!;
    private Label _label = null!;
    private Color _baseColor;

    public string CityId { get; private set; } = "";

    public override void _Ready()
    {
        _shadow = GetNode<Polygon2D>("Shadow");
        _body = GetNode<Polygon2D>("Body");
        _battlements = GetNode<Polygon2D>("Battlements");
        _flagPole = GetNode<Line2D>("FlagPole");
        _flag = GetNode<Polygon2D>("Flag");
        _label = GetNode<Label>("NameLabel");
        InputPickable = true;
        MouseEntered += OnMouseEntered;
        MouseExited += OnMouseExited;
    }

    public override void _ExitTree()
    {
        MouseEntered -= OnMouseEntered;
        MouseExited -= OnMouseExited;
    }

    public override void _InputEvent(Viewport viewport, InputEvent @event, int shapeIdx)
    {
        if (@event is not InputEventMouseButton mouseButton ||
            !mouseButton.Pressed ||
            mouseButton.ButtonIndex != MouseButton.Left)
        {
            return;
        }

        EmitSignal(SignalName.CitySelected, CityId);
        viewport.SetInputAsHandled();
    }

    public void Bind(StrategicRegionPreviewCity city, Color color)
    {
        CityId = city.CityId;
        _baseColor = color;
        Name = $"CityAnchor_{SanitizeName(CityId)}";
        Position = city.WorldPosition;
        _label.Text = city.DisplayName;
        ApplyState(false, false);
    }

    public void ApplyState(bool hovered, bool selected)
    {
        Color active = selected ? _baseColor.Lightened(0.34f) : hovered ? _baseColor.Lightened(0.2f) : _baseColor;
        _shadow.Color = new Color(0.02f, 0.025f, 0.03f, selected ? 0.92f : 0.72f);
        _body.Color = active;
        _battlements.Color = active.Lightened(0.12f);
        _flagPole.DefaultColor = active.Lightened(0.35f);
        _flag.Color = active.Lightened(0.25f);
        _label.Modulate = selected || hovered ? Colors.White : new Color(0.92f, 0.92f, 0.86f, 0.94f);
        Scale = selected ? Vector2.One * 1.12f : hovered ? Vector2.One * 1.06f : Vector2.One;
    }

    private void OnMouseEntered()
    {
        EmitSignal(SignalName.CityHoverChanged, CityId, true);
    }

    private void OnMouseExited()
    {
        EmitSignal(SignalName.CityHoverChanged, CityId, false);
    }

    private static string SanitizeName(string value)
    {
        return value.Replace(':', '_').Replace('/', '_');
    }
}
