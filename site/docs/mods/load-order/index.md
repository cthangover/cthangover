# Mod Load Order

The mod load order determines which mod's assets and data take priority when two or more
mods define the same resource. The system combines automatic dependency-based ordering with optional manual
user overrides.

## How ordering works

The game builds the final load order from three inputs, applied in strict priority:

```
1. depends (manifest.declared) → 2. Manual order (user preference) → 3. Alphabetical (deterministic fallback)
```

### Layer 1: `depends` — mandatory

Every mod may declare dependencies in `manifest.json` using **versioned dependency strings**:

```jsonc
{
  "id": "cooking",                                         // Canonical mod identifier
  "depends": ["cthangover_game>=1", "ui_base>=1"]                   // Versioned dependency strings
}
```

A dependency string has the format: `modId[operator][version][?]`

#### Operators

| String | Meaning | Example |
|--------|---------|---------|
| `cthangover_game>=1` | version ≥ 1.0.0 | Any core 1.x or higher |
| `cthangover_game=1.0.0` | exact match | Strictly 1.0.0 |
| `cthangover_game^1` | prefix match | 1.x.x — matches 1.0.0, 1.5.0, but not 2.0.0 |
| `cthangover_game>1` | greater than | Any version above 1.0.0 |
| `cthangover_game<2` | less than | Any version below 2.0.0 |
| `cthangover_game<=1` | less or equal | Version ≤ 1.0.0 |
| `cthangover_game` | any version | No version constraint |
| `cthangover_game>=1?` | optional | If core is installed, must be ≥1; no error if absent |

Version comparison is **segment-based** with zero-padding: `1` equals `1.0` equals `1.0.0`.
Segments may contain letters (e.g. `1a`, `alpha-3`). Each segment is compared as integer
if both sides are parseable, otherwise as string (`Ordinal`).

#### Optional dependencies (`?`)

Appending `?` to a dependency makes it **optional**:

```jsonc
{ "depends": ["optional_mod>=1?"] }                        // ? suffix makes this dependency optional
```

- If the optional mod is **not installed** → no error, the dependency is skipped.
- If the optional mod **is installed** → its version must satisfy the constraint.

This is useful for mods that want to react to another mod being present without
requiring it.

#### Strict equality (`=`) vs prefix match (`^`)

`=` requires **all** segments to match (with zero-padding):

- `cthangover_game=1.0` does **not** match `1.0.5` — the third segment `"0"` ≠ `"5"`.
- `cthangover_game=1.0.0` matches `1.0.0`.

`^` matches only the specified segments, ignoring the rest:

- `cthangover_game^1.0` matches `1.0.0`, `1.0.5`, but **not** `1.1.0`.
- `cthangover_game^1` matches `1.0.0`, `1.1.0`, `1.9.0`, but **not** `2.0.0`.

This follows the convention used by npm, Cargo, pip, Factorio, and Minecraft Fabric.

#### `conflicts_with`

Mods can declare mutual incompatibility:

```jsonc
{
  "id": "ff_battle",                                       // Canonical mod identifier
  "conflicts_with": ["card_battle"]                        // Declared incompatible mod IDs — informational only
}
```

!!! warning "Declarative metadata only"
    `conflicts_with` is parsed from the manifest and copied into catalog
    exports, but it is **never enforced at runtime**. Declaring a conflict
    disables nothing, blocks nothing, and shows no UI warning — two mods
    that declare each other as conflicting will still load and run
    together. Treat this field purely as informational metadata (e.g. for
    catalogs and mod platforms); do not rely on it to prevent broken
    combinations.

### Layer 2: Manual order — user preference

The manual order is a saved per-device preference in `user://mod_load_order.json` — it does **not**
transfer between devices or affect other players. Within the same dependency level, mods earlier
in the saved list load earlier, so a later-loaded mod still overrides them under the "last wins"
rule below.

The ordering machinery exists in the kernel (`ModLoadOrderStore` with `MoveUp`/`MoveDown`/`Remove`
helpers, `ModRegistry.InvalidateOrder` to recompute the cached order), but the current
**Mods & Collections** screen does not expose reorder buttons — the row order it displays is the
computed order. A mod whose dependency is missing is dropped from the registry entirely rather
than reordered, so the saved list can never place a dependent above its dependency.

### Layer 3: Alphabetical — deterministic fallback

When two mods have no dependency relationship and no manual order preference,
they are sorted alphabetically by their mod ID. This guarantees the same result
on every device and operating system.

## Conflict resolution strategy

The game uses **"last wins"** — the mod later in the load order overrides earlier mods:

| Asset type | Resolution |
|---|---|
| **Textures** | Same-name texture from a later mod overrides the earlier one |
| **JSON data** (characters, items, recipes, quests, interactives) | Same `ID` entry from a later mod overrides the earlier one |
| **Files** (audio, prefabs, etc.) | Same-path file from a later mod overrides the earlier one |
| **Shaders** | Same-name shader from a later mod overrides the earlier one |
| **Locale strings** | Same key from a later translation file overrides the earlier one |
| **Scene definitions** | Multiple mods can contribute to the same scene — they accumulate |
| **Scenarios** | Different names accumulate and run by their own `priority` and `after` metadata; the **same file name** from a later mod replaces the earlier scenario (even if it targets another scene) |
| **Property arrays** | Arrays in `ExtraProperties` (e.g. `Skills: ["a"]`) are **concatenated** across patches, not overwritten |

### Add or replace — the mod decides

Overriding is a right of a mod, not a duty. Nothing is replaced unless a mod
asks for it, and it asks in one way only:

- **To add**, use your own name: a new scenario for someone else's scene, a new
  item, a new background. It works next to the existing content.
- **To replace**, ship the **same name** (texture, file path, entity `ID`,
  scenario file name) **and declare `depends` on the mod that owns it**. The
  dependency puts your mod later in the load order, so your version wins and
  the original files stay untouched.
- **Same name without a dependency is a mistake**: two unrelated mods have no
  defined order between them, so the winner would be arbitrary. The
  resource atlas (the `resource_atlas` mod, `--resource-atlas`) reports it as an error
  ("one key provided by mods not linked by depends").

When a resource is overridden, a `Warning` is logged so you can inspect the conflict.
Overrides are also recorded in `ModConflictRegistry` (resource type, winning and overridden
mod id, whether arrays were concatenated); the registry is queryable from code and tests,
but the current Mods & Collections screen does not render a conflicts list.

## Version validation

On load, the game validates all dependency versions:

- If a required dependency is missing or its version is too low/too high → the mod is disabled with an error.
- `conflicts_with` is **not validated** — see the warning above; declared conflicts never disable anything.
- Disable reasons are **accumulated** (not overwritten) and shown in the UI status label.
- Cascade: if mod A is disabled, all mods depending on A are also disabled.

## UI overview

The **Mods** tab of the Mods & Collections screen shows one card per discovered mod:

- **Mod name, author, version, category and description**
- **Enable/disable toggle** — disable cascades to dependents; enabling re-enables the dependencies it needs
- **Status** — installed / update available / not installed / modified locally when a catalog entry exists
- **Delete** — offered only for mods whose files live in a writable mods root (`user://mods`);
  takes effect after a restart, like every other mod-set change

Mods are displayed in the computed load order (dependencies first). There is currently
no UI to reorder mods manually — see Layer 2 above.

## Storage

Manual load order is saved to `user://mod_load_order.json` as a JSON array of mod IDs:

```jsonc
["cthangover_game", "cooking", "hunting", "ui_overhaul"]              // Mod IDs in manual load order (first = highest priority)
```

New mods (not yet in the saved order) are placed alphabetically within their
dependency level — no manual position is assigned until the user explicitly reorders them.

Removed mods are cleaned up automatically on the next game launch.

## For mod creators

Declare dependencies with version constraints for safety:

```jsonc
{
  "id": "my_mod",                                          // Canonical mod identifier
  "depends": ["cthangover_game>=1", "ui_base>=1"],                  // Versioned dependency strings
  "version": "1.0.0"                                       // Semver version of this mod
}
```

- Use `>=1` (not `>=1.0.0`) — trailing zeros are unnecessary due to zero-padding comparison.
- Declare `conflicts_with` for documentation/catalog purposes if your mod is incompatible with another — remember it is informational only and not enforced.
- Use `?` for optional soft dependencies.
