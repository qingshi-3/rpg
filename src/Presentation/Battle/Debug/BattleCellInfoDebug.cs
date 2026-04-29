using System.Linq;
using System.Text;
using Godot;
using Rpg.Domain.Battle.Grid;

namespace Rpg.Presentation.Battle.Debug;

public partial class BattleCellInfoDebug : BattleDebugComponent
{
    [ExportGroup("Hover信息")]

    [Export]
    public Vector2 PanelOffset { get; set; } = new(18, 18);

    [Export]
    public int MaxLayerLines { get; set; } = 8;

    private CanvasLayer _canvasLayer;
    private PanelContainer _panel;
    private Label _label;
    private BattleMapLayer _coordinateLayer;

    public override void _Ready()
    {
        BuildPanel();
        SetPanelVisible(false);
        SetProcess(false);
    }

    public override void Configure(BattleRoot battleRoot, BattleMapView battleMapView, BattleGridMap gridMap)
    {
        base.Configure(battleRoot, battleMapView, gridMap);
        _coordinateLayer = battleMapView == null ? null : BattleMapLayerQueries.FindCoordinateLayer(battleMapView);
    }

    public override void _Process(double delta)
    {
        if (!DebugEnabled || BattleMapView == null || GridMap == null || _coordinateLayer == null)
        {
            SetPanelVisible(false);
            return;
        }

        Vector2 mouseGlobal = BattleMapView.GetGlobalMousePosition();
        Vector2I tilePosition = _coordinateLayer.LocalToMap(_coordinateLayer.ToLocal(mouseGlobal));
        var position = new GridPosition(tilePosition.X, tilePosition.Y);

        if (!GridMap.TryGetCell(position, out GridCell cell))
        {
            SetPanelVisible(false);
            return;
        }

        _label.Text = FormatCell(cell);
        _panel.GlobalPosition = GetViewport().GetMousePosition() + PanelOffset;
        SetPanelVisible(true);
    }

    protected override void OnDebugEnabledChanged(bool enabled)
    {
        SetProcess(enabled);

        if (!enabled)
        {
            SetPanelVisible(false);
        }
    }

    private void BuildPanel()
    {
        _canvasLayer = new CanvasLayer();
        AddChild(_canvasLayer);

        _panel = new PanelContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore
        };

        var style = new StyleBoxFlat
        {
            BgColor = new Color(0, 0, 0, 0.82f),
            BorderColor = new Color(1, 1, 1, 0.25f)
        };
        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(4);
        _panel.AddThemeStyleboxOverride("panel", style);

        var margin = new MarginContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        margin.AddThemeConstantOverride("margin_left", 8);
        margin.AddThemeConstantOverride("margin_top", 6);
        margin.AddThemeConstantOverride("margin_right", 8);
        margin.AddThemeConstantOverride("margin_bottom", 6);

        _label = new Label
        {
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _label.AddThemeColorOverride("font_color", Colors.White);

        margin.AddChild(_label);
        _panel.AddChild(margin);
        _canvasLayer.AddChild(_panel);
    }

    private void SetPanelVisible(bool visible)
    {
        if (_panel != null)
        {
            _panel.Visible = visible;
        }
    }

    private string FormatCell(GridCell cell)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"地块 {cell.Position}");
        builder.AppendLine($"高度：{cell.Height}");
        builder.AppendLine($"有地基：{BoolText(cell.HasFoundation)}");
        builder.AppendLine($"可行走：{BoolText(cell.IsWalkable)}");
        builder.AppendLine($"阻挡视线：{BoolText(cell.BlocksLineOfSight)}");
        builder.AppendLine($"高度转换：{BoolText(cell.IsHeightTransition)}");

        if (cell.HasFoundationHeightConflict)
        {
            builder.AppendLine("地基高度冲突：是");
        }

        builder.AppendLine($"来源图层：{cell.Layers.Count}");

        foreach (GridCellLayerData layer in cell.Layers.Take(MaxLayerLines))
        {
            builder.AppendLine(
                $"- {layer.LayerName} {RoleText(layer.Role)} 高度={layer.Height} 源={layer.SourceId} 图集=({layer.AtlasX},{layer.AtlasY}) 变体={layer.AlternativeTile}");
        }

        if (cell.Layers.Count > MaxLayerLines)
        {
            builder.AppendLine($"另有 {cell.Layers.Count - MaxLayerLines} 层未显示");
        }

        return builder.ToString();
    }

    private static string BoolText(bool value)
    {
        return value ? "是" : "否";
    }

    private static string RoleText(LayerRole role)
    {
        return role switch
        {
            LayerRole.Foundation => "地基",
            LayerRole.Detail => "细节",
            LayerRole.Object => "物件",
            LayerRole.Stair => "楼梯",
            LayerRole.Overlay => "覆盖",
            _ => role.ToString()
        };
    }
}
