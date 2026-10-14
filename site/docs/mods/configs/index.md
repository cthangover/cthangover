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
  // Catalog category slug or comma-separated combination (e.g. "library, gameplay")
  "category": "library",
  // Glob patterns for native shared libraries (.dll / .so / .dylib)
  "depends_native": ["bin/*.dll"],
  // List of mod IDs that must be loaded before this mod (versioned strings allowed)
  "depends": ["visual_novel_core", "interface"],
  // Mutually exclusive mod IDs — informational metadata, never enforced (see load-order)
  "conflicts_with": [],
  // Declarative GUI entries anchored to shell slots (panel / overlay)
  "ui": [
    { "id": "my_panel", "kind": "panel", "slot": "ModBeforeUIPanel", "scene_path": "ui/my_panel.tscn" }
  ],
  // Title-screen buttons opening fullscreen mod scenes
  "main_menu": [
    { "id": "my_screen", "scene_path": "menu/my_screen.tscn", "button": { "locale": "mymod/menu_button" } }
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
| `depends` | No | List of mod IDs that must be loaded before this mod (versioned strings allowed — see [Load Order](../load-order/)) |
| `category` | No | Catalog category slug or comma-separated combination (e.g. `"library, gameplay"`). See the [catalog](../catalog/) page for the full list |
| `depends_native` | No | Glob patterns for native shared libraries (`.dll` / `.so` / `.dylib`). Copied to the mod cache and wired into P/Invoke resolution by `ModNativeLoader` |
| `conflicts_with` | No | Mutually exclusive mod IDs. **Informational only** — parsed and copied into catalog exports, never enforced at runtime (see the [load order](../load-order/) warning) |
| `ui` | No | Declarative GUI entries anchored to named shell slots — toolbar-toggled `panel`s and code-driven `overlay`s, collected by `UiRegistry` |
| `main_menu` | No | Title-screen buttons; each opens a fullscreen mod `.tscn`, collected by `MenuRegistry` (section `mods`) |

### Sources — glob patterns

- `"src/*.cs"` — all `.cs` files in `src/`, one level deep
- `"src/**/*.cs"` — all `.cs` files recursively

### GDScript sources — `gd_sources`

- `"scripts/*.gd"` — all `.gd` files in `scripts/`, one level deep

GDScript files are compiled by Godot's GDScript engine (no Roslyn needed). A single `.gd` file can implement multiple interfaces simultaneously — actions, conditions, battle executors, item actions, and mod initializers. See [GDScript in Mods](site/docs/mods/src/gdscript) for the full API reference.

### Dependencies

```jsonc
"depends": ["visual_novel_core"]
```

The kernel resolves dependencies in topological order. Circular dependencies cause a load error. `visual_novel_core` is always loaded first.

### Scene lifecycle hooks

There is no `subscriptions` manifest field anymore — it was removed from the schema. To run mod code when a scene loads or unloads:

- declare scene events in a `.scenario` file — see [Scenes](../scenes/) and [Scenarios](../scenes/scenarios/);
- or annotate a C# class with the `[SceneEvent]` attribute — see [Scene Event Metadata](../metadata/).

To attach mod **UI** to a scene or the shell, use the `ui` array (see the field table above and [UI Wrappers](../wrappers/)):

## Real example — Cooking mod

From `mods/cooking/manifest.json`:

```jsonc
{
  "name": "Cooking",
  "author": "ct",
  "description": "Cooking mechanic - full autonomous mod",
  "sources": ["src/*.cs"],
  "ui": [
    // Workbench overlay for the kitchen — toggled by its own code / ui.toggle_panel
    { "id": "cooking_workbench", "kind": "overlay", "scene": "home_kitchen", "slot": "ModLastPanel",
      "scene_path": "scenes/cooking_panel.tscn", "once": true, "show_on_start": false, "priority": 10 },
    // Ration statistics — shown automatically on entering the kitchen
    { "id": "ration_stats", "kind": "overlay", "scene": "home_kitchen", "slot": "Lastground",
      "scene_path": "scenes/ration_statistics_panel.tscn", "once": false, "show_on_start": true, "priority": 20 }
  ]
}
```

Two overlays are anchored to the `home_kitchen` scene: the cooking workbench lives for the whole session (`once`) and shows itself when its code toggles it; the ration statistics panel is created on scene entry (`once: false`) and shown immediately (`show_on_start: true`).

## Real example — Multiple dependencies

From `mods/test_interactives/manifest.json`:

```jsonc
{
  "name": "Test Interactives",
  "author": "ct",
  "description": "Test mod demonstrating the interactive object system",
  // core and interface are guaranteed to load before this mod
  "depends": ["visual_novel_core", "interface"]
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

Save as `mods/my_first_mod/manifest.json`. The mod is now loaded by the kernel. Even without `sources` or `ui`, the mod's scenarios, items, and other data files are discovered automatically from their conventional locations.
