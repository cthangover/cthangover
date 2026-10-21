# Event Bus

The **ModEventBus** is a global publish-subscribe event bus that allows mods to react
to game events and communicate with each other without direct dependencies.

It is registered as a Godot autoload, accessible from both C# and GDScript.

## Built-in engine events

The engine publishes these events automatically. Each event has a string key
(GDScript layer) and a typed C# class.

### Lifecycle events (ordered by priority)

| Event | Key | Priority | Fires when |
|-------|-----|----------|------------|
| `SceneExitedEvent` | `"scene_exited"` | lifecycle | Leaving the current scene |
| `SceneEnteredEvent` | `"scene_entered"` | lifecycle | New scene loaded, root in tree |
| `BattleStartedEvent` | `"battle_started"` | lifecycle | Battle core initializes *(declared by `turn_battle_base`)* |
| `BattleEndedEvent` | `"battle_ended"` | lifecycle | Battle ends (victory/defeat) *(declared by `turn_battle_base`)* |

Lifecycle events support subscriber **priorities**: lower numbers fire first.
Engine subscribers use these priorities:

| Subscriber | Priority | Purpose |
|------------|----------|---------|
| `SceneManager` internals | 0 | Scene background (`ApplySceneDefaults`) |
| `SceneManager` internals | 5 | Audio (`PlaySceneAmbient`) |
| `SceneManager` internals | 10 | Music (`UpdateMusicType`) |
| `UiHost` | 30 | Shows/hides scene-scoped manifest `ui` elements on enter/exit |
| `SceneContextNode` | 0 (default) | Clears cached node lookups and interactive objects on `scene_exited` |

Note that scenario event composition (`ComposeEvents`) is **not** a bus subscriber — it runs synchronously inside `SceneManager.SwitchScene`.

### Game state events

| Event | Key | Payload |
|-------|-----|---------|
| `CharacterDiedEvent` | `"character_died"` | `character_id`, `character_name`, `source` ("battle"/"world") *(declared by `turn_battle_base`)* |
| `CharactersDefeatedEvent` | `"characters_defeated"` | `source`, `character_ids` — a group defeat (battle won, trap); subscribers like the recruiting mod resolve ids through `CharacterFactory` |
| `QuestStatusChangedEvent` | `"quest_status_changed"` | `quest_id`, `old_status`, `new_status` |
| `QuestTagChangedEvent` | `"quest_tag_changed"` | `quest_id`, `tag`, `added` (bool) |
| `QuestDataStatusChangedEvent` | `"quest_data_status_changed"` | `old`, `new` — numeric quest progress counter; carries no quest id (a change marker for condition caches) |
| `QuestStateRestoredEvent` | `"quest_state_restored"` | (empty) — published once after a save's quest state is fully restored |
| `FlagChangedEvent` | `"flag_changed"` | `key`, `value` — a scenario condition flag changed (`ScenarioCondition.SetFlag`) |
| `InteractiveStateChangedEvent` | `"interactive_state_changed"` | `id` — an interactive object was removed (not published on add, so `interactive_add` re-runs stay idempotent) |
| `MapZoneChangedEvent` | `"map_zone_changed"` | `zone_id` (empty for bulk changes), `change` — a map zone's open/visible state changed |
| `ConditionStateChangedEvent` | `"condition_state_changed"` | `version` — something scenario conditions read changed (any of the quest/flag/interactive/map events above, or `ConditionStateMonitor.MarkStateChanged`); event chains start the next scenario from it. May arrive mid-change: read state in a deferred call, not in the handler |
| `InventoryChangedEvent` | `"inventory_changed"` | `item_id`, `delta`, `new_count` |
| `TimeTickEvent` | `"time_tick"` | `time_tick`, `normalized_time` |
| `NewGameStartedEvent` | `"new_game_started"` | (empty) — published by `GameData.StartNewGame` after runtime state is wiped; mods re-seed per-session data here |
| `RecruitJoinedEvent` | `"recruit_joined"` | `recruit_id`, `recruit_name` *(declared by the `recruiting` mod, not yet published)* |
| `RecruitLeftEvent` | `"recruit_left"` | `recruit_id`, `recruit_name` *(declared by the `recruiting` mod, not yet published)* |

### UI events

| Event | Key | Payload |
|-------|-----|---------|
| `DialogStartedEvent` | `"dialog_started"` | `dialog_id`, `speaker` *(declared, not yet published by the engine)* |
| `DialogEndedEvent` | `"dialog_ended"` | `locker_id` — id of the event (scenario) that ran the dialog, empty for an interactive's click dialog; published exactly once per finished dialog (not on a studio restart). `dialog_id` is declared but not filled yet |
| `NotificationRequestedEvent` | `"notification_requested"` | `text`, `category` |
| `ShellReadyEvent` | `"shell_ready"` | `root` — the UI shell finished building; `UiHost` attaches mod panels on this event |

### Mod lifecycle events

| Event | Key | Payload |
|-------|-----|---------|
| `ModLoadedEvent` | `"mod_loaded"` | `mod_id` |
| `ModResourcesReadyEvent` | `"mod_resources_ready"` | (empty) |

## C# API

### Subscribe to a typed event

```csharp
using Cthangover.Core.Events;

ModEventBus.Subscribe<SceneEnteredEvent>(e =>
{
    GameLogger.Log("MY_MOD", $"Scene entered: {e.SceneName}");
}, priority: 0, modId: "my_mod");
```

- `handler` — callback receiving the typed event.
- `priority` — only meaningful for `[LifecycleEvent]` classes (lower = earlier). Default 0.
- `modId` — optional, used for cleanup via `ClearForMod(modId)` on unload.

### Publish a custom event

```csharp
[EventKey("my_mod:custom_event")]
public class MyCustomEvent : ModEvent
{
    public string Data { get; init; }
}

// Publish:
ModEventBus.Publish(new MyCustomEvent
{
    SenderModId = "my_mod",
    Data = "hello"
});
```

Other mods subscribe by:

```csharp
ModEventBus.Subscribe<MyCustomEvent>(e =>
{
    GameLogger.Log("OTHER_MOD", $"Received: {e.Data}");
});
```

GDScript mods subscribe via the string key `"my_mod:custom_event"`.

### Unsubscribe

```csharp
Action<MyCustomEvent> handler = e => { /* ... */ };
ModEventBus.Subscribe(handler);
// later:
ModEventBus.Unsubscribe(handler);
```

## GDScript API

### Auto-registration (convention-based)

GDScript objects loaded by `ModGDScriptLoader` are automatically scanned for
methods starting with `on_mod_event_`. Each such method is subscribed to the
string event bus under the key after the prefix.

```gdscript
# scripts/my_handler.gd
func on_mod_event_scene_entered(data: Dictionary) -> void:
    print("Entered scene: " + data.scene_name)

func on_mod_event_battle_ended(data: Dictionary) -> void:
    print("Battle ended: " + data.result)

func on_mod_event_cooking_meal_ready(data: Dictionary) -> void:
    if data.dish == "wolf_stew":
        boost_morale()
```

The method receives a `Dictionary` with snake_case keys (e.g. `scene_name`,
`item_id`, `old_status`) matching the typed event properties.

### Manual subscription

```gdscript
ModEventBus.subscribe("scene_entered", _on_scene, 0, "my_mod")

func _on_scene(data: Dictionary) -> void:
    print(data.scene_name)
```

### Publishing from scenario actions

GDScript scenario actions can publish events via `ctx.PublishEvent()`:

```gdscript
func get_name() -> String:
    return "cooking.meal_cooked"

func run(ctx) -> void:
    ctx.PublishEvent("cooking_meal_ready", {
        "dish": ctx.GetParam("dish"),
        "quality": ctx.GetParam("quality")
    })
```

### Publishing directly

```gdscript
ModEventBus.publish("cooking_meal_ready", { "dish": "wolf_stew", "quality": 0.8 })
```

## Error isolation

Each handler is executed in a try/catch. An error in one handler does **not**
prevent other handlers from executing. Errors are logged to the `EVENT_BUS` category.

## Cleanup

When a mod is reloaded or unloaded, its subscriptions are removed:

```csharp
ModEventBus.Instance.ClearForMod("my_mod");
```

This covers both typed (C#) and string (GDScript) subscriptions for that mod.
GDScript mods automatically get `ClearForMod` called on reload via `ModGDScriptLoader`.

## Typed layer reference

The typed C# layer lives in `Core/Events/ModEventBus.cs`. All three methods are static and constrained to `T : ModEvent`:

| Method | Signature | Notes |
|---|---|---|
| `Subscribe<T>` | `Subscribe<T>(Action<T> handler, int priority = 0, string modId = null)` | `priority` is only meaningful for `[LifecycleEvent]` classes (lower = earlier); ignored otherwise |
| `Unsubscribe<T>` | `Unsubscribe<T>(Action<T> handler)` | Removes a single typed handler |
| `Publish<T>` | `Publish<T>(T evt)` | Calls all typed handlers first, then auto-bridges to the string layer |

### Auto-bridge via `[EventKey]`

On `Publish<T>`, the bus reads the event class's `[EventKey("snake_case_key")]` attribute (`Core/Events/EventKeyAttribute.cs`) and broadcasts a `Dictionary` on that key so GDScript subscribers receive the same event. Public instance properties **declared on the event class itself** become snake_case keys (`SceneName` → `scene_name`); inherited `ModEvent` members are not included, and `Node` references pass through unwrapped. An event class without `[EventKey]` does not bridge — only typed C# subscribers receive it.

### `[LifecycleEvent]` and delivery order

Marking an event class with `[LifecycleEvent]` (`Core/Events/LifecycleEventAttribute.cs`) enables priority-ordered delivery: every `Subscribe<T>` call re-sorts that event's subscriber list by `priority`, so lower numbers fire first. Without the attribute, handlers run in subscription order.

### Base class `ModEvent`

All typed events derive from `ModEvent` (`Core/Events/ModEvent.cs`), which carries two members available to every handler:

| Member | Type | Meaning |
|---|---|---|
| `SenderModId` | `string` | Sending mod (`null` = engine) |
| `Tick` | `long` | Game-time tick at publish time (from `GameData.Instance.Runtime.Time`); `0` if `GameData` is not yet initialized |

### Cleanup API

- `ClearForMod(string modId)` — removes all subscriptions registered with that `modId`, across both the typed and string layers. Called on mod unload/reload.
- `ClearAll()` — wipes every subscription on both layers.
