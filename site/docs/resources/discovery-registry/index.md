# Discovery Registry — `ModDiscoveryRegistry`

The registry discovers concrete `IModScanner` and `IModLoader` implementations by reflection. `Initialize()` scans the assemblies available at initialization and can be called more than once. The assembly lifecycle registers later contributions with `RegisterAssembly(assembly)` and removes them with `UnregisterAssembly(assembly)`.

| API | Behaviour |
|---|---|
| `Scanners`, `Loaders` | Return thread-safe snapshots in registration order |
| `FindScanner(url)`, `FindLoader(url)` | Walk the corresponding snapshot in reverse registration order. Return the first strategy whose `IsCanProvide` accepts the URL, or `null` |
| `RegisterAssembly(assembly)` | Discover scanners and loaders in that assembly |
| `UnregisterAssembly(assembly)` | Remove the strategies owned by that assembly |

## Lookup during boot

`ModBootPipeline.PreparePaths` gets roots from each scanner and keeps a reference to the scanner that supplied each root. Boot runs that scanner for its own root and calls `FindLoader` for each entry. A strategy registered later becomes visible to subsequent lookups. The boot root list is captured during path preparation.

Loader order and source priority serve different purposes. Loader order selects a way to open one entry. Source priority and version decide which discovered candidate gets a shared ID. See [Resources](/docs/resources/) for the selection rules.
