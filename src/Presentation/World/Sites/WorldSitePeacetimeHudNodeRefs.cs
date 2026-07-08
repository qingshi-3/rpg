using Godot;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.World.Sites;

internal sealed class WorldSitePeacetimeHudNodeRefs
{
    internal Control SitePeacetimePanel { get; private init; }
    internal Control SiteResourceBar { get; private init; }
    internal Control SiteManagementTabRail { get; private init; }
    internal Control BottomCommandHost { get; private init; }
    internal Control MinimapHost { get; private init; }
    internal Control ModalHost { get; private init; }
    internal Control BattleRuntimeSummaryBar { get; private init; }
    internal HBoxContainer BattleRuntimeSummaryList { get; private init; }
    internal Control BattleRuntimeCommandBar { get; private init; }
    internal Control BattleRuntimePauseDetailPanel { get; private init; }
    internal Control BattleRuntimeHeroFrame { get; private init; }
    internal HBoxContainer BattleRuntimeHeroSelectorList { get; private init; }
    internal Label BattleRuntimeHeroNameLabel { get; private init; }
    internal Label BattleRuntimeHeroStateLabel { get; private init; }
    internal ProgressBar BattleRuntimeHeroHealthBar { get; private init; }
    internal ProgressBar BattleRuntimeHeroManaBar { get; private init; }
    internal HBoxContainer BattleRuntimeHeroSkillList { get; private init; }
    internal Label SiteHudTitle { get; private init; }
    internal Label SiteResourceLabel { get; private init; }
    internal Button ReturnMapButton { get; private init; }
    internal Button BuildTabButton { get; private init; }
    internal Button ConscriptionTabButton { get; private init; }
    internal Button RecruitTabButton { get; private init; }
    internal Button OverviewTabButton { get; private init; }
    internal Button SitePanelCloseButton { get; private init; }
    internal Control SiteBuildSection { get; private init; }
    internal Control SiteConscriptionSection { get; private init; }
    internal Control SiteOverviewSection { get; private init; }
    internal Control MilitaryWorkbenchBackdrop { get; private init; }
    internal Control MilitaryWorkbenchPanel { get; private init; }
    internal VBoxContainer MilitaryHeroList { get; private init; }
    internal GridContainer MilitaryMusterGrid { get; private init; }
    internal Label MilitaryHeroSummaryLabel { get; private init; }
    internal Label MilitaryNoticeLabel { get; private init; }
    internal BattleUnitPlinthPreview MilitarySelectedHeroPreview { get; private init; }
    internal Label MilitarySelectedHeroNameLabel { get; private init; }
    internal Label MilitarySelectedHeroCorpsLabel { get; private init; }
    internal Button MilitaryCloseButton { get; private init; }
    internal Label SiteHudBody { get; private init; }
    internal Label SiteSelectionLabel { get; private init; }
    internal Control BattlePreparationRosterDock { get; private init; }
    internal VBoxContainer BattlePreparationRosterList { get; private init; }
    internal Control BattlePreparationLaunchDock { get; private init; }
    internal Button BattlePreparationStartButton { get; private init; }
    internal Control BattlePreparationObjectiveThumbnailDock { get; private init; }
    internal BattlePreparationObjectiveThumbnail BattlePreparationObjectiveThumbnail { get; private init; }
    internal Label BattlePreparationTopPromptLabel { get; private init; }
    internal Label SiteBuildingBuildTitle { get; private init; }
    internal GridContainer SiteBuildingOptionGrid { get; private init; }
    internal VBoxContainer SiteConscriptionList { get; private init; }
    internal VBoxContainer SiteBuildingList { get; private init; }
    internal Label SiteNoticeLabel { get; private init; }
    internal Label BuildingTitleLabel { get; private init; }
    internal Label NoticeTitleLabel { get; private init; }

    internal static WorldSitePeacetimeHudNodeRefs Resolve(Control root, string ownerName)
    {
        return new WorldSitePeacetimeHudNodeRefs
        {
            SitePeacetimePanel = Get<Control>(root, "OverlayHost/SitePeacetimePanel", ownerName),
            SiteResourceBar = Get<Control>(root, "TopBarHost/TopLeftStatus", ownerName),
            SiteManagementTabRail = Get<Control>(root, "OverlayHost/SiteManagementTabRail", ownerName),
            BottomCommandHost = Get<Control>(root, "BottomCommandHost", ownerName),
            MinimapHost = Get<Control>(root, "MinimapHost", ownerName),
            ModalHost = Get<Control>(root, "ModalHost", ownerName),
            BattleRuntimeSummaryBar = Get<Control>(root, "BottomCommandHost/BattleRuntimeSummaryBar", ownerName),
            BattleRuntimeSummaryList = Get<HBoxContainer>(root, "BottomCommandHost/BattleRuntimeSummaryBar/SummaryMargin/BattleRuntimeSummaryScroll/BattleRuntimeSummaryList", ownerName),
            BattleRuntimeCommandBar = Get<Control>(root, "BottomCommandHost/BattleRuntimeCommandBar", ownerName),
            BattleRuntimePauseDetailPanel = Get<Control>(root, "BottomCommandHost/BattleRuntimePauseDetailPanel", ownerName),
            BattleRuntimeHeroFrame = Get<Control>(root, "BottomCommandHost/BattleRuntimePauseDetailPanel/PauseDetailMargin/PauseDetailStack/PauseDetailBody/BattleRuntimeHeroFramePanel/HeroFrameMargin/BattleRuntimeHeroFrame", ownerName),
            BattleRuntimeHeroSelectorList = Get<HBoxContainer>(root, "BottomCommandHost/BattleRuntimePauseDetailPanel/PauseDetailMargin/PauseDetailStack/PauseDetailBody/BattleRuntimeHeroFramePanel/HeroFrameMargin/BattleRuntimeHeroFrame/BattleRuntimeHeroSelectorList", ownerName),
            BattleRuntimeHeroNameLabel = Get<Label>(root, "BottomCommandHost/BattleRuntimePauseDetailPanel/PauseDetailMargin/PauseDetailStack/PauseDetailBody/BattleRuntimeHeroFramePanel/HeroFrameMargin/BattleRuntimeHeroFrame/BattleRuntimeHeroInfoStack/BattleRuntimeHeroNameLabel", ownerName),
            BattleRuntimeHeroStateLabel = Get<Label>(root, "BottomCommandHost/BattleRuntimePauseDetailPanel/PauseDetailMargin/PauseDetailStack/PauseDetailBody/BattleRuntimeHeroFramePanel/HeroFrameMargin/BattleRuntimeHeroFrame/BattleRuntimeHeroInfoStack/BattleRuntimeHeroStateLabel", ownerName),
            BattleRuntimeHeroHealthBar = Get<ProgressBar>(root, "BottomCommandHost/BattleRuntimePauseDetailPanel/PauseDetailMargin/PauseDetailStack/PauseDetailBody/BattleRuntimeHeroFramePanel/HeroFrameMargin/BattleRuntimeHeroFrame/BattleRuntimeHeroInfoStack/BattleRuntimeHeroHealthBar", ownerName),
            BattleRuntimeHeroManaBar = Get<ProgressBar>(root, "BottomCommandHost/BattleRuntimePauseDetailPanel/PauseDetailMargin/PauseDetailStack/PauseDetailBody/BattleRuntimeHeroFramePanel/HeroFrameMargin/BattleRuntimeHeroFrame/BattleRuntimeHeroInfoStack/BattleRuntimeHeroManaBar", ownerName),
            BattleRuntimeHeroSkillList = Get<HBoxContainer>(root, "BottomCommandHost/BattleRuntimePauseDetailPanel/PauseDetailMargin/PauseDetailStack/PauseDetailBody/BattleRuntimeSkillCommandPanel/SkillCommandMargin/BattleRuntimeSkillCommandStack/BattleRuntimeHeroSkillList", ownerName),
            SiteHudTitle = Get<Label>(root, "OverlayHost/SitePeacetimePanel/Margin/SiteManagementStack/SiteHudTitle", ownerName),
            SiteResourceLabel = Get<Label>(root, "TopBarHost/TopLeftStatus/Margin/SiteResourceLabel", ownerName),
            ReturnMapButton = Get<Button>(root, "OverlayHost/SiteManagementTabRail/ReturnMapTabButton", ownerName),
            BuildTabButton = Get<Button>(root, "OverlayHost/SiteManagementTabRail/BuildTabButton", ownerName),
            ConscriptionTabButton = Get<Button>(root, "OverlayHost/SiteManagementTabRail/ConscriptionTabButton", ownerName),
            RecruitTabButton = Get<Button>(root, "OverlayHost/SiteManagementTabRail/RecruitTabButton", ownerName),
            OverviewTabButton = Get<Button>(root, "OverlayHost/SiteManagementTabRail/OverviewTabButton", ownerName),
            SitePanelCloseButton = Get<Button>(root, "OverlayHost/SitePeacetimePanel/Margin/SiteManagementStack/SiteManagementHeader/SitePanelCloseButton", ownerName),
            SiteBuildSection = Get<Control>(root, "OverlayHost/SitePeacetimePanel/Margin/SiteManagementStack/ManagementContentScroll/ManagementContent/SiteBuildSection", ownerName),
            SiteConscriptionSection = Get<Control>(root, "OverlayHost/SitePeacetimePanel/Margin/SiteManagementStack/ManagementContentScroll/ManagementContent/SiteConscriptionSection", ownerName),
            SiteOverviewSection = Get<Control>(root, "OverlayHost/SitePeacetimePanel/Margin/SiteManagementStack/ManagementContentScroll/ManagementContent/SiteOverviewSection", ownerName),
            MilitaryWorkbenchBackdrop = Get<Control>(root, "ModalHost/MilitaryWorkbenchBackdrop", ownerName),
            SiteHudBody = Get<Label>(root, "OverlayHost/SitePeacetimePanel/Margin/SiteManagementStack/ManagementContentScroll/ManagementContent/SiteOverviewSection/SiteHudBody", ownerName),
            SiteSelectionLabel = Get<Label>(root, "OverlayHost/SitePeacetimePanel/Margin/SiteManagementStack/ManagementContentScroll/ManagementContent/SiteOverviewSection/SiteSelectionLabel", ownerName),
            BattlePreparationRosterDock = Get<Control>(root, "OverlayHost/BattlePreparationRosterDock", ownerName),
            BattlePreparationRosterList = Get<VBoxContainer>(root, "OverlayHost/BattlePreparationRosterDock/RosterMargin/BattlePreparationRosterList", ownerName),
            BattlePreparationLaunchDock = Get<Control>(root, "OverlayHost/BattlePreparationLaunchDock", ownerName),
            BattlePreparationStartButton = Get<Button>(root, "OverlayHost/BattlePreparationLaunchDock/BattlePreparationStartButton", ownerName),
            BattlePreparationObjectiveThumbnailDock = Get<Control>(root, "MinimapHost/BattlePreparationObjectiveThumbnailDock", ownerName),
            BattlePreparationObjectiveThumbnail = Get<BattlePreparationObjectiveThumbnail>(root, "MinimapHost/BattlePreparationObjectiveThumbnailDock/BattlePreparationObjectiveThumbnail", ownerName),
            BattlePreparationTopPromptLabel = Get<Label>(root, "OverlayHost/BattlePreparationTopPromptDock/BattlePreparationTopPromptLabel", ownerName),
            SiteBuildingBuildTitle = Get<Label>(root, "OverlayHost/SitePeacetimePanel/Margin/SiteManagementStack/ManagementContentScroll/ManagementContent/SiteBuildSection/BuildTitle", ownerName),
            SiteBuildingOptionGrid = Get<GridContainer>(root, "OverlayHost/SitePeacetimePanel/Margin/SiteManagementStack/ManagementContentScroll/ManagementContent/SiteBuildSection/SiteBuildingOptionGrid", ownerName),
            SiteConscriptionList = Get<VBoxContainer>(root, "OverlayHost/SitePeacetimePanel/Margin/SiteManagementStack/ManagementContentScroll/ManagementContent/SiteConscriptionSection/SiteConscriptionList", ownerName),
            MilitaryWorkbenchPanel = Get<Control>(root, "ModalHost/MilitaryWorkbenchPanel", ownerName),
            MilitaryHeroList = Get<VBoxContainer>(root, "ModalHost/MilitaryWorkbenchPanel/WorkbenchMargin/WorkbenchStack/MilitaryBody/MilitaryHeroScroll/MilitaryHeroList", ownerName),
            MilitaryMusterGrid = Get<GridContainer>(root, "ModalHost/MilitaryWorkbenchPanel/WorkbenchMargin/WorkbenchStack/MilitaryBody/MilitaryDetailStack/MilitaryMusterScroll/MilitaryMusterGrid", ownerName),
            MilitaryHeroSummaryLabel = Get<Label>(root, "ModalHost/MilitaryWorkbenchPanel/WorkbenchMargin/WorkbenchStack/MilitaryHeader/MilitaryHeroSummaryLabel", ownerName),
            MilitaryNoticeLabel = Get<Label>(root, "ModalHost/MilitaryWorkbenchPanel/WorkbenchMargin/WorkbenchStack/MilitaryNoticeLabel", ownerName),
            MilitarySelectedHeroPreview = Get<BattleUnitPlinthPreview>(root, "ModalHost/MilitaryWorkbenchPanel/WorkbenchMargin/WorkbenchStack/MilitaryBody/MilitaryDetailStack/SelectedHeroPanel/SelectedHeroMargin/SelectedHeroRow/SelectedHeroAvatarFrame/SelectedHeroPlinthPreview", ownerName),
            MilitarySelectedHeroNameLabel = Get<Label>(root, "ModalHost/MilitaryWorkbenchPanel/WorkbenchMargin/WorkbenchStack/MilitaryBody/MilitaryDetailStack/SelectedHeroPanel/SelectedHeroMargin/SelectedHeroRow/SelectedHeroTextStack/SelectedHeroNameLabel", ownerName),
            MilitarySelectedHeroCorpsLabel = Get<Label>(root, "ModalHost/MilitaryWorkbenchPanel/WorkbenchMargin/WorkbenchStack/MilitaryBody/MilitaryDetailStack/SelectedHeroPanel/SelectedHeroMargin/SelectedHeroRow/SelectedHeroTextStack/SelectedHeroCorpsLabel", ownerName),
            MilitaryCloseButton = Get<Button>(root, "ModalHost/MilitaryWorkbenchPanel/WorkbenchMargin/WorkbenchStack/MilitaryHeader/MilitaryCloseButton", ownerName),
            SiteBuildingList = Get<VBoxContainer>(root, "OverlayHost/SitePeacetimePanel/Margin/SiteManagementStack/ManagementContentScroll/ManagementContent/SiteOverviewSection/SiteBuildingList", ownerName),
            SiteNoticeLabel = Get<Label>(root, "OverlayHost/SitePeacetimePanel/Margin/SiteManagementStack/ManagementContentScroll/ManagementContent/SiteOverviewSection/SiteNoticeLabel", ownerName),
            BuildingTitleLabel = Get<Label>(root, "OverlayHost/SitePeacetimePanel/Margin/SiteManagementStack/ManagementContentScroll/ManagementContent/SiteOverviewSection/BuildingTitle", ownerName),
            NoticeTitleLabel = Get<Label>(root, "OverlayHost/SitePeacetimePanel/Margin/SiteManagementStack/ManagementContentScroll/ManagementContent/SiteOverviewSection/NoticeTitle", ownerName)
        };
    }

    private static T Get<T>(Control root, NodePath path, string ownerName) where T : Node =>
        GameUiSceneFactory.GetRequiredNode<T>(root, path, ownerName);
}
