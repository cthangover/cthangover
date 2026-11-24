# Display settings

`GameConfig` reads installation defaults from `config/game_config.json`. `SettingsData` loads the device profile from `user://settings.json`. If the profile is missing or malformed, the application uses defaults. Saving writes a complete temporary file before replacing the profile.

| Setting | Effect |
|---|---|
| `WindowMode`, `CurrentScreen`, `ResolutionWidth`, `ResolutionHeight` | Select window mode and monitor. Width and height apply to windowed mode |
| `VsyncEnabled`, `MaxFps` | Toggle vertical sync and set the frame cap. `MaxFps = 0` means unlimited |
| `RenderScale3D` | Override `Viewport.Scaling3DScale`. A missing or null override uses the project setting `rendering/scaling_3d/scale` |

`DisplaySettingsService.Apply` applies the full profile, including the 3D scale. The scale changes the internal 3D rendering resolution and can reduce GPU work on slower hardware. Godot applies it to the viewport's 3D rendering. Window size and 2D UI layout have their own settings.

## Inspect and change live settings

`settings.read` returns `effective` values from the current process and `profile` values from the user settings. `settings.apply` accepts `preset: "low_power"`, `values`, or both. `values` overrides the corresponding preset fields.

| Field in `values` | Valid input |
|---|---|
| `max_fps` | Integer from 0 through 1000 |
| `vsync_enabled` | Boolean |
| `render_scale_3d` | Number from 0.25 through 1.0 |

The `low_power` preset caps FPS at 30 and 3D scale at 0.75. An already lower limit is kept. By default the command changes live values. Send `persist: true` to also save them in the profile. The reply includes a `restore_id`; pass it to `settings.restore` to undo that change. V-Sync is unavailable in headless mode.

```json
{"command":"settings.apply","values":{"max_fps":30,"render_scale_3d":0.75}}
```
