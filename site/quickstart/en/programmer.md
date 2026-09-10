# Level 3 — Programmer: C# Mod

<div class="qs-meta">
<span>C#</span>
<span>20 minutes</span>
<span>Roslyn compilation</span>
</div>

You'll write a C# mod that adds to the game: a custom DSL action and settings in the menu. Code is compiled on the fly via Roslyn — no Godot needed.

---

## Step 1: Mod structure

```filestree
mods/
└── quickstart_programmer/
    ├── manifest.json
    ├── src/
    │   ├─── MyModSettings.cs
    │   ├─── MyMod.cs
    │   └─── MyEffectAction.cs
    └── locale/
        └─── en.properties
```

---

## Step 2: manifest.json

`mods/quickstart_programmer/manifest.json`:

```jsonc
{
  "id": "quickstart_programmer",                // Mod ID
  "name": "My Code Mod",                        // Mod name shown in the mod list
  "description": "New action and mod settings", // Mod description shown in the mod list
  "sources": ["src/*.cs"],                      // Glob pattern for collecting source files (include all ".cs" files)
  "author": "programmer",                       // Author
  "depends": ["core"]                           // Dependency on the core game (so manual mod load ordering is not required)
}
```

The `sources` field specifies which C# files to compile via Roslyn.

---

## Step 3: Entry point — IMod

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

The `IMod` interface is the mandatory entry point for any C# mod. The `Initialize` method is called when the mod loads.

> Use `GameLogger.Log(...)` instead of `GD.Print(...)` if you want to work with logs.

---

## Step 4: Menu settings — IModSettings

Add to `MyModSettings.cs`:

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

After the mod loads, go to "Settings → Mod Settings" — your mod appears with a volume slider and a toggle.

---

## Step 5: Custom DSL action — IScenarioAction

Let's add a new `my_effect` action, usable in `.scenario` files. Create `MyEffectAction.cs`:

```csharp
using Cthangover.Core.Scenarios;
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

The engine finds your class via reflection and calls `Execute`.

---

## Step 6: Localization

`mods/quickstart_programmer/locale/en.properties`:

```properties
mymod/effect_volume = Effect volume
mymod/enable_mod = Enable mod
```

---

## Step 7: Check it

Start the game:

1. Open the settings menu → "Mod Settings" tab → your mod with a slider and checkbox.
2. Write a test `.scenario` with the `action my_effect` command.
3. The game logs show `[MY_MOD] New action executed!`.

---

## What's next?

| Want to | Go here |
|---|---|
| Subscribe to scene entry/exit | [Scene Subscriptions](site/docs/mods/configs/) |
| Create your own battle mode | [Battle System](site/docs/mods/battle/) |
| Make a tool window | [Creating a Custom Tool](site/docs/mods/tools/tutorial) |
| Write mods in GDScript | [GDScript in Mods](site/docs/mods/src/gdscript/) |
| All available interfaces | [C# Source Code in Mods](site/docs/mods/src/) |

---

## Download the finished example

<a href="examples/quickstart_programmer.zip" class="qs-download" download>
<svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><path d="M12 5v14"/><path d="M5 12l7 7 7-7"/><path d="M4 22h16"/></svg>
Download quickstart_programmer.zip
</a>

The downloadable mod is in English.
