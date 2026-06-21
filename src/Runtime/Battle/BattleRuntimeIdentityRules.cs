namespace Rpg.Runtime.Battle;

internal static class BattleRuntimeIdentityRules
{
    internal const string CommandAssault = "Assault";
    internal const string CommandFocusFire = "FocusFire";
    internal const string CommandHoldLine = "HoldLine";
    private const string PlayerFactionId = "player";

    // Runtime identity normalization is shared so command, targeting, tactical
    // facts, and session bootstrap do not grow their own incompatible defaults.
    internal static string NormalizeCorpsCommandId(string commandId)
    {
        string value = commandId?.Trim() ?? "";
        if (string.Equals(value, CommandFocusFire, System.StringComparison.OrdinalIgnoreCase))
        {
            return CommandFocusFire;
        }

        if (string.Equals(value, CommandHoldLine, System.StringComparison.OrdinalIgnoreCase))
        {
            return CommandHoldLine;
        }

        return CommandAssault;
    }

    internal static bool IsFocusFireCommand(string commandId)
    {
        return string.Equals(NormalizeCorpsCommandId(commandId), CommandFocusFire, System.StringComparison.Ordinal);
    }

    internal static bool IsHoldLineCommand(string commandId)
    {
        return string.Equals(NormalizeCorpsCommandId(commandId), CommandHoldLine, System.StringComparison.Ordinal);
    }

    internal static bool SameFaction(BattleRuntimeActor first, BattleRuntimeActor second)
    {
        return SameFaction(first?.FactionId, second?.FactionId);
    }

    internal static bool SameFaction(string first, string second)
    {
        return string.Equals(
            NormalizeFaction(first),
            NormalizeFaction(second),
            System.StringComparison.Ordinal);
    }

    internal static bool IsPlayerFaction(string factionId)
    {
        return string.Equals(NormalizeFaction(factionId), PlayerFactionId, System.StringComparison.Ordinal);
    }

    internal static string NormalizeFaction(string factionId)
    {
        return string.IsNullOrWhiteSpace(factionId) ? PlayerFactionId : factionId.Trim();
    }
}
