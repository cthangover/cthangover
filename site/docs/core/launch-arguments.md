# Launch arguments

Put application arguments after Godot's `--` separator. A switch takes the form `--key`. Supply a value as `--key=value` and quote values with spaces so the shell passes each occurrence as one argument.

```shell
godot --path . -- --game=my_game --new --log-file="logs/dev run.log"
```

| Arguments | Purpose |
|---|---|
| `--game=<id>`, `--new`, `--load=<slot>` | Select a collection and start fresh or load a slot |
| `--with-mods=<id,id>`, `--all-mods` | Add IDs to the selection or load the full discovered set |
| `--scene=<name>`, `--frontend` | Request a logical scene or keep the launcher frontend |
| `--hot-reload`, `--no-hot-reload` | Enable file watching or suppress it. The explicit disable wins |
| `--development-control`, `--no-development-control`, `--allow-debug-scripts` | Control [DebugHost](debug-host.md) startup features |
| `--run-tests`, `--mod=<id>`, `--test-filter=<pattern>`, `--itest=<id,id>` | Select automated test cases or integration suites. `--itest` also accepts repeated occurrences |
| `--with-test-suites`, `--choices=<id,id>`, `--timeout=<seconds>` | Mount test suites, supply ordered automatic choices, or set a driver watchdog |
| `--resource-atlas`, `--locale-atlas`, `--atlas-out=<path>` | Write resource or localization reports |
| `--log-file=<path>`, `--wait-for-debugger` | Override the log path or offer debugger attachment before boot |

## Read arguments from C#

`GameLaunchArgs.Startup` is the original snapshot of engine and user arguments. `GameLaunchArgs.Current` is the effective snapshot. `Get<T>()` returns the typed parameter or its default, `Contains<T>()` checks for a valid occurrence, and `GetAll<T>()` reads repeated user arguments in order. For `--log-file`, the user argument takes priority over the native engine argument.

```csharp
using Cthangover.Core.Games;
using Cthangover.Core.Games.LaunchParameters;

var launch = GameLaunchArgs.Current;
var extraMods = launch.Get<WithModsParameter>().Value;
var startFresh = launch.Contains<NewGameParameter>();
```

Each definition in `Core/Games/LaunchParameters` has a `Key`, `Documentation`, and `Format`. Its public parameterless constructor returns the default. Its public `string` constructor parses the value after `=`, or receives `null` for a bare switch. The type decides the value shape and validity. A flag such as `--all-mods` is a typed parameter as well.

## Read and change the effective snapshot

`args.read` returns the startup engine and user arguments and the effective user arguments. `args.catalog` lists keys, types, owners, and startup and effective values. Pass `key` to request one definition. `args.set` takes `arguments`, the complete new array of user arguments, and validates known values before replacing the effective snapshot. Include any arguments you want to keep. The startup snapshot and engine arguments remain available as originally captured. Consumers that read `Current` see the new values when they next read them. Any change that requires boot, such as permitting scripts at startup, still requires a new process.
