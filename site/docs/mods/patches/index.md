# Patches

Patches let one mod add or override properties of entities defined in another mod — without rewriting their JSON files entirely.

- **`"Id": "EntityName"`** — apply only to the entity with this ID.
- **`"Id": "*"`** — wildcard: applies to **all** entities of this type. Its values overwrite the entity's own JSON values; a targeted patch (`"Id": "EntityName"`) is applied after the wildcard and wins over it.

Works for every entity kind the game defines — `character`, `item`, `action`, `skill`, `quest`, `recipe`, `status_effect` — keyed by kind, not by directory.

## Overview

Each mod can contain an optional `patches/` directory. Patch sources are
collected by `PatchResolver` (`Core/Mods/Resolvers/Impls/PatchResolver.cs`),
which scans **both** layouts per entity kind:

```filestree
mods/
└── my_mod/
    └── patches/
        ├── character.json          ← flat file: patches for characters
        ├── item.json               ← flat file: patches for items
        └── character/              ← directory form: any *.json inside is scanned too
            ├── player.json
            └── enemies.json
```

- Flat files: `patches/{kind}.json` (e.g. `patches/character.json`)
- Directory form: every `.json` inside `patches/{kind}/`

Both forms use the same format below; results from all mods are grouped by
target entity id. From C#, patches are collected via the static facade
`Patches.Collect(kind)`, which proxies through
`ModManager.Instance.GetResolver<PatchResolver>()` (results are cached per
kind until `ClearCache()`). The kind is the entity kind declared in the target
file's envelope (`"Kind": "character"`), so a character patch is never applied
to skills or actions.

## Format

Standard `{"Items": [...]}` wrapper. Each entry contains an `Id` and arbitrary properties to add:

```jsonc
{
  "Items": [
    {
      "Id": "Marao",                    // Id matching: applies only to entity with this exact Id
      "TestPatched": 42,                // Arbitrary property added via patch
      "TestDefault": 100                // Will be layered over wildcard values
    },
    {
      "Id": "*",                        // Wildcard: applies to ALL entities of the kind
      "TestDefault": -20,               // Overridden by specific patches (overwrites the entity's own value)
      "TestGlobalPatch": 1              // Global property applied to all entities
    }
  ]
}
```

## Property Resolution Order

When creating an entity, its properties are assembled in layers. Each subsequent layer overwrites keys from the previous ones:

| Layer | Source | Priority |
|---|---|---|
| 1 | Entity's own properties (from its JSON) | Base |
| 2 | Wildcard patches `"*"` (in mod load order) | Higher |
| 3 | Targeted patches by `Id` (in mod load order) | Highest |

Property names match in any letter case (`"health"` patches `Health`), the same way entity JSON is read.

## Patches from Different Mods

Patches **accumulate**: each mod adds its entries to the list for the target ID. When applied, they are iterated in load order, and a later mod overwrites keys of an earlier one.

Mod A:
```jsonc
// Wildcard patch adds Test and Strength to all characters
{ "Items": [{ "Id": "*", "Test": -20, "Strength": 10 }] }
```

Mod B (loaded after A):
```jsonc
// Mod B (loaded after A): overwrites Test but preserves Strength
{ "Items": [{ "Id": "*", "Test": 0 }] }
```

Result for all characters: `{ "Test": 0, "Strength": 10 }` — `Test` overridden, `Strength` preserved.

## Full Example

### Mod A — defines a character

```jsonc
// mods/mod_a/characters/player/hero.json
{
  "Id": "Hero",           // Unique entity identifier for patch targeting
  "Health": 100,          // Base properties defined by the entity itself
  "Attack": 20,
  "Strength": 15
}
```

### Mod B — adds patches

```jsonc
// mods/mod_b/patches/character.json
{
  "Items": [
    { "Id": "Hero", "Test": 50, "CritChance": 15 },                    // Specific patch: applies only to Hero
    { "Id": "*", "Test": -20, "CritChance": 5, "GlobalBuff": 10 }      // Wildcard: applies to every character; Hero gets its own values above
  ]
}
```

### Result for character `Hero`

| Property | Value | Source |
|---|---|---|
| `Strength` | 15 | Entity properties |
| `Test` | **50** | Specific patch `Hero` overrode wildcard `-20` |
| `CritChance` | **15** | Specific patch `Hero` overrode wildcard `5` |
| `GlobalBuff` | **10** | Wildcard patch `*` |

### Result for `Wolf` (no specific patch)

| Property | Value | Source |
|---|---|---|
| `Test` | **-20** | Wildcard `*` |
| `CritChance` | **5** | Wildcard `*` |
| `GlobalBuff` | **10** | Wildcard `*` |

## Items

Everything works identically for items. `ItemInfo` accepts arbitrary JSON keys via `[JsonExtensionData]`, which end up in `Item.Properties` (mirroring characters via `CharacterAttributes.Properties`).

```jsonc
// patches/item.json
{
  "Items": [
    { "Id": "food/ration", "Durability": 99, "SpecialProp": true },  // Specific item patch by Id
    { "Id": "*", "Durability": 50, "GlobalItemProp": "patched" }      // Wildcard: fallback for all items
  ]
}
```

## Test fixture

`mods/test_defaults_and_patches/` is a test fixture (`"fixture": true` in its manifest): `--all-mods` and games
never load it, so its wildcard patches cannot leak into other tests. The `property_patches` integration test
loads it explicitly and checks the merged entities:

```bash
godot --path . --headless -- --with-mods=test_defaults_and_patches --itest=property_patches
```

```filestree
mods/test_defaults_and_patches/
├── manifest.json
├── src/PropertyPatchesTest.cs   ← itest driver
└── patches/
    ├── character.json    ← Maraо: TestPatched=42, TestDefault=100; *: TestDefault=-20, TestGlobalPatch=1
    └── item.json         ← food/ration: Durability=99; *: Durability=50, GlobalItemProp=patched
```

## Array Operations & Public Helpers

Besides scalar last-wins overwrites, patch keys can carry array operation
suffixes — `$add`, `$remove`, and `$set` (parsed by
`ArrayMergeHelper.ParseArrayOperation`). These enable appending, removing, and
explicitly replacing array elements, including per-field merging of object
arrays matched by identity keys. See
[Array Operations & Identity Keys](arrays-and-identity.md) for the full
reference.

Mechanically, `JsonPatchMerger.Merge` applies all patches whose entry id is
`"*"` (the wildcard targeting every entity of the type) first, then the
patches matching the concrete entity id.

Public helpers on `JsonPatchMerger` (`Core/Mods/Patches/JsonPatchMerger.cs`)
are available to C# code:

| Member | Purpose |
|---|---|
| `DetectIdentityKey(JsonElement)` | Auto-detects the identity field of an object array from its JSON: `"Id"` first, then the first property ending in `Id`; returns `null` if none |
| `BuildMergeKeys<T>()` | Reflects over type `T` and returns a map of list-typed property → identity key for element types implementing `IMergeable` |

## Pages

- [Array Operations & Identity Keys](arrays-and-identity.md) — $add/$remove/$set, per-field merge, identity key detection
- [Scene Patches](scenes.md) — patching `.tscn` scenes with inherited scenes and `metadata/_patch_*` operations
