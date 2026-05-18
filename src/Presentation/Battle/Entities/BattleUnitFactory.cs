using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle;
using Rpg.Definitions.Battle.Abilities;
using Rpg.Definitions.Battle;
using Rpg.Domain.Battle.Grid;
using Rpg.Infrastructure.Logging;

namespace Rpg.Presentation.Battle.Entities;

public sealed class BattleUnitFactory
{
    private const string DefaultUnitEntityScenePath = "res://scenes/battle/entities/units/BattleUnitBase.tscn";
    private const string UnitDefinitionRootPath = "res://assets/battle/units";
    private static readonly Dictionary<string, BattleUnitDefinition> SharedDefinitions = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, string> SharedDefinitionPathIndex = new(StringComparer.Ordinal);
    private static readonly HashSet<string> SharedLoggedWarningKeys = new(StringComparer.Ordinal);
    private static bool SharedDefinitionPathIndexBuilt;

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

        return CreateFromDefinition(force, forceIndex, fallbackFaction, fallbackPosition, definition);
    }

    public BattleEntity CreatePreview(BattleUnitDefinition definition, BattleFaction faction = BattleFaction.Player)
    {
        if (definition == null)
        {
            GameLog.Warn(nameof(BattleUnitFactory), "Cannot create preview unit because unit definition is missing.");
            return null;
        }

        string definitionId = string.IsNullOrWhiteSpace(definition.Id)
            ? "preview_unit"
            : definition.Id.Trim();
        var force = new BattleForceRequest
        {
            ForceId = $"preview:{definitionId}",
            SourceKind = "EditorPreview",
            SourceId = definition.ResourcePath ?? "",
            UnitDefinitionId = definitionId,
            Count = 1,
            FactionId = faction.ToString()
        };
        force.PreferredPlacements.Add(new BattleForcePlacementRequest
        {
            PlacementId = "preview",
            CellX = 0,
            CellY = 0,
            CellHeight = 0
        });

        return CreateFromDefinition(force, 0, faction, new GridPosition(0, 0), definition);
    }

    private BattleEntity CreateFromDefinition(
        BattleForceRequest force,
        int forceIndex,
        BattleFaction fallbackFaction,
        GridPosition fallbackPosition,
        BattleUnitDefinition definition)
    {
        PackedScene entityScene = ResolveEntityScene(definition);
        if (entityScene == null)
        {
            return null;
        }

        BattleEntity entity = entityScene.Instantiate() as BattleEntity;
        if (entity == null)
        {
            GameLog.Warn(nameof(BattleUnitFactory), $"Battle unit scene root is not BattleEntity id={definition.Id} scene={entityScene.ResourcePath}");
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
        entity.DisplayName = BattleUnitDisplayNameFormatter.FormatInstanceName(definition.DisplayName, forceIndex);
        entity.DebugMarkerColor = definition.DebugMarkerColor;
        if (!ApplyVisualDefinition(entity, definition))
        {
            return null;
        }

        ApplyComponents(entity, definition, faction, gridPosition, placement);
        entity.QueueRedraw();
        return entity;
    }

    public bool TryGetUnitDefinition(string unitDefinitionId, out BattleUnitDefinition definition)
    {
        return TryGetDefinition(unitDefinitionId, out definition);
    }

    public string ResolveUnitDisplayName(string unitDefinitionId)
    {
        if (TryGetDefinition(unitDefinitionId, out BattleUnitDefinition definition))
        {
            return string.IsNullOrWhiteSpace(definition.DisplayName)
                ? unitDefinitionId
                : definition.DisplayName.Trim();
        }

        return string.IsNullOrWhiteSpace(unitDefinitionId) ? "战斗单位" : unitDefinitionId;
    }

    public string ResolveUnitInstanceDisplayName(string unitDefinitionId, int zeroBasedIndex)
    {
        return BattleUnitDisplayNameFormatter.FormatInstanceName(
            ResolveUnitDisplayName(unitDefinitionId),
            zeroBasedIndex);
    }

    private PackedScene ResolveEntityScene(BattleUnitDefinition definition)
    {
        PackedScene defaultScene = GD.Load<PackedScene>(DefaultUnitEntityScenePath);
        if (defaultScene == null)
        {
            WarnOnce(
                $"missing-entity-scene:{definition.Id}",
                nameof(BattleUnitFactory),
                $"Battle unit definition has no scene and default unit scene is missing id={definition.Id} path={DefaultUnitEntityScenePath}");
        }

        return defaultScene;
    }

    private bool TryGetDefinition(string unitDefinitionId, out BattleUnitDefinition definition)
    {
        definition = null;
        if (string.IsNullOrWhiteSpace(unitDefinitionId))
        {
            return false;
        }

        if (SharedDefinitions.TryGetValue(unitDefinitionId, out definition))
        {
            return definition != null;
        }

        string legacyPath = $"{UnitDefinitionRootPath}/{unitDefinitionId}.tres";
        if (TryLoadDefinitionAtPath(unitDefinitionId, legacyPath, out definition))
        {
            return true;
        }

        EnsureDefinitionPathIndex();
        if (SharedDefinitionPathIndex.TryGetValue(unitDefinitionId, out string indexedPath) &&
            TryLoadDefinitionAtPath(unitDefinitionId, indexedPath, out definition))
        {
            return true;
        }

        GameLog.Warn(
            nameof(BattleUnitFactory),
            $"Missing battle unit definition id={unitDefinitionId} legacyPath={legacyPath} indexed={SharedDefinitionPathIndex.Count}");
        return false;
    }

    private bool TryLoadDefinitionAtPath(
        string requestedUnitDefinitionId,
        string path,
        out BattleUnitDefinition definition)
    {
        definition = null;
        if (string.IsNullOrWhiteSpace(path) || !ResourceLoader.Exists(path))
        {
            return false;
        }

        definition = GD.Load<BattleUnitDefinition>(path);
        if (definition == null)
        {
            WarnOnce(
                $"invalid-definition:{path}",
                nameof(BattleUnitFactory),
                $"Battle unit definition resource is invalid id={requestedUnitDefinitionId} path={path}");
            return false;
        }

        if (!string.Equals(definition.Id, requestedUnitDefinitionId, StringComparison.Ordinal))
        {
            WarnOnce(
                $"definition-id-mismatch:{requestedUnitDefinitionId}:{path}",
                nameof(BattleUnitFactory),
                $"Battle unit definition id mismatch requested={requestedUnitDefinitionId} actual={definition.Id} path={path}");
        }

        SharedDefinitions[requestedUnitDefinitionId] = definition;
        return true;
    }

    private void EnsureDefinitionPathIndex()
    {
        if (SharedDefinitionPathIndexBuilt)
        {
            return;
        }

        SharedDefinitionPathIndexBuilt = true;
        IndexUnitDefinitionDirectory(UnitDefinitionRootPath);
        GameLog.Info(
            nameof(BattleUnitFactory),
            $"Indexed nested battle unit definitions root={UnitDefinitionRootPath} count={SharedDefinitionPathIndex.Count}");
    }

    private void IndexUnitDefinitionDirectory(string directoryPath)
    {
        foreach (string fileName in DirAccess.GetFilesAt(directoryPath))
        {
            if (string.Equals(fileName, "unit.tres", StringComparison.Ordinal))
            {
                IndexUnitDefinitionPath($"{directoryPath}/{fileName}");
            }
        }

        foreach (string directoryName in DirAccess.GetDirectoriesAt(directoryPath))
        {
            IndexUnitDefinitionDirectory($"{directoryPath}/{directoryName}");
        }
    }

    private void IndexUnitDefinitionPath(string path)
    {
        if (!TryReadDefinitionId(path, out string definitionId))
        {
            WarnOnce(
                $"invalid-indexed-definition:{path}",
                nameof(BattleUnitFactory),
                $"Nested battle unit definition has no readable id path={path}");
            return;
        }

        if (SharedDefinitionPathIndex.ContainsKey(definitionId))
        {
            WarnOnce(
                $"duplicate-indexed-definition:{definitionId}",
                nameof(BattleUnitFactory),
                $"Duplicate nested battle unit definition id={definitionId} ignoredPath={path} keptPath={SharedDefinitionPathIndex[definitionId]}");
            return;
        }

        SharedDefinitionPathIndex[definitionId] = path;
    }

    private static bool TryReadDefinitionId(string path, out string definitionId)
    {
        definitionId = "";
        using Godot.FileAccess file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
        if (file == null)
        {
            return false;
        }

        string text = file.GetAsText();
        const string marker = "\nId = \"";
        int start = text.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
        {
            return false;
        }

        start += marker.Length;
        int end = text.IndexOf('"', start);
        if (end <= start)
        {
            return false;
        }

        definitionId = text[start..end];
        return !string.IsNullOrWhiteSpace(definitionId);
    }

    private void ApplyComponents(
        BattleEntity entity,
        BattleUnitDefinition definition,
        BattleFaction faction,
        GridPosition gridPosition,
        BattleForcePlacementRequest placement)
    {
        if (TryGetComponent(entity, definition, out FactionComponent factionComponent))
        {
            factionComponent.Faction = faction;
        }

        if (TryGetComponent(entity, definition, out BattleUnitPresentationComponent presentation))
        {
            presentation.SetFaction(faction);
        }

        if (TryGetComponent(entity, definition, out HealthComponent health))
        {
            health.MaxHp = definition.MaxHp;
            health.Hp = definition.MaxHp;
        }

        if (TryGetComponent(entity, definition, out MovementComponent movement))
        {
            movement.MoveRange = definition.MoveRange;
            movement.CanEnterWater = definition.CanEnterWater;
        }

        if (TryGetComponent(entity, definition, out AttackComponent attack))
        {
            attack.Damage = definition.AttackDamage;
            attack.Range = definition.AttackRange;
            attack.AttackSpeed = definition.AttackSpeed;
        }

        if (TryGetComponent(entity, definition, out GridOccupantComponent gridOccupant))
        {
            gridOccupant.GridX = gridPosition.X;
            gridOccupant.GridY = gridPosition.Y;
            gridOccupant.FootprintWidth = definition.FootprintWidth;
            gridOccupant.FootprintHeight = definition.FootprintHeight;
            if (placement?.CellHeight > 0)
            {
                gridOccupant.GridHeight = placement.CellHeight;
                gridOccupant.UseExplicitHeight = true;
            }

            gridOccupant.BlocksMovement = definition.BlocksMovement;
            gridOccupant.BlocksLineOfSight = definition.BlocksLineOfSight;
        }

        if (TryGetComponent(entity, definition, out SelectableComponent selectable))
        {
            selectable.IsSelectable = IsSelectableByControlMode(definition.ControlMode, faction);
        }

        if (TryGetComponent(entity, definition, out TargetableComponent targetable))
        {
            targetable.IsTargetable = definition.IsTargetable;
            targetable.Tags = BattleTargetTags.Unit |
                              (faction == BattleFaction.Player ? BattleTargetTags.Ally : BattleTargetTags.Enemy);
        }

        if (TryGetComponent(entity, definition, out AbilityComponent abilityComponent))
        {
            abilityComponent.Abilities = CopyConfiguredAbilities(definition);
            if (abilityComponent.Abilities.Count == 0)
            {
                WarnOnce(
                    $"legacy-attack-fallback:{definition.Id}",
                    nameof(BattleUnitFactory),
                    $"Battle unit has no configured abilities and will use AttackComponent fallback id={definition.Id}");
            }
        }

        if (TryGetComponent(entity, definition, out UnitAnimationComponent animationComponent))
        {
            animationComponent.AnimationSet = definition.Visual?.AnimationSet;
            animationComponent.AttackSpeed = definition.AttackSpeed;
        }

        if (TryGetComponent(entity, definition, out BattleUnitAudioComponent audioComponent))
        {
            audioComponent.Audio = definition.Audio;
        }
    }

    private bool TryGetComponent<T>(
        BattleEntity entity,
        BattleUnitDefinition definition,
        out T component) where T : BattleEntityComponent
    {
        string componentName = typeof(T).Name;
        component = entity.GetNodeOrNull<T>(componentName) ??
                    entity.GetChildren().OfType<T>().FirstOrDefault();
        if (component != null)
        {
            return true;
        }

        WarnOnce(
            $"missing-component:{definition.Id}:{componentName}",
            nameof(BattleUnitFactory),
            $"Battle unit missing component id={definition.Id} entity={entity.Name} component={componentName}");
        return false;
    }

    private static Godot.Collections.Array<AbilityDefinition> CopyConfiguredAbilities(BattleUnitDefinition definition)
    {
        var abilities = new Godot.Collections.Array<AbilityDefinition>();
        if (definition.Abilities == null)
        {
            return abilities;
        }

        foreach (AbilityDefinition ability in definition.Abilities)
        {
            if (ability != null)
            {
                abilities.Add(ability);
            }
        }

        return abilities;
    }

    private static bool IsSelectableByControlMode(BattleUnitControlMode controlMode, BattleFaction faction)
    {
        return controlMode switch
        {
            BattleUnitControlMode.Player => true,
            BattleUnitControlMode.Ai or BattleUnitControlMode.Passive => false,
            _ => faction == BattleFaction.Player
        };
    }

    private bool ApplyVisualDefinition(BattleEntity entity, BattleUnitDefinition definition)
    {
        BattleUnitVisualDefinition visual = definition.Visual;
        if (visual == null)
        {
            WarnOnce(
                $"missing-visual:{definition.Id}",
                nameof(BattleUnitFactory),
                $"Battle unit definition has no visual resource id={definition.Id}");
            return false;
        }

        Node2D visualRoot = entity.GetNodeOrNull<Node2D>("VisualRoot");
        if (visualRoot == null)
        {
            WarnOnce(
                $"missing-visual-root:{definition.Id}",
                nameof(BattleUnitFactory),
                $"Battle unit missing VisualRoot id={definition.Id} entity={entity.Name}");
            return false;
        }

        AnimatedSprite2D animatedSprite = visualRoot.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        if (animatedSprite == null)
        {
            WarnOnce(
                $"missing-animated-sprite:{definition.Id}",
                nameof(BattleUnitFactory),
                $"Battle unit base scene missing AnimatedSprite2D id={definition.Id} entity={entity.Name}");
            return false;
        }

        if (visual.SpriteFrames == null)
        {
            WarnOnce(
                $"missing-sprite-frames:{definition.Id}",
                nameof(BattleUnitFactory),
                $"Battle unit visual resource has no SpriteFrames id={definition.Id}");
            return false;
        }

        animatedSprite.SpriteFrames = visual.SpriteFrames;
        animatedSprite.Centered = true;
        ApplySpriteLayout(animatedSprite, visual, definition.Id, ResolveFootprintVisualScale(definition));
        // Faction readability is handled by unit markers; keep the sprite art untinted.
        animatedSprite.Modulate = Colors.White;

        return true;
    }

    private void ApplySpriteLayout(
        AnimatedSprite2D animatedSprite,
        BattleUnitVisualDefinition visual,
        string definitionId,
        Vector2 footprintScale)
    {
        if (!visual.AutoLayoutFromSpriteFrames ||
            !BattleUnitVisualLayoutCalculator.TryCalculateAutoLayout(
                visual.SpriteFrames,
                visual.TargetMaxSpriteSizePixels * BattleUnitVisualScale.Default.SpriteScaleMultiplier,
                visual.GroundAnchorOffsetPixels,
                visual.VisibleAlphaThreshold,
                out BattleUnitVisualLayout layout))
        {
            animatedSprite.Position = Vector2.Zero;
            animatedSprite.Offset = new Vector2(0f, visual.Offset.Y);
            // Runtime footprint owns occupied cells; presentation grows high-resolution art uniformly to avoid stretched or oversized sprites.
            animatedSprite.Scale = visual.Scale * BattleUnitVisualScale.Default.SpriteScaleMultiplier * footprintScale;
            return;
        }

        animatedSprite.Position = layout.Position;
        animatedSprite.Offset = Vector2.Zero;
        animatedSprite.Scale = layout.Scale * footprintScale;
        GameLog.Info(
            nameof(BattleUnitFactory),
            $"Battle unit visual auto layout id={definitionId} visibleSize={layout.VisibleSize} visibleCenterOffset={layout.VisibleCenterOffset} targetMax={visual.TargetMaxSpriteSizePixels:0.##} scaleMultiplier={BattleUnitVisualScale.Default.SpriteScaleMultiplier:0.##} footprintScale={footprintScale} scale={animatedSprite.Scale} scaledHeight={layout.ScaledHeight:0.##} position={layout.Position}");
    }

    private static Vector2 ResolveFootprintVisualScale(BattleUnitDefinition definition)
    {
        int width = System.Math.Clamp(definition?.FootprintWidth ?? 1, 1, 3);
        int height = System.Math.Clamp(definition?.FootprintHeight ?? 1, 1, 3);
        int footprintSize = System.Math.Max(width, height);
        float uniformScale = 1f + ((footprintSize - 1) * BattleUnitVisualScale.Default.FootprintScaleStepMultiplier);
        return new Vector2(uniformScale, uniformScale);
    }

    private void WarnOnce(string key, string owner, string message)
    {
        if (SharedLoggedWarningKeys.Add(key))
        {
            GameLog.Warn(owner, message);
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
