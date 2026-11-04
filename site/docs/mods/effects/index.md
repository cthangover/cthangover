# Effects and atmosphere profiles

`effects_base` supplies scene effects and continuous atmosphere. A scenario's `effect <id>` loads an effect scene from `effects/`. Atmosphere uses a separate `atmosphere` entity with emitter data; it can cover a map or a painted subset of cells and is not a scenario command.

## A profile

```json
{
  "Kind": "atmosphere",
  "Items": [{
    "Id": "my_game/ash", "Name": "my_game/ash/name",
    "Emitters": [{
      "Type": "particles", "Shape": "flake", "Count": 80,
      "Color": "#a0a0a070", "Velocity": [0.1, 0.02, -0.4],
      "Height": [0.1, 5], "Size": [0.5, 1.2], "Sway": 0.2, "Seed": 17
    }]
  }]
}
```

Put profiles in `atmospheres/`. A map's `Atmosphere` selects a whole-map profile. Its `Effects` list adds profiles, optionally with `Cells` masks. RPG Map Editor's **Effects** tool paints and erases masks; Effects Editor edits profile parameters and opens the map editor with its profile selected. Removing the last cell removes that mask placement. Masks are implemented in model v2.

`particles` supports `flake`, `leaf`, `glow`, `dust`, `streak`. Velocity is XYZ world units per second, positive Z up; Height is a world-unit range and Size a pixel-radius range. `Pulse` fades periodically and `Trail` controls streak duration. Counts are capped at 512 per emitter and 2048 per profile. `ripples` draws projected rings in a named `Region`; the tile host supplies `water`. A missing region draws nothing. Cosmetic randomness is separate from gameplay randomness.

## Extending and hosting

Implement `IAtmosphereEmitterFactory` in a mod depending on `effects_base`. Its `Id` selects JSON `Type`; `Create` receives `AtmosphereEmitterInfo`, including custom `ExtraFields`, and returns an `IAtmosphereEmitter`. `Advance` updates once per host frame; `Draw` draws through the context projection without advancing; `Dispose` releases owned assets. Factories are discovered and removed through the mod pipeline. Duplicate types, unknown types and invalid ranges are diagnosed.

Outside an RPG map, host an `AtmosphereView`, call `Configure(profileId, context)` with bounds and a `Func<Vector3, Vector2>` projection, then `Advance(delta)` each frame. Call `Release()` before manual freeing; tree exit also releases resources. There is no RPG dependency in the atmosphere engine. Scene exit, renderer replacement and profile changes release owned emitters; compatible geometry edits retain animation state.

Implementation: `mods/effects_base/src/Atmosphere/`, `mods/effects_editor/`, the atmosphere schema and `mods/rpg/src/Model/RpgMap.cs`.
