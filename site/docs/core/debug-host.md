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
| `args.read`, `args.catalog`, `args.set` | Read or change the [effective launch arguments](launch-arguments.md) |
| `memory.stats`, `memory.inspect` | Read only [registered memory metrics and previews](memory.md) |
| `settings.read`, `settings.apply`, `settings.restore` | Inspect or change [display settings](display-settings.md) |
| `scene.switch`, `assets.refresh`, `mod.attach`, `mod.reload` | Request a logical scene switch, asset refresh, or development reload |
| `operation.status` | Query a tracked operation by `operation_id` |

For a mutation, send an `operation_id` UUID. If the connection closes, query `operation.status` with the same UUID before retrying. The host retains a bounded history for the current process.

## One-shot C# scripts

`script.run` requires `--allow-debug-scripts` in both startup and effective arguments. `--no-development-control` blocks it. Provide an entry class implementing `IDebugRuntimeScript` in `source`, up to 32 KiB. `timeout_ms` accepts 100–30,000 ms and defaults to 5,000 ms. The compiler accepts optional references to active assemblies by ID in `mods`. The reply reports the detached JSON result, compiler diagnostics, and the unload status of the temporary context. Scripts run with the game process's permissions and cancel cooperatively. A blocked call can hold the game thread past the timeout, so run trusted source that checks the cancellation token.
