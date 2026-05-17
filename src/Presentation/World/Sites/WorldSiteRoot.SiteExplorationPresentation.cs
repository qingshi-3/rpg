using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Reports;
using Rpg.Application.World;
using Rpg.Definitions.Battle;
using Rpg.Definitions.World;
using Rpg.Domain.Battle.Grid;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Battle;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Battle.Preview;
using Rpg.Presentation.Battle.Rules;
using Rpg.Presentation.Common;
using Rpg.Presentation.World;

namespace Rpg.Presentation.World.Sites;

public partial class WorldSiteRoot
{
    private void RefreshSiteExplorationPresentation(WorldSiteState site, WorldSiteDefinition definition)
    {
        ClearSiteExplorationMarkers();
        if (!IsSiteExplorationActive(site, definition) || _sitePlacementEntityRoot == null)
        {
            return;
        }

        EnsureSiteExplorationStateReady(site, definition);
        // Exploration entities are presentation/control projections; WorldSiteState.Exploration remains the authoritative tick state.
        _siteExplorationPartyMarker = CreateSiteExplorationPartyEntity(site);
        if (_siteExplorationPartyMarker != null)
        {
            _sitePlacementEntityRoot.AddChild(_siteExplorationPartyMarker);
        }

        WorldSiteExplorationService.ReconcilePatrolStates(site, definition);
        foreach (SiteExplorationPatrolState patrol in site.Exploration.PatrolUnits.Where(item => item is { IsRemoved: false }))
        {
            if (TryBindSiteExplorationPatrolEntity(definition, patrol, out Node2D marker))
            {
                _siteExplorationPatrolMarkers[patrol.PatrolId] = marker;
                continue;
            }

            GameLog.Warn(nameof(WorldSiteRoot), $"Exploration patrol has no configured site unit placement site={site.SiteId} patrol={patrol.PatrolId}");
        }

        SyncSiteExplorationMarkerPositions(site);
        RefreshSiteExplorationAlertRangePresentation(site, definition);
        EnsureSiteExplorationHud(site, definition);
    }

    private void PresentSiteExplorationTickResult(WorldSiteState site, SiteExplorationTickResult result)
    {
        if (site?.Exploration == null || result == null)
        {
            return;
        }

        bool animatedAnyMovement = false;
        if (result.PartyMoved && IsLiveNode(_siteExplorationPartyMarker) && _siteExplorationPartyMarker is BattleEntity partyEntity)
        {
            SyncSiteExplorationPartyPlacement(site);
            bool partyWillContinueMoving = !result.Paused && site.Exploration.PendingPathCellKeys.Count > 0;
            // Exploration advances a long path as discrete realtime steps; keep the battle move loop alive until the path stops.
            _unitRoot?.MoveEntityTo(
                partyEntity,
                result.PartyPathStep,
                restartMoveAnimation: !partyWillContinueMoving,
                returnToIdleOnComplete: !partyWillContinueMoving);
            animatedAnyMovement = true;
        }

        foreach (SiteExplorationPatrolMove patrolMove in result.PatrolMoves)
        {
            if (!_siteExplorationPatrolMarkers.TryGetValue(patrolMove.PatrolId, out Node2D marker) ||
                !IsLiveNode(marker) ||
                marker is not BattleEntity patrolEntity)
            {
                _siteExplorationPatrolMarkers.Remove(patrolMove.PatrolId);
                continue;
            }

            SyncSiteExplorationPatrolPlacement(site, patrolMove.PatrolId, patrolMove.To);
            _unitRoot?.MoveEntityTo(patrolEntity, new[] { patrolMove.From, patrolMove.To });
            animatedAnyMovement = true;
        }

        if (!animatedAnyMovement)
        {
            SyncSiteExplorationMarkerPositions(site);
        }
    }

    private void SyncSiteExplorationPatrolPlacement(WorldSiteState site, string patrolId, GridSurfacePosition surface)
    {
        WorldSiteDefinition definition = ResolveSiteDefinition(site?.SiteId);
        SiteExplorationPatrolDefinition patrolDefinition = definition?.ExplorationPatrols.FirstOrDefault(item => item.Id == patrolId);
        if (site == null || patrolDefinition == null)
        {
            return;
        }

        WorldSiteUnitPlacement placement = null;
        if (!string.IsNullOrWhiteSpace(patrolDefinition.SourcePlacementId))
        {
            placement = site.UnitPlacements.FirstOrDefault(item => item.PlacementId == patrolDefinition.SourcePlacementId);
        }

        if (placement == null && string.IsNullOrWhiteSpace(patrolDefinition.SourcePlacementId))
        {
            placement = site.UnitPlacements.FirstOrDefault(item => item.UnitTypeId == patrolDefinition.UnitTypeId);
        }

        if (placement == null)
        {
            return;
        }

        // Patrol AI owns this configured unit during exploration; keep the original placement state aligned with the patrol state.
        placement.CellX = surface.X;
        placement.CellY = surface.Y;
        placement.CellHeight = surface.Height;
    }

    private void ClearSiteExplorationMarkers()
    {
        if (IsLiveNode(_siteExplorationPartyMarker))
        {
            _siteExplorationPartyMarker.QueueFree();
        }
        _siteExplorationPartyMarker = null;
        // Patrol entries bind to existing site placement entities; never free them from the exploration layer.
        _siteExplorationPatrolMarkers.Clear();
        if (IsLiveNode(_siteExplorationAlertRangeRoot))
        {
            _siteExplorationAlertRangeRoot.QueueFree();
        }
        _siteExplorationAlertRangeRoot = null;
        if (IsLiveNode(_siteExplorationHud))
        {
            _siteExplorationHud.QueueFree();
        }
        _siteExplorationHud = null;
        _siteExplorationWaitButton = null;
        ClearSiteExplorationPathPreview();
    }

    private Node2D CreateSiteExplorationPartyEntity(WorldSiteState site)
    {
        WorldSiteUnitPlacement placement = ResolveSiteExplorationPartyPlacement(site);
        string unitTypeId = ResolveExplorationPartyUnitTypeId(site);
        BattleEntity entity = CreateExplorationEntity(
            "SiteExplorationParty",
            placement?.PlacementId ?? "site_exploration_party",
            unitTypeId,
            BattleFaction.Player,
            new GridSurfacePosition(site.Exploration.CurrentCellX, site.Exploration.CurrentCellY, site.Exploration.CurrentCellHeight));
        entity?.GetComponent<BattleUnitPresentationComponent>()?.SetSelected(true);
        return entity;
    }

    private bool TryBindSiteExplorationPatrolEntity(WorldSiteDefinition definition, SiteExplorationPatrolState patrol, out Node2D marker)
    {
        marker = null;
        SiteExplorationPatrolDefinition patrolDefinition = definition?.ExplorationPatrols.FirstOrDefault(item => item.Id == patrol.PatrolId);
        if (patrolDefinition == null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(patrolDefinition.SourcePlacementId) &&
            _sitePlacementEntities.TryGetValue(patrolDefinition.SourcePlacementId, out marker))
        {
            marker.Name = $"SiteExplorationPatrol_{patrol.PatrolId}";
            return true;
        }

        if (!string.IsNullOrWhiteSpace(patrolDefinition.SourcePlacementId))
        {
            return false;
        }

        KeyValuePair<string, Node2D>? fallback = _sitePlacementEntities
            .Where(item => ResolveSiteState(_siteHudSiteId)?.UnitPlacements.Any(placement =>
                placement.PlacementId == item.Key &&
                placement.UnitTypeId == patrolDefinition.UnitTypeId) == true)
            .Select(item => (KeyValuePair<string, Node2D>?)item)
            .FirstOrDefault();
        if (!fallback.HasValue)
        {
            return false;
        }

        marker = fallback.Value.Value;
        marker.Name = $"SiteExplorationPatrol_{patrol.PatrolId}";
        return true;
    }

    private BattleEntity CreateExplorationEntity(
        string nodeName,
        string forceId,
        string unitTypeId,
        BattleFaction faction,
        GridSurfacePosition surface)
    {
        BattleForceRequest force = new()
        {
            ForceId = forceId,
            UnitDefinitionId = unitTypeId,
            Count = 1,
            FactionId = faction == BattleFaction.Player ? StrategicWorldIds.FactionPlayer : StrategicWorldIds.FactionUndead
        };
        force.PreferredPlacements.Add(new BattleForcePlacementRequest
        {
            PlacementId = forceId,
            CellX = surface.X,
            CellY = surface.Y,
            CellHeight = surface.Height
        });

        BattleEntity entity = _battleUnitFactory.Create(force, 0, faction, new GridPosition(surface.X, surface.Y));
        if (entity == null)
        {
            return null;
        }

        entity.Name = nodeName;
        entity.InputPickable = false;
        entity.GetComponent<UnitAnimationComponent>()?.PlayIdle();
        SyncExplorationEntityGridOccupant(entity, surface);
        if (TryGetCellGlobalPosition(new GridPosition(surface.X, surface.Y), out Vector2 globalPosition))
        {
            entity.GlobalPosition = globalPosition;
        }

        return entity;
    }

    private string ResolveExplorationPartyUnitTypeId(WorldSiteState site)
    {
        return ResolveSiteExplorationPartyPlacement(site)?.UnitTypeId ?? StrategicWorldIds.UnitPlayerKnight;
    }

    private void SyncSiteExplorationMarkerPositions(WorldSiteState site)
    {
        if (site?.Exploration == null)
        {
            return;
        }

        if (_unitRoot?.HasActiveMovementTweens == true)
        {
            return;
        }

        if (_siteExplorationPartyMarker != null && !IsLiveNode(_siteExplorationPartyMarker))
        {
            _siteExplorationPartyMarker = null;
        }

        if (_siteExplorationPartyMarker != null &&
            TryGetCellGlobalPosition(new GridPosition(site.Exploration.CurrentCellX, site.Exploration.CurrentCellY), out Vector2 partyPosition))
        {
            _siteExplorationPartyMarker.GlobalPosition = partyPosition;
            if (_siteExplorationPartyMarker is BattleEntity partyEntity)
            {
                SyncExplorationEntityGridOccupant(
                    partyEntity,
                    new GridSurfacePosition(site.Exploration.CurrentCellX, site.Exploration.CurrentCellY, site.Exploration.CurrentCellHeight));
            }
        }

        foreach (SiteExplorationPatrolState patrol in site.Exploration.PatrolUnits)
        {
            if (patrol == null ||
                patrol.IsRemoved ||
                !_siteExplorationPatrolMarkers.TryGetValue(patrol.PatrolId, out Node2D marker) ||
                !IsLiveNode(marker))
            {
                _siteExplorationPatrolMarkers.Remove(patrol?.PatrolId ?? "");
                continue;
            }

            if (TryGetCellGlobalPosition(new GridPosition(patrol.CellX, patrol.CellY), out Vector2 patrolPosition))
            {
                marker.GlobalPosition = patrolPosition;
                if (marker is BattleEntity patrolEntity)
                {
                    SyncExplorationEntityGridOccupant(patrolEntity, new GridSurfacePosition(patrol.CellX, patrol.CellY, patrol.CellHeight));
                }
            }
        }
    }

    private void SyncExplorationEntityGridOccupant(BattleEntity entity, GridSurfacePosition surface)
    {
        if (!IsLiveNode(entity))
        {
            return;
        }

        GridOccupantComponent gridOccupant = entity?.GetComponent<GridOccupantComponent>();
        if (gridOccupant == null)
        {
            return;
        }

        gridOccupant.GridX = surface.X;
        gridOccupant.GridY = surface.Y;
        gridOccupant.GridHeight = surface.Height;
        gridOccupant.UseExplicitHeight = surface.Height > 0;
        ResolveEntitySurfaceHeight(gridOccupant);
        ApplyEntityRenderSort(entity, gridOccupant.SurfacePosition);
    }

    private static bool IsLiveNode(GodotObject node)
    {
        if (node == null || !GodotObject.IsInstanceValid(node))
        {
            return false;
        }

        return node is not Node queuedNode || !queuedNode.IsQueuedForDeletion();
    }

    private void RemoveDisposedSitePlacementEntityRefs()
    {
        if (_sitePlacementEntities.Count == 0)
        {
            return;
        }

        foreach (string placementId in _sitePlacementEntities
                     .Where(item => !IsLiveNode(item.Value))
                     .Select(item => item.Key)
                     .ToArray())
        {
            _sitePlacementEntities.Remove(placementId);
        }
    }

    private void RefreshSiteExplorationAlertRangePresentation(WorldSiteState site, WorldSiteDefinition definition)
    {
        if (IsLiveNode(_siteExplorationAlertRangeRoot))
        {
            _siteExplorationAlertRangeRoot.QueueFree();
        }
        _siteExplorationAlertRangeRoot = null;
        if (_sitePlacementEntityRoot == null || site?.Exploration == null || definition?.ExplorationPatrols == null)
        {
            return;
        }

        _siteExplorationAlertRangeRoot = new Node2D
        {
            Name = "SiteExplorationAlertRangeRoot",
            ZIndex = 42
        };
        _sitePlacementEntityRoot.AddChild(_siteExplorationAlertRangeRoot);

        foreach (SiteExplorationPatrolState patrol in site.Exploration.PatrolUnits.Where(item => item is { IsRemoved: false }))
        {
            SiteExplorationPatrolDefinition patrolDefinition = definition.ExplorationPatrols.FirstOrDefault(item => item.Id == patrol.PatrolId);
            if (patrolDefinition == null)
            {
                continue;
            }

            foreach (GridPosition cell in EnumerateManhattanCells(patrol.CellX, patrol.CellY, patrolDefinition.AlertRadiusCells))
            {
                if (!_activeGridMap.TryGetCell(cell, out _))
                {
                    continue;
                }

                // Draw only the outer alert ring so risk is readable without filling every internal cell.
                _siteExplorationAlertRangeRoot.AddChild(new Line2D
                {
                    Points = ClosePolygon(BuildCellPolygonGlobal(cell)),
                    Width = 2.0f,
                    DefaultColor = new Color(1.0f, 0.32f, 0.12f, 0.88f),
                    ZIndex = 42
                });
            }
        }
    }

    private static IEnumerable<GridPosition> EnumerateManhattanCells(int centerX, int centerY, int radius)
    {
        int safeRadius = System.Math.Max(0, radius);
        for (int x = centerX - safeRadius; x <= centerX + safeRadius; x++)
        {
            for (int y = centerY - safeRadius; y <= centerY + safeRadius; y++)
            {
                if (System.Math.Abs(x - centerX) + System.Math.Abs(y - centerY) == safeRadius)
                {
                    yield return new GridPosition(x, y);
                }
            }
        }
    }

    private static Vector2[] ClosePolygon(Vector2[] polygon)
    {
        if (polygon == null || polygon.Length == 0)
        {
            return System.Array.Empty<Vector2>();
        }

        Vector2[] closed = new Vector2[polygon.Length + 1];
        polygon.CopyTo(closed, 0);
        closed[^1] = polygon[0];
        return closed;
    }

    private bool TryAppendSiteExplorationPointActions(WorldSiteState site, WorldSiteDefinition definition)
    {
        if (site?.Exploration == null ||
            definition?.ExplorationPoints == null ||
            definition.ExplorationPoints.Count == 0)
        {
            return false;
        }

        WorldSiteIntelViewModel intel = WorldSiteIntelService.BuildCurrentView(
            StrategicWorldRuntime.State,
            StrategicWorldRuntime.Definition,
            site.SiteId,
            WorldIntelVisibility.Visible);
        HashSet<string> knownPointIds = new(intel.KnownExplorationPointIds, System.StringComparer.Ordinal);
        AddKnownSiteExplorationPointIds(knownPointIds, site.Exploration.RevealedPointIds);
        AddKnownSiteExplorationPointIds(knownPointIds, site.Exploration.ResolvedPointIds);
        AddKnownSiteExplorationPointIds(knownPointIds, site.Memory?.RevealedExplorationPointIds);
        AddKnownSiteExplorationPointIds(knownPointIds, site.Memory?.ResolvedPointIds);

        int appendedCount = 0;
        foreach (SiteExplorationPointDefinition point in definition.ExplorationPoints)
        {
            if (point == null ||
                string.IsNullOrWhiteSpace(point.Id) ||
                point.Actions.Count == 0 ||
                IsSiteExplorationPointResolved(site, point.Id) ||
                !knownPointIds.Contains(point.Id) ||
                !IsSiteExplorationPointInRange(site.Exploration, point))
            {
                continue;
            }

            foreach (SiteExplorationActionDefinition action in point.Actions.Where(item => item != null))
            {
                Button button = GameUiSceneFactory.CreateWorldPrimaryActionButton(nameof(WorldSiteRoot));
                if (button == null)
                {
                    continue;
                }

                button.Text = BuildSiteExplorationPointActionButtonText(point, action);
                button.TooltipText = BuildSiteExplorationPointActionTooltip(point, action);
                button.Pressed += () => ExecuteSiteExplorationPointAction(site.SiteId, point.Id, action.Id);

                _siteActionList.AddChild(button);
                appendedCount++;
            }
        }

        return appendedCount > 0;
    }

    private void ExecuteSiteExplorationPointAction(string siteId, string pointId, string actionId)
    {
        WorldSiteState site = ResolveSiteState(siteId);
        WorldSiteDefinition definition = ResolveSiteDefinition(siteId);
        SiteExplorationPointDefinition point = definition?.ExplorationPoints.FirstOrDefault(item => item.Id == pointId);
        SiteExplorationActionDefinition action = point?.Actions.FirstOrDefault(item => item.Id == actionId);
        if (!IsSiteExplorationActive(site, definition) ||
            point == null ||
            action == null ||
            IsSiteExplorationPointResolved(site, point.Id) ||
            !IsSiteExplorationPointInRange(site.Exploration, point))
        {
            StrategicWorldRuntime.LastNotice = "探索行动已失效。";
            RefreshSiteManagementUi(StrategicWorldRuntime.LastNotice);
            GameLog.Warn(nameof(WorldSiteRoot), $"Site exploration point action ignored site={siteId} point={pointId} action={actionId}");
            return;
        }

        if (action.StartsBattle)
        {
            RequestSiteExplorationPointBattle(site, definition, point, action);
            return;
        }

        WorldActionResult applyResult = WorldSiteExplorationService.ApplyActionResult(
            StrategicWorldRuntime.State,
            StrategicWorldRuntime.Definition,
            site,
            point.Id,
            action);
        StrategicWorldRuntime.LastNotice = applyResult.Message;
        GameLog.Info(
            nameof(WorldSiteRoot),
            $"Site exploration point action executed site={site.SiteId} point={point.Id} action={action.Id} success={applyResult.Success} alert={site.Exploration.AlertLevel}");
        RefreshSiteManagementUi(StrategicWorldRuntime.LastNotice);
    }

    private static void AddKnownSiteExplorationPointIds(HashSet<string> knownPointIds, IEnumerable<string> pointIds)
    {
        if (knownPointIds == null || pointIds == null)
        {
            return;
        }

        foreach (string id in pointIds)
        {
            if (!string.IsNullOrWhiteSpace(id))
            {
                knownPointIds.Add(id);
            }
        }
    }

    private static bool IsSiteExplorationPointResolved(WorldSiteState site, string pointId)
    {
        return site?.Exploration?.ResolvedPointIds.Contains(pointId) == true ||
               site?.Memory?.ResolvedPointIds.Contains(pointId) == true;
    }

    private static bool IsSiteExplorationPointInRange(
        WorldSiteExplorationState exploration,
        SiteExplorationPointDefinition point)
    {
        if (exploration == null || point == null || exploration.CurrentCellHeight != point.CellHeight)
        {
            return false;
        }

        int distance = System.Math.Abs(exploration.CurrentCellX - point.CellX) +
                       System.Math.Abs(exploration.CurrentCellY - point.CellY);
        return distance <= System.Math.Max(0, point.InteractionRange);
    }

    private static string BuildSiteExplorationPointActionButtonText(
        SiteExplorationPointDefinition point,
        SiteExplorationActionDefinition action)
    {
        string actionName = ResolveSiteExplorationActionDisplayName(point, action);
        string pointName = ResolveSiteExplorationPointDisplayName(point);
        return $"{actionName}\n{pointName}";
    }

    private static string BuildSiteExplorationPointActionTooltip(
        SiteExplorationPointDefinition point,
        SiteExplorationActionDefinition action)
    {
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(point?.Description))
        {
            lines.Add(point.Description);
        }
        if (!string.IsNullOrWhiteSpace(action?.Description))
        {
            lines.Add(action.Description);
        }
        if (action?.StartsBattle == true)
        {
            lines.Add("将进入场域遭遇战。");
        }

        return string.Join("\n", lines);
    }

    private static string ResolveSiteExplorationActionDisplayName(
        SiteExplorationPointDefinition point,
        SiteExplorationActionDefinition action)
    {
        if (!string.IsNullOrWhiteSpace(action?.DisplayName))
        {
            return action.DisplayName;
        }

        return string.IsNullOrWhiteSpace(action?.Id)
            ? ResolveSiteExplorationPointDisplayName(point)
            : action.Id;
    }

    private static string ResolveSiteExplorationPointDisplayName(SiteExplorationPointDefinition point)
    {
        return string.IsNullOrWhiteSpace(point?.DisplayName)
            ? point?.Id ?? ""
            : point.DisplayName;
    }

    private bool TryAppendSiteExplorationAlertChoices(WorldSiteState site)
    {
        if (site?.Exploration == null ||
            site.Exploration.PauseReason != SiteExplorationPauseAlertRadius ||
            string.IsNullOrWhiteSpace(site.Exploration.ActiveAlertPatrolId))
        {
            return false;
        }

        AddSiteExplorationActionButton("进入遭遇战", () => RequestSiteExplorationBattle(site));
        AddSiteExplorationActionButton("撤退并保持警戒", () => RetreatFromSiteExplorationAlert(site));
        return true;
    }

    private void AddSiteExplorationActionButton(string text, System.Action pressed)
    {
        Button button = GameUiSceneFactory.CreateWorldPrimaryActionButton(nameof(WorldSiteRoot));
        if (button == null)
        {
            return;
        }

        button.Text = text;
        button.Pressed += pressed;
        _siteActionList.AddChild(button);
    }

    private void EnsureSiteExplorationHud(WorldSiteState site, WorldSiteDefinition definition)
    {
        if (!IsSiteExplorationActive(site, definition))
        {
            if (IsLiveNode(_siteExplorationHud))
            {
                _siteExplorationHud.QueueFree();
            }
            _siteExplorationHud = null;
            _siteExplorationHudPanel = null;
            _siteExplorationAlertActions = null;
            _siteExplorationAlertLabel = null;
            _siteExplorationWaitButton = null;
            _siteExplorationEngageButton = null;
            _siteExplorationRetreatButton = null;
            return;
        }

        if (_siteExplorationHud != null)
        {
            RefreshSiteExplorationHud(site, definition);
            return;
        }

        PackedScene scene = GD.Load<PackedScene>(SiteExplorationHudScenePath);
        _siteExplorationHud = scene?.Instantiate<Control>();
        if (_siteExplorationHud == null)
        {
            GameLog.Warn(nameof(WorldSiteRoot), $"Site exploration HUD missing path={SiteExplorationHudScenePath}");
            return;
        }

        // Exploration HUD is screen-space UI. Attach it to the CanvasLayer via the full-screen site HUD root;
        // it must never participate in map/unit Node2D sorting.
        (_siteHudRoot ?? GetNodeOrNull<Node>("CanvasLayer") ?? (Node)this).AddChild(_siteExplorationHud);
        _siteExplorationHud.ZIndex = 650;
        _siteExplorationHudPanel = _siteExplorationHud.GetNodeOrNull<Control>("Panel");
        _siteExplorationAlertActions = _siteExplorationHud.GetNodeOrNull<Control>("Panel/Margin/Stack/AlertActions");
        _siteExplorationAlertLabel = _siteExplorationHud.GetNodeOrNull<Label>("Panel/Margin/Stack/AlertLabel");
        _siteExplorationWaitButton = _siteExplorationHud.GetNodeOrNull<Button>("Panel/Margin/Stack/WaitButton");
        _siteExplorationEngageButton = _siteExplorationHud.GetNodeOrNull<Button>("Panel/Margin/Stack/AlertActions/EngageButton");
        _siteExplorationRetreatButton = _siteExplorationHud.GetNodeOrNull<Button>("Panel/Margin/Stack/AlertActions/RetreatButton");
        ConfigureSiteExplorationButton(_siteExplorationWaitButton, OnSiteExplorationWaitPressed, "wait");
        ConfigureSiteExplorationButton(_siteExplorationEngageButton, OnSiteExplorationEngagePressed, "engage");
        ConfigureSiteExplorationButton(_siteExplorationRetreatButton, OnSiteExplorationRetreatPressed, "retreat");
        GameLog.Info(
            nameof(WorldSiteRoot),
            $"Site exploration HUD bound site={site.SiteId} alertLabel={_siteExplorationAlertLabel != null} wait={_siteExplorationWaitButton != null} engage={_siteExplorationEngageButton != null} retreat={_siteExplorationRetreatButton != null}");

        RefreshSiteExplorationHud(site, definition);
    }

    private void ConfigureSiteExplorationButton(Button button, System.Action pressed, string role)
    {
        if (button == null || pressed == null)
        {
            GameLog.Warn(nameof(WorldSiteRoot), $"Site exploration HUD button missing role={role}");
            return;
        }

        button.MouseFilter = Control.MouseFilterEnum.Stop;
        button.Pressed += () => DispatchSiteExplorationButton(role, pressed, "pressed");
        // Some exploration HUD containers are manually excluded from map input. This GUI fallback gives us
        // deterministic diagnostics if a scene-level mouse filter prevents BaseButton.Pressed from firing.
        button.GuiInput += input =>
        {
            if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false })
            {
                DispatchSiteExplorationButton(role, pressed, "gui_release");
            }
        };
    }

    private void DispatchSiteExplorationButton(string role, System.Action pressed, string source)
    {
        ulong now = Time.GetTicksMsec();
        if (_lastSiteExplorationButtonRole == role &&
            now - _lastSiteExplorationButtonMsec < 120UL)
        {
            return;
        }

        _lastSiteExplorationButtonRole = role ?? "";
        _lastSiteExplorationButtonMsec = now;
        GameLog.Info(nameof(WorldSiteRoot), $"Site exploration HUD button dispatched role={role} source={source}");
        pressed();
    }

    private void RefreshSiteExplorationHud(WorldSiteState site, WorldSiteDefinition definition)
    {
        if (_siteExplorationHud == null)
        {
            return;
        }

        bool active = IsSiteExplorationActive(site, definition);
        bool alertPaused = IsSiteExplorationAlertPaused(site);
        _siteExplorationHud.Visible = active;
        if (_siteExplorationAlertActions != null)
        {
            _siteExplorationAlertActions.Visible = active && alertPaused;
        }
        if (_siteExplorationAlertLabel != null)
        {
            _siteExplorationAlertLabel.Visible = active && alertPaused;
            _siteExplorationAlertLabel.Text = alertPaused
                ? $"已进入警惕范围：{ResolveExplorationPatrolName(definition, site.Exploration.ActiveAlertPatrolId)}"
                : "";
        }
        if (_siteExplorationWaitButton != null)
        {
            _siteExplorationWaitButton.Visible = active && !alertPaused;
            _siteExplorationWaitButton.Disabled =
                !active ||
                _unitRoot?.HasActiveMovementTweens == true ||
                alertPaused;
        }
        if (_siteExplorationEngageButton != null)
        {
            _siteExplorationEngageButton.Visible = active && alertPaused;
            _siteExplorationEngageButton.Disabled = !active || !alertPaused;
        }
        if (_siteExplorationRetreatButton != null)
        {
            _siteExplorationRetreatButton.Visible = active && alertPaused;
            _siteExplorationRetreatButton.Disabled = !active || !alertPaused;
        }
    }
}
