using System.Collections.Generic;
using Godot;
using Rpg.Definitions.Battle;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.World.Sites;

public partial class BattleDestinationBeaconMarker : Node2D
{
    private const string BaseOutlineColorParameter = "base_outline_color";
    private const string BaseOutlineWidthParameter = "base_outline_width";
    private const string ActiveOutlineEnabledParameter = "active_outline_enabled";
    private const string ActiveOutlineColorParameter = "active_outline_color";
    private const string ActiveOutlineWidthParameter = "active_outline_width";
    private const string UnitBodyOutlineShaderPath = "res://resource/shaders/battle/unit_body_outline.gdshader";

    [Export]
    public SpriteFrames HeroSpriteFrames { get; set; }

    [Export]
    public string HeroAnimationName { get; set; } = "idle";

    [Export]
    public BattleUnitVisualDefinition HeroVisual { get; set; }

    [Export]
    public NodePath PlinthPreviewPath { get; set; } = new("DecorationRoot/PlinthPreview");

    [Export]
    public NodePath TargetCellFramePath { get; set; } = new("TargetCellFrame");

    [Export]
    public NodePath DecorationRootPath { get; set; } = new("DecorationRoot");

    [Export]
    public NodePath PlinthPath { get; set; } = new("DecorationRoot/PlinthPreview/Plinth");

    [Export]
    public NodePath HeroPreviewSpritePath { get; set; } = new("DecorationRoot/PlinthPreview/HeroPreview/AnimatedSprite2D");

    [Export]
    // Keep the scaled plinth preview clear of the floating arrow; viewport
    // avoidance moves this same decoration root below the target cell.
    public Vector2 DecorationAboveOffset { get; set; } = new(0f, -42f);

    [Export]
    public float DecorationTopExtent { get; set; } = 66f;

    [Export]
    public float DecorationBottomOffset { get; set; } = 52f;

    [Export]
    public float ViewportTopMargin { get; set; } = 12f;

    [Export]
    public Color SelectionOutlineColor { get; set; } = new(1f, 0.92f, 0.34f, 1f);

    [Export(PropertyHint.Range, "0.5,6,0.25")]
    public float SelectionOutlineWidth { get; set; } = 1.5f;

    private BattleUnitPlinthPreview _plinthPreview;
    private BattleDestinationBeaconCellFrame _targetCellFrame;
    private Sprite2D _plinth;
    private AnimatedSprite2D _heroPreviewSprite;
    private Node2D _decorationRoot;
    private Shader _outlineShader;
    private ShaderMaterial _plinthOutlineMaterial;
    private ShaderMaterial _heroPreviewOutlineMaterial;
    private bool _selected;

    public override void _Ready()
    {
        ApplyBinding();
        ApplyDecorationOffset(DecorationAboveOffset);
        ApplySelectionOutline();
    }

    public void Bind(
        BattleUnitAnimatedPreviewModel heroPreview,
        IReadOnlyList<Vector2> targetCellPolygon = null,
        bool selected = false)
    {
        HeroSpriteFrames = heroPreview?.SpriteFrames;
        HeroAnimationName = heroPreview?.AnimationName ?? "idle";
        HeroVisual = heroPreview?.Visual;
        _selected = selected;
        ApplyBinding();
        ApplyTargetCellPolygon(targetCellPolygon);
        ApplySelectionOutline();
    }

    public void ApplyViewportAvoidance(Vector2 markerViewportPosition, Vector2 viewportSize)
    {
        bool hasViewport = viewportSize.X > 0f && viewportSize.Y > 0f;
        bool moveDecorationBelow = hasViewport &&
            markerViewportPosition.Y - Mathf.Max(0f, DecorationTopExtent) < ViewportTopMargin;
        ApplyDecorationOffset(moveDecorationBelow
            ? new Vector2(DecorationAboveOffset.X, DecorationBottomOffset)
            : DecorationAboveOffset);
    }

    private void ApplyBinding()
    {
        _plinthPreview ??= GetNodeOrNull<BattleUnitPlinthPreview>(PlinthPreviewPath);
        if (_plinthPreview == null)
        {
            return;
        }

        _plinthPreview.Bind(HeroSpriteFrames, HeroAnimationName, HeroVisual);
    }

    private void ApplyDecorationOffset(Vector2 offset)
    {
        _decorationRoot ??= GetNodeOrNull<Node2D>(DecorationRootPath);
        if (_decorationRoot == null)
        {
            return;
        }

        // Only the decorative hero/plinth flips for viewport safety; the arrow
        // and cell frame stay anchored to the commanded destination cell.
        _decorationRoot.Position = offset;
    }

    private void ApplySelectionOutline()
    {
        ResolveSelectionOutlineNodes();
        ApplySelectionOutlineMaterial(_plinth, ref _plinthOutlineMaterial);
        ApplySelectionOutlineMaterial(_heroPreviewSprite, ref _heroPreviewOutlineMaterial);
    }

    private void ResolveSelectionOutlineNodes()
    {
        _plinth ??= GetNodeOrNull<Sprite2D>(PlinthPath);
        _heroPreviewSprite ??= GetNodeOrNull<AnimatedSprite2D>(HeroPreviewSpritePath);
    }

    private void ApplySelectionOutlineMaterial(CanvasItem target, ref ShaderMaterial material)
    {
        if (target == null)
        {
            return;
        }

        material ??= CreateSelectionOutlineMaterial();
        if (material == null)
        {
            return;
        }

        target.Material = material;
        material.SetShaderParameter(BaseOutlineColorParameter, Colors.Transparent);
        material.SetShaderParameter(BaseOutlineWidthParameter, 0f);
        material.SetShaderParameter(ActiveOutlineEnabledParameter, _selected);
        material.SetShaderParameter(ActiveOutlineColorParameter, SelectionOutlineColor);
        material.SetShaderParameter(ActiveOutlineWidthParameter, SelectionOutlineWidth);
    }

    private ShaderMaterial CreateSelectionOutlineMaterial()
    {
        _outlineShader ??= GD.Load<Shader>(UnitBodyOutlineShaderPath);
        return _outlineShader == null
            ? null
            : new ShaderMaterial { Shader = _outlineShader, ResourceLocalToScene = true };
    }

    private void ApplyTargetCellPolygon(IReadOnlyList<Vector2> targetCellPolygon)
    {
        _targetCellFrame ??= GetNodeOrNull<BattleDestinationBeaconCellFrame>(TargetCellFramePath);
        if (_targetCellFrame == null || targetCellPolygon == null)
        {
            return;
        }

        _targetCellFrame.SetCellPolygon(targetCellPolygon);
    }
}
