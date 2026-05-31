using Rpg.Definitions.Battle.Abilities;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Battle.Intents;

namespace Rpg.Presentation.Battle.AI;

public sealed class BattleAiDecisionResult
{
    public BattleAiDecisionResult(BattleIntentTemplate template, int power, string reason = "", BattleAiDecisionFacts facts = null)
    {
        Template = template ?? BattleIntentTemplates.Hold;
        Power = System.Math.Max(0, power);
        Reason = reason ?? "";
        if (facts?.HasLocalCombatObservation == true)
        {
            HasLocalCombatObservation = true;
            LocalCombatOwnerBattleGroupId = facts.LocalCombatOwnerBattleGroupId ?? "";
            LocalCombatRegionId = facts.LocalCombatRegionId ?? "";
            LocalCombatTargetActorId = facts.LocalCombatTargetActorId ?? "";
            LocalCombatCenterCellX = facts.LocalCombatCenterCellX;
            LocalCombatCenterCellY = facts.LocalCombatCenterCellY;
            LocalCombatCenterCellHeight = facts.LocalCombatCenterCellHeight;
            LocalCombatWidth = System.Math.Max(1, facts.LocalCombatWidth);
            LocalCombatHeight = System.Math.Max(1, facts.LocalCombatHeight);
            LocalCombatVersion = facts.LocalCombatVersion;
            LocalCombatSelectedSlotKind = facts.LocalCombatSelectedSlotKind ?? "";
            LocalCombatSelectedSlotRole = facts.LocalCombatSelectedSlotRole ?? "";
            LocalCombatSelectedSlotCellX = facts.LocalCombatSelectedSlotCellX;
            LocalCombatSelectedSlotCellY = facts.LocalCombatSelectedSlotCellY;
            LocalCombatSelectedSlotCellHeight = facts.LocalCombatSelectedSlotCellHeight;
            LocalCombatReasonCode = facts.LocalCombatReasonCode ?? "";
        }
    }

    public BattleIntentTemplate Template { get; }
    public string TemplateId => Template.Id;
    public int Power { get; }
    public string Reason { get; }
    public bool HasLocalCombatObservation { get; }
    public string LocalCombatOwnerBattleGroupId { get; } = "";
    public string LocalCombatRegionId { get; } = "";
    public string LocalCombatTargetActorId { get; } = "";
    public int LocalCombatCenterCellX { get; }
    public int LocalCombatCenterCellY { get; }
    public int LocalCombatCenterCellHeight { get; }
    public int LocalCombatWidth { get; } = 1;
    public int LocalCombatHeight { get; } = 1;
    public int LocalCombatVersion { get; }
    public string LocalCombatSelectedSlotKind { get; } = "";
    public string LocalCombatSelectedSlotRole { get; } = "";
    public int LocalCombatSelectedSlotCellX { get; }
    public int LocalCombatSelectedSlotCellY { get; }
    public int LocalCombatSelectedSlotCellHeight { get; }
    public string LocalCombatReasonCode { get; } = "";

    public BattleIntent ToIntent(BattleEntity actor, AbilityDefinition preferredAbility)
    {
        AbilityDefinition ability = Template == BattleIntentTemplates.Hold ? null : preferredAbility;
        return Template.Create(actor, ability, Power, Reason);
    }
}
