# Mod Collections

A **collection** is a curated set of mods that together implement a game
mechanic — a card novel, a developer toolset, a battle system with its UI.
Unlike a category label on a single mod, a collection describes a
*composition*: the same mod can be the required backbone of one collection
and an optional layer of another, so mechanics belong to the collection file,
not to the mod manifest.

Collections are the game-construction unit the catalog is organized by: the
player is shown a dependency graph split into mechanics instead of a flat mod
list, and can install/enable everything a mechanic needs with one action. The
collection file itself never stores the graph — the arrows are derived from the
mods' own `depends`, so the graph cannot go stale when a mod author changes
dependencies.

## Where collections live

| Source | Location | Precedence |
|---|---|---|
| Bundled with a mod | `<mod root>/collections/*.json` (folder or zip) | overrides catalog |
| Created by the player | `user://collections.json` (JSON array) | overrides everything |
| Published in a catalog | `"collections"` array of a [catalog JSON](../catalog/) | lowest |

`ModCollectionStore.LoadAll()` merges the three channels by collection `id`
(case-insensitive): catalog entries are inserted first, bundled files replace
them, and player collections replace both. That lets a shipping mod fix an
outdated online collection without asking the catalog owner, and lets the
player edit any collection locally.

Any mod can ship collections — just add a `collections/` directory next to
`manifest.json`. Collections are read through `IModFileProvider`, so they work
identically for folder mods, zip mods, and custom providers.

## Collection JSON schema

```jsonc
{
  "id": "card_novel",                                      // Canonical collection id (required, unique across sources)
  "name": "Card Novel",                                    // Display name (falls back to id)
  "author": "ct",                                          // Author credit
  "description": "A visual novel with card battles.",      // Shown in the details panel and as a tooltip
  "version": "1.0.0",                                      // Semver; picks the newest of same-id catalog collections
  "mechanics": [                                           // Ordered mechanics — drives the graph layout
    {
      "id": "vn_core",                                     // Stable id, unique within the collection
      "name": "Novel Core",                                // Frame title in the graph
      "description": "Scenes, scenarios, dialogue.",       // Tooltip
      "mods": ["visual_novel_core>=1"]                     // Versioned mod references (same syntax as manifest depends)
    },
    {
      "id": "cards",
      "name": "Card Game",
      "mods": ["card_battle>=1"],
      "optional": true                                     // Marked as skippable in the graph
    }
  ]
}
```

### Field reference

| Field | Required | Description |
|---|---|---|
| `id` | Yes | Canonical collection id. Entries without an id are ignored |
| `name` | No | Display name; the id is used when absent |
| `author` | No | Author credit |
| `description` | No | Short description shown in the UI |
| `version` | No | Semver. When several catalogs publish the same id, the highest version wins |
| `mechanics` | Yes | Ordered list of mechanics; a collection without mechanics shows an empty graph |
| `mechanics[].id` | No | Unique id used to group nodes; empty ids get a stable `mechanic_<index>` fallback |
| `mechanics[].name` | No | Frame header; the id is used when absent |
| `mechanics[].description` | No | Frame tooltip |
| `mechanics[].mods` | Yes | Versioned mod strings (see below) |
| `mechanics[].optional` | No | Marks the mechanic as skippable (`false` by default). Shown in the graph; the install plan still includes it |

`source_urls` is the only field written by the game itself: catalog refreshes
tag each collection with the URLs that published it, so the UI can show
provenance. Local files should not set it.

**Note:** collection `name` and `description` are not localized. Catalog mod
entries support a `locale` array, but collection localization is not
implemented yet — author them in the language you ship.

### Versioned mod strings

Entries of `mechanics[].mods` use the same format as `manifest.json`
`depends`: `modId[op][version][?]`, where `op` is one of `>=`, `<=`, `>`, `<`,
`=`, `^` or omitted (any version). Examples:

```jsonc
"mods": [
  "visual_novel_core",       // any version
  "turn_battle_base>=1",     // at least 1.x
  "card_battle=1.2.0"        // exactly this version
]
```

The pin is used in two places: a locally installed mod that does not satisfy it
is shown as a **version mismatch**, and when installing from a catalog the
newest download satisfying the pin is chosen. The optional suffix `?` is parsed
but ignored — optionality of a whole mechanic is expressed with the
`optional` flag.

## How the graph is built

`ModCollectionResolver` turns a collection into a `ModCollectionGraph`:

1. Every mod named by a mechanic becomes a node in that mechanic's frame; if
   several mechanics list the same mod, the first one owns it.
2. For each node, the mod's `depends` list (from the local registry) adds an
   edge **dependency → dependent**, and unknown dependencies are pulled into
   the graph as external nodes shown in the *Dependencies* frame — including
   transitive dependencies, recursively.
3. Each node gets a state from the three live sources:

| State | Node color | Meaning | Plan action |
|---|---|---|---|
| `Installed` | green | Installed, enabled, satisfies the version pin | none |
| `Disabled` | amber | Installed but listed in `disabled_mods.json` | re-enabled on apply |
| `InCatalog` | blue | Not installed, downloadable from a catalog | installed on apply |
| `VersionMismatch` | amber | Installed, but the version fails the collection's pin | reported as a warning |
| `Missing` | red | Unknown to the registry and every catalog | reported; blocks apply |

The graph is resolved fresh every time the collections screen opens or a
catalog refresh finishes, so it always reflects the current registry, disabled
store and catalog cache.

## The Collections screen

The mod manager mod contributes a second main-menu button, **Collections**,
next to the mod catalog. The screen has three parts:

- **Left** — every merged collection, sorted by origin (bundled, player,
  catalog) and then by name.
- **Center** — the dependency graph: one `GraphFrame` per mechanic, one
  `GraphNode` per mod, arrows from dependencies to dependents. Node colors
  follow the state table above; clicking a node shows its id, version, pin,
  dependencies and dependents in the details panel.
- **Right** — collection metadata, the mechanic list, and the **install plan**:
  which mods will be downloaded, which disabled mods will be enabled, and what
  is missing. **Apply collection** downloads the missing mods sequentially,
  removes the listed mods from `disabled_mods.json`, and reports that changes
  take effect after a restart (the same banner as the mod manager).

Applying is blocked while any required mod is `Missing` from the catalog —
enabling a mod whose dependency cannot be loaded would only make the registry
drop it at the next boot. Version mismatches are shown as warnings but do not
block apply.

Mods installed from the screen are remembered for the rest of the session
(`sessionInstalled`), so the graph updates immediately even though the registry
only learns about them after a restart.

## Catalog distribution

A catalog may publish collections in its root envelope:

```jsonc
{
  "catalog": { "version": 1, "updated_at": "2026-09-28T00:00:00Z" },
  "mods": [ /* ... */ ],
  "collections": [
    {
      "id": "card_novel",
      "name": "Card Novel",
      "version": "1.0.0",
      "mechanics": [
        { "id": "vn_core", "name": "Novel Core", "mods": ["visual_novel_core>=1"] }
      ]
    }
  ]
}
```

Collections from all configured catalog URLs are merged by `id` — the highest
`version` wins while `source_urls` accumulates every publisher — and cached
together with the mod entries in `user://catalog_cache.json`. The `collections`
key is optional, so catalogs written before collections existed keep working.

## Core API

Collections are implemented in the kernel (`Core/Mods/Collections/`), so
launcher code, tools and other mods can use them without the UI:

| Type | Purpose |
|---|---|
| `ModCollection`, `ModCollectionMechanic` | JSON DTOs |
| `ModCollectionStore` | `LoadAll()`, `LoadUser()`, `SaveUser()`, `LoadFromProvider(provider)` |
| `ModCollectionResolver.Resolve(collection, localMods, disabledIds, catalogEntries, sessionInstalled = null)` | Builds `ModCollectionGraph` (nodes, edges, states) |
| `ModCollectionPlan.Build(graph, catalogEntries)` | `Install` items (id, version, source URL), `Enable`, `Missing`, `Mismatches` |
| `ModCollectionNodeState` | `Installed`, `Disabled`, `InCatalog`, `Missing`, `VersionMismatch` |

Example — resolve a collection and inspect its plan:

```csharp
var collections = ModCollectionStore.LoadAll();
var collection  = collections[0];
var catalog     = CatalogService.CachedEntries ?? new List<CatalogExportEntry>();

var graph = ModCollectionResolver.Resolve(
    collection,
    ModRegistry.Instance.AllDiscoveredMods,
    DisabledModsStore.Load(),
    catalog);

var plan = ModCollectionPlan.Build(graph, catalog);
foreach (var item in plan.Install)
    GameLogger.Log("MY_MOD", $"install {item.ModId} v{item.Version} from {item.SourceUrl}");
```

Writes (downloading, enabling) stay outside the plan: the UI passes
`plan.Install` to `ModDownloader.InstallAsync` and removes `plan.Enable` from
`DisabledModsStore`.

## Real examples

`mods/card_battle/collections/card_novel.json` — a story game assembled from
five mechanics (novel core, story, turn battle, cards, interface); the arrows
show real dependencies such as `card_battle → turn_battle_base → interface`.

`mods/tools/collections/dev_tools.json` — the developer toolset, with the
optional `validation` mechanic demonstrating the `optional` flag.

## Related

- [Configs — manifest.json](../configs/) — mod identity and `depends` syntax
- [Mod Catalog & Network Repository](../catalog/) — catalog schema, merging, checksums
- [Mod Load Order](../load-order/) — how `depends` affects loading
