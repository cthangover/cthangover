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
  // Cache compiled assemblies to disk after first compilation
  "use_assembly_cache": false,
  // File/directory patterns excluded from catalog export checksum
  "catalog_ignore_patterns": [".*","bin","obj"],
  // Catalog JSON URLs (multiple are merged)
  "catalog_urls": [],
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
| `use_assembly_cache` | bool | Cache compiled mod assemblies to disk. Saves compilation time on restart |
| `catalog_ignore_patterns` | string[] | File/directory patterns excluded when computing `install_sha256` for catalog export. Default: `[".*"]` excludes all dot-prefixed entries (`.godot/`, `.import/`, `.gitignore`, etc.) |
| `catalog_urls` | string[] | URLs of catalog JSON files to fetch for the online mod browser. Multiple URLs are merged — metadata from highest version wins, `downloads` arrays are combined |
| `texture_extensions` | string[] | File extensions treated as textures |
| `audio_extensions` | string[] | File extensions treated as audio streams |
| `shader_extensions` | string[] | File extensions treated as shader files |
| `texture_groups` | string[] | Mod subdirectories scanned for textures. A file in `mods/{mod}/ui/image.png` is found because `"ui"` is listed here |
| `cache` | object | Cache configuration (see below) |

### `texture_groups`

Each entry in `texture_groups` corresponds to a mod subdirectory where textures are discovered:

| Group | Mod directory | Purpose |
|---|---|---|
| `ui` | `ui/` | UI textures, themes |
| `backgrounds` | `backgrounds/` | Scene backgrounds |
| `avatars` | `avatars/` | Character portraits |
| `icons` | `icons/` | UI icons |
| `items` | `items/` | Item sprites |
| `cards` | `cards/` | Battle action card images |
| `skills` | `skills/` | Skill card artwork |

### `cache` section

| Field | Type | Description |
|---|---|---|
| `root` | string | Cache storage path (Godot user path, e.g. `"user://mod_cache/"`) |
| `max_sizes` | object | Per-category cache size limits (LRU eviction) |

### `max_sizes` entries

Each factory type has a maximum cache capacity. When exceeded, the least recently used entry is evicted.

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

## How textures are discovered

1. `ModManager` scans each mod's directory
2. For each `texture_groups` entry (e.g. `"avatars"`), it looks in `mods/{mod}/avatars/`
3. Files matching `texture_extensions` (`.png`, `.jpg`, `.jpeg`) are registered in `ModRegistry`
4. Factories (`AvatarFactory`, `BackgroundFactory`, etc.) resolve paths through the registry with caching
