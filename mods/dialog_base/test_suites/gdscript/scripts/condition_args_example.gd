extends RefCounted

# Custom condition with arguments: checks if a flag equals the passed value
# Usage in scenario: condition: gds.flag_equals("expected_value")

func get_condition_name():
	return "gds.flag_equals"

func evaluate(flags, args):
	var expected = args.get("0", "")
	if expected == "":
		return false
	return flags.get("custom_flag", "") == expected
