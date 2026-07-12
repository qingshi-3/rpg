using Godot;

namespace Rpg.Presentation.World.Preview;

public partial class StrategicRegionPreviewHud : CanvasLayer
{
    [Signal]
    public delegate void ResetViewRequestedEventHandler();

    [Signal]
    public delegate void ClearSelectionRequestedEventHandler();

    private Label _cityValue = null!;
    private Label _regionValue = null!;
    private Label _roleValue = null!;
    private Label _directionValue = null!;
    private Label _statusLabel = null!;
    private Button _resetButton = null!;
    private Button _clearButton = null!;

    public override void _Ready()
    {
        _cityValue = GetNode<Label>("%CityValue");
        _regionValue = GetNode<Label>("%RegionValue");
        _roleValue = GetNode<Label>("%RoleValue");
        _directionValue = GetNode<Label>("%DirectionValue");
        _statusLabel = GetNode<Label>("%StatusLabel");
        _resetButton = GetNode<Button>("%ResetButton");
        _clearButton = GetNode<Button>("%ClearButton");
        _resetButton.Pressed += OnResetPressed;
        _clearButton.Pressed += OnClearPressed;
        ShowContext(null, null, false);
    }

    public override void _ExitTree()
    {
        if (_resetButton != null)
        {
            _resetButton.Pressed -= OnResetPressed;
        }

        if (_clearButton != null)
        {
            _clearButton.Pressed -= OnClearPressed;
        }
    }

    public void ShowContext(
        StrategicRegionPreviewCity city,
        StrategicRegionPreviewRegion region,
        bool locked)
    {
        _cityValue.Text = city?.DisplayName ?? "—";
        _regionValue.Text = region == null ? "—" : TranslateRegionName(region.RegionId);
        _roleValue.Text = region == null ? "—" : TranslateRole(region.Role);
        _directionValue.Text = region == null ? "—" : TranslateDirection(region.Direction);
        _statusLabel.Text = locked
            ? "已锁定选择；点击其他区域或城市可切换"
            : city != null
                ? "悬停预览；左键点击可锁定"
                : "将鼠标移到城池或区域上查看信息";
    }

    public void ShowError(string message)
    {
        _cityValue.Text = "加载失败";
        _regionValue.Text = "—";
        _roleValue.Text = "—";
        _directionValue.Text = "—";
        _statusLabel.Text = message;
    }

    private void OnResetPressed()
    {
        EmitSignal(SignalName.ResetViewRequested);
    }

    private void OnClearPressed()
    {
        EmitSignal(SignalName.ClearSelectionRequested);
    }

    private static string TranslateRegionName(string regionId)
    {
        return regionId switch
        {
            "qinghe_core" => "青河城区",
            "qinghe_fan" => "青河西湾",
            "qinghe_forest_ribbon" => "青河林带",
            "qinghe_bottleneck" => "青河峡口",
            "qinghe_river_delta" => "青河河扇",
            "chiyan_west_ridge" => "赤岩西脊",
            "chiyan_pass" => "赤岩关隘",
            "chiyan_high_basin" => "赤岩高盆",
            "chiyan_east_ridge" => "赤岩东脊",
            "chiyan_mine" => "赤岩矿区",
            "chiyan_wasteland" => "赤岩荒地",
            _ => regionId
        };
    }

    private static string TranslateRole(string role)
    {
        return role switch
        {
            "city-core" => "中央城区",
            "river-fan" => "河谷扇地",
            "forest-ribbon" => "河谷林带",
            "river-bottleneck" => "河谷峡口",
            "river-delta" => "河谷扇尾",
            "mountain-pass" => "山地关隘",
            "mountain-ridge" => "山地脊线",
            "mountain-basin" => "山间盆地",
            "mine" => "矿产区域",
            "wasteland" => "荒地区域",
            _ => role
        };
    }

    private static string TranslateDirection(string direction)
    {
        return direction switch
        {
            "northwest" => "西北",
            "northeast" => "东北",
            "southwest" => "西南",
            "southeast" => "东南",
            "north" => "北",
            "south" => "南",
            "west" => "西",
            "east" => "东",
            _ => direction
        };
    }
}
