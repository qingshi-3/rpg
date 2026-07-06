@tool
extends BTAction

@export var template_id: StringName = &"hold"
@export var power_var: StringName = &"intent_power"
@export var reason: String = ""


func _generate_name() -> String:
	return "EmitBattleIntent %s" % [template_id]


func _tick(_delta: float) -> Status:
	if agent == null or not agent.has_method("emit_battle_intent"):
		return FAILURE

	return SUCCESS if agent.emit_battle_intent(template_id, power_var, reason, blackboard) else FAILURE
