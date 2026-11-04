# Mod save API

Implement a public `IModSaveable` class in normal mod sources. The assembly consumer discovers it and registers the handler under its mod ID. Use one handler per mod and version each logical `DataBlob` independently.

## Handler contract

```csharp
namespace ExampleMod
{
    using Cthangover.Core.Mods;
    using Cthangover.Core.Settings;

    public class CounterSave : IModSaveable
    {
        public static int Visits { get; set; }

        public void OnSave(ModSlot slot)
        {
            // ModId, ModName and ModVersion are already populated.
            slot.Data["Visits"] = new DataBlob(1, Visits);
        }

        public bool OnLoad(ModSlot slot, out string errorMessage)
        {
            errorMessage = null;
            if (slot?.Data == null || !slot.Data.TryGetValue("Visits", out var blob))
                return true;
            if (blob.Version != 1)
            {
                errorMessage = "Unsupported Visits blob version";
                return false;
            }
            Visits = blob.ReadJson<int>();
            return true;
        }

        public void OnJoinedExistingGame()
        {
            // Optional: supply starter state when no slot was restored.
        }
    }
}
```

Reset new-game state through the normal reset lifecycle as well; loading is not the only way a game starts. `OnJoinedExistingGame` is called for new, dormant or rejected mechanics with no restored slot. Do not use it to overwrite a successfully restored value.

The preferred load method is `bool OnLoad(ModSlot, out string)`. The legacy `void OnLoadVoid(ModSlot)` may throw; the default implementation delegates to it. Override at least one or the default throws `NotSupportedException`. `OnJoinedExistingGame()` has a no-op default. There is no `IsRequired` metadata property.

## Blobs and migrations

Use `slot.Data.TryGetValue` for missing keys and inspect `blob.Version` before reading a payload. For a schema upgrade, keep branches reading old DTOs and convert them to the current DTO; write only the current version. Return false with a descriptive error if conversion is impossible.

- `new DataBlob(2, data)` stores a versioned JSON payload, read with `ReadJson<T>()`.
- Key-value fields use `SetInt`/`GetInt`, `SetFloat`/`GetFloat`, `SetString`/`GetString`, `SetBool`/`GetBool`.
- Raw bytes use `new DataBlob(bytes)` or `SetBytes`/`GetBytes`.

Choose one access mode per blob. Key-value writes and whole JSON writes replace one another. The core fills `ModId`, `ModName`, `ModVersion`; a handler may set `MinCoreVersion`, `CustomData` and its own data. A mod slot requiring a newer core is kept dormant instead of handed to the old implementation.

## Discovery and error handling

`ModAssemblyLoader.RegisterAssembly(assembly, modId)` fans out through `ModSaveableConsumer` and `ModSaveableRegistry`.

| Registry operation | Behaviour |
|---|---|
| `RegisterAssembly(assembly, modId)` | Discover save handlers |
| `CollectSaveData()` | Create metadata and call each handler's `OnSave` |
| `DispatchLoad(modData, failedModIds)` | Restore compatible live slots; collect failures |
| `NotifyJoined(restoredModIds)` | Notify handlers without a restored slot |

An `OnSave` exception is logged and that handler's new slot is skipped. A false/throwing `OnLoad` is reported and its original slot is retained as dormant (`LoadFailed`). Other compatible mod slots continue loading. The core does not inspect the payload or migrate it on the handler's behalf. Do not depend on the order of unrelated save handlers.

## Save browser API

| `SaveService` member | Purpose |
|---|---|
| `SaveDirectory`, `Path` | Active game's Godot directory and globalized path |
| `Save(fileName)`, `Load(fileName)` | Save/load the active game's logical slot |
| `ApplyPackage(package, sourceName)` | Restore an already read package |
| `GameDirectory(gameId)` | Directory for a specific game |
| `ListSaves(gameId)` | Unlimited saved-slot list with previews |
| `GetSaveSlots(slotCount)` | Compatibility grid with empty slot placeholders |
| `ListGamesWithSaves()` | Games represented in the save browser |
| `NextSlotName(gameId)` | Next available logical name |
| `ReadPackage(gameId, fileName)` | Read without applying |
| `BackupSave(gameId, fileName)` | Create a backup |
| `CopyGameSaves(fromGameId, toGameId)` | Copy saves for a duplicated game and rewrite the game ID |
| `LastLoadDormancy` | Classification from the last applied save |
| `PurgeDormantData()` | Explicitly remove dormant slots from saves and current session |
| `DeleteSave(fileName)` | Delete active-game JSON and PNG |

Named-game saves live at `user://saves/<game_id>/`; unscoped development saves use `user://saves/`. Slot names are sanitized for filesystem use.

## Preview and screenshots

`RuntimeSerializer.ReadSlotPreview(metadata)` reads `CurrentSceneName`, `GameTime`, `CharacterCount` from the reserved slot's `CustomData` without reading gameplay blobs. `SaveSlotInfo` also exposes game identity and the saved mod composition; see [Data model](data-model.md).

`SaveScreenshotService.CaptureAndSave(fileName)` captures the background view and writes a 320×180 PNG beside the JSON. If no background is available it uses a solid fallback image before scaling. `LoadScreenshot(fileName)` uses the active game's directory; `LoadScreenshotFromPath(path)` can load a preview from another game's directory. Both return null if the PNG is absent or cannot be decoded. Screenshots are separate files, not package blobs.
