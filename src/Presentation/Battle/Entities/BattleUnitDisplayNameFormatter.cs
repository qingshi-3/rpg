namespace Rpg.Presentation.Battle.Entities;

public static class BattleUnitDisplayNameFormatter
{
    private const string FallbackDisplayName = "战斗单位";

    public static string FormatInstanceName(string displayName, int zeroBasedIndex)
    {
        string baseName = string.IsNullOrWhiteSpace(displayName)
            ? FallbackDisplayName
            : displayName.Trim();
        int visibleIndex = System.Math.Max(0, zeroBasedIndex) + 1;
        return $"{baseName}{visibleIndex:00}";
    }
}
