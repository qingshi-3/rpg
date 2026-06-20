using Godot;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.World.Sites;

internal sealed class WorldSitePeacetimeHudNodeRefs
{
    internal Control SitePeacetimePanel { get; private init; }
    internal Control BottomCommandHost { get; private init; }
    internal Control MinimapHost { get; private init; }
    internal Control ModalHost { get; private init; }
    internal Control BattleRuntimeCommandBar { get; private init; }
    internal Control BattleRuntimeHeroFrame { get; private init; }
    internal HBoxContainer BattleRuntimeHeroSelectorList { get; private init; }
    internal Label BattleRuntimeHeroNameLabel { get; private init; }
    internal Label BattleRuntimeHeroStateLabel { get; private init; }
    internal ProgressBar BattleRuntimeHeroHealthBar { get; private init; }
    internal ProgressBar BattleRuntimeHeroManaBar { get; private init; }
    internal HBoxContainer BattleRuntimeHeroSkillList { get; private init; }
    internal Button BattleRuntimeRegroupButton { get; private init; }
    internal Label SiteHudTitle { get; private init; }
    internal Label SiteResourceLabel { get; private init; }
    internal Button ReturnMapButton { get; private init; }
    internal HBoxContainer SiteManagementTabBar { get; private init; }
    internal Button BuildTabButton { get; private init; }
    internal Button RecruitTabButton { get; private init; }
    internal Button CorpsTabButton { get; private init; }
    internal Button OverviewTabButton { get; private init; }
    internal Control SiteBuildSection { get; private init; }
    internal Control SiteRecruitSection { get; private init; }
    internal Control SiteCorpsSection { get; private init; }
    internal Control SiteOverviewSection { get; private init; }
    internal Label SiteHudBody { get; private init; }
    internal Label SiteSelectionLabel { get; private init; }
    internal Control BattlePreparationRosterDock { get; private init; }
    internal VBoxContainer BattlePreparationRosterList { get; private init; }
    internal Control BattlePreparationPlanBar { get; private init; }
    internal Label BattlePreparationCompanyLabel { get; private init; }
    internal Label BattlePreparationObjectiveLabel { get; private init; }
    internal HBoxContainer BattlePreparationRuleButtonRow { get; private init; }
    internal Button BattlePreparationMoveFirstButton { get; private init; }
    internal Button BattlePreparationAttackFirstButton { get; private init; }
    internal Button BattlePreparationHoldButton { get; private init; }
    internal Button BattlePreparationStartButton { get; private init; }
    internal Control BattlePreparationObjectiveThumbnailDock { get; private init; }
    internal BattlePreparationObjectiveThumbnail BattlePreparationObjectiveThumbnail { get; private init; }
    internal Label SiteFacilityBuildTitle { get; private init; }
    internal GridContainer SiteFacilityBuildList { get; private init; }
    internal VBoxContainer SiteRecruitList { get; private init; }
    internal VBoxContainer SiteFacilityList { get; private init; }
    internal VBoxContainer SiteGarrisonList { get; private init; }
    internal Label SiteNoticeLabel { get; private init; }
    internal Label FacilityTitleLabel { get; private init; }
    internal Label GarrisonTitleLabel { get; private init; }
    internal Label NoticeTitleLabel { get; private init; }

    internal static WorldSitePeacetimeHudNodeRefs Resolve(Control root, string ownerName)
    {
        return new WorldSitePeacetimeHudNodeRefs
        {
            SitePeacetimePanel = Get<Control>(root, "LeftPrimaryPanelHost/SitePeacetimePanel", ownerName),
            BottomCommandHost = Get<Control>(root, "BottomCommandHost", ownerName),
            MinimapHost = Get<Control>(root, "MinimapHost", ownerName),
            ModalHost = Get<Control>(root, "ModalHost", ownerName),
            BattleRuntimeCommandBar = Get<Control>(root, "BottomCommandHost/BattleRuntimeCommandBar", ownerName),
            BattleRuntimeHeroFrame = Get<Control>(root, "BottomCommandHost/BattleRuntimeCommandBar/CommandMargin/BattleRuntimeHeroFrame", ownerName),
            BattleRuntimeHeroSelectorList = Get<HBoxContainer>(root, "BottomCommandHost/BattleRuntimeCommandBar/CommandMargin/BattleRuntimeHeroFrame/BattleRuntimeHeroSelectorList", ownerName),
            BattleRuntimeHeroNameLabel = Get<Label>(root, "BottomCommandHost/BattleRuntimeCommandBar/CommandMargin/BattleRuntimeHeroFrame/BattleRuntimeHeroInfoStack/BattleRuntimeHeroNameLabel", ownerName),
            BattleRuntimeHeroStateLabel = Get<Label>(root, "BottomCommandHost/BattleRuntimeCommandBar/CommandMargin/BattleRuntimeHeroFrame/BattleRuntimeHeroInfoStack/BattleRuntimeHeroStateLabel", ownerName),
            BattleRuntimeHeroHealthBar = Get<ProgressBar>(root, "BottomCommandHost/BattleRuntimeCommandBar/CommandMargin/BattleRuntimeHeroFrame/BattleRuntimeHeroInfoStack/BattleRuntimeHeroHealthBar", ownerName),
            BattleRuntimeHeroManaBar = Get<ProgressBar>(root, "BottomCommandHost/BattleRuntimeCommandBar/CommandMargin/BattleRuntimeHeroFrame/BattleRuntimeHeroInfoStack/BattleRuntimeHeroManaBar", ownerName),
            BattleRuntimeHeroSkillList = Get<HBoxContainer>(root, "BottomCommandHost/BattleRuntimeCommandBar/CommandMargin/BattleRuntimeHeroFrame/BattleRuntimeHeroSkillList", ownerName),
            BattleRuntimeRegroupButton = Get<Button>(root, "BottomCommandHost/BattleRuntimeCommandBar/CommandMargin/BattleRuntimeHeroFrame/BattleRuntimeRegroupButton", ownerName),
            SiteHudTitle = Get<Label>(root, "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/SiteManagementStack/SiteHudTitle", ownerName),
            SiteResourceLabel = Get<Label>(root, "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/SiteManagementStack/SiteResourceLabel", ownerName),
            ReturnMapButton = Get<Button>(root, "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/SiteManagementStack/SiteManagementHeader/ReturnMapButton", ownerName),
            SiteManagementTabBar = Get<HBoxContainer>(root, "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/SiteManagementStack/SiteManagementHeader/SiteManagementTabBar", ownerName),
            BuildTabButton = Get<Button>(root, "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/SiteManagementStack/SiteManagementHeader/SiteManagementTabBar/BuildTabButton", ownerName),
            RecruitTabButton = Get<Button>(root, "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/SiteManagementStack/SiteManagementHeader/SiteManagementTabBar/RecruitTabButton", ownerName),
            CorpsTabButton = Get<Button>(root, "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/SiteManagementStack/SiteManagementHeader/SiteManagementTabBar/CorpsTabButton", ownerName),
            OverviewTabButton = Get<Button>(root, "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/SiteManagementStack/SiteManagementHeader/SiteManagementTabBar/OverviewTabButton", ownerName),
            SiteBuildSection = Get<Control>(root, "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/SiteManagementStack/ManagementContentScroll/ManagementContent/SiteBuildSection", ownerName),
            SiteRecruitSection = Get<Control>(root, "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/SiteManagementStack/ManagementContentScroll/ManagementContent/SiteRecruitSection", ownerName),
            SiteCorpsSection = Get<Control>(root, "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/SiteManagementStack/ManagementContentScroll/ManagementContent/SiteCorpsSection", ownerName),
            SiteOverviewSection = Get<Control>(root, "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/SiteManagementStack/ManagementContentScroll/ManagementContent/SiteOverviewSection", ownerName),
            SiteHudBody = Get<Label>(root, "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/SiteManagementStack/ManagementContentScroll/ManagementContent/SiteOverviewSection/SiteHudBody", ownerName),
            SiteSelectionLabel = Get<Label>(root, "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/SiteManagementStack/ManagementContentScroll/ManagementContent/SiteOverviewSection/SiteSelectionLabel", ownerName),
            BattlePreparationRosterDock = Get<Control>(root, "OverlayHost/BattlePreparationRosterDock", ownerName),
            BattlePreparationRosterList = Get<VBoxContainer>(root, "OverlayHost/BattlePreparationRosterDock/RosterMargin/BattlePreparationRosterList", ownerName),
            BattlePreparationPlanBar = Get<Control>(root, "OverlayHost/BattlePreparationPlanBar", ownerName),
            BattlePreparationCompanyLabel = Get<Label>(root, "OverlayHost/BattlePreparationPlanBar/PlanMargin/PlanRow/BattlePreparationCompanyLabel", ownerName),
            BattlePreparationObjectiveLabel = Get<Label>(root, "OverlayHost/BattlePreparationPlanBar/PlanMargin/PlanRow/BattlePreparationObjectiveLabel", ownerName),
            BattlePreparationRuleButtonRow = Get<HBoxContainer>(root, "OverlayHost/BattlePreparationPlanBar/PlanMargin/PlanRow/BattlePreparationRuleButtonRow", ownerName),
            BattlePreparationMoveFirstButton = Get<Button>(root, "OverlayHost/BattlePreparationPlanBar/PlanMargin/PlanRow/BattlePreparationRuleButtonRow/MoveFirstRuleButton", ownerName),
            BattlePreparationAttackFirstButton = Get<Button>(root, "OverlayHost/BattlePreparationPlanBar/PlanMargin/PlanRow/BattlePreparationRuleButtonRow/AttackFirstRuleButton", ownerName),
            BattlePreparationHoldButton = Get<Button>(root, "OverlayHost/BattlePreparationPlanBar/PlanMargin/PlanRow/BattlePreparationRuleButtonRow/HoldRuleButton", ownerName),
            BattlePreparationStartButton = Get<Button>(root, "OverlayHost/BattlePreparationPlanBar/PlanMargin/PlanRow/BattlePreparationStartButton", ownerName),
            BattlePreparationObjectiveThumbnailDock = Get<Control>(root, "MinimapHost/BattlePreparationObjectiveThumbnailDock", ownerName),
            BattlePreparationObjectiveThumbnail = Get<BattlePreparationObjectiveThumbnail>(root, "MinimapHost/BattlePreparationObjectiveThumbnailDock/BattlePreparationObjectiveThumbnail", ownerName),
            SiteFacilityBuildTitle = Get<Label>(root, "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/SiteManagementStack/ManagementContentScroll/ManagementContent/SiteBuildSection/BuildTitle", ownerName),
            SiteFacilityBuildList = Get<GridContainer>(root, "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/SiteManagementStack/ManagementContentScroll/ManagementContent/SiteBuildSection/SiteFacilityBuildList", ownerName),
            SiteRecruitList = Get<VBoxContainer>(root, "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/SiteManagementStack/ManagementContentScroll/ManagementContent/SiteRecruitSection/SiteRecruitList", ownerName),
            SiteFacilityList = Get<VBoxContainer>(root, "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/SiteManagementStack/ManagementContentScroll/ManagementContent/SiteOverviewSection/SiteFacilityList", ownerName),
            SiteGarrisonList = Get<VBoxContainer>(root, "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/SiteManagementStack/ManagementContentScroll/ManagementContent/SiteCorpsSection/SiteGarrisonList", ownerName),
            SiteNoticeLabel = Get<Label>(root, "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/SiteManagementStack/ManagementContentScroll/ManagementContent/SiteOverviewSection/SiteNoticeLabel", ownerName),
            FacilityTitleLabel = Get<Label>(root, "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/SiteManagementStack/ManagementContentScroll/ManagementContent/SiteOverviewSection/FacilityTitle", ownerName),
            GarrisonTitleLabel = Get<Label>(root, "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/SiteManagementStack/ManagementContentScroll/ManagementContent/SiteCorpsSection/GarrisonTitle", ownerName),
            NoticeTitleLabel = Get<Label>(root, "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/SiteManagementStack/ManagementContentScroll/ManagementContent/SiteOverviewSection/NoticeTitle", ownerName)
        };
    }

    private static T Get<T>(Control root, NodePath path, string ownerName) where T : Node =>
        GameUiSceneFactory.GetRequiredNode<T>(root, path, ownerName);
}
