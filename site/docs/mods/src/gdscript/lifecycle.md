# Mod Lifecycle

A GDScript with `on_mod_loaded(modId)` or `on_mod_resources_ready()` is registered as a **mod initializer**. The kernel calls these lifecycle hooks:

```gdscript
extends RefCounted

func on_mod_loaded(mod_id):
    print("[MY_MOD] on_mod_loaded: " + mod_id)

func on_mod_resources_ready():
    print("[MY_MOD] All mod resources are ready")
```

| Hook | Called when |
|---|---|
| `on_mod_loaded(modId)` | Another mod finishes loading. Called for every mod (including itself). |
| `on_mod_resources_ready()` | All mods and their JSON resources are fully loaded. Called once. |

## Event bus auto-registration (`on_mod_event_*`)

GDScript objects are automatically scanned for methods starting with `on_mod_event_`.
Each method is subscribed to the string event bus under the key after the prefix:

```gdscript
# scripts/quest_watcher.gd
func on_mod_event_quest_status_changed(data: Dictionary) -> void:
    if data.quest_id == "main_quest" and data.new_status == "completed":
        print("Quest completed!")

func on_mod_event_battle_ended(data: Dictionary) -> void:
    if data.result == "victory":
        heal_party()
```

The method receives a `Dictionary` with `snake_case` keys matching the event's
typed C# properties. See [Events](../../events/) for the full list of built-in events
and their payloads.

## Publishing events from scenario actions

Scenario action scripts can publish events via `ctx.PublishEvent()`:

```gdscript
func get_name() -> String:
    return "cooking.meal_cooked"

func run(ctx) -> void:
    ctx.PublishEvent("cooking_meal_ready", {
        "dish": ctx.GetParam("dish"),
        "quality": ctx.GetParam("quality")
    })
```

Other mods (C# or GDScript) can subscribe to this event by its string key.
See [Events](../../events/) for the full event bus API.
