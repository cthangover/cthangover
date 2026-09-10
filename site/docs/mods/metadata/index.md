# Scene Event Metadata

The `[SceneEvent]` attribute allows mod C# code to hook into scene lifecycle events declaratively, without needing manifest subscriptions.

## `[SceneEvent]` attribute

Place the attribute on a **class** in a mod assembly to register it for automatic execution. Only classes deriving from `ExecutableEvent` are registered (`Core/Scenes/SceneEventRegistry.cs` skips any other type):

```csharp
using Cthangover.Core.Scenes;
using Cthangover.Core.UI.Executable;

[SceneEvent("home_kitchen", Priority = 5)]
public class KitchenInitEvent : ExecutableEvent
{
    // Runs when home_kitchen scene loads, ordered within the
    // scene's event chain by its Priority / After metadata
}

[SceneEvent("town_entry", After = "other_event")]
public class TownEnterEvent : ExecutableEvent
{
    // Runs after the event whose ID is "other_event"
}
```

There is no method-based registration and no `SceneContext` parameter — the attribute cannot be applied to methods (`AttributeUsage(AttributeTargets.Class, AllowMultiple = false)`).

## Attribute parameters

| Parameter | Type | Description |
|---|---|---|
| `SceneName` | string | *(positional, required)* Target scene name |
| `Priority` | int | Execution order (lower = earlier). Default 0 |
| `After` | string | Name of another handler this should run after |
| `Condition` | string | Condition expression — handler only runs if expression is true |

## Registration

`SceneEventRegistry` uses reflection at assembly load time to discover all **classes** decorated with `[SceneEvent]`. Only types deriving from `ExecutableEvent` register; decorated classes without that base type are silently skipped. No manual registration is needed — the attribute itself serves as registration.

## Execution order

`[SceneEvent]` classes are not a separate phase: `SceneManager.ComposeEvents` composes them into the **same event chain** as `.scenario` definitions. That composition runs as a `SceneEnteredEvent` bus subscriber at priority 15, while manifest subscriptions execute at priority 20 (i.e., after scenario chains). So the order on scene enter is:

1. Scenario chain events + `[SceneEvent]` class events, interleaved and sorted by their own priority / `After` metadata (bus priority 15)
2. Manifest subscriptions fire afterwards (bus priority 20, ordered among themselves by their `priority` field)

On scene exit:
1. `on_exit` manifest subscriptions fire (by `priority`)

## vs Manifest subscriptions

| Feature | `[SceneEvent]` attribute | Manifest `subscriptions` |
|---|---|---|
| Registration | Attribute on class (must derive from `ExecutableEvent`) | JSON entry in `manifest.json` |
| Execution timing | Same event chain as scenario definitions (bus priority 15), before subscriptions (priority 20) | On enter/exit trigger |
| UI injection | No | Yes (via `template` and wrappers) |
| Condition support | Yes (`Condition` param) | No |
| Priority ordering | Yes | Yes |
| Dependencies (`After`) | Yes | No |

Use `[SceneEvent]` for game-logic hooks. Use manifest `subscriptions` when you need UI injection through wrapper templates.

## Manifest `conflicts_with` — declarative only

The `conflicts_with` manifest field is **declarative only**: it is parsed (`Core/Mods/ModManifest.cs`) and exported to catalog entries, but the runtime does **not** enforce conflicts — no mods are disabled or blocked at load time. Treat it as informational metadata describing mutually exclusive mod pairs for catalogs and tooling.

## `manifest.json` field reference

Every mod's `manifest.json` is deserialized into `ModManifest` (`Core/Mods/ModManifest.cs`). All fields are optional:

| Field | Type | Description |
|---|---|---|
| `id` | string | Canonical mod identifier. Used for deduplication (when two mods declare the same `id`, the higher `version` wins) and in other mods' `depends`. Falls back to the filesystem entry name when absent |
| `name` | string | Human-readable mod name (shown in UI) |
| `version` | string | Semver mod version (e.g. `"1.0.0"`). Used by the save system to detect downgrades and trigger migrations |
| `author` | string | Author credit string |
| `description` | string | Short description of what the mod does |
| `sources` | string[] | Glob-style paths to C# source files (e.g. `"src/"`, `"scripts/*.cs"`). Collected and compiled via Roslyn by `ModCompiler` |
| `gd_sources` | string[] | Glob-style paths to GDScript files (e.g. `"scripts/*.gd"`, `"gd/**/*.gd"`). `ModGDScriptLoader` collects them, compiles via Godot's GDScript runtime, and registers them as scenario actions or mod initializers |
| `depends` | string[] | Versioned dependencies in format `modId[op][version][?]` — see below. Parsed into `ModDependency` entries via `ModDependencyListConverter`; affects topological compile order and version validation |
| `depends_native` | string[] | Glob patterns for native shared libraries handled by `ModNativeLoader` — see below |
| `conflicts_with` | string[] | Mutually exclusive mod IDs. Declarative only — see section above |
| `subscriptions` | array | Scene event bindings — each entry describes which wrapper template to inject into which scene, on which trigger |

### `depends` — version syntax

Each `depends` entry is a string `[modId][op][version][?]`, parsed by `ModDependency.Parse` (`Core/Mods/ModDependency.cs`):

```jsonc
{
    "depends": [
        "core",           // bare id — any version
        "core>=1",        // core 1 or newer
        "my_lib^1.2.0",   // version starting with segments 1.2.0
        "extra_icons<2",
        "soft_dep?"       // trailing ? — optional dependency
    ]
}
```

| Operator | Meaning |
|---|---|
| *(none)* | Any version — bare `"modId"` |
| `>=` / `<=` / `>` / `<` | Per-segment comparison against the dependency's actual version |
| `=` | Exact match (segment-wise) |
| `^` | Prefix match — required segments must equal the leading segments of the actual version |

- A trailing `?` marks an **optional** dependency: if the mod is missing, that is not an error; if present, its version is still checked.
- Comparison goes through `VersionHelper.CompareSegments` (`Core/Utils/VersionHelper.cs`): both version strings are split on `.`, both segment arrays are padded with `"0"` to equal length, and each segment compares numerically if both parse as int, otherwise via ordinal string comparison. Segments may contain letters (`1a`, `alpha-3`, `1b.2.0`).
- Entries are read from JSON by `ModDependencyListConverter`: malformed strings are logged as errors, kept with `IsValid = false`, and reported later during `ValidateDependencies`.
- Valid dependency IDs also drive load order: `ModRegistry.GetOrderedModIds` topologically sorts so dependencies always precede their dependents.

### `depends_native` — native libraries

`depends_native` lists glob patterns (e.g. `"bin/*.dll"`, `"native/*.so"`, `"lib/*.dylib"`) for native shared libraries shipped inside the mod. `ModNativeLoader.LoadNativeDlls` (`Core/Mods/ModNativeLoader.cs`), called from `ModRegistry.Initialize()` before managed mod code runs:

1. Collects platform-appropriate files (`.dll` on Windows, `.so` on Linux, `.dylib` on macOS)
2. Copies them to the cache directory (`user://mod_cache/native/`) so zip-based mods can serve native code
3. Registers a `DllImportResolver` so P/Invoke (`[DllImport]`) declarations resolve against the mod-provided native code

If multiple mods provide the same library name, the first one wins; duplicates are logged as warnings. On reload, `ModRegistry.Reload()` calls `ModNativeLoader.FreeAll()` to release all native handles so the files can be overwritten on the next load cycle.

### Precompiled DLLs (`dll/` directory)

A mod may ship precompiled .NET assemblies in a `dll/` directory. During `ModRegistry.Initialize()`, `ModAssemblyLoader.LoadPrecompiledDlls` (`Core/Mods/ModAssemblyLoader.cs`) scans that directory recursively, copies each `.dll` to the assembly cache (`{modId}_dll_{fileName}`), loads it, and runs the standard `RegisterAssembly` fan-out (the same registries listed on the [C# Source Code page](../src/)).

This runs **before** Roslyn compilation of `sources`, so source-based mods can reference types from precompiled API assemblies. Each loaded precompiled assembly also gets a `DllImportResolver` installed (`ModNativeLoader.InstallResolverOnAssembly`) for its own P/Invoke calls.
