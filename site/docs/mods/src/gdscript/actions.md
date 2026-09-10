# Scenario Actions

A GDScript that has both `get_name()` and `run(ctx)` is registered as a **scenario action**. The `ctx` parameter is a `GDActionContext` providing access to all game subsystems.

## Minimal action

`scripts/greet.gd`:
```gdscript
extends RefCounted

func get_name():
    return "my.hello"

func run(ctx):
    ctx.Log("EVENT", "Hello from GDScript!")
```

Usage in `.scenario`:
```scenario
action my.hello
```

## Action with parameters

```gdscript
extends RefCounted

func get_name():
    return "my.announce"

func run(ctx):
    var name = ctx.GetParam("name")
    var count = ctx.GetParam("count")
    if name == "":
        name = "Unknown"
    ctx.Log("EVENT", "Announcing: " + name + " x" + count)
```

```scenario
action my.announce name=Marao count=3
```

## Action context (GDActionContext) API

The `ctx` object provides these properties and methods:

| Member | Type | Description |
|---|---|---|
| `GetParam(name)` | `string` | Read a parameter passed via `key=value` in the DSL |
| `Log(category, msg)` | `void` | Debug-level log |
| `LogWarning(category, msg)` | `void` | Warning-level log |
| `Quests` | `GDQuestService` | Full quest subsystem API |
| `Character` | `GDCharacterService` | Character party management |
| `Battle` | `GDBattleService` | Battle initiation |
| `Lighting` | `GDLightingService` | Lighting/depth map control |
| `Scene` | `GDSceneNodeService` | Scene node instantiation/removal |
| `Inventory` | `GDInventoryService` | Player inventory queries and mutations |
| `ModRegistry` | `GDModRegistryService` | Loaded mod introspection |
| `PublishEvent(key, data)` | `void` | Publish an event to the global event bus (see [Events](../../events/)) |

## Service API reference

### GDQuestService

| Method | Description |
|---|---|
| `Exists(id)` | Check if a quest is registered |
| `SetStatus(id, status)` | Set quest lifecycle state (e.g. `"Active"`, `"Completed"`) |
| `SetDataStatus(id, level)` | Set numeric progress counter |
| `AddTag(id, tag)` | Attach a tag for conditional logic |
| `RemoveTag(id, tag)` | Remove a tag |
| `SendNotification(id)` | Trigger "Quest Updated" UI popup |

```gdscript
func run(ctx):
    var qid = ctx.GetParam("quest_id")
    if ctx.Quests.Exists(qid):
        ctx.Quests.SetStatus(qid, "Active")
        ctx.Quests.AddTag(qid, "started_by_gd")
        ctx.Quests.SendNotification(qid)
```

### GDCharacterService

| Method | Description |
|---|---|
| `AddToParty(type)` | Add a character to the player's party |
| `SendNotification(type)` | Show "new party member" UI popup |

### GDBattleService

| Method | Description |
|---|---|
| `Init(sceneType, enemies, questId, newTag)` | Construct battle data and prepare encounter |

Pass empty strings `""` for `questId`/`newTag` to omit them.

### GDLightingService

| Method | Description |
|---|---|
| `ClearDepthMap()` | Reset lighting depth/albedo maps |
| `SetUseTime(useTime)` | Enable/disable time-of-day lighting |

### GDSceneNodeService

| Method | Description |
|---|---|
| `Instantiate(scenePath, nodeName)` | Load a PackedScene and add to scene tree |
| `Remove(nodeName)` | Remove and free a named node |

### GDInventoryService

| Method | Description |
|---|---|
| `HasItem(id)` | Check if item exists in inventory |
| `CheckCount(id)` | Get stack count of an item |
| `Add(id, count)` | Add items (default count=1) |
| `Remove(id, count)` | Remove items (returns `true` on success) |

### GDModRegistryService

| Method | Description |
|---|---|
| `IsLoaded(modId)` | Check if a mod is loaded |
| `GetOrderedModIds()` | Get ordered list of loaded mod IDs |

## Full action example

```gdscript
extends RefCounted

func get_name():
    return "my.quest_and_char"

func run(ctx):
    var quest = ctx.GetParam("quest_id")
    var hero = ctx.GetParam("hero")

    ctx.Quests.SetStatus(quest, "Active")
    ctx.Quests.AddTag(quest, "gds_managed")
    ctx.Quests.SendNotification(quest)

    ctx.Character.AddToParty(hero)
    ctx.Character.SendNotification(hero)

    if ctx.ModRegistry.IsLoaded("core"):
        ctx.Log("EVENT", "Core is loaded, all good")
```
