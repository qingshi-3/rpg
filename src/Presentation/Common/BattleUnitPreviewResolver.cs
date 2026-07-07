using System;
using System.Collections.Generic;
using Godot;
using Rpg.Application.Config;
using Rpg.Definitions.Battle;
using Rpg.Infrastructure.Logging;

namespace Rpg.Presentation.Common;

public static class BattleUnitPreviewResolver
{
    private const string DefaultIdleAnimation = "idle";

    private static readonly Dictionary<string, Texture2D> TextureCache = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, BattleUnitAnimatedPreviewModel> AnimatedPreviewCache = new(StringComparer.Ordinal);
    private static readonly HashSet<string> LoggedWarnings = new(StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, string> UnitDefinitionPaths = new Dictionary<string, string>(StringComparer.Ordinal);
    private static bool UnitDefinitionPathsLoaded;

    public static Texture2D ResolvePreviewTexture(string battleUnitId)
    {
        BattleUnitAnimatedPreviewModel preview = ResolveAnimatedPreview(battleUnitId);
        return ResolvePreviewTexture(preview);
    }

    public static Texture2D ResolvePreviewTexture(BattleUnitAnimatedPreviewModel preview)
    {
        if (preview == null)
        {
            return null;
        }

        string cacheKey = $"{preview.UnitId}:{preview.AnimationName}";
        if (TextureCache.TryGetValue(cacheKey, out Texture2D cached))
        {
            return cached;
        }

        StringName animationName = preview.AnimationName;
        Texture2D texture = preview.SpriteFrames.GetFrameTexture(animationName, 0);
        if (texture == null)
        {
            WarnOnce(
                $"missing-preview-frame:{preview.UnitId}:{preview.AnimationName}",
                $"Battle unit preview frame texture is missing for UI preview id={preview.UnitId} animation={preview.AnimationName}");
            return null;
        }

        TextureCache[cacheKey] = texture;
        return texture;
    }

    public static BattleUnitAnimatedPreviewModel ResolveAnimatedPreview(string battleUnitId)
    {
        string unitId = battleUnitId?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(unitId))
        {
            WarnOnce("missing-unit-id", "Missing battle unit id for UI preview");
            return null;
        }

        if (!TryResolveUnitDefinitionPath(unitId, out string unitDefinitionPath))
        {
            WarnOnce($"missing-unit-definition:{unitId}", $"Missing battle unit definition index entry for UI preview id={unitId}");
            return null;
        }

        BattleUnitDefinition definition = GD.Load<BattleUnitDefinition>(unitDefinitionPath);
        if (definition == null)
        {
            WarnOnce($"load-unit-definition:{unitId}", $"Cannot load battle unit definition for UI preview id={unitId} path={unitDefinitionPath}");
            return null;
        }

        string idleName = string.IsNullOrWhiteSpace(definition.Visual?.AnimationSet?.IdleAnimation)
            ? DefaultIdleAnimation
            : definition.Visual.AnimationSet.IdleAnimation.Trim();
        string cacheKey = $"{unitId}:{idleName}";
        if (AnimatedPreviewCache.TryGetValue(cacheKey, out BattleUnitAnimatedPreviewModel cached))
        {
            return cached;
        }

        SpriteFrames spriteFrames = definition.Visual?.SpriteFrames;
        if (spriteFrames == null)
        {
            WarnOnce($"missing-sprite-frames:{unitId}", $"Battle unit visual has no SpriteFrames for UI preview id={unitId}");
            return null;
        }

        StringName idleAnimation = idleName;
        if (!spriteFrames.HasAnimation(idleAnimation))
        {
            WarnOnce($"missing-idle-animation:{unitId}:{idleName}", $"Battle unit visual has no idle animation for UI preview id={unitId} animation={idleName}");
            return null;
        }

        if (spriteFrames.GetFrameCount(idleAnimation) <= 0)
        {
            WarnOnce($"empty-idle-animation:{unitId}:{idleName}", $"Battle unit idle animation has no frames for UI preview id={unitId} animation={idleName}");
            return null;
        }

        BattleUnitAnimatedPreviewModel preview = new(unitId, spriteFrames, idleName, definition.Visual);
        AnimatedPreviewCache[cacheKey] = preview;
        return preview;
    }

    private static bool TryResolveUnitDefinitionPath(string unitId, out string path)
    {
        EnsureUnitDefinitionPathsLoaded();
        return UnitDefinitionPaths.TryGetValue(unitId ?? "", out path);
    }

    private static void EnsureUnitDefinitionPathsLoaded()
    {
        if (UnitDefinitionPathsLoaded)
        {
            return;
        }

        UnitDefinitionPathsLoaded = true;
        try
        {
            UnitDefinitionPaths = BattleUnitDefinitionIndexLoader.LoadDefaultPathIndex();
        }
        catch (Exception exception)
        {
            UnitDefinitionPaths = new Dictionary<string, string>(StringComparer.Ordinal);
            WarnOnce(
                "load-unit-definition-index",
                $"Cannot load battle unit definition index for UI previews path={BattleUnitDefinitionIndexLoader.DefaultConfigPath} reason={exception.Message}");
        }
    }

    private static void WarnOnce(string key, string message)
    {
        if (!LoggedWarnings.Add(key ?? ""))
        {
            return;
        }

        GameLog.Warn(nameof(BattleUnitPreviewResolver), message);
    }
}

public sealed class BattleUnitAnimatedPreviewModel
{
    public BattleUnitAnimatedPreviewModel(
        string unitId,
        SpriteFrames spriteFrames,
        string animationName,
        BattleUnitVisualDefinition visual)
    {
        UnitId = unitId?.Trim() ?? "";
        SpriteFrames = spriteFrames;
        AnimationName = string.IsNullOrWhiteSpace(animationName) ? "idle" : animationName.Trim();
        Visual = visual;
    }

    public string UnitId { get; }

    public SpriteFrames SpriteFrames { get; }

    public string AnimationName { get; }

    public BattleUnitVisualDefinition Visual { get; }
}
