# Scenario Actions

The `action` command bridges the DSL to game systems. Actions can be implemented in **C#** (via `IScenarioAction` + Roslyn compilation) or in **GDScript** (via `get_name()` + `run(ctx)` auto-detection).

## Syntax

```scenario
action namespace.action_name param1=value1 param2=value2 ...
```

Parameters use `key=value` syntax (no spaces around `=`).

## Real examples

### Character actions

```scenario
action character.add_to_party type=Murakami
```

Adds the character `Murakami` to the player's party. `type=` matches the character's `Id` field in their JSON definition.

```scenario
action character.send_notification type=Murakami
```

Triggers the "new party member" UI notification for character `Murakami`.

### Battle initiation

```scenario
action battle.set_core core=CardBattle
action battle.init scene=town_entry enemies=wolf_2,werewolf_girl_1 quest_id=MeetingMurakamiQuest new_tag=boss_battle
```

`battle.set_core` selects the combat ruleset (e.g. `CardBattle`, `FFBattle`) before `battle.init` starts the encounter.

| Parameter | Description |
|---|---|
| `scene` | Scene to return to after battle |
| `enemies` | Comma-separated list of enemy character IDs |
| `quest_id` | Quest to update on battle outcome |
| `new_tag` | Tag to set on the quest when the battle starts |

### Cooking workbench

```scenario
action ui.toggle_panel name=CookingPanel
```

Toggles a named UI panel. The action name alone is enough — no extra parameters required.

## Available action namespaces

### `character.*`

| Action | Parameters | Description |
|---|---|---|
| `character.add_to_party` | `type=CharId` | Add character to player's party |
| `character.send_notification` | `type=CharId` | Show "new member" notification |

### `battle.*`

| Action | Parameters | Description |
|---|---|---|
| `battle.init` | `scene`, `enemies`, `quest_id`, `new_tag` | Start a battle |
| `battle.set_core` | `core=CardBattle` | Select combat ruleset |

### `quest.*`

| Action | Parameters | Description |
|---|---|---|
| `quest.set_status` | `quest_id=Q status=Progress` | Set the quest lifecycle state by `QuestStatus` enum name: `NotStarted`, `Progress`, or `End` |
| `quest.set_data_status` | `quest_id=Q level=3` | Set the quest numeric progress counter |
| `quest.add_tag` | `quest_id=Q tag=T` | Add a tag to a quest |
| `quest.remove_tag` | `quest_id=Q tag=T` | Remove a tag |
| `quest.send_notification` | `quest_id=Q` | Trigger "Quest Updated" UI popup |

> **Note:** The `status` parameter is parsed as a case-insensitive `QuestStatus` **enum name** (`NotStarted` / `Progress` / `End`) via `Enums<QuestStatus>.Parse`. Use enum names in scenarios. Numeric strings such as `status=2` only happen to work because `Enum.Parse` aliases numbers to enum values — do not rely on this. For numeric progress use `quest.set_data_status` with `level=N` instead.

### `scene.*`

| Action | Parameters | Description |
|---|---|---|
| `scene.instantiate` | `path=res://scenes/ui/CookingPanel.tscn name=CookingNode` | Load and add a PackedScene to the scene tree |
| `scene.remove_object` | `name=CookingNode` | Remove a named node from the scene tree |

### `ui.*`

| Action | Parameters | Description |
|---|---|---|
| `ui.toggle_panel` | `name=PanelName` | Toggle visibility of a Control node by name |

### `lighting.*`

| Action | Parameters | Description |
|---|---|---|
| `lighting.clear_map` | *(none)* | Reset lighting depth/albedo maps |

## Adding a custom action

Define an action class in your mod's `src/`:

```csharp
public class OpenTreasureChestAction : IScenarioAction
{
    public string Name => "treasure.open";

    public void Run(IActionContext ctx)
    {
        var item = ctx.GetParam("item");
        var count = ctx.GetParam("count");
        ctx.Log("SCENARIO", $"Gave player {count}x {item}");
    }
}
```

`IActionContext` exposes `GetParam(string)` for reading action parameters plus the game subsystem services and `Log`/`LogWarning` methods (see `Core/Actions/IActionContext.cs`). There is no typed getter such as `GetParamInt` — parse values yourself.

### Registration

You do **not** need to register actions manually: every implementation of `IScenarioAction` in your mod's compiled assembly is discovered automatically by reflection when the assembly is loaded.

If you need to register an assembly manually (outside the standard mod loading flow), call:

```csharp
ScenarioActionFactory.Instance.RegisterAssembly(assembly);
```

If two actions register the same `Name`, the earliest registration wins and later duplicates are silently ignored.

Then use in scenarios:

```scenario
action treasure.open item=food/ration count=5
```

### From GDScript (no Roslyn needed)

Create `scripts/my_actions.gd`:

```gdscript
extends RefCounted

func get_name():
    return "my.quest_activate"

func run(ctx):
    var questId = ctx.GetParam("quest_id")
    ctx.Quests.SetStatus(questId, "Active")
    ctx.Quests.SendNotification(questId)
    ctx.Log("EVENT", "Quest " + questId + " activated via GDScript")
```

Declare in `manifest.json`:

```jsonc
{
  "name": "My GDScript Mod",                               // Human-readable mod name
  "gd_sources": ["scripts/*.gd"]                           // Glob patterns for .gd files — auto-discovered for actions, conditions, etc.
}
```

The kernel auto-discovers `get_name()` + `run()` and registers the action — no factory or provider code needed. The `ctx` parameter provides access to all game subsystems (Quests, Character, Battle, Inventory, etc.). See [GDScript in Mods](site/docs/mods/src/gdscript) for the full action context API.
