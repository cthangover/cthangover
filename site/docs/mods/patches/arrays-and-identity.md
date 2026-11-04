# Array operations and declared identity

The patch engine operates on JSON trees at any depth. Object properties merge recursively; scalars and bare arrays replace the previous value. A null patch value removes the property, so deserialization sees its default. `Key$set` replaces even an object wholesale.

| Patch key | Behaviour |
|---|---|
| `Skills` or `Skills$set` | Replace the array |
| `Skills$add` | Append; with a declared identity, merge an existing matching element instead |
| `Skills$remove` | Remove by declared identity, otherwise by semantic JSON value |

```json
{"Items": [{"Id": "Marao", "Skills$add": ["cook"], "Skills$remove": ["block"]}]}
```

Unkeyed additions are not deduplicated. JSON object property order does not change semantic equality. A missing array can be created by `$add`; removing from a missing array does nothing. Incompatible operations are diagnosed with a path inside the entity.

## Identity is explicit

The engine does **not** guess an identity from fields named `Id`, `ItemId` or `Name`. Keys come from these sources, in priority order:

1. An explicit operation value `{"$key": ..., "$items": [...]}`.
2. `[MergeKey("Id")]` on the typed DTO's list property.
3. `IMergeable.MergeKeyProperty` on the typed list element.

The type walk follows nested objects, lists and dictionaries, respecting JSON property names. Unknown extension-data fields have no DTO metadata; use an explicit `$key` there. A key can be one property or a composite list. Key names and string values compare case-insensitively.

For example, a mod-defined reward array must declare its key when updating one reward:

```json
{
  "Items": [{
    "Id": "daily_hunt",
    "Rewards$add": {"$key": "Id", "$items": [{"Id": "gold", "Count": 200}]},
    "Rewards$remove": {"$key": "Id", "$items": ["exp"]}
  }]
}
```

`$add` deep-merges only supplied fields into an element with the same key; it appends when the key is absent. `$remove` accepts an object with the key fields, or the bare value for a single-property key. A composite operation uses, for example, `"$key": ["Layer", "X", "Y"]`, and supplies all key fields in each item.

Known declarations include RPG object `Parts` and `States`, map `Strokes` and `Events` (key `Id`), and map `Lights` (key `id`). `LootEntry` uses its `IMergeable` key `ItemId`. Shop offers do not declare a key: use `"Sells$add": {"$key": "Item", "$items": [...]}` to update an existing price rather than append a duplicate.

## Layers applied later

`Variants`, placement overrides and state components can store patches that will only apply when the effective object is built. `[PatchLayer(typeof(...))]` tells the engine to compose such layers rather than execute their operations prematurely. For example, adding to `Interact.Commands` in a placement patch must still add to the object's commands when that placement is resolved.

Use the editors' field patches or preserve the stored layer structure; a fully resolved object is not the same as its stored inheritance layer.

## Deletion, isolation and API

An entity patch with `"$delete": true` removes the entity. A null property removes that property; it does not delete the entity. Wildcard patches run first, then targeted patches, in mod order within each group. The resolver applies each patch to a copy and verifies deserialization. A failing patch is logged with its mod, file, entity and JSON path and is skipped without discarding other valid patches.

`JsonPatchMerger.Apply(JsonObject target, JsonObject patch, Type targetType)` mutates a JSON object; callers applying it directly must handle rollback themselves. `JsonPatchMerger.Merge(...)` applies grouped patches and accepts an optional target DTO type. `MergeTypeMap` supplies nested type/key metadata. The former `DetectIdentityKey` and `BuildMergeKeys<T>` helpers are not current APIs.

Implementation: `Core/Mods/Patches/JsonPatchMerger.cs`, `Core/Mods/Patches/MergeTypeMap.cs`, `Core/Mods/Patches/PatchLayerComposer.cs` and `Core/Mods/Resolvers/Impls/PatchResolver.cs`.
