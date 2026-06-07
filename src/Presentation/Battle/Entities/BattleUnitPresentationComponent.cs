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
    public Color HitOutlineColor { get; set; } = new(1f, 0.16f, 0.08f, 0.42f);

    [Export(PropertyHint.Range, "0.25,4,0.05")]
    public float HitOutlineWidth { get; set; } = 1.55f;

    [Export(PropertyHint.Range, "0.02,0.25,0.01")]
    public double HitOutlinePulseRiseSeconds { get; set; } = 0.07;

    [Export(PropertyHint.Range, "0.04,0.5,0.01")]
    public double HitOutlinePulseFallSeconds { get; set; } = 0.2;

    [Export]
    public bool RaiseEntityWhileSelected { get; set; } = true;

    [Export]
    public bool RaiseEntityWhilePreviewFocused { get; set; } = true;

    [Export]
    public bool RaiseEntityWhileTargetPreviewed { get; set; } = true;

    [Export(PropertyHint.Range, "1,2048,1")]
    public int SelectedZIndexBoost { get; set; } = 1000;

    [Export(PropertyHint.Range, "1,2048,1")]
    public int PreviewFocusZIndexBoost { get; set; } = 900;

    [Export(PropertyHint.Range, "1,2048,1")]
    public int TargetPreviewZIndexBoost { get; set; } = 1100;

    private AnimatedSprite2D _animatedSprite;
    private BattleUnitAffiliationMarker _affiliationMarker;
    private BattleUnitSelectionSpotlight _selectionSpotlight;
    private BattleUnitHealthBarComponent _healthBar;
    private ShaderMaterial _selectionMaterial;
    private BattleFaction _faction = BattleFaction.Neutral;
    private bool _selected;
    private bool _hitOutlined;
    private bool _targetPreviewed;
    private bool _previewFocused;
    private Tween _hitOutlinePulseTween;
    private float _hitOutlinePulseIntensity;
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
        ApplyHealthBarAttention();
    }

    public void SetHitOutlineVisible(bool visible)
    {
        if (!visible)
        {
            KillHitOutlinePulse();
            ApplyPresentationZIndex();
            return;
        }

        if (_hitOutlined == visible)
        {
            return;
        }

        _hitOutlined = visible;
        _hitOutlinePulseIntensity = 1f;
        ApplySelectionOutline();
        ApplyPresentationZIndex();
    }

    public void PlayHitOutlinePulse()
    {
        if (_selectionMaterial == null)
        {
            return;
        }

        KillHitOutlinePulse();
        _hitOutlined = true;
        ApplyHitOutlinePulseIntensity(0f);
        ApplyPresentationZIndex();

        // Impact feedback should read as a soft flash, not a full-strength target marker.
        _hitOutlinePulseTween = CreateTween();
        _hitOutlinePulseTween.SetTrans(Tween.TransitionType.Sine);
        _hitOutlinePulseTween.SetEase(Tween.EaseType.InOut);
        _hitOutlinePulseTween.TweenMethod(
            Callable.From<float>(ApplyHitOutlinePulseIntensity),
            0f,
            1f,
            System.Math.Max(0.02, HitOutlinePulseRiseSeconds));
        _hitOutlinePulseTween.TweenMethod(
            Callable.From<float>(ApplyHitOutlinePulseIntensity),
            1f,
            0f,
            System.Math.Max(0.04, HitOutlinePulseFallSeconds));
        _hitOutlinePulseTween.TweenCallback(Callable.From(CompleteHitOutlinePulse));
    }

    public void SetAttackTargetPreview(bool previewed)
    {
        if (_targetPreviewed == previewed)
        {
            return;
        }

        _targetPreviewed = previewed;
        ApplySelectionOutline();
        ApplyPresentationZIndex();
        ApplyHealthBarAttention();
    }

    public void SetPreviewFocus(bool focused)
    {
        if (_previewFocused == focused)
        {
            return;
        }

        _previewFocused = focused;
        ApplyPresentationZIndex();
        ApplyHealthBarAttention();
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
        _healthBar =
            Entity.GetComponent<BattleUnitHealthBarComponent>() ??
            Entity.GetNodeOrNull<BattleUnitHealthBarComponent>("BattleUnitHealthBarComponent");
        _affiliationMarker?.SetFaction(_faction);
        _selectionSpotlight?.SetSelected(_selected);
        EnsureSelectionMaterial();
        ApplySelectionOutline();
        ApplyPresentationZIndex();
        ApplyHealthBarAttention();
    }

    public override void _ExitTree()
    {
        KillHitOutlinePulse();
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
        _animatedSprite.Material = _selectionMaterial;
    }

    private void ApplySelectionOutline()
    {
        if (_selectionMaterial == null)
        {
            return;
        }

        bool outlineEnabled = _hitOutlined || _targetPreviewed || _selected;
        _selectionMaterial.SetShaderParameter(OutlineEnabledParameter, outlineEnabled);
        _selectionMaterial.SetShaderParameter(OutlineColorParameter, _hitOutlined ? ResolveHitOutlineColor() : _targetPreviewed ? HitOutlineColor : SelectionOutlineColor);
        _selectionMaterial.SetShaderParameter(OutlineWidthParameter, _hitOutlined ? ResolveHitOutlineWidth() : _targetPreviewed ? HitOutlineWidth : SelectionOutlineWidth);
    }

    private void ApplyHealthBarAttention()
    {
        _healthBar?.SetAttentionVisible(_selected || _targetPreviewed || _previewFocused);
    }

    private Color ResolveHitOutlineColor()
    {
        Color color = HitOutlineColor;
        color.A *= Mathf.Clamp(_hitOutlinePulseIntensity, 0f, 1f);
        return color;
    }

    private float ResolveHitOutlineWidth()
    {
        return Mathf.Max(0f, HitOutlineWidth * Mathf.Clamp(_hitOutlinePulseIntensity, 0f, 1f));
    }

    private void ApplyHitOutlinePulseIntensity(float intensity)
    {
        _hitOutlinePulseIntensity = Mathf.Clamp(intensity, 0f, 1f);
        ApplySelectionOutline();
    }

    private void CompleteHitOutlinePulse()
    {
        _hitOutlinePulseTween = null;
        _hitOutlinePulseIntensity = 0f;
        _hitOutlined = false;
        ApplySelectionOutline();
        ApplyPresentationZIndex();
    }

    private void KillHitOutlinePulse()
    {
        if (_hitOutlinePulseTween != null && GodotObject.IsInstanceValid(_hitOutlinePulseTween))
        {
            _hitOutlinePulseTween.Kill();
        }

        _hitOutlinePulseTween = null;
        _hitOutlinePulseIntensity = 0f;
        _hitOutlined = false;
        ApplySelectionOutline();
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

        if (!_suppressRaiseForMapSort && (_hitOutlined || _targetPreviewed) && RaiseEntityWhileTargetPreviewed)
        {
            zIndex = Mathf.Max(zIndex, _sortBaseZIndex + Mathf.Max(1, TargetPreviewZIndexBoost));
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
