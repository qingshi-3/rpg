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
        }

        if (TryGetComponent(entity, definition, out GridOccupantComponent gridOccupant))
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
        ApplySpriteLayout(animatedSprite, visual, definition.Id);
        // Faction readability is handled by unit markers; keep the sprite art untinted.
        animatedSprite.Modulate = Colors.White;

        return true;
    }

    private void ApplySpriteLayout(
        AnimatedSprite2D animatedSprite,
        BattleUnitVisualDefinition visual,
        string definitionId)
    {
        if (!visual.AutoLayoutFromSpriteFrames ||
            !TryCalculateSpriteAutoLayout(
                visual.SpriteFrames,
                visual.TargetMaxSpriteSizePixels * BattleUnitVisualScale.Default.SpriteScaleMultiplier,
                visual.GroundAnchorOffsetPixels,
                visual.VisibleAlphaThreshold,
                out Vector2 scale,
                out Vector2 position,
                out Vector2 visibleSize,
                out Vector2 visibleCenterOffset,
                out float scaledHeight))
        {
            animatedSprite.Position = Vector2.Zero;
            animatedSprite.Offset = visual.Offset;
            // Keep authored per-unit proportions but apply the current battle-wide readability multiplier.
            animatedSprite.Scale = visual.Scale * BattleUnitVisualScale.Default.SpriteScaleMultiplier;
            return;
        }

        animatedSprite.Position = position;
        animatedSprite.Offset = Vector2.Zero;
        animatedSprite.Scale = scale;
        GameLog.Info(
            nameof(BattleUnitFactory),
            $"Battle unit visual auto layout id={definitionId} visibleSize={visibleSize} visibleCenterOffset={visibleCenterOffset} targetMax={visual.TargetMaxSpriteSizePixels:0.##} scaleMultiplier={BattleUnitVisualScale.Default.SpriteScaleMultiplier:0.##} scale={scale.X:0.###} scaledHeight={scaledHeight:0.##} position={position}");
    }

    private static bool TryCalculateSpriteAutoLayout(
        SpriteFrames spriteFrames,
        float targetMaxSpriteSizePixels,
        float groundAnchorOffsetPixels,
        float visibleAlphaThreshold,
        out Vector2 scale,
        out Vector2 position,
        out Vector2 visibleSize,
        out Vector2 visibleCenterOffset,
        out float scaledHeight)
    {
        scale = Vector2.One;
        position = Vector2.Zero;
        visibleSize = Vector2.Zero;
        visibleCenterOffset = Vector2.Zero;
        scaledHeight = 0f;
        if (spriteFrames == null || targetMaxSpriteSizePixels <= 0)
        {
            return false;
        }

        bool hasVisibleBounds = false;
        Rect2 visibleBounds = default;
        Vector2 frameSize = Vector2.Zero;
        foreach (StringName animationName in EnumerateLayoutAnimationNames(spriteFrames))
        {
            int frameCount = spriteFrames.GetFrameCount(animationName);
            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                Texture2D texture = spriteFrames.GetFrameTexture(animationName, frameIndex);
                if (texture == null)
                {
                    continue;
                }

                if (!TryGetVisibleTextureBounds(
                        texture,
                        visibleAlphaThreshold,
                        out Rect2 frameVisibleBounds,
                        out Vector2 currentFrameSize))
                {
                    continue;
                }

                frameSize.X = System.Math.Max(frameSize.X, currentFrameSize.X);
                frameSize.Y = System.Math.Max(frameSize.Y, currentFrameSize.Y);
                visibleBounds = hasVisibleBounds
                    ? MergeBounds(visibleBounds, frameVisibleBounds)
                    : frameVisibleBounds;
                hasVisibleBounds = true;
            }
        }

        if (!hasVisibleBounds || frameSize.X <= 0f || frameSize.Y <= 0f)
        {
            return false;
        }

        visibleSize = visibleBounds.Size;
        float sourceMax = System.Math.Max(visibleSize.X, visibleSize.Y);
        if (sourceMax <= 0f || visibleSize.Y <= 0f)
        {
            return false;
        }

        Vector2 visibleCenter = visibleBounds.Position + visibleBounds.Size / 2f;
        Vector2 frameCenter = frameSize / 2f;
        visibleCenterOffset = visibleCenter - frameCenter;
        float uniformScale = targetMaxSpriteSizePixels / sourceMax;
        scaledHeight = visibleSize.Y * uniformScale;
        float upwardOffset = System.Math.Max(0f, scaledHeight / 2f - groundAnchorOffsetPixels);
        scale = new Vector2(uniformScale, uniformScale);
        position = new Vector2(
            -visibleCenterOffset.X * uniformScale,
            -upwardOffset - visibleCenterOffset.Y * uniformScale);
        return true;
    }

    private static IEnumerable<StringName> EnumerateLayoutAnimationNames(SpriteFrames spriteFrames)
    {
        foreach (string preferredName in new[] { "idle", "breathing", "move" })
        {
            var animationName = new StringName(preferredName);
            if (spriteFrames.HasAnimation(animationName))
            {
                yield return animationName;
                yield break;
            }
        }

        foreach (StringName animationName in spriteFrames.GetAnimationNames())
        {
            yield return animationName;
        }
    }

    private static Rect2 MergeBounds(Rect2 a, Rect2 b)
    {
        float left = System.Math.Min(a.Position.X, b.Position.X);
        float top = System.Math.Min(a.Position.Y, b.Position.Y);
        float right = System.Math.Max(a.Position.X + a.Size.X, b.Position.X + b.Size.X);
        float bottom = System.Math.Max(a.Position.Y + a.Size.Y, b.Position.Y + b.Size.Y);
        return new Rect2(left, top, right - left, bottom - top);
    }

    private static bool TryGetVisibleTextureBounds(
        Texture2D texture,
        float alphaThreshold,
        out Rect2 visibleBounds,
        out Vector2 frameSize)
    {
        visibleBounds = default;
        frameSize = Vector2.Zero;
        if (texture == null)
        {
            return false;
        }

        Image image;
        int startX;
        int startY;
        int width;
        int height;
        if (texture is AtlasTexture atlasTexture && atlasTexture.Atlas != null)
        {
            image = atlasTexture.Atlas.GetImage();
            Rect2 region = atlasTexture.Region;
            startX = System.Math.Max(0, (int)System.Math.Floor(region.Position.X));
            startY = System.Math.Max(0, (int)System.Math.Floor(region.Position.Y));
            width = System.Math.Max(0, (int)System.Math.Ceiling(region.Size.X));
            height = System.Math.Max(0, (int)System.Math.Ceiling(region.Size.Y));
        }
        else
        {
            image = texture.GetImage();
            startX = 0;
            startY = 0;
            Vector2 size = texture.GetSize();
            width = System.Math.Max(0, (int)System.Math.Ceiling(size.X));
            height = System.Math.Max(0, (int)System.Math.Ceiling(size.Y));
        }

        if (image == null || width <= 0 || height <= 0)
        {
            return false;
        }

        int imageWidth = image.GetWidth();
        int imageHeight = image.GetHeight();
        int endX = System.Math.Min(imageWidth, startX + width);
        int endY = System.Math.Min(imageHeight, startY + height);
        if (endX <= startX || endY <= startY)
        {
            return false;
        }

        int minX = width;
        int minY = height;
        int maxX = -1;
        int maxY = -1;
        for (int y = startY; y < endY; y++)
        {
            for (int x = startX; x < endX; x++)
            {
                if (image.GetPixel(x, y).A <= alphaThreshold)
                {
                    continue;
                }

                int localX = x - startX;
                int localY = y - startY;
                minX = System.Math.Min(minX, localX);
                minY = System.Math.Min(minY, localY);
                maxX = System.Math.Max(maxX, localX);
                maxY = System.Math.Max(maxY, localY);
            }
        }

        frameSize = new Vector2(width, height);
        if (maxX < minX || maxY < minY)
        {
            return false;
        }

        visibleBounds = new Rect2(minX, minY, maxX - minX + 1, maxY - minY + 1);
        return true;
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
