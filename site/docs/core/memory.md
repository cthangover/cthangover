# Memory and caches

`ModMemoryService.Shared` reports process-wide .NET heap metrics alongside named, opt-in registrations. Each registered store belongs to a load generation. Kernel stores use the owner ID `__core__`. The registry holds weak references and drops the scope on unload. Each owner holds its own strong references and is responsible for invalidation and for releasing owned resources.

| API | Use |
|---|---|
| `IStorageMetrics` | Report counts and optional cost estimates. `ReadPreview` can project entries into detached text |
| `ICache` | Extend the metrics contract with `Clear()` for rebuildable data |
| `CreateList`, `CreateHashSet`, `CreateDictionary` on `ModMemoryScope` | Register observable collections for retained data |
| `CreateCache` on `ModMemoryScope` | Register a dictionary cache with hit and miss counts. The owner decides when to invalidate or clear it |
| `BoundedCache<TKey, TValue>` | Bound a cache by entry count with least-recently-used eviction. An optional release callback supports owned resource lifetimes |
| `Track` and `Untrack` | Register or remove a custom store or an adapter implementing `IStorageMetrics` |

Use `ModMemoryService.Shared.For(assembly)` to get the scope for an assembly loaded by the mod pipeline. Use `TryFor(assembly)` in standalone tools or tests where the assembly may have no scope. A bounded cache or custom collection can publish metrics by registering the instance through `Track`.

## Query via DebugHost

```json
{"command":"memory.stats","mod":"__core__","offset":0,"limit":20}
```

Without `mod`, `memory.stats` lists stores from all owners. `managed_heap_process` covers the whole managed heap even when you filter store results by owner. Store byte values are estimates reported by the store. A plain unregistered .NET collection is visible only as part of the process total. Native or GPU usage requires estimates from the owner and the heap total covers managed memory only.

```json
{"command":"memory.inspect","mod":"__core__","name":"<name from memory.stats>","limit":10}
```

Use the returned `next_offset` to paginate and pass `generation` from `memory.stats` to keep a request tied to a particular load. The page limit is 100 entries. `entries: null` means the store has no preview. Previews are bounded and may truncate. The commands are read-only and use the store's own metrics and projection.
