# Mod Config — mod_config.json

Mod system configuration loaded from `config/mod_config.json`. Controls file discovery, caching, and assembly compilation.

## File location

```filestree
config/
└── mod_config.json
```

## Full schema

```jsonc
{
  // Cache compiled assemblies to disk after first compilation (code default: true)
  "use_assembly_cache": true,
  // File/directory patterns excluded from catalog export checksum
  "catalog_ignore_patterns": [".*","bin","obj"],
  // Catalog JSON URLs (multiple are merged)
  "catalog_urls": [],
  // Optional private catalog URL -> bearer token file, never the token itself
  "catalog_token_files": {},
  // Explicit development-time mod unit tests
  "run_mod_tests": false,
  "mod_test_exclude": [],
  // File extensions treated as textures
  "texture_extensions": [".png", ".jpg", ".jpeg"],
  // Audio file extensions (OGG and WAV)
  "audio_extensions": [".ogg", ".wav"],
  // Shader file extensions
  "shader_extensions": [".gdshader", ".gdshaderinclude"],
  // Mod subdirectories scanned for textures
  "texture_groups": ["ui", "backgrounds", "avatars", "icons", "items", "cards", "effects", "skills"],
  // Resource cache configuration
  "cache": {
    // Cache root directory (Godot user:// path)
    "root": "user://mod_cache/",
    // Per-category cache size limits (LRU eviction on overflow)
    "max_sizes": {
      "avatars": 64,
      "backgrounds": 64,
      "items": 64,
      "ui/icons": 64,
      "ui": 64,
      "music": 64,
      "sounds": 64,
      "effects": 64,
      "actions": 128,
      "characters": 128,
      "recipes": 128
    }
  }
}
```

## Section reference

### Top-level fields

| Field | Type | Description |
|---|---|---|
| `use_assembly_cache` | bool | Cache compiled mod assemblies to disk (`true` by default in code). Set to `false` to delete cached DLLs and recompile on every launch — useful during mod development |
| `catalog_ignore_patterns` | string[] | File/directory patterns excluded when computing `install_sha256` for catalog export. Default: `[".*"]` excludes all dot-prefixed entries (`.godot/`, `.import/`, `.gitignore`, etc.) |
| `catalog_urls` | string[] | URLs of catalog JSON files to fetch for the online mod browser. Multiple URLs are merged — metadata from highest version wins, `downloads` arrays are combined |
| `catalog_token_files` | object | Exact catalog URL to token-file path; [private sources](../catalog/) are fetched without redirects |
| `run_mod_tests` | bool | Run mod unit tests (default false); `--run-tests` also enables explicit test mode |
| `mod_test_exclude` | string[] | Mod IDs excluded from that test run |
| `texture_extensions` | string[] | File extensions treated as textures |
| `audio_extensions` | string[] | DTO field; runtime `ModConfig.Load` currently keeps the code-default `.ogg`/`.wav` list instead of copying this parsed field |
| `shader_extensions` | string[] | File extensions treated as shader files |
| `texture_groups` | string[] | Mod subdirectories scanned for textures. A file in `mods/{mod}/ui/image.png` is found because `"ui"` is listed here |
| `cache` | object | Cache configuration (see below) |

### `texture_groups`

Each entry in `texture_groups` corresponds to a mod subdirectory where textures are discovered. The code default is `["ui", "backgrounds", "avatars", "icons", "items", "characters", "effects", "skills"]`; the shipped `config/mod_config.json` replaces `characters` with `cards`:

| Group | Mod directory | Purpose |
|---|---|---|
| `ui` | `ui/` | UI textures, themes |
| `backgrounds` | `backgrounds/` | Scene backgrounds |
| `avatars` | `avatars/` | Character portraits |
| `characters` | `characters/` | Character sprites (code default only) |
| `icons` | `icons/` | UI icons |
| `items` | `items/` | Item sprites |
| `cards` | `cards/` | Battle action card images (shipped config) |
| `effects` | `effects/` | Effect textures |
| `skills` | `skills/` | Skill card artwork |

### `cache` section

| Field | Type | Description |
|---|---|---|
| `root` | string | Cache storage path (Godot user path, e.g. `"user://mod_cache/"`) |
| `max_sizes` | object | Per-category cache size limits (LRU eviction) |

### `max_sizes` entries

Each factory type has a maximum cache capacity. When exceeded, the least recently used entry is evicted. The code default for `max_sizes` is an **empty dictionary** — the values below ship in `config/mod_config.json` and override the fallback capacities each factory passes to `CacheConfig.GetCacheSize`.

| Factory | Default | Caches |
|---|---|---|
| `avatars` | 64 | Avatar textures |
| `backgrounds` | 64 | Background textures |
| `items` | 64 | Item textures |
| `ui/icons` | 64 | UI icon textures |
| `ui` | 64 | General UI textures |
| `music` | 64 | Music audio streams |
| `sounds` | 64 | Sound effect audio |
| `actions` | 128 | Character action definitions |
| `characters` | 128 | Character definitions |
| `recipes` | 128 | Recipe definitions |

The first user edit of the catalog source list creates `user://catalog_servers.json`. That list then supplies enabled catalog URLs and token paths, including an intentionally empty list; shipped `catalog_urls` is the seed before the first edit. See [Catalog configuration](../catalog/).

## How textures are discovered

1. `ModManager` scans each mod's directory
2. For each `texture_groups` entry (e.g. `"avatars"`), it looks in `mods/{mod}/avatars/`
3. Files matching `texture_extensions` (`.png`, `.jpg`, `.jpeg`) are registered in `ModRegistry`
4. Factories (`AvatarFactory`, `BackgroundFactory`, etc.) resolve paths through the registry with caching
