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
    public override void _Draw()
    {
        if (Definition == null)
        {
            return;
        }

        StrategicWorldDefinitionQueries queries = new(Definition);
        Rect2 mapBounds = GetMapBounds();
        if (!HasConfiguredWorldMapSurface())
        {
            DrawStrategicMapBackground(mapBounds);
        }

        DrawSiteIcons(queries);
        DrawLegacyThreatMarkers(queries);
        DrawWorldOpportunities(queries);
        DrawWorldArmies();
        DrawArmySelectionBox();
    }

    private void DrawStrategicMapBackground(Rect2 mapBounds)
    {
        DrawRect(mapBounds, new Color(0.18f, 0.25f, 0.19f, 1.0f), true);
        DrawRect(mapBounds, new Color(0.04f, 0.05f, 0.045f, 1.0f), false, 2.0f);

        // 地貌先作为战略地图底色表达，不替代后续手工 TileMap。
        DrawRect(new Rect2(mapBounds.Position + new Vector2(28.0f, 34.0f), new Vector2(390.0f, 180.0f)), new Color(0.22f, 0.33f, 0.21f, 0.92f), true);
        DrawRect(new Rect2(mapBounds.Position + new Vector2(510.0f, 60.0f), new Vector2(280.0f, 150.0f)), new Color(0.34f, 0.33f, 0.27f, 0.82f), true);
        DrawRect(new Rect2(mapBounds.Position + new Vector2(840.0f, 64.0f), new Vector2(310.0f, 190.0f)), new Color(0.22f, 0.21f, 0.23f, 0.86f), true);

        Vector2[] river =
        {
            mapBounds.Position + new Vector2(60.0f, mapBounds.Size.Y - 210.0f),
            mapBounds.Position + new Vector2(300.0f, mapBounds.Size.Y - 176.0f),
            mapBounds.Position + new Vector2(560.0f, mapBounds.Size.Y - 205.0f),
            mapBounds.Position + new Vector2(840.0f, mapBounds.Size.Y - 150.0f),
            mapBounds.Position + new Vector2(1120.0f, mapBounds.Size.Y - 178.0f)
        };
        for (int i = 0; i < river.Length - 1; i++)
        {
            DrawLine(river[i], river[i + 1], new Color(0.18f, 0.34f, 0.42f, 0.72f), 16.0f, true);
            DrawLine(river[i], river[i + 1], new Color(0.32f, 0.55f, 0.64f, 0.78f), 5.0f, true);
        }

        for (int i = 0; i < 6; i++)
        {
            Vector2 basePoint = mapBounds.Position + new Vector2(890.0f + i * 46.0f, 118.0f + (i % 2) * 22.0f);
            DrawPolygon(
                new[] { basePoint + new Vector2(0, 38), basePoint + new Vector2(24, 0), basePoint + new Vector2(48, 38) },
                new[] { new Color(0.28f, 0.27f, 0.29f, 0.94f) });
        }
    }

    private void DrawLegacyThreatMarkers(StrategicWorldDefinitionQueries queries)
    {
        foreach (EnemyThreatPlan threat in State.ThreatPlans.Values.Where(threat => threat.Stage != ThreatStage.Resolved))
        {
            if (!string.IsNullOrWhiteSpace(threat.WorldArmyId) &&
                State.ArmyStates.ContainsKey(threat.WorldArmyId))
            {
                continue;
            }

            WorldSiteDefinition source = queries.GetSite(threat.SourceSiteId);
            WorldSiteDefinition target = queries.GetSite(threat.TargetSiteId);
            if (source == null || target == null)
            {
                continue;
            }

            Vector2 sourceCenter = GetSiteCenter(source);
            Vector2 targetCenter = GetSiteCenter(target);
            List<Vector2> navigationPoints = GetLegacyThreatNavigationPoints(threat, sourceCenter, targetCenter);
            if (navigationPoints.Count == 0)
            {
                continue;
            }

            if (threat.Stage == ThreatStage.Attacking)
            {
                if (!IsScreenPositionVisible(targetCenter))
                {
                    continue;
                }

                DrawArc(targetCenter, SiteIconRadius + 13.0f, 0, Mathf.Tau, 40, new Color(1.0f, 0.23f, 0.15f, 0.92f), 4.0f, true);
                DrawThreatArmyMarker(targetCenter + new Vector2(34.0f, -28.0f), true);
                continue;
            }

            int initialCountdown = threat.InitialCountdownTicks > 0 ? threat.InitialCountdownTicks : 3;
            float progress = 1.0f - Mathf.Clamp(threat.CountdownTicks / (float)initialCountdown, 0.0f, 1.0f);
            Vector2 marker = SamplePolyline(navigationPoints, Mathf.Clamp(0.08f + progress * 0.84f, 0.08f, 0.92f));
            if (!IsScreenPositionVisible(marker))
            {
                continue;
            }

            DrawThreatArmyMarker(marker, false);
        }
    }

    private void DrawThreatArmyMarker(Vector2 position, bool attacking)
    {
        Color fill = attacking
            ? new Color(1.0f, 0.12f, 0.08f, 1.0f)
            : new Color(0.88f, 0.18f, 0.12f, 1.0f);
        Color dark = new(0.12f, 0.035f, 0.03f, 1.0f);
        DrawRect(new Rect2(position - new Vector2(11.0f, 11.0f), new Vector2(22.0f, 22.0f)), dark, true);
        DrawRect(new Rect2(position - new Vector2(8.0f, 8.0f), new Vector2(16.0f, 16.0f)), fill, true);
        DrawLine(position + new Vector2(-2.0f, -16.0f), position + new Vector2(-2.0f, 12.0f), dark, 3.0f, true);
        DrawPolygon(
            new[] { position + new Vector2(0.0f, -16.0f), position + new Vector2(18.0f, -10.0f), position + new Vector2(0.0f, -4.0f) },
            new[] { attacking ? new Color(1.0f, 0.32f, 0.14f, 1.0f) : new Color(0.78f, 0.12f, 0.08f, 1.0f) });
    }

    private void DrawWorldArmies()
    {
        if (State?.ArmyStates == null)
        {
            return;
        }

        foreach (WorldArmyState army in State.ArmyStates.Values)
        {
            if (army.Status == WorldArmyStatus.Defeated ||
                army.Status == WorldArmyStatus.Garrisoned)
            {
                continue;
            }

            if (army.OwnerFactionId != State.PlayerFactionId && !IsMapPositionVisible(army.WorldPosition))
            {
                continue;
            }

            DrawWorldArmyMarker(army);
        }
    }

    private void DrawWorldOpportunities(StrategicWorldDefinitionQueries queries)
    {
        if (State?.OpportunityStates == null)
        {
            return;
        }

        foreach (WorldOpportunityState opportunity in State.OpportunityStates.Values)
        {
            if (opportunity.Status != WorldOpportunityStatus.Active)
            {
                continue;
            }

            if (!IsMapPositionVisible(opportunity.WorldPosition))
            {
                continue;
            }

            DrawWorldOpportunityMarker(opportunity, queries.GetOpportunity(opportunity.DefinitionId));
        }
    }

    private void DrawWorldOpportunityMarker(WorldOpportunityState opportunity, WorldOpportunityDefinition definition)
    {
        Vector2 position = MapToScreen(opportunity.WorldPosition);
        bool selected = opportunity.OpportunityId == _selectedOpportunityId;
        int remainingTicks = Mathf.Max(0, opportunity.ExpiresTick - State.WorldTick);
        float pulse = 1.0f + Mathf.Clamp(remainingTicks, 0, 5) * 0.03f;
        Color fill = new(0.95f, 0.72f, 0.22f, 0.94f);
        Color border = new(0.14f, 0.08f, 0.02f, 0.98f);
        float radius = OpportunityMarkerRadius * pulse;

        if (selected)
        {
            DrawArc(position, radius + 8.0f, 0, Mathf.Tau, 40, new Color(1.0f, 0.95f, 0.55f, 0.98f), 3.0f, true);
        }

        DrawCircle(position, radius + 4.0f, border);
        DrawCircle(position, radius, fill);
        DrawPolygon(
            new[]
            {
                position + new Vector2(0.0f, -radius - 10.0f),
                position + new Vector2(10.0f, -radius + 4.0f),
                position + new Vector2(-10.0f, -radius + 4.0f)
            },
            new[] { new Color(1.0f, 0.9f, 0.32f, 1.0f) });
        if (definition != null)
        {
            DrawString(ThemeDB.FallbackFont, position + new Vector2(-42.0f, radius + 20.0f), definition.DisplayName, HorizontalAlignment.Center, 84.0f, 13, new Color(1.0f, 0.92f, 0.68f, 0.95f));
        }
    }

    private void DrawWorldArmyMarker(WorldArmyState army)
    {
        Vector2 position = MapToScreen(army.WorldPosition);
        bool playerOwned = army.OwnerFactionId == State.PlayerFactionId;
        Color fill = playerOwned
            ? new Color(0.35f, 0.72f, 0.92f, 1.0f)
            : new Color(0.88f, 0.18f, 0.12f, 1.0f);
        Color border = new(0.04f, 0.045f, 0.04f, 0.98f);
        float radius = Mathf.Clamp(army.Radius, 10.0f, 28.0f);

        if (army.Status == WorldArmyStatus.Attacking)
        {
            DrawArc(position, radius + 11.0f, 0, Mathf.Tau, 40, new Color(1.0f, 0.23f, 0.15f, 0.92f), 4.0f, true);
        }
        else if (army.Status == WorldArmyStatus.NavigationBlocked)
        {
            DrawArc(position, radius + 11.0f, 0, Mathf.Tau, 40, new Color(1.0f, 0.78f, 0.12f, 0.96f), 4.0f, true);
            DrawLine(position + new Vector2(-radius * 0.45f, -radius * 0.45f), position + new Vector2(radius * 0.45f, radius * 0.45f), new Color(0.12f, 0.035f, 0.03f, 1.0f), 3.0f, true);
            DrawLine(position + new Vector2(radius * 0.45f, -radius * 0.45f), position + new Vector2(-radius * 0.45f, radius * 0.45f), new Color(0.12f, 0.035f, 0.03f, 1.0f), 3.0f, true);
        }

        if (_selectedArmyIds.Contains(army.ArmyId))
        {
            DrawArc(position, radius + 8.0f, 0, Mathf.Tau, 40, new Color(1.0f, 0.9f, 0.38f, 0.98f), 3.0f, true);
        }

        DrawCircle(position, radius + 4.0f, border);
        DrawCircle(position, radius, fill);
        DrawLine(position + new Vector2(-3.0f, -radius - 11.0f), position + new Vector2(-3.0f, radius * 0.65f), border, 3.0f, true);
        DrawPolygon(
            new[] { position + new Vector2(0.0f, -radius - 12.0f), position + new Vector2(20.0f, -radius - 6.0f), position + new Vector2(0.0f, -radius) },
            new[] { playerOwned ? new Color(0.62f, 0.9f, 1.0f, 1.0f) : new Color(1.0f, 0.3f, 0.15f, 1.0f) });

        if (army.Status == WorldArmyStatus.Moving)
        {
            DrawCircle(MapToScreen(army.Destination), 5.0f, new Color(fill.R, fill.G, fill.B, 0.75f));
        }
    }

    private void DrawArmySelectionBox()
    {
        if (!_isArmyBoxSelecting)
        {
            return;
        }

        Rect2 rect = BuildScreenRect(_armySelectionStartScreen, _armySelectionCurrentScreen);
        DrawRect(rect, new Color(0.32f, 0.7f, 0.95f, 0.16f), true);
        DrawRect(rect, new Color(0.52f, 0.86f, 1.0f, 0.9f), false, 2.0f);
    }

    private void DrawSiteIcons(StrategicWorldDefinitionQueries queries)
    {
        foreach (WorldSiteDefinition definition in Definition.SiteDefinitions)
        {
            WorldSiteState state = State.SiteStates[definition.Id];
            Vector2 center = GetSiteCenter(definition);
            Color color = GetSiteColor(state);
            bool selected = definition.Id == _selectedSiteId;

            if (TryGetSiteVisualScreenBounds(definition.Id, out Rect2 visualBounds))
            {
                DrawSiteVisualOverlay(state, visualBounds, selected);
                continue;
            }

            DrawCircle(center, SiteIconRadius + (selected ? 10.0f : 6.0f), selected ? new Color(1.0f, 0.86f, 0.32f, 0.95f) : new Color(0.04f, 0.045f, 0.04f, 0.95f));
            DrawCircle(center, SiteIconRadius + 2.0f, new Color(0.08f, 0.08f, 0.075f, 1.0f));
            DrawSiteSymbol(definition.SiteKind, center, color);

            if (state.ControlState == SiteControlState.Damaged)
            {
                DrawLine(center + new Vector2(-18.0f, -20.0f), center + new Vector2(18.0f, 20.0f), new Color(0.95f, 0.72f, 0.2f, 1.0f), 4.0f, true);
            }
        }
    }

    private void DrawSiteVisualOverlay(WorldSiteState state, Rect2 visualBounds, bool selected)
    {
        Color stateColor = GetSiteColor(state);
        Rect2 outline = visualBounds.Grow(selected ? 8.0f : 3.0f);
        if (selected)
        {
            DrawRect(outline, new Color(1.0f, 0.86f, 0.32f, 0.12f), true);
            DrawRect(outline, new Color(1.0f, 0.86f, 0.32f, 0.98f), false, 3.0f);
        }
        else
        {
            DrawRect(outline, new Color(stateColor.R, stateColor.G, stateColor.B, 0.55f), false, 2.0f);
        }

        if (state.ControlState == SiteControlState.Damaged)
        {
            Vector2 start = visualBounds.Position + new Vector2(6.0f, 6.0f);
            Vector2 end = visualBounds.End - new Vector2(6.0f, 6.0f);
            DrawLine(start, end, new Color(0.95f, 0.72f, 0.2f, 1.0f), 4.0f, true);
        }
    }

    private void DrawSiteSymbol(WorldSiteKind kind, Vector2 center, Color color)
    {
        switch (kind)
        {
            case WorldSiteKind.Base:
                DrawRect(new Rect2(center - new Vector2(20.0f, 8.0f), new Vector2(40.0f, 26.0f)), color, true);
                DrawPolygon(
                    new[] { center + new Vector2(-24.0f, -8.0f), center + new Vector2(0.0f, -28.0f), center + new Vector2(24.0f, -8.0f) },
                    new[] { new Color(0.62f, 0.34f, 0.24f, 1.0f) });
                break;
            case WorldSiteKind.ResourceSite:
                DrawPolygon(
                    new[] { center + new Vector2(0.0f, -27.0f), center + new Vector2(25.0f, 0.0f), center + new Vector2(0.0f, 27.0f), center + new Vector2(-25.0f, 0.0f) },
                    new[] { color });
                DrawRect(new Rect2(center - new Vector2(13.0f, 8.0f), new Vector2(26.0f, 16.0f)), new Color(0.13f, 0.12f, 0.1f, 0.92f), true);
                break;
            case WorldSiteKind.EnemySource:
                DrawCircle(center, SiteIconRadius, color);
                DrawRect(new Rect2(center - new Vector2(7.0f, 24.0f), new Vector2(14.0f, 30.0f)), new Color(0.16f, 0.14f, 0.16f, 1.0f), true);
                DrawLine(center + new Vector2(-16.0f, -5.0f), center + new Vector2(16.0f, -5.0f), new Color(0.95f, 0.45f, 0.38f, 1.0f), 3.0f, true);
                break;
            default:
                DrawCircle(center, SiteIconRadius, color);
                break;
        }
    }
}
