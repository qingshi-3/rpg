using Godot;
using Rpg.Infrastructure.Logging;

namespace Rpg.Presentation.Battle.Entities;

public partial class BattleUnitPresentationComponent : BattleEntityComponent
{
    private const string OutlineEnabledParameter = "outline_enabled";
    private const string OutlineColorParameter = "outline_color";
    private const string OutlineWidthParameter = "outline_width";
    private const string SelectionOutlineShaderPath = "res://assets/battle/shaders/unit_selection_outline.gdshader";

    [Export]
    public NodePath AnimatedSpritePath { get; set; } = "VisualRoot/AnimatedSprite2D";

    [Export]
    public NodePath AffiliationMarkerPath { get; set; } = "AffiliationMarker";

    [Export]
    public NodePath SelectionSpotlightPath { get; set; } = "SelectionSpotlight";

    [Export]
    public Color SelectionOutlineColor { get; set; } = new(1f, 0.92f, 0.34f, 1f);

    [Export(PropertyHint.Range, "0.5,6,0.25")]
    public float SelectionOutlineWidth { get; set; } = 2.0f;

    [Export]
    public bool RaiseEntityWhileSelected { get; set; } = true;

    [Export]
    public bool RaiseEntityWhilePreviewFocused { get; set; } = true;

    [Export(PropertyHint.Range, "1,2048,1")]
    public int SelectedZIndexBoost { get; set; } = 1000;

    [Export(PropertyHint.Range, "1,2048,1")]
    public int PreviewFocusZIndexBoost { get; set; } = 900;

    private AnimatedSprite2D _animatedSprite;
    private BattleUnitAffiliationMarker _affiliationMarker;
    private BattleUnitSelectionSpotlight _selectionSpotlight;
    private ShaderMaterial _selectionMaterial;
    private BattleFaction _faction = BattleFaction.Neutral;
    private bool _selected;
    private bool _previewFocused;
    private bool _hasOriginalZIndex;
    private int _originalZIndex;
    private int _sortBaseZIndex;
    private bool _suppressRaiseForMapSort;

    public void SetFaction(BattleFaction faction)
    {
        _faction = faction;
        _affiliationMarker?.SetFaction(faction);
    }

    public void SetSelected(bool selected)
    {
        if (_selected == selected)
        {
            return;
        }

        _selected = selected;
        _selectionSpotlight?.SetSelected(selected);
        ApplySelectionOutline();
        ApplyPresentationZIndex();
    }

    public void SetPreviewFocus(bool focused)
    {
        if (_previewFocused == focused)
        {
            return;
        }

        _previewFocused = focused;
        ApplyPresentationZIndex();
    }

    public void SetMapSortZIndex(int zIndex, bool suppressRaise)
    {
        CaptureOriginalZIndex();
        _sortBaseZIndex = zIndex;
        _suppressRaiseForMapSort = suppressRaise;
        ApplyPresentationZIndex();
    }

    protected override void OnAttached()
    {
        CaptureOriginalZIndex();
        _animatedSprite = Entity.GetNodeOrNull<AnimatedSprite2D>(AnimatedSpritePath);
        _affiliationMarker = Entity.GetNodeOrNull<BattleUnitAffiliationMarker>(AffiliationMarkerPath);
        _selectionSpotlight = Entity.GetNodeOrNull<BattleUnitSelectionSpotlight>(SelectionSpotlightPath);
        _affiliationMarker?.SetFaction(_faction);
        _selectionSpotlight?.SetSelected(_selected);
        EnsureSelectionMaterial();
        ApplySelectionOutline();
        ApplyPresentationZIndex();
    }

    public override void _ExitTree()
    {
        RestoreOriginalZIndex();
    }

    private void EnsureSelectionMaterial()
    {
        if (_animatedSprite == null)
        {
            GameLog.Warn(nameof(BattleUnitPresentationComponent), $"Unit presentation missing AnimatedSprite2D entity={Entity?.EntityId}");
            return;
        }

        Shader shader = GD.Load<Shader>(SelectionOutlineShaderPath);
        if (shader == null)
        {
            GameLog.Warn(nameof(BattleUnitPresentationComponent), $"Selection outline shader missing path={SelectionOutlineShaderPath}");
            return;
        }

        _selectionMaterial = new ShaderMaterial
        {
            Shader = shader,
            ResourceLocalToScene = true
        };
        _selectionMaterial.SetShaderParameter(OutlineColorParameter, SelectionOutlineColor);
        _selectionMaterial.SetShaderParameter(OutlineWidthParameter, SelectionOutlineWidth);
        _animatedSprite.Material = _selectionMaterial;
    }

    private void ApplySelectionOutline()
    {
        _selectionMaterial?.SetShaderParameter(OutlineEnabledParameter, _selected);
    }

    private void ApplyPresentationZIndex()
    {
        if (Entity == null)
        {
            return;
        }

        CaptureOriginalZIndex();
        int zIndex = _sortBaseZIndex;
        if (!_suppressRaiseForMapSort && _previewFocused && RaiseEntityWhilePreviewFocused)
        {
            zIndex = Mathf.Max(zIndex, _sortBaseZIndex + Mathf.Max(1, PreviewFocusZIndexBoost));
        }

        if (!_suppressRaiseForMapSort && _selected && RaiseEntityWhileSelected)
        {
            zIndex = Mathf.Max(zIndex, _sortBaseZIndex + Mathf.Max(1, SelectedZIndexBoost));
        }

        Entity.ZIndex = zIndex;
    }

    private void CaptureOriginalZIndex()
    {
        if (_hasOriginalZIndex || Entity == null)
        {
            return;
        }

        _originalZIndex = Entity.ZIndex;
        _sortBaseZIndex = _originalZIndex;
        _hasOriginalZIndex = true;
    }

    private void RestoreOriginalZIndex()
    {
        if (!_hasOriginalZIndex || Entity == null)
        {
            return;
        }

        Entity.ZIndex = _originalZIndex;
    }
}
