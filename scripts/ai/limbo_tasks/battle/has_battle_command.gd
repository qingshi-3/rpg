@tool
extends BTCondition

@export var expected_command: String = "Assault"


func _generate_name() -> String:
	return "HasBattleCommand %s" % [expected_command]


func _tick(_delta: float) -> Status:
	if agent == null or not agent.has_method("has_battle_command"):
		return FAILURE

	return SUCCESS if agent.has_battle_command(expected_command, blackboard) else FAILURE
