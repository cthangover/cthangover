# Game Config — game_config.json

Default game configuration loaded from `config/game_config.json` at startup. Defines audio, display, language, and logging settings.

## File location

```filestree
config/
└── game_config.json
```

## Full schema

```jsonc
{
  // Audio defaults
  "audio": {
    "sounds_enabled": true,       // Sound effects master toggle
    "musics_enabled": true,       // Music master toggle
    "ambient_enabled": true,      // Ambient sounds master toggle
    "sounds_volume": 100,         // SFX volume (0–100)
    "musics_volume": 15,          // Music volume (0–100)
    "ambient_volume": 100         // Ambient volume (0–100)
  },
  // Locale code (UI language)
  "language": "en-en",
  // Display settings
  "display": {
    "window_mode": 3,             // 0=Windowed, 1=Min, 2=Max, 3=Fullscreen, 4=Exclusive
    "resolution_width": 1920,     // Width in pixels
    "resolution_height": 1080,    // Height in pixels
    "current_screen": 0,          // Monitor index (0-based)
    "vsync_enabled": false,       // Vertical sync
    "scale": 1                    // UI window scale factor
  },
  // Logging configuration
  "logging": {
    "enabled": true,              // Master logging on/off
    "console_enabled": true,      // Write logs to console
    "minimum_level": "Debug",     // Min level: Debug, Message, Warning, Error
    "enabled_categories": ["SAVE", "MODS", "..."]
  }
}
```

## `audio` section

| Field | Type | Description |
|---|---|---|
| `sounds_enabled` | bool | Master toggle for sound effects |
| `musics_enabled` | bool | Master toggle for music |
| `ambient_enabled` | bool | Master toggle for ambient sounds |
| `sounds_volume` | int | Sound effects volume (0–100) |
| `musics_volume` | int | Music volume (0–100) |
| `ambient_volume` | int | Ambient volume (0–100) |

## `display` section

| Field | Type | Values | Description |
|---|---|---|---|
| `window_mode` | int | 0=Windowed, 1=Min, 2=Max, 3=Fullscreen, 4=Exclusive | Window display mode (Godot `DisplayServer.WindowMode`) |
| `resolution_width` | int | pixels | Target screen width |
| `resolution_height` | int | pixels | Target screen height |
| `current_screen` | int | 0-indexed | Monitor/display index |
| `vsync_enabled` | bool | true/false | Vertical sync |
| `scale` | int | multiplier | UI/window scaling factor |

## `language` section

| Field | Type | Description |
|---|---|---|
| `language` | string | Locale code (e.g. `"en-en"`, `"ru-ru"`) |

## `logging` section

| Field | Type | Description |
|---|---|---|
| `enabled` | bool | Master logging toggle |
| `console_enabled` | bool | Write logs to console output |
| `minimum_level` | string | Minimum log level: `"Debug"`, `"Message"`, `"Warning"`, `"Error"` |
| `enabled_categories` | string[] | Active log channels (see below) |

### Available log categories

Categories are plain strings passed to `GameLogger.Log(category, ...)` — **no `LOG_` prefix**.

| Category | Covers |
|---|---|
| `AUDIO` | Audio playback |
| `BATTLE` | Battle engine events |
| `BATTLE_UI` | Battle interface events |
| `CATALOG` | Remote mod catalog refresh/cache |
| `CATALOG_EXPORT` | Catalog export dialog |
| `CACHE` | Mod cache (shader extraction) |
| `COND` | Condition evaluation |
| `CONFIG` | Configuration loading |
| `CONVERTER` | Scenario converter |
| `DOWNLOADER` | Mod downloads |
| `DUPLICATE` | Duplicate singleton detection warnings |
| `EVENT_BUS` | Mod event bus subscriptions |
| `FACTORY` | Factory loading |
| `GAME` | Game lifecycle |
| `ICON_CACHE` | Catalog icon downloads/cache |
| `GDS_MOD` | GDScript mod loading |
| `INTERACTIVE` | Interactive objects/layers |
| `LANG` | Localization loading |
| `MOD_COLLECTIONS` | Collection store/resolver/plan |
| `MOD_HUB` | Mods & Collections hub screens |
| `MOD_MANAGER` | Mod manager screen actions |
| `MODS` | General mod loading |
| `MODS_ASSEMBLY` | Mod assembly loading |
| `MODS_COMPILE` | Roslyn C# compilation |
| `MODS_EXPORT` | Mod export |
| `MODS_INIT` | Mod initialization |
| `MODS_NATIVE` | Native library handling in mods |
| `MODS_DISCOVERY` | Scanner/loader registry init |
| `MODS_REGISTRY` | Mod registry operations |
| `MODS_SAVE` | Mod save dispatch |
| `MODS_SETTINGS` | Mod settings persistence |
| `MODS_ZIP` | Zip provider file access |
| `QUEST` | Quest system |
| `SAVE` | Save/load operations |
| `SCENE` | Scene transitions |
| `SCENE_CTX` | Scene context nodes |
| `SHADER` | Shader operations |
| `TEST` | Test runner (SceneSwitcher) |
| `TSCN` | `.tscn` scene resolution/parsing |
| `TSCN_TEST` | `.tscn` test driver |
| `UNINSTALLER` | Mod uninstall/cleanup |
| `UI` | UI events |

> **Warning**: the `enabled_categories` whitelist filter is currently **commented out** inside `GameLogger.Log` — category filtering is inert, and all categories are logged regardless of this setting. Note also that the `config/game_config.json` shipped with the game still lists legacy `LOG_`-prefixed strings (e.g. `"LOG_MODS"`); since categories are plain unprefixed strings, those entries never match a real category.

### Log output files

Logs are written to `user://logs/cthangover.log`; entries with level `Error` are also duplicated to `user://logs/cthangover_errors.log`. A `--log-file=<path>` command-line argument overrides the log file location.

## Runtime settings persistence

User-adjusted settings are persisted to `user://settings.json` as indented JSON (`SettingsData.ConfigPath`), separate from this config file. The `game_config.json` only provides defaults — changes made in the settings menu are saved per-user. Note that this is JSON, not an INI `.cfg` file.
