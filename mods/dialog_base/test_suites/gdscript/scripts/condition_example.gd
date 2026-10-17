extends RefCounted

# Custom condition: checks if the "weather" flag equals "rain"
# Usage in scenario: condition: gds.is_rain
# Or: condition: gds.is_rain && Quest.hasTag("active")

func get_condition_name():
	return "gds.is_rain"

func evaluate(flags, args):
	return flags.get("weather", "") == "rain"
