# DSL Command Reference

Complete reference to the Scenario DSL commands: 29 keywords registered in the command strategy registry, plus `select`/`option`, which are handled specially by the parser (31 commands total, grouped by category).

---

## Flow Control

### `select`
Opens a choice menu. String is the prompt text (can be a locale key or literal).

```scenario
select "What now?" key=choice/prompt
option "Go home" key=choice/home -> :home
option "Explore" key=choice/explore -> :explore
```

- Must be followed by one or more `option` commands
- The `key=` parameter references a locale key for localization
- If no `key=`, the text itself is displayed
- Supports the same `first=`/`second=` avatar parameters as `text`

A `select` with no following `option` commands is silently dropped from the queue — no error is raised.

### `option`
Defines a choice within a `select` block. Three ways to specify the jump target:

```scenario
# Arrow form
option "Leave" key=rooms/kitchen/opt_exit -> :exit

# Named parameter form
option "Leave" key=rooms/kitchen/opt_exit goto=exit

# Bare positional label form
option "Leave" :exit
```

- Text can be a literal or a `key=` locale reference
- All three target forms are equivalent; the `->` arrow takes priority when several are present
- An `option` outside a `select` block is ignored

### `:label`
Anchor point for jumps. Must start with `:` at the beginning of a line.

```scenario
:enter_room
background "town/meetingmurakami/00079"
text "[Marao stepped inside]" key=enter/room
```

Labels are local to the scenario file. They cannot be jumped to from other scenarios.

### `goto`
Unconditional jump to a label.

```scenario
goto -> :cleanup
```

or equivalently:

```scenario
goto target=cleanup
```

> **Note:** The bare form `goto :cleanup` is **not** supported — it is silently ignored by the parser. Always use the `->` arrow or the `target=` parameter.

Useful for skipping optional content or implementing complex branching without nested options.

### `end`
Terminates the scenario script. Required at the end of every execution path.

```scenario
end
```

---

## Scene & Visuals

### `background`
Sets or changes the background image.

```scenario
background "town/meetingmurakami/00002-1920617752"
```

Path is relative to the mod's `backgrounds/` folder. No `.png` extension.

### `background_color`
Sets a solid color background (overrides image).

```scenario
background_color "#1a1a2e"
```

Accepts an HTML hex color (`#1a1a2e`) or a named color (`black`, `white`, `red`, `green`, `blue`, `yellow`, `gray`/`grey`). Anything else falls back to black. Useful for transitions, flashbacks, or UI overlays.

### `background_show_hide`
Animates the background layer in or out with a fade.

```scenario
background_show_hide hide
background_show_hide show duration=0.5 wait=true
```

| Parameter | Description |
|---|---|
| (positional) | `show` or `hide` (defaults to `show`; any other value is treated as `hide`) |
| `duration=` | Fade duration in seconds (default `1`) |
| `wait=` | `wait=true` pauses the script until the fade animation completes |

### `switch_scene`
Transitions to a different scene.

```scenario
switch_scene home_vestibule
switch_scene Battle
```

The target is a scene name defined in `scenes/{name}.json`.

| Parameter | Description |
|---|---|
| (positional) | Scene name to switch to |
| `speed=` | Transition speed as a float (default `4`) |
| `hidden=` | `hidden=true` transitions showing only the scene background |

The names `mainmenu` and `menu` are built-in: they are routed to the engine's native Godot scenes instead of mod-defined scenes. Any other name may be a scenario scene or a scene route registered by a mod through `SceneRouteRegistry` — for example `battle` is registered by `turn_battle_base` (fade transition plus combat music), so the core does not hardcode it.

### `foreground`
Sets or clears the foreground layer image (rendered above characters).

```scenario
foreground "effects/rain"
foreground ""
```

### `effect`
Plays a visual effect by ID. The syntax is `effect <effect_id>`:

```scenario
effect rain_overlay
```

The ID references a Godot scene (`.tscn`/`.scn`) from the mod's `effects/` resource group, resolved via the effect factory and instantiated above the scene. The script continues immediately — the effect plays independently and persists until its own lifetime ends.

> **Note:** Effect scenes are only loadable from **folder mods** — zip mods skip them with a warning, because `PackedScene` cannot be reconstructed from raw bytes in managed code.

---

## Text & Dialogue

### `text`
Displays a dialogue line with optional avatar portraits.

```scenario
text "- Dear lady!" key=meeting/greeting first=marao/think second=murakami/fear_1
```

| Parameter | Description |
|---|---|
| `key=` | Locale key for the text content |
| `first=` | Left-side avatar: `{character}/{expression}` |
| `second=` | Right-side avatar |
| `hide_color=` | `hide_color=true` shows avatars in shader-based silhouette mode (color suppressed) — useful for flashbacks or unknown speakers |
| `wait=` | `wait=false` advances the script immediately without waiting for a click |
| (inline) | If no `key=`, the text is displayed literally |

### `ptext`
Like `text`, but enables runtime `${var}` variable substitution in the text content.

```scenario
set player_name Marao
ptext "Well met, ${player_name}!"
```

The dialogue box is still shown, and `first=`/`second=` avatar parameters work the same as in `text`. Use `ptext` whenever you need to interpolate variables into a line; plain `text` does not substitute variables. Variables that are not set resolve to `?`.

| Parameter | Description |
|---|---|
| `key=` | Locale key for the text content (substitution still applies after lookup) |
| `first=` | Left-side avatar: `{character}/{expression}` |
| `second=` | Right-side avatar |
| `hide_color=` | `hide_color=true` shows avatars in shader-based silhouette mode |
| `wait=` | `wait=false` advances the script immediately without waiting for a click |
| (inline) | If no `key=`, the text is displayed literally (with `${var}` substitution) |

### `title`
Displays a large centered title, often used for chapter/section headers.

```scenario
title "Chapter 1 — The Hangover"
```

The text can be a literal or a `key=` locale reference; lines without resolvable text are silently skipped.

---

## Audio

### `music`
Switches the background music playlist.

```scenario
music playlists/tavern
music playlists/battle
```

Target is a playlist file in `music/playlists/`.

### `music_play`
Resumes paused music.

```scenario
music_play
```

### `music_pause`
Pauses the current music track.

```scenario
music_pause
```

### `sound`
Plays a one-shot sound effect.

```scenario
sound ui/click
sound battle/hit
```

Path is relative to the mod's `sounds/` folder. No `.ogg` extension.

---

## Lighting

### `light_set`
Configures dynamic 2D light sources. Accepts a JSON array of light objects.

```scenario
light_set [{"x":0.43,"y":0.58,"radius":500,"influence":1,"color":"#ffff00"}]
```

| Field | Description |
|---|---|
| `x`, `y` | Normalized position (0.0–1.0, relative to the viewport) |
| `radius` | Light radius in pixels |
| `influence` | Intensity multiplier (0.0–1.0) |
| `color` | Hex color (`#ffff00`) |

Multiple lights in one array:

```scenario
light_set [
  {"x":0.84,"y":0.04,"radius":600,"influence":1,"color":"#ffff00"},
  {"x":0.29,"y":0.16,"radius":600,"influence":1,"color":"#ffff00"}
]
```

At most 10 static lights are applied (plus the player's lamp); extra array entries are ignored.

Call with no arguments to reset all lights:

```scenario
light_set
```

### `light_use_time`
Enables/disables time-of-day influence on light color and angle.

```scenario
light_use_time true
light_use_time false
```

When enabled, the shader system adjusts warm/cool tones based on in-game time.

---

### `delay`
Pauses script execution for N seconds.

```scenario
delay 2.5
text "..."
delay 1.0
text "Oh."
```

Extended forms:

```scenario
# Invisible pause — no dialogue box activity
delay 1.5 hidden

# Pause with a caption shown during the wait
delay 3.0 "Meanwhile, across town..."

# Same, but the caption is resolved from a locale key
delay 3.0 "..." key=scene/meanwhile_caption
```

- The duration defaults to `1` second if omitted
- `hidden` can also be given as a named parameter: `delay 1.5 hidden=true`
- `key=` only applies when a text to display was given

### `animation`
Plays a sprite animation. Supports two forms:

Single sprite base with frame controls:

```scenario
animation marao/walk start=0 count=6 speed=8.0 loop=true
```

Multiple explicit sprites:

```scenario
animation sprites=town/rain_1,town/rain_2,town/rain_3 speed=4.0 loop=true
```

| Parameter | Description |
|---|---|
| (positional) | Sprite base path (first form only) |
| `sprites=` | Comma-separated sprite list (alternative to positional base) |
| `start=` | First frame index (default `0`, first form only) |
| `count=` | Number of frames to play (default `1`) |
| `speed=` | Playback speed as a float multiplier (default `1.0`) |
| `loop=` | `true`/`false` — loop the animation (defaults to `true` when omitted) |

There is no `duration=` parameter — control timing via `speed=` and `count=`.

---

## Interactive Objects

### `interactive_add`
Adds a clickable interactive object to the scene.

```scenario
interactive_add kitchen_lamp
```

The name references an interactive definition in `interactives/{name}.json`.

### `interactive_remove`
Removes a specific interactive object.

```scenario
interactive_remove kitchen_lamp
```

### `interactive_clear`
Removes all interactive objects from the current scene.

```scenario
interactive_clear
```

### `interactive_set`
Enables, disables, shows, or hides a specific interactive object.

```scenario
interactive_set kitchen_lamp enabled=false
interactive_set cabinet_door visible=true
```

Supported named parameters: `enabled=true|false`, `visible=true|false`.

---

## Dialog Visibility

### `show_dialog`
Shows the dialogue box UI (if hidden).

```scenario
show_dialog
```

### `hide_dialog`
Hides the dialogue box UI without clearing text.

```scenario
hide_dialog
```

Used for cutscenes where you want the background visible without UI.

---

## Variables & Logic

### `set`
Stores a variable in the dialog runtime's variable store for later use — readable via `${var}` substitution in `ptext` lines and via `ctx.GetParam(...)` in actions.

```scenario
set met_murakami true
set door_locked false
```

The syntax is **space-separated**: first token is the name, second token is the value. Do not write `set name = value` with spaced equals — the parser stores the literal `"="` as the value. A `name=value` token (no spaces around `=`) is treated as a named argument, which is a different mechanism and not how `set` reads its operands.

`set` variables live only while this dialog runs. For state that scenario conditions read, use `flag`.

### `flag`
Sets a **story flag** of the game. Unlike `set`, a flag is game state: it is saved, a new game starts
without it, and scenario `condition:` headers read it. Setting a flag rescans the scene's events, so a
scenario waiting for it can start as soon as this dialog ends.

```scenario
flag door open
flag visits 3
```

```scenario
scene: home_kitchen
condition: door == "open" && visits >= 3
---
text "The door stands open now."
```

Same space-separated syntax as `set`. Numeric values compare as integers in conditions.

### `empty`
A no-op command — it queues a placeholder action that does nothing and is skipped instantly.

```scenario
empty
```

Labels (`:label`) are implemented as named `empty` markers, so a bare `empty` line is also a convenient placeholder slot during development.

---

## Notifications

### `notify`
Queues a notification banner in the top-left corner of the screen. Notifications appear one by one, stack vertically, and auto-dismiss after 10 seconds. Use for quest updates, item pickups, or any non-blocking informational messages.

```scenario
notify "Quest updated: Find the key"
```

```scenario
notify "Received: Health Potion"
```

| Parameter | Description |
|---|---|
| (inline) | The notification text to display |
| `key=` | Locale key for localization (takes priority over inline text) |

Notifications are queued in a buffer — they appear in order and will wait if the screen already has the maximum number of visible notifications (4). The 10-second timer starts only when the notification becomes visible, not when it is queued.

---

## Special Syntax

### `#` — Comments

```scenario
# This line is ignored by the parser
# Use comments to document complex branching
```

### `---` — Metadata Separator

Separates the YAML-like metadata header from the script body. Must appear on its own line.

### `->` — Jump Operator

Used in `option` and `goto` commands to point to `:label` targets.

```scenario
option "Go inside" -> :enter
```

### `${path}` — Include Directive

Inside `.scenario` scripts and other mod text files, `${ path/to/file }` is replaced with the full content of the referenced file at **mod resolution time** — before the script is ever parsed. This happens while the mod loader reads the file (the same mechanism used by `ScenarioResolver` and the other text-based resolvers).

```scenario
# dialogue/intro.scenario
${ events/common_ambience.txt }
background "town/meetingmurakami/00079"
```

- Includes are resolved relative to the mod root and may be nested (an included file can contain includes of its own)
- If the path does not resolve and `path + ".json"` exists, that file is used instead
- Circular includes are detected and reported as errors; resolution fails for the affected file
- Whitespace inside the braces is allowed (`${ path }`)

Do not confuse this with the runtime `${var}` substitution in dialogue text (see `ptext`): includes are expanded once when the mod loads, variables are substituted every time the line is shown.
