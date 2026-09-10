# Quests

Quest definitions with status-based progression and tag-based conditions that control which scenarios execute.

## JSON structure

```jsonc
{
  // Wrapping array — all quests are listed under Items
  "Items": [{
    // Unique quest identifier (PascalCase); used in scenario condition expressions
    "Id": "QuestId",
    // Locale key prefix for the quest's display name in the journal
    "Name": "quest/locale/prefix",
    // Maps integer status codes to locale keys for journal descriptions at each stage
    "StatusToDescription": {
      "0": "locale_key_for_status_0",
      "1": "locale_key_for_status_1",
      "2": "locale_key_for_status_2"
    }
  }]
}
```

## Field reference

| Field | Description |
|---|---|
| `Id` | Unique quest identifier (PascalCase). Used in scenario `condition:` expressions |
| `Name` | Locale key prefix for the quest's display name |
| `StatusToDescription` | Map of status integers to locale keys for the journal description at each stage |

## Real examples

### Prologue quest — `core/quests/quests.json`

```jsonc
{
  "Items": [{
    // Quest ID — checked in scenario condition expressions
    "Id": "PrologQuest",
    // Locale key prefix → "Prologue"
    "Name": "quest/prolog",
    // Status descriptions: 0=cold, 1=look around, 2=found recipe book
    "StatusToDescription": {
      "0": "quest/prolog/0",
      "1": "quest/prolog/1",
      "2": "quest/prolog/2"
    }
  }]
}
```

### Meeting Murakami quest — `murakami/quests/quests.json`

```jsonc
{
  "Items": [{
    // Quest ID — used in scenario conditions like MeetingMurakamiQuest.hasTag(...)
    "Id": "MeetingMurakamiQuest",
    // Locale key prefix → "The Girl at the Edge of Town"
    "Name": "quest/meeting/murakami",
    // Status descriptions for each quest stage
    "StatusToDescription": {
      "0": "quest/meeting/murakami/0",
      "1": "quest/meeting/murakami/1",
      "2": "quest/meeting/murakami/2"
    }
  }]
}
```

## Two kinds of status — don't conflate them

A quest carries **two independent state values** (`Core/Quests/`):

| Where | Type | What it means | How it changes |
|---|---|---|---|
| `QuestBase.Status` | `QuestStatus` enum (`byte`) | Quest **lifecycle** — not started / active / finished | `quest.set_status` → `QuestService.SetStatus` |
| `QuestData.Status` (i.e. `quest.Data.Status`) | raw `int` | Numeric **stage counter** used for journal descriptions and fine-grained progress | `quest.set_data_status level=N` → `QuestService.SetDataStatus` |

They are stored and mutated separately. Setting one does not touch the other.

### QuestStatus enum

`QuestStatus` (`Core/Quests/QuestStatus.cs`) is a byte-sized enum with exactly
three values:

| Name | Value | Meaning |
|---|---|---|
| `NotStarted` | 0 | Never accepted or activated; hidden from the journal by default |
| `Progress` | 1 | Accepted, possibly with sub-objectives tracked via tags |
| `End` | 2 | Terminal completed state |

`action quest.set_status` expects the **status names** above as the `status`
parameter; parsing via `Enums<QuestStatus>.Parse` is case-insensitive
(`notstarted`, `Progress`, `END` all work). Invalid names are logged and the
call is ignored.

## Quest tags

Quests use a tag system to track progress. Tags are set via scenario `action` commands:

```scenario
action battle.init scene=town_entry enemies=wolf_2,werewolf_girl_1 quest_id=MeetingMurakamiQuest new_tag=boss_battle
```

This sets the tag `boss_battle` on `MeetingMurakamiQuest` when the battle is initiated.

On the C# side, tags live in a `HashSet<string>` on `QuestBase.Data`
(`Core/Quests/QuestBase.cs`):

| Method | Description |
|---|---|
| `AddTag(tag)` | Adds a milestone tag |
| `RemoveTag(tag)` | Revokes a tag (no-op if absent) |
| `ContainsTag(tag)` | Membership check |
| `NotContainsTag(tag)` | Negated membership check |

Scenario `condition:` expressions map onto these directly: `hasTag(...)` calls
`IQuest.ContainsTag`, `notHasTag(...)` calls `IQuest.NotContainsTag` (resolved
by `QuestMethodCondition`, `Core/Scenes/QuestMethodCondition.cs`).

## Conditional scenarios

Scenarios use quest tags in their `condition:` metadata to decide whether they should run:

```scenario
scene: town_entry
priority: 20
condition: MeetingMurakamiQuest.hasTag("wolf_battle") && MeetingMurakamiQuest.notHasTag("boss_battle")
---
background "town/meetingmurakami/00002-1920617752"
text "[Wolf tracks lead to the door]" key=meeting/after_wolf/tracks
```

This scenario only executes if:
1. The player killed the wolf (`hasTag("wolf_battle")`)
2. The player has NOT yet fought the boss (`notHasTag("boss_battle")`)

## Status progression in practice

The journal reads the current numeric stage (`quest.Data.Status`) and displays
the matching locale key from `StatusToDescription`. Stage advancement happens
via C# or scenario actions:

```scenario
action quest.set_data_status quest_id=PrologQuest level=1
```

Lifecycle changes use the named statuses instead:

```scenario
action quest.set_status quest_id=PrologQuest status=Progress
```

To surface a "new journal entry" banner after changing quest state, call:

```scenario
action quest.send_notification quest_id=PrologQuest
```

Under the hood this routes to `IQuestExtensions.SendNotification(this IQuest)`
(`Core/Quests/IQuestExtensions.cs`), which posts a localized
`ui/journal/new - <quest name>` message to the notification banner queue. It
does not modify quest state.

## Events emitted

Both events are published from `QuestServiceImpl` (`Core/Actions/QuestServiceImpl.cs`)
and reach GDScript mods through the ModEventBus string layer (payload keys are
snake_case versions of the C# properties):

| Event key | Payload | Fired when |
|---|---|---|
| `quest_status_changed` | `quest_id`, `old_status`, `new_status` | `QuestService.SetStatus` succeeds |
| `quest_tag_changed` | `quest_id`, `tag`, `added` (bool) | `QuestService.AddTag` (`added=true`) or `RemoveTag` (`added=false`) |

Note: tag events fire even when the target quest ID doesn't exist (the state
mutation is skipped, the event still publishes).

## Persistence pipeline

`QuestFactory` (`Core/Quests/QuestFactory.cs`) owns quest instances end-to-end:

- `QuestFactory.Instance.Get(id)` — returns the shared runtime `QuestBase`.
  **Throws `KeyNotFoundException`** when the id was never loaded from mod data
  (typically a typo in a scenario reference).
- `QuestFactory.Instance.GetAll()` — snapshot list of every known quest; used
  by the save system to serialize full quest state.
- `QuestFactory.Instance.SetAll(List<QuestBase>)` — save/restore: looks up each
  entry from the save file by ID and overwrites its `Data` (tags + stage) and
  lifecycle `Status`. Empty/null lists are ignored.

Scenario-facing `QuestService` methods wrap the factory with an internal safe-get,
so a missing quest in an action command logs an error instead of crashing the
scenario.

## Locale keys

Quest names and descriptions are localizable:

```properties
quest/prolog = Prologue
quest/prolog/0 = It's cold, need to put something on...
quest/prolog/1 = I don't remember a thing... need to look around.
quest/prolog/2 = Found the recipe book. Need shelter before dark.

quest/meeting/murakami = The Girl at the Edge of Town
quest/meeting/murakami/0 = A wolf attacked a house on the outskirts. Investigate.
quest/meeting/murakami/1 = The girl inside needs help. Something's in the back room.
quest/meeting/murakami/2 = She's safe. Murakami joined the party.
```

## Adding a new quest

1. Create `quests/quests.json` in your mod
2. Define `Id`, `Name`, and `StatusToDescription`
3. Add locale keys for all status descriptions
4. Set tags via `action battle.init new_tag=X` or dedicated quest actions
5. Write scenarios with `condition:` that check quest state
6. Scenarios with higher `priority` take precedence when conditions match

## Quest conditions — full expression reference

See [Conditions](../scenes/scenarios/dsl/conditions/) for the complete condition expression language.
