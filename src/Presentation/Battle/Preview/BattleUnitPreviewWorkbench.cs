using Godot;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Battle.Entities;

namespace Rpg.Presentation.Battle.Preview;

public enum BattleUnitPreviewAnimation
{
    Idle = 0,
    Move = 1,
    Attack = 2,
    Hit = 3,
    Defeated = 4
}

public enum BattleUnitPreviewAnimationNameSet
{
    StandardDuelyst = 0,
    BreathingIdleDuelyst = 1
}

[Tool]
public partial class BattleUnitPreviewWorkbench : Node2D
{
    [Export]
    public BattleUnitPreviewAnimationNameSet AnimationNameSet
    {
        get => _animationNameSet;
        set
        {
            _animationNameSet = value;
            _previewDirty = true;
        }
    }

    [Export]
    public bool AutoLayoutFromSpriteFrames
    {
        get => _autoLayoutFromSpriteFrames;
        set
        {
            _autoLayoutFromSpriteFrames = value;
            _previewDirty = true;
        }
    }

    [Export]
    public float TargetMaxSpriteSizePixels
    {
        get => _targetMaxSpriteSizePixels;
        set
        {
            _targetMaxSpriteSizePixels = value;
            _previewDirty = true;
        }
    }

    [Export]
    public float GroundAnchorOffsetPixels
    {
        get => _groundAnchorOffsetPixels;
        set
        {
            _groundAnchorOffsetPixels = value;
            _previewDirty = true;
        }
    }

    [Export]
    public float VisibleAlphaThreshold
    {
        get => _visibleAlphaThreshold;
        set
        {
            _visibleAlphaThreshold = value;
            _previewDirty = true;
        }
    }

    [Export]
    public Vector2 Offset
    {
        get => _offset;
        set
        {
            _offset = value;
            _previewDirty = true;
        }
    }

    [Export]
    public Vector2 ManualScale
    {
        get => _manualScale;
        set
        {
            _manualScale = value;
            _previewDirty = true;
        }
    }

    [Export]
    public Color PreviewModulate
    {
        get => _previewModulate;
        set
        {
            _previewModulate = value;
            _previewDirty = true;
        }
    }

    [Export]
    public BattleUnitPreviewAnimation PreviewAnimation
    {
        get => _previewAnimation;
        set
        {
            _previewAnimation = value;
            PlayPreviewAnimation();
        }
    }

    [Export(PropertyHint.Range, "1,3,1")]
    public int FootprintWidth
    {
        get => _footprintWidth;
        set
        {
            _footprintWidth = System.Math.Clamp(value, 1, 3);
            _previewDirty = true;
            UpdateFootprintOverlay();
        }
    }

    [Export(PropertyHint.Range, "1,3,1")]
    public int FootprintHeight
    {
        get => _footprintHeight;
        set
        {
            _footprintHeight = System.Math.Clamp(value, 1, 3);
            _previewDirty = true;
            UpdateFootprintOverlay();
        }
    }

    [Export]
    public bool ShowFootprint
    {
        get => _showFootprint;
        set
        {
            _showFootprint = value;
            UpdateFootprintOverlay();
        }
    }

    [Export(PropertyHint.Range, "16,160,1")]
    public float FootprintCellSizePixels
    {
        get => _footprintCellSizePixels;
        set
        {
            _footprintCellSizePixels = value;
            UpdateFootprintOverlay();
        }
    }

    [Export]
    public Vector2 PreviewUnitOffset
    {
        get => _previewUnitOffset;
        set
        {
            _previewUnitOffset = value;
            _previewDirty = true;
        }
    }

    [Export]
    public NodePath PreviewRootPath { get; set; } = new("PreviewRoot");

    [Export]
    public NodePath PreviewSpritePath { get; set; } = new("PreviewRoot/AnimatedSprite2D");

    [Export]
    public NodePath FootprintOverlayPath { get; set; } = new("FootprintOverlay");

    [Export]
    public NodePath StatusLabelPath { get; set; } = new("StatusLabel");

    private BattleUnitPreviewAnimationNameSet _animationNameSet = BattleUnitPreviewAnimationNameSet.StandardDuelyst;
    private bool _autoLayoutFromSpriteFrames = true;
    private float _targetMaxSpriteSizePixels = 40f;
    private float _groundAnchorOffsetPixels = 5f;
    private float _visibleAlphaThreshold = 0.05f;
    private Vector2 _offset = Vector2.Zero;
    private Vector2 _manualScale = Vector2.One;
    private Color _previewModulate = Colors.White;
    private BattleUnitPreviewAnimation _previewAnimation;
    private int _footprintWidth = 1;
    private int _footprintHeight = 1;
    private bool _showFootprint = true;
    private float _footprintCellSizePixels = 48f;
    private Vector2 _previewUnitOffset = Vector2.Zero;
    private Node2D _previewRoot;
    private AnimatedSprite2D _previewSprite;
    private BattleUnitPreviewFootprintOverlay _footprintOverlay;
    private Label _statusLabel;
    private bool _previewDirty = true;
    private bool _warnedMissingPreviewRoot;
    private bool _warnedMissingPreviewSprite;
    private string _lastSignature = "";

    public override void _Ready()
    {
        if (!Engine.IsEditorHint())
        {
            SetProcess(false);
            return;
        }

        ResolveNodes();
        RefreshPreview();
    }

    public override void _Process(double delta)
    {
        if (!Engine.IsEditorHint() || !IsInsideTree())
        {
            return;
        }

        string signature = BuildPreviewSignature();
        if (_previewDirty || !string.Equals(signature, _lastSignature, System.StringComparison.Ordinal))
        {
            RefreshPreview();
        }
    }

    private void ResolveNodes()
    {
        _previewRoot = GetNodeOrNull<Node2D>(PreviewRootPath);
        _previewSprite = GetNodeOrNull<AnimatedSprite2D>(PreviewSpritePath);
        _footprintOverlay = GetNodeOrNull<BattleUnitPreviewFootprintOverlay>(FootprintOverlayPath);
        _statusLabel = GetNodeOrNull<Label>(StatusLabelPath);
    }

    private void RefreshPreview()
    {
        ResolveNodes();
        _previewDirty = false;
        _lastSignature = BuildPreviewSignature();

        if (_previewRoot == null)
        {
            SetStatus("预览失败：缺少 PreviewRoot。");
            WarnMissingPreviewRootOnce();
            UpdateFootprintOverlay();
            return;
        }

        if (_previewSprite == null)
        {
            SetStatus("预览失败：缺少 PreviewRoot/AnimatedSprite2D。");
            WarnMissingPreviewSpriteOnce();
            UpdateFootprintOverlay();
            return;
        }

        SpriteFrames spriteFrames = ResolvePreviewSpriteFrames();
        if (spriteFrames == null)
        {
            _previewSprite.Stop();
            SetStatus("选中 PreviewRoot/AnimatedSprite2D，将 frames.tres 拖到它的 SpriteFrames。");
            UpdateFootprintOverlay();
            return;
        }

        _previewSprite.Visible = true;
        _previewSprite.Centered = true;
        _previewSprite.Modulate = PreviewModulate;
        ApplySpriteLayout(spriteFrames);
        UpdateFootprintOverlay();
        PlayPreviewAnimation(spriteFrames);
        SetStatus(BuildStatusText(spriteFrames));
    }

    private void ApplySpriteLayout(SpriteFrames spriteFrames)
    {
        Vector2 footprintScale = ResolveFootprintVisualScale();
        if (!AutoLayoutFromSpriteFrames ||
            !BattleUnitVisualLayoutCalculator.TryCalculateAutoLayout(
                spriteFrames,
                TargetMaxSpriteSizePixels * BattleUnitVisualScale.Default.SpriteScaleMultiplier,
                GroundAnchorOffsetPixels,
                VisibleAlphaThreshold,
                out BattleUnitVisualLayout layout))
        {
            _previewSprite.Position = PreviewUnitOffset;
            _previewSprite.Offset = new Vector2(0f, Offset.Y);
            _previewSprite.Scale = ManualScale * BattleUnitVisualScale.Default.SpriteScaleMultiplier * footprintScale;
            return;
        }

        _previewSprite.Position = PreviewUnitOffset + layout.Position;
        _previewSprite.Offset = Vector2.Zero;
        _previewSprite.Scale = layout.Scale * footprintScale;
    }

    private void UpdateFootprintOverlay()
    {
        if (_footprintOverlay == null)
        {
            return;
        }

        _footprintOverlay.Configure(
            FootprintWidth,
            FootprintHeight,
            FootprintCellSizePixels,
            ShowFootprint && ResolvePreviewSpriteFrames() != null);
    }

    private void PlayPreviewAnimation()
    {
        PlayPreviewAnimation(ResolvePreviewSpriteFrames());
    }

    private void PlayPreviewAnimation(SpriteFrames spriteFrames)
    {
        if (_previewSprite == null || !GodotObject.IsInstanceValid(_previewSprite) || spriteFrames == null)
        {
            return;
        }

        string animationName = ResolveAnimationName(spriteFrames, PreviewAnimation);
        if (string.IsNullOrWhiteSpace(animationName))
        {
            _previewSprite.Stop();
            return;
        }

        _previewSprite.Animation = animationName;
        _previewSprite.Play(animationName);
    }

    private string ResolveAnimationName(SpriteFrames spriteFrames, BattleUnitPreviewAnimation animation)
    {
        string configured = ResolveConfiguredAnimationName(animation);
        string fallback = ResolveStandardAnimationName(animation);

        if (HasAnimation(spriteFrames, configured))
        {
            return configured.Trim();
        }

        if (HasAnimation(spriteFrames, fallback))
        {
            return fallback;
        }

        foreach (StringName animationName in spriteFrames.GetAnimationNames())
        {
            return animationName.ToString();
        }

        return "";
    }

    private string ResolveConfiguredAnimationName(BattleUnitPreviewAnimation animation)
    {
        if (AnimationNameSet == BattleUnitPreviewAnimationNameSet.BreathingIdleDuelyst &&
            animation == BattleUnitPreviewAnimation.Idle)
        {
            return "breathing";
        }

        return ResolveStandardAnimationName(animation);
    }

    private static string ResolveStandardAnimationName(BattleUnitPreviewAnimation animation)
    {
        return animation switch
        {
            BattleUnitPreviewAnimation.Move => "move",
            BattleUnitPreviewAnimation.Attack => "attack",
            BattleUnitPreviewAnimation.Hit => "hit",
            BattleUnitPreviewAnimation.Defeated => "defeated",
            _ => "idle"
        };
    }

    private static bool HasAnimation(SpriteFrames spriteFrames, string animationName)
    {
        return spriteFrames != null &&
               !string.IsNullOrWhiteSpace(animationName) &&
               spriteFrames.HasAnimation(animationName.Trim());
    }

    private SpriteFrames ResolvePreviewSpriteFrames()
    {
        if (_previewSprite == null)
        {
            return null;
        }

        return _previewSprite.SpriteFrames;
    }

    private Vector2 ResolveFootprintVisualScale()
    {
        int footprintSize = System.Math.Max(FootprintWidth, FootprintHeight);
        float uniformScale = 1f + ((footprintSize - 1) * BattleUnitVisualScale.Default.FootprintScaleStepMultiplier);
        return new Vector2(uniformScale, uniformScale);
    }

    private void WarnMissingPreviewRootOnce()
    {
        if (_warnedMissingPreviewRoot)
        {
            return;
        }

        _warnedMissingPreviewRoot = true;
        GameLog.Warn(nameof(BattleUnitPreviewWorkbench), $"Preview root missing path={PreviewRootPath}");
    }

    private void WarnMissingPreviewSpriteOnce()
    {
        if (_warnedMissingPreviewSprite)
        {
            return;
        }

        _warnedMissingPreviewSprite = true;
        GameLog.Warn(nameof(BattleUnitPreviewWorkbench), $"Preview sprite missing path={PreviewSpritePath}");
    }

    private void SetStatus(string text)
    {
        if (_statusLabel != null)
        {
            _statusLabel.Text = text ?? "";
        }
    }

    private string BuildStatusText(SpriteFrames spriteFrames)
    {
        string source = string.IsNullOrWhiteSpace(spriteFrames?.ResourcePath)
            ? "SpriteFrames"
            : spriteFrames.ResourcePath.GetFile();
        return $"{source}  动画 {ResolveAnimationName(spriteFrames, PreviewAnimation)}  自动布局 {AutoLayoutFromSpriteFrames}  目标 {TargetMaxSpriteSizePixels:0.##}px  占格 {FootprintWidth}x{FootprintHeight}";
    }

    private string BuildPreviewSignature()
    {
        SpriteFrames spriteFrames = ResolvePreviewSpriteFrames();
        return string.Join(
            "|",
            spriteFrames?.ResourcePath ?? "",
            AnimationNameSet,
            AutoLayoutFromSpriteFrames,
            TargetMaxSpriteSizePixels,
            GroundAnchorOffsetPixels,
            VisibleAlphaThreshold,
            Offset.ToString(),
            ManualScale.ToString(),
            PreviewModulate.ToString(),
            PreviewAnimation,
            FootprintWidth,
            FootprintHeight,
            ShowFootprint,
            FootprintCellSizePixels,
            PreviewUnitOffset.ToString(),
            PreviewRootPath.ToString(),
            PreviewSpritePath.ToString(),
            FootprintOverlayPath.ToString(),
            StatusLabelPath.ToString());
    }
}
