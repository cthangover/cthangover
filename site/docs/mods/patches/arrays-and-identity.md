# Array Operations & Identity Keys

## Array Operations

When a patch modifies a JSON **array** property (e.g. `Skills`, `Perks`, `Tags`), the suffix on the key determines how the arrays are merged:

| Key suffix | Behaviour |
|---|---|
| *(no suffix)* | **Overwrite** — replaces the entire array. Same semantics as scalar properties. |
| `$add` | **Add** — appends incoming elements to the existing array. If the property doesn't exist yet, creates it. |
| `$remove` | **Remove** — removes incoming elements from the existing array. If the property doesn't exist, no-op. |
| `$set` | **Overwrite** — explicit form, same as bare key. |

### Examples

**Base character definition** (the entity object inside a `Kind: "character"` file):
```jsonc
// Base entity definition (Items entry of a character file)
{ "Id": "Marao", "Skills": ["slash", "block"] }  // Id is the entity identifier; Skills is the array to patch
```

**Add elements** — a cooking mod extends skills:
```jsonc
// patches/character.json
// $add appends elements to the existing array (creates if missing)
{ "Items": [{ "Id": "Marao", "Skills$add": ["cook"] }] }
```
Result: `["slash", "block", "cook"]`

**Remove elements** — a balance patch removes a skill:
```jsonc
// $remove deletes matching elements from the array (no-op if missing)
{ "Items": [{ "Id": "Marao", "Skills$remove": ["block"] }] }
```
Result: `["slash", "cook"]`

**Replace entirely** — a weapon overhaul resets skills:
```jsonc
// $set replaces the entire array (explicit form, same as bare key)
{ "Items": [{ "Id": "Marao", "Skills$set": ["sword", "shield_bash"] }] }
```
Result: `["sword", "shield_bash"]`

**Overwrite (bare key)** — equivalent to `$set`:
```jsonc
// Bare key overwrites the entire array (same as $set); previous values discarded
{ "Items": [{ "Id": "Marao", "Skills": ["slash", "block"] }] }
```
Result: `["slash", "block"]` (previous values are discarded)

### Multi-layer example

All three operations can be combined across mod layers. Processing order follows the standard property pipeline (entity → wildcard `*` → specific `Id`):

```jsonc
// Entity base: ["slash", "block"]

// Wildcard patch: $add appends "dodge" to ALL entities first
{ "Items": [{ "Id": "*", "Skills$add": ["dodge"] }] }
// → ["slash", "block", "dodge"]

// Specific patch: $remove deletes "slash" from Marao only
{ "Items": [{ "Id": "Marao", "Skills$remove": ["slash"] }] }
// → ["block", "dodge"]
```

**No deduplication** is performed — if the same element is added twice, it appears twice.

### Universality

This system applies to **any** array property on any entity type — characters, items, and any mod-defined extras. There is no special-casing for specific property names.

```jsonc
// Works for any array property — no special-casing needed
{ "Items": [{ "Id": "sword/iron", "CraftingMaterials$add": ["steel_ingot"] }] }   // Specific item patch
{ "Items": [{ "Id": "*", "Tags$add": ["undead"] }] }                                // Wildcard: tags all entities
```

Scalar properties (int, float, bool, string) still use **last-wins** semantics — the suffix convention only affects JSON array values.

---

## Object Arrays with Identity Keys

> ⚠ **IMPORTANT CONVENTION: the identity field MUST be named `Id` (or end in `Id`, like `ItemId`, `QuestId`, `ActionId`).**
>
> For mod-defined (extra-property) arrays this field is **auto-detected** — no registration, no config, no C# code needed.
> For **typed** C# array properties there is a second path: if the element type
> implements `IMergeable`, its `MergeKeyProperty` value is used **first**, taking
> priority over the naming convention (see below).

For arrays of **objects** (e.g. `Loot`, `Rewards`, `Drops`), the system matches elements by an **identity field** instead of raw JSON text. This enables **per-field merge**: a patch can update a single property of an existing object without repeating all its fields.

### How identity detection works

Resolution order per property:

1. **Interface first** — if the entity type is known and the array property's
   element type implements `IMergeable` (`Core/Mods/Patches/IMergeable.cs`,
   a single `MergeKeyProperty` string property), the key comes from
   `MergeKeyProperty`. The map is built by `JsonPatchMerger.BuildMergeKeys<T>()`.
   Example: `LootEntry` (`Core/Characters/LootEntry.cs`) implements
   `IMergeable` with `ItemId` as its key, so the typed `Loot` property always
   merges by `ItemId`.
2. **Convention fallback** — when no interface-provided key exists, the key is
   auto-detected from the array JSON itself (table below).

| Array element type | Detection | Identity field |
|---|---|---|
| `{"Id": "gold", ...}` | First field named `Id` → detected | `Id` |
| `{"ItemId": "sword", ...}` | No `Id`, but field ending in `Id` → detected | `ItemId` |
| `{"QuestId": "q1", ...}` | Same logic | `QuestId` |
| `{"Name": "test"}` | Nothing matches → no identity | Raw text compare |
| `"string"` or `42` | Not an object → no identity | Raw text compare |

**The system prioritises exact `"Id"` over suffix matches.** If your object has both `Id` and `ItemId`, `Id` wins.

### Per-field merge example

```jsonc
// Base entity: object array with ItemId as identity key
{ "Loot": [
    {"ItemId": "food/wolf_meat", "MinCount": 1, "MaxCount": 1, "Probability": 100},    // Matched by ItemId
    {"ItemId": "resource/branches", "MinCount": 1, "MaxCount": 1, "Probability": 100}  // Matched by ItemId
]}

// Balance patch: uses $add/$remove on the object array
{ "Items": [{ "Id": "wolf_1",
    "Loot$add": [{"ItemId": "food/ration", "Probability": 50}],        // Adds new entry
    "Loot$remove": [{"ItemId": "resource/branches"}]                    // Removes by identity match
}]}

// Result:
// → food/wolf_meat: unchanged
// → resource/branches: removed (matched by ItemId)
// → food/ration: added as new entry
```

### Per-field property overwrite

When `$add` finds an existing element with the same identity value, the patch **overwrites only the specified properties**, preserving unmentioned fields from the base:

```jsonc
// Base: {"ItemId": "food/bread", "MinCount": 1, "MaxCount": 2, "Probability": 50}
// Patch: {"Loot$add": [{"ItemId": "food/bread", "Probability": 80}]}   // Matches by ItemId, merges only Probability
// Result: {"ItemId": "food/bread", "MinCount": 1, "MaxCount": 2, "Probability": 80}
//         (Probability overridden, MinCount/MaxCount preserved from base — per-field merge)
```

### Removing with identity key

The `$remove` operation only needs the identity field — other fields in the remove object are ignored:

```jsonc
// $remove only needs the identity field (ItemId); other fields are ignored
{ "Items": [{ "Id": "wolf_1", "Loot$remove": [{"ItemId": "food/wolf_meat"}] }] }
```

### Mod-defined arrays — no config needed

Any mod can introduce a new array of objects in a character or item JSON, and patches immediately work with per-field merge — as long as objects have an `Id` or `*Id` field:

```jsonc
// Mod A defines a quest with rewards (identity key: Id)
{
  "Id": "daily_hunt",
  "Name": "Daily Hunt",
  "Rewards": [
    {"Id": "gold", "Count": 100},     // Identity field "Id" detected automatically
    {"Id": "exp", "Count": 50}
  ]
}

// Mod B patches the rewards without knowing Mod A's internals:
{ "Items": [{ "Id": "daily_hunt",
    "Rewards$add": [{"Id": "gold", "Count": 200}],   // Matched by Id → overwrites Count, preserves Id
    "Rewards$remove": [{"Id": "exp"}]                 // Matched by Id → removes entire exp entry
}]}

// Result Rewards: [{"Id": "gold", "Count": 200}]
```

**No C# code, no interface, no registration** is needed for mod-defined
extra-property arrays — the `Id` / `*Id` convention is all that's required.
The `IMergeable` interface is an optional, higher-priority path for typed C#
array properties (see the resolution order above); it does not need to be
implemented for convention-based detection to work.
