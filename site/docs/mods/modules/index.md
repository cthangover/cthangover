# Bundled modules and their references

Each row corresponds to an installed top-level `mods/<id>/manifest.json`, audited on 2026-11-03. A game selects modules through its collection and transitive dependencies; installing a module alone does not select it for every game. Versions and dependency details on the home-page catalog are generated from these same manifests.

Read a mod's **Documentation** tab in Mods & Collections for its bundled `docs/<language>.md` guide and actual game/editor captures. The table links to the system reference on this site. Test fixtures and conditionally mounted integrations are not ordinary gameplay content.

| Mod | Purpose from its manifest | Reference |
|---|---|---|
| `audio_base` | Audio engine: scene music, sound effects and ambience on separate buses with their own volume, and a music player that follows the current scene. | [Guide](../musics/) |
| `avatar_base` | Reusable avatar definitions and independent preset state, with renderer-neutral composition for layered images and animated models. | [Guide](../avatars/layers.md) |
| `avatar_layers` | Layered PNG avatar renderer: preset-controlled images, ordering, placement, visibility and opacity. | [Guide](../avatars/layers.md) |
| `battle_formulas` | Battle skills written as data: damage and heal formulas over the participants' stats, resource costs, pierce and stun — no code, and the same skill works in every battle core. | [Guide](../stats/) |
| `card_battle` | A battle system with cards: pick a character card and drag its action cards onto targets. Built on Turn Battle Base; the battle of Cthangover. | [Guide](../battle/) |
| `catalog_export` | Export mod catalog metadata and installable archives. | [Guide](../editor_tools/) |
| `characters_base` | Characters engine: heroes, companions and enemies with their battle attributes and action cards, the character actions panel and dialog portraits. | [Guide](../characters/) |
| `characters_editor` | Edits a mod's characters while the game runs: names in every language, portrait, RPG walking sprite, battle numbers and actions. A save reaches the running game at once. | [Guide](../editor_tools/) |
| `classic_rpg_pack` | A ready set of classic fantasy rules as data: six attributes, three classes, equipment slots, gear and four formula skills. RPG Plus is built on it. | [Guide](../stats/) |
| `cooking` | The kitchen of Cthangover: learn recipes, cook at the home_kitchen workbench and keep an eye on the party's rations. | [Guide](../items/) |
| `crafting` | Workbench stations that turn materials into items: stations and recipes are plain data, and any scenario opens a station with one command. | [Guide](../crafting/) |
| `cthangover_game` | The story content of Cthangover: scenes, scenarios, characters, items, quests and the world map of Marao's very bad night. The machinery lives in engine mods. | [Guide](../collections/) |
| `cthangover_murakami` | A Cthangover story line: follow the beast prowling the town to a meeting with Murakami — scenarios, her character and expressions, a journal quest and battles. | [Guide](../collections/) |
| `dialog_base` | Dialog engine: the dialog box with portraits and answers, and the .scenario language in which every game of the collection tells its story. | [Guide](../scenes/scenarios/) |
| `editor_tools` | Compatibility toolset: installs the individual scene, scenario, map, interactive, light, Godot scene and catalog tools. | [Guide](../editor_tools/) |
| `effects_base` | Scene effects and continuous atmosphere: editable particle profiles and extensible emitters for leaves, fireflies, snow, rain and dust. | [Guide](../effects/) |
| `effects_editor` | Live atmosphere authoring: particle and ripple profiles, preview, map and cell placement. | [Guide](../effects/) |
| `equipment` | Wearable gear for any game with stats: slots as data, items become equipment by their JSON keys, and worn bonuses flow into sheets, battles, checks and conditions at once. | [Guide](../stats/) |
| `expressions_base` | Reusable numeric expressions with mod-defined function and operator strategies, strict runtime assignments and compatible game formulas. | [Guide](../scenes/scenarios/expressions.md) |
| `ff_battle` | A battle system in the style of classic console RPGs: the party, the enemies and a command menu — Attack, Items, Defend, Escape. The battle of RPG Plus. | [Guide](../battle/) |
| `interactive_base` | Clickable objects for picture scenes: a picture of its own over the background, a glow under the mouse, and a click that starts a scenario. | [Guide](../interactives/) |
| `interactive_editor` | Edit scene interactive objects and their placement on backgrounds. | [Guide](../editor_tools/) |
| `items_base` | Items and the inventory: what an item is, the bag panel, what is saved, and scenario commands to give, take and check items. | [Guide](../items/) |
| `items_editor` | Edits a mod's items while the game runs: names and descriptions in every language, price, picture, types and the use action. The game shows a save at once. | [Guide](../editor_tools/) |
| `junior_reaper_paper_grief` | Расследование повторных смертей стражника: фэнтезийный RPG-мир, осмотр мельницы мышью, сброс, свидетели и решение семейной причины. | [Guide](../collections/) |
| `light_editor` | Edit scene lights and their placement on backgrounds. | [Guide](../time/) |
| `live2d_base` | Live2D Cubism runtime for C#: drawn characters that breathe, blink and move with physics, playing the motions and expressions their artist made. | [Guide](../scenes/scenarios/actions/catalog.md) |
| `locale_atlas` | Maps every translation key of every mod: what each language declares, where keys are used, and what is wrong — missing, empty, undeclared or duplicated keys and encoding problems. | [Guide](../locale/) |
| `locale_editor` | A translations window: lists every text a language lacks (from the locale atlas), and writes the translation into the locale file of the mod that owns the key, so the game shows it at once. | [Guide](../locale/) |
| `map_base` | World map engine: a full-screen picture of the world with places to travel between; scenarios open, close, hide and reveal them as the story goes. | [Guide](../map/) |
| `map_editor` | Edit world map zones, polygons, labels and markers. | [Guide](../map/) |
| `menu_base` | The main menu: start or continue a game, pick one of the installed games, load a save and change the language, sound and display settings. | [Guide](../collections/) |
| `mod_manager` | The Mods & Collections screen: what is installed, what each mod does, installing and updating from the online catalog, and which mods make up each ready-made game. | [Guide](../collections/) |
| `music_pack` | Shared music for any game: 22 tracks with exploration, combat and main menu playlists; a game can override playlists while keeping the track names. | [Guide](../musics/) |
| `my_novel_game` | The novel template's content: an evening town, a bar and a home connected by choices. Copy it, rename it and grow your own story from these three places. | [Guide](../collections/) |
| `npc_barks_base` | Concurrent ambient scenario hosts, speech cues and NPC attention. Prototype. | [Guide](../scenes/scenarios/background.md) |
| `quests_base` | Quests engine: quests with numbered steps that scenarios start, advance and finish, and the journal panel that shows them. | [Guide](../quests/) |
| `recruiting` | Defeated enemies can be captured after a victory and later fight on the player's side; while they wait in the recruits list, they live by rules of their own. | [Guide](../recruiting/) |
| `resource_atlas` | Maps which mod uses whose resources and whether every link is backed by depends — catching undeclared dependencies and duplicate providers no depends order can settle. | [Guide](../testing/) |
| `rpg` | A walkable tile world in the spirit of RPG Maker: grid movement with WASD, events on E/F, teleports; a map is an ordinary scene of the game. Battles and random encounters come with turn_battle_base through the battles integration, so the world runs without it. | [Guide](../rpg/) |
| `rpg_editor` | RPG map editor for model v2 (plan 12.4): a palette by shape, the map in the game's own top-down and isometric projections, placements (own and foreign, as patches), strokes, collision, spawn, teleport links, undo and a byte-stable save; encounters when rpg__battles is mounted. | [Guide](../editor_tools/) |
| `rpg_object_editor` | Edits RPG objects (model v2) while the game runs: surfaces, segments, sprites and actors in a form by shape, their pictures, footprint, anchor, interaction and extends, with a preview in the top-down and the isometric projection drawn by the game's own renderer. A foreign object is edited as a patch in the own mod; the source stays untouched. | [Guide](../editor_tools/) |
| `rpg_plus_game` | A complete small RPG: a prologue that picks the class, a village and forest mystery, crafting, trade, a desert and turn-based battles — data and scenarios only. | [Guide](../collections/) |
| `scenario_editor` | Edit scenario text and transition graphs, then play-test inside the current game. | [Guide](../editor_tools/) |
| `scenario_studio` | A dock beside the running game that pauses, steps and edits the playing scenario: breakpoints, restarts with state rollback, live edits into a mod or a per-game layer. | [Guide](../editor_tools/) |
| `scenario_tools_base` | Shared scenario file access, syntax and graph models for authoring tools. No tool windows. | [Guide](../editor_tools/) |
| `scene_builder` | Inspect and edit Godot scene nodes and their properties with a live preview. | [Guide](../editor_tools/) |
| `scene_editor` | Create and edit logical scenes, backgrounds, ambient sound and scenario bindings. | [Guide](../editor_tools/) |
| `schema_base` | Lets every mod describe its scenario actions, DSL commands and data fields as data; the tools build forms, pickers and checks from these descriptions. | [Guide](../schemas/) |
| `schema_tools` | The Data Editor window and the command palette: edit any described entity in a form and assemble checked scenario lines from the mods' schemas. | [Guide](../schemas/) |
| `shell2d_base` | The frame every 2D game is drawn in: the background view box, the toolbar, panel slots and notifications that content mods fill. | [Guide](../editor_tools/) |
| `skills_base` | The player's collection of skill cards: skills that open possibilities in other mechanics, grant battle action cards and add up into the collection effect. | [Guide](../skills/) |
| `stat_checks` | One scenario command, check: roll a character's stat plus dice against a difficulty and branch the story on success or failure. | [Guide](../stats/) |
| `stats_base` | A stats engine for any genre: stats, archetypes and traits as data, formulas for derived numbers, character sheets, levels and pools — handed to battles unchanged. | [Guide](../stats/) |
| `test_defaults_and_patches` | A test fixture, not content: property patches the property_patches test loads to check how patches of other mods' characters and items merge. Never part of a game. | [Guide](../testing/) |
| `test_integrations` | A test fixture, not content: hosts the integration 'probe' that joins a mod set only together with quests_base, so the tests can check both the boot and the in-process mounting gate. Never part of a game. | [Guide](../testing/) |
| `tile_isometric_base` | Draws tile maps at an angle, a grid of 2:1 diamonds, so buildings and trees stand up out of the ground. One of the two views of Tile Rendering. | [Guide](../rpg/) |
| `tile_render_base` | The shared engine behind tile maps: turns a prepared map description into a picture; the viewing angle is a small renderer mod on top. | [Guide](../rpg/) |
| `tile_topdown_base` | Draws tile maps straight from above, on a square grid, in the style of classic 2D RPGs. One of the two views of Tile Rendering; RPG Plus uses it. | [Guide](../rpg/) |
| `time_base` | The in-game clock, day and night: scenes change their look with the time, lamps and windows light them, and a portable lamp helps after dark. | [Guide](../time/) |
| `tools_base` | Shared forms, file access, localization editing and UI components for authoring tools. No tool windows. | [Guide](../editor_tools/) |
| `town_pack` | Ready-made scenery for a small-town story: the town entrance, a home vestibule and an evening bar as backgrounds, with the bar's ambience. No story inside. | [Guide](../collections/) |
| `trading` | Shops as data and a trade window of two inventories: the merchant's goods on one side, the player's bag on the other, and money as an ordinary item. | [Guide](../editor_tools/) |
| `turn_battle_base` | The platform under every turn-based battle: starting from a scenario, the battle scene, victory and defeat, experience and loot — how the player fights is a separate battle system. | [Guide](../battle/) |
| `ui_base` | The interface layer every game shares: the toolbar, the in-game menu, the save indicator, notifications and the common look of panels, buttons and lists. | [Guide](../editor_tools/) |
| `validator` | Checks every mod without running the game: broken references, unknown scenario commands, wrong parameters, missing dependencies — a report on every game start. | [Guide](../testing/) |
| `village_pack` | Reusable RPG art: nine tilesets, walking sprites, battle artwork, snow enemies, six shared architectural interiors, reusable containers and editable atmosphere profiles. | [Guide](../rpg/) |

## Games and toolsets

The four `kind: game` collections are `cthangover`, `novel_template`, `rpg_plus`, and `junior_reaper_paper_grief`. `frontend` is the menu/shell toolset; `dev_tools` groups authoring capabilities. Collections are packages under `collections/<id>/collection.json`, not mods.

`editor_tools` is a source-free compatibility package bringing seven independent editors. A game can choose individual tools instead. `tools_base` supplies common widgets/documents and neutral services; `scenario_tools_base` adds dialog-specific file and graph models. Neither registers a window of its own.

`rpg__battles` and `turn_battle_base__quests` are production [integrations](../src/integrations.md). Their host/partner combination determines whether they mount. Mod test suites and fixture mods instead require explicit [test activation](../testing/).
