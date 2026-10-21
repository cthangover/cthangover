![cthangover logo](../img/logo.png)

# Documentation

Welcome to the cthangover documentation. Here you'll find guides, code examples, and configuration references for mod and core development.

## Kernel, engine mods and games

cthangover is a game engine, not a game of one genre. The C# kernel (`Core/`) is a **mod player**: it loads, compiles and
registers mods, resolves their data, keeps saves, runs the event bus and hosts the UI. It ships no game, no scene and no
genre — a visual novel and a top-down RPG are both just sets of mods.

**Engine mods** (`*_base`) are the building blocks a game picks: the 2D shell (`shell2d_base`), dialogues with the
scenario DSL (`dialog_base`), characters, items, quests, skills, the world map, time and lighting, the turn-based battle
platform with its cores (`card_battle`, `ff_battle`), the tile world (`rpg`), the UI toolkit and the main menu.

A **game** is a [collection](mods/collections/): the game's own content mod plus the mechanics it chooses. The project
ships three of them, all running on the same engine mods and switching inside one process:

| Game | Content | Built from |
|---|---|---|
| `cthangover` | `cthangover_game`, `cthangover_murakami` | a visual novel with characters, quests, items, cooking and turn-based battles |
| `rpg_demo` | `rpg_demo_game`, `village_pack` | an RPG Maker style game: tile maps walked with WASD, events on E/F, battles, the map editor |
| `novel_template` | `my_novel_game`, `town_pack` | the smallest playable novel, a starting point for a new story |

| Layer | Location | Responsibility |
|---|---|---|
| Kernel | `Core/` | Genre-neutral services: mod pipeline, data resolvers, scenes, saves, events, UI hosting |
| Engine mods | `mods/<subject>_base`, mechanics | Subsystems a game may take: shell, dialogues and DSL, characters, battles, tile world, menu |
| Game | `collections/<game>` | The set of mechanics and the entry scene of one game |
| Content mods | `mods/<game>_game`, `mods/<theme>_pack` | Scenes, maps, scenarios, characters, art and text of a game, or reusable art |

The kernel never depends on a game or a genre. A mod that needs another one declares it in `depends` — see
[Configs — manifest.json](mods/configs/).

## Mod assets & data

| Topic | Description |
|---|---|
| [Avatars](mods/avatars/) | Character portrait images |
| [Backgrounds](mods/backgrounds/) | Scene background images and depth maps |
| [Characters](mods/characters/) | Character JSON definitions and attributes |
| [Interactive Objects](mods/interactives/) | Clickable scene objects with hit areas and actions |
| [Items](mods/items/) | Item system — consumables, resources, recipes |
| [Property Patches](mods/patches/) | Extend entity properties from another mod without redefining JSON |
| [Skills](mods/skills/) | Card-based skill system |
| [Sounds](mods/sounds/) | Sound effect definitions |
| [Shaders](mods/shaders/) | Custom shader effects |

## Mod configuration

| Topic | Description |
|---|---|
| [Configs — manifest.json](mods/configs/) | Mod manifest reference |
| [Game Config](mods/configs/game_config) | Default game settings |
| [Mod Config](mods/configs/mod_config) | Mod system cache and file discovery |
| [Catalog & Network](mods/catalog/) | Online mod catalog schema, merging, and export |
| [Collections](mods/collections/) | Curated mod sets implementing a mechanic, shown as a dependency graph |

## Scenarios & DSL

| Topic | Description |
|---|---|
| [Scenes](mods/scenes/) | Scene definitions and lifecycle |
| [Runtime TSCN Scenes](mods/scenes/tscn-runtime/) | Loading Godot `.tscn` scene files at runtime from mods |
| [Scenarios](mods/scenes/scenarios/) | `.scenario` format and metadata |
| [Scenario Actions](mods/scenes/scenarios/actions/) | `action` command reference |
| [DSL Commands](mods/scenes/scenarios/dsl/) | Full command reference (30 commands) |
| [Conditions](mods/scenes/scenarios/dsl/conditions/) | Quest condition expression language |

## Gameplay systems

| Topic | Description |
|---|---|
| [Battle](mods/battle/) | Battle system architecture and custom battle mods |
| [Quests](mods/quests/) | Quest definitions and progression |
| [Status Effects](mods/status_effects/) | Buff, debuff, and stun effect definitions |
| [Time](mods/time/) | In-game clock, day/night phases, lighting |
| [Map & Travel Zones](mods/map/) | World map, travel zones, map DSL actions, in-game map editor |
| [Recruiting](mods/recruiting/) | Character recruitment system |
| [Save System](mods/save/) | Save/load data structure and persistence |
| [Mod Settings](mods/settings/) | Per-mod settings in the in-game menu |

## Audio

| Topic | Description |
|---|---|
| [Music & Playlists](mods/musics/) | Background music and playlist configuration |
| [Playlists — JSON Reference](mods/musics/playlists/) | Detailed playlist schema |

## Mod development

| Topic | Description |
|---|---|
| [Source Code (C#)](mods/src/) | C# in mods — Roslyn compilation, interfaces |
| [GDScript in Mods](mods/src/gdscript/) | Scenario actions, conditions, battle, items, lifecycle |
| [Developer Tools](mods/editor_tools/) | Custom editor windows and toolbar integration |
| [Locale](mods/locale/) | Localization system |
| [UI Wrappers](mods/wrappers/) | UI template wrapper system |
| [Scene Event Metadata](mods/metadata/) | `[SceneEvent]` attribute for declarative hooks |
| [ZIP Distribution](mods/zip/) | Mod packaging and distribution |

## Resources — Pluggable Infrastructure

| Topic | Description |
|---|---|
| [Resources Overview](resources/) | Scanner → Loader → Provider pipeline |
| [Providers](resources/providers/) | `IModFileProvider` — folder, zip, and custom backends |
| [Scanners](resources/scanners/) | `IModScanner` — local dirs, HTTP catalogs, custom sources |
| [Loaders](resources/loaders/) | `IModLoader` — folder, zip, remote download, archive formats |
| [Discovery Registry](resources/discovery-registry/) | `ModDiscoveryRegistry` — auto-discovery and LIFO lookup |
