using Godot;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Battle.Entities;

namespace Rpg.Presentation.Battle.Preview;

public partial class BattleSelectionVignetteOverlay : ColorRect
{
    private const string ShaderPath = "res://assets/battle/shaders/battle_selection_vignette.gdshader";
    private const string FocusUvParameter = "focus_uv";
    private const string ViewportSizeParameter = "viewport_size";
    private const string ShadowColorParameter = "shadow_color";
    private const string InnerRadiusParameter = "inner_radius";
    private const string OuterRadiusParameter = "outer_radius";
    private const string CenterAlphaParameter = "center_alpha";

    [Export]
    public Color ShadowColor { get; set; } = new(0f, 0f, 0f, 0.38f);

    [Export(PropertyHint.Range, "0.02,0.6,0.01")]
    public float InnerRadius { get; set; } = 0.18f;

    [Export(PropertyHint.Range, "0.1,1.2,0.01")]
    public float OuterRadius { get; set; } = 0.62f;

    [Export(PropertyHint.Range, "0,0.5,0.01")]
    public float CenterAlpha { get; set; } = 0f;

    private ShaderMaterial _material;
    private BattleEntity _target;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Color = Colors.White;
        EnsureMaterial();
        ClearTarget();
    }

    public override void _Process(double delta)
    {
        UpdateFocus();
    }

    public void SetTarget(BattleEntity target)
    {
        _target = target;
        Visible = target != null && GodotObject.IsInstanceValid(target);
        SetProcess(Visible);
        UpdateFocus();
    }

    public void ClearTarget()
    {
        _target = null;
        Visible = false;
        SetProcess(false);
    }

    private void EnsureMaterial()
    {
        Shader shader = GD.Load<Shader>(ShaderPath);
        if (shader == null)
        {
            GameLog.Warn(nameof(BattleSelectionVignetteOverlay), $"Selection vignette shader missing path={ShaderPath}");
            return;
        }

        _material = new ShaderMaterial
        {
            Shader = shader,
            ResourceLocalToScene = true
        };
        Material = _material;
        ApplyStaticParameters();
    }

    private void ApplyStaticParameters()
    {
        if (_material == null)
        {
            return;
        }

        _material.SetShaderParameter(ShadowColorParameter, ShadowColor);
        _material.SetShaderParameter(InnerRadiusParameter, InnerRadius);
        _material.SetShaderParameter(OuterRadiusParameter, OuterRadius);
        _material.SetShaderParameter(CenterAlphaParameter, CenterAlpha);
    }

    private void UpdateFocus()
    {
        if (_material == null || _target == null || !GodotObject.IsInstanceValid(_target))
        {
            ClearTarget();
            return;
        }

        Vector2 viewportSize = GetViewportRect().Size;
        if (viewportSize.X <= 0f || viewportSize.Y <= 0f)
        {
            return;
        }

        Vector2 screenPosition = _target.GetGlobalTransformWithCanvas().Origin;
        Vector2 focusUv = new(
            Mathf.Clamp(screenPosition.X / viewportSize.X, 0f, 1f),
            Mathf.Clamp(screenPosition.Y / viewportSize.Y, 0f, 1f));
        _material.SetShaderParameter(FocusUvParameter, focusUv);
        _material.SetShaderParameter(ViewportSizeParameter, viewportSize);
    }
}
