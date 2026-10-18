# Music & Playlists

Background music tracks (`.ogg`) organized into playlists with ambient/combat categorization. Playlist selection happens per scene: the engine picks the playlist whose `Scene` matches the current scene name and switches between its categories based on game state (exploration vs battle).

## Directory structure

```filestree
music/
├── track_name_1.ogg
├── track_name_2.ogg
└── playlists/
    ├── default.json
    ├── battle.json
    └── town_entry.json
```

## File format

- `.ogg` — Vorbis-compressed audio, any bitrate
- Files are referenced by name (without extension) in playlists

## Real example — `cthangover_game/music/playlists/default.json`

```jsonc
{
  "Scene": "default",               // Scene name this playlist belongs to ("default" is the fallback)
  "Musics": [
    {
      "MusicType": "Ambient",       // Category: music for exploration/non-combat states
      "MusicNames": [               // Track filenames without .ogg extension
        "Abyssal Void",
        "Airborne Melody",
        "Binary Prelude",
        "Cinders of Dawn",
        "Melancholic Journey",
        "Nebula Pulse Echoes",
        "Quantum Archive",
        "Twilight Interlude"
      ]
    },
    {
      "MusicType": "Combat",        // Category: music switched to during battles
      "MusicNames": [
        "Aura of Decay",
        "Circuit Dreams II",
        "Crimson Echoes",
        "Crimson Horizon",
        "Glass Symphony",
        "Hex Doom Cart",
        "Iron Resonance",
        "Iron Resonance II",
        "Luminous Twilight",
        "Rusty Frequencies",
        "Skull Resonance",
        "Venom's Embrace",
        "Empty Save Room"
      ]
    }
  ]
}
```

8 ambient tracks for exploration, 13 combat tracks for battles — 21 total.

## Playlist JSON schema

```jsonc
{
  "Scene": "scene_name",              // Scene name this playlist targets ("default" = fallback for any scene)
  "Musics": [
    {
      "MusicType": "Force | Combat | Ambient",  // Category: Ambient (explore), Combat (battle), Force (transient)
      "MusicNames": ["track_1", "track_2", "..."]   // Track filenames without .ogg extension
    }
  ]
}
```

| Field | Required | Description |
|---|---|---|
| `Scene` | Yes | Scene name the playlist applies to (`"default"` is used when no playlist matches the scene) |
| `MusicType` | Yes | Category: `Ambient`, `Combat`, `Force`. `Force` is transient — normalised to `Ambient` |
| `MusicNames` | Yes | List of track filenames (no extension, no path) |

These are the only supported fields. There is no shuffle flag — a random track
is picked each time playback starts/advances. Fading is hard-coded in the
engine (~1s delay, then a volume ramp) and cannot be configured per playlist.

## Usage in scenarios

The `music`, `music_play`, and `music_pause` commands control music playback:

```scenario
# Play a single track by id (resolved via MusicFactory, as Ambient type)
music tavern_theme

text "A bard strums a melancholy tune..."

# Pause everything
music_pause

# Resume
music_play
```

Note that `music <id>` plays one track — it does not switch playlists.
Playlist selection is driven by scenes, not by scenario commands.

## Scene default ambient

Scene definitions can specify a `defaultAmbient` — but this is an **ambient
sound loop**, not a playlist. It is played via `AudioService.PlayAmbient(id)`
through the sound system (`SoundFactory`, group `"sounds"`), separate from the
music playlist system:

```jsonc
{
  "name": "town_entry",                                 // Scene ID
  "defaultBackground": "town/entry",                    // Background image path
  "defaultAmbient": "locations/wind",                   // Ambient sound id from sounds/
  "defaultScenario": "scenarios/town/town_entry_default.scenario"  // Default scenario on enter
}
```

Music playlists are matched to scenes automatically by scene name (see below),
not via `defaultAmbient`.

## Adding a new track

1. Place the `.ogg` file in `music/`
2. Add its name (without `.ogg`) to the desired playlist's `MusicNames` array
3. Restart the game — tracks are loaded at startup

## How playlists are selected

- `PlaylistFactory.CreatePlaylist(sceneName)` collects every `music/playlists/*.json`
  across all mods and uses those whose `Scene` matches the current scene name;
  if none match, it falls back to `Scene: "default"`
- When several mods define the same `MusicType` for a matched scene, the last
  loaded mod **replaces** that entire category's track list (last one wins)
- The music type is chosen per scene through the scene-route registry
  (`SceneRouteRegistry`): a mod registers a logical scene name together with a
  `MusicType` — `turn_battle_base` registers `Battle` → `Combat` — and any
  scene without a registered route plays `Ambient` tracks

## `MusicPlayerBehaviour` public API

`Cthangover.Core.Audio.MusicPlayerBehaviour` (`Core/Audio/MusicPlayerBehaviour.cs`)
is the node that drives playlist auto-advance and scene-aware switching. Its
public surface:

| Member | Behaviour |
|---|---|
| `IsCanAutoPlay` | Top-level toggle for the auto-advance state machine; when `false` no tracks are picked and scene switches don't trigger playback |
| `EnabledAutoPlay()` | Re-enables auto-play and resumes the last saved track |
| `DisabledAutoPlay()` | Saves the current playback position, disables auto-play, stops playback |
| `UpdateMusicType(scene)` | Scene-transition entry point (`GodotSceneType` or string overload); orchestrates the Combat ↔ Ambient handoff |
| `PlayMusic(name[, stream][, isLooped])` | Plays a track by name (stream resolved via `MusicFactory`) or with an explicit `AudioStream`; the `isLooped` parameter is accepted but currently unused |
| `PauseMusic()` / `StopMusic()` | Pause in-place via `StreamPaused` vs. full stop (stream reference kept for restart) |
| `FadeMusic()` | Hard-coded fade-out: 1s delay, then a 6-second volume ramp to silence, then stop and reset bus volume |
| `NextSound()` | Picks a random track from the current playlist's category bucket, avoiding an immediate repeat when more than one track exists |

When combat ends, the ambient track that was interrupted is resumed from its
saved position — if that position is greater than 0.5s,
`OggPacketParser.CreateTrimmedStream` builds a truncated OGG stream starting at
the saved time instead of seeking.

## Creating a new playlist

1. Create `music/playlists/my_scene.json`
2. Set `Scene` to the exact scene name the playlist targets
3. Define `MusicType` and `MusicNames` entries — the engine picks the playlist up automatically when that scene loads
