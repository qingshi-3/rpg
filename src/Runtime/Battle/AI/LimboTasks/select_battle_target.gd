@tool
extends BTAction

@export_enum("nearest_hostile", "lowest_health_hostile") var mode: String = "nearest_hostile"
@export var target_var: StringName = &"target_id"


func _generate_name() -> String:
	return "SelectBattleTarget %s -> %s" % [mode, LimboUtility.decorate_var(target_var)]


func _tick(_delta: float) -> Status:
	if agent == null or not agent.has_method("select_battle_target"):
		return FAILURE

	return SUCCESS if agent.select_battle_target(mode, target_var, blackboard) else FAILURE
