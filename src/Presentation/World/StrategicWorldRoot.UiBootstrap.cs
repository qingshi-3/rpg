using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle;
using Rpg.Application.World;
using Rpg.Definitions.World;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.World;

public partial class StrategicWorldRoot
{
    private void BuildUi()
    {
        Control hud = GameUiSceneFactory.Instantiate<Control>(
            GameUiSceneFactory.StrategicWorldHudScenePath,
            nameof(StrategicWorldRoot));
        if (hud == null)
        {
            return;
        }

        AddChild(hud);
        BindStrategicHud(hud);
        UpdateMainWorldViewportLayout(GetMapBounds());
        WarmupStrategicSelectionUiScenes();
        BuildMapArea();
        BuildSiteHoverSummaryPanel();
    }

    private void WarmupStrategicSelectionUiScenes()
    {
        GameUiSceneFactory.Preload(
            GameUiSceneFactory.WorldMutedLineScenePath,
            GameUiSceneFactory.WorldPrimaryActionButtonScenePath,
            GameUiSceneFactory.WorldSecondaryActionButtonScenePath,
            GameUiSceneFactory.WorldCompactMarkerButtonScenePath,
            GameUiSceneFactory.WorldExpeditionCountRowScenePath,
            GameUiSceneFactory.PreBattleDialogScenePath);

        Node warmupRoot = new() { Name = "StrategicUiWarmup" };
        AddChild(warmupRoot);
        // Selection rebuilds detail/action rows on demand. Instantiate once during
        // scene setup so the first site click does not pay the scene construction cost.
        AddWarmupNode(warmupRoot, GameUiSceneFactory.CreateWorldMutedLine(nameof(StrategicWorldRoot)));
        AddWarmupNode(warmupRoot, GameUiSceneFactory.CreateWorldPrimaryActionButton(nameof(StrategicWorldRoot)));
        AddWarmupNode(warmupRoot, GameUiSceneFactory.CreateWorldSecondaryActionButton(nameof(StrategicWorldRoot)));
        AddWarmupNode(warmupRoot, GameUiSceneFactory.CreateWorldCompactMarkerButton(nameof(StrategicWorldRoot)));
        AddWarmupNode(warmupRoot, GameUiSceneFactory.CreateWorldExpeditionCountRow(nameof(StrategicWorldRoot)));
        warmupRoot.QueueFree();
    }

    private static void AddWarmupNode(Node parent, Node child)
    {
        if (parent == null || child == null)
        {
            return;
        }

        parent.AddChild(child);
    }

    private void BuildStrategicFogOverlay()
    {
        _fogOverlay = new StrategicWorldFogOverlay
        {
            Name = "StrategicWorldFogOverlay",
            ZIndex = 60
        };
        SetFullRect(_fogOverlay);
        // Fog is world presentation, so it lives in the viewport overlay instead of the root UI canvas.
        if (_worldMapOverlay == null)
        {
            GameLog.Error(nameof(StrategicWorldRoot), "WorldMapOverlayMissingForFog");
            return;
        }

        _worldMapOverlay.AddChild(_fogOverlay);
    }

    private void BindStrategicHud(Control hud)
    {
        // Layout hosts are Presentation-only containers. They organize panels without
        // owning strategic state, site state, or action authority.
        _topBarHost = GameUiSceneFactory.GetRequiredNode<Control>(
            hud,
            "TopBarHost",
            nameof(StrategicWorldRoot));
        _leftPrimaryPanelHost = GameUiSceneFactory.GetRequiredNode<Control>(
            hud,
            "LeftPrimaryPanelHost",
            nameof(StrategicWorldRoot));
        Label title = GameUiSceneFactory.GetRequiredNode<Label>(
            hud,
            "TopBarHost/TopResourceBar/TopResourceBarContent/TopResourceRow/TitleResourceStack/Title",
            nameof(StrategicWorldRoot));
        if (title != null)
        {
            title.Text = Definition.DisplayName;
        }

        _resourceLabel = GameUiSceneFactory.GetRequiredNode<Label>(
            hud,
            "TopBarHost/TopResourceBar/TopResourceBarContent/TopResourceRow/TitleResourceStack/ResourceLabel",
            nameof(StrategicWorldRoot));
        _worldClockLabel = GameUiSceneFactory.GetRequiredNode<Label>(
            hud,
            "TopBarHost/TopResourceBar/TopResourceBarContent/TopResourceRow/WorldClockLabel",
            nameof(StrategicWorldRoot));
        _noticeLabel = GameUiSceneFactory.GetRequiredNode<Label>(
            hud,
            "TopBarHost/TopResourceBar/TopResourceBarContent/TopResourceRow/NoticeLabel",
            nameof(StrategicWorldRoot));
        _worldClockToggleButton = GameUiSceneFactory.GetRequiredNode<Button>(
            hud,
            "TopBarHost/TopResourceBar/TopResourceBarContent/TopResourceRow/TopRightControls/PauseButton",
            nameof(StrategicWorldRoot));
        _worldClockSpeedButton = GameUiSceneFactory.GetRequiredNode<Button>(
            hud,
            "TopBarHost/TopResourceBar/TopResourceBarContent/TopResourceRow/TopRightControls/QuickButton",
            nameof(StrategicWorldRoot));
        _siteTitleLabel = GameUiSceneFactory.GetRequiredNode<Label>(
            hud,
            "LeftPrimaryPanelHost/SiteDetailPanel/Margin/Scroll/Content/SummaryCard/SummaryMargin/SummaryStack/SiteTitleLabel",
            nameof(StrategicWorldRoot));
        _siteBodyLabel = GameUiSceneFactory.GetRequiredNode<Label>(
            hud,
            "LeftPrimaryPanelHost/SiteDetailPanel/Margin/Scroll/Content/SummaryCard/SummaryMargin/SummaryStack/SiteBodyLabel",
            nameof(StrategicWorldRoot));
        _siteSummaryCard = GameUiSceneFactory.GetRequiredNode<Control>(
            hud,
            "LeftPrimaryPanelHost/SiteDetailPanel/Margin/Scroll/Content/SummaryCard",
            nameof(StrategicWorldRoot));
        _opportunityCard = GameUiSceneFactory.GetRequiredNode<Control>(
            hud,
            "LeftPrimaryPanelHost/SiteDetailPanel/Margin/Scroll/Content/OpportunityCard",
            nameof(StrategicWorldRoot));
        _opportunityDetailContent = GameUiSceneFactory.GetRequiredNode<VBoxContainer>(
            hud,
            "LeftPrimaryPanelHost/SiteDetailPanel/Margin/Scroll/Content/OpportunityCard/OpportunityMargin/OpportunitySlot",
            nameof(StrategicWorldRoot));
        _facilityCard = GameUiSceneFactory.GetRequiredNode<Control>(
            hud,
            "LeftPrimaryPanelHost/SiteDetailPanel/Margin/Scroll/Content/InfrastructureCard",
            nameof(StrategicWorldRoot));
        _facilityTitleLabel = GameUiSceneFactory.GetRequiredNode<Label>(
            hud,
            "LeftPrimaryPanelHost/SiteDetailPanel/Margin/Scroll/Content/InfrastructureCard/InfrastructureMargin/InfrastructureStack/FacilityTitle",
            nameof(StrategicWorldRoot));
        _facilityList = GameUiSceneFactory.GetRequiredNode<VBoxContainer>(
            hud,
            "LeftPrimaryPanelHost/SiteDetailPanel/Margin/Scroll/Content/InfrastructureCard/InfrastructureMargin/InfrastructureStack/FacilityList",
            nameof(StrategicWorldRoot));
        _defenseCard = GameUiSceneFactory.GetRequiredNode<Control>(
            hud,
            "LeftPrimaryPanelHost/SiteDetailPanel/Margin/Scroll/Content/DefenseCard",
            nameof(StrategicWorldRoot));
        _garrisonTitleLabel = GameUiSceneFactory.GetRequiredNode<Label>(
            hud,
            "LeftPrimaryPanelHost/SiteDetailPanel/Margin/Scroll/Content/DefenseCard/DefenseMargin/DefenseStack/GarrisonTitle",
            nameof(StrategicWorldRoot));
        _garrisonList = GameUiSceneFactory.GetRequiredNode<VBoxContainer>(
            hud,
            "LeftPrimaryPanelHost/SiteDetailPanel/Margin/Scroll/Content/DefenseCard/DefenseMargin/DefenseStack/GarrisonList",
            nameof(StrategicWorldRoot));
        _threatList = GameUiSceneFactory.GetRequiredNode<VBoxContainer>(
            hud,
            "LeftPrimaryPanelHost/SiteDetailPanel/Margin/Scroll/Content/DefenseCard/DefenseMargin/DefenseStack/ThreatList",
            nameof(StrategicWorldRoot));
        Label threatTitleLabel = GameUiSceneFactory.GetRequiredNode<Label>(
            hud,
            "LeftPrimaryPanelHost/SiteDetailPanel/Margin/Scroll/Content/DefenseCard/DefenseMargin/DefenseStack/ThreatTitle",
            nameof(StrategicWorldRoot));
        _actionCard = GameUiSceneFactory.GetRequiredNode<Control>(
            hud,
            "LeftPrimaryPanelHost/SiteDetailPanel/Margin/Scroll/Content/ActionCard",
            nameof(StrategicWorldRoot));
        _actionList = GameUiSceneFactory.GetRequiredNode<VBoxContainer>(
            hud,
            "LeftPrimaryPanelHost/SiteDetailPanel/Margin/Scroll/Content/ActionCard/ActionMargin/ActionStack/ActionList",
            nameof(StrategicWorldRoot));
        _actionTitleLabel = GameUiSceneFactory.GetRequiredNode<Label>(
            hud,
            "LeftPrimaryPanelHost/SiteDetailPanel/Margin/Scroll/Content/ActionCard/ActionMargin/ActionStack/ActionTitle",
            nameof(StrategicWorldRoot));
        _opportunityDetailPanel = GameUiSceneFactory.Instantiate<WorldOpportunityDetailPanel>(
            GameUiSceneFactory.WorldOpportunityDetailPanelScenePath,
            nameof(StrategicWorldRoot));
        if (_opportunityDetailPanel != null && _opportunityDetailContent != null)
        {
            _opportunityDetailPanel.Visible = false;
            _opportunityDetailPanel.CompletePressed += CompleteSelectedOpportunity;
            _opportunityDetailContent.AddChild(_opportunityDetailPanel);
        }

        if (_facilityTitleLabel != null)
        {
            _facilityTitleLabel.Text = "建筑配置";
        }

        if (_garrisonTitleLabel != null)
        {
            _garrisonTitleLabel.Text = "驻防兵力";
        }

        if (threatTitleLabel != null)
        {
            threatTitleLabel.Text = "敌情追踪";
        }

        if (_actionTitleLabel != null)
        {
            _actionTitleLabel.Text = "行动面板";
        }

        if (_worldClockToggleButton != null)
        {
            _worldClockToggleButton.Pressed += ToggleWorldClock;
        }

        if (_worldClockSpeedButton != null)
        {
            _worldClockSpeedButton.Pressed += CycleWorldClockSpeed;
        }

        Button saveButton = GameUiSceneFactory.GetRequiredNode<Button>(
            hud,
            "TopBarHost/TopResourceBar/TopResourceBarContent/TopResourceRow/TopRightControls/SaveButton",
            nameof(StrategicWorldRoot));
        Button loadButton = GameUiSceneFactory.GetRequiredNode<Button>(
            hud,
            "TopBarHost/TopResourceBar/TopResourceBarContent/TopResourceRow/TopRightControls/LoadButton",
            nameof(StrategicWorldRoot));
        Button resetButton = GameUiSceneFactory.GetRequiredNode<Button>(
            hud,
            "TopBarHost/TopResourceBar/TopResourceBarContent/TopResourceRow/TopRightControls/ResetButton",
            nameof(StrategicWorldRoot));

        if (saveButton != null)
        {
            saveButton.Text = "保存";
            saveButton.Pressed += SaveWorld;
        }

        if (loadButton != null)
        {
            loadButton.Text = "读取";
            loadButton.Pressed += LoadWorld;
        }

        if (resetButton != null)
        {
            resetButton.Text = "重置";
            resetButton.Pressed += ResetWorld;
        }
    }

    private void BuildMapArea()
    {
        if (_worldMapOverlay == null)
        {
            GameLog.Error(nameof(StrategicWorldRoot), "WorldMapOverlayMissing");
            return;
        }

        foreach (WorldSiteDefinition site in Definition.SiteDefinitions)
        {
            Rect2 hitRect = ToViewportLocal(GetSiteHitRect(site));
            Rect2 labelRect = ToViewportLocal(GetSiteLabelRect(site));
            Button button = GameUiSceneFactory.CreateWorldSiteHitButton(nameof(StrategicWorldRoot));
            if (button == null)
            {
                continue;
            }

            button.Name = $"{site.Id}Button";
            button.Position = hitRect.Position;
            button.Size = hitRect.Size;
            button.Pressed += () =>
            {
                HideSiteHoverSummary(site.Id);
                SelectSite(site.Id);
            };
            button.MouseEntered += () => ShowSiteHoverSummary(site.Id);
            button.MouseExited += () => HideSiteHoverSummary(site.Id);
            button.GuiInput += @event => OnSiteButtonGuiInput(site.Id, @event);
            _worldMapOverlay.AddChild(button);
            _siteButtons[site.Id] = button;

            Label label = GameUiSceneFactory.CreateWorldSiteLabel(nameof(StrategicWorldRoot));
            if (label == null)
            {
                continue;
            }

            label.Name = $"{site.Id}Label";
            label.Position = labelRect.Position;
            label.Size = labelRect.Size;
            _worldMapOverlay.AddChild(label);
            _siteLabels[site.Id] = label;
        }
    }

    private void BuildSiteHoverSummaryPanel()
    {
        _siteHoverSummaryPanel = GameUiSceneFactory.CreateWorldSiteHoverSummaryPanel(nameof(StrategicWorldRoot));
        if (_siteHoverSummaryPanel == null)
        {
            return;
        }

        _siteHoverSummaryPanel.HideSummary();
        AddChild(_siteHoverSummaryPanel);
    }

    private void RefreshResources()
    {
        ResourceStore resources = State.PlayerResources;
        StrategicWorldDefinitionQueries queries = new(Definition);
        _resourceLabel.Text =
            $"{StrategicWorldDisplayNames.GetResourceLabel(queries, StrategicWorldIds.ResourcePopulation)} {resources.GetAvailable(StrategicWorldIds.ResourcePopulation)}/{resources.GetAmount(StrategicWorldIds.ResourcePopulation)}    " +
            $"{StrategicWorldDisplayNames.GetResourceLabel(queries, StrategicWorldIds.ResourceEconomy)} {resources.GetAmount(StrategicWorldIds.ResourceEconomy)}    " +
            $"{StrategicWorldDisplayNames.GetResourceLabel(queries, StrategicWorldIds.ResourceStone)} {resources.GetAmount(StrategicWorldIds.ResourceStone)}    " +
            $"世界步 {State.WorldTick}";
    }

    private void RefreshSiteButtons(StrategicWorldDefinitionQueries queries)
    {
        foreach ((string siteId, Button button) in _siteButtons)
        {
            WorldSiteState state = State.SiteStates[siteId];
            WorldSiteDefinition definition = queries.GetSite(siteId);
            Rect2 hitRect = ToViewportLocal(GetSiteHitRect(definition));
            button.Position = hitRect.Position;
            button.Size = hitRect.Size;
            WorldIntelVisibility siteVisibility = GetSiteIntelVisibility(definition);
            bool siteKnown = siteVisibility != WorldIntelVisibility.Unknown;
            button.Visible = siteKnown;
            if (!siteKnown && _hoveredSiteId == siteId)
            {
                HideSiteHoverSummary(siteId);
            }

            string threatMark = state.PendingThreatIds
                .Select(id => State.ThreatPlans.TryGetValue(id, out EnemyThreatPlan threat) ? threat : null)
                .Any(threat => threat is { Stage: ThreatStage.Attacking })
                ? "\n进攻中"
                : "";
            button.Text = "";
            bool isBlockedTarget = _isExpeditionTargeting
                ? IsSiteBlockedForExpeditionTarget(siteId)
                : IsSiteBlockedForSelectedSiteCommand(siteId);
            button.MouseDefaultCursorShape = isBlockedTarget
                ? CursorShape.Forbidden
                : CursorShape.PointingHand;
            button.TooltipText = "";

            if (_siteLabels.TryGetValue(siteId, out Label label))
            {
                Rect2 labelRect = ToViewportLocal(GetSiteLabelRect(definition));
                label.Position = labelRect.Position;
                label.Size = labelRect.Size;
                label.Visible = siteKnown;
                string displayName = siteVisibility == WorldIntelVisibility.Revealed &&
                                     State.Intel.KnownSites.TryGetValue(siteId, out WorldSiteIntelSnapshot snapshot) &&
                                     !string.IsNullOrWhiteSpace(snapshot.DisplayName)
                    ? snapshot.DisplayName
                    : definition.DisplayName;
                label.Text = siteVisibility == WorldIntelVisibility.Revealed
                    ? $"{displayName}\n旧情报"
                    : string.IsNullOrWhiteSpace(threatMark)
                    ? definition.DisplayName
                    : $"{definition.DisplayName}{threatMark}";
                label.TooltipText = "";
            }

            if (_hoveredSiteId == siteId)
            {
                RefreshSiteHoverSummary(queries, siteId);
            }
        }
    }

    private void ShowSiteHoverSummary(string siteId)
    {
        _hoveredSiteId = siteId;
        RefreshSiteHoverSummary(new StrategicWorldDefinitionQueries(Definition), siteId);
    }

    private void HideSiteHoverSummary(string siteId)
    {
        if (!string.IsNullOrWhiteSpace(siteId) && _hoveredSiteId != siteId)
        {
            return;
        }

        _hoveredSiteId = "";
        _siteHoverSummaryPanel?.HideSummary();
    }

    private void RefreshSiteHoverSummary(StrategicWorldDefinitionQueries queries, string siteId)
    {
        if (_siteHoverSummaryPanel == null ||
            string.IsNullOrWhiteSpace(siteId) ||
            !State.SiteStates.TryGetValue(siteId, out WorldSiteState state))
        {
            return;
        }

        WorldSiteDefinition definition = queries.GetSite(siteId);
        if (definition == null)
        {
            _siteHoverSummaryPanel.HideSummary();
            return;
        }

        WorldIntelVisibility visibility = GetSiteIntelVisibility(definition);
        if (visibility == WorldIntelVisibility.Unknown)
        {
            _siteHoverSummaryPanel.HideSummary();
            return;
        }

        if (visibility == WorldIntelVisibility.Revealed &&
            State.Intel.KnownSites.TryGetValue(siteId, out WorldSiteIntelSnapshot snapshot))
        {
            _siteHoverSummaryPanel.Bind(WorldSiteHoverSummaryPresenter.BuildSnapshot(queries, definition, snapshot));
        }
        else
        {
            _siteHoverSummaryPanel.Bind(WorldSiteHoverSummaryPresenter.Build(queries, definition, state));
        }

        _siteHoverSummaryPanel.Visible = true;
        _siteHoverSummaryPanel.ResetSize();

        Rect2 anchorBounds = TryGetSiteVisualScreenBounds(siteId, out Rect2 visualBounds)
            ? visualBounds
            : GetSiteHitRect(definition);
        Vector2 panelSize = _siteHoverSummaryPanel.Size;
        Vector2 minimumSize = _siteHoverSummaryPanel.GetCombinedMinimumSize();
        panelSize = new Vector2(
            Mathf.Max(panelSize.X, minimumSize.X),
            Mathf.Max(panelSize.Y, minimumSize.Y));
        Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
        _siteHoverSummaryPanel.Position = WorldSiteHoverSummaryPresenter.CalculatePanelPosition(
            anchorBounds,
            panelSize,
            viewportSize);
    }
}
