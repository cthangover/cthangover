# Map-owned events

An invisible interaction area belongs directly to an RPG map's `Events` list. It needs no picture, `rpg_object` or `rpg_placement`. Use a placement for a visible chest or person, and a map event for an invisible doorway, story zone or action-key trigger.

```json
{
  "Id": "greeting", "X": 1, "Y": 1, "Width": 2, "Height": 1,
  "Trigger": "Step", "When": "self.done != 1", "Solid": false,
  "Commands": ["text \"Welcome to the meadow.\"", "flag self.done 1", "end"]
}
```

This is one entry inside `"Events": [...]` of an `rpg_map` entity. `Width` and `Height` default to 1; `Trigger` defaults to `Use` (E/F); `Solid` defaults to false. `When` is evaluated in the event's own scope. An optional `Name` is a locale key for the interaction cue. A shared `Scenario` can run before `Commands`; a `Teleport` is an alternative to both.

The event `greeting` on `my_meadow` has the scope `my_meadow/greeting`, so `flag self.done 1` writes `my_meadow/greeting:done`. IDs must be unique across both the map's events and placements. Preserve this identity when moving a former invisible placement into `Events`, so existing flags keep their meaning.

## Authoring with the mouse

In RPG Map Editor choose **Event**, drag the rectangle, choose **When stepped on** or **On use (E / F)**, type the commands, press **Apply event**, then **Save**. Optional settings expose the condition, shared scenario and teleport. Select moves the rectangle; its corner handles resize it in either projection. Undo restores a whole drag as one step.

These events save `X`, `Y`, `Width`, `Height` in the map. Legacy picture-less placements still use `Footprint.Size`; they remain supported but are a different storage format. Collision painting shows both, and `o`/`#` overrides their solidity.

## Extending someone else's map

Declare a dependency on the map's owner, then patch the map in `patches/rpg_map/<your_mod>.json`:

```json
{
  "Items": [{
    "Id": "my_meadow",
    "Events$add": [{
      "Id": "my_mod_exit", "X": 3, "Y": 1,
      "Trigger": "Step", "Teleport": {"Map": "rpg_village", "X": 1, "Y": 8, "Dir": "Right"}
    }]
  }]
}
```

`Events` declares `Id` as its merge key. `$add` therefore updates a matching event or appends a new one; `"Events$remove": ["my_mod_exit"]` removes by identity. It is a map patch, not a separate `rpg_map_event` entity kind. The editor, validator and resource atlas scan event conditions, scripts, shared scenarios and teleport targets.

Implementation: `RpgMap.Events`, `RpgMapEvent`, the RPG interaction runner, and `mods/rpg_editor/src/Document/RpgMapDocument.Events.cs`.
