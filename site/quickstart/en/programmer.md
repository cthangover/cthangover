# Level 3 — Programmer: C# Mod

<div class="qs-meta">
<span>C#</span>
<span>20 minutes</span>
<span>Roslyn compilation</span>
</div>

You'll write a C# mod that adds a custom DSL action and menu settings to the game. The code is compiled on the fly via Roslyn when the game starts — you need neither Godot nor `dotnet build`.

---

## Step 1: Mod structure

```filestree
mods/
└── quickstart_programmer/
    ├── manifest.json
    ├── src/
    │   ├── MyModSettings.cs
    │   ├── MyMod.cs
    │   └── MyEffectAction.cs
    ├── scenarios/
    │   └── my_effect_demo.scenario
    └── locale/
        ├── ru.properties
        └── en.properties
```

---

## Step 2: manifest.json

`mods/quickstart_programmer/manifest.json`:

```jsonc
{
  "id": "quickstart_programmer",                // Mod ID
  "name": "My Code Mod",                        // Mod name shown in the mod list
  "description": "New action and mod settings", // Mod description shown in the mod list
  "sources": ["src/*.cs"],                      // Pattern for collecting source files (all ".cs" files)
  "author": "programmer",                       // Author
  "depends": ["cthangover_game"]              // Dependency on the main game's content (so manual mod load ordering is not required)
}
```

The `sources` field specifies which C# files to compile via Roslyn. Compilation errors (with file and line) go to the log under the `MODS_COMPILE` category.

---

## Step 3: Entry point — IModInitializer

`mods/quickstart_programmer/src/MyMod.cs`:

```csharp
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.MyCodeMod
{
    public class MyMod : IModInitializer
    {
        public void OnModLoaded(string modId) { }
        
        public void OnModResourcesReady()
        {
            
            GameLogger.Log("MY_MOD", "My mod loaded!", LogLevel.Message);
        }
        
    }
}
```

The `IModInitializer` interface is the lifecycle entry point of a mod's C# code; implementations are discovered via reflection. `OnModLoaded` is called when each mod loads (not only yours — `modId` holds the id of the mod that was loaded), `OnModResourcesReady` — once after all mods and their resources are loaded.

> Use `GameLogger.Log(...)` instead of `GD.Print(...)` if you want to work with logs.

---

## Step 4: Menu settings — IModSettings

`mods/quickstart_programmer/src/MyModSettings.cs`:

```csharp
using System.Collections.Generic;
using Cthangover.Core.Mods;
using Cthangover.Core.Settings;

public class MyModSettings : IModSettings
{
    private float _volume = 1.0f;
    private bool _enabled = true;

    public IReadOnlyList<ModSettingDefinition> GetDefinitions()
    {
        return new List<ModSettingDefinition>
        {
            new()
            {
                Key = "effect_volume",
                Name = "mymod/effect_volume",
                Type = SettingType.Slider,
                DefaultValue = "1.0",
                Min = 0f,
                Max = 2f,
                Step = 0.1f
            },
            new()
            {
                Key = "enable_mod",
                Name = "mymod/enable_mod",
                Type = SettingType.Bool,
                DefaultValue = "true"
            }
        };
    }

    public void WriteValues(DataBlob blob)
    {
        blob.SetFloat("effect_volume", _volume);
        blob.SetBool("enable_mod", _enabled);
    }

    public void ReadValues(DataBlob blob)
    {
        _volume = blob.GetFloat("effect_volume", 1.0f);
        _enabled = blob.GetBool("enable_mod", true);
    }
}
```

`GetDefinitions` describes the controls, `WriteValues`/`ReadValues` write and read the values symmetrically; the core takes care of saving to disk and of the UI. After the mod loads, your mod appears in "Settings" → the "Mod Settings" tab with a slider and a toggle.

---

## Step 5: Custom DSL action — IScenarioAction

Let's add a new action `my_effect`, available in `.scenario` files. `mods/quickstart_programmer/src/MyEffectAction.cs`:

```csharp
using Cthangover.Core.Actions;
using Cthangover.Core.Utils;

public class MyEffectAction : IScenarioAction
{
    public string Name => "my_effect";
    
    public void Run(IActionContext context)
    {
        GameLogger.Log("MY_MOD", "New action executed!", LogLevel.Message);
    }
}
```

Now in any `.scenario` file you can write:

```scenario
action my_effect
```

The engine will find your class via reflection and call `Run`. Through `context` you get dialog variables (`GetParam`) and game services: quests, characters, lighting, the scene.

---

## Step 6: A scenario to try it

To see the action in action, let's call it when the player steps out of the house. `mods/quickstart_programmer/scenarios/my_effect_demo.scenario`:

```scenario
scene: home_outside
priority: 0
---
action my_effect
end
```

`priority: 0` — our scenario runs before the standard one (`priority: 10`), and after `end` the game continues as usual: the scene opens its normal menu.

---

## Step 7: Localization

The setting labels are the localization keys from `Name`. `mods/quickstart_programmer/locale/ru.properties`:

```properties
mymod/effect_volume = Громкость эффекта
mymod/enable_mod = Включить мод
```

`mods/quickstart_programmer/locale/en.properties`:

```properties
mymod/effect_volume = Effect volume
mymod/enable_mod = Enable mod
```

---

## Step 8: Connect the mod to the game

The game does not load every installed mod — only the mods that belong to the game's collection:

1. In the main menu, open **"Mods & Collections"** → the **"Collections"** tab.
2. On the **Cthangover** game card, click **"Edit scheme"**.
3. Select any mechanic on the scheme (for example, **"Story"**) and click **"Add mod"**.
4. Find your mod in the list (by the name from `manifest.json`) and click **"Add"**.
5. Click **"Save"** and confirm. Changes apply the next time the game starts.

> **For developers.** From source, without the menu: `godot --path . -- --game=cthangover --new --with-mods=quickstart_programmer --scene=home_outside`.

---

## Step 9: Testing

Restart the game:

1. Open "Settings" → the "Mod Settings" tab → your mod with a slider and a toggle.
2. The game log (`user://logs/cthangover.log`, on Windows — `%APPDATA%\cthangover\logs\cthangover.log`) contains `[MY_MOD] My mod loaded!` after loading.
3. Step out of the house — `[MY_MOD] New action executed!` appears in the log.

---

## What's next?

| Want to | Go to |
|---|---|
| Subscribe to scene enter/exit | [Scene Subscriptions](site/docs/mods/configs/) |
| Create your own battle mode | [Battle System](site/docs/mods/battle/) |
| Build a tool window | [Creating a Custom Tool](site/docs/mods/editor_tools/tutorial) |
| Write mods in GDScript | [GDScript in Mods](site/docs/mods/src/gdscript/) |
| All available interfaces | [C# Source Code in Mods](site/docs/mods/src/) |

---

## Download the finished example

<a href="examples/quickstart_programmer.zip" class="qs-download" download>
<svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><path d="M12 5v14"/><path d="M5 12l7 7 7-7"/><path d="M4 22h16"/></svg>
Download quickstart_programmer.zip
</a>

The downloadable mod is in English. Unpack the archive into `mods/quickstart_programmer/` or put the `.zip` into `user://mods/`, then connect the mod to the game (step 8).
