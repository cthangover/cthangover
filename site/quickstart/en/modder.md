# Level 2 — Modder: DSL Scene

<div class="qs-meta">
<span>JSON + DSL</span>
<span>15 minutes</span>
<span>No code</span>
</div>

You'll create a mod with your own scene — a bar with dialogue, choices, and transitions. And you'll add an entrance to the bar to the existing town scene without touching the original files.

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
    │   └── town_entry_override.scenario  # Extend the town scene to add a transition to our new location
    ├── backgrounds/
    │   └── my_alko_bar/
    │       └── bg.png                    # Bar interior image
    └── sounds/
        └── locations/
            └── alkobar/
                └── bar_ambient.ogg       # Background bar noise sound
```

---

## Step 2: manifest.json

`mods/quickstart_modder/manifest.json`:

```jsonc
{
  "id": "quickstart_modder",                           // Mod ID
  "name": "My Booze Bar",                              // Mod name shown in the mod menu
  "description": "Adds a booze bar scene to the town", // Mod description shown in the mod menu
  "author": "modder",                                  // Mod author
  "depends": ["cthangover_game"]                     // Dependency on the main game's content (so manual mod load ordering is not required)
}
```

---

## Step 3: Scene definition

`mods/quickstart_modder/scenes/my_alko_bar.json`:

```jsonc
{
  "name": "my_alko_bar",                              // Create a new, unique scene identifier (used in switch_scene), this will be the new scene.
  "defaultBackground": "my_alko_bar/bg",              // Path to the PNG background inside backgrounds/ (without extension)
  "defaultAmbient": "locations/alkobar/bar_ambient",  // ID of the ambient sound for this location (path inside sounds/ without extension), e.g. "bar noise" that plays on entering the scene
  "defaultScenario": "scenarios/my_alko_bar.scenario" // Default scenario that will run in this scene if nothing else is specified
}
```

| Field | Description |
|---|---|
| `name` | Unique scene ID. Used in `switch_scene` |
| `defaultBackground` | Path to background inside `backgrounds/` (no extension) |
| `defaultAmbient` | Ambient sound: path inside `sounds/` without extension. Empty string — no sound |
| `defaultScenario` | Path to the `.scenario` file that runs on scene entry |

---

## Step 4: Background and sound

Place an image at `mods/quickstart_modder/backgrounds/my_alko_bar/bg.png`, and the ambient sound at `mods/quickstart_modder/sounds/locations/alkobar/bar_ambient.ogg`.

Any PNG image will work. The game supports shader-based lighting and time of day — the background will be automatically lit.

> If you don't have an image, temporarily copy `mods/cthangover_game/backgrounds/home/kitchen.png` → `mods/quickstart_modder/backgrounds/my_alko_bar/bg.png`. You can also skip the sound: set `"defaultAmbient": ""`.

---

## Step 5: DSL Scenario

`mods/quickstart_modder/scenarios/my_alko_bar.scenario`:

```scenario
scene: my_alko_bar
priority: 10
---
text "You walk into a half-empty bar."
text "A bored bartender stands behind the counter."

:menu
select "[What will you do?]"
option "Approach the counter" -> :bar
option "Leave" -> :leave

:bar
text "The bartender nods at you."
text "— What can I get you?"
goto -> :menu

:leave
switch_scene town_entry
end
```

### What's happening here

- **`scene:`** / **`priority:`** — the header: which scene the scenario belongs to and in what order it runs. The header is separated from the body by a `---` line.
- **`text`** — displays a line of dialogue. The `key=` parameter is a localization key (not needed here: without it the text itself is shown).
- **`select`** — opens a choice menu. The text in quotes is the prompt.
- **`option`** — a choice option. `-> :label` — where to jump when selected.
- **`:menu`**, **`:bar`**, **`:leave`** — labels for jumps.
- **`goto -> :menu`** — an unconditional jump: after talking to the bartender the menu opens again.
- **`switch_scene`** — moves the player to another scene.
- **`end`** — ends the scenario.

---

## Step 6: Extending the town (scene priority)

Now let's add an entrance to the bar to the town scene, **without touching** the original `cthangover_game` files.

`mods/quickstart_modder/scenarios/town_entry_override.scenario`:

```scenario
scene: town_entry
priority: 1
---
select "Music drifts out of the bar's half-open door..."
option "Go into the bar" -> :alko_bar
option "Walk past" -> :pass

:alko_bar
switch_scene my_alko_bar
end

:pass
end
```

### Why this works

The `town_entry` scene now has several scenarios:

| priority | Scenario | Mod |
|---|---|---|
| `1` | our `town_entry_override` | `quickstart_modder` |
| `10`–`30` | meeting Murakami (only while their `condition:` holds) | `cthangover_murakami` |
| `40` | the standard town menu | `cthangover_game` |

On entering a scene the engine runs the first eligible scenario in ascending `priority` order — starting from the **lowest**. When a scenario ends without changing the scene, the next one starts. So:

- "Go into the bar" — `switch_scene` takes the player to the bar;
- "Walk past" — our scenario simply ends (`end`), and everything continues as in the original game: Murakami's story scene or the standard town menu.

This way the mod adds something new without breaking anything. If our scenario drew the town menu itself, it would cover both the story and other mods.

> Learn more: [Scene Priority System](site/docs/mods/scenes/).

---

## Step 7: Connect the mod to the game

The game does not load every installed mod — only the mods that belong to the game's collection. Add your mod to it:

1. In the main menu, open **"Mods & Collections"** → the **"Collections"** tab.
2. On the **Cthangover** game card, click **"Edit scheme"**.
3. Select any mechanic on the scheme (for example, **"Story"**) and click **"Add mod"**.
4. Find your mod in the list (by the name from `manifest.json`) and click **"Add"**.
5. Click **"Save"** and confirm. Changes apply the next time the game starts.

---

## Step 8: Testing

Restart the game:

1. Go to the town — the first thing you see is the "Go into the bar" / "Walk past" choice.
2. Click "Go into the bar" — you're in your `my_alko_bar` scene with the background, bar noise, dialogue and choices.
3. Choose "Leave" — you return to the town.
4. Choose "Walk past" — the game continues as usual.

> **For developers.** From source you can open the scene right away, without playing through the prologue: `godot --path . -- --game=cthangover --new --with-mods=quickstart_modder --scene=town_entry`.

---

## What's next?

| Want to | Go to |
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

The downloadable mod's dialogue text is in Russian. Unpack the archive into `mods/quickstart_modder/` or put the `.zip` into `user://mods/`, then connect the mod to the game (step 7).
