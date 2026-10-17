extends RefCounted

# Custom battle action executor: deals triple damage based on the user's Attack.
# ActionId must match the battle action ID in the character's JSON data.
# The execute method receives plain dictionaries (action/user/target) and must
# return a dictionary with any of: source_damage, source_defence, target_damage,
# target_defence, result.

func get_action_id():
	return "gds.triple_attack"

func execute(action, user, target):
	var damage = user["attack_value"] * 3 - target["defence_value"]
	if damage < 1:
		damage = 1

	return {
		"target_damage": damage,
		"target_defence": target["defence_value"],
		"result": true,
	}
