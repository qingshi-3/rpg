using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle;
using Rpg.Domain.Battle.Grid;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.World.Sites;

namespace Rpg.Presentation.Battle;

public partial class BattleGridHighlightOverlay
{
    private void ConfigureTileLayers()
    {
        if (_coordinateLayer?.TileSet == null)
        {
            _tileLayerRenderer.Configure(
                this,
                null,
                null,
                System.Array.Empty<BattleGridHighlightKind>(),
                null);
            return;
        }

        BattleGridHighlightKind[] drawOrder = GetTileLayerDrawOrder().ToArray();
        BattleGridHighlightTileSetSpec tileSetSpec = BattleGridHighlightTileSetFactory.Create(
            _coordinateLayer.TileSet,
            BuildTileStyles(drawOrder),
            drawOrder);
        _tileLayerRenderer.Configure(
            this,
            _coordinateLayer,
            tileSetSpec,
            drawOrder,
            ConfigureHighlightLayer);
    }

    private void ConfigureHighlightLayer(TileMapLayer layer, BattleGridHighlightKind kind)
    {
        if (IsPerceptionKind(kind))
        {
            ApplyPerceptionRangeShader(layer);
            return;
        }

        ApplyDynamicRangeStyle(layer, kind);
    }

    private void ApplyPerceptionRangeShader(TileMapLayer layer)
    {
        if (layer == null)
        {
            return;
        }

        if (_tacticalPauseVisualsStatic)
        {
            layer.Material = null;
            return;
        }

        Shader shader = GD.Load<Shader>(PerceptionRangeShaderPath);
        if (shader == null)
        {
            GD.PushWarning($"BattleGridHighlightOverlay could not load perception shader: {PerceptionRangeShaderPath}.");
            return;
        }

        var material = new ShaderMaterial
        {
            Shader = shader
        };
        material.SetShaderParameter(PerceptionPulseSpeedParameter, PerceptionPulseSpeed);
        material.SetShaderParameter(PerceptionPulseStrengthParameter, PerceptionPulseStrength);
        material.SetShaderParameter(PerceptionEdgeGlowParameter, PerceptionEdgeGlow);
        material.SetShaderParameter(PerceptionEdgeAlphaBoostParameter, PerceptionEdgeAlphaBoost);
        material.SetShaderParameter(PerceptionScanlineStrengthParameter, PerceptionScanlineStrength);
        material.SetShaderParameter(PerceptionScanlineScaleParameter, PerceptionScanlineScale);
        layer.Material = material;
    }

    private void ApplyAllCellLayers()
    {
        foreach (BattleGridHighlightKind kind in GetTileLayerDrawOrder())
        {
            if (!_cellsByKind.TryGetValue(kind, out HashSet<GridPosition> cells))
            {
                continue;
            }

            _tileLayerRenderer.SetCells(kind, cells);
        }
    }

    private (Color fill, Color border, float borderWidth) GetStyle(BattleGridHighlightKind kind)
    {
        return kind switch
        {
            BattleGridHighlightKind.Move => (MoveColor, WithAlpha(MoveColor, 0.76f), RangeBorderWidth),
            BattleGridHighlightKind.Path => (PathColor, WithAlpha(PathColor, 0.72f), RangeBorderWidth + 0.35f),
            BattleGridHighlightKind.Threat => (ThreatColor, WithAlpha(ThreatColor, 0.82f), RangeBorderWidth + 0.2f),
            BattleGridHighlightKind.Attack => (AttackColor, WithAlpha(AttackColor, 0.42f), RangeBorderWidth),
            BattleGridHighlightKind.Skill => (SkillRangeFillColor, SkillRangeBorderColor, SkillRangeBorderWidth),
            BattleGridHighlightKind.Target => (WithAlpha(TargetLockRingColor, 0f), TargetLockRingColor, TargetLockRingWidth),
            BattleGridHighlightKind.FriendlyMove => (FriendlyMoveColor, WithAlpha(FriendlyMoveColor, 0.78f), RangeBorderWidth),
            BattleGridHighlightKind.EnemyDeployment => (EnemyDeploymentColor, WithAlpha(EnemyDeploymentColor, 0.76f), RangeBorderWidth),
            BattleGridHighlightKind.FriendlyPerception => (FriendlyPerceptionColor, WithAlpha(FriendlyPerceptionColor, 0.18f), 0f),
            BattleGridHighlightKind.EnemyPerception => (EnemyPerceptionColor, WithAlpha(EnemyPerceptionColor, 0.17f), 0f),
            BattleGridHighlightKind.FriendlyAttack => (FriendlyAttackColor, WithAlpha(FriendlyAttackColor, 0.84f), RangeBorderWidth + 0.2f),
            BattleGridHighlightKind.Selected => (SelectedColor, WithAlpha(SelectedColor, 0.62f), RangeBorderWidth),
            BattleGridHighlightKind.Invalid => (InvalidColor, WithAlpha(InvalidColor, 0.45f), RangeBorderWidth),
            BattleGridHighlightKind.Hover => (HoverFillColor, HoverBorderColor, HoverBorderWidth),
            _ => (HoverFillColor, HoverBorderColor, HoverBorderWidth)
        };
    }

    private Dictionary<BattleGridHighlightKind, BattleGridHighlightStyle> BuildTileStyles(IEnumerable<BattleGridHighlightKind> kinds)
    {
        Dictionary<BattleGridHighlightKind, BattleGridHighlightStyle> styles = new();
        foreach (BattleGridHighlightKind kind in kinds)
        {
            (Color fill, Color border, float borderWidth) = GetStyle(kind);
            BattleGridHighlightTileShape shape = kind switch
            {
                BattleGridHighlightKind.FriendlyMove or BattleGridHighlightKind.EnemyDeployment => BattleGridHighlightTileShape.Square,
                BattleGridHighlightKind.FriendlyPerception or BattleGridHighlightKind.EnemyPerception => BattleGridHighlightTileShape.SoftAura,
                _ => BattleGridHighlightTileShape.Diamond
            };
            styles[kind] = new BattleGridHighlightStyle(fill, border, borderWidth, shape);
        }

        return styles;
    }

    private static bool IsPerceptionKind(BattleGridHighlightKind kind)
    {
        return kind is BattleGridHighlightKind.FriendlyPerception or BattleGridHighlightKind.EnemyPerception;
    }

    private static bool UsesTileLayer(BattleGridHighlightKind kind)
    {
        return kind is not BattleGridHighlightKind.Skill and not BattleGridHighlightKind.Target;
    }

    private static IEnumerable<BattleGridHighlightKind> GetTileLayerDrawOrder()
    {
        yield return BattleGridHighlightKind.Move;
        yield return BattleGridHighlightKind.Path;
        yield return BattleGridHighlightKind.Threat;
        yield return BattleGridHighlightKind.Attack;
        yield return BattleGridHighlightKind.FriendlyMove;
        yield return BattleGridHighlightKind.EnemyDeployment;
        yield return BattleGridHighlightKind.FriendlyPerception;
        yield return BattleGridHighlightKind.EnemyPerception;
        yield return BattleGridHighlightKind.FriendlyAttack;
        yield return BattleGridHighlightKind.Invalid;
    }
}
