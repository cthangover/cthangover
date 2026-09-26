![cthangover logo](../img/logo.png)

# Documentation

Welcome to the cthangover documentation. Here you'll find guides, code examples, and configuration references for mod and core development.

## Kernel and game cores

cthangover is not tied to a single genre. The C# kernel (`Core/`) provides genre-neutral infrastructure only: scenes and scenarios with the DSL, the mod registry, saves, resources, UI hosting, events, audio, and pluggable battle rulesets. It makes no assumptions about dialogue, portraits, or the shape of the game.

A **game core** is an ordinary mod that turns the kernel into a concrete game. `visual_novel_core` is the reference core shipped with the project: it defines characters, avatars, backgrounds, quests, items, and scenarios for a dialogue-driven story game. It is only one possible core — the kernel does not require it, and it uses the same public interfaces available to every mod.

Any genre can be built the same way. For example, a 2D sprite RPG with tile movement, turn-based encounters, and its own inventory UI can be plugged in as a core mod: it reuses the kernel (scenes, saves, events, resource pipeline) and brings its own assets, scenarios, actions, and UI scenes. The same approach works for other genres.

| Layer | Location | Responsibility |
|---|---|---|
| Kernel | `Core/` | Genre-neutral services: scenes, scenario DSL, mods, saves, events, resources, UI, audio |
| Game core | `mods/<core>` | Defines a concrete game: content, rules, scenes, actions, UI |
| Gameplay mods | `mods/<mod>` | Extend a core or the kernel: patches, assets, custom actions, battle rulesets |

A core can be disabled or replaced. Mods that depend on a specific core must declare it in `depends` — see [Configs — manifest.json](mods/configs/). The kernel itself never depends on a core.

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
| [Developer Tools](mods/tools/) | Custom editor windows and toolbar integration |
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
