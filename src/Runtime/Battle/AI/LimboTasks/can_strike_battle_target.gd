@tool
extends BTCondition

@export var target_var: StringName = &"target_id"
@export var ability_var: StringName = &"ability_id"


func _generate_name() -> String:
	return "CanStrikeBattleTarget %s" % [LimboUtility.decorate_var(target_var)]


func _tick(_delta: float) -> Status:
	if agent == null or not agent.has_method("can_strike_battle_target"):
		return FAILURE

	return SUCCESS if agent.can_strike_battle_target(target_var, ability_var, blackboard) else FAILURE
