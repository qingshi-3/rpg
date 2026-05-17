using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle;
using Rpg.Application.World;
using Rpg.Definitions.World;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.World;

public partial class StrategicWorldRoot
{
    private bool HasConfiguredWorldMapSurface()
    {
        if (_worldMapRoot == null)
        {
            return false;
        }

        foreach (Node child in _worldMapRoot.GetChildren())
        {
            string childName = child.Name.ToString();
            if (childName is "MapAnchors")
            {
                continue;
            }

            if (child is TileMapLayer tileMapLayer)
            {
                if (tileMapLayer.GetUsedCells().Count > 0)
                {
                    return true;
                }

                continue;
            }

            return true;
        }

        return false;
    }

    private List<Vector2> GetLegacyThreatNavigationPoints(EnemyThreatPlan threat, Vector2 sourceCenter, Vector2 targetCenter)
    {
        Vector2 sourceMapPosition = ViewportLocalToMap(sourceCenter);
        Vector2 targetMapPosition = ViewportLocalToMap(targetCenter);
        if (_strategicNavigationContext.TryBuildPath(
                sourceMapPosition,
                targetMapPosition,
                out StrategicNavigationPath path,
                out string failureReason))
        {
            return path.Points.Select(MapToViewportLocal).ToList();
        }

        string failureKey = $"{threat?.Id ?? ""}:{failureReason}";
        if (_reportedThreatNavigationFailures.Add(failureKey))
        {
            GameLog.Error(
                nameof(StrategicWorldRoot),
                $"ThreatNavigationPathFailed threat={threat?.Id ?? ""} rule={threat?.RuleId ?? ""} reason={failureReason}");
        }

        return new List<Vector2>();
    }

    private static Vector2 SamplePolyline(IReadOnlyList<Vector2> points, float progress)
    {
        if (points == null || points.Count == 0)
        {
            return Vector2.Zero;
        }

        if (points.Count == 1)
        {
            return points[0];
        }

        float totalLength = 0.0f;
        for (int i = 0; i < points.Count - 1; i++)
        {
            totalLength += points[i].DistanceTo(points[i + 1]);
        }

        if (totalLength <= 0.001f)
        {
            return points[^1];
        }

        float targetLength = Mathf.Clamp(progress, 0.0f, 1.0f) * totalLength;
        float walked = 0.0f;
        for (int i = 0; i < points.Count - 1; i++)
        {
            float segmentLength = points[i].DistanceTo(points[i + 1]);
            if (segmentLength <= 0.001f)
            {
                continue;
            }

            if (walked + segmentLength >= targetLength)
            {
                float segmentProgress = (targetLength - walked) / segmentLength;
                return points[i].Lerp(points[i + 1], segmentProgress);
            }

            walked += segmentLength;
        }

        return points[^1];
    }

    private static void AddMutedLine(Container parent, string text)
    {
        Label label = GameUiSceneFactory.CreateWorldMutedLine(nameof(StrategicWorldRoot));
        if (label == null)
        {
            return;
        }

        label.Text = text;
        parent.AddChild(label);
    }

    private static void ClearChildren(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            node.RemoveChild(child);
            child.QueueFree();
        }
    }

    private static string GetControlStateLabel(SiteControlState state)
    {
        return state switch
        {
            SiteControlState.Unknown => "未知",
            SiteControlState.Neutral => "中立",
            SiteControlState.Hostile => "敌控",
            SiteControlState.Contested => "争夺中",
            SiteControlState.PlayerHeld => "玩家控制",
            SiteControlState.Damaged => "受损",
            SiteControlState.Lost => "丢失",
            _ => "未知"
        };
    }

    private static string GetSiteKindLabel(WorldSiteKind kind)
    {
        return kind switch
        {
            WorldSiteKind.Base => "据点",
            WorldSiteKind.ResourceSite => "资源点",
            WorldSiteKind.EnemySource => "敌方源头",
            _ => "场域"
        };
    }

    private static string GetSiteModeLabel(WorldSiteMode mode)
    {
        return mode switch
        {
            WorldSiteMode.Peacetime => "非战时",
            WorldSiteMode.Alert => "警戒",
            WorldSiteMode.Wartime => "战时",
            WorldSiteMode.Aftermath => "战后",
            _ => "未知"
        };
    }

    private static string GetFacilityStateLabel(FacilityState state)
    {
        return state switch
        {
            FacilityState.Planned => "规划",
            FacilityState.Building => "建造中",
            FacilityState.Active => "运行",
            FacilityState.Damaged => "受损",
            FacilityState.Disabled => "停用",
            FacilityState.Destroyed => "摧毁",
            _ => "未知"
        };
    }

    private string GetUnitLabel(string unitTypeId)
    {
        return _battleUnitFactory.ResolveUnitDisplayName(unitTypeId);
    }

    private static string GetBattleKindLabel(BattleKind kind)
    {
        return kind switch
        {
            BattleKind.AssaultSite => "攻城战",
            BattleKind.DefenseRaid => "守城战",
            BattleKind.FieldIntercept => "野外遭遇战",
            BattleKind.SearchAndExtract => "搜索撤离",
            BattleKind.Rescue => "救援战",
            BattleKind.Sabotage => "破坏战",
            BattleKind.BossAssault => "首领战",
            _ => "战斗"
        };
    }

    private static Color GetSiteColor(WorldSiteState state)
    {
        return state.ControlState switch
        {
            SiteControlState.PlayerHeld => new Color(0.52f, 0.84f, 0.68f, 1.0f),
            SiteControlState.Damaged => new Color(0.88f, 0.72f, 0.36f, 1.0f),
            SiteControlState.Hostile => new Color(0.88f, 0.38f, 0.34f, 1.0f),
            SiteControlState.Lost => new Color(0.72f, 0.28f, 0.28f, 1.0f),
            SiteControlState.Neutral => new Color(0.66f, 0.72f, 0.78f, 1.0f),
            _ => Colors.White
        };
    }

    private Rect2 GetMapBounds()
    {
        return ResolveMainWorldViewportRect();
    }

    private Rect2 ResolveMainWorldViewportRect()
    {
        Vector2 viewport = GetViewportRect().Size;
        if (_topBarHost == null ||
            _leftPrimaryPanelHost == null ||
            _mainWorldViewportHost == null)
        {
            Rect2 hostRect = _mainWorldViewportHost?.GetGlobalRect() ?? new Rect2(Vector2.Zero, viewport);
            return hostRect.Size.X > 0.0f && hostRect.Size.Y > 0.0f
                ? hostRect
                : new Rect2(Vector2.Zero, viewport);
        }

        Rect2 leftHostRect = _leftPrimaryPanelHost.GetGlobalRect();
        Rect2 topHostRect = _topBarHost.GetGlobalRect();
        if (!TryResolveFirstControlChildRect(_leftPrimaryPanelHost, leftHostRect, out Rect2 primaryPanelRect) ||
            !TryResolveFirstControlChildRect(_topBarHost, topHostRect, out Rect2 topBarRect))
        {
            Rect2 hostRect = _mainWorldViewportHost.GetGlobalRect();
            return hostRect.Size.X > 0.0f && hostRect.Size.Y > 0.0f
                ? hostRect
                : new Rect2(Vector2.Zero, viewport);
        }

        Rect2 authoredWorldRect = _mainWorldViewportHost.GetGlobalRect();
        float sideMargin = Mathf.Max(0.0f, primaryPanelRect.Position.X - leftHostRect.Position.X);
        float gapAfterPanel = Mathf.Max(sideMargin, authoredWorldRect.Position.X - primaryPanelRect.End.X);
        float topGap = Mathf.Max(0.0f, primaryPanelRect.Position.Y - topBarRect.End.Y);
        float topOffset = Mathf.Max(0.0f, authoredWorldRect.Position.Y - primaryPanelRect.Position.Y);
        float bottomMargin = Mathf.Max(sideMargin, topGap * 2.0f);
        Vector2 position = new(primaryPanelRect.End.X + gapAfterPanel, topBarRect.End.Y + topGap + topOffset);

        // MainWorldViewport follows the authored HUD host geometry. The world view no
        // longer owns a separate copy of left-panel or top-bar dimensions.
        return new Rect2(
            position,
            new Vector2(
                Mathf.Max(1.0f, viewport.X - position.X - sideMargin),
                Mathf.Max(1.0f, viewport.Y - position.Y - bottomMargin)));
    }

    private static bool TryResolveFirstControlChildRect(Control host, Rect2 hostRect, out Rect2 rect)
    {
        rect = default;
        if (host == null)
        {
            return false;
        }

        foreach (Node child in host.GetChildren())
        {
            if (child is not Control control)
            {
                continue;
            }

            Rect2 childRect = control.GetGlobalRect();
            if (childRect.Size.X > 0.0f && childRect.Size.Y > 0.0f)
            {
                rect = childRect;
                return true;
            }
        }

        if (hostRect.Size.X <= 0.0f || hostRect.Size.Y <= 0.0f)
        {
            return false;
        }

        rect = hostRect;
        return true;
    }

    private Vector2 MapToScreen(Vector2 mapPosition)
    {
        return ToRootScreen(MapToViewportLocal(mapPosition));
    }

    private Vector2 ScreenToMap(Vector2 screenPosition)
    {
        return ViewportLocalToMap(ToViewportLocal(screenPosition));
    }

    private Vector2 MapToViewportLocal(Vector2 mapPosition)
    {
        return _worldMapRoot?.ToGlobal(mapPosition) ?? mapPosition;
    }

    private Vector2 ViewportLocalToMap(Vector2 viewportLocalPosition)
    {
        return _worldMapRoot?.ToLocal(viewportLocalPosition) ?? viewportLocalPosition;
    }

    private Vector2 ToRootScreen(Vector2 viewportLocalPosition)
    {
        return (_mainWorldViewportHost?.GlobalPosition ?? Vector2.Zero) + viewportLocalPosition;
    }

    private Vector2 ToViewportLocal(Vector2 rootScreenPosition)
    {
        return rootScreenPosition - (_mainWorldViewportHost?.GlobalPosition ?? Vector2.Zero);
    }

    private Rect2 ToViewportLocal(Rect2 rootScreenRect)
    {
        return new Rect2(ToViewportLocal(rootScreenRect.Position), rootScreenRect.Size);
    }

    private static Rect2 BuildScreenRect(Vector2 a, Vector2 b)
    {
        Vector2 position = new(Mathf.Min(a.X, b.X), Mathf.Min(a.Y, b.Y));
        Vector2 size = new(Mathf.Abs(a.X - b.X), Mathf.Abs(a.Y - b.Y));
        return new Rect2(position, size);
    }

    private static bool TryIntersectSegments(
        Vector2 a,
        Vector2 b,
        Vector2 c,
        Vector2 d,
        out float segmentRatio,
        out Vector2 point)
    {
        segmentRatio = 0.0f;
        point = default;
        Vector2 r = b - a;
        Vector2 s = d - c;
        float denominator = Cross(r, s);
        if (Mathf.Abs(denominator) <= 0.0001f)
        {
            return false;
        }

        Vector2 delta = c - a;
        float t = Cross(delta, s) / denominator;
        float u = Cross(delta, r) / denominator;
        if (t < 0.0f || t > 1.0f || u < 0.0f || u > 1.0f)
        {
            return false;
        }

        segmentRatio = t;
        point = a + r * t;
        return true;
    }

    private static float Cross(Vector2 a, Vector2 b)
    {
        return a.X * b.Y - a.Y * b.X;
    }

    private static bool IsFinite(Vector2 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y);
    }

    private Rect2 MapRectToScreen(Rect2 mapRect)
    {
        return BuildScreenRect(MapToScreen(mapRect.Position), MapToScreen(mapRect.End));
    }

    private Rect2 MapRectToViewportLocal(Rect2 mapRect)
    {
        return BuildScreenRect(MapToViewportLocal(mapRect.Position), MapToViewportLocal(mapRect.End));
    }

    private bool TryGetSiteVisualScreenBounds(string siteId, out Rect2 screenBounds)
    {
        screenBounds = default;
        if (string.IsNullOrWhiteSpace(siteId) ||
            !_siteVisualFootprints.TryGetValue(siteId, out SiteVisualFootprint footprint))
        {
            return false;
        }

        screenBounds = MapRectToScreen(footprint.MapBounds);
        return true;
    }

    private bool TryGetSiteVisualViewportBounds(string siteId, out Rect2 viewportBounds)
    {
        viewportBounds = default;
        if (string.IsNullOrWhiteSpace(siteId) ||
            !_siteVisualFootprints.TryGetValue(siteId, out SiteVisualFootprint footprint))
        {
            return false;
        }

        viewportBounds = MapRectToViewportLocal(footprint.MapBounds);
        return true;
    }

    private Rect2 GetSiteHitRect(WorldSiteDefinition definition)
    {
        if (definition != null &&
            TryGetSiteVisualScreenBounds(definition.Id, out Rect2 screenBounds))
        {
            return screenBounds.Grow(SiteVisualHitPadding);
        }

        Vector2 center = GetSiteCenter(definition);
        return new Rect2(center - SiteButtonSize / 2.0f, SiteButtonSize);
    }

    private Rect2 GetSiteLabelRect(WorldSiteDefinition definition)
    {
        if (definition != null &&
            TryGetSiteVisualScreenBounds(definition.Id, out Rect2 screenBounds))
        {
            float width = Mathf.Max(SiteLabelFallbackSize.X, screenBounds.Size.X + 24.0f);
            return new Rect2(
                new Vector2(screenBounds.GetCenter().X - width / 2.0f, screenBounds.End.Y + SiteVisualLabelGap),
                new Vector2(width, SiteLabelFallbackSize.Y));
        }

        Vector2 center = GetSiteCenter(definition);
        return new Rect2(
            center + new Vector2(-SiteButtonSize.X / 2.0f - 14.0f, SiteButtonSize.Y / 2.0f - 18.0f),
            SiteLabelFallbackSize);
    }

    private Vector2 GetSiteMapPosition(WorldSiteDefinition definition)
    {
        if (definition == null)
        {
            return Vector2.Zero;
        }

        if (_worldMapRoot != null &&
            _siteAnchorRoot?.GetNodeOrNull<Node2D>(definition.Id) is { } anchor)
        {
            return _worldMapRoot.ToLocal(anchor.GlobalPosition);
        }

        return definition.MapPosition;
    }

    private Vector2 GetSiteCenter(WorldSiteDefinition definition)
    {
        if (definition == null)
        {
            return Vector2.Zero;
        }

        return MapToScreen(GetSiteMapPosition(definition));
    }

    private Vector2 GetSiteViewportCenter(WorldSiteDefinition definition)
    {
        if (definition == null)
        {
            return Vector2.Zero;
        }

        return MapToViewportLocal(GetSiteMapPosition(definition));
    }

    private static void SetFullRect(Control control)
    {
        control.AnchorLeft = 0.0f;
        control.AnchorTop = 0.0f;
        control.AnchorRight = 1.0f;
        control.AnchorBottom = 1.0f;
        control.OffsetLeft = 0.0f;
        control.OffsetTop = 0.0f;
        control.OffsetRight = 0.0f;
        control.OffsetBottom = 0.0f;
    }

    private static void SetFixedRect(Control control, Rect2 rect)
    {
        control.AnchorLeft = 0.0f;
        control.AnchorTop = 0.0f;
        control.AnchorRight = 0.0f;
        control.AnchorBottom = 0.0f;
        control.OffsetLeft = rect.Position.X;
        control.OffsetTop = rect.Position.Y;
        control.OffsetRight = rect.End.X;
        control.OffsetBottom = rect.End.Y;
    }
}
