# Mod Settings

Mods can expose user-configurable settings that appear in the in-game settings menu under a dedicated "Mod Settings" tab. The UI shows a vertical list of mods (only those that have settings) on the left; clicking a mod name displays its settings controls on the right.

## Architecture

The system mirrors the save system (`IModSaveable`) using a similar dispatch pattern:

```
Mod assembly loaded
  → ModSettingsRegistry.RegisterAssembly(assembly, modId)
    → Finds IModSettings implementation via reflection
    → Stores (modId, instance) pair

Boot time:
  → ModSettingsRegistry.LoadAll()
    → For each registered mod: load user://mod_settings/{modId}.json
    → Call mod.ReadValues(blob)

Settings menu opened:
  → ModSettingsRegistry.GetModsWithSettings()
    → Returns sorted list of mod IDs (only mods with IModSettings)
  → BuildModList() — creates buttons in left panel
  → On click: BuildModSettingsPanel(modId)
    → mod.GetDefinitions() → list of ModSettingDefinition
    → Creates UI controls (CheckButton, SpinBox, OptionButton, etc.)
    → mod.WriteValues(blob) → populates controls with current values

Setting changed:
  → Read all control values into DataBlob
  → ModSettingsRegistry.SaveModSettings(modId, blob)
  → ModSettingsRegistry.ApplyValues(modId, blob)
    → mod.ReadValues(blob) — immediate apply

Reset:
  → ModSettingsRegistry.DeleteModSettings(modId)
  → Rebuild panel with DefaultValues from definitions
```

## Core concepts

### IModSettings interface

Mods implement one class implementing `IModSettings`. It is auto-discovered when the mod assembly loads.

```csharp
public interface IModSettings
{
    IReadOnlyList<ModSettingDefinition> GetDefinitions();
    void WriteValues(DataBlob blob);
    void ReadValues(DataBlob blob);
}
```

- **GetDefinitions()** — Declares which settings the mod has. Called every time the settings panel opens.
- **WriteValues(blob)** — The mod writes its current values into the blob. Called to populate UI controls and to persist.
- **ReadValues(blob)** — The mod reads values from the blob and applies them. Called on boot (from persisted data) and on every UI change.

### ModSettingDefinition

| Field | Type | Description |
|---|---|---|
| `Key` | `string` | Unique key within this mod (e.g. `"atb_speed"`) |
| `Name` | `string` | Localisation key shown as label (e.g. `"mod_ffbattle/atb_speed"`) |
| `Type` | `SettingType` | Widget type: `Bool`, `Integer`, `Float`, `String`, `Enum`, `Slider` |
| `DefaultValue` | `string` | String representation of the default. Used when no persisted value exists and on Reset |
| `Options` | `Dict<string,string>` | For `Enum` only: maps internal values to localisation keys |
| `Min` / `Max` / `Step`| `float` | For `Slider` only |

### Setting types and UI controls

| Type | Godot control | Description |
|---|---|---|
| `Bool` | `CheckButton` | On/off toggle |
| `Integer` | `SpinBox` (step=1) | Whole-number input |
| `Float` | `SpinBox` (step=0.1) | Decimal input |
| `String` | `LineEdit` | Free-form text input |
| `Enum` | `OptionButton` | Dropdown, populated from `Options` dictionary |
| `Slider` | `HSlider` + value label | Range slider with configurable min/max/step |

### Persistence

Each mod's settings are stored as a JSON-serialised `DataBlob` at `user://mod_settings/{modId}.json`. The file contains key-value pairs matching the mod's definitions.

The core handles all file I/O; the mod never touches the filesystem.

### Lifecycle

```
┌──────────────────────────────────────────────────────────────┐
│ Boot                                                         │
│   ModRegistry.Initialize()                                   │
│     → ModAssemblyLoader.RegisterAssembly()                   │
│       → ModSettingsRegistry.RegisterAssembly(asm, modId)     │
│         → Finds IModSettings, instantiates, stores           │
│   SceneManager.Initialize()                                  │
│     → ModInitializerRegistry.NotifyResourcesReady()          │
│     → ModSettingsRegistry.LoadAll()                          │
│       → For each mod: loads JSON, calls ReadValues()         │
└──────────────────────────────────────────────────────────────┘
                              │
┌─────────────────────────────▼────────────────────────────────┐
│ Settings menu opened                                         │
│   BuildModList()                                             │
│     → GetModsWithSettings() → buttons in list                │
│   Select first mod                                           │
│   BuildModSettingsPanel(modId)                               │
│     → GetDefinitions() → controls in right panel             │
│     → WriteValues(blob) → populate controls                  │
└──────────────────────────────────────────────────────────────┘
                              │
┌─────────────────────────────▼────────────────────────────────┐
│ User changes a setting                                       │
│   OnModSettingChanged()                                      │
│     → Read all controls → DataBlob                           │
│     → SaveModSettings(modId, blob) → disk                    │
│     → ApplyValues(modId, blob) → mod.ReadValues()            │
└──────────────────────────────────────────────────────────────┘
```

## Implementing IModSettings in a mod

### Minimal example — simple toggles and sliders

```csharp
using System.Collections.Generic;
using Cthangover.Core.Mods;
using Cthangover.Core.Settings;

public class MyModSettings : IModSettings
{
    private float _explosionRadius = 1.5f;
    private bool _debugMode;

    public IReadOnlyList<ModSettingDefinition> GetDefinitions()
    {
        return new List<ModSettingDefinition>
        {
            new()
            {
                Key = "explosion_radius",
                Name = "mymod/explosion_radius",
                Type = SettingType.Slider,
                DefaultValue = "1.5",
                Min = 0.5f,
                Max = 10f,
                Step = 0.5f
            },
            new()
            {
                Key = "debug_mode",
                Name = "mymod/debug_mode",
                Type = SettingType.Bool,
                DefaultValue = "false"
            }
        };
    }

    public void WriteValues(DataBlob blob)
    {
        blob.SetFloat("explosion_radius", _explosionRadius);
        blob.SetBool("debug_mode", _debugMode);
    }

    public void ReadValues(DataBlob blob)
    {
        _explosionRadius = blob.GetFloat("explosion_radius", 1.5f);
        _debugMode = blob.GetBool("debug_mode", false);
    }
}
```

![Mod Settings example view](docs/mods/settings/mod_example.png)

### Example with enum

```csharp
new ModSettingDefinition
{
    Key = "difficulty",
    Name = "mymod/difficulty",
    Type = SettingType.Enum,
    DefaultValue = "normal",
    Options = new Dictionary<string, string>
    {
        { "easy",   "mymod/diff_easy" },
        { "normal", "mymod/diff_normal" },
        { "hard",   "mymod/diff_hard" }
    }
}
```

![Mod Settings example view](docs/mods/settings/mod_example_enum.png)

The `Options` dictionary maps internal values (stored in the blob) to localisation keys (shown in the dropdown). The mod provides translations in its `locale/` files.

### Localisation

Every `Name` field and every value in `Options` is a localisation key resolved via `TranslationServer.Translate()`. Add entries to your mod's `locale/` files:

```
# locale/en.properties
mymod/explosion_radius = Explosion Radius
mymod/debug_mode = Debug Mode
mymod/difficulty = Difficulty
mymod/diff_easy = Easy
mymod/diff_normal = Normal
mymod/diff_hard = Hard
```

### Schema changes

**`DataBlob.Version` is NOT persisted for mod settings.** `ModSettingsRegistry.SaveModSettings` writes only the key-value payload (`blob.GetBytes()`), and on load the registry constructs a fresh `DataBlob(payload)`, which resets `Version` to its default of 1. (`Version` *is* persisted through the main save system's `SavePackage` — just not here.)

Detect schema changes by reading keys with defaults instead — missing keys resolve to your fallback values, so added fields need no migration logic:

```csharp
public void WriteValues(DataBlob blob)
{
    blob.SetFloat("explosion_radius", _explosionRadius);
    blob.SetFloat("knockback_force", _knockbackForce); // added later
}

public void ReadValues(DataBlob blob)
{
    _explosionRadius = blob.GetFloat("explosion_radius", 1.5f);
    _knockbackForce = blob.GetFloat("knockback_force", 3.0f); // default for old saves
}
```

## Important rules

- **One `IModSettings` per mod** — create a single implementation in your assembly.
- **Always provide `DefaultValue`** — used when no persisted value exists and on Reset.
- **Use `DataBlob` key-value API** — `GetInt/SetInt`, `GetFloat/SetFloat`, `GetString/SetString`, `GetBool/SetBool`.
- **Always call with defaults** — `blob.GetInt("key", defaultVal)` so missing keys don't break.
- **Key names are persistent** — changing a `Key` creates a new setting; the old value is lost.
- **Provide localisation** — add translations for all `Name` and `Options` values in `locale/`.
- **The `version` field in `manifest.json`** is used by the save system for validation; mod settings have no persisted schema version (`DataBlob.Version` is reset to 1 on load) — handle schema changes via read-with-defaults.
- **Declare `"sources"` in manifest.json** — your `.cs` files must be listed in the `sources` field of `manifest.json` for the compiler to pick them up. See [Manifest — sources](../configs/index.md#manifestjson) for details.
