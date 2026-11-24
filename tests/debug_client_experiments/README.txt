DebugClient experiments

Run from the Cthangover repository root. Each probe uses an isolated temporary
directory. The Windows hostserver action is the entry point for Windows runs.

MCP transport and lifetime, .NET 10
    python -B tests/debug_client_experiments/sdk/run.py

Exercises the official ModelContextProtocol.Core 1.4.1 server through a
subprocess's stdio relay and a same-user pipe. The client checks tool schema,
UTF-8 text in both directions, disconnect during idle and in-flight calls,
cooperative cancellation, re-enable, and collection of both adapter contexts.
This probe uses a host stub whose dispatcher and state survive adapter unload.

Managed reference packaging and unloading, .NET 10
    python -B tests/debug_client_experiments/managed/run.py

Compiles a fixture via ModCompiler.PrepareAll using a manifest references glob.
It verifies the missing-DLL diagnostic, stages the actual SDK dependency closure,
runs a development command that constructs the SDK transport in the mod's
collectible context, forces collection after unload, and reloads with the same
cached artifact. NuGet restore and a build of Cthangover.csproj are required.

Current kernel diagnostics in the Godot 4.6.3 .NET runtime
    python -B tests/debug_client_experiments/boot/boot_probe.py --godot <mono-godot>
    python -B tests/debug_client_experiments/boot/boot_probe.py --godot <mono-godot> --mode disabled

Uses the existing Debug Cthangover build and two disposable mods. An intentionally
broken command provider cannot register. The independent healthy provider can
read the kernel compiler diagnostic with its precise source location. The case
checks that errors.pull captures the startup error and a later warning. The
disabled case checks that the healthy mod still loads while the legacy
DebugHost transport stays off. This baseline uses an explicitly configured
discovery file only inside the disposable test project.

MCP bridge in a live Godot mod, Linux
    python -B tests/debug_client_experiments/live/live_probe.py --godot <mono-godot>

Packages a bridge DLL built with .NET 10 and runtime-compiles its thin initializer.
The direct-SDK fixture checks that the current game compiler rejects the SDK's
required-member constructor with CS0619. The MCP tool reads both real compiler
errors. The kernel unloads the bridge with its MCP session open. The probe checks
EOF, refusal of new connections, collection of the bridge context, and a reply
from the same kernel DebugHost after unload. It drives kernel unload directly.

Production Debug Client smoke test
    python -B tests/debug_client_smoke.py --godot <mono-godot>

Uses the staged Release DLLs, the production runtime initializer and the root
stdio relay. It asserts all four MCP tools, the compiler error and cache status,
an operation receipt, reconnect, and cancellation when the mod is unloaded
during an active command. Test-only legacy
control uses its per-process pipe and verifies that discovery wrote no file.

Live settings, performance counters and one-shot scripts
    python -B tests/debug_client_features.py --godot <mono-godot>

Runs the actual MCP bridge with ui_base and perf_overlay. It verifies temporary
settings and restore, hidden sampling and stop, script results and diagnostics,
typed mod references, cooperative timeout, and release of the script context.
On the host, add --restart-cache to relaunch the same prepared project, verify
that script execution requires its opt-in flag, and exercise the assembly cache
after editing one mod.
