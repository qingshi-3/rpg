@tool
extends BTCondition

@export var ability_var: StringName = &"ability_id"


func _generate_name() -> String:
	return "HasRangedBattleAbility %s" % [LimboUtility.decorate_var(ability_var)]


func _tick(_delta: float) -> Status:
	if agent == null or not agent.has_method("has_ranged_battle_ability"):
		return FAILURE

	return SUCCESS if agent.has_ranged_battle_ability(ability_var, blackboard) else FAILURE
