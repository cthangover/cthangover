# Kernel

The C# kernel in `Core/` provides the boot pipeline, runtime compilation, resource lookup, scene and event infrastructure, persistence, application settings, and diagnostics. A collection supplies the game selection and entry scene. The kernel exposes APIs for gameplay assemblies to register their own systems.

| Guide | Covers |
|---|---|
| [Launch arguments](launch-arguments.md) | Typed parameters, the startup snapshot, and runtime overrides |
| [DebugHost](debug-host.md) | Local command transport, diagnostics, and command results |
| [Memory and caches](memory.md) | Observable collections, cache limits, and memory queries |
| [Display settings](display-settings.md) | Default, persisted, and live settings, including 3D render scale |
| [Resource discovery](/docs/resources/) | Scanners, loaders, providers, and source precedence |
| [Installation defaults](/docs/mods/configs/game_config) and [kernel configuration](/docs/mods/configs/mod_config) | The two installation-wide JSON files |

## Build and run

From the repository root, use **Godot 4.6.3 .NET** and **.NET SDK 10.0.201** or a later stable 10.0 feature band. The kernel targets `net10.0`. Desktop export also requires matching Godot .NET templates.

```shell
dotnet build Cthangover.csproj
godot --path .
```

`Cthangover.csproj` compiles `Core/**/*.cs`. During launch, Roslyn compiles the selected gameplay assemblies and runs the Godot source generators when available. The release includes CLR 10.0.15, the .NET reference pack, the core reference assembly and its verification receipt, and the Godot source generator. The shipped reference pack lets the runtime compiler work on a machine without an installed SDK.

## Boot and reload

1. `ModBootPipeline.PreparePaths` resolves roots and runtime paths on the Godot thread.
2. `DiscoverAndCompile` scans those roots, selects candidates, resolves dependencies, and prepares assemblies. Launcher boot runs this stage on a worker.
3. `FinalizeGodotInit` registers assemblies, loads resources, and calls initializers on the Godot thread. When boot reaches `Ready`, it checks whether to enable the reload watcher and installs DebugHost.

Assemblies use collectible load contexts. When a reload changes the selected set, the kernel unregisters the affected assemblies and their contributions before releasing the contexts. Use the `MODS_COMPILE` log category for compiler diagnostics and `MODS_REGISTRY` for discovery or manifest errors.

## Compilation cache

`use_assembly_cache` in `config/mod_config.json` enables reuse across launches. The runtime verifies the core reference assembly against the loaded kernel and the build receipt. A kernel implementation-only edit can reuse compatible artifacts. A change to the reference assembly invalidates the corresponding artifacts. Missing or stale metadata falls back to the loaded kernel's module version ID, which conservatively invalidates cached output when the kernel binary changes. Source, dependency, and compiler inputs also affect reuse.
