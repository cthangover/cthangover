# Map and Travel Zones

The world map is a full-screen overlay where the player picks a destination and travels there. A map is a set of **zones**: interactive areas drawn over the map background, each pointing at a scene. Zone geometry lives in mod JSON data, while the open/hidden state is runtime state that scenarios change through dedicated DSL actions and the save system persists.

## Directory structure

Map data is read from every loaded mod, in mod load order:

```filestree
mods/<mod_name>/
└── map/
    └── map.json        # any *.json in this folder is loaded
```

The base mod ships one file:

```filestree
mods/cthangover_game/
└── map/
    └── map.json        # "home" and "town_entry" zones + two labels
```

## Zone data format

Files use the standard `{"Items": [...]}` envelope. All coordinates are normalised `[0.0 .. 1.0]` against the displayed map background, which keeps layouts resolution-independent:

```jsonc
{
  "Items": [
    {
      "ID": "home",                         // unique zone id (also the DSL reference)
      "points": [[0.58, 0.26], [0.61, 0.25]], // polygon highlight + click area
      "title": "",                          // localisation key for the name pill
      "scene": "home_outside",              // target scene, passed to SceneManager
      "description": "mapzone/home/info",   // optional localisation key for the card text
      "image": "",                          // asset id of the marker icon (empty = no marker)
      "image_pos": [0.6, 0.2],              // normalised marker position
      "label_pos": [0.59, 0.15],            // optional free-standing text label position
      "locked": false,                      // authoring default: closed for travel
      "hidden": false                       // authoring default: absent from the map
    }
  ]
}
```

| Field | Type | Description |
|---|---|---|
| `ID` | `string` | Unique zone identifier; the key used by DSL actions and the save |
| `points` | `[x, y][]` | Polygon vertices, normalised `[0..1]`; drawn as a two-tone hatch and used as the generous click target (fewer than 3 points = no polygon) |
| `title` | `string` | Localisation key resolved via `TranslationServer.Translate` |
| `scene` | `string` | Logical scene name for the transition; empty or unknown scenes keep the zone closed (dimmed marker, blocked button) |
| `description` | `string` | Optional localisation key for the location card flavour text |
| `image` | `string` | Marker icon asset id resolved through the mod texture system |
| `image_pos` | `[x, y]` | Normalised marker position |
| `label_pos` | `[x, y]` | Optional position of a non-interactive text label |
| `locked` | `bool` | Authoring default for the closed state (`false` = enterable) |
| `hidden` | `bool` | Authoring default for visibility (`false` = drawn) |

### Zone kinds

- **Location** — has an `image` icon and a `scene`: clickable marker, polygon, location card, transition button.
- **Label (inscription)** — has `label_pos` but no `image`: a plain text caption naming a region. It is not clickable, does not open the card and is excluded from the zone counter.
- **Hidden zone** — `"hidden": true` or hidden at runtime: completely absent (no polygon, marker, label or counter entry) until revealed.

## Opening the map

The map is declared like any mod panel — in the manifest `ui` array:

```jsonc
{
  "id": "map",
  "kind": "panel",
  "slot": "ModBeforeUIPanel",
  "scene_path": "scenes/ui/map_widget.tscn",
  "button": { "icon": "map", "locale": "ui/map/title", "index": 10 },
  "priority": 10
}
```

`UiBuilder` instantiates the scene and hands a toolbar button to `ToolBox` (the HUD toolbar); pressing it toggles the panel through `UiRegistry`. The widget closes on `Escape`, on the header ✕ button, and automatically after a successful transition.

## Runtime state

Zone state has two layers:

- **Authoring defaults** — `locked` / `hidden` flags from the JSON above.
- **Runtime overrides** — sparse dictionaries owned by the `MapZoneService` singleton (`Core/UI/Tool/MapZoneService.cs`), changed via `Open`/`Close`/`Show`/`Hide`/`ResetZone`.

The effective state is the override when present, otherwise the authoring default. Every change publishes a `MapZoneChangedEvent`, so an open map updates live when a scenario changes a zone. Extra rule shared by the game and the editor (`MapZoneRules`): a zone whose `scene` is empty or unregistered is always treated as locked, so authors can sketch future regions safely. The zone matching the active scene is marked *you are here* and cannot be entered again.

## Scenario DSL actions

Four actions change zone state from a scenario. Each takes the required `zone_id` parameter; unknown IDs are ignored with a warning, so scripts can reference zones added by other mods.

| Action | Parameters | Effect |
|---|---|---|
| `action map.open_zone` | `zone_id=Id` | Opens the zone for travel |
| `action map.close_zone` | `zone_id=Id` | Closes the zone for travel |
| `action map.show_zone` | `zone_id=Id` | Reveals a hidden zone on the map |
| `action map.hide_zone` | `zone_id=Id` | Removes the zone from the map until shown again |

```scenario
action map.open_zone zone_id=temple
action map.show_zone zone_id=temple_label
```

Typical authoring pattern: ship zones with `"locked": true` / `"hidden": true` and open them from story events; the overrides survive scene changes and play sessions.

## Saving

Only the sparse overrides are saved — not the effective state. `RuntimeSerializer` stores them as a `MapZones` blob in the core `__core__` save slot (`open` and `visible` dictionaries keyed by zone ID, see `MapZoneStateData`), and `MapZoneService.ImportState` restores them on load. Storing deltas means a mod update that changes its authoring defaults is respected for every zone the player never touched. Starting a new game resets all overrides (`AppServices` resettable `map_zones`).

Persistence is covered by an integration test that runs the real `SaveService` save → reset → load cycle (`tests/Cthangover.Tests.Integration/Map/MapZonePersistenceTestSetup.cs`).

## Map editor

The `editor_tools` mod ships a visual map editor (`mods/editor_tools/src/MapEditor/`, tool id `map_editor`). It edits the same zone JSON: a canvas with draggable polygon, marker and label handles, per-zone fields, load/save of `map/*.json` into the selected mod, and a game-like preview that renders the real `map_widget.tscn` with the unsaved data. The editor and the game share one style implementation (`MapZoneStyle`), so both views render zones identically; after saving, the running map reloads without a game restart.

See [Creating a Custom Tool](../tools/tutorial.md) for how tools like this are registered.

## Code reference

| Component | Location |
|---|---|
| `MapWidget` | `Core/UI/Tool/MapWidget.cs` — overlay behaviour, markers, location card |
| `MapZonesLayer` | `Core/UI/Tool/MapZonesLayer.cs` — polygon hit-testing and painting |
| `MapMarker` | `Core/UI/Tool/MapMarker.cs` — marker emblem (`scenes/ui/map_marker.tscn`) |
| `MapZone` | `Core/UI/Tool/MapZone.cs` — JSON zone DTO |
| `MapZoneService` | `Core/UI/Tool/MapZoneService.cs` — zone list + runtime overrides singleton |
| `IMapZoneService` | `Core/Actions/IMapZoneService.cs` — scenario-facing facade (`ctx.Map`) |
| `MapZoneRules` / `MapZoneStyle` | `Core/UI/Tool/` — shared enterability rule and zone visuals |
| `Map*ZoneAction` | `Core/Actions/Atomic/` — the four DSL commands |
