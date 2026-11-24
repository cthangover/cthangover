Cthangover Debug Client

Add debug_client to the active collection and apply the mod set to enable it
from the game. For a single test run, --with-mods=debug_client after Godot's --
separator forces selection for that process. Start the MCP client on the same
operating system and under the same user as the game.

Example MCP client configuration for Linux:

    {
      "mcpServers": {
        "cthangover": {
          "command": "python3",
          "args": ["-B", "/absolute/path/to/DebugClient/mcp_stdio.py"]
        }
      }
    }

For Windows use command "py" and add "-3" before "-B". The relay discovers the
running game's endpoint automatically. If it reports several game processes,
append "--pid" and the chosen PID to the argument list. --connect-timeout
sets how long the relay waits for the game at startup (default five seconds).
On Linux the relay and game must see the same TMPDIR (default /tmp).

Call these tools from the MCP client:

  diagnose({"mod":"my_mod"}) shows relevant compiler and runtime diagnostics.
  inspect({"area":"node","path":"/root/SceneManager"}) reads live state.
      The areas are node, cache, runtime, resource, and mod.
  describe({"query":"model"}) searches the live command catalog.
  run({"command":"status"}) calls a registered command.
      Use provider for a mod command and arguments for its JSON object, e.g.
      run({"provider":"scene3d_base","command":"replace-model",
           "arguments":{"object":"ogn_forage_000"}}).
      When changing state, provide your own operation_id UUID. After a lost
      connection, use run({"command":"operation.status","operation_id":"<uuid>"})
      to recover its state and response. The game keeps a bounded recent history.

The relay forwards newline-delimited UTF-8 MCP messages on stdin and stdout.
It writes its own diagnostics to stderr. The game and relay communicate over
a local named pipe. The endpoint serves one MCP session per game process and
accepts reconnects. Disabling the mod closes its session. Start a fresh MCP
client session after enabling it again.

The dev_control.py script is the separate legacy kernel development client.
Its pipe is opt-in with --development-control or development_control in config.
--no-development-control disables both external endpoints for that launch.

Temporary settings and measurements

  run({"command":"settings.read"}) shows current values.
  run({"command":"settings.apply","arguments":{"preset":"low_power"}})
      limits rendering to at most 30 FPS and scales the 3D viewport down to
      at most 0.75. It returns the previous and actual values and a restore ID.
      You can also patch named values such as {"max_fps":24} instead of a preset.
      Add persist:true only when you want to save supported profile settings.
  run({"command":"settings.restore","arguments":{"restore_id":"<id>"}})
      restores the settings captured by that change.

Select the perf_overlay mod to access performance measurements:

  run({"provider":"perf_overlay","command":"perf.configure",
       "arguments":{"enabled":true,"overlay":false,"reset":true}})
      starts a clean measurement window without showing the overlay.
  run({"provider":"perf_overlay","command":"perf.read"})
      returns frame count, mean FPS, p95/p99, phase timings and scene context.
      Renderer-dependent timings can be unavailable, for example in headless mode.
  run({"provider":"perf_overlay","command":"perf.configure",
       "arguments":{"enabled":false}})
      stops collection and retains the final snapshot.

One-shot C# scripts

To enable script.run, add --allow-debug-scripts to Godot's user arguments.
The script runs with the permissions of the game process. Cancellation is
cooperative. An infinite loop or blocking call can stall the game.

Pass the text below as arguments.source to run({"command":"script.run", ...}).
The source limit is 32 KiB. Use arguments.timeout_ms to set a cooperative
deadline for compilation and execution (default 5000, range 100–30000).
When the script uses types from loaded mods, list their IDs in arguments.mods.
The game resolves those mods and their active dependencies.

    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;
    using Cthangover.Core.Mods.Host;

    public sealed class CurrentSceneProbe : IDebugRuntimeScript
    {
        public Task<JsonNode> RunAsync(DebugScriptContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<JsonNode>(new JsonObject
            {
                ["scene"] = context.Tree.CurrentScene?.Name.ToString()
            });
        }
    }

The reply carries a JSON value or compiler diagnostics with file and line,
plus an unload status for the transient assembly context. Use context.MarkChanged()
when your script commits a change to the game. A committed mutation remains in
the game after its script context is released.
