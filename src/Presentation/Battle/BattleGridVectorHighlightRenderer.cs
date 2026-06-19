using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle;
using Rpg.Domain.Battle.Grid;

namespace Rpg.Presentation.Battle;

internal sealed class BattleGridVectorHighlightRenderer
{
    public void Render(
        BattleGridHighlightOverlay owner,
        Node2D root,
        BattleGridHighlightGeometry geometry,
        IReadOnlyDictionary<BattleGridHighlightKind, HashSet<GridPosition>> cellsByKind,
        IReadOnlyList<GridPosition> pathCells,
        IReadOnlyCollection<GridPosition> hoverCells,
        bool tacticalPauseVisualsStatic)
    {
        if (owner == null || root == null || geometry == null)
        {
            return;
        }

        AddSkillRangeDeploymentStyle(owner, root, geometry, cellsByKind, tacticalPauseVisualsStatic);
        AddPathArrows(owner, root, geometry, pathCells);
        AddTargetLockRing(owner, root, geometry, cellsByKind, tacticalPauseVisualsStatic);

        if (hoverCells?.Count > 0)
        {
            AddHoverFrame(owner, root, geometry.BuildHoverFramePolygon(hoverCells));
        }
    }

    private static void AddSkillRangeDeploymentStyle(
        BattleGridHighlightOverlay owner,
        Node2D root,
        BattleGridHighlightGeometry geometry,
        IReadOnlyDictionary<BattleGridHighlightKind, HashSet<GridPosition>> cellsByKind,
        bool tacticalPauseVisualsStatic)
    {
        if (!cellsByKind.TryGetValue(BattleGridHighlightKind.Skill, out HashSet<GridPosition> cells) ||
            cells.Count == 0)
        {
            return;
        }

        int fillZIndex = (int)BattleGridHighlightKind.Skill * 2;
        int borderZIndex = fillZIndex + 1;
        Color glowColor = WithAlpha(
            owner.SkillRangeBorderColor,
            Mathf.Clamp(owner.SkillRangeBorderColor.A * 0.24f, 0.0f, 0.42f));

        foreach (GridPosition cell in cells.OrderBy(cell => cell.Y).ThenBy(cell => cell.X))
        {
            var fillNode = new Polygon2D
            {
                Polygon = geometry.BuildCellPolygon(cell),
                Color = owner.SkillRangeFillColor,
                ZIndex = fillZIndex
            };
            root.AddChild(fillNode);
            ApplyDynamicRangeStyle(owner, fillNode, BattleGridHighlightKind.Skill, tacticalPauseVisualsStatic);
        }

        foreach ((Vector2 start, Vector2 end) in geometry.BuildBoundarySegments(cells))
        {
            var glowLine = new Line2D
            {
                Points = new[] { start, end },
                Width = owner.SkillRangeGlowWidth,
                DefaultColor = glowColor,
                ZIndex = borderZIndex
            };
            root.AddChild(glowLine);
            ApplyDynamicRangeStyle(owner, glowLine, BattleGridHighlightKind.Skill, tacticalPauseVisualsStatic);

            var borderLine = new Line2D
            {
                Points = new[] { start, end },
                Width = owner.SkillRangeBorderWidth,
                DefaultColor = owner.SkillRangeBorderColor,
                ZIndex = borderZIndex + 1
            };
            root.AddChild(borderLine);
            ApplyDynamicRangeStyle(owner, borderLine, BattleGridHighlightKind.Skill, tacticalPauseVisualsStatic);
        }
    }

    private static void AddHoverFrame(
        BattleGridHighlightOverlay owner,
        Node2D root,
        Vector2[] polygon)
    {
        if (owner.HoverFillColor.A > 0f)
        {
            root.AddChild(new Polygon2D
            {
                Polygon = polygon,
                Color = owner.HoverFillColor,
                ZIndex = (int)BattleGridHighlightKind.Hover * 2
            });
        }

        float cornerLengthRatio = Mathf.Clamp(owner.HoverCornerLengthRatio, 0.02f, 0.45f);

        for (int index = 0; index < polygon.Length; index++)
        {
            Vector2 corner = polygon[index];
            Vector2 previous = polygon[(index - 1 + polygon.Length) % polygon.Length];
            Vector2 next = polygon[(index + 1) % polygon.Length];

            AddHoverCornerSegment(owner, root, corner, corner.Lerp(previous, cornerLengthRatio));
            AddHoverCornerSegment(owner, root, corner, corner.Lerp(next, cornerLengthRatio));
        }
    }

    private static void AddHoverCornerSegment(
        BattleGridHighlightOverlay owner,
        Node2D root,
        Vector2 start,
        Vector2 end)
    {
        root.AddChild(new Line2D
        {
            Points = new[] { start, end },
            Width = owner.HoverBorderWidth,
            DefaultColor = owner.HoverBorderColor,
            ZIndex = (int)BattleGridHighlightKind.Hover * 2 + 1
        });
    }

    private static void AddTargetLockRing(
        BattleGridHighlightOverlay owner,
        Node2D root,
        BattleGridHighlightGeometry geometry,
        IReadOnlyDictionary<BattleGridHighlightKind, HashSet<GridPosition>> cellsByKind,
        bool tacticalPauseVisualsStatic)
    {
        if (!owner.ShowTargetLockRing ||
            !cellsByKind.TryGetValue(BattleGridHighlightKind.Target, out HashSet<GridPosition> cells) ||
            cells.Count == 0)
        {
            return;
        }

        Vector2[] ringPoints = geometry.BuildTargetLockFramePolygon(cells);
        if (ringPoints.Length < 4)
        {
            return;
        }

        int zIndex = (int)BattleGridHighlightKind.Target * 2 + 1;
        var glow = new Line2D
        {
            Points = ringPoints,
            Closed = true,
            Width = owner.TargetLockGlowWidth,
            DefaultColor = owner.TargetLockGlowColor,
            ZIndex = zIndex
        };
        root.AddChild(glow);
        ApplyDynamicRangeStyle(owner, glow, BattleGridHighlightKind.Target, tacticalPauseVisualsStatic);

        var ring = new Line2D
        {
            Points = ringPoints,
            Closed = true,
            Width = owner.TargetLockRingWidth,
            DefaultColor = owner.TargetLockRingColor,
            ZIndex = zIndex + 1
        };
        root.AddChild(ring);
        ApplyDynamicRangeStyle(owner, ring, BattleGridHighlightKind.Target, tacticalPauseVisualsStatic);
    }

    private static void AddPathArrows(
        BattleGridHighlightOverlay owner,
        Node2D root,
        BattleGridHighlightGeometry geometry,
        IReadOnlyList<GridPosition> pathCells)
    {
        if (!owner.ShowPathArrows || pathCells.Count < 2)
        {
            return;
        }

        for (int index = 0; index < pathCells.Count - 1; index++)
        {
            AddPathArrow(owner, root, geometry, pathCells[index], pathCells[index + 1]);
        }
    }

    private static void AddPathArrow(
        BattleGridHighlightOverlay owner,
        Node2D root,
        BattleGridHighlightGeometry geometry,
        GridPosition from,
        GridPosition to)
    {
        Vector2 fromCenter = geometry.BuildCellCenter(from);
        Vector2 toCenter = geometry.BuildCellCenter(to);
        Vector2 delta = toCenter - fromCenter;
        float length = delta.Length();
        if (length <= 0.01f)
        {
            return;
        }

        Vector2 direction = delta / length;
        float padding = Mathf.Min(
            length * 0.35f,
            geometry.GetCellHalfExtent(from) * Mathf.Clamp(owner.PathArrowCellPaddingRatio, 0f, 0.45f));
        Vector2 start = fromCenter + direction * padding;
        Vector2 end = toCenter - direction * padding;

        if ((end - start).Length() <= 1f)
        {
            start = fromCenter;
            end = toCenter;
        }

        int zIndex = (int)BattleGridHighlightKind.Hover * 2 - 1;
        root.AddChild(new Line2D
        {
            Points = new[] { start, end },
            Width = owner.PathArrowWidth,
            DefaultColor = owner.PathArrowColor,
            ZIndex = zIndex
        });

        float headLength = Mathf.Min(owner.PathArrowHeadLength, length * 0.32f);
        float headAngle = Mathf.DegToRad(owner.PathArrowHeadAngleDegrees);
        Vector2 back = -direction;
        AddPathArrowHeadSegment(owner, root, end, end + back.Rotated(headAngle) * headLength, zIndex);
        AddPathArrowHeadSegment(owner, root, end, end + back.Rotated(-headAngle) * headLength, zIndex);
    }

    private static void AddPathArrowHeadSegment(
        BattleGridHighlightOverlay owner,
        Node2D root,
        Vector2 start,
        Vector2 end,
        int zIndex)
    {
        root.AddChild(new Line2D
        {
            Points = new[] { start, end },
            Width = owner.PathArrowWidth,
            DefaultColor = owner.PathArrowColor,
            ZIndex = zIndex
        });
    }

    private static void ApplyDynamicRangeStyle(
        BattleGridHighlightOverlay owner,
        CanvasItem item,
        BattleGridHighlightKind kind,
        bool tacticalPauseVisualsStatic)
    {
        if (!ShouldAnimateOverlay(owner, kind, tacticalPauseVisualsStatic) || item == null || !owner.IsInsideTree())
        {
            return;
        }

        float minAlpha = Mathf.Clamp(owner.DynamicPulseMinAlphaMultiplier, 0.1f, 1f);
        double pulseSeconds = System.Math.Max(0.2, owner.DynamicPulseSeconds);
        item.Modulate = new Color(1f, 1f, 1f, 1f);

        Tween tween = owner.CreateTween();
        tween.BindNode(item);
        tween.SetLoops();
        tween.SetTrans(Tween.TransitionType.Sine);
        tween.SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(item, "modulate", new Color(1f, 1f, 1f, minAlpha), pulseSeconds);
        tween.TweenProperty(item, "modulate", Colors.White, pulseSeconds);
    }

    private static bool ShouldAnimateOverlay(
        BattleGridHighlightOverlay owner,
        BattleGridHighlightKind kind,
        bool tacticalPauseVisualsStatic)
    {
        return !tacticalPauseVisualsStatic && ShouldPulse(owner, kind);
    }

    private static bool ShouldPulse(BattleGridHighlightOverlay owner, BattleGridHighlightKind kind)
    {
        return owner.EnableDynamicRangeStyle &&
               ((kind == BattleGridHighlightKind.Threat && owner.PulseThreatHighlights) ||
                (kind == BattleGridHighlightKind.Attack && owner.PulseAttackHighlights) ||
                (kind == BattleGridHighlightKind.FriendlyAttack && owner.PulseAttackHighlights) ||
                (kind == BattleGridHighlightKind.Target && owner.PulseTargetHighlights) ||
                (kind == BattleGridHighlightKind.Skill && owner.PulseSkillHighlights));
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        return new Color(color.R, color.G, color.B, alpha);
    }
}
