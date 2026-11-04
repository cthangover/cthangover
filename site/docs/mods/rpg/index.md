# RPG maps, objects and events

`rpg` supplies a grid world over `shell2d_base`. A map is a logical scene: ordinary dialogs, scene transitions, music and saves still work. WASD or arrows walk, Shift runs, and E/F uses the object or event the player faces. Party members follow the leader's trail and gather after a teleport. The leader's `Charset` supplies the walking picture.

This is model v2. Pictures are individual PNGs in `rpg_images/`; maps do not use a tileset atlas. The separate world-map travel overlay is described in [Map & Travel Zones](../map/).

## A minimal map

Put this entity in `maps/meadow.json` in your own mod. Declare `rpg>=1`, a tile renderer and `village_pack>=1` in `depends` for this example.

```json
{
  "Kind": "rpg_map",
  "Items": [{
    "Id": "my_meadow", "Name": "my_game/meadow/name",
    "Width": 4, "Height": 3,
    "Legend": {"g": "village_pack/ground/grass"},
    "Ground": ["gggg", "gggg", "gggg"],
    "Collision": ["....", "....", "...."],
    "Spawn": {"X": 1, "Y": 1, "Dir": "Down"}
  }]
}
```

Also supply a scene definition with `"name": "my_meadow"`, following [Scenes](../scenes/), and select this scene as the collection entry or enter it with `switch_scene my_meadow`. Map IDs contain no `/`.

| Map field | Meaning |
|---|---|
| `Width`, `Height` | Cell dimensions |
| `TileSize`, `PatchSize` | Picture pixels per cell (default 32) and surface material patch size (128) |
| `Zoom` | View zoom, default 2 |
| `Legend` | One character to an `rpg_object` ID; `.` is empty |
| `Ground`, `Structure` | Rows of surface and segment legend codes respectively |
| `Collision` | `.` uses object collision, `#` blocks, `o` forces passage even through a solid object |
| `Strokes` | Ordered cell edits over the base rows; entries merge by `Id` |
| `Spawn` | Default `X`, `Y`, `Dir` |
| `Variant` | Default object variant for this map |
| `Ambient`, `LightMode`, `Lights` | Fixed ambient colour, `cycle`/`day`/`night`, and map lights (radius in cells) |
| `Atmosphere`, `Effects` | Whole-map profile and additional profiles or painted cell masks; see [Effects](../effects/) |
| `Events` | Map-owned interaction rectangles; see [Map events](events.md) |
| `Encounters` | Read by the optional battles integration, rather than by the grid world |

## Objects and placements

An object is an entity of kind `rpg_object`, normally in `objects/`. A placement is an entity of kind `rpg_placement`, normally in `placements/<map>/`. Its ID is `<map>/<local_id>`.

| `Form` | Use |
|---|---|
| `surface` | Ground material through `Surface` |
| `segment` | Joined wall or fence cells through `Segment` |
| `sprite` | One or more pictures in `Parts`, with a multi-cell `Footprint` |
| `actor` | A 1×1 character using `Charset`, `Facing`, `Column` and an optional `Route` |

```json
{
  "Kind": "rpg_placement",
  "Items": [{
    "Id": "my_meadow/chest",
    "Object": "village_pack/chest", "X": 2, "Y": 1,
    "Interact": {"Trigger": "Use", "Commands": [
      "text \"There is a note inside.\"", "flag self.opened 1", "end"
    ]}
  }]
}
```

`Solid` overrides the form default (surfaces are walkable; other forms are solid). `Footprint.Size` is `[width,height]`; optional `Footprint.Solid` rows use `#` for blocked cells. `Hidden` removes drawing, collision and interaction. Explicit map collision codes take precedence.

`Parts` entries can have `Id`, `Image` (relative to `rpg_images/`, without extension), pixel `Crop`, cell `At` and `Size`, `Geometry`, projection restriction `Only`, `DrawOrder` and `SortOffset`. Geometry supports upright pictures, ground pictures, planes and explicit quads. `Anchor` is the object's sorting and standing point in cells. The object editor includes a visual parts workspace for movement, resizing and depth checks with a reference character.

Objects support `Extends` and named `Variants`. A placement's overrides merge over the object and its selected variant. Ordered `States` choose the last matching `When`; supplied state components replace their corresponding component. For example, a state can hide a defeated enemy or remove a locked door's teleport. `self.opened` belongs to this placement, not every copy of the chest: it resolves to the story flag `my_meadow/chest:opened`.

`Interact` accepts `Scenario` and/or inline `Commands` (scenario first, commands afterwards), or a standalone `Teleport` with `Map`, `X`, `Y`, `Dir`. This allows a shared character dialog followed by `party.add`. An `end` in the shared scenario does not discard the appended interaction commands.

## Battles and teleporting

`rpg` has no dependency on `turn_battle_base`. When both are loaded, the integration `rpg__battles` supplies random encounters and `rpg.battle`:

```scenario
action rpg.battle enemies=rpg_wolf,rpg_slime win_flag=my_wolf_dead
end
```

Victory sets the named flag to 1 and returns to the saved map cell. Random battles use map data such as `"Encounters": {"Steps": 48, "Groups": ["rpg_slime", "rpg_slime,rpg_slime"]}`; 0 steps disables them. No battle mod means no random battles. Teleports belong to `rpg` itself:

```scenario
action rpg.teleport map=rpg_village x=1 y=8 dir=right
end
```

## Projection, editing and saving

`tile_render_base` consumes the prepared world. `tile_topdown_base` draws squares from above; `tile_isometric_base` draws 2:1 diamonds. Set the collection setting `tile.renderer` to `topdown` or `isometric`, or override it with `--tile-renderer=isometric`. This changes presentation, not map coordinates or event identities.

The RPG Map Editor paints grids, collision and effect masks; places objects and characters; edits interactions and map events; supports rectangle selection, copy/paste, undo/redo, spawn and teleport targets; and can play from the chosen cell. The RPG Object Editor edits the reusable definitions, parts, states, variants and PNG imports. Both use the game's renderer. Changes to a dependency are saved as patches in the chosen writable mod.

The `rpg` save slot owns `Position`; story flags, including `self.` values, are in the core `Flags` blob. Keep placement and event IDs stable across updates. See [Save data](../save/data-model.md) and [Developer tools](../editor_tools/).

Implementation: `mods/rpg/src/Model/`, `mods/rpg/src/Play/`, `mods/rpg/src/State/`, `mods/rpg/integrations/battles/`, and the three `tile_*_base` mods.
