# Developer Tools

The developer tools system allows mods to create custom editor windows and register them in the in-game toolbar. Tools are discovered automatically via reflection, so any mod with C# code can contribute new tool windows without modifying the kernel.

## Architecture

```mermaid
flowchart TD
    subgraph Discovery
        TF[ToolFactory] -->|Reflections.FindAndCreate| ITP[IToolProvider implementations]
        TBF[ToolBoxButtonFactory] -->|Reflections.FindAndCreate| ITBB[IToolBoxButton implementations]
    end

    subgraph Kernel
        MMA[MainMenu] -->|GetAll + AddMenuButton| TF
        TB[ToolBox] -->|GetVisible| TBF
    end

    subgraph Runtime
        MMB[Main-menu tool buttons] -->|"Pressed: ShowWindow(CreateWindow())"| TW[ToolWindow popup]
        TB -->|AddToolButtons| BTN[Dynamic toolbar buttons]
        BTN -->|"Pressed: Get + ShowWindow(CreateWindow())"| TW
    end

    subgraph Mod Assembly
        MTP[MyToolProvider : IToolProvider]
        MTB[MyToolBoxButton : IToolBoxButton]
    end

    ITP --> MTP
    ITBB --> MTB
```

Tools bootstrapping happens at startup:

1. `ToolFactory.Instance` scans all loaded assemblies for `IToolProvider` implementations via `Reflections.FindAndCreate`.
2. `ToolBoxButtonFactory.Instance` scans for `IToolBoxButton` implementations.
3. When a mod assembly loads (`ModAssemblyLoader`), both factories call `RegisterAssembly(assembly)` to discover mod-provided tools.
4. `MainMenu` asks every registered tool to append its launcher button to the tool block below **Exit** via `IToolProvider.AddMenuButton`.
5. `ToolBox` dynamically creates HUD toolbar buttons from `ToolBoxButtonFactory.GetVisible()`.

## Core interfaces

### `IToolProvider`

The entry point for a custom tool. Every tool window must have a provider that implements this interface.

| Member | Type | Description |
|---|---|---|
| `Id` | `string` | Unique identifier used as the registry key (e.g. `"scenario_editor"`) |
| `LocaleKey` | `string` | Translation key for the tool's display name, resolved via `TranslationServer.Translate` |
| `CreateWindow()` | `Window` | Creates a new `Window` instance for this tool. The caller is responsible for adding it to the scene tree and displaying it |
| `AddMenuButton(Container)` | `void` | Appends the tool's launcher button to the main menu's tool block. Use `ToolMenuButton.Attach(container, this)` for the standard button (localized caption, opens the window on press, retranslated on language change) |

### `IToolBoxButton`

Optional companion interface. Implement it alongside `IToolProvider` to add a persistent button on the in-game HUD toolbar that opens your tool.

| Member | Type | Description |
|---|---|---|
| `ToolId` | `string` | Matches an `IToolProvider.Id` in `ToolFactory`, wiring the button to a specific tool |
| `IconPath` | `string` | Resource path to the icon texture loaded for this button |
| `LocaleKey` | `string` | Translation key for the button's tooltip or accessible label |
| `IsVisible()` | `bool` | Return `false` to hide the button dynamically (e.g. dev-only buttons in release builds) |

`IconPath` and `LocaleKey` **are** consumed by `ToolBox.AddToolButtons`: the locale key is
translated into the button's tooltip/label, and the icon is loaded via `ResourceLoader`
when the path exists (a button without a usable icon falls back to its text label).
`ToolId` wires the press to `ToolFactory.Get(toolId)` and opens the tool through
`ToolWindow.ShowWindow(tool.CreateWindow())`.

## `ToolWindow` base class

Abstract base class for tool popup windows. Extends Godot's `Window`. Key features:

- **Custom chrome** — the native border is removed and replaced with a custom
  title bar (title label, maximize toggle, close button) plus drag-to-move on
  the title bar and dedicated edge/corner resize zones around all four sides.
- **Maximize toggle** — via the title-bar button or double-clicking the title;
  maximized fills the game viewport, restore returns to the previous geometry.
- **Sizing limits** — `MinSize` is 800×400; `MaxSize` equals the game viewport
  size, so tool windows never exceed the game window.
- **Dirty-state tracking** — `SetDirty()` / `MarkClean()` add/remove a `*` prefix on the title bar. On close with unsaved changes, a confirmation dialog appears automatically.
- **Confirmation dialogs** — `ShowUnsavedDialog(Action)` shows a localized "unsaved changes" prompt; `ConfirmDiscardUnsaved()` provides a return-value variant.
- **Layout helpers**:
  - `CreateFillContainer()` — full-anchor `VBoxContainer` filling the entire window
  - `CreateToolbar()` — pre-configured `HBoxContainer` with 8px spacing
  - `CreateSidebar(VBoxContainer, int width = 280)` — fixed-width `PanelContainer` with vertical scrolling
- **Monospace font** — `GetMonospaceFont()` resolves a monospace font for code displays, falling back to `ThemeDB.FallbackFont`.
- **Static openers** — `Open<T>()` (generic, parameterless constructor) and `ShowWindow(Window)` (for `IToolProvider`-created instances) center the window on screen. Both apply the chrome via `SetupChrome()` before showing.
- **Teardown hook** — override the virtual `Cleanup()` to release resources; it is called automatically from `_ExitTree` when the window leaves the tree.

## Registry

### `ToolFactory`

Singleton registry (`ToolFactory.Instance`) for `IToolProvider` implementations.

| Method | Description |
|---|---|
| `Register(IToolProvider)` | Adds a tool; duplicates by `Id` are silently ignored (first-wins) |
| `RegisterAssembly(Assembly)` | Scans an assembly for `IToolProvider` implementations and registers them — the primary hook for mods |
| `Get(string id)` | Looks up a tool by `Id`, returns `null` if not found |
| `GetAll()` | Returns all registered tools as a read-only list — used by `MainMenu` to ask each tool for its menu button |

Logging uses the `"TOOLS"` category.

### `ToolBoxButtonFactory`

Singleton registry (`ToolBoxButtonFactory.Instance`) for `IToolBoxButton` implementations.

| Method | Description |
|---|---|
| `Register(IToolBoxButton)` | Adds a button definition (duplicates not filtered — same `ToolId` may have different visibility rules) |
| `RegisterAssembly(Assembly)` | Scans an assembly for `IToolBoxButton` implementations — the primary hook for mods |
| `GetVisible()` | Returns all registered buttons that pass `IsVisible()`, used by `ToolBox.AddToolButtons` |

## HUD integration — `ToolBox`

The `ToolBox` control (`Core/UI/Tool/ToolBox.cs`) is the main toolbar/HUD container. It is
manifest-driven and knows no concrete panels itself: `UiBuilder` hands it one
`UiEntry` per mod-declared toolbar panel (from `UiRegistry.PanelEntries`, pre-sorted by
button index, mod load order, then id), and `ApplyPanelButtons(entries)` builds a styled
button per entry — icon via `UIIconFactory`, label from the entry's `button.locale` key —
whose press calls `UiRegistry.ToggleEntry(id)` to show/hide the panel.

Tool windows ride the same bar: during `_Ready()`, `AddToolButtons(container)` appends one
button per `ToolBoxButtonFactory.GetVisible()` definition — label from `LocaleKey`, icon
from `IconPath` when the resource exists — and opens its tool via
`ToolWindow.ShowWindow(tool.CreateWindow())`, so the window gets the full chrome
(title bar, resize zones) and is centered rather than being shown raw from
`CreateWindow()` alone.

## Built-in tools

The `editor_tools` mod provides six development tools:

| Tool ID | Description | Locale Key |
|---|---|---|
| `scenario_editor` | Full editor for `.scenario` DSL files — file tree, text/graph tabs, metadata inspection, background thumbnail previews, play-test mode | `tools/scenario_editor/title` |
| `scene_builder` | Interactive Godot scene inspector — wrapper template picker, live viewport with node hierarchy tree, C# code editor with compile-and-run | `tools/scene_builder/title` |
| `light_editor` | Edits static lights on backgrounds — light list (max 10), per-light radius/influence/color editors, draggable handles, shader preview, JSON export/import | `tools/light_editor/title` |
| `interactive_editor` | Creates/edits interactive object definitions — BG preview with draggable collider handles (rect, circle, polygon), JSON output, load/save to mod directories | `tools/interactive_editor/title` |
| `map_editor` | Visual world-map zone editor — canvas with draggable regions/markers/labels per scene, locked/hidden flags, per-zone title keys and images, load/save of the zone JSON (see [Map & Zones](../map/)) | `tools/map_editor/title` |
| `catalog_export` | Exports a catalog entry (`catalog.json`) for a mod — download URL, category, tags, changelog, status, min game version, icon URL | `mods/catalog_title` |

The scenario editor's play-test works inside a running game too: **Stop**
brings the player back to the scene they were in (or to the tool's own route,
or to the main menu when the editor was opened from there), and a scene switch
made by the played scenario itself ends the play-test where it is instead of
throwing the player to the main menu.

Each tool has a provider class (`ScenarioEditorToolProvider`, `SceneBuilderToolProvider`, `LightEditorToolProvider`, `InteractiveEditorToolProvider`, `MapEditorToolProvider`, `CatalogExportToolProvider`) implementing `IToolProvider`, and a dedicated window class extending `ToolWindow`.

Three tools also provide `IToolBoxButton` implementations for quick access from the HUD toolbar:
- `LightEditorToolBoxButton` — always visible
- `InteractiveEditorToolBoxButton` — always visible
- `MapEditorToolBoxButton` — always visible (map authoring is part of the normal dev workflow)

## Pages

- [Creating a Custom Tool](tutorial.md) — step-by-step guide with code examples and locale keys
