using System.Collections.Generic;
using Godot;
using Rpg.Application.Battle;
using Rpg.Definitions.Battle;
using Rpg.Domain.Battle.Grid;
using Rpg.Infrastructure.Logging;

namespace Rpg.Presentation.Battle.Entities;

public sealed class BattleUnitFactory
{
    private const string UnitDefinitionRootPath = "res://assets/battle/units";
    private readonly Dictionary<string, BattleUnitDefinition> _definitions = new();

    public BattleEntity Create(
        BattleForceRequest force,
        int forceIndex,
        BattleFaction fallbackFaction,
        GridPosition fallbackPosition)
    {
        if (force == null || string.IsNullOrWhiteSpace(force.UnitDefinitionId))
        {
            GameLog.Warn(nameof(BattleUnitFactory), "Cannot create battle unit because force or unit definition id is missing.");
            return null;
        }

        if (!TryGetDefinition(force.UnitDefinitionId, out BattleUnitDefinition definition))
        {
            return null;
        }

        if (definition.EntityScene == null)
        {
            GameLog.Warn(nameof(BattleUnitFactory), $"Battle unit definition has no scene id={definition.Id}");
            return null;
        }

        BattleEntity entity = definition.EntityScene.Instantiate<BattleEntity>();
        if (entity == null)
        {
            GameLog.Warn(nameof(BattleUnitFactory), $"Battle unit scene root is not BattleEntity id={definition.Id} scene={definition.EntityScene.ResourcePath}");
            return null;
        }

        BattleFaction faction = fallbackFaction;
        BattleForcePlacementRequest placement = forceIndex < force.PreferredPlacements.Count
            ? force.PreferredPlacements[forceIndex]
            : null;
        GridPosition gridPosition = placement == null
            ? fallbackPosition
            : new GridPosition(placement.CellX, placement.CellY);

        entity.Name = BuildNodeName(force, forceIndex);
        entity.EntityId = BuildEntityId(force, forceIndex);
        entity.DisplayName = definition.DisplayName;
        entity.DebugMarkerColor = definition.DebugMarkerColor;
        ApplyComponents(entity, definition, faction, gridPosition, placement);
        entity.QueueRedraw();
        return entity;
    }

    private bool TryGetDefinition(string unitDefinitionId, out BattleUnitDefinition definition)
    {
        definition = null;
        if (string.IsNullOrWhiteSpace(unitDefinitionId))
        {
            return false;
        }

        if (_definitions.TryGetValue(unitDefinitionId, out definition))
        {
            return definition != null;
        }

        string path = $"{UnitDefinitionRootPath}/{unitDefinitionId}.tres";
        definition = GD.Load<BattleUnitDefinition>(path);
        if (definition == null)
        {
            GameLog.Warn(nameof(BattleUnitFactory), $"Missing battle unit definition id={unitDefinitionId} path={path}");
            return false;
        }

        _definitions[unitDefinitionId] = definition;
        return true;
    }

    private static void ApplyComponents(
        BattleEntity entity,
        BattleUnitDefinition definition,
        BattleFaction faction,
        GridPosition gridPosition,
        BattleForcePlacementRequest placement)
    {
        if (entity.GetNodeOrNull<FactionComponent>("FactionComponent") is { } factionComponent)
        {
            factionComponent.Faction = faction;
        }

        if (entity.GetNodeOrNull<HealthComponent>("HealthComponent") is { } health)
        {
            health.MaxHp = definition.MaxHp;
            health.Hp = definition.MaxHp;
        }

        if (entity.GetNodeOrNull<ActionPointComponent>("ActionPointComponent") is { } actionPoints)
        {
            actionPoints.MaxAp = definition.MaxActionPoints;
            actionPoints.Ap = definition.MaxActionPoints;
        }

        if (entity.GetNodeOrNull<MovementComponent>("MovementComponent") is { } movement)
        {
            movement.MoveRange = definition.MoveRange;
            movement.ApCost = definition.MoveActionPointCost;
            movement.MaxMoveUsesPerTurn = definition.MaxMoveUsesPerTurn;
            movement.MoveUsesRemaining = definition.MaxMoveUsesPerTurn;
            movement.CanEnterWater = definition.CanEnterWater;
        }

        if (entity.GetNodeOrNull<AttackComponent>("AttackComponent") is { } attack)
        {
            attack.Damage = definition.AttackDamage;
            attack.Range = definition.AttackRange;
            attack.ApCost = definition.AttackActionPointCost;
        }

        if (entity.GetNodeOrNull<GridOccupantComponent>("GridOccupantComponent") is { } gridOccupant)
        {
            gridOccupant.GridX = gridPosition.X;
            gridOccupant.GridY = gridPosition.Y;
            if (placement?.CellHeight > 0)
            {
                gridOccupant.GridHeight = placement.CellHeight;
                gridOccupant.UseExplicitHeight = true;
            }

            gridOccupant.BlocksMovement = definition.BlocksMovement;
            gridOccupant.BlocksLineOfSight = definition.BlocksLineOfSight;
        }

        if (entity.GetNodeOrNull<SelectableComponent>("SelectableComponent") is { } selectable)
        {
            selectable.IsSelectable = faction == BattleFaction.Player;
        }

        if (entity.GetNodeOrNull<TargetableComponent>("TargetableComponent") is { } targetable)
        {
            targetable.IsTargetable = definition.IsTargetable;
            targetable.Tags = BattleTargetTags.Unit |
                              (faction == BattleFaction.Player ? BattleTargetTags.Ally : BattleTargetTags.Enemy);
        }
    }

    private static string BuildEntityId(BattleForceRequest force, int forceIndex)
    {
        string source = string.IsNullOrWhiteSpace(force.ForceId)
            ? force.UnitDefinitionId
            : force.ForceId;
        return $"{source}:{forceIndex + 1}";
    }

    private static string BuildNodeName(BattleForceRequest force, int forceIndex)
    {
        string nameSource = string.IsNullOrWhiteSpace(force.ForceId)
            ? force.UnitDefinitionId
            : force.ForceId;
        string safeId = string.IsNullOrWhiteSpace(nameSource)
            ? "BattleUnit"
            : nameSource.Replace(':', '_').Replace('-', '_');
        return $"{safeId}_{forceIndex + 1}";
    }
}
