using System.Collections.Generic;
using Godot;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Battle.Feedback;
using Rpg.Presentation.Battle.Intents;
using Rpg.Presentation.World;
using Rpg.Presentation.World.Sites;

namespace Rpg.Presentation.Common;

public static class GameUiSceneFactory
{
    public const string StrategicWorldHudScenePath = "res://scenes/world/ui/StrategicWorldHud.tscn";
    public const string WorldSitePeacetimeHudScenePath = "res://scenes/world/ui/WorldSitePeacetimeHud.tscn";
    public const string BattleObjectiveMapDialogScenePath = "res://scenes/world/ui/BattleObjectiveMapDialog.tscn";
    public const string BattlePreparationRosterRowScenePath = "res://scenes/world/ui/BattlePreparationRosterRow.tscn";
    public const string BattlePreparationObjectiveThumbnailScenePath = "res://scenes/world/ui/BattlePreparationObjectiveThumbnail.tscn";
    public const string BattleRuntimeHeroSwitchButtonScenePath = "res://scenes/world/ui/BattleRuntimeHeroSwitchButton.tscn";
    public const string BattleRuntimeSkillSlotScenePath = "res://scenes/world/ui/BattleRuntimeSkillSlot.tscn";
    public const string BattleRuntimeHeroTroopSummaryRowScenePath = "res://scenes/world/ui/BattleRuntimeHeroTroopSummaryRow.tscn";
    public const string PreBattleDialogScenePath = "res://scenes/world/ui/PreBattleDialog.tscn";
    public const string PostBattleSettlementDialogScenePath = "res://scenes/world/ui/PostBattleSettlementDialog.tscn";
    public const string WorldSiteHitButtonScenePath = "res://scenes/world/ui/WorldSiteHitButton.tscn";
    public const string WorldSiteNameBadgeScenePath = "res://scenes/world/ui/WorldSiteNameBadge.tscn";
    public const string WorldSiteHoverSummaryPanelScenePath = "res://scenes/world/ui/WorldSiteHoverSummaryPanel.tscn";
    public const string WorldMutedLineScenePath = "res://scenes/world/ui/WorldMutedLine.tscn";
    public const string WorldPrimaryActionButtonScenePath = "res://scenes/world/ui/WorldPrimaryActionButton.tscn";
    public const string WorldSecondaryActionButtonScenePath = "res://scenes/world/ui/WorldSecondaryActionButton.tscn";
    public const string WorldCompactMarkerButtonScenePath = "res://scenes/world/ui/WorldCompactMarkerButton.tscn";
    public const string WorldBuildingOptionCardScenePath = "res://scenes/world/ui/WorldBuildingOptionCard.tscn";
    public const string WorldMusterOptionCardScenePath = "res://scenes/world/ui/WorldMusterOptionCard.tscn";
    public const string WorldMilitaryHeroCardScenePath = "res://scenes/world/ui/WorldMilitaryHeroCard.tscn";
    public const string WorldExpeditionCountRowScenePath = "res://scenes/world/ui/WorldExpeditionCountRow.tscn";
    public const string WorldResourceFloatTextScenePath = "res://scenes/world/ui/WorldResourceFloatText.tscn";
    public const string WorldOpportunityDetailPanelScenePath = "res://scenes/world/ui/WorldOpportunityDetailPanel.tscn";
    public const string BattleIntentMarkerScenePath = "res://scenes/battle/intents/BattleIntentMarker.tscn";
    public const string BattleDamageNumberScenePath = "res://scenes/battle/feedback/BattleDamageNumber.tscn";
    public const string BattleCellInfoDebugPanelScenePath = "res://scenes/battle/debug/BattleCellInfoDebugPanel.tscn";

    private static readonly Dictionary<string, PackedScene> SceneCache = new();

    public static T Instantiate<T>(string scenePath, string ownerName) where T : Node
    {
        PackedScene scene = LoadScene(scenePath, ownerName);
        if (scene == null)
        {
            return null;
        }

        T node = scene.Instantiate<T>();
        if (node == null)
        {
            GameLog.Warn(nameof(GameUiSceneFactory), $"UI scene type mismatch owner={ownerName} path={scenePath} expected={typeof(T).Name}");
        }

        return node;
    }

    public static void Preload(params string[] scenePaths)
    {
        foreach (string scenePath in scenePaths ?? System.Array.Empty<string>())
        {
            LoadScene(scenePath, nameof(GameUiSceneFactory));
        }
    }

    public static Button CreateWorldSiteHitButton(string ownerName)
    {
        return Instantiate<Button>(WorldSiteHitButtonScenePath, ownerName);
    }

    public static WorldSiteNameBadge CreateWorldSiteNameBadge(string ownerName)
    {
        return Instantiate<WorldSiteNameBadge>(WorldSiteNameBadgeScenePath, ownerName);
    }

    public static WorldSiteHoverSummaryPanel CreateWorldSiteHoverSummaryPanel(string ownerName)
    {
        return Instantiate<WorldSiteHoverSummaryPanel>(WorldSiteHoverSummaryPanelScenePath, ownerName);
    }

    public static BattleObjectiveMapDialog CreateBattleObjectiveMapDialog(string ownerName)
    {
        return Instantiate<BattleObjectiveMapDialog>(BattleObjectiveMapDialogScenePath, ownerName);
    }

    public static BattlePreparationRosterRow CreateBattlePreparationRosterRow(string ownerName)
    {
        return Instantiate<BattlePreparationRosterRow>(BattlePreparationRosterRowScenePath, ownerName);
    }

    public static BattlePreparationObjectiveThumbnail CreateBattlePreparationObjectiveThumbnail(string ownerName)
    {
        return Instantiate<BattlePreparationObjectiveThumbnail>(BattlePreparationObjectiveThumbnailScenePath, ownerName);
    }

    public static BattleRuntimeSkillSlot CreateBattleRuntimeSkillSlot(string ownerName)
    {
        return Instantiate<BattleRuntimeSkillSlot>(BattleRuntimeSkillSlotScenePath, ownerName);
    }

    public static BattleRuntimeHeroSwitchButton CreateBattleRuntimeHeroSwitchButton(string ownerName)
    {
        return Instantiate<BattleRuntimeHeroSwitchButton>(BattleRuntimeHeroSwitchButtonScenePath, ownerName);
    }

    public static BattleRuntimeHeroTroopSummaryRow CreateBattleRuntimeHeroTroopSummaryRow(string ownerName)
    {
        return Instantiate<BattleRuntimeHeroTroopSummaryRow>(BattleRuntimeHeroTroopSummaryRowScenePath, ownerName);
    }

    public static StrategicBattleGateDialog CreateStrategicBattleGateDialog(string ownerName)
    {
        return Instantiate<StrategicBattleGateDialog>(PreBattleDialogScenePath, ownerName);
    }

    public static PostBattleSettlementDialog CreatePostBattleSettlementDialog(string ownerName)
    {
        return Instantiate<PostBattleSettlementDialog>(PostBattleSettlementDialogScenePath, ownerName);
    }

    public static Label CreateWorldMutedLine(string ownerName)
    {
        return Instantiate<Label>(WorldMutedLineScenePath, ownerName);
    }

    public static Button CreateWorldPrimaryActionButton(string ownerName)
    {
        return Instantiate<Button>(WorldPrimaryActionButtonScenePath, ownerName);
    }

    public static Button CreateWorldSecondaryActionButton(string ownerName)
    {
        return Instantiate<Button>(WorldSecondaryActionButtonScenePath, ownerName);
    }

    public static Button CreateWorldCompactMarkerButton(string ownerName)
    {
        return Instantiate<Button>(WorldCompactMarkerButtonScenePath, ownerName);
    }

    public static WorldBuildingOptionCard CreateWorldBuildingOptionCard(string ownerName)
    {
        return Instantiate<WorldBuildingOptionCard>(WorldBuildingOptionCardScenePath, ownerName);
    }

    public static WorldMusterOptionCard CreateWorldMusterOptionCard(string ownerName)
    {
        return Instantiate<WorldMusterOptionCard>(WorldMusterOptionCardScenePath, ownerName);
    }

    public static WorldMilitaryHeroCard CreateWorldMilitaryHeroCard(string ownerName)
    {
        return Instantiate<WorldMilitaryHeroCard>(WorldMilitaryHeroCardScenePath, ownerName);
    }

    public static HBoxContainer CreateWorldExpeditionCountRow(string ownerName)
    {
        return Instantiate<HBoxContainer>(WorldExpeditionCountRowScenePath, ownerName);
    }

    public static WorldResourceFloatText CreateWorldResourceFloatText(string ownerName)
    {
        return Instantiate<WorldResourceFloatText>(WorldResourceFloatTextScenePath, ownerName);
    }

    public static BattleIntentMarker CreateBattleIntentMarker(string ownerName)
    {
        return Instantiate<BattleIntentMarker>(BattleIntentMarkerScenePath, ownerName);
    }

    public static BattleDamageNumber CreateBattleDamageNumber(string ownerName)
    {
        return Instantiate<BattleDamageNumber>(BattleDamageNumberScenePath, ownerName);
    }

    public static T GetRequiredNode<T>(Node root, NodePath path, string ownerName) where T : Node
    {
        if (root == null)
        {
            GameLog.Warn(nameof(GameUiSceneFactory), $"Missing UI root owner={ownerName} path={path}");
            return null;
        }

        T node = root.GetNodeOrNull<T>(path);
        if (node == null)
        {
            GameLog.Warn(nameof(GameUiSceneFactory), $"Missing UI node owner={ownerName} root={root.GetPath()} path={path} expected={typeof(T).Name}");
        }

        return node;
    }

    private static PackedScene LoadScene(string scenePath, string ownerName)
    {
        if (string.IsNullOrWhiteSpace(scenePath))
        {
            GameLog.Warn(nameof(GameUiSceneFactory), $"Missing UI scene path owner={ownerName}");
            return null;
        }

        if (SceneCache.TryGetValue(scenePath, out PackedScene cached))
        {
            return cached;
        }

        PackedScene scene = GD.Load<PackedScene>(scenePath);
        if (scene == null)
        {
            GameLog.Warn(nameof(GameUiSceneFactory), $"Cannot load UI scene owner={ownerName} path={scenePath}");
        }

        SceneCache[scenePath] = scene;
        return scene;
    }
}
