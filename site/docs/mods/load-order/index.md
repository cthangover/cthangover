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
  "depends": ["core>=1", "interface>=1"]                   // Versioned dependency strings
}
```

A dependency string has the format: `modId[operator][version][?]`

#### Operators

| String | Meaning | Example |
|--------|---------|---------|
| `core>=1` | version ≥ 1.0.0 | Any core 1.x or higher |
| `core=1.0.0` | exact match | Strictly 1.0.0 |
| `core^1` | prefix match | 1.x.x — matches 1.0.0, 1.5.0, but not 2.0.0 |
| `core>1` | greater than | Any version above 1.0.0 |
| `core<2` | less than | Any version below 2.0.0 |
| `core<=1` | less or equal | Version ≤ 1.0.0 |
| `core` | any version | No version constraint |
| `core>=1?` | optional | If core is installed, must be ≥1; no error if absent |

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

- `core=1.0` does **not** match `1.0.5` — the third segment `"0"` ≠ `"5"`.
- `core=1.0.0` matches `1.0.0`.

`^` matches only the specified segments, ignoring the rest:

- `core^1.0` matches `1.0.0`, `1.0.5`, but **not** `1.1.0`.
- `core^1` matches `1.0.0`, `1.1.0`, `1.9.0`, but **not** `2.0.0`.

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

In the **Mods** menu (accessed from the main menu), each mod row has **▲▼** buttons.
Moving a mod up increases its priority (its files override mods below it);
moving a mod down decreases priority.

The manual order is saved per device in `user://mod_load_order.json` — it does **not**
transfer between devices or affect other players.

Move buttons are **disabled** when the move would violate a dependency:
you cannot move a mod above its own dependency, or below a mod that depends on it.

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
| **Scenario definitions** | Accumulated and sorted by their own `priority` and `after` metadata |
| **Property arrays** | Arrays in `ExtraProperties` (e.g. `Skills: ["a"]`) are **concatenated** across patches, not overwritten |

When a resource is overridden, a `Warning` is logged so you can inspect the conflict.
The **Mods** menu also shows a **Conflicts** section for the selected mod, listing
which resources it overrides and which mods override its resources.

## Version validation

On load, the game validates all dependency versions:

- If a required dependency is missing or its version is too low/too high → the mod is disabled with an error.
- `conflicts_with` is **not validated** — see the warning above; declared conflicts never disable anything.
- Disable reasons are **accumulated** (not overwritten) and shown in the UI status label.
- Cascade: if mod A is disabled, all mods depending on A are also disabled.

## UI overview

The **Mods** menu shows:

- **Order number** — position in the load order (leftmost column)
- **Mod name, version, thumbnail**
- **Enable/disable toggle** — disable cascades to dependents
- **▲▼ buttons** — move mod up/down in load order
- **State** — button is greyed out when movement is blocked by dependency constraints
- **Status** — shows all reasons a mod is disabled (version mismatch, missing dependency)
- **Conflicts** — list of resources overridden by or overridden in this mod

## Storage

Manual load order is saved to `user://mod_load_order.json` as a JSON array of mod IDs:

```jsonc
["core", "cooking", "hunting", "ui_overhaul"]              // Mod IDs in manual load order (first = highest priority)
```

New mods (not yet in the saved order) are placed alphabetically within their
dependency level — no manual position is assigned until the user explicitly reorders them.

Removed mods are cleaned up automatically on the next game launch.

## For mod creators

Declare dependencies with version constraints for safety:

```jsonc
{
  "id": "my_mod",                                          // Canonical mod identifier
  "depends": ["core>=1", "interface>=1"],                  // Versioned dependency strings
  "version": "1.0.0"                                       // Semver version of this mod
}
```

- Use `>=1` (not `>=1.0.0`) — trailing zeros are unnecessary due to zero-padding comparison.
- Declare `conflicts_with` for documentation/catalog purposes if your mod is incompatible with another — remember it is informational only and not enforced.
- Use `?` for optional soft dependencies.
