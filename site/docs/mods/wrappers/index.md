# Wrappers — UI Templates

A mod can attach its own UI (panels, overlays, widgets) to the game shell in two ways:

- **Manifest `ui[]` entries** — the supported, declarative way. A mod declares panels and overlays in `manifest.json`; the kernel instantiates the corresponding `.tscn` scenes, anchors them to named slots, and (for panels) wires a toolbar toggle button.
- **Wrapper template files (`.wrappertmpl`)** — plain-text UI tree templates collected from mods. The collection pipeline still exists, but **nothing instantiates these templates automatically**; see [Wrapper template files](#wrapper-template-files-wrappertmpl).

The old `manifest.json → subscriptions` array (template + code fragment + trigger) is **no longer part of the manifest schema** and has been removed from the engine.

## Attaching mod UI — the `ui` manifest entries

Each entry of the manifest's `ui` array declares one GUI element. Two kinds exist:

- `"kind": "panel"` — button-toggled panel. The UI registry owns a toolbar button for it (declared in `button`) and is the only one allowed to change its visibility. Panels always live for the whole session.
- `"kind": "overlay"` — no button; the element's own code decides when it is visible (optionally auto-shown with `show_on_start`).

Real example — `mods/cooking/manifest.json`:

```jsonc
{
  "ui": [
    {
      "id": "cooking_workbench",               // Unique within the mod; doubles as the toggle target
      "kind": "overlay",                       // No toolbar button
      "scene": "home_kitchen",                 // Only available in this logical scene
      "slot": "ModLastPanel",                  // Anchor slot node in the shell
      "scene_path": "scenes/cooking_panel.tscn", // Mod-relative .tscn, loaded at runtime
      "once": true,                            // Lives for the whole application
      "show_on_start": false,                  // Waits for a toggle or its own code
      "priority": 10                           // Layer/order inside the slot
    },
    {
      "id": "ration_stats",
      "kind": "overlay",
      "scene": "home_kitchen",
      "slot": "Lastground",
      "scene_path": "scenes/ration_statistics_panel.tscn",
      "once": false,                           // Created on scene enter, freed on scene exit
      "show_on_start": true,                   // Shown as soon as the scene is entered
      "priority": 20
    }
  ]
}
```

A `panel` entry additionally declares its toolbar button — from `mods/cthangover_game/manifest.json`:

```jsonc
{
  "id": "map",
  "kind": "panel",
  "slot": "ModBeforeUIPanel",
  "scene_path": "scenes/ui/map_widget.tscn",
  "button": {
    "icon": "map",              // Icon name resolved through the UI icon factory
    "locale": "ui/map/title",   // Tooltip/title key from locale files
    "index": 10                 // Button position on the toolbar
  },
  "priority": 10
}
```

### Entry fields

| Field | Applies to | Description |
|---|---|---|
| `id` | both | Unique within the owning mod; doubles as the runtime cache key and the toggle target (e.g. for `ui.toggle_panel`) |
| `kind` | both | `"panel"` (button-toggled) or `"overlay"` (code-controlled visibility) |
| `scene` | both, optional | Logical scene this GUI belongs to; the GUI is hidden when leaving it. Absent = scene-independent, lives for the whole session |
| `slot` | both | Name of the anchor slot node in the shell (e.g. `ModBeforeUIPanel`, `ModLastPanel`, `Lastground`) |
| `scene_path` | both | Mod-relative path to a `.tscn` scene, resolved through the runtime TSCN loader — see [Runtime TSCN Scenes](../scenes/tscn-runtime/) |
| `once` | overlay | `true`: the instance lives for the whole application; `false` (default): created on scene entry, freed on scene exit. Panels are always treated as `true` |
| `show_on_start` | overlay | `true`: shown as soon as its scene is entered; `false` (default): waits for a registry toggle or the element's own code. Ignored for panels |
| `priority` | both | Layer/order inside the anchor slot. Defaults to `10`; ordering ties between mods are broken by mod load order |
| `button` | panel only | Toolbar button declaration: `icon`, `locale`, `index`. Forbidden for overlays |

Entries are validated at startup by `UiRegistry.Initialize`; invalid entries are logged and skipped. Panels get their toolbar buttons from the toolbox (`ToolBox.ApplyPanelButtons`) and are toggled with `UiRegistry` toggles — the same mechanism the DSL action `ui.toggle_panel` drives (see [Scenario Actions](../scenes/scenarios/actions/)). The in-game map is itself a `panel` UI entry — see [Map & Travel Zones](../map/).

## Scene lifecycle hooks

To run logic when a scene is entered or exited, use the event mechanisms instead of the removed `subscriptions` array:

- `.scenario` scene events — see [Scenes](../scenes/) and [Scenarios](../scenes/scenarios/);
- the `[SceneEvent]` attribute for declarative C# hooks — see [Scene Event Metadata](../metadata/).

Both are composed into one synchronous chain by `SceneManager.ComposeEvents` on scene enter; subscribers run in priority order (0/5/10/30). For the event bus used inside those hooks, see [Event Bus](../events/).

## Wrapper template files (`.wrappertmpl`)

The engine still collects wrapper template files from mods:

```filestree
wrappers/
├── default.wrappertmpl
└── custom_layout.wrappertmpl
```

- `WrapperTemplateResolver` scans every loaded mod recursively for `.wrappertmpl` files and caches the result.
- The static `WrapperTemplates.Collect()` facade returns them as `WrapperDefinition` records (`ModId`, `Name` = filename minus extension, `Content` = raw text).

**Currently nothing consumes them.** There is no automatic instantiation on scene enter, no `{{USER_CODE}}` substitution, and no manifest field that would bind a template to a scene — that pipeline was removed together with the manifest `subscriptions` array. A mod (or a future engine feature) can still call `WrapperTemplates.Collect()` and instantiate the raw template text itself; the content is kept as a raw string because it is parsed at instantiation time, not JSON.

## Building mod UI from code — ModWidget

When creating UI components from mod C# code, inherit from `ModWidget` (`Core/UI/Base/Lists/ModWidget.cs`). It provides an idempotent `Construct()`/`Destruct()` lifecycle for UI built from code inside mods: `EnsureConstructed()` calls the abstract `Construct()` exactly once, and the virtual `Destruct()` is called automatically on `_ExitTree`. Usage = override `Construct()` and build your children there:

```csharp
public class MyUIPanel : ModWidget
{
    protected override void Construct()
    {
        var label = new Label { Text = "Hello from mod!" };
        AddChild(label);
    }
}
```

Call `EnsureConstructed()` before adding the widget to the tree (as `CardBattleCore` does), so its children are constructed exactly once regardless of how many times construction is requested.
