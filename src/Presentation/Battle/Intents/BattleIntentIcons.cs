using System.Collections.Generic;
using Godot;

namespace Rpg.Presentation.Battle.Intents;

public static class BattleIntentIcons
{
    public const string Attack = "attack";
    public const string Advance = "advance";
    public const string Defense = "defense";
    public const string Support = "support";
    public const string Control = "control";
    public const string Mobility = "mobility";
    public const string Summon = "summon";
    public const string Charge = "charge";
    public const string Retreat = "retreat";
    public const string Unknown = "unknown";

    private const string BasePath = "res://assets/textures/ui/intent-icons/";

    private static readonly Dictionary<string, string> Paths = new()
    {
        [Attack] = BasePath + "attack.png",
        [Advance] = BasePath + "advance.png",
        [Defense] = BasePath + "defense.png",
        [Support] = BasePath + "support.png",
        [Control] = BasePath + "control.png",
        [Mobility] = BasePath + "mobility.png",
        [Summon] = BasePath + "summon.png",
        [Charge] = BasePath + "charge.png",
        [Retreat] = BasePath + "retreat.png",
        [Unknown] = BasePath + "unknown.png"
    };

    private static readonly Dictionary<string, Texture2D> TextureCache = new();

    public static string Normalize(string templateId, string iconKey)
    {
        if (IsKnown(iconKey))
        {
            return iconKey;
        }

        return templateId switch
        {
            "melee_pressure" => Advance,
            "direct_strike" => Attack,
            "ranged_pressure" => Attack,
            "hold" => Unknown,
            _ => Unknown
        };
    }

    public static Texture2D LoadTexture(string iconKey)
    {
        string normalizedKey = Normalize("", iconKey);
        if (!Paths.TryGetValue(normalizedKey, out string path))
        {
            return null;
        }

        if (TextureCache.TryGetValue(path, out Texture2D cached) && cached != null)
        {
            return cached;
        }

        Texture2D texture = GD.Load<Texture2D>(path);
        if (texture != null)
        {
            TextureCache[path] = texture;
        }

        return texture;
    }

    public static bool IsKnown(string iconKey)
    {
        return !string.IsNullOrWhiteSpace(iconKey) && Paths.ContainsKey(iconKey);
    }
}
