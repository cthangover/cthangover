# Mod Catalog & Network Repository

The mod catalog system lets players browse and install mods from online
repositories. A catalog is a JSON file hosted on a web server; the game
fetches it, merges entries from multiple URLs, and compares them against
locally-installed mods.

## Catalog JSON Schema

```jsonc
{
  "catalog": {                                             // Catalog metadata envelope
    "version": 1,                                          // Schema version of this catalog file
    "updated_at": "2026-10-23T07:10:38Z"                   // ISO-8601 timestamp of last server update
  },
  "mods": [                                                // List of mod entries in this catalog
    {
      "id": "live2d_base",                                 // Canonical mod identifier (matches manifest.json id)
      "name": "Live2D Cubism",                             // Fallback display name (used when no locale matches)
      "author": "ct",                                      // Author credit string
      "version": "1.0.0",                                  // Latest release version (semver)
      "description": "Live2D Cubism C# runtime: model loading, rendering, physics, animation, effects", // Fallback short description
      "locale": [                                          // Localized name and description entries
        {
          "lang": "en",                                    // Language code (ISO 639-1)
          "name": "Live2D Cubism",                         // Localized display name
          "description": "Live2D Cubism C# runtime: model loading, rendering, physics, animation, effects."
        },
        {
          "lang": "ru",
          "name": "Live2D Cubism",
          "description": "C#-рантайм Live2D Cubism: загрузка и отрисовка моделей, физика, анимации и эффекты."
        }
      ],
      "category": "library, visual",                       // Category slugs: story, characters, gameplay, visual, audio, ui, locale, library, engine, overhaul, tools, editor, debug, other
      "tags": ["live2d", "base", "library", "visual"],     // Search/filter tags
      "icon_url": "https://git.cthangover.ru/cthangover/mod_live2d_base/raw/branch/master/mod.jpg", // Full URL to preview icon (PNG or JPEG)
      "depends": ["ui_base>=1"],                           // Versioned dependency strings
      "conflicts_with": [],                                // Mutually exclusive mod IDs
      "downloads": [                                       // Version-specific download entries with mirrors
        {
          "version": "1.0.0",                              // Semver version of this download entry
          "urls": [                                        // Mirror URLs tried in order
            "https://git.cthangover.ru/cthangover/mod_live2d_base/releases/download/v1.0.0/mod_live2d_base-v1.0.0.zip"
          ],
          "size_bytes": 48031383,                          // File size of the zip for progress display
          "archive_sha256": "9d20f4eda7a3ba7b380fefa5964098dc696b3c8399049614e4cd4a03cc6bb1d6", // SHA256 of the .zip file (integrity check after download)
          "install_sha256": "abece6c284f36c35e1b975025dc7b465009f67ce42870c9ad896c5816bb01df4",  // SHA256 of extracted mod content (modification detection)
          "changelog": null                                // Optional changelog for this specific version
        }
      ],
      "status": "stable",                                  // stable, beta, deprecated, or archived
      "min_game_version": null,                            // Minimum compatible game version (semver)
      "max_game_version": null                             // Maximum compatible game version, or null for no upper bound
    }
  ]
}
```

### Root envelope

| Field | Type | Description |
|---|---|---|
| `catalog.version` | int | Schema version of this catalog file |
| `catalog.updated_at` | string | ISO-8601 timestamp of last server update |
| `mods` | array | List of mod entries |

### Per-mod entry

| Field | Type | Description |
|---|---|---|
| `id` | string | Canonical mod identifier (must match the mod's `manifest.json` `id`) |
| `name` | string | Fallback display name (used when no `locale` entry matches the player's language) |
| `author` | string | Author credit |
| `version` | string | Semver version of the latest release (e.g. "1.0.0") |
| `description` | string | Fallback short description (used when no `locale` entry matches the player's language) |
| `locale` | object[] | Localized name/description entries for different languages (see below) |
| `category` | string | Category slug or comma-separated combination of slugs (e.g. `"library, gameplay"`): `story`, `characters`, `gameplay`, `visual`, `audio`, `ui`, `locale`, `library`, `engine`, `overhaul`, `tools`, `editor`, `debug`, `other` |
| `tags` | string[] | Search/filter tags |
| `icon_url` | string | Full URL to preview icon (PNG or JPEG) |
| `depends` | string[] | Versioned dependency strings (e.g. `"cthangover_game>=1"`) |
| `conflicts_with` | string[] | Mutually exclusive mod IDs |
| `downloads` | object[] | Version-specific download entries with mirrors |
| `downloads[].version` | string | Semver version of this download entry |
| `downloads[].urls` | string[] | Mirror URLs for the .zip archive (tried in order) |
| `downloads[].size_bytes` | long | File size of the zip (for progress display) |
| `downloads[].archive_sha256` | string | SHA256 of the .zip file itself (integrity check after download) |
| `downloads[].install_sha256` | string | SHA256 of the extracted mod content (modification detection) |
| `downloads[].changelog` | string | Optional changelog describing what changed in this specific version |
| `status` | string | `stable`, `beta`, `deprecated`, or `archived` |
| `min_game_version` | string | Minimum compatible game version (semver) |
| `max_game_version` | string | Maximum game version, or null |
| `source_urls` | string[] | *Written by the game, not by catalog authors*: filled in during merge with every catalog URL that published the entry, and persisted in `user://catalog_cache.json` |
| `variant_conflict` | bool | *Written by the game*: set when two catalogs publish the same id/version/title with different content hashes — the entries are kept separate instead of merged so the UI can warn about a likely modified build |

### `locale[]` sub-fields

Each entry in the `locale` array has three fields:

| Field | Type | Description |
|---|---|---|
| `lang` | string | Language code (ISO 639-1, e.g. `"en"`, `"ru"`) |
| `name` | string | Localized display name |
| `description` | string | Localized short description |

Resolution order: exact language match → fallback to `"en"` → first entry in the array → top-level `name`/`description`.

## Checksums

Two SHA256 hashes are stored per download entry (per version):

**`archive_sha256`** hash of the .zip archive as it sits on the server.
The game verifies this after downloading to detect transfer corruption.

**`install_sha256`** hash of the mod's extracted content, computed by:
1. Enumerating all files in the mod (recursively)
2. Excluding dot-prefixed files/directories (`.godot/`, `.import/`,
   `.gitignore`) and patterns from `catalog_ignore_patterns` in
   `mod_config.json`
3. Sorting files by path (case-insensitive)
4. For each file: `SHA256(path_bytes + content_bytes)`
5. Final: `SHA256(file_hash_1 + file_hash_2 + ...)`

When the game detects that a locally-installed mod's `install_sha256`
differs from the catalog value, it shows a "Modified locally" warning.
Overwriting such a mod from the network would lose the player's changes.

## Configuration

Catalog URLs are configured in `config/mod_config.json`:

```jsonc
{
  "catalog_urls": [                                        // URLs to fetch catalog.json from (merged in order)
    "https://example.com/mods/catalog.json",
    "https://mirror.example.com/mods/catalog.json"
  ],
  "catalog_ignore_patterns": [".*", "bin", "obj"]          // Glob patterns to exclude from install_sha256 computation
}
```

The **code default** for `catalog_ignore_patterns` is `[".*"]` (`ModConfig.CatalogIgnorePatterns`), which excludes all dot-prefixed entries. The `config/mod_config.json` shipped with the game extends this to `[".*", "bin", "obj"]` to also skip build output directories.

Multiple URLs are merged — when the same mod appears in two catalogs,
the entry with the highest `version` wins for metadata (name, description,
category, etc.), while `downloads` arrays from all sources are merged:
same-version entries have their `urls` lists combined (deduplicated).

The game caches fetched catalogs to `user://catalog_cache.json`.
Open the Network tab in the mods menu (Mods > Network) and press
**Refresh** to clear the cache and re-fetch.

Architecture note: the catalog system provides the *data source* for an HTTP catalog scanner. For details on how a custom scanner turns catalog JSON into `DiscoveredModEntry` items that the kernel can load, see [Resources — Scanners](../resources/scanners/).

## Exporting a mod to the catalog

From the mods menu, select a locally-installed mod and click
**Export to Catalog**. This opens a dialog where you fill in the
download URL, category, tags, and changelog, then generates:

- A `.zip` archive of the mod (if it was a folder)
- A `.json` catalog entry file alongside the archive

Categories are pre-checked from the mod's `manifest.json` `category`
field (falling back to `other` when absent); several categories can be
toggled at once.

Both files go into the mods root directory. The JSON snippet can be
copied to clipboard and pasted into the catalog JSON you host online.

Ignore patterns for the SHA256 computation can be customized in
`mod_config.json` via `catalog_ignore_patterns`. The code default `[".*"]`
excludes all dot-prefixed entries; the shipped `config/mod_config.json`
adds `"bin"` and `"obj"`.

## Building releases of all mods

`build_mod_releases.py` in the repository root (wrappers `build_mod_releases.bat`, `build_mod_releases.sh`)
does the same as **Export to Catalog** for every folder mod at once and writes to `releases/`:

- `mod_<id>-v<version>.zip` — the mod's content files at the archive root, hidden entries (`.git/`, …) left out;
  entries are sorted and dated 1980-01-01, so an unchanged mod rebuilds with the same `archive_sha256`;
- `mod_<id>-v<version>.json` — its catalog entry;
- `gitea/catalog.json` and `github/catalog.json` — the whole catalog for each catalog repository, with that
  host's release download URL and raw `mod.jpg` icon URL.

Entries come from `manifest.json` (name, author, version, description, category, depends, conflicts_with) and
the mod's `mod_<id>_name` / `mod_<id>_desc` keys in every language; tags are the id's words plus the categories;
`install_sha256` uses `catalog_ignore_patterns` from `config/mod_config.json`. Options: `--only=a,b`,
`--skip=a,b`, `--hosts=gitea,github`, `--out=<dir>`. Upload each archive to the mod repository's release
`v<version>` and the catalog file to its catalog repository.

## Mod status display

In the Network tab, each remote mod shows one of four statuses:

| Status | Meaning |
|---|---|
| **Installed** | Same version, same content hash |
| **Update available** | Newer version in catalog |
| **Not installed** | No local copy found |
| **Modified locally** | Same version, but `install_sha256` differs player edited the mod |
