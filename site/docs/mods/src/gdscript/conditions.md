# Custom Conditions

A GDScript that has both `get_condition_name()` and `evaluate(flags, args)` is registered as a **custom condition leaf parser**. Conditions can be used in the `condition:` metadata field of `.scenario` files.

## Minimal condition

`scripts/weather_check.gd`:
```gdscript
extends RefCounted

func get_condition_name():
    return "gds.is_rain"

func evaluate(flags, args):
    return flags.get("weather", "") == "rain"
```

Usage:
```scenario
scene: town_entry
priority: 5
condition: gds.is_rain
---
text "It's pouring outside."
end
```

## Condition with arguments

Arguments passed in parentheses become integer-keyed entries in the `args` dictionary:

```gdscript
extends RefCounted

func get_condition_name():
    return "gds.flag_equals"

func evaluate(flags, args):
    var expected = args.get("0", "")
    if expected == "":
        return false
    return flags.get("custom_flag", "") == expected
```

```scenario
condition: gds.flag_equals("victory")
```

## Condition parameters

| Parameter | Type | Description |
|---|---|---|
| `flags` | `Dictionary` | Scene flag dictionary (string → string). Populated by `set` DSL commands and action code. |
| `args` | `Dictionary` | Positional arguments from condition call (string index → string value). Empty if no parentheses. |
