# Sounds — Sound Effects

One-shot sound effect files (`.ogg` / `.wav`) organized by category, referenced from scenarios via the `sound` command.

## Directory structure

```filestree
sounds/
├── battle/           # combat sounds
│   ├── hit.ogg
│   ├── damage.ogg
│   └── stun.ogg
├── locations/        # ambient location sounds
│   ├── door_open.ogg
│   └── fire_crackle.ogg
├── ui/               # interface feedback
│   ├── click.ogg
│   ├── hover.ogg
│   └── notification.ogg
└── record_scratch.ogg
```

## File format

- `.ogg` (Vorbis) and `.wav` are both accepted — see `audio_extensions` in `config/mod_config.json`
- Short duration (0.1–5 seconds) recommended for one-shot effects
- The sound id is the path relative to the mod's `sounds/` folder, minus extension: `sounds/ui/click.ogg` → `ui/click`, `battle/hit`

## Real example — `core/sounds/`

From `mods/core/sounds/`:

```filestree
sounds/
├── record_scratch.ogg         # comedic timing effect
├── battle/
│   ├── physics_damage.ogg
│   ├── physics_defence.ogg
│   ├── death.ogg
│   └── stun_apply.ogg
├── locations/
│   ├── door_creak.ogg
│   └── wind_ambient.ogg
└── ui/
    ├── button_click.ogg
    ├── notification_chime.ogg
    └── quest_update.ogg
```

## Real example — `card_battle/sounds/`

From `mods/card_battle/sounds/`:

```filestree
sounds/
├── card_pick.ogg              # picking up a card
├── card_drop.ogg              # placing a card on target
├── card_damage.ogg            # damage number appears
├── card_stun.ogg              # stun effect applied
├── card_death.ogg             # card death dissolve
├── turn_start.ogg             # new turn
└── turn_end.ogg               # end turn button
```

## Usage in scenarios

```scenario
sound ui/click
text "- ..." key=dialogue_line
sound battle/hit
delay 0.5
sound ui/notification_chime
```

- `sound` plays immediately and does not block script execution
- Multiple sounds can play simultaneously
- The engine manages audio channels and prevents clipping

## Usage from C#

Sounds are played through `Cthangover.Core.Audio.IAudioService` (implemented by `AudioService`):

```csharp
var audio = GetNodeOrNull<AudioService>("/root/AudioService");

// Exact id
audio.PlaySound("ui/click", SoundType.UI);

// With random variations: picks "battle/hit_1" .. "battle/hit_3"
audio.PlaySound("battle/hit", 3, SoundType.Timed);
```

`SoundType` selects the playback pool — each type gets its own player, so a new
`CardEffect` sound cuts the previous one while a `UI` sound plays independently:
`Timed`, `Background`, `Foreground`, `UI`, `Notification`, `CardEffect`, `CardAction`.

### Engine internals

A few extra members on `AudioService` are useful for mod code:

- `IsMusicPlaying()` — returns `true` when the dedicated Music player is
  currently producing audio; lets UI display playback state without touching
  the player node directly
- `static LinearToDb(float linear)` — public helper converting a linear
  amplitude (0–1) to decibels via `20 * log10(linear)`, clamped to −80 dB at
  the bottom so zero never becomes `-infinity`
- `MixerType` enum (`Sounds`, `Musics`, `Ambient`) — identifies the three audio
  buses (SFX / Music / Ambient) used by `SetVolume(MixerType, float)` and
  `SetEnabled(MixerType, bool)` for per-bus volume and mute control

## Adding a new sound

1. Place the `.ogg` or `.wav` file in `sounds/{category}/`
2. Reference in scenario: `sound category/filename` (no extension)
3. No config or registration needed — audio files are discovered at load time

## Sound categories

Categories are organizational, not enforced. Use whatever folder structure fits your mod:

| Category | Typical use |
|---|---|
| `battle/` | Combat sounds: hits, blocks, stuns, death |
| `locations/` | Environmental: doors, water, wind, fire |
| `ui/` | Interface feedback: clicks, hovers, notifications |
| `music/` | Note: music tracks go in `music/`, not `sounds/` |

For music / ambient tracks, use `music/` and the `music`/`music_play`/`music_pause` scenario commands instead. See `musics/` documentation.
