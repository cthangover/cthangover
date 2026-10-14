# C# Source Code in Mods

Mods can include C# source files that are compiled at runtime via Roslyn. This allows mods to add new game mechanics, UI, battle systems, and scenario actions without modifying the kernel.

## How it works

1. Kernel discovers `.cs` files via `manifest.json` → `"sources": ["src/**/*.cs"]`
2. Files are loaded as text, compiled via Roslyn into a separate assembly
3. The assembly is loaded into the running game
4. Types implementing known interfaces (`IModInitializer`, `IBattleCore`, `IScenarioAction`, etc.) are auto-discovered

## What gets auto-registered from your assembly

Step 4 happens in `ModAssemblyLoader.RegisterAssembly` (`Core/Mods/ModAssemblyLoader.cs`) — the single point through which every new assembly enters the game. It fans out into every reflection-driven registry, so a public class implementing any of these is discovered without registration code:

| Registry | Discovers |
|---|---|
| `ScenarioActionFactory` | `IScenarioAction` implementations — new `action` commands for the scenario DSL |
| `ScenarioCommandStrategyFactory` | `IScenarioCommandStrategy` implementations, indexed by `Command` name |
| `SceneEventRegistry` (`Core/Scenes/`) | Classes decorated with `[SceneEvent]` deriving from `ExecutableEvent` |
| `ItemActionFactory` | `IItemAction` implementations — item on-use behaviours, indexed by ID |
| `ToolFactory` | `IToolProvider` implementations — tools that add launcher buttons to the main menu's tool block |
| `ToolBoxButtonFactory` | `IToolBoxButton` implementations — toolbox UI buttons |
| `ModInitializerRegistry` | `IModInitializer` mod entry points |
| `ModSaveableRegistry` | `IModSaveable` implementations participating in save/load |
| `ModSettingsRegistry` | `IModSettings` implementations exposed in the settings menu |
| `ModDiscoveryRegistry` | Custom discovery scanners (new places mods can be found) |
| `SectionParserRegistry` | `.tscn` section parsers |
| `PropertyResolverRegistry` | Property resolvers used while building scenes from `.tscn` |
| `SubResourceBuilderRegistry` | Sub-resource builders for `.tscn` construction |
| `ExtResourceBuilderRegistry` | External-resource builders for `.tscn` construction |
| `ModManager.RegisterResolversFromAssembly` | `IModAssetResolver` implementations |

Adding a new reflection-based plugin system to the kernel requires exactly one line in this method. Note that `ScriptTypeRegistry` (below) is populated separately — by `ModRegistry.FinalizeGodotInit`, which calls `ScriptTypeRegistry.RegisterAssembly` right after registering each compiled assembly through `ModAssemblyLoader`.

Subsystems that now live in mods register their own implementations through an `IModInitializer` instead of this fan-out: battle cores and winground listeners (`turn_battle_base`) and recruit behaviours/conditions (`recruiting`). `ModEventBus` typed events with `[EventKey]` can also be declared in any mod — the GDScript bridge reads the attribute at publish time.

## ScriptTypeRegistry — resolving `.tscn` script paths

When a scene file references a script (`path="..."` in its headers), `ScriptTypeRegistry` (`Core/Mods/ScriptTypeRegistry.cs`) maps that path to the compiled `System.Type` inside the mod assembly, respecting mod load-order priority. It is populated by `ModCompiler` after each mod's C# compiles; `ModRegistry.Initialize` supplies the priority order via `SetModOrder`.

Only non-abstract classes deriving from `Godot.Node` are indexed — by class name, full name, and source file path. Paths are normalized before lookup (`res://` prefix stripped, backslashes → forward slashes) and comparisons are case-insensitive.

API (all static):

| Method | Purpose |
|---|---|
| `SetModOrder(List<(string ModId, int Priority)>)` | Sets the mod priority order used during resolution |
| `RegisterAssembly(assembly, modId, sourceFiles)` | Indexes the assembly's node classes and maps source file paths to matching class names |
| `ResolveByPath(tscnPath)` | Priority-ordered lookup by normalized path — first match wins |
| `ResolveByName(className)` | Priority-ordered lookup by class name / full name, with a case-insensitive fallback scan |
| `Resolve(tscnPath)` | Input without `/`, `\` or `.` → name lookup; otherwise path first, then file-name → class-name fallback |
| `Clear()` | Wipes all mappings (called on mod reload) |

## Mod entry point — `IModInitializer`

Mods that need lifecycle logic implement this:

```csharp
public interface IModInitializer
{
    void OnModLoaded(string modId);
    void OnModResourcesReady();
}
```

Implementations are discovered optionally via reflection when the compiled assembly is registered (`ModInitializerRegistry.RegisterAssembly`). Mods that only ship data (JSON scenarios, items, wrappers, etc.) work without any initializer.

## Real example — Card Battle Core

From `mods/card_battle/src/CardBattleCore.cs` (excerpt):

```csharp
namespace Cthangover.CardBattle
{
    public class CardBattleCore : IBattleCore
    {
        public string Id => "card_battle";

        public IActionExecutorProvider ActionProvider { get; }
            = new Actions.CardBattleActionProvider();

        public void Init(Character[] playerChars, Character[] enemyChars,
                         IBattleContext ctx)
        {
            _playerChars = playerChars;
            _ctx = ctx;

            // Deep-copy enemies so defeats are per-instance
            _enemyChars = new Character[enemyChars.Length];
            for (int i = 0; i < enemyChars.Length; i++)
                _enemyChars[i] = enemyChars[i]?.Copy() ?? enemyChars[i];

            ActionExecutorHub.Instance.SetActiveProvider(ActionProvider);

            var root = ctx.RootNode as Node;
            var panel = root.GetNodeOrNull<Control>("Panel") ?? root;

            _playerPanel = (BattleCardPanel)TscnScenes.LoadAndBuild("scenes/battle_card_panel.tscn");
            _playerPanel.Name = "PlayerPanel";
            _playerPanel.EnsureConstructed();
            panel.AddChild(_playerPanel);

            _enemyPanel = (BattleCardPanel)TscnScenes.LoadAndBuild("scenes/battle_card_panel.tscn");
            _enemyPanel.Name = "EnemyPanel";
            _enemyPanel.AlignType = "Right";
            _enemyPanel.EnsureConstructed();
            _enemyPanel.MouseFilter = Control.MouseFilterEnum.Ignore;
            panel.AddChild(_enemyPanel);

            // ... action panel omitted from this excerpt ...

            _endTurnButton = (EndTurnButton)TscnScenes.LoadAndBuild("scenes/end_turn_button.tscn");
            _endTurnButton.Name = "EndTurnButton";
            _endTurnButton.EnsureConstructed();
            _endTurnButton.OnPressed += OnEndTurnPressed;

            _cardController = new CardController { Name = "CardController" };
            panel.AddChild(_cardController);
        }

        public void Start()
        {
            // Position panels, calculate card scale, spawn character cards
            // ...
            StartPlayerTurn();
        }
    }
}
```

Key patterns visible here:
- **Deep copy** enemies to avoid shared state across battles
- **Godot node construction** via `TscnScenes.LoadAndBuild` + `EnsureConstructed()` before `AddChild`, or plain `new` + `AddChild` — the Roslyn assembly is separate from Godot's source generators, so `AddChild` must happen before setting `GlobalPosition`
- **Event subscriptions** via C# events (`OnPressed +=`, `OnCardDead +=`)
- **Async battle flow** via `async void` and `await ToSignal`

## Minimal mod from scratch

`mods/my_mod/src/MyMod.cs`:

```csharp
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.MyMod
{
    public class MyMod : IModInitializer
    {
        public void OnModLoaded(string modId) { }

        public void OnModResourcesReady()
        {
            GameLogger.Log("MY_MOD", "Мой мод загружен!", LogLevel.Message);
        }
    }
}
```

`mods/my_mod/manifest.json`:

```jsonc
{
    "name": "My First Mod",                                // Human-readable mod name
    "author": "you",                                       // Author credit
    "description": "Learning how to write C# mods",        // Short description
    "sources": ["src/MyMod.cs"]                            // Glob patterns for C# source files to compile via Roslyn
}
```

## Key APIs available to mods

| Namespace / Class | Purpose |
|---|---|
| `Cthangover.Core.Mods.IModInitializer` | Mod entry point: `OnModLoaded(string)` + `OnModResourcesReady()` |
| `Cthangover.Core.Utils.GameLogger` | Structured logging: `GameLogger.Log(module, msg, level)` |
| `Cthangover.Core.Battle.IBattleCore` | Implement to create a battle system |
| `Cthangover.Core.Actions.IScenarioAction` | Implement to add `action` commands to the DSL: `string Name { get; }` + `void Run(IActionContext context)` |
| `manifest.json` → `ui[]` | Declare panels and overlays in the shell — no interface to implement: the kernel instantiates your `.tscn`, anchors it to a named slot, and (for panels) wires a toolbar toggle button. See [UI Wrappers](wrappers/) |
| `Cthangover.Core.Mods.IModSettings` | Implement to expose settings in the in-game menu |
| `Cthangover.Core.Characters.Character` | Character data model — use `Copy()` for battles |
| `Cthangover.Core.Characters.CharacterAttributes` | Stats container: `Health`, `Point`, `Defence`, `Attack` |
| `Godot.Control` / `Godot.Node2D` | Standard Godot API — all available in mods |

## Logging

Always use `GameLogger` instead of `GD.Print`:

```csharp
GameLogger.Log("MY_MOD", "This is debug info", LogLevel.Debug);
GameLogger.Log("MY_MOD", "Something happened", LogLevel.Message);
GameLogger.Log("MY_MOD", "This might be a problem", LogLevel.Warning);
```

## Important: Godot input in mods

Because mod code compiles into a separate assembly (not processed by Godot's source generators), two rules apply:

1. **`AddChild` BEFORE `GlobalPosition`**: If you set `GlobalPosition` before `AddChild`, the node has no parent canvas transform and the position will be misinterpreted.

```csharp
// CORRECT
parent.AddChild(handle);
handle.GlobalPosition = target;

// WRONG
handle.GlobalPosition = target;
parent.AddChild(handle);
```

2. **Use `GuiInput` event instead of `_GuiInput` override**: Due to Godot dispatch quirks between assemblies, subscribe to the C# event rather than overriding the virtual method:

```csharp
public class MyHandle : ColorRect
{
    public MyHandle()
    {
        MouseFilter = Control.MouseFilterEnum.Stop;
        GuiInput += OnGuiInput;
    }

    private void OnGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.Pressed)
        {
            // handle click
            AcceptEvent();
        }
    }
}
```

## Compilation errors

Roslyn compilation errors appear in the game log. Common issues:
- Missing `using` directives — check namespace imports
- Reference to non-existent APIs — verify the kernel version supports the method
- Syntax errors — standard C# issues

The game will still run even if a mod's C# fails to compile. Non-C# assets (scenarios, items, etc.) from the same mod remain functional.
