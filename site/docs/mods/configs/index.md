# Configs — manifest.json

Every mod needs a manifest and optionally a properties file. These define the mod's identity, dependencies, C# source compilation rules, and scene lifecycle hooks.

## `manifest.json` — full schema

```jsonc
{
  // Canonical mod identifier (falls back to folder name if omitted)
  "id": "my_mod",
  // Human-readable name shown in the mod menu (required)
  "name": "MyMod",
  // Semver version — used for saves and deduplication
  "version": "1.0.0",
  // Author identifier (arbitrary string)
  "author": "author_name",
  // Short description for the mod menu and catalog
  "description": "What this mod does",
  // Glob patterns for C# files to compile via Roslyn
  "sources": ["src/*.cs", "src/**/*.cs"],
  // Glob patterns for GDScript files (.gd)
  "gd_sources": ["scripts/*.gd"],
  // List of mod IDs that must be loaded before this mod
  "depends": ["core", "interface"],
  // Scene lifecycle subscriptions
  "subscriptions": [
    {
      // Scene name to hook into
      "scene": "home_kitchen",
      // UI template (usually "default")
      "template": "default",
      // C# code fragment name (injected into wrapper template)
      "code": "my_init_handler",
      // Trigger moment: "on_enter" or "on_exit"
      "trigger": "on_enter",
      // Execution order (lower = runs first)
      "priority": 10
    }
  ]
}
```

### Field reference

| Field | Required | Description |
|---|---|---|
| `id` | No | Canonical mod identifier (falls back to folder/zip name) |
| `name` | Yes | Human-readable mod name |
| `version` | No | Semver version string (e.g. "1.0.0"). Used by the save system and for deduplication |
| `author` | No | Author identifier |
| `description` | No | Short description |
| `sources` | No | Glob patterns for C# files to compile via Roslyn |
| `gd_sources` | No | Glob patterns for `.gd` GDScript files to compile via Godot's engine |
| `depends` | No | List of mod IDs that must be loaded before this mod |
| `subscriptions` | No | Scene lifecycle hooks (see below) |

### Sources — glob patterns

- `"src/*.cs"` — all `.cs` files in `src/`, one level deep
- `"src/**/*.cs"` — all `.cs` files recursively

### GDScript sources — `gd_sources`

- `"scripts/*.gd"` — all `.gd` files in `scripts/`, one level deep

GDScript files are compiled by Godot's GDScript engine (no Roslyn needed). A single `.gd` file can implement multiple interfaces simultaneously — actions, conditions, battle executors, item actions, and mod initializers. See [GDScript in Mods](site/docs/mods/src/gdscript) for the full API reference.

### Dependencies

```jsonc
"depends": ["core"]
```

The kernel resolves dependencies in topological order. Circular dependencies cause a load error. `core` is always loaded first.

### Subscriptions — scene lifecycle hooks

Subscriptions let mods run C# code when a scene loads or unloads:

```jsonc
"subscriptions": [
  {
    "scene": "home_kitchen",        // Scene to hook into
    "template": "default",          // UI template
    "code": "cooking_panel_init",   // C# code fragment name (injected into wrapper template)
    "trigger": "on_enter",          // Run when entering the scene
    "priority": 10                  // Lower runs first
  },
  {
    "scene": "home_kitchen",
    "template": "default",
    "code": "dinner_tools_hide",
    "trigger": "on_exit",           // Run when leaving the scene
    "priority": 10
  }
]
```

| Field | Description |
|---|---|
| `scene` | Scene name to hook into |
| `template` | UI template name (`"default"` for standard layout) |
| `code` | C# code fragment name — injected into the wrapper template via `{{USER_CODE}}` and compiled at runtime |
| `trigger` | `"on_enter"` or `"on_exit"` |
| `priority` | Execution order (lower = runs first). Multiple mods can subscribe to the same scene |

The `code` field points to a C# source file (without `.cs` extension) at `subscriptions/{code}.cs`. Its content is injected into the wrapper template via `{{USER_CODE}}` and the combined source is compiled at runtime into a `SceneBuilderScript.Run(Node)` method. The code fragment receives the scene root node as its parameter:

## Real example — Cooking mod

From `mods/cooking/manifest.json`:

```jsonc
{
  "name": "Cooking",
  "author": "ct",
  "description": "Cooking mechanic - full autonomous mod",
  "sources": ["src/*.cs"],
  "subscriptions": [
    // Priority 10 — initialize cooking panel on enter
    { "scene": "home_kitchen", "template": "default", "code": "cooking_panel_init", "trigger": "on_enter", "priority": 10 },
    // Priority 20 — show tools (runs after panel init)
    { "scene": "home_kitchen", "template": "default", "code": "dinner_tools_show", "trigger": "on_enter", "priority": 20 },
    // Hide tools when leaving the kitchen
    { "scene": "home_kitchen", "template": "default", "code": "dinner_tools_hide", "trigger": "on_exit", "priority": 10 }
  ]
}
```

Three handlers run on the `home_kitchen` scene: `cooking_panel_init` (priority 10) runs before `dinner_tools_show` (priority 20). `dinner_tools_hide` runs when leaving the kitchen.

## Real example — Multiple dependencies

From `mods/test_interactives/manifest.json`:

```jsonc
{
  "name": "Test Interactives",
  "author": "ct",
  "description": "Test mod demonstrating the interactive object system",
  // core and interface are guaranteed to load before this mod
  "depends": ["core", "interface"]
}
```

## Creating a new mod — minimal manifest

```jsonc
{
  "name": "MyFirstMod",
  "author": "you",
  "description": "Adds a custom item and a new scenario option"
}
```

Save as `mods/my_first_mod/manifest.json`. The mod is now loaded by the kernel. Even without `sources` or `subscriptions`, the mod's scenarios, items, and other data files are discovered automatically from their conventional locations.
