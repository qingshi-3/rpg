using Godot;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.World.Sites;

internal sealed class WorldSitePeacetimeHudNodeRefs
{
    internal Control SiteTopBar { get; private init; }
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
    internal Label SiteHudBody { get; private init; }
    internal Label SiteSelectionLabel { get; private init; }
    internal Control SiteOverviewCard { get; private init; }
    internal Control SiteFacilityBuildCard { get; private init; }
    internal Control SiteFacilityCard { get; private init; }
    internal Control SiteDefenseCard { get; private init; }
    internal Control SiteActionCard { get; private init; }
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
    internal VBoxContainer SiteFacilityBuildList { get; private init; }
    internal VBoxContainer SiteFacilityList { get; private init; }
    internal VBoxContainer SiteGarrisonList { get; private init; }
    internal VBoxContainer SiteActionList { get; private init; }
    internal Label SiteNoticeLabel { get; private init; }
    internal Label SiteOperationHintLabel { get; private init; }
    internal Label FacilityTitleLabel { get; private init; }
    internal Label GarrisonTitleLabel { get; private init; }
    internal Label ActionTitleLabel { get; private init; }
    internal Label NoticeTitleLabel { get; private init; }

    internal static WorldSitePeacetimeHudNodeRefs Resolve(Control root, string ownerName)
    {
        return new WorldSitePeacetimeHudNodeRefs
        {
            SiteTopBar = Get<Control>(root, "TopBarHost/SiteTopBar", ownerName),
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
            SiteHudTitle = Get<Label>(root, "TopBarHost/SiteTopBar/TopMargin/TopBox/SiteHudTitle", ownerName),
            SiteResourceLabel = Get<Label>(root, "TopBarHost/SiteTopBar/TopMargin/TopBox/SiteResourceLabel", ownerName),
            ReturnMapButton = Get<Button>(root, "TopBarHost/SiteTopBar/TopMargin/TopBox/ReturnMapButton", ownerName),
            SiteHudBody = Get<Label>(root, "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/Scroll/Content/OverviewCard/OverviewMargin/OverviewStack/SiteHudBody", ownerName),
            SiteSelectionLabel = Get<Label>(root, "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/Scroll/Content/OverviewCard/OverviewMargin/OverviewStack/SiteSelectionLabel", ownerName),
            SiteOverviewCard = Get<Control>(root, "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/Scroll/Content/OverviewCard", ownerName),
            SiteFacilityBuildCard = Get<Control>(root, "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/Scroll/Content/BuildCard", ownerName),
            SiteFacilityCard = Get<Control>(root, "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/Scroll/Content/FacilityCard", ownerName),
            SiteDefenseCard = Get<Control>(root, "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/Scroll/Content/DefenseCard", ownerName),
            SiteActionCard = Get<Control>(root, "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/Scroll/Content/ActionCard", ownerName),
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
            SiteFacilityBuildTitle = Get<Label>(root, "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/Scroll/Content/BuildCard/BuildMargin/BuildStack/BuildTitle", ownerName),
            SiteFacilityBuildList = Get<VBoxContainer>(root, "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/Scroll/Content/BuildCard/BuildMargin/BuildStack/SiteFacilityBuildList", ownerName),
            SiteFacilityList = Get<VBoxContainer>(root, "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/Scroll/Content/FacilityCard/FacilityMargin/FacilityStack/SiteFacilityList", ownerName),
            SiteGarrisonList = Get<VBoxContainer>(root, "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/Scroll/Content/DefenseCard/DefenseMargin/DefenseStack/SiteGarrisonList", ownerName),
            SiteActionList = Get<VBoxContainer>(root, "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/Scroll/Content/ActionCard/ActionMargin/ActionStack/SiteActionList", ownerName),
            SiteNoticeLabel = Get<Label>(root, "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/Scroll/Content/ActionCard/ActionMargin/ActionStack/SiteNoticeLabel", ownerName),
            SiteOperationHintLabel = Get<Label>(root, "TopBarHost/SiteTopBar/TopMargin/TopBox/SiteOperationHintLabel", ownerName),
            FacilityTitleLabel = Get<Label>(root, "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/Scroll/Content/FacilityCard/FacilityMargin/FacilityStack/FacilityTitle", ownerName),
            GarrisonTitleLabel = Get<Label>(root, "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/Scroll/Content/DefenseCard/DefenseMargin/DefenseStack/GarrisonTitle", ownerName),
            ActionTitleLabel = Get<Label>(root, "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/Scroll/Content/ActionCard/ActionMargin/ActionStack/ActionTitle", ownerName),
            NoticeTitleLabel = Get<Label>(root, "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/Scroll/Content/ActionCard/ActionMargin/ActionStack/NoticeTitle", ownerName)
        };
    }

    private static T Get<T>(Control root, NodePath path, string ownerName) where T : Node =>
        GameUiSceneFactory.GetRequiredNode<T>(root, path, ownerName);
}
