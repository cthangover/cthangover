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
        MMA[MainMenu] -->|GetAll| TF
        TB[ToolBox] -->|GetVisible| TBF
    end

    subgraph Runtime
        MMA -->|CreateWindow| TW[ToolWindow popup]
        TB -->|AddToolButtons| BTN[Dynamic toolbar buttons]
        BTN -->|Pressed: Get + ShowWindow(CreateWindow())| TW
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
4. `MainMenu` populates a **Tools** dropdown from `ToolFactory.GetAll()`.
5. `ToolBox` dynamically creates HUD toolbar buttons from `ToolBoxButtonFactory.GetVisible()`.

## Core interfaces

### `IToolProvider`

The entry point for a custom tool. Every tool window must have a provider that implements this interface.

| Member | Type | Description |
|---|---|---|
| `Id` | `string` | Unique identifier used as the registry key (e.g. `"scenario_editor"`) |
| `LocaleKey` | `string` | Translation key for the tool's display name, resolved via `TranslationServer.Translate` |
| `CreateWindow()` | `Window` | Creates a new `Window` instance for this tool. The caller is responsible for adding it to the scene tree and displaying it |

### `IToolBoxButton`

Optional companion interface. Implement it alongside `IToolProvider` to add a persistent button on the in-game HUD toolbar that opens your tool.

| Member | Type | Description |
|---|---|---|
| `ToolId` | `string` | Matches an `IToolProvider.Id` in `ToolFactory`, wiring the button to a specific tool |
| `IconPath` | `string` | Resource path to the icon texture loaded for this button |
| `LocaleKey` | `string` | Translation key for the button's tooltip or accessible label |
| `IsVisible()` | `bool` | Return `false` to hide the button dynamically (e.g. dev-only buttons in release builds) |

> ⚠ **WARNING:** `IconPath` and `LocaleKey` are declared interface members but
> are currently **not consumed** by the `ToolBox` UI — dynamic toolbar buttons
> are created as iconless `TextureButton` instances and only `ToolId` is read.
> The members are kept as declared surface for future use; do not rely on them
> having a visual effect today.

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
| `GetAll()` | Returns all registered tools as a read-only list — used by `MainMenu` for the Tools dropdown |

Logging uses the `"TOOLS"` category.

### `ToolBoxButtonFactory`

Singleton registry (`ToolBoxButtonFactory.Instance`) for `IToolBoxButton` implementations.

| Method | Description |
|---|---|
| `Register(IToolBoxButton)` | Adds a button definition (duplicates not filtered — same `ToolId` may have different visibility rules) |
| `RegisterAssembly(Assembly)` | Scans an assembly for `IToolBoxButton` implementations — the primary hook for mods |
| `GetVisible()` | Returns all registered buttons that pass `IsVisible()`, used by `ToolBox.AddToolButtons` |

## HUD integration — `ToolBox`

The `ToolBox` control (`Core/UI/Tool/ToolBox.cs`) is the main toolbar/HUD container. It manages 5 built-in widgets:

- `MapWidget`
- `BagWidget` (inventory)
- `CharactersWidget` (character panel)
- `SkillWidget` (skills panel)
- `JournalWidget` (quest journal)

During `_Ready()`:

1. Built-in widget references are discovered via recursive tree search (`FindWidget<T>`).
2. Static buttons (Map, Bag, Characters, Skills, Settings, Journal) are wired.
3. `AddToolButtons(container)` dynamically creates `TextureButton` instances from `ToolBoxButtonFactory.GetVisible()` — each button opens its tool via `ToolWindow.ShowWindow(tool.CreateWindow())`, so the window gets the full chrome (title bar, resize zones) and is centered, rather than being shown raw from `CreateWindow()` alone.

## Built-in tools

The `tools` mod provides four development tools:

| Tool ID | Description | Locale Key |
|---|---|---|
| `scenario_editor` | Full editor for `.scenario` DSL files — file tree, text/graph tabs, metadata inspection, background thumbnail previews, play-test mode | `tools/scenario_editor/title` |
| `scene_builder` | Interactive Godot scene inspector — wrapper template picker, live viewport with node hierarchy tree, C# code editor with compile-and-run | `tools/scene_builder/title` |
| `light_editor` | Edits static lights on backgrounds — light list (max 10), per-light radius/influence/color editors, draggable handles, shader preview, JSON export/import | `tools/light_editor/title` |
| `interactive_editor` | Creates/edits interactive object definitions — BG preview with draggable collider handles (rect, circle, polygon), JSON output, load/save to mod directories | `tools/interactive_editor/title` |

Each tool has a provider class (`ScenarioEditorToolProvider`, `SceneBuilderToolProvider`, `LightEditorToolProvider`, `InteractiveEditorToolProvider`) implementing `IToolProvider`, and a dedicated window class extending `ToolWindow`.

Two tools also provide `IToolBoxButton` implementations for quick access from the HUD toolbar:
- `LightEditorToolBoxButton` — always visible
- `InteractiveEditorToolBoxButton` — always visible

## Pages

- [Creating a Custom Tool](tutorial.md) — step-by-step guide with code examples and locale keys
