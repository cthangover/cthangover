# Level 2 — Modder: DSL Scene

<div class="qs-meta">
<span>JSON + DSL</span>
<span>15 minutes</span>
<span>No code</span>
</div>

You'll create a mod with your own scene — a room with dialogue, choices, and transitions. And you'll add a "Go to the tavern" option to the existing kitchen scene without touching the original files.

---

## Step 1: Mod structure

Create the folder and base files:

```filestree
mods/
└── quickstart_modder/
    ├── manifest.json
    ├── scenes/
    │   └── my_alko_bar.json              # Our new location — the booze bar!
    ├── scenarios/
    │   ├── my_alko_bar.scenario          # Scenario inside the bar, let the player interact with the new location
    │   └── town_entry_override.scenario  # Override the town scene to add a transition to our new location
    ├── backgrounds/
    │   └── my_alko_bar/
    │       └── bg.png                    # Bar interior image
    ├── backgrounds/
    │   └── sounds/
    │       └── alko_bar_ambient.ogg      # Background bar noise sound
    └── locale/
        └── ru.properties                 # Localization for Russian
```

---

## Step 2: manifest.json

`mods/quickstart_modder/manifest.json`:

```jsonc
{
  "id": "alko_bar",                                    // Mod ID
  "name": "My Booze Bar",                              // Mod name shown in the mod menu
  "description": "Adds a booze bar scene to the town", // Mod description shown in the mod menu
  "author": "modder",                                  // Mod author
  "depends": ["core"]                                  // Dependency on the core game (so manual mod load ordering is not required)
}
```

---

## Step 3: Scene definition

`mods/quickstart_modder/scenes/my_alko_bar.json`:

```jsonc
{
  "name": "my_alko_bar",                              // Create a new, unique scene identifier (used in switch_scene), this will be the new scene.
  "defaultBackground": "my_alko_bar/bg",              // Path to the PNG background inside backgrounds/ (without extension)
  "defaultAmbient": "locations/alkobar/bar_ambient",  // ID of the ambient sound for this location (without extension), e.g. "bar noise" that plays on entering the scene
  "defaultScenario": "scenarios/my_alko_bar.scenario" // Default scenario that will run in this scene if nothing else is specified
}
```

| Field | Description |
|---|---|
| `name` | Unique scene ID. Used in `switch_scene` |
| `defaultBackground` | Path to background inside `backgrounds/` (no extension) |
| `defaultAmbient` | Ambient sound. Empty string — no sound |
| `defaultScenario` | Path to the `.scenario` file that runs on scene entry |

---

## Step 4: Background

Place an image at `mods/quickstart_modder/backgrounds/my_alko_bar/bg.png`.

Any PNG image will work. The game supports shader-based lighting and time of day — the background will be automatically lit.

> If you don't have an image, temporarily copy `mods/core/backgrounds/home/kitchen.png` → `mods/quickstart_modder/backgrounds/my_alko_bar/bg.png`.

---

## Step 5: DSL Scenario

`mods/quickstart_modder/scenarios/my_alko_bar.scenario`:

```scenario
scene: my_alko_bar
priority: 10
---
text "You walk into a half-empty bar."
text "A bored bartender stands behind the counter."

select "[What will you do?]"
option "Approach the counter" -> :bar
option "Leave" -> :leave

:bar
text "The bartender nods at you."
text "— What can I get you?"
end

:leave
switch_scene town_entry
end
```

### What's happening here

- **`text`** — displays a line of dialogue. The `key=` parameter is a localization key.
- **`select`** — opens a choice menu. The text in quotes is the prompt.
- **`option`** — a choice option. `-> :label` — where to jump when selected.
- **`:bar`**, **`:leave`** — labels for jumps.
- **`switch_scene`** — moves the player to another scene.
- **`end`** — ends the scenario (player stays in the scene).

---

## Step 6: Extending the town (scene priority)

Now let's add a "Go to the bar" option to the town scene, **without touching** the original `core` file.

`mods/quickstart_modder/scenarios/town_entry_override.scenario`:

```scenario
scene: town_entry
priority: 1
---
select "The town smells different today..."
option "Go back home" -> :home
option "Go to the bar" -> :alko_bar

:home
switch_scene home_outside
end

:alko_bar
switch_scene my_alko_bar
end
```

### Why this works

The `town_entry` scene now has two scenarios:
- `priority: 10` — default (from the `core` mod)
- `priority: 1` — ours (from `quickstart_modder`)

The engine runs the scenario with the **lowest** `priority`. Our `priority: 1` wins — the player sees the new option.

> Learn more: [Scene Priority System](site/docs/mods/scenes/).

---

## Step 7: Check it

Start the game:

1. Enter the town — you see the "Go to the bar" option.
2. Click it — you're in your `my_alko_bar` scene with dialogue and choices.
3. Choose "Leave" — you return to the town.

---

## What's next?

| Want to | Go here |
|---|---|
| Master all DSL commands | [DSL Command Reference](site/docs/mods/scenes/scenarios/dsl/) |
| Add interactive objects | [Interactive Objects](site/docs/mods/interactives/) |
| Write a C# mod | [Level 3 — Programmer](#quickstart/programmer) |

---

## Download the finished example

<a href="examples/quickstart_modder.zip" class="qs-download" download>
<svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><path d="M12 5v14"/><path d="M5 12l7 7 7-7"/><path d="M4 22h16"/></svg>
Download quickstart_modder.zip
</a>

The downloadable mod is in English.
