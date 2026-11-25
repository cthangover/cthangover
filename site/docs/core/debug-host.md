# DebugHost

`DebugHost` is the kernel's runtime command dispatcher. Boot installs it after initializing the game. A transport can submit a JSON request through `DebugHost.ExecuteAsync`, and the host dispatches commands sequentially on the Godot thread, including while the tree is paused. The separate MCP adapter uses the same dispatcher.

## Connect to the kernel pipe

Start Godot with `--development-control` after `--`, or set `development_control` in `config/mod_config.json`, to enable the legacy same-user pipe. `--no-development-control` takes precedence and blocks the external debug endpoints for that launch. The pipe is specific to the process. For a discovery document, set `development_discovery` to a file path such as `user://dev_host.json`. The kernel can be queried with `DebugClient/dev_control.py` from the source tree.

```shell
godot --path . -- --development-control
# From a second terminal on the same system under the same user, replace 12345 with the game PID.
python DebugClient/dev_control.py 12345 '{"command":"args.catalog","key":"game"}'
```

## Kernel commands

| Commands | Use |
|---|---|
| `status`, `commands`, `describe` | Get process status or discover registered commands and their descriptions |
| `diagnose`, `errors.pull` | Read boot and compiler diagnostics. Use `next` from `errors.pull` as the next `since` cursor |
| `inspect` | Inspect live nodes, resources, caches, or registered game data. Use `describe` for current selectors |
| `node.inspect`, `node.set`, `node.tween`, `node.sine`, `node.motion.stop` | Discover, set, or animate properties of a live node |
| `args.read`, `args.catalog`, `args.set` | Read or change the [effective launch arguments](launch-arguments.md) |
| `memory.stats`, `memory.inspect` | Read only [registered memory metrics and previews](memory.md) |
| `settings.read`, `settings.apply`, `settings.restore` | Inspect or change [display settings](display-settings.md) |
| `scene.switch`, `assets.refresh`, `mod.attach`, `mod.reload` | Request a logical scene switch, asset refresh, or development reload |
| `operation.status` | Query a tracked operation by `operation_id` |

For a mutation, send an `operation_id` UUID. If the connection closes, query `operation.status` with the same UUID before retrying. The host retains a bounded history for the current process.

### Live scene controls

Find a node path with `inspect` using `area=node` and a `query` or `type` filter. Use `node.inspect` with that `path` and `list_properties=true` to discover Godot property names and types. All motion commands take a `path` and a `property`. Pass vectors in `[x, y, z]` order for 3D.

| Command | Example fields |
|---|---|
| `node.set` | `property="scale"`, `value=[1.5,1.5,1.5]` |
| `node.tween` | `property="rotation_degrees"`, `value=[-15,40,0]`, `duration_s=6` |
| `node.sine` | `property="position"`, `amplitude=[0,0.2,0]`, `period_s=4` |
| `node.motion.stop` | `property="position"` |

A new motion on the same property replaces the previous one. Sine motion uses the property's current value as its midpoint. Stopping holds the current value. The scene owns each motion's lifetime. After scheduling a tween, use `node.inspect` to verify the final value. For a node whose own controller writes the same property each frame, use a camera or node specifically prepared for manual control.

In a 3D location with development control enabled, **F7** takes over the active view with a free camera. Use the mouse to look, **WASD** to fly, **Space/Ctrl** to rise or descend and **Shift** for faster travel. Hold **Alt** to release the pointer. Press **F7** again or **Esc** to return to the previous camera.

## Repeat tests in one game process

Keep one windowed Godot process running while you change settings, reset the scenario state, and measure successive variants through DebugHost. The process, kernel, and mods start once for the series. Allow time to rebuild and warm up each scene. Use separate processes when the metric is cold startup itself.

1. Start the appropriate game or focused TestRunner case. From `/workspace`, run Windows work through a `hostserver` action as described in `/workspace/hostserver/README.md`. The bridge runs the Windows wrapper, and DebugHost dispatches the game's commands. Use the child Godot PID from the launch or `debug host ready` log line. TestRunner has its own `dotnet` PID.
2. Wait for `status` and confirm the `pid`, `instance`, game and scene. Discover available commands with `commands` or `describe`. The pipe opens during startup. `dev_control.py --connect-timeout 45` waits up to 45 seconds for a connection before sending the request. `command not sent` means connection failed. If the reply to a mutation is lost, query `operation.status` before retrying.
3. Restore a known state before each case. Keep the seed, route, participants, graphics, warmup and sample window constant. Repeat the baseline last to check for warmup drift. Give each case its own output directory and record its PID, scene instance and checks.
4. Have long-running test commands return an acceptance receipt and yield to the main loop. Use separate short Windows actions to start and inspect a long scenario so the bridge can accept diagnostics. The receipt covers scheduling. Verify completion from the case artifacts and final result.

### Sample current frame performance

Start a game with `perf_overlay` active, for example with the user argument `--with-mods=perf_overlay`. In a PowerShell session on the same system and user as the game, substitute its PID.

```powershell
$gamePid = 12345
py -3 -B DebugClient/dev_control.py --pid $gamePid --connect-timeout 45 status
$start = @{ provider = 'perf_overlay'; command = 'perf.configure'; enabled = $true; overlay = $false; reset = $true; operation_id = [guid]::NewGuid().ToString() }
$start | ConvertTo-Json -Compress | py -3 -B DebugClient/dev_control.py $gamePid '-'
# Run the same route for the same interval before reading each variant.
'{"provider":"perf_overlay","command":"perf.read"}' | py -3 -B DebugClient/dev_control.py $gamePid '-'
```

To test a temporary display change, read `settings.read`, send `settings.apply`, and save its `result.restore_id`. Open a fresh `perf.configure` window with `reset=true`, measure, and restore the previous values with `settings.restore`. Keep `persist` disabled for a temporary experiment.

The CLI timeout applies to connecting. The Windows PowerShell example in the Russian guide uses a separate reply deadline with `NamedPipeClientStream`. A reply timeout leaves the mutation outcome unknown. Keep the original UUID and check `operation.status` if the connection returns. [Russian guide](debug-host.ru.md)

### Ognishchanin battle and window checks

The collision experiment replaced a Godot restart per variant with one windowed `ognishchanin_battle_field` TestRunner launch. The launch used an isolated, verified test context, `--itest=ognishchanin_battle_profile`, inherited `CTHANGOVER_BATTLE_DEBUG_MATRIX=1`, and `--development-control` in Godot's user arguments. A temporary runner copy forwarded the game flag and kept a stable prepared-input cache. We used native `--fixed-fps 60` before `--` for fixed-step comparison and omitted it for the separate window check.

```powershell
$provider = 'ognishchanin_game__battle_profile'
py -3 -B DebugClient/dev_control.py --pid $gamePid commands --mod $provider
'{"provider":"ognishchanin_game__battle_profile","command":"battle.status"}' |
    py -3 -B DebugClient/dev_control.py $gamePid '-'
$operation = [guid]::NewGuid().ToString()
@{
    provider = $provider
    command = 'battle.profile'
    modes = @('baseline', 'filter', 'support', 'late', 'filtered-support', 'combined', 'baseline')
    operation_id = $operation
} | ConvertTo-Json -Compress | py -3 -B DebugClient/dev_control.py $gamePid '-'
```

The first connection attempt reached the host before the pipe opened. We verified the same Godot PID and reconnected to `cthangover-dev-28608` with Windows `NamedPipeClientStream` using newline-delimited UTF-8 JSON. `result.scheduled = 7` confirmed acceptance. The driver uses `GameSession.StartNew` and verifies a new combat state for each case. It then waits for a replacement `Scene3dHost` and the four original fighters before staging the same fight with 50 opponents. Compare `process_id` and `scene_host_id` in the per-case `report.json` files and read `frames.json` and `matrix.json`. The first run demonstrated process reuse. Opening-strike failures and uncertainty about the visible window made that run unsuitable for comparing collision performance.

For window problems, take two `battle.status` snapshots 2–3 seconds apart in the same process. Compare `world_ready`, `world_progress`, `graphics_apply`, input and pause state, and the changes in `process_frames`, `drawn_frames`, `physics_frames`, and `combat_tick`. Check the process and HWND independently in Windows, including the desktop session, visibility, minimized state, monitor intersection and foreground state. If the debug channel stops replying, inspect the child PID and native log. The dispatcher itself runs on the game thread.

In the separate run without combat, the window was responsive and in the background. World preparation advanced from 3% to 100% in about 50 seconds. After that, three seconds produced 309 drawn frames and 185 physics ticks. The original disappearance was not reproduced. Godot counters show engine activity. For frames actually displayed by Windows, use an independent PID-scoped [PresentMon](https://github.com/GameTechDev/PresentMon) trace.

## Repeated location preparation after editing textures

In the terrain review on 25 November 2026, the location repeatedly returned to its loading screen while new flower PNGs were being saved. The live error feed did not report a preparation failure. The native log instead showed `MODS_RELOAD` naming each changed PNG, followed by a checkpoint restore and another `SceneEnteredEvent`. The legacy file watcher treated every relevant file change as permission to reload the mod set, including a texture-only change with no assembly changes.

Read both the error channel and reload events. A quiet error feed does not establish that the scene instance remained unchanged. Kernel `status.phase = Ready` describes the command host; inspect the owning scene's readiness as well.

```powershell
py -3 -B DebugClient/dev_control.py --pid $gamePid errors.pull --since 0 --level error --max 40
# Temporary containment for a legacy automatic watcher, without restarting the game:
$pauseWatcher = @{
    command = 'node.set'
    path = '/root/ModHotReload'
    property = 'process_mode'
    value = 4
    operation_id = [guid]::NewGuid().ToString()
}
$pauseWatcher | ConvertTo-Json -Compress |
    py -3 -B DebugClient/dev_control.py --pid $gamePid raw -
py -3 -B DebugClient/dev_control.py --pid $gamePid node.inspect --path /root/ModHotReload --property process_mode
```

Use `raw -` with the `--pid` command syntax. The separate legacy form is `dev_control.py <PID> -`. Mixing `--pid <PID> -` is a CLI parse error and sends no command. Send structured JSON through stdin instead of embedding it in nested shell quotes.

Verify containment with two samples from the same process. The captured review kept the same scene host and terrain instance, `world_ready = true`, terrain readiness and progress 1. Between samples, process frames advanced by 120, drawn frames by 119 and physics frames by 105; the watcher read back `process_mode = 4`. The viewport was captured with `ViewportCapture.SavePng`, preserving the display pipeline. Those observations established a running world without another location rebuild.

Keep the original failed bridge results alongside the successful probes. A bridge `helper_failed` result is not proof that its child game never started: recover the launch artifact and confirm the PID before issuing another launch. The original missing scatter-recipe failure and the subsequent normal watcher reloads were different causes and must not be combined into one error diagnosis.

To update one species, first save its PNG/recipe in the mod, then send the `terrain_base` provider command through `raw -`: `{"provider":"terrain_base","command":"terrain.species.refresh","world":"ognishchanin/valley","species":"ognishchanin/valley/meadow_daisy","operation_id":"<fresh UUID>"}`. It rereads persisted resources and replaces only that species; a failure preserves its previous runtime. In PID 32400 the response reported `updated_sessions: 1`: scene, terrain and player instance IDs stayed unchanged, the world stayed ready and frame counters advanced. This verifies a resource update as well as watcher containment.

## One-shot C# scripts

`script.run` requires `--allow-debug-scripts` in both startup and effective arguments. `--no-development-control` blocks it. Provide an entry class implementing `IDebugRuntimeScript` in `source`, up to 32 KiB. `timeout_ms` accepts 100–30,000 ms and defaults to 5,000 ms. The compiler accepts optional references to active assemblies by ID in `mods`. The reply reports the detached JSON result, compiler diagnostics, and the unload status of the temporary context. Scripts run with the game process's permissions and cancel cooperatively. A blocked call can hold the game thread past the timeout, so run trusted source that checks the cancellation token.
