# GDScript in Mods

Mods can include `.gd` (GDScript) files alongside or instead of C# source code. GDScript files are compiled by Godot's built-in GDScript engine and can implement scenario actions, custom conditions, battle action executors, item actions, mod initializers, and winground listeners — all without Roslyn or a C# toolchain.

## How it works

1. Mod manifest declares `"gd_sources": ["scripts/*.gd"]`
2. On mod load, the kernel finds `.gd` files, compiles them via `GDScript.Reload()`, instantiates, and inspects methods
3. If the GDScript has known method patterns, it is auto-registered into the corresponding subsystem
4. No manual registration code needed — the kernel does it automatically

## manifest.json — gd_sources field

```jsonc
{
  "id": "my_mod",                                          // Canonical mod identifier
  "name": "My GDScript Mod",                               // Human-readable mod name
  "version": "1.0.0",                                      // Semver version
  "gd_sources": ["scripts/*.gd"],                          // Glob patterns for .gd files
  "depends": ["core"]                                      // Versioned dependency strings
}
```

| Field | Description |
|---|---|
| `gd_sources` | Glob patterns for `.gd` files |

> **Note on pattern matching:** despite the glob-like syntax, the directory walk is **always recursive**. A pattern like `scripts/*.gd` is split at the last `/`: the part before it (`scripts`) selects the root directory, and the suffix after it (`*.gd`) only acts as a **file name filter**. As a result, `scripts/*.gd` also picks up files such as `scripts/any/deep/foo.gd`. There is no way to limit matching to a single directory level; use distinct top-level folders if you need separation.

A single `.gd` file can implement multiple interfaces simultaneously — the kernel checks all known patterns on each script.

## Pages

- [Scenario Actions](actions.md) — actions + GDActionContext + service APIs
- [Custom Conditions](conditions.md) — condition leaf parsers
- [Battle Action Executors](battle.md) — custom battle logic
- [Item Actions](items.md) — item usage handlers
- [Mod Lifecycle](lifecycle.md) — initializers, event bus, publishing events

## Auto-detection summary

| GDScript methods | Registered as | Subsystem |
|---|---|---|
| `get_name()` + `run(ctx)` | `IScenarioAction` | Scenario action factory (`run` receives a GDScript action context) |
| `get_condition_name()` + `evaluate(flags, args)` | `ILeafParser` | Condition parser registry (priority over QuestParser) |
| `get_action_id()` + `execute(action, user, target)` | `IActionExecutor` | Battle action executor hub (global); returns a result object converted to attribute changes |
| `get_item_action_id()` + `use(item)` | `IItemAction` | Item action factory; `item` is a GDScript item wrapper, return `bool` |
| `on_mod_loaded(mod_id)` / `on_mod_resources_ready()` | `IModInitializer` | Mod initializer registry; `on_mod_loaded` receives the mod ID string |
| `get_winground_listener_id()` + optional `on_winground_before_exp(ctx)` / `on_winground_after_loot(ctx)` / `on_winground_before_show(ctx)` | `IWingroundListener` | Winground listener registry; all three hooks are optional and receive a context object synced back after the call |
| `on_mod_event_*` | String event subscriber | Global event bus |

Detection details:

- Scenario actions are detected by the presence of **`get_name()`** together with `run()` — there is no separate `get_action_name()` convention.
- Initializers register when *either* callback is present; each missing one is simply not invoked.
- Winground listeners register only if `get_winground_listener_id()` returns a non-empty ID in addition to at least one hook method.

A single `.gd` file can implement multiple patterns simultaneously.

## Order among C# and GDScript

1. C# assemblies are compiled and loaded first (via Roslyn or precompiled DLLs)
2. GDScript files are loaded second
3. All registrations happen during the same load phase

GDScript **conditions** use `RegisterFirst` — they are inserted at the front of the leaf parser list, taking priority over the built-in `QuestParser`. This means `gds.is_rain` resolves before the parser tries to interpret `gds` as a quest ID.
