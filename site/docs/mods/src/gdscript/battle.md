# Battle Action Executors

A GDScript that has both `get_action_id()` and `execute(action, user, target)` is registered as a **battle action executor**. The `action_id` must match an `ActionCharacter.ID` in the character's JSON data.

Recognition and registration happen in the turn-battle mod: `TurnBattleGDScriptHook` is installed into the core's `GDScriptHookRegistry`, which `ModGDScriptLoader` offers every compiled `.gd` file. A script that returns a dictionary from `execute` is wrapped in `GDBattleActionExecutor` and added to the global `ActionExecutorHub` pool.

## Executor contract

Arguments are plain Godot dictionaries — mod-authored C# classes get no Godot source generation, so custom RefCounted DTOs would be invisible to GDScript.

```gdscript
extends RefCounted

func get_action_id():
    return "my.custom_attack"

func execute(action, user, target):
    var damage = user["attack_value"] * 2 - target["defence_value"]
    if damage < 1:
        damage = 1

    return {
        "target_damage": damage,
        "target_defence": target["defence_value"],
        "result": true,
    }
```

## Parameters

| Parameter | Type | Description |
|---|---|---|
| `action` | `Dictionary` | The ActionCharacter definition (`id`, `name`, `description`, `type`, `properties`) |
| `user` | `Dictionary` | The character performing the action |
| `target` | `Dictionary` | The character receiving the action |

## `action` fields

| Field | Type |
|---|---|
| `id` | `String` |
| `name` | `String` |
| `description` | `String` |
| `type` | `int` |
| `properties` | `Dictionary` (string → string, the raw `PropertyData` bag) |

## Character fields

| Field | Type | Description |
|---|---|---|
| `id` | `String` | Character identifier |
| `name` | `String` | Display name |
| `level` | `int` | Current level |
| `exp` | `int` | Experience points |
| `recruitment_chance` | `int` | Recruitment chance (0-100) |
| `health_value` / `health_max` | `int` | Current / max health |
| `defence_value` / `defence_max` | `int` | Current / max defence |
| `attack_value` / `attack_max` | `int` | Current / max attack |
| `point_value` / `point_max` | `int` | Current / max action points |
| `properties` | `Dictionary` | Raw string properties (`Speed`, `StunImmune`, …) |

## Return value

`execute` must return a `Dictionary`. Missing keys default to zero; `result` defaults to `true`, so a script that only reports deltas counts as a successful action.

| Key | Type | Description |
|---|---|---|
| `source_damage` | `int` | Damage dealt by the source |
| `source_defence` | `int` | Source defence delta |
| `target_damage` | `int` | Damage dealt to the target |
| `target_defence` | `int` | Target defence delta |
| `result` | `bool` | Whether the action succeeded (default `true`) |

A script that returns a non-dictionary value yields a failed result (`Result = false`), so one broken mod cannot abort the action pipeline.
