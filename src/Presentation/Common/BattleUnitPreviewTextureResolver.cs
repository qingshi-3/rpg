using System;
using System.Collections.Generic;
using Godot;
using Rpg.Application.Config;
using Rpg.Definitions.Battle;
using Rpg.Infrastructure.Logging;

namespace Rpg.Presentation.Common;

public static class BattleUnitPreviewTextureResolver
{
    private const string DefaultIdleAnimation = "idle";

    private static readonly Dictionary<string, Texture2D> TextureCache = new(StringComparer.Ordinal);
    private static readonly HashSet<string> LoggedWarnings = new(StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, string> UnitDefinitionPaths = new Dictionary<string, string>(StringComparer.Ordinal);
    private static bool UnitDefinitionPathsLoaded;

    public static Texture2D ResolvePreviewTexture(string battleUnitId)
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
        if (TextureCache.TryGetValue(cacheKey, out Texture2D cached))
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

        Texture2D texture = spriteFrames.GetFrameTexture(idleAnimation, 0);
        if (texture == null)
        {
            WarnOnce($"missing-idle-frame:{unitId}:{idleName}", $"Battle unit idle frame texture is missing for UI preview id={unitId} animation={idleName}");
            return null;
        }

        TextureCache[cacheKey] = texture;
        return texture;
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

        GameLog.Warn(nameof(BattleUnitPreviewTextureResolver), message);
    }
}
