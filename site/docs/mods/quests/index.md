# Quests

Quest definitions with status-based progression and tag-based conditions that control which scenarios execute.

## JSON structure

```jsonc
{
  // Entity kind — routes the file to the quest collector
  "Kind": "quest",
  // All quests of the file are listed under Items
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

### Awakening quest — `cthangover_game/quests/quests.json`

```jsonc
{
  "Kind": "quest",
  "Items": [{
    // Quest ID — checked in scenario condition expressions
    "Id": "AwakeningQuest",
    // Locale key prefix → "Awakening"
    "Name": "quest/awakening",
    // One journal entry per prologue chapter; the journal strikes through
    // past entries and highlights the current `quest.Data.Status`
    "StatusToDescription": {
      "0": "quest/awakening/0",
      "1": "quest/awakening/1",
      "2": "quest/awakening/2",
      "3": "quest/awakening/3",
      "4": "quest/awakening/4",
      "5": "quest/awakening/5"
    }
  }]
}
```

### Old House quest — `cthangover_game/quests/quests.json`

Opened when the hero reaches the villa; the house arc is not written yet, so
the quest deliberately has no `StatusToDescription` — the journal shows its
name with the "pages are still empty" placeholder:

```jsonc
{
  "Kind": "quest",
  "Items": [{
    "Id": "OldHouseQuest",
    "Name": "quest/old_house"
  }]
}
```

### Meeting Murakami quest — `murakami/quests/quests.json`

```jsonc
{
  "Kind": "quest",
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
action quest.set_data_status quest_id=AwakeningQuest level=3
```

Lifecycle changes use the named statuses instead:

```scenario
action quest.set_status quest_id=AwakeningQuest status=End
```

To surface a "new journal entry" banner after changing quest state, call:

```scenario
action quest.send_notification quest_id=OldHouseQuest
```

Under the hood this routes to `IQuestExtensions.SendNotification(this IQuest)`
(`Core/Quests/IQuestExtensions.cs`), which posts a localized
`ui/journal/new - <quest name>` message to the notification banner queue. It
does not modify quest state.

## Events emitted

`quest_status_changed` is published from `QuestServiceImpl.SetStatus`
(`Core/Actions/QuestServiceImpl.cs`); `quest_tag_changed` is published from
`QuestBase.AddTag` / `QuestBase.RemoveTag` (`Core/Quests/QuestBase.cs`) — and
only when the tag set actually changed. Both reach GDScript mods through the
ModEventBus string layer (payload keys are snake_case versions of the C#
properties):

| Event key | Payload | Fired when |
|---|---|---|
| `quest_status_changed` | `quest_id`, `old_status`, `new_status` | `QuestService.SetStatus` succeeds |
| `quest_tag_changed` | `quest_id`, `tag`, `added` (bool) | `QuestService.AddTag` (`added=true`) or `RemoveTag` (`added=false`) |

Note: a quest action with an unknown quest ID does nothing — the safe `TryGet`
inside `QuestServiceImpl` logs the miss and skips the mutation, so no event is
published for a quest that doesn't exist.

## Persistence pipeline

`QuestFactory` (`Core/Quests/QuestFactory.cs`) owns quest instances end-to-end:

- `QuestFactory.Instance.Get(id)` — returns the shared runtime `QuestBase`.
  **Throws `KeyNotFoundException`** when the id was never loaded from mod data
  (typically a typo in a scenario reference).
- `QuestFactory.Instance.GetAll()` — snapshot list of every known quest; used
  by the save system to serialize full quest state.
- `QuestFactory.Instance.SetAll(List<QuestBase>)` — save/restore: looks up each
  entry from the save file by ID and overwrites its `Data` (tags + stage) and
  lifecycle `Status`. Empty/null lists are ignored; entries whose quest no
  longer exists (removed mod, renamed quest) are skipped with a warning so an
  ageing save cannot block loading.
- `QuestFactory.Instance.ResetAll()` — returns every quest to `NotStarted` with
  an empty tag set. Called by `GameData.StartNewGame` because the factory
  singleton survives returning to the main menu and would otherwise leak the
  previous playthrough's progress into the new one.

Scenario-facing `QuestService` methods wrap the factory with an internal safe-get,
so a missing quest in an action command logs an error instead of crashing the
scenario.

## Locale keys

Quest names and descriptions are localizable:

```properties
quest/awakening = Awakening
quest/awakening/0 = Woke up naked in an alley. My head is splitting. Look around — find something to wear.
quest/awakening/1 = Covered myself with a rag and grabbed the book. Then the police showed up. No point arguing — run.
quest/awakening/2 = The whistles died down. I'm at the cemetery. Catch my breath and figure out what to do with the book.
quest/awakening/3 = Read it aloud. Pretty sure I did something. Just what exactly?
quest/awakening/4 = The book started talking. Calls itself Al Azif. Time to get out of here.
quest/awakening/5 = Walking along the river to the villa. I'll wait it out somewhere there.
quest/old_house = The Old House

quest/meeting/murakami = The Girl at the Edge of Town
quest/meeting/murakami/0 = A wolf attacked a house on the outskirts. Investigate.
quest/meeting/murakami/1 = The girl inside needs help. Something's in the back room.
quest/meeting/murakami/2 = She's safe. Murakami joined the party.
```

### Tag locale keys

Tags displayed as journal stamps are resolved through a composite key built
from the quest's `Name` prefix and the tag itself: `{quest.Name}/tag/{tag}`.
For `MeetingMurakamiQuest` (`Name` is `quest/meeting/murakami`) the tag
`go_home` maps to `quest/meeting/murakami/tag/go_home`, so same-named tags in
different quests can carry different translations:

```properties
quest/meeting/murakami/tag/wolf_battle = Wolves defeated
quest/meeting/murakami/tag/boss_battle = Werewolf defeated
quest/meeting/murakami/tag/go_home = Escort home
```

When the composite key is missing, the journal falls back to the raw tag id —
a tag without locale entries still appears as a stamp.


## Adding a new quest

1. Create a JSON file in your mod with the `{ "Kind": "quest", "Items": [ ... ] }` envelope (e.g. `quests/quests.json`)
2. Define `Id`, `Name`, and `StatusToDescription`
3. Add locale keys for all status descriptions
4. Set tags via `action battle.init new_tag=X` or dedicated quest actions
5. Write scenarios with `condition:` that check quest state
6. Scenarios with higher `priority` take precedence when conditions match

## Quest conditions — full expression reference

See [Conditions](../scenes/scenarios/dsl/conditions/) for the complete condition expression language.
