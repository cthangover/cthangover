# Playlists — JSON Reference

Detailed schema for music playlist configuration JSON files.

## Full schema

```jsonc
{
  "Scene": "scene_name",                // Scene name this playlist targets ("default" = fallback for any scene)
  "Musics": [
    {
      "MusicType": "Force | Combat | Ambient",  // Category: Ambient (explore), Combat (battle), Force (transient)
      "MusicNames": ["track1", "track2", "..."]   // Track filenames without .ogg extension
    }
  ]
}
```

These are the only supported fields. There is no shuffle flag — a random track
is picked each time playback starts or advances. Fading is hard-coded in the
engine (~1s delay, then a volume ramp) and cannot be configured per playlist.

## Field details

| Field | Type | Description |
|---|---|---|
| `Scene` | string | Scene name this playlist applies to (`"default"` is used when no playlist matches the loaded scene) |
| `MusicType` | string | Category: `Ambient` (exploration), `Combat` (battle), `Force` (transient, normalised to `Ambient`). A scene named `Battle` switches playback to `Combat`, otherwise `Ambient` plays |
| `MusicNames` | string[] | Track filenames without extension. Must match `.ogg` files in `music/` |

## Real example — default playlist

```jsonc
{
  "Scene": "default",               // Scene name; "default" is the fallback playlist
  "Musics": [
    {
      "MusicType": "Ambient",       // Exploration music
      "MusicNames": [
        "Abyssal Void", "Airborne Melody", "Binary Prelude",
        "Cinders of Dawn", "Melancholic Journey",
        "Nebula Pulse Echoes", "Quantum Archive", "Twilight Interlude"
      ]
    },
    {
      "MusicType": "Combat",        // Battle music — auto-switched by engine
      "MusicNames": [
        "Aura of Decay", "Circuit Dreams II", "Crimson Echoes",
        "Crimson Horizon", "Glass Symphony", "Hex Doom Cart",
        "Iron Resonance", "Iron Resonance II", "Luminous Twilight",
        "Rusty Frequencies", "Skull Resonance", "Venom's Embrace"
      ]
    }
  ]
}
```

## Multiple playlists example

A single playlist can cover different states by defining multiple `MusicType` buckets:

```jsonc
// music/playlists/tavern.json — applies to the scene named "tavern"
{
  "Scene": "tavern",
  "Musics": [
    { "MusicType": "Ambient", "MusicNames": ["Tavern_Day", "Tavern_Night"] },
    { "MusicType": "Combat", "MusicNames": ["Tavern_Fight_01", "Tavern_Fight_02"] }
  ]
}
```

## Scene integration

Playlist selection happens per scene, automatically. When a scene loads,
`PlaylistFactory.CreatePlaylist(sceneName)` uses the playlist whose `Scene`
matches the scene name (falling back to `"default"`):

```jsonc
{
  "name": "town_tavern"    // Scene ID — playlists with "Scene": "town_tavern" apply here
}
```

Note: `defaultAmbient` in a scene JSON is unrelated to playlists — it plays an
ambient sound loop via the sound system (`AudioService.PlayAmbient`), not music.

The scenario command `music <id>` also does not switch playlists; it plays a
single track by id.

## How the engine switches playlists

1. Scene loads → `PlaylistFactory.CreatePlaylist(sceneName)` picks the playlist whose `Scene` matches (fallback `"default"`) → starts playing `Ambient` tracks
2. A scene named `Battle` switches `MusicType` to `Combat` → tracks come from that playlist's `Combat` bucket; on returning to a non-battle scene, playback resumes with `Ambient`
3. Scene changes → the new scene's playlist takes over, with the hard-coded fade
4. Multi-mod note: if several mods define playlists for the same scene, the last loaded mod replaces each `MusicType` bucket entirely (last one wins)

Playback always picks a random track from the active category bucket — there is
no ordered or shuffle-flagged mode.

Music types: `Force`, `Combat`, `Ambient`. Mixer buses (`MixerType`): `Sounds`,
`Musics`, `Ambient` — music plays through the dedicated `Musics` bus.
