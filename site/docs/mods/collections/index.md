# Mod Collections

A **collection** is a curated set of mods that together implement a game
mechanic — a card novel, a developer toolset, a battle system with its UI.
Unlike a category label on a single mod, a collection describes a
*composition*: the same mod can be the required backbone of one collection
and an optional layer of another, so mechanics belong to the collection file,
not to the mod manifest.

Collections are the game-construction unit the catalog is organized by: the
player is shown a scheme split into mechanics and blocks instead of a flat mod
list, and can install everything the game needs with one action. The
collection file itself never stores the dependency arrows — they are derived
from the mods' own `depends`, so the scheme cannot go stale when a mod author
changes dependencies.

## Where collections live

A collection is a **package of its own, not a file inside a mod**. A game is an
assembly of mods by many authors — no single mod owns it — so collections ship
next to the mods: anyone can publish a game, a toolset or a block without
touching (or forking) the mods it uses.

A package is a folder or a `.zip` named after the collection id:

```
collections/my_novel/
  collection.json        ← the document (schema below)
  cover.png              ← "cover": "cover.png" — shown in the game picker
  locale/en.properties   ← texts of the collection ("@key" markers), optional
  locale/ru.properties
```

| Source | Location | Precedence |
|---|---|---|
| Shipped with the game | `collections/<id>/` in the game directory (`res://collections/`) | overrides catalog |
| The player's | `user://collections/<id>/` — edited, duplicated and downloaded collections | overrides everything |
| Published in a catalog | `"collections"` array of a [catalog JSON](../catalog/) (document only: no cover, no locale files) | lowest |

`ModCollectionStore.LoadAll()` merges the three channels by collection `id`
(case-insensitive): catalog entries are inserted first, shipped packages
replace them, and the player's packages replace both. Packages are read through
`IModFileProvider`, so folders and zips work identically. The package's
`locale/` files are merged into the game's translations at boot, before any
mod, so the game picker shows localized names before the game's mods are
loaded. **Mods do not ship collections**: a `collections/` folder inside a mod
is not read.

## Collection JSON schema

```jsonc
{
  "id": "my_novel",                                        // Required. Canonical id, unique across all sources
  "kind": "Game",                                          // Game | Component | Toolset — see "Collection kinds"
  "name": "My Novel",                                      // Display name (falls back to id; see Localization)
  "author": "ct",                                          // Author credit
  "version": "1.0.0",                                      // Semver; picks the newest of same-id catalog collections
  "description": "A visual novel with card battles.",      // Shown on the gallery card and in the details panel
  "tags": ["novel", "cards"],                              // Free-form search tags
  "provides": ["battle_system"],                           // Capability ids this collection implements
  "requires": ["character_system"],                        // Capability ids needed from sibling blocks
  "includes": [                                            // Child collections this one is assembled from
    {
      "ref": "rpg_2d>=1",                                  // Versioned collection reference (same syntax as depends)
      "role": "systems",                                   // Slot role; two required blocks cannot claim one slot
      "optional": false,                                   // True when the block may be excluded by the player
      "selected": null,                                    // Player's choice for an optional block (null = on)
      "label": "2D Foundation",                            // Per-game name override (falls back to child name)
      "note": "Platformer core with map and inventory."    // Per-game description override
    }
  ],
  "mechanics": [                                           // Ordered mechanics — drives the scheme layout
    {
      "id": "scenes",                                     // Stable id, unique within the collection
      "name": "Scenes and dialogues",                      // Frame title in the scheme
      "description": "Scenes, scenarios, dialogue.",       // Tooltip
      "role": "story",                                     // Slot role the mechanic fills in the scheme editor
      "mods": ["cthangover_game>=1"],                    // Versioned mod references (same syntax as manifest depends)
      "optional": false,                                   // Marks the mechanic as skippable
      "selected": null,                                    // Player's choice for an optional mechanic (null = on)
      "locale": [                                          // Per-mechanic localized strings, same shape as below
        { "lang": "ru", "name": "Сцены и диалоги", "description": "Сцены, сценарии, диалоги." }
      ]
    }
  ],
  "locale": [                                              // Localized name/description of the whole collection
    { "lang": "en", "name": "Card Novel", "description": "A visual novel with card battles." },
    { "lang": "ru", "name": "Карточная новелла", "description": "Визуальная новелла с карточными боями." }
  ]
}
```

### Field reference

| Field | Required | Description |
|---|---|---|
| `id` | Yes | Canonical collection id. Entries without an id are ignored |
| `kind` | No | `Game`, `Component` or `Toolset`. Missing values deserialize to `Game` so documents written before kinds existed keep working |
| `name` | No | Display name; the id is used when absent |
| `author` | No | Author credit |
| `description` | No | Short description shown in the UI |
| `version` | No | Semver. When several catalogs publish the same id, the highest version wins |
| `tags` | No | Free-form search tags (`"systems"`, `"cards"`, `"2d"`, ...) |
| `provides` | No | Capability ids this collection implements. The scheme editor uses them to suggest compatible blocks; resolution itself does not enforce them |
| `requires` | No | Capability ids needed from sibling blocks. The resolver reports a problem when no resolved block provides them |
| `includes` | No | Child collections this one is assembled from — see below |
| `mechanics` | No | Ordered list of mechanics; the order drives the top-to-bottom scheme layout |
| `mechanics[].id` | No | Unique id used to group nodes; empty ids get a stable `mechanic_<index>` fallback |
| `mechanics[].name` | No | Frame header; the id is used when absent |
| `mechanics[].description` | No | Frame tooltip |
| `mechanics[].role` | No | Slot role (`"characters"`, `"story"`, `"battle"`, ...). The scheme editor groups the mechanic's mods under this label and role conflicts are detected against it |
| `mechanics[].mods` | No | Versioned mod strings (see below) |
| `mechanics[].optional` | No | Marks the mechanic as skippable (`false` by default). The install plan still includes it until the player turns it off |
| `mechanics[].selected` | No | Player's choice for an optional mechanic. `null`/omitted means on — old documents behave as before |
| `mechanics[].locale` | No | Localized name/description entries, same shape as the collection-level `locale` |
| `includes[].ref` | Yes | Versioned collection reference, e.g. `"rpg_2d>=1"` |
| `includes[].role` | No | Slot role inside the parent; two *required* includes claiming one role is a problem |
| `includes[].optional` | No | True when the block may be excluded without breaking the parent |
| `includes[].selected` | No | Player's choice; `null`/omitted means the block is on |
| `includes[].label` / `note` | No | Per-game name/description overrides; null falls back to the child collection's own fields |
| `locale` | No | Localized `name`/`description` entries — see [Localization](#localization) |
| `source_urls` | — | Written by the game only: catalog refreshes tag each collection with the URLs that published it. Local files should not set it |

A collection with neither `mechanics` nor `includes` shows an empty scheme.

### Collection kinds

`kind` is metadata — resolution does not depend on it — but it drives the
games gallery: only `Game` collections are offered as playable assemblies,
while `Component` and `Toolset` blocks are building material picked in the
scheme editor.

| Kind | Meaning |
|---|---|
| `Game` | Finished composition the player can assemble and play (default) |
| `Component` | Reusable block: a mechanic, a system layer, a content group |
| `Toolset` | Developer toolset (editors, validators, the mod manager) |

### Includes — collections made of collections

`includes[]` is the recursive edge of the collection pyramid: a parent game
declares which blocks compose it, each block can itself include others, and
the same block may be included by several parents with different roles.
`ref` uses the same versioned syntax as `manifest.json` dependencies and is
looked up in the merged collection store (user > built-in > catalog).

`label` and `note` are scheme-editor overlays: the player may rename a block
inside one game without touching the shared collection document, so a
component can be called "2D RPG" in the catalog and "Основа" in a specific
game.

### Versioned mod strings

Entries of `mechanics[].mods` and `includes[].ref` use the same format as
`manifest.json` `depends`: `modId[op][version][?]`, where `op` is one of
`>=`, `<=`, `>`, `<`, `=`, `^` or omitted (any version). Examples:

```jsonc
"mods": [
  "cthangover_game",       // any version
  "turn_battle_base>=1",     // at least 1.x
  "card_battle=1.2.0"        // exactly this version
]
```

The pin is used in two places: a locally installed mod that does not satisfy it
is shown as a **version mismatch**, and when installing from a catalog the
newest download satisfying the pin is chosen. The optional suffix `?` is parsed
but ignored — optionality of a whole mechanic or block is expressed with the
`optional` flag.

## Localization

Collection text is localized with two mechanisms; they can be combined.

### 1. The `locale[]` array

Entries carry `lang`, `name` and `description` — the same shape as catalog
mod entries. Mechanics carry their own `locale[]` with the same fields. The
best match for the player's language is picked as:

```
exact lang  ->  flat name/description fields  ->  "en"  ->  first entry with text  ->  id
```

so a shipped collection can present itself in several languages without
duplicating the document.

### 2. The `@key` marker

Any text field that starts with `@` is a **locale key**, not literal text:

```jsonc
"name": "@collections/my_novel/name"
```

The text after `@` is resolved through `TranslationServer`, which merges the
`locale/*.properties` files of **every loaded mod** (keys use `/` as the
namespace separator). If no translation is registered for the key, the raw
text after `@` is displayed — an untranslated marker degrades to a readable
id-like string instead of an error.

Mechanic names get a conventional key layout:

```
collections/<collection_id>/mechanic/<mechanic_id>/name
collections/<collection_id>/mechanic/<mechanic_id>/description
```

Put the keys into the package's own `locale/{lang}.properties` — the
collection carries its texts with it:

```properties
# en.properties
collections/my_novel/name = My Novel
collections/my_novel/description = A visual novel with an optional card-battle layer.
collections/my_novel/mechanic/scenes/name = Scenes and dialogues
collections/my_novel/mechanic/cards/name = Card Battle

# ru.properties — same keys, Russian values
collections/my_novel/name = Моя новелла
```

Because translations are merged from all loaded mods, a separate language-pack
mod can localize collections it does not own.

### Priority

```
locale[] of the exact lang  >  "@key" / flat field (translated if the key exists, raw otherwise)  >  fallback
```

The final fallback is the collection id (names) or an empty string
(descriptions).

## How the scheme is built

`ModCollectionResolver` turns a collection into a `ModCollectionGraph`:

1. `includes[]` are expanded first, recursively, from the merged collection
   store. Each included block becomes a scheme **group** with the child's mods
   flattened inside it; nested blocks collapse into the nearest ancestor, so
   the editor always draws a two-level pyramid. The root's own mechanics
   become leaf groups after the includes.
2. Every mod named by a mechanic becomes a node in that group; if several
   mechanics list the same mod, the first one owns it.
3. For each node, the mod's `depends` list (from the local registry) adds an
   edge **dependency → dependent**, and unknown dependencies are pulled into
   the scheme as external nodes shown in the *Dependencies* frame — including
   transitive dependencies, recursively.
4. Each node gets a state from the live sources:

| State | Node color | Meaning | Plan action |
|---|---|---|---|
| `Installed` | green | Installed, enabled, satisfies the version pin | none |
| `Disabled` | amber | Installed but listed in `disabled_mods.json` | re-enabled on apply |
| `InCatalog` | blue | Not installed, downloadable from a catalog | installed on apply |
| `VersionMismatch` | amber | Installed, but the version fails the collection's pin | reported as a warning |
| `Missing` | red | Unknown to the registry and every catalog | reported; blocks apply |

Inconsistencies found while expanding the pyramid are reported as **problems**
instead of failing resolution:

| Problem | Meaning |
|---|---|
| Missing block | an `includes[]` reference points at a collection nobody provides |
| Cycle | the include chain references a collection already on the active stack |
| Role conflict | two *required* includes claim the same role slot (optional blocks never conflict) |
| Unmet requirement | a resolved collection `requires` a capability no block `provides` |

The scheme is resolved fresh every time the collections screen opens or a
catalog refresh finishes, so it always reflects the current registry, disabled
store and catalog cache.

## The Mods & Collections hub

The mod manager contributes a single main-menu button, **Mods & Collections**
(with a counter of problems requiring attention), which opens one screen with
two tabs — **Mods** (the default) and **Collections**. The tab choice is
remembered, and the two screens stay cross-linked: a mod card lists the
collections that use it and links straight to their schemes, and scheme
problems link back to the mod catalog search.

### Collections tab — the games gallery

The first level of the Collections tab is a gallery of **games**: collections
with `"kind": "Game"`. Every card shows the cover, author/version/origin, the
mod count, tags and a status:

| Status | Meaning | Primary action |
|---|---|---|
| Ready (full progress bar) | every mod installed, enabled, pins satisfied | **Play** |
| Missing N mods | some mods are neither installed nor downloadable | **Show problems** |
| K problems | scheme errors (missing block, cycle, role conflict, unmet requirement) | **Show problems** |
| Draft | a partially assembled player collection, possibly with unsaved edits | **Continue building** |

Secondary action on every card is **Edit scheme**. Tiles **+ Assemble your own
game** create a new player collection, stored as a package in `user://collections/<id>/`.

How a player assembles a game: pick a game in the gallery → **Install** — the
build plan downloads the missing mods and re-enables disabled ones → the game
shows the familiar "changes take effect after a restart" banner. Mods
installed this way are remembered for the rest of the session
(`sessionInstalled`), so the scheme updates immediately even though the
registry only learns about them after a restart.

Editing the collection of the game that is already running (say, adding the
editors mechanic from the main menu) applies the next time the player enters
that game: **New game** or **Load** sees that the loaded mod set or the
definition no longer matches the collection (`GameCatalog.IsActiveOutdated`)
and switches the mod set in place instead of re-entering the stale one. No
restart of the process is needed.

Applying is blocked while any required mod is `Missing` from the catalog or
while scheme errors are present — enabling a mod whose dependency cannot be
loaded would only make the registry drop it at the next boot. Version
mismatches are shown as warnings but do not block apply.

### Scheme editor

**Edit scheme** opens the second level: a canvas with the game's contours.

- **Canvas** — one draggable/resizable group per mechanic or included block,
  with mod chips inside (state-colored dots); external dependencies render as
  dashed chips. Groups move by their header, mods move inside, the empty
  canvas pans with LMB. A toolbar offers **+ Mechanic** and **+ Block from
  catalog**; a toggle shows/hides dependency links.
- **Inspector** (right side) — tabs: *Inspector* (block metadata,
  provides/requires, actions: add mod, replace block, turn off, remove),
  *Check* (the problem list with "show" navigation to the offending group),
  *Plan* (install/enable counts, missing and mismatched mods).
- **Build bar** (bottom, always visible) — assembly name, **Install N**,
  **Enable N**, **Problems K**, a hint message, a shortcut to the mod manager
  and the primary **Apply collection** button (disabled while errors are
  present).
- **Header** — Save (writes the package `user://collections/<id>/`), Reset and a raw
  **JSON** view, plus the unsaved-edits marker.

## Ship your own collection

1. Create a folder named after your collection id: `collections/<id>/` for a
   game shipped with the build, or `user://collections/<id>/` (zip it to share).
2. Put the document into `collection.json`; the `id` inside must match the
   folder name and be unique across all sources.
3. Set `"kind": "Game"` for a playable assembly, `"Component"` for a
   reusable block, `"Toolset"` for developer tools.
4. Declare `mechanics` (or `includes` of other collections) with versioned
   mod references.
5. Localize: add a `locale[]` array and/or `@key` markers, then put the keys
   into the package's `locale/{lang}.properties`; add a `cover.png`.
6. Ship the package. The store merges it automatically and your
   shipped copy overrides an online catalog entry with the same id; the player
   can override both locally.

A complete example — a game that names its texts through `@key` markers and also
carries `locale[]` for the game picker:

```jsonc
{
  "id": "my_novel",
  "kind": "Game",
  "name": "@collections/my_novel/name",              // resolved from locale/*.properties
  "description": "@collections/my_novel/description",
  "author": "you",
  "version": "1.0.0",
  "entry": { "scene": "my_town" },
  "tags": ["novel", "cards"],
  "locale": [
    { "lang": "en", "name": "My Novel", "description": "A visual novel with an optional card-battle layer." },
    { "lang": "ru", "name": "Моя новелла", "description": "Визуальная новелла с необязательным слоем карточных боёв." }
  ],
  "mechanics": [
    {
      "id": "story",
      "name": "@collections/my_novel/mechanic/story/name",
      "description": "@collections/my_novel/mechanic/story/description",
      "role": "story",
      "mods": ["my_novel_story>=1", "shell2d_base>=1", "dialog_base>=1"]
    },
    {
      "id": "cards",
      "name": "@collections/my_novel/mechanic/cards/name",
      "description": "@collections/my_novel/mechanic/cards/description",
      "role": "battle",
      "optional": true,
      "mods": ["turn_battle_base>=1", "card_battle>=1"]
    }
  ]
}
```

## Catalog distribution

A catalog may publish collections in its root envelope:

```jsonc
{
  "catalog": { "version": 1, "updated_at": "2026-09-28T00:00:00Z" },
  "mods": [ /* ... */ ],
  "collections": [
    {
      "id": "my_novel",
      "kind": "Game",
      "name": "My Novel",
      "version": "1.0.0",
      "mechanics": [
        { "id": "story", "name": "Story", "mods": ["my_novel_story>=1"] }
      ]
    }
  ]
}
```

Collections from all configured catalog URLs are merged by `id` — the highest
`version` wins while `source_urls` accumulates every publisher — and cached
together with the mod entries in `user://catalog_cache.json`. The
`collections` key is optional, so catalogs written before collections existed
keep working.

## Core API

Collections are implemented in the kernel (`Core/Mods/Collections/`), so
launcher code, tools and other mods can use them without the UI:

| Type | Purpose |
|---|---|
| `ModCollection`, `ModCollectionMechanic`, `ModCollectionInclude` | JSON DTOs; `GetLocalizedName(lang)` / `GetLocalizedDescription(lang)` implement the `locale[]` lookup |
| `ModCollectionStore` | `LoadAll()`, `LoadUser()`, `SaveUser()`, `LoadFromProvider(provider)` |
| `ModCollectionResolver.Resolve(collection, collections, localMods, disabledIds, catalogEntries, sessionInstalled = null, language = null, translator = null)` | Builds `ModCollectionGraph` (nodes, edges, groups, problems, states). `translator` (e.g. `CollectionText.Resolve`) resolves `@key` display text; null keeps raw keys |
| `ModCollectionGraph` | `Mechanics`, `Groups` (two-level pyramid), `Problems`, `Nodes`, `Edges`, `InstalledCount`/`TotalCount` |
| `ModCollectionPlan.Build(graph, catalogEntries)` | `Install` items (id, version, source URL), `Enable`, `Missing`, `Mismatches` |
| `ModCollectionNodeState` | `Installed`, `Disabled`, `InCatalog`, `Missing`, `VersionMismatch` |
| `ModCollectionProblem(Kind)` | `MissingCollection`, `Cycle`, `RoleConflict`, `RequirementMissing` |

Example — resolve a collection and inspect its plan:

```csharp
var collections = ModCollectionStore.LoadAll();
var byId = collections.ToDictionary(c => c.Id, StringComparer.OrdinalIgnoreCase);
var collection = collections[0];
var catalog = CatalogService.CachedEntries ?? new List<CatalogExportEntry>();

var graph = ModCollectionResolver.Resolve(
    collection,
    byId,
    ModRegistry.Instance.AllDiscoveredMods,
    DisabledModsStore.Load(),
    catalog,
    language: "ru");

var plan = ModCollectionPlan.Build(graph, catalog);
foreach (var item in plan.Install)
    GameLogger.Log("MY_MOD", $"install {item.ModId} v{item.Version} from {item.SourceUrl}");
```

Writes (downloading, enabling) stay outside the plan: the UI passes
`plan.Install` to `ModDownloader.InstallAsync` and removes `plan.Enable` from
`DisabledModsStore`.

## Start your own game

A game is three layers, and only the first one is yours to write:

1. **Your game's mod** — `<game>_game` (for example `lighthouse_game`): your
   scenes, scenarios and texts. Copy `mods/my_novel_game`, rename the folder,
   `id`, scene and scenario names (prefix them with your game) and locale keys.
   Its scenario lines are written in English (`text "…" key=…`); other
   languages, Russian included, live only in `locale/scenarios_<lang>.properties`.
2. **Packs and mechanics you reuse** — `town_pack` for backgrounds and sound,
   `cooking` or `card_battle` for gameplay. Depend on them instead of copying
   their files: a fix in the pack reaches every game. To change a pack's
   scenario or background for your game only, ship the same name and depend on
   the pack — your version wins (see [load order](../load-order/)).
3. **The engine** — `shell2d_base`, `dialog_base`, `audio_base`, …

Then copy `collections/novel_template/` to `collections/<your game>/`, set the
`id`, `entry.scene`, the cover and the mod lists, and the game appears in the
picker.

## Real examples

- `collections/novel_template/` — the smallest playable
  game: the game's own content (`my_novel_game`: scenes, scenarios, text),
  reusable assets (`town_pack`: town, home and bar backgrounds, bar ambience —
  the main game uses the same pack) and the engine mods it needs (`shell2d_base`,
  `dialog_base`, `audio_base`). See "Start your own game" below.
- `collections/cthangover/` — the main game, a visual novel: story, the 2D
  engine, characters, quests, items, battles with an optional second battle
  core, Live2D and tools, each as its own mechanic.
- `collections/rpg_demo/` — an RPG Maker style game on the same engine mods:
  its content (`rpg_demo_game`), pixel art (`village_pack`), the tile world
  (`rpg`), the FF battle core with the card core as an option, and the map
  editor (`rpg_editor`) as an optional tool.
- `collections/novel_editor/` — a "game" whose entry scene
  is the editor hub instead of a story scene.
- `collections/dev_tools/` — the developer toolset to add to any game: the
  editors (`editor_tools`), the in-game scenario studio (`scenario_studio`,
  opened by the **Studio** button on the game's toolbar), validators and the
  mod manager. Add it to a game in the collection editor and enter the game
  again — the mod set switches without a restart.
