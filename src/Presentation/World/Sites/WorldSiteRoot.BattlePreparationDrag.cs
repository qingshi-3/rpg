using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Snapshots;
using Rpg.Application.World;
using Rpg.Definitions.Maps;
using Rpg.Definitions.World;
using Rpg.Domain.Battle.Grid;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Battle;
using Rpg.Presentation.Battle.Entities;

namespace Rpg.Presentation.World.Sites;

public partial class WorldSiteRoot
{
    private static void SetDeploymentDragComponent(BattleEntity entity, string placementId, bool dragEnabled)
    {
        if (entity == null)
        {
            return;
        }

        WorldSiteDeploymentDragComponent component =
            entity.GetComponent<WorldSiteDeploymentDragComponent>() ??
            entity.GetNodeOrNull<WorldSiteDeploymentDragComponent>("WorldSiteDeploymentDragComponent");
        if (component == null)
        {
            component = new WorldSiteDeploymentDragComponent
            {
                Name = "WorldSiteDeploymentDragComponent"
            };
            entity.AddChild(component);
        }

        component.Configure(placementId, dragEnabled);
    }

    private static bool IsDeploymentDragEnabled(Node2D entity, string placementId)
    {
        if (entity is not BattleEntity battleEntity)
        {
            return false;
        }

        WorldSiteDeploymentDragComponent component =
            battleEntity.GetComponent<WorldSiteDeploymentDragComponent>() ??
            battleEntity.GetNodeOrNull<WorldSiteDeploymentDragComponent>("WorldSiteDeploymentDragComponent");
        return component?.CanDragPlacement(placementId) == true;
    }

    private void SetAllDeploymentDragEnabled(bool enabled)
    {
        int updated = 0;
        foreach (Node2D entity in _sitePlacementEntities.Values)
        {
            WorldSiteDeploymentDragComponent component = entity is BattleEntity battleEntity
                ? battleEntity.GetComponent<WorldSiteDeploymentDragComponent>() ??
                  battleEntity.GetNodeOrNull<WorldSiteDeploymentDragComponent>("WorldSiteDeploymentDragComponent")
                : null;
            if (component == null)
            {
                continue;
            }

            component.SetDragEnabled(enabled);
            updated++;
        }

        GameLog.Info(nameof(WorldSiteRoot), $"DeploymentDragComponentsToggled enabled={enabled} count={updated}");
    }

    private void BeginBattlePreparationCompanyDrag(string groupKey)
    {
        if (!_isBattlePreparationActive || string.IsNullOrWhiteSpace(groupKey))
        {
            return;
        }
        BattleRuntimeCommandGroupView group = FindBattlePreparationCompany(groupKey);
        if (group == null)
        {
            return;
        }
        _selectedBattlePreparationPlanGroupKey = group.GroupKey;
        _draggedBattlePreparationGroupKey = group.GroupKey;
        CaptureBattlePreparationCompanyPlacements(group);
        RemoveBattlePreparationCompanyPreviewEntities(group);
        ClearBattlePreparationCompanyPlacements(group);
        ClearDraggedBattlePreparationCompanyEntities();
        CreateBattlePreparationCompanyPreviewEntities(group, BattleFaction.Player);
        RaiseBattlePreparationCompanyPreviewEntities(); BattlePreparationCommandSelectionPresenter.Apply(_unitRoot, group, group.GroupKey);
        SetBattlePreparationHudRetreated(true, "company_drag_start");
        UpdateBattlePreparationCompanyDragPreview();
        GameLog.Info(
            nameof(WorldSiteRoot),
            $"BattlePreparationCompanyDragStarted group={group.GroupKey} members={_draggedBattlePreparationCompanyEntities.Count}");
        GetViewport().SetInputAsHandled();
    }

    private void BeginBattlePreparationCompanyPlacementFollow(string groupKey) => BeginBattlePreparationCompanyDrag(groupKey);

    private void HandleBattlePreparationCompanyDragInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion)
        {
            UpdateBattlePreparationCompanyDragPreview();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
        {
            return;
        }
        string groupKey = _draggedBattlePreparationGroupKey;
        bool placed = TryCommitBattlePreparationCompanyPlacement(out string failureReason);
        if (!placed)
        {
            GameLog.Info(
                nameof(WorldSiteRoot),
                $"BattlePreparationCompanyPlacementRejected group={groupKey} reason={failureReason}");
            RefreshBattlePreparationPlanUi(FormatPlacementFailure(failureReason), "battle_preparation_company_placement_rejected");
            GetViewport().SetInputAsHandled();
            return;
        }
        ClearBattlePreparationCompanyDragState();
        SetBattlePreparationTopPrompt("右键选择部队目的地");
        RefreshBattlePreparationAfterCompanyDrag(
            groupKey,
            placed ? "部队阵型已部署。" : FormatPlacementFailure(failureReason));
        GameLog.Info(
            nameof(WorldSiteRoot),
            $"BattlePreparationCompanyDragEnded group={groupKey} placed={placed} reason={failureReason}");
        GetViewport().SetInputAsHandled();
    }

    private void UpdateBattlePreparationCompanyDragPreview()
    {
        if (string.IsNullOrWhiteSpace(_draggedBattlePreparationGroupKey))
        {
            return;
        }
        if (!TryResolveMouseFormationIntentAnchor(out GridPosition formationAnchor))
        {
            _draggedBattlePreparationDraft = BattlePreparationCompanyFormationDraft.Invalid("placement_cell_invalid");
            SetBattlePreparationDragFootprintPreview(System.Array.Empty<GridPosition>(), valid: false);
            SetBattlePreparationCompanyPreviewInvalid(true);
            return;
        }
        BattlePreparationCompanyFormationDraft draft = BuildBattlePreparationCompanyFormationDraft(formationAnchor);
        _draggedBattlePreparationDraft = draft;
        foreach (BattlePreparationCompanyPlacementDraft placement in draft.Placements)
        {
            BattlePreparationCompanyPreviewEntity preview = _draggedBattlePreparationCompanyEntities
                .FirstOrDefault(item => ReferenceEquals(item.Force, placement.Force) && item.ForceIndex == placement.ForceIndex);
            if (preview?.Entity == null)
            {
                continue;
            }
            SyncBattlePreparationPreviewGridOccupant(preview.Entity, placement);
            SetDeploymentDragEntityToFootprintCenter(preview.Entity, placement.Anchor, placement.FootprintSize);
        }

        SetBattlePreparationDragFootprintPreview(draft.CoveredCells, draft.IsValid);
        SetBattlePreparationCompanyPreviewInvalid(!draft.IsValid);
        if (_lastBattlePreparationCompanyDragValid != draft.IsValid ||
            !string.Equals(_lastBattlePreparationCompanyDragReason, draft.FailureReason, System.StringComparison.Ordinal))
        {
            _lastBattlePreparationCompanyDragValid = draft.IsValid;
            _lastBattlePreparationCompanyDragReason = draft.FailureReason ?? "";
            GameLog.Info(
                nameof(WorldSiteRoot),
                $"BattlePreparationCompanyDragPreview group={_draggedBattlePreparationGroupKey} valid={draft.IsValid} reason={draft.FailureReason ?? ""}");
        }
    }

    private bool TryResolveMouseFormationIntentAnchor(out GridPosition formationAnchor)
    {
        bool resolved = TryResolveCellCenterCoordinates(GetWorldViewportMousePosition(), out float centerX, out float centerY);
        formationAnchor = resolved ? BattleFootprintCells.ResolveAnchorFromCenter(centerX, centerY, 1, 1) : default;
        return resolved;
    }

    private BattlePreparationCompanyFormationDraft BuildBattlePreparationCompanyFormationDraft(GridPosition formationAnchor)
    {
        BattleRuntimeCommandGroupView group = FindBattlePreparationCompany(_draggedBattlePreparationGroupKey);
        if (group == null)
        {
            return BattlePreparationCompanyFormationDraft.Invalid("battle_force_missing");
        }

        SemanticDeploymentSide deploymentSide = ResolveBattlePreparationCompanyDeploymentSide(group);
        string factionId = group.Forces.FirstOrDefault(force => !string.IsNullOrWhiteSpace(force?.FactionId))?.FactionId ??
                           ResolveBattlePreparationPlayerDeploymentFactionId();
        BattleGroupPlanSnapshot plan = ResolveBattlePreparationGroupPlan(_battlePreparationRequest, group.GroupKey, create: true);
        return _battlePreparationCompanyFormationPlanner.BuildDraft(
            group.Forces,
            plan?.InitialFormationId ?? "",
            formationAnchor,
            deploymentSide,
            factionId,
            ResolveBattlePreparationDeploymentDirection(deploymentSide, factionId),
            _activeGridMap,
            _deploymentCache,
            (_battlePreparationRequest?.PlayerForces ?? Enumerable.Empty<BattleForceRequest>())
            .Concat(_battlePreparationRequest?.EnemyForces ?? Enumerable.Empty<BattleForceRequest>()),
            ResolveForceCanEnterWater);
    }

    private SemanticDeploymentSide ResolveBattlePreparationCompanyDeploymentSide(BattleRuntimeCommandGroupView group)
    {
        BattleForceRequest force = group?.Forces?.FirstOrDefault(item => item != null);
        return ResolveBattlePreparationDeploymentSide(force?.FactionId, BattleFaction.Player);
    }

    private bool TryCommitBattlePreparationCompanyPlacement(out string failureReason)
    {
        failureReason = "";
        if (_draggedBattlePreparationDraft?.IsValid != true)
        {
            failureReason = _draggedBattlePreparationDraft?.FailureReason ?? "placement_cell_invalid";
            return false;
        }

        _battlePreparationCompanyFormationPlanner.ApplyDraft(_draggedBattlePreparationDraft);
        GameLog.Info(
            nameof(WorldSiteRoot),
            $"BattlePreparationCompanyDragCommitted group={_draggedBattlePreparationGroupKey} placements={_draggedBattlePreparationDraft.Placements.Count}");
        return true;
    }

    private void SetBattlePreparationDragFootprintPreview(IEnumerable<GridPosition> footprintCells)
        => SetBattlePreparationDragFootprintPreview(footprintCells, valid: true);

    private void SetBattlePreparationDragFootprintPreview(IEnumerable<GridPosition> footprintCells, bool valid)
    {
        GridPosition[] cells = footprintCells?.Distinct().ToArray() ?? System.Array.Empty<GridPosition>();
        if (valid)
        {
            _highlightOverlay?.SetCells(BattleGridHighlightKind.Hover, cells);
            _highlightOverlay?.ClearCells(BattleGridHighlightKind.Invalid);
            return;
        }

        _highlightOverlay?.SetCells(BattleGridHighlightKind.Invalid, cells);
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Hover);
    }

    private BattleRuntimeCommandGroupView FindBattlePreparationCompany(string groupKey)
        => BuildBattlePreparationPlayerGroups()
            .FirstOrDefault(group => string.Equals(group.GroupKey, groupKey, System.StringComparison.Ordinal));

    private void CaptureBattlePreparationCompanyPlacements(BattleRuntimeCommandGroupView group)
    {
        _draggedBattlePreparationPreviousPlacements.Clear();
        foreach (BattleForceRequest force in group?.Forces ?? System.Array.Empty<BattleForceRequest>())
        {
            _draggedBattlePreparationPreviousPlacements.Add(new BattlePreparationCompanyPlacementSnapshot
            {
                Force = force,
                Placements = (force.PreferredPlacements ?? new List<BattleForcePlacementRequest>())
                    .Select(CloneBattlePreparationPlacement)
                    .ToList()
            });
        }
    }

    private void RestoreBattlePreparationCompanyPlacements()
    {
        foreach (BattlePreparationCompanyPlacementSnapshot snapshot in _draggedBattlePreparationPreviousPlacements)
        {
            if (snapshot?.Force == null)
            {
                continue;
            }

            snapshot.Force.PreferredPlacements.Clear();
            foreach (BattleForcePlacementRequest placement in snapshot.Placements)
            {
                snapshot.Force.PreferredPlacements.Add(CloneBattlePreparationPlacement(placement));
            }
        }

        GameLog.Info(nameof(WorldSiteRoot), $"BattlePreparationCompanyDragCancelled group={_draggedBattlePreparationGroupKey} reason=restore_previous");
    }

    private static BattleForcePlacementRequest CloneBattlePreparationPlacement(BattleForcePlacementRequest placement)
    {
        return placement == null
            ? null
            : new BattleForcePlacementRequest
            {
                PlacementId = placement.PlacementId ?? "",
                CellX = placement.CellX,
                CellY = placement.CellY,
                CellHeight = placement.CellHeight
            };
    }

    private void ClearBattlePreparationCompanyPlacements(BattleRuntimeCommandGroupView group)
    {
        foreach (BattleForceRequest force in group?.Forces ?? System.Array.Empty<BattleForceRequest>())
        {
            force?.PreferredPlacements?.Clear();
        }
    }

    private void RemoveBattlePreparationCompanyPreviewEntities(BattleRuntimeCommandGroupView group)
    {
        foreach (BattleForceRequest force in group?.Forces ?? System.Array.Empty<BattleForceRequest>())
        {
            foreach (BattleForcePlacementRequest placement in force?.PreferredPlacements ?? new List<BattleForcePlacementRequest>())
            {
                RemoveBattlePreparationPlacementEntity(placement?.PlacementId);
            }
        }
    }

    private void CreateBattlePreparationCompanyPreviewEntities(
        BattleRuntimeCommandGroupView group,
        BattleFaction previewFaction)
    {
        if (_unitRoot == null)
        {
            return;
        }

        foreach (BattleForceRequest force in group?.Forces ?? System.Array.Empty<BattleForceRequest>())
        {
            for (int index = 0; index < force.Count; index++)
            {
                BattleEntity entity = _battleUnitFactory.Create(force, index, previewFaction, new GridPosition(0, 0));
                if (entity == null)
                {
                    continue;
                }

                _unitRoot.AddChild(entity);
                _draggedBattlePreparationCompanyEntities.Add(new BattlePreparationCompanyPreviewEntity
                {
                    Force = force,
                    ForceIndex = index,
                    Entity = entity
                });
            }
        }
    }

    private void RaiseBattlePreparationCompanyPreviewEntities()
    {
        foreach (BattlePreparationCompanyPreviewEntity preview in _draggedBattlePreparationCompanyEntities)
        {
            RaiseDeploymentDragEntity(preview.Entity);
        }
    }

    private void SyncBattlePreparationPreviewGridOccupant(
        BattleEntity entity,
        BattlePreparationCompanyPlacementDraft placement)
    {
        GridOccupantComponent gridOccupant = entity?.GetComponent<GridOccupantComponent>();
        if (gridOccupant == null || placement == null)
        {
            return;
        }

        gridOccupant.GridX = placement.Anchor.X;
        gridOccupant.GridY = placement.Anchor.Y;
        gridOccupant.GridHeight = placement.CellHeight;
        gridOccupant.FootprintWidth = placement.FootprintSize.X;
        gridOccupant.FootprintHeight = placement.FootprintSize.Y;
        gridOccupant.UseExplicitHeight = placement.CellHeight > 0;
        ResolveEntitySurfaceHeight(gridOccupant);
        ApplyEntityRenderSort(entity, gridOccupant.SurfacePosition);
        RaiseDeploymentDragEntity(entity);
    }

    private void SetBattlePreparationCompanyPreviewInvalid(bool invalid)
    {
        Color color = invalid ? new Color(1.0f, 0.18f, 0.14f, 0.78f) : Colors.White;
        foreach (BattlePreparationCompanyPreviewEntity preview in _draggedBattlePreparationCompanyEntities)
        {
            if (preview?.Entity != null)
            {
                preview.Entity.Modulate = color;
            }
        }
    }

    private void ClearBattlePreparationCompanyDragState()
    {
        ClearDraggedBattlePreparationCompanyEntities();
        _draggedBattlePreparationGroupKey = "";
        _draggedBattlePreparationDraft = null;
        _draggedBattlePreparationPreviousPlacements.Clear();
        _lastBattlePreparationCompanyDragValid = false;
        _lastBattlePreparationCompanyDragReason = "";
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Hover);
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Invalid);
        SetBattlePreparationHudRetreated(false, "company_drag_end");
    }

    private void ClearDraggedBattlePreparationCompanyEntities()
    {
        foreach (BattlePreparationCompanyPreviewEntity preview in _draggedBattlePreparationCompanyEntities)
        {
            BattleEntity entity = preview?.Entity;
            if (entity != null && IsLiveNode(entity))
            {
                entity.GetParent()?.RemoveChild(entity);
                entity.QueueFree();
            }
        }

        _draggedBattlePreparationCompanyEntities.Clear();
    }

    private void RemoveBattlePreparationPlacementEntity(string placementId)
    {
        if (string.IsNullOrWhiteSpace(placementId) ||
            !_sitePlacementEntities.Remove(placementId, out Node2D entity) ||
            !IsLiveNode(entity))
        {
            return;
        }

        entity.GetParent()?.RemoveChild(entity);
        entity.QueueFree();
    }
}
