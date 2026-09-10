# Mod Save API

## Implementing IModSaveable in a mod

Your mod must provide a class implementing `IModSaveable`. It is auto-discovered via reflection when your mod assembly loads.

### API

```csharp
public interface IModSaveable
{
    // Called on save. Slot metadata (ModId, ModName, ModVersion) is
    // already filled by the core. Set MinCoreVersion, IsRequired,
    // CustomData, and write Data[...].
    void OnSave(ModSlot slot);

    // Preferred: explicit contract. Return false + error message on
    // failure. Default impl calls OnLoadVoid for backward compat.
    bool OnLoad(ModSlot slot, out string errorMessage);

    // Legacy: throw-based error handling. Override instead of OnLoad
    // if you prefer exceptions for error reporting.
    void OnLoadVoid(ModSlot slot);
}
```

### Minimal example

```csharp
using Cthangover.Core.Mods;
using Cthangover.Core.Settings;

public class MyModSaveHandler : IModSaveable
{
    public void OnSave(ModSlot slot)
    {
        // ModId, ModName, ModVersion already set by core.
        // Set only what the core needs for validation:
        slot.Metadata.IsRequired = true;

        var blob = new DataBlob();
        blob.SetInt("Health", player.Health);
        blob.SetInt("Mana",   player.Mana);
        blob.SetString("Name", player.Name);

        slot.Data["Stats"] = blob;
    }

    public bool OnLoad(ModSlot slot, out string errorMessage)
    {
        if (slot.Data.TryGetValue("Stats", out var blob))
        {
            player.Health = blob.GetInt("Health", 100);
            player.Mana   = blob.GetInt("Mana", 50);
            player.Name   = blob.GetString("Name", "Hero");
            errorMessage = null;
            return true;
        }

        errorMessage = null;
        return true; // No data → nothing to restore, not an error
    }
}
```

### Versioned migration

When your data schema changes, use `DataBlob.Version` to handle migration:

```csharp
public void OnSave(ModSlot slot)
{
    var data = new PlayerStateV2 { Health = 80, Mana = 40, Stamina = 100 };
    slot.Data["FullState"] = new DataBlob(version: 2, data);
}

public bool OnLoad(ModSlot slot, out string errorMessage)
{
    if (!slot.Data.TryGetValue("FullState", out var blob))
    {
        errorMessage = null;
        return true; // no data — nothing to restore
    }

    if (blob.Version == 2)
    {
        var state = blob.ReadJson<PlayerStateV2>();
        Apply(state);
        errorMessage = null;
        return true;
    }

    if (blob.Version == 1)
    {
        var old = blob.ReadJson<PlayerStateV1>();
        Apply(new PlayerStateV2
        {
            Health  = old.Health,
            Mana    = old.Mana,
            Stamina = 100, // default for new field
        });
        errorMessage = null;
        return true;
    }

    errorMessage = $"Unsupported data version: {blob.Version}";
    return false;
}
```

### Binary data

`DataBlob` can hold raw bytes: construct it from a `byte[]`, or use the binary API (`GetBytes`/`SetBytes`) to read and write raw data.

```csharp
public void OnSave(ModSlot slot)
{
    slot.Data["Skin"] = new DataBlob(textureBytes);
}

public bool OnLoad(ModSlot slot, out string errorMessage)
{
    if (slot.Data.TryGetValue("Skin", out var blob))
    {
        byte[] bytes = blob.GetBytes();
        // Decode the bytes yourself, e.g. via Godot's Image + ImageTexture.
        ApplySkin(bytes);
    }

    errorMessage = null;
    return true;
}
```

### Marking a mod as optional

If a save can be loaded without your mod, set `IsRequired = false`:

```csharp
slot.Metadata.IsRequired = false;
```

The core will log a warning and skip the mod's data if the mod is not installed.

### Compatibility guard (MinCoreVersion)

If your save data depends on a specific core API, set the minimum version:

```csharp
slot.Metadata.MinCoreVersion = "0.2.0";
```

On load, if the current core version is below `0.2.0`, the load is aborted with an error message.

## Registration & dispatch (ModSaveableRegistry)

You never invoke the registry yourself — implementations are discovered **automatically via reflection** when your mod assembly is loaded (`ModAssemblyLoader.RegisterAssembly` scans it with `Reflections.FindAndCreate<IModSaveable>` and registers every implementation under your mod's ID).

| Method | When | Behaviour |
|---|---|---|
| `ModSaveableRegistry.RegisterAssembly(assembly, modId)` | Mod assembly load | Scans the assembly and creates one instance per `IModSaveable` implementation |
| `CollectSaveData() : Dictionary<string, ModSlot>` | Save time | Per registered mod: builds a `ModSlot` with `ModId`, `ModName`, `ModVersion` pre-filled from `IModInfo`, then calls `OnSave(slot)` |
| `DispatchLoad(Dictionary<string, ModSlot>) : List<string>` | Load time (phase 3) | Per registered mod whose ID is present in the loaded dictionary, calls `OnLoad(slot, out error)`; collects error strings (empty list = success) |

Error handling rules:

- An exception thrown from `OnSave` is logged and **that mod's slot is skipped**; other mods keep saving.
- If you override neither load method, the defaults chain into `void OnLoadVoid(ModSlot)`, which throws `NotSupportedException` — override at least one of `bool OnLoad(...)` or `OnLoadVoid`.
- A `false` return from `OnLoad` (or an exception from either overload) is converted to `Mod '<id>' (<name>): <message>`; any such error aborts the whole load after phase 3.

## Save slot API (SaveService)

| Member | Description |
|---|---|
| `Path : string` (static property) | Saves directory — globalised `user://saves` at runtime, `"saves"` otherwise |
| `Save(string fileName)` | Build `"__core__"` slot, collect mod slots, write JSON |
| `Load(string fileName) : bool` | Deserialize, validate + restore + dispatch |
| `GetSaveSlots(int slotCount) : List<SaveSlotInfo>` | Enumerate slots with preview metadata |
| `DeleteSave(string fileName)` | Delete JSON + PNG screenshot |

## Slot preview API (RuntimeSerializer)

| Member | Description |
|---|---|
| `RuntimeSerializer.ReadSlotPreview(ModMetadata meta) : CoreSlotPreview` | Deserialises the preview JSON from `ModMetadata.CustomData`; returns a default (empty) preview when the field is absent or malformed |
| `RuntimeSerializer.CoreSlotPreview` | Nested public DTO: `CurrentSceneName` (`string`), `GameTime` (`long`), `CharacterCount` (`int`) |

Lets a custom save-slot UI read slot info without deserialising any game-state blobs.

## Save screenshots (SaveScreenshotService)

| Method | Description |
|---|---|
| `CaptureAndSave(string fileName)` | Grabs the current background texture from the `ViewBox` node, downscales it to 320×180 with Lanczos interpolation, writes `user://saves/{fileName}.png` (same name sanitisation as the JSON save) |
| `LoadScreenshot(string fileName) : Texture2D` | Reads the thumbnail PNG back as a `Texture2D`; returns `null` if the file is missing or fails to decode |

Notes:

- If no valid background texture exists at capture time, a solid dark-grey **64×64 fallback** image is saved instead.
- The thumbnail is a **separate PNG file** next to the save JSON — it is not part of `SavePackage`. Only the textual preview (`CurrentSceneName`, `GameTime`, `CharacterCount`) is embedded in the save, inside `ModMetadata.CustomData` of the `"__core__"` slot (see [Data Model](data-model.md)).
- `DeleteSave(fileName)` removes both the JSON and the PNG.
