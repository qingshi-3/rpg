using Godot;
using Rpg.Application.Battle;
using Rpg.Application.World;
using Rpg.Domain.Battle.Grid;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Battle;
using Rpg.Presentation.Battle.Entities;

namespace Rpg.Presentation.World.Sites;

public partial class WorldSiteRoot
{
    private void RefreshBattlePreparationPlanUi(
        string notice = "",
        string layoutReason = "battle_preparation_plan_refresh")
    {
        if (!_isBattlePreparationActive)
        {
            RefreshSiteManagementUi(notice);
            return;
        }

        StrategicWorldRuntime.EnsureInitialized();
        EnsureBattlePreparationPlanDefaults(_battlePreparationRequest);
        SetBattlePreparationNoticeText(notice);
        // Plan-only interactions must not rebuild map entities; doing so restarts
        // every unit's idle animation and creates a visible input hitch.
        BindBattlePreparationCompanyRoster();
        BindBattlePreparationCompactPlanControls();
        BindBattlePreparationObjectiveThumbnail();
        UpdateSitePeacetimePanelVisibility(layoutReason);
        UpdateMainWorldViewportLayout(layoutReason);
    }

    private void SetBattlePreparationNoticeText(string notice)
    {
        if (_siteResourceLabel != null && !string.IsNullOrWhiteSpace(notice))
        {
            _siteResourceLabel.Text = notice.Trim();
        }
    }

    private void RefreshBattlePreparationAfterCompanyDrag(string groupKey, string notice)
    {
        RebuildBattlePreparationCompanyMapEntities(groupKey);
        // Dropping a company replaces the selected preview nodes with formal map
        // entities, so the current command selection must be replayed onto them.
        BattlePreparationCommandSelectionPresenter.Apply(
            _unitRoot,
            ResolveSelectedBattlePreparationGroup(),
            _selectedBattlePreparationPlanGroupKey);
        RefreshBattlePreparationPlanUi(notice, "battle_preparation_company_drag");
    }

    private void RebuildBattlePreparationCompanyMapEntities(string groupKey)
    {
        BattleRuntimeCommandGroupView group = FindBattlePreparationCompany(groupKey);
        if (group == null)
        {
            return;
        }

        // Company drag replaces only the selected company's temporary previews.
        // Other deployed entities keep their node identity and current animation phase.
        RemoveBattlePreparationCompanyPreviewEntities(group);
        CreateBattlePreparationCompanyMapEntities(group);
    }

    private void CreateBattlePreparationCompanyMapEntities(BattleRuntimeCommandGroupView group)
    {
        if (_unitRoot == null)
        {
            return;
        }

        foreach (BattleForceRequest force in group?.Forces ?? System.Array.Empty<BattleForceRequest>())
        {
            for (int index = 0; index < force.Count; index++)
            {
                BattleForcePlacementRequest placement = index < (force.PreferredPlacements?.Count ?? 0)
                    ? force.PreferredPlacements[index]
                    : null;
                if (placement == null)
                {
                    continue;
                }

                var fallbackPosition = new GridPosition(placement.CellX, placement.CellY);
                BattleEntity entity = _battleUnitFactory.Create(force, index, BattleFaction.Player, fallbackPosition);
                if (entity == null)
                {
                    continue;
                }

                ApplyBattleRequestDeployment(entity, force, index, BattleFaction.Player, _battlePreparationRequest, null);
                RegisterBattlePreparationPlacement(entity, force, index, BattleFaction.Player);
                _unitRoot.AddChild(entity);
                PlaceBattleEntityOnGrid(entity);
            }
        }
    }

    private bool PlaceBattleEntityOnGrid(BattleEntity entity)
    {
        if (_activeSiteMap is not BattleMapView || _unitRoot == null)
        {
            GameLog.Warn(nameof(WorldSiteRoot), $"Skip entity placement activeSiteMapIsBattleMap={_activeSiteMap is BattleMapView} unitRoot={_unitRoot != null}");
            return false;
        }

        if (_coordinateLayer == null)
        {
            GameLog.Warn(nameof(WorldSiteRoot), "Skip entity placement because coordinate layer is missing.");
            return false;
        }

        return PlaceBattleEntityOnGridResolved(entity);
    }

    private bool PlaceBattleEntityOnGridResolved(BattleEntity entity)
    {
        if (entity == null)
        {
            return false;
        }

        GridOccupantComponent gridOccupant = entity.GetComponent<GridOccupantComponent>();
        if (gridOccupant == null)
        {
            GameLog.Info(nameof(WorldSiteRoot), $"Entity has no grid occupant entity={entity.EntityId} name={entity.DisplayName}");
            return false;
        }

        ResolveEntitySurfaceHeight(gridOccupant);
        var cell = new Vector2I(gridOccupant.GridX, gridOccupant.GridY);
        if (TryGetFootprintCenterGlobalPosition(
                gridOccupant.Position,
                new Vector2I(gridOccupant.FootprintWidth, gridOccupant.FootprintHeight),
                out Vector2 globalPosition))
        {
            entity.GlobalPosition = globalPosition;
        }
        else
        {
            entity.GlobalPosition = _coordinateLayer.ToGlobal(_coordinateLayer.MapToLocal(cell));
        }

        ApplyEntityRenderSort(entity, gridOccupant.SurfacePosition);
        GameLog.Info(
            nameof(WorldSiteRoot),
            $"Placed entity id={entity.EntityId} name={entity.DisplayName} surface={gridOccupant.SurfacePosition} global={entity.GlobalPosition} {DescribeGridCell(gridOccupant.Position)} {DescribeGridSurface(gridOccupant.SurfacePosition)}");

        WarnIfEntityStartsOnInvalidSurface(entity, gridOccupant.SurfacePosition);
        return true;
    }
}
