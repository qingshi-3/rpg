using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle;
using Rpg.Application.StrategicManagement;
using Rpg.Application.World;
using Rpg.Definitions.StrategicManagement;
using Rpg.Definitions.World;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.World;

public partial class StrategicWorldRoot
{
    private readonly Dictionary<string, WorldResourceTicker> _resourceAmountTickers = new(System.StringComparer.Ordinal);
    private string _lastResourceTickerSignature = "";

    private void BuildUi()
    {
        BuildSiteNameOverlay();
        Control hud = GameUiSceneFactory.Instantiate<Control>(
            GameUiSceneFactory.StrategicWorldHudScenePath,
            nameof(StrategicWorldRoot));
        if (hud == null)
        {
            return;
        }

        _strategicHudRoot = hud;
        AddChild(hud);
        BindStrategicHud(hud);
        UpdateMainWorldViewportLayout(GetMapBounds());
        WarmupStrategicSelectionUiScenes();
        SyncSiteNameOverlay();
        BuildSiteHoverSummaryPanel();
    }

    private void WarmupStrategicSelectionUiScenes()
    {
        GameUiSceneFactory.Preload(
            GameUiSceneFactory.WorldMutedLineScenePath,
            GameUiSceneFactory.WorldSiteNameBadgeScenePath,
            GameUiSceneFactory.WorldPrimaryActionButtonScenePath,
            GameUiSceneFactory.WorldSecondaryActionButtonScenePath,
            GameUiSceneFactory.WorldCompactMarkerButtonScenePath,
            GameUiSceneFactory.WorldExpeditionCountRowScenePath,
            GameUiSceneFactory.WorldResourceFloatTextScenePath,
            GameUiSceneFactory.StrategicBattleGateForcePreviewCardScenePath,
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
        AddWarmupNode(warmupRoot, GameUiSceneFactory.CreateStrategicBattleGateForcePreviewCard(nameof(StrategicWorldRoot)));
        AddWarmupNode(warmupRoot, GameUiSceneFactory.CreateWorldResourceFloatText(nameof(StrategicWorldRoot)));
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
        _modalHost = GameUiSceneFactory.GetRequiredNode<Control>(
            hud,
            "ModalHost",
            nameof(StrategicWorldRoot));
        BindWorldResourceFloatOverlay(hud);
        _resourceAmountTickers.Clear();
        BindResourceAmountTicker(
            hud,
            StrategicManagementIds.ResourceFood,
            "TopBarHost/TopLeftStatus/Margin/ResourceStrip/FoodSlot/FoodAmountTicker");
        BindResourceAmountTicker(
            hud,
            StrategicManagementIds.ResourceMoney,
            "TopBarHost/TopLeftStatus/Margin/ResourceStrip/MoneySlot/MoneyAmountTicker");
        BindResourceAmountTicker(
            hud,
            StrategicManagementIds.ResourceOre,
            "TopBarHost/TopLeftStatus/Margin/ResourceStrip/OreSlot/OreAmountTicker");
        BindResourceAmountTicker(
            hud,
            StrategicManagementIds.ResourceWood,
            "TopBarHost/TopLeftStatus/Margin/ResourceStrip/WoodSlot/WoodAmountTicker");
        _worldClockLabel = GameUiSceneFactory.GetRequiredNode<Label>(
            hud,
            "TopBarHost/WorldClockLabel",
            nameof(StrategicWorldRoot));
        _noticeLabel = GameUiSceneFactory.GetRequiredNode<Label>(
            hud,
            "TopBarHost/NoticeLabel",
            nameof(StrategicWorldRoot));
        _worldClockToggleButton = GameUiSceneFactory.GetRequiredNode<TextureButton>(
            hud,
            "TopBarHost/TopRightControls/PauseButton",
            nameof(StrategicWorldRoot));
        _worldClockSpeedButton = GameUiSceneFactory.GetRequiredNode<TextureButton>(
            hud,
            "TopBarHost/TopRightControls/QuickButton",
            nameof(StrategicWorldRoot));
        _siteDetailPanel = GameUiSceneFactory.GetRequiredNode<Control>(
            hud,
            "OverlayHost/SiteDetailPanel",
            nameof(StrategicWorldRoot));
        _siteDetailBodyScroll = GameUiSceneFactory.GetRequiredNode<ScrollContainer>(
            hud,
            "OverlayHost/SiteDetailPanel/Margin/SheetContent/BodyScroll",
            nameof(StrategicWorldRoot));
        _siteTitleLabel = GameUiSceneFactory.GetRequiredNode<Label>(
            hud,
            "OverlayHost/SiteDetailPanel/Margin/SheetContent/BodyScroll/BodyContent/SummaryCard/SummaryMargin/SummaryStack/SiteTitleLabel",
            nameof(StrategicWorldRoot));
        _siteBodyLabel = GameUiSceneFactory.GetRequiredNode<Label>(
            hud,
            "OverlayHost/SiteDetailPanel/Margin/SheetContent/BodyScroll/BodyContent/SummaryCard/SummaryMargin/SummaryStack/SiteBodyLabel",
            nameof(StrategicWorldRoot));
        _siteSummaryCard = GameUiSceneFactory.GetRequiredNode<Control>(
            hud,
            "OverlayHost/SiteDetailPanel/Margin/SheetContent/BodyScroll/BodyContent/SummaryCard",
            nameof(StrategicWorldRoot));
        _opportunityCard = GameUiSceneFactory.GetRequiredNode<Control>(
            hud,
            "OverlayHost/SiteDetailPanel/Margin/SheetContent/BodyScroll/BodyContent/OpportunityCard",
            nameof(StrategicWorldRoot));
        _opportunityDetailContent = GameUiSceneFactory.GetRequiredNode<VBoxContainer>(
            hud,
            "OverlayHost/SiteDetailPanel/Margin/SheetContent/BodyScroll/BodyContent/OpportunityCard/OpportunityMargin/OpportunitySlot",
            nameof(StrategicWorldRoot));
        _actionCard = GameUiSceneFactory.GetRequiredNode<Control>(
            hud,
            "OverlayHost/SiteDetailPanel/Margin/SheetContent/ActionCard",
            nameof(StrategicWorldRoot));
        _actionList = GameUiSceneFactory.GetRequiredNode<VBoxContainer>(
            hud,
            "OverlayHost/SiteDetailPanel/Margin/SheetContent/ActionCard/ActionMargin/ActionStack/ActionScroll/ActionList",
            nameof(StrategicWorldRoot));
        _actionTitleLabel = GameUiSceneFactory.GetRequiredNode<Label>(
            hud,
            "OverlayHost/SiteDetailPanel/Margin/SheetContent/ActionCard/ActionMargin/ActionStack/ActionTitle",
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

        if (_actionTitleLabel != null)
        {
            _actionTitleLabel.Text = "可执行";
        }

        if (_worldClockToggleButton != null)
        {
            _worldClockToggleButton.Pressed += ToggleWorldClock;
        }

        if (_worldClockSpeedButton != null)
        {
            _worldClockSpeedButton.Pressed += CycleWorldClockSpeed;
        }

        TextureButton resetButton = GameUiSceneFactory.GetRequiredNode<TextureButton>(
            hud,
            "TopBarHost/TopRightControls/ResetButton",
            nameof(StrategicWorldRoot));

        if (resetButton != null)
        {
            resetButton.TooltipText = "重置大地图";
            resetButton.Pressed += ResetWorld;
        }
    }

    private void BindResourceAmountTicker(Control hud, string resourceId, string nodePath)
    {
        WorldResourceTicker ticker = GameUiSceneFactory.GetRequiredNode<WorldResourceTicker>(
            hud,
            nodePath,
            nameof(StrategicWorldRoot));
        if (ticker != null)
        {
            _resourceAmountTickers[resourceId] = ticker;
        }
    }

    private void ResetWorld()
    {
        StrategicWorldRuntime.Reset();
        StrategicManagementRuntime.Reset();
        ResetStrategicFogMaskCache();
        _selectedSiteId = "";
        _selectedOpportunityId = "";
        _selectedArmyIds.Clear();
        _worldClockAccumulator = 0.0;
        _worldClockPaused = false;
        StrategicWorldRuntime.LastNotice = "世界状态已重置。";
        RefreshAll();
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
        StrategicManagementDashboardViewModel dashboard = StrategicManagementRuntime.BuildDashboard(
            StrategicManagementIds.FactionPlayer,
            StrategicManagementIds.LocationQingheCore);
        string signature = string.Join(
            "|",
            dashboard.Resources.Select(resource => $"{resource.ResourceId}:{resource.Amount}"));
        bool animate = !string.IsNullOrWhiteSpace(_lastResourceTickerSignature) &&
                       !string.Equals(signature, _lastResourceTickerSignature, System.StringComparison.Ordinal);
        _lastResourceTickerSignature = signature;
        foreach (StrategicResourceViewModel resource in dashboard.Resources)
        {
            if (_resourceAmountTickers.TryGetValue(resource.ResourceId, out WorldResourceTicker ticker))
            {
                ticker.SetText(resource.Amount.ToString(), animate);
            }
        }
    }

    private Rect2 GetSiteMarkerLayoutHitRect(WorldSiteDefinition definition, float zoom = 1.0f)
    {
        float inverseZoom = 1.0f / Mathf.Max(zoom, 0.001f);
        if (definition != null && TryGetSiteVisualMapBounds(definition.Id, out Rect2 mapBounds))
        {
            return mapBounds.Grow(SiteVisualHitPadding * inverseZoom);
        }

        Vector2 center = GetSiteMapPosition(definition);
        Vector2 size = SiteButtonSize * inverseZoom;
        return new Rect2(center - size / 2.0f, size);
    }

    private void ShowSiteHoverSummary(string siteId)
    {
        _hoveredSiteId = siteId;
        RefreshSiteHoverSummary(new StrategicWorldDefinitionQueries(Definition), siteId);
        SyncSiteNameOverlay();
    }

    private void HideSiteHoverSummary(string siteId)
    {
        if (!string.IsNullOrWhiteSpace(siteId) && _hoveredSiteId != siteId)
        {
            return;
        }

        _hoveredSiteId = "";
        _siteHoverSummaryPanel?.HideSummary();
        SyncSiteNameOverlay();
    }

    private void RefreshSiteHoverSummary(StrategicWorldDefinitionQueries queries, string siteId)
    {
        if (_siteHoverSummaryPanel == null ||
            string.IsNullOrWhiteSpace(siteId))
        {
            return;
        }

        WorldSiteDefinition definition = queries.GetSite(siteId);
        if (definition == null ||
            !TryBuildStrategicLocationDashboardForMapSite(siteId, out StrategicManagementDashboardViewModel dashboard))
        {
            _siteHoverSummaryPanel.HideSummary();
            return;
        }

        _siteHoverSummaryPanel.Bind(WorldSiteHoverSummaryPresenter.Build(definition, dashboard));
        _siteHoverSummaryPanel.Visible = true;
        _siteHoverSummaryPanel.ResetSize();

        Rect2 anchorBounds = TryGetSiteVisualScreenBounds(siteId, out Rect2 visualBounds) ? visualBounds : GetSiteHitRect(definition);
        Vector2 panelSize = _siteHoverSummaryPanel.Size;
        Vector2 minimumSize = _siteHoverSummaryPanel.GetCombinedMinimumSize();
        panelSize = new Vector2(Mathf.Max(panelSize.X, minimumSize.X), Mathf.Max(panelSize.Y, minimumSize.Y));
        Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
        _siteHoverSummaryPanel.Position = WorldSiteHoverSummaryPresenter.CalculatePanelPosition(anchorBounds, panelSize, viewportSize);
    }
}
