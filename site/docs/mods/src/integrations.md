# Integrations, assembly consumers and reload

## Conditional integrations

An integration is glue that joins the loaded mod set only when its host and every named partner are present. It does not pull a partner into a game that did not select it. The host declares:

```json
{"integrations": [{"id": "battles", "with": ["turn_battle_base"]}]}
```

`integrations/battles/manifest.json` and its source/data files form the virtual mod `<host>__battles`. An optional `path` changes that folder. The virtual mod implicitly depends on its host and partners; its manifest can declare additional dependencies. It is not a separate player-toggleable mod card. Integrations are recomputed at boot and in-process game switches.

For example, `rpg__battles` connects the RPG world to turn-based battles and random encounters. `turn_battle_base__quests` applies battle results to quests. The `rpg` host can therefore be used without any battle engine, and the battle platform without quests. These are production integrations; `test_suites` are a separate opt-in test mechanism.

## Discovering extensions

`ModAssemblyLoader.RegisterAssembly(assembly, modId)` sends loaded assemblies through `AssemblyConsumerRegistry`. Kernel consumers cover actions, initializers, save/settings handlers, discovery, tools, scene events, TSCN building and asset resolvers. A subsystem owned by a mod can declare a public parameterless subclass of `ModTypeConsumer<T>` with a unique `Id`, `Add` and `Remove` methods.

The consumer receives implementations from already registered assemblies, including its own, and from later assemblies. Unloading calls `Remove` for exactly that assembly's contributions. Registries must release those instances and references; a static list retaining an unloaded implementation prevents collectible assemblies from being released. This mechanism is used by expression strategies, avatar renderer factories, atmosphere emitters and other mod-owned extension systems.

The namespace is `Cthangover.Core.Mods.Consumers`; source is `Core/Mods/Consumers/`. A new registry declares a consumer instead of inserting a subsystem-specific branch in the kernel's assembly loader. See [C# sources](index.md) for the other compilation contracts.

## Hot reload

When run from the Godot editor, file watching is enabled by default except for automated tests/atlas runs. Exported builds do not watch by default. `--hot-reload` opts in explicitly and `--no-hot-reload` wins over it. These are Godot user arguments after `--`.

The watcher considers source, manifests, data, scenes, locale and asset changes, while ignoring build folders, editor bookkeeping and temporary files. It prepares and compiles a candidate set before applying it. A rejected compile leaves the previous running version in place and reports the error. Applied changes go through normal unload and registration, and publish a mod-set reload event. Data changes invalidate the relevant resolvers.

Reloadable mods must unsubscribe events, free owned UI/native resources, release callbacks and discard old plugin instances on unload. Godot nodes, live avatar adapters and atmosphere emitters belong to their hosts; merely swapping an assembly does not transfer ownership. In-process game selection uses this same mod-set lifecycle, so test repeated game switches as well as initial boot.

Implementation: `Core/Mods/HotReload/`, `Core/Mods/Pipeline/ModSetReloader.cs`, `Core/Mods/ModIntegrations.cs`. The `hot_reload` and `resettables` suites exercise compile rejection and repeated switching.
