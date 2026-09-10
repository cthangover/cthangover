# Versioning & Compatibility

## Backward compatibility

Old saves (without `ModData`, `SaveId`, `Timestamp`, `CoreVersion` fields) will fail to load because the `"__core__"` slot is required. The old `SaveData` format must be migrated manually or via a one-time converter tool.

## Version comparison

During phase-1 validation the core compares `ModMetadata.ModVersion` from the save with `IModInfo.Version` from the installed mod. Both values originate from the `"version"` field in `manifest.json`.

### Comparison strategy

The core uses **per-segment comparison** via `VersionHelper.Compare` (Core/Utils/VersionHelper.cs). There is no `System.Version` parsing anywhere — the algorithm is:

1. Both strings are trimmed and split on `.`. The shorter segment array is padded with `"0"` to equal length (`"1.0"` equals `"1.0.0"`).
2. Segments are compared **pairwise, left to right**. For each position: if *both* segments parse as `int`, they are compared numerically (handles `"9" < "10"`); otherwise that segment pair is compared with an ordinal string comparison (`String.CompareOrdinal`).
3. The first non-zero comparison decides the result.

Consequences: a segment like `"1b"` does **not** parse as int, so it compares **ordinally**, not numerically. Mixed versions are fine — each segment independently picks numeric or ordinal comparison.

### Supported version formats

| Format | Example | Comparison |
|---|---|---|
| Numeric segments | `1.0.0`, `2.5.3.1`, `1.0` | Numeric per segment; shorter string padded with `"0"` |
| Letter in a segment | `1b.2.0` | That segment compares ordinally (`"1b"` vs `"10"` is ordinal, not numeric) |
| Prefixed tags | `v1`, `v2.0` | Ordinal per segment (`"v2" > "v10"` ordinally) |
| Codename-style | `Enigma1.5`, `Enigma2.0` | Ordinal per segment |
| Alpha/beta tags | `alpha-1`, `beta-3` | Ordinal per segment |

### Validation rules

| Condition | Result |
|---|---|
| No version in save or installed mod | Skip check |
| `save.Version > installed.Version` | **Abort** — save was made by a newer mod; downgrade not supported |
| `save.Version < installed.Version` | **Warning** — save is from an older mod version; the mod must handle migration in `OnLoad` |
| `save.Version == installed.Version` | OK |

## Adding version to manifest.json

Every mod's `manifest.json` must include a semver version string:

```jsonc
{
  "name": "My Mod",                                        // Human-readable mod name
  "version": "1.0.0",                                      // Semver version — used for save validation and migration detection
  "author": "...",                                         // Author credit
  "description": "...",                                    // Short description
  "sources": ["src/**/*.cs"]                              // Glob patterns for C# source files
}
```

This value flows through `ModManifest.Version` → `IModInfo.Version` → `ModMetadata.ModVersion` and is used for the save-system version checks above.

## Important rules for mod authors

- **Add `"version"` to `manifest.json`** — the core uses it for save validation and migration detection.
- **One `IModSaveable` per mod** — create a single implementation in your assembly.
- **Never set `ModId`, `ModName`, or `ModVersion`** — the core pre-fills them. Set `MinCoreVersion`, `IsRequired`, `CustomData`, and `Data`.
- **Prefer `bool OnLoad(ModSlot, out string)`** — explicit contract, no exceptions. Override `OnLoadVoid` only for legacy/compatibility.
- **Mods must not depend on each other's save data** — the dispatch order is undefined.
- **Version your `DataBlob` blocks** — use separate blobs per logical entity, version each independently.
- **Handle missing keys** — use `TryGetValue` and provide defaults.
