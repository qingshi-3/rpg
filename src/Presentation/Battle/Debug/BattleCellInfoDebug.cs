using System.Linq;
using System.Text;
using Godot;
using Rpg.Domain.Battle.Grid;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Battle.Rules;
using Rpg.Presentation.Common;
using Rpg.Presentation.World.Sites;

namespace Rpg.Presentation.Battle.Debug;

public partial class BattleCellInfoDebug : BattleDebugComponent
{
    private const string GoodColor = "#8ff0a4";
    private const string BadColor = "#ff6b6b";
    private const string WarnColor = "#ffd166";
    private const string MutedColor = "#9fb2c8";
    private const string SectionColor = "#7dd3fc";
    private const string TextColor = "#eef4ff";

    [ExportGroup("Hover 信息")]

    [Export]
    // Hover details are docked away from the cursor so range highlights stay readable.
    public Vector2 FixedPanelMargin { get; set; } = new(18, 96);

    [Export]
    public Vector2 FixedPanelSize { get; set; } = new(320, 220);

    [Export]
    public bool ShowLayerDetails { get; set; }

    [Export]
    public int MaxLayerLines { get; set; } = 2;

    private CanvasLayer _canvasLayer;
    private PanelContainer _panel;
    private RichTextLabel _label;
    private BattleMapLayer _coordinateLayer;

    public override void _Ready()
    {
        BuildPanel();
        SetPanelVisible(false);
        SetProcess(false);
    }

    public override void Configure(WorldSiteRoot siteRoot, BattleMapView battleMapView, BattleGridMap gridMap)
    {
        base.Configure(siteRoot, battleMapView, gridMap);
        battleMapView?.EnsureRuntimeData();
        _coordinateLayer = battleMapView?.CoordinateLayer;
    }

    public override void _Process(double delta)
    {
        if (!DebugEnabled ||
            SiteRoot?.AllowsDebugHoverInfo != true ||
            BattleMapView == null ||
            GridMap == null ||
            _label == null ||
            _coordinateLayer == null)
        {
            SetPanelVisible(false);
            return;
        }

        Vector2 mouseGlobal = BattleMapView.GetGlobalMousePosition();
        Vector2I tilePosition = _coordinateLayer.LocalToMap(_coordinateLayer.ToLocal(mouseGlobal));
        var position = new GridPosition(tilePosition.X, tilePosition.Y);

        BattleEntity hoveredEntity = SiteRoot?.FindEntityAt(position);

        GridMap.TryGetCell(position, out GridCell cell);
        if (hoveredEntity != null)
        {
            _label.Text = FormatEntity(hoveredEntity, cell);
            MovePanelToFixedInspectorSlot();
            SetPanelVisible(true);
            return;
        }

        if (cell == null)
        {
            SetPanelVisible(false);
            return;
        }

        _label.Text = FormatCell(cell);
        MovePanelToFixedInspectorSlot();
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
        _canvasLayer = GameUiSceneFactory.Instantiate<CanvasLayer>(
            GameUiSceneFactory.BattleCellInfoDebugPanelScenePath,
            nameof(BattleCellInfoDebug));
        if (_canvasLayer == null)
        {
            return;
        }

        AddChild(_canvasLayer);
        _panel = GameUiSceneFactory.GetRequiredNode<PanelContainer>(
            _canvasLayer,
            "Panel",
            nameof(BattleCellInfoDebug));
        _label = GameUiSceneFactory.GetRequiredNode<RichTextLabel>(
            _canvasLayer,
            "Panel/Margin/Label",
            nameof(BattleCellInfoDebug));
        ApplyFixedPanelSizing();
    }

    private void MovePanelToFixedInspectorSlot()
    {
        if (_panel == null)
        {
            return;
        }

        ApplyFixedPanelSizing();
        Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
        _panel.GlobalPosition = BattleHoverInfoPanelLayout.CalculateRightDockedPosition(
            viewportSize,
            ResolvePanelSize(),
            FixedPanelMargin);
    }

    private void ApplyFixedPanelSizing()
    {
        if (_panel != null)
        {
            _panel.CustomMinimumSize = FixedPanelSize;
            _panel.Size = FixedPanelSize;
        }

        if (_label != null)
        {
            _label.CustomMinimumSize = new Vector2(Mathf.Max(120f, FixedPanelSize.X - 18f), 0f);
        }
    }

    private Vector2 ResolvePanelSize()
    {
        if (_panel == null)
        {
            return FixedPanelSize;
        }

        return new Vector2(
            Mathf.Max(FixedPanelSize.X, _panel.Size.X),
            Mathf.Max(FixedPanelSize.Y, _panel.Size.Y));
    }

    private void SetPanelVisible(bool visible)
    {
        if (_panel != null)
        {
            _panel.Visible = visible;
        }
    }

    private static string FormatEntity(BattleEntity entity, GridCell cell)
    {
        HealthComponent health = entity.GetComponent<HealthComponent>();
        MovementComponent movement = entity.GetComponent<MovementComponent>();
        AttackComponent attack = entity.GetComponent<AttackComponent>();
        GridOccupantComponent grid = entity.GetComponent<GridOccupantComponent>();
        FactionComponent faction = entity.GetComponent<FactionComponent>();

        var builder = new StringBuilder();
        builder.Append($"{ColorText("单位", SectionColor)}  [b]{ColorText(entity.DisplayName, TextColor)}[/b]");
        if (!string.IsNullOrWhiteSpace(entity.EntityId))
        {
            builder.Append($"  {ColorText(entity.EntityId, MutedColor)}");
        }

        builder.AppendLine();
        AppendField(builder, "阵营", FactionText(faction?.Faction ?? BattleFaction.Neutral), SectionColor);

        if (grid != null)
        {
            AppendField(builder, "位置", grid.Position.ToString(), TextColor);
        }

        if (health != null)
        {
            string color = health.IsDead ? BadColor : GoodColor;
            AppendField(builder, "生命", $"{health.Hp}/{health.MaxHp}", color);
        }

        if (movement != null)
        {
            string water = movement.CanEnterWater ? "可入水" : "不可入水";
            AppendField(
                builder,
                "移动",
                $"范围 {movement.MoveRange} / {water}",
                TextColor);
        }

        if (attack != null)
        {
            AppendField(builder, "攻击", $"伤害 {attack.Damage} / 射程 {attack.Range}", TextColor);
        }

        string flags = BuildEntityFlagSummary(entity, cell);
        if (!string.IsNullOrWhiteSpace(flags))
        {
            builder.AppendLine();
            builder.Append(flags);
        }

        if (cell != null)
        {
            builder.AppendLine();
            builder.Append(ColorText($"脚下 {TerrainText(cell.TerrainTag)} H{cell.Height} C{cell.MoveCost}", MutedColor));
        }

        return builder.ToString();
    }

    private string FormatCell(GridCell cell)
    {
        bool passable = cell.IsWalkable && cell.MoveCost > 0;
        string passColor = passable ? GoodColor : BadColor;
        string tags = cell.TerrainTags.Count == 0
            ? "-"
            : string.Join(",", cell.TerrainTags.Select(SafeText));

        var builder = new StringBuilder();
        builder.Append(
            $"{ColorText("地块", SectionColor)}  " +
            $"[color={passColor}][b]{(passable ? "可通行" : "不可通行")}[/b][/color]  " +
            $"{ColorText(cell.Position.ToString(), TextColor)}  " +
            $"{ColorText($"H{cell.Height} C{cell.MoveCost}", MutedColor)}  " +
            $"{ColorText(TerrainText(cell.TerrainTag), SectionColor)}");

        if (tags != "-")
        {
            builder.Append($"  {ColorText(tags, MutedColor)}");
        }

        string flags = BuildCellFlagSummary(cell);
        if (!string.IsNullOrWhiteSpace(flags))
        {
            builder.AppendLine();
            builder.Append(flags);
        }

        if (!ShowLayerDetails)
        {
            return builder.ToString();
        }

        builder.AppendLine();
        builder.Append(ColorText($"图层 {cell.Layers.Count}", SectionColor));

        foreach (GridCellLayerData layer in cell.Layers.Take(MaxLayerLines))
        {
            builder.AppendLine();
            builder.Append("  ");
            builder.Append(FormatLayer(layer));
        }

        if (cell.Layers.Count > MaxLayerLines)
        {
            builder.AppendLine();
            builder.Append("  ");
            builder.Append(ColorText($"+{cell.Layers.Count - MaxLayerLines} 层", MutedColor));
        }

        return builder.ToString();
    }

    private static string BuildEntityFlagSummary(BattleEntity entity, GridCell cell)
    {
        var builder = new StringBuilder();

        if (BattleRuleQueries.IsDefeated(entity))
        {
            AppendFlag(builder, "已倒下", BadColor);
        }

        SelectableComponent selectable = entity.GetComponent<SelectableComponent>();
        if (selectable is { IsSelectable: false })
        {
            AppendFlag(builder, "不可选择", MutedColor);
        }

        TargetableComponent targetable = entity.GetComponent<TargetableComponent>();
        if (targetable is { IsTargetable: false })
        {
            AppendFlag(builder, "不可作为目标", MutedColor);
        }

        GridOccupantComponent grid = entity.GetComponent<GridOccupantComponent>();
        if (grid is { BlocksMovement: true })
        {
            AppendFlag(builder, "阻挡占位", WarnColor);
        }

        MovementComponent movement = entity.GetComponent<MovementComponent>();
        if (cell != null && movement != null && BattleRuleQueries.IsWater(cell) && !movement.CanEnterWater)
        {
            AppendFlag(builder, "脚下为禁入水域", BadColor);
        }

        return builder.ToString();
    }

    private static string BuildCellFlagSummary(GridCell cell)
    {
        var builder = new StringBuilder();

        if (!cell.HasFoundation)
        {
            AppendFlag(builder, "无地基", BadColor);
        }

        if (cell.IsObstacle)
        {
            AppendFlag(builder, "障碍", BadColor);
        }

        if (cell.IsHeightTransition)
        {
            AppendFlag(builder, "高度过渡", WarnColor);
        }

        if (cell.CanStandOn)
        {
            AppendFlag(builder, "特殊可站立", WarnColor);
        }

        if (cell.HasFoundationHeightConflict)
        {
            AppendFlag(builder, "高度冲突", BadColor);
        }

        return builder.ToString();
    }

    private static void AppendField(StringBuilder builder, string label, string value, string color)
    {
        builder.AppendLine();
        builder.Append($"{ColorText(label, MutedColor)}：{ColorText(value, color)}");
    }

    private static void AppendFlag(StringBuilder builder, string label, string color)
    {
        if (builder.Length > 0)
        {
            builder.Append("  ");
        }

        builder.Append(ColorText(label, color));
    }

    private static string FormatLayer(GridCellLayerData layer)
    {
        string role = RoleText(layer.Role);
        string walkText = layer.AffectsWalkability ? layer.Walkable ? "可走" : "阻挡" : "不影响通行";
        string walkColor = layer.AffectsWalkability ? layer.Walkable ? GoodColor : BadColor : MutedColor;
        string objectMark = layer.IsObstacle ? $" {ColorText("障碍", BadColor)}" : "";
        string standMark = layer.CanStandOn ? $" {ColorText("可站", WarnColor)}" : "";
        string tag = string.IsNullOrWhiteSpace(layer.TerrainTag)
            ? ""
            : $" {ColorText(TerrainText(layer.TerrainTag), MutedColor)}";

        return $"{ColorText(layer.LayerName, TextColor)} {ColorText(role, MutedColor)} H{layer.Height} {ColorText(walkText, walkColor)} C{layer.MoveCost}{standMark}{objectMark}{tag}";
    }

    private static string FactionText(BattleFaction faction)
    {
        return faction switch
        {
            BattleFaction.Player => "我方",
            BattleFaction.Enemy => "敌方",
            BattleFaction.Neutral => "中立",
            _ => faction.ToString()
        };
    }

    private static string RoleText(LayerRole role)
    {
        return role switch
        {
            LayerRole.Foundation => "地基",
            LayerRole.Detail => "细节",
            LayerRole.Object => "物体",
            LayerRole.Stair => "楼梯",
            LayerRole.Overlay => "覆盖",
            _ => role.ToString()
        };
    }

    private static string TerrainText(string terrainTag)
    {
        if (string.IsNullOrWhiteSpace(terrainTag))
        {
            return "陆地";
        }

        return terrainTag.Equals("water", System.StringComparison.OrdinalIgnoreCase)
            ? "水域"
            : SafeText(terrainTag);
    }

    private static string ColorText(string text, string color)
    {
        return $"[color={color}]{SafeText(text)}[/color]";
    }

    private static string SafeText(string text)
    {
        return (text ?? "")
            .Replace("[", "［", System.StringComparison.Ordinal)
            .Replace("]", "］", System.StringComparison.Ordinal);
    }
}
