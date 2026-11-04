# Launching, testing and diagnostic tools

Use Godot 4.6 .NET and .NET 8. `dotnet build Cthangover.csproj` builds the kernel only: sources under `mods/` compile at runtime with Roslyn. A successful kernel build does not prove that mod compilation succeeds.

## Choosing a game or scene

Godot user arguments follow the separator `--`:

```text
godot --path . -- --game=novel_template --new
godot --path . -- --game=rpg_plus --load=slot_1
godot --path . -- --game=junior_reaper_paper_grief --new
godot --path . -- --game=rpg_plus --new --with-mods=rpg_object_editor --studio
```

| Argument | Effect |
|---|---|
| `--game=<collection_id>` | Load that game's mod set and enter it |
| `--new`, `--load=<slot>` | Start fresh (also the default) or load the game's save |
| `--with-mods=a,b` | Add the named mods and required dependencies for this run |
| `--all-mods` | Load all ordinary installed mods, overriding game/frontend selection; fixtures and inactive suites stay excluded |
| `--frontend` | Force the menu/mod-manager set when no explicit game or all-mods selection wins |
| `--scene=<logical_id>` | Navigate after boot to a registered logical scene; unknown IDs log an error and retain normal launcher behaviour |
| `--tile-renderer=topdown` / `isometric` | Override the collection's tile renderer |
| `--studio` | Open Scenario Studio when available |
| `--hot-reload`, `--no-hot-reload` | Override the editor/default file-watching policy |
| `--wait-for-debugger` | Show the attach-debugger dialog before boot |

With no game or development flag, only the frontend is loaded. Development flags (`--scene=`, `--itest=`, `--run-tests`, autoplay, atlases, debugger) imply all-mods when no game or frontend is selected. `--all-mods` takes precedence, then `--game=`, then `--frontend`, then the development fallback. An actual `.tscn` path is Godot's positional main-scene argument, not a logical `--scene=` value.

## Unified runner

```text
dotnet run --project tests/Cthangover.TestRunner -- --list
dotnet run --project tests/Cthangover.TestRunner -- --test unit
dotnet run --project tests/Cthangover.TestRunner -- --test mod_units
dotnet run --project tests/Cthangover.TestRunner -- --test resource_atlas
dotnet run --project tests/Cthangover.TestRunner -- --test locale_atlas
dotnet run --project tests/Cthangover.TestRunner -- --test coverage
dotnet run --project tests/Cthangover.TestRunner -- --json test-results.json
```

No selection runs the complete manifest: kernel build, xUnit and integration tests. `--no-build` skips the kernel build. Test IDs, scenes, arguments and window requirements live in `tests/tests.manifest.json`; logs go to `tests/artifacts/logs/<test>.log`. Exit 0 means the selected checks passed. Pure xUnit tests live under `tests/Cthangover.Tests`; mod units use each manifest's `test_sources`, compiled into a separate test assembly.

Direct mod testing uses `--run-tests [--mod=<id>]`. GUI suites declared in `test_suites` mount as `<host>__<suite>` only when an activation prefix matches, or with `--with-test-suites`; ordinary games and `--all-mods` do not activate them. A fixture needs explicit `--with-mods=`. Windowed tests need a real renderer/display, including pixel, Live2D and editor screenshot checks.

## Autoplay and captures

`--choices=0,1 --timeout=30` mounts `dialog_base__autoplay`, advances text and chooses indexed answers. `--dialogs=N` finishes after N completed dialogs for hub games that never become idle. The result marker is `[TEST_RESULT]`; inspect error logs and runtime mod compilation, not only process exit.

`dialog_base__doc_shots` runs capture steps with a selected game, `--itest=doc_shots --doc-steps=<file> --shots=<directory>`. It can navigate scenes, operate dialogs and panels, drive keys/mouse and save actual runtime frames. `cthangover_game__site_shots` and `site/make-shots.ps1` provide the site's game frames. Keep source captures and analysis outside mod repositories; bundled documentation assets are selected runtime captures.

## Validator and atlases

`validator` checks data/scenario references, schemas, conditions and unknown parameters independently of the authoring windows. `resource_atlas` maps providers and consumers, detects foreign resources without a transitive dependency, unrelated providers of one key, and dependency-less patches; missing providers are warnings. It also scans map-owned event scripts and teleport links. `locale_atlas` maps declarations and references in all installed languages: undeclared keys, invalid text/encoding and contradictory declarations are errors; missing translations are warnings and retain fallback behaviour.

Direct flags `--resource-atlas` / `--locale-atlas` generate reports and quit. `--with-test-suites` includes inert test resources for a full audit. The locale editor consumes the atlas scan and writes translations into the owning mod's locale files.

In the shared `/workspace` environment, heavy or longer-than-about-30-second runs must use the existing Windows watcher: publish an atomic, uniquely named job with a Russian purpose, track it to `.exit`, and inspect its result/state/log. See the workspace's `watch/README.md`; do not start another watcher or infer success from a pending job.

## Screenshot colour pipeline

Keep the normal renderer and `rendering/viewport/hdr_2d=true`. Capture through `Cthangover.Mods.Interface.View.ViewportCapture.SavePng(viewport, path)` or `Take(viewport)`, disposing the returned image. HDR 2D viewport pixels are linear; saving them directly to PNG makes screenshots dark. The shared helper converts RGB to sRGB before 8-bit quantization and does not transform alpha. `Convert(Rgba8)` alone is insufficient. Do not change HDR, exposure, display colour profiles or rendering backend to compensate. Compare a trial capture before a batch. This requirement is also recorded in workspace and project `AGENTS.md`.
