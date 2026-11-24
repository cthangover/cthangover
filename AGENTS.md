# cthangover — Godot 4.6 C#

Instructions for AI agents (OpenCode, DeepSeek Harness, Claude Code, …). All agent-facing notes in this
repository are written in English. Player-facing game text is Russian (see "Localization" below).

Mod documentation lives in `mods/<mod>/docs/`. Before writing or updating it,
analyse the mod and its actual consumers, and select explanatory media from
real captures; file presence alone is not completion. Keep raw captures, editable sources and analysis records in
`../art_sources/cthangover/<mod>/documentation/`, outside all repositories.

## Screenshots: preserve the game's colour pipeline

- Keep `project.godot` → `rendering/viewport/hdr_2d=true` and the game's normal renderer. Never disable HDR 2D,
  switch to `gl_compatibility`, alter exposure/gamma/lighting, or change Windows HDR/Auto HDR, the monitor's
  colour profile or driver colour settings merely to take screenshots. Preserve the user's display mode.
- Capture through `Cthangover.Mods.Interface.View.ViewportCapture.SavePng(viewport, path)` or
  `ViewportCapture.Take(viewport)` (`mods/ui_base/src/View/ViewportCapture.cs`). `Take` returns an owned image:
  dispose it with `using`. The `doc_shots` driver already uses this helper.
- With `Viewport.UseHdr2D`, the viewport texture contains **linear HDR**, while the displayed frame is sRGB.
  Direct `GetTexture().GetImage().SavePng(...)` produces dark PNGs and is forbidden for documentation/site
  captures. A plain `Convert(Image.Format.Rgba8)` does not perform the colour-space conversion. The helper
  converts linear RGB to sRGB before 8-bit quantization, leaves alpha linear, and avoids double conversion
  for non-HDR viewports. Do not apply another sRGB conversion to its result.
- Before a capture batch, inspect the actual capture path (including test-suite drivers), use the shared helper,
  and visually compare one saved PNG with the game. Re-capture dark legacy images through the correct pipeline;
  do not compensate with brightness filters or change the game's colour mode. Store raw captures and provenance
  outside repositories as described above.

Godot reference: https://docs.godotengine.org/en/4.6/classes/class_viewporttexture.html

System concepts (how each engine system works, its invariants and traps) are in `docs/concepts/` — read the one
for the system you change; larger rewrite proposals are in `docs/analysis/rewrite-proposals.md`.

For future cthangover / Ogneshchanin optimization requests, read the
[battle optimization research note](../art_sources/cthangover/ogneshchanin/concept_20261122/OPTIMIZATION_RESEARCH_RU.md).
The author requires mass battles and defines **50 enemies as the reference large battle**, not merely an
optional stress case. Investigate GPU Gems 3 animated crowd rendering alongside existing animation and simulation LOD.

For future Ogneshchanin rendering, physics, vegetation, and effects work, consult the
[technical debt register](../art_sources/cthangover/ogneshchanin/TECH_DEBT_RU.md).
It links the GPU Gems 3 research to the current modules, existing measurements, and acceptance criteria.

### Graphics quality and hardware budgets

Every new or revised rendering algorithm must expose player-selectable quality tiers through the owning
module's settings. For 3D, integrate those settings with the existing `graphics3d_base` presets. Give the
low, medium and high tiers distinct cost or quality budgets where the feature supports them. Use an off or
simpler path for features that only fit higher budgets or require specific renderer capabilities. Apply tier
changes to presentation while preserving simulation and saved state. Verify the effective settings and
compare frame time and visual quality on the same scene and route across the affected tiers.

For RPG art generation or changes to RPG pictures, **read `comfy/rpg_life/ART_GUIDE.md` first**.
For creating, repairing or extending animated charsets, also **read
[`CHARSET_GUIDE.md`](CHARSET_GUIDE.md)**. It records the
individual-frame workflow used for Junior: the character's original idle sprite
defines its physique; Stas or the female `charset_template.png` supplies only
walking articulation. Generate and review one frame at a time, align against
that character's own upper body, and preserve approved idle pixels. Never fit
another character's body to Stas's silhouette. Junior's approved Stas assets
are protected references unless the user explicitly requests changing them.
User art rules (2026-10-28): generate all objects, buildings, vegetation and
creatures explicitly in **isometric view**; use the word "isometric" / "изометрия"
in every such prompt. Surface materials (floor, grass, earth, sand, snow, etc.)
are the exception and use an overhead surface view. Immediately save **every
intermediate generation** into `gen/<batch>/` together with its exact prompt,
including rejected attempts; do this before the next generation. Selected
production sources and generation provenance must live outside all repositories,
in `../art_sources/cthangover/<mod>/`. Never put `art_sources/`, `art_resources/`
or `terrain_sources/` inside a mod; mods contain only runtime-ready assets.
Generate **one object at a time** for image quality. There are no atlases: every
object and surface material is its own PNG in `rpg_images/<category>/<name>.png`
with an `rpg_object` entry (`docs/analysis/rpg-model-v2.md`), built by
`comfy/rpg_life/build_object.py` (picture + object stub + recipe). Existing
supplied sheets can be cut into individual objects without regenerating the set.
The guide records the approved Sacred-inspired 2.5D direction, host-watcher/ComfyUI
and built-in generation workflows, whole-room prompt constraints, source files,
the builder and visual/runtime checks. The approved cottage interior is
generated as one coherent room and is one object picture whose parts draw it
(flat in top-down, floor and wall quads in isometric); do not replace it with
repeating 1x1 stone, brick or plaster wall swatches. Older pixel-art scripts are
historical inputs, not the current style guide.

Agent skills live in `.opencode/skills/<name>/SKILL.md` (canonical copy; `.dsh/skills` and `.claude/skills`
point to it):

- `skill-code` — rules for editing C# code, scenes and tests. **Load it before any code change.**
- `skill-test` — running and debugging scenes and the test runner.
- `skill-text` — writing story text, dialogs and `.scenario` content.

## Hard rules

- **Git is for the human.** Do not run git commands that change anything (commit, checkout, reset, stash,
  add, rm, clean, …) unless the user explicitly asks. Commit only on request; when committing, do not add
  `Co-Authored-By` trailers or other author lines — use only the author `iisaev <vpcoder@ya.ru>`.
- Game resources live in `mods/`, not in the core. Never put assets into `Core/`.
- Logging only through `GameLogger.Log`; never call `GD.Print`/`GD.PrintErr` directly.
- **Subagent model delegation (MANDATORY):** Always spawn subagents (via the `Task` tool / `general-purpose` agent)
  with `model: "haiku"`. The main orchestrator stays on its current model (Opus/Sonnet). Never use Opus or Sonnet for
  subagents unless the user explicitly overrides this.

## Build

```powershell
dotnet build Cthangover.csproj
```

`Cthangover.csproj` builds only the core (`Core/`); everything under `mods/` is excluded and compiled at
runtime by the mod pipeline (see "Architecture").

### Toolchain in the Linux/WSL workspace

Godot and the .NET SDK live outside the repository in `/workspace/tools` (not on PATH by default):

- Godot 4.6.3 mono — `/workspace/tools/godot/Godot_v4.6.3-stable_mono_linux_x86_64/Godot_v4.6.3-stable_mono_linux.x86_64` (CLR 10 launcher wrapper `/workspace/tools/bin/godot`)
- .NET SDK 10.0.204 and CLR 10.0.15 — `/workspace/tools/dotnet10`; no links to SDK/runtime 8
- Environment: `source /workspace/tools/env.sh; export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` (the container has no ICU). `dotnet --version` returns 10.0.204.
- No display: windowed suites require a Windows host or an installed X server; keep HDR 2D and the normal renderer.

All owned C# projects target `net10.0`. `global.json` requires SDK 10.0.201 or a newer stable
10.0 feature band (`latestFeature`), accepting the existing Windows SDK and container SDK 10.0.204.
SDK/runtime 8 is not required. Godot 4.6.3 mono supports this target, including editor tools and
self-contained desktop export. Its own vendor tools still target net8, but GodotPlugins already
requests `LatestMajor`; the project does not inject a roll-forward policy or `DOTNET_HOST_PATH`.
Boot retains the CLR >= 10 check for the collectible-context fix (dotnet/runtime#116953).

`Directory.Build.targets` copies the SDK net10.0 reference pack beside the core and through
project references. Runtime mod/test compilation uses that shipped pack; its content signature
joins cache keys. Players can compile mods without an SDK. Framework implementation assemblies
are excluded from the metadata scan. Godot/vendor package targets are not rewritten.

`Directory.Build.props` no longer pins the runtime of developer tools. `Cthangover.csproj` pins
10.0.15 only for ExportDebug/ExportRelease, preserving player servicing even with SDK 10.0.201.
`build_release.sh` / `.bat` verify net10.0, the bundled runtime, all shipped reference assemblies
and the mod source generator before archiving (Python 3 required). Windows developers need only
SDK 10 + Godot 4.6.3 mono/templates (+ Python 3 for release validation); SDK 8 is not needed.

Release verification uses the archive created in that invocation, or the freshly exported folder with
`--no-archive`; an older archive must never override that folder. It compares the played core/deps/config
with the current export and rejects development trees in the PCK. Only public `mods/` ship loose;
`test_*` fixture mods are excluded, while gameplay `stat_checks` remains required by RPG Plus.
Русский: verify проверяет текущий экспорт; старые архивы при `--no-archive` не используются.
PCK не содержит рабочих/приватных каталогов, loose-моды не содержат `test_*` фикстур;
`stat_checks` — игровой мод, остаётся в релизе.

Русский: все собственные проекты — SDK 10/net10.0. Godot 4.6.3 поддерживает сборку, tool-скрипты
и self-contained экспорт; vendor assemblies не меняются. Reference pack поставляется с игрой,
SDK игрокам не нужен. Runtime 10.0.15 закреплён только для релизных экспортов.

## Running tests

Single cross-platform entry point — `tests/Cthangover.TestRunner` (C#, net10.0 target, SDK 10 + Godot .NET).

```powershell
# Everything: build → xUnit → all integration tests → summary
dotnet run --project tests/Cthangover.TestRunner

# One test from tests/tests.manifest.json
dotnet run --project tests/Cthangover.TestRunner -- --test unit
dotnet run --project tests/Cthangover.TestRunner -- --test battle
dotnet run --project tests/Cthangover.TestRunner -- --test coverage
dotnet run --project tests/Cthangover.TestRunner -- --list
dotnet run --project tests/Cthangover.TestRunner -- --json test-results.json
dotnet run --project tests/Cthangover.TestRunner -- --no-build --test inventory   # skip the core build
```

- Exit code `0` requires a nonempty valid result, the expected terminal name/core, matching counters and process exit 0. Schema-2 contracts are explicit in `tests/tests.manifest.json`; a missing or duplicate marker never passes.
- `--test` accepts repeated name globs. `--mod rpg --filter "followers/*"` selects individual mod cases; `--test unit --filter <VSTest expression>` selects xUnit cases. Other selectors: `--profile`, `--kind`, `--tag`, `--game`, `--renderer`.
- `--list` / `--catalog <path>` expose the catalogue, including manual-suite reasons. `--plan` prints dependency closure without running tests. `--failed <report.json>` repeats unsuccessful launches with fresh prerequisite preparation.
- Every scheduler mode reuses content-fenced prepared inputs under OS leases (`--input-cache`, outside the source tree).
  The first baseline population is counted; later revisions synchronize changed files/removals, and warm same-revision
  read runs copy zero input files/bytes. No full project copy belongs in each host wrapper. `dependsOn` chains share
  sequential component DATA; failed setup blocks dependents. Run-owned homes/temp are removed on success unless kept;
  reusable inputs have separate retention. Failed run state is retained.
- `--jobs N` defaults to half the logical cores, clamped to 2..8; `--gui-jobs G` and `--write-jobs W` default to 2.
  Read/output entries share sealed inputs; write components lease bounded independent physical lanes with delta
  restoration and no writable hardlinks to source/baseline. Components schedule longest first; results stay in
  plan order. `--jobs 1` changes scheduling only; the prepared override is fixed, never rewritten per launch.
- In parallel outer runs, xUnit collections run sequentially inside their one child job (saved `unit.runsettings`), preserving all assertions and avoiding nested scheduling starvation. `--jobs 1` retains the original xUnit settings.
- Manifest `project` is `read`, `output` (only argument-declared `res://tests/artifacts/...` directories) or `write`
  (conservative default). Component OS homes are private (`XDG_DATA_HOME` / Windows `APPDATA` / macOS `HOME`).
  Each launch has unique logs/native stdout/stderr/JSON/screens/sandbox/temp outside prepared inputs. Native
  `--log-file` precedes `--`; Core validates actual res/user/temp before Settings. Mismatch fails closed.
  Ctrl+C stops child trees and retains partial JSON/run state. `requiresExplicitSelection: true` admits a suite
  only through its exact `--test <name>`, including prerequisites; ordinary default/profile/glob runs exclude it.
- Build outputs are reused only with matching compilation identity, receipt and actual hashes; `--no-build` verifies
  those artifacts and refuses stale/missing ones. Unexpected immutable changes quarantine the generation;
  `--repair-inputs` is explicit recovery under the exclusive gate. Named build scratch is separate from inputs;
  there is no blanket `.godot`/`tests/artifacts` integrity exemption. Retention is scoped to owned inactive stores
  and run homes; active leases prevent deletion.
- Reports and streamed logs are unique per run: `tests/artifacts/runs/<run-id>/` (`report.json`, `report.html`, `report.junit.xml`, original TRX/mod reports and per-case results). Partial reports cannot claim success. Details and examples: `tests/Tests.md`.
- Shutdown joins background owners and drains finalizers before engine teardown. Menu warm-up
  is cancelled when its menu leaves and joined before starting/loading gameplay. This does not
  repair Godot 4.6.3's separately reproduced movable-wrapper/native-pointer race; do not mask
  native crashes with retries or GC policy changes that have not passed extended stress.
  Русский: выход ожидает фоновые задачи и финализаторы; прогрев меню отменяется при уходе
  из меню и завершается перед входом в игру. Отдельная гонка interop Godot остаётся дефектом
  движка; не скрывайте аварии повторами или непроверенными настройками GC.
- Compilation artifacts persist under `<temp>/cthangover-tests/cache/assemblies` (and sibling `test_assemblies`),
  outside disposable run workspaces. `--fresh-cache` uses a private cold cache. Startup prunes pairs unused
  for fourteen days. Compilation uses per-key OS file leases and immutable PDB-first/DLL-last publication;
  invalidation advances a generation instead of deleting artifacts another process may load.
- JSON checkpoints remain atomic after each launch; HTML/JUnit export only at completion or handled abort.
  Reports include phase totals, wall/child seconds and per-launch overhead, first-driver boot time,
  sampled root-process CPU/peak memory and log-line counts. `inputPreparation`, `workspaceMetrics` and launch
  `execution.inputsCopied` expose actual file/byte transfers, identities, conflicts and scoped retention reasons;
  `cacheHit` alone never proves warm zero-copy behavior. Cached PDBs retain their producer's absolute paths.
- Scene paths in the manifest are checked with exact case before Godot starts (Windows forgives wrong case,
  Linux/CI and `.pck` do not).
- Unit tests (xUnit, pure C# without Godot): `tests/Cthangover.Tests/`. Integration scenes and their setups
  (compiled only in the dev build): `tests/Cthangover.Tests.Integration/`. Human-oriented details — `tests/Tests.md`.

### Launch flags (user args after `--`)

Parsed by `Core/Games/GameLaunchArgs.cs` and `Core/Scenes/SceneLaunchArgs.cs`.

| Flag | Effect |
|---|---|
| `--scene=<logical name>` | after boot, jump into a mod-defined scene (`SceneManager.HasScene`); unknown name → ERROR `--scene: logical scene '<name>' not found` and the normal launcher. A `.tscn` path is passed positionally instead (Godot loads it as the main scene) |
| `--game=<id> [--new \| --load=slot_N]` | load only that game's mod set and enter it |
| `--all-mods` | load all installed mods (dev mode; also implied by dev flags) |
| `--frontend` | load only the shell (menu, mod manager), even with dev flags |
| `--with-mods=a,b` | add mods (with their dependencies) to a game or the shell; a mod disabled in the mod manager is enabled for this run (with its required dependencies) |
| `--choices=0,1` / `--timeout=<s>` | mount the `dialog_base__autoplay` test suite (auto-plays dialogs, see below); `--dialogs=N` passes it after N finished dialogs instead of at the first idle box (hub games never go idle) |
| `--itest=<id>` | mount the matching mod test suite (integration GUI test) |
| `--run-tests [--mod=<id>]` | run mod unit tests and quit |
| `--resource-atlas` / `--locale-atlas` | build the atlas reports and quit |
| `--with-test-suites` | mount every test suite (used by the atlases) |
| `--wait-for-debugger` | show the system "attach debugger" dialog before the boot phases |

`TestRunner` adds `--all-mods` to integration tests that do not name `--game=`/`--frontend`/`--all-mods`.

### Mod test suites (`test_suites`)

Integration-test code lives inside the mod it tests, not in separate mods. A host manifest declares:

```json
"test_suites": [
  {"id": "inventory", "activate": ["--itest=inventory"]}
]
```

The suite folder `mods/<host>/test_suites/<id>/` holds an ordinary `manifest.json` (sources, ui, depends, …) and
content laid out like a mod. When a user arg starts with one of the `activate` prefixes (or `--with-test-suites`
is passed), `Core/Mods/ModTestSuites.cs` mounts it as the virtual mod `<host>__<id>`, which implicitly depends on
its host and may depend on mods the host itself must not. Without the flag the folder is inert: a normal launch,
a game and `--all-mods` never load it. Suites do not nest.

Current suites:

| Virtual mod | Activation | What it does |
|---|---|---|
| `dialog_base__autoplay` | `--choices=`, `--timeout=` (+ `--dialogs=N`) | `DialogAutoDriverNode` clicks through the active dialog, prints `[TEST_RESULT] name=test_runner …`, quits 0/1 |
| `dialog_base__gdscript` | `--itest=gdscript` | GDScript mod hooks |
| `dialog_base__doc_shots` | `--itest=doc_shots --doc-steps=<file>` (with a game; + `--shots=<dir>`) | documentation screenshots: plays a steps file (`scene`, `time`, `wait`, `abort`, `next`, `waitanswer`, `choose`, `panel`, `key`, `tap`, `press`, `give`, `party`, `signal`, `hover`, `click`, `drag`, `hurt`, `run`, `tool`, `call`, `type`, `shot`) and saves a picture per `shot` (windowed) |
| `items_base__inventory` | `--itest=inventory` | inventory GUI driven like a player |
| `cooking__cooking_walk` | `--itest=cooking_walk` | cooking walk-through |
| `interactive_base__interactives` | `--itest=interactives` | scene interactives |
| `menu_base__menu_flow` | `--itest=menu_flow` | shell, menu, game picker, saves, collection editor |
| `menu_base__save_dormancy` | `--itest=save_dormancy` | changing a game's mod set → dormant save data → cleanup |
| `menu_base__collection_edit` | `--itest=collection_edit` (with `--game=novel_template --new`) | edits the running game's collection (includes the `dev_tools` toolset), menu → New game of the same game must switch the mod set in-process with the editors, the studio and its toolbar button; a player's own package of the game is moved aside and restored |
| `menu_base__display_settings` | `--itest=display_settings` (with `--frontend`; + `--shots=<dir>`) | Display tab of the settings menu: windowed size centred on the monitor, borderless fullscreen, frame rate cap, V-Sync, restore from the saved file, reset; `--shots=` saves the main menu and every settings tab; restores the player's settings file (windowed) |
| `menu_base__new_game` | `--itest=new_game` (with `--frontend`) | player path into the main game: the menu warms the games up, then Play → `cthangover` card → New game switches in-process (no restart) → `prolog_alley` with Marao's opening dialog clicked through |
| `menu_base__resettables` | `--itest=resettables` (with `--frontend`) | `rpg_plus` → `novel_template` → `rpg_plus` → New game in one process: the stats owner of an unloaded mod is dropped, a reloaded mod registers exactly one owner, old contexts are released |
| `menu_base__hot_reload` | `--itest=hot_reload --hot-reload` (with `--frontend`) | two probe mods are loaded in a running front end, edited and reloaded by the file watcher; a compile error is rejected while the previous version stays |
| `live2d_base__avatar` | `--itest=live2d_avatar` | Live2D avatar (windowed) |
| `editor_tools__windows` | `--itest=tools_windows` (+ `--shots=<dir>`) | opens every tool window, fails on any engine error, drives the map/interactive/light canvases, creates and removes a scene through the scene editor; `--shots=` saves a screenshot per window (windowed) |
| `scenario_editor__ingame_play` | `--itest=ingame_play` (with `--game=novel_template --new --with-mods=scenario_editor`) | play-test from the scenario editor inside a running game: the play-test stays in the game, Stop and a scene switch of the played scenario return into the game, never to the main menu (windowed) |
| `editor_tools__novel_flow` | `--itest=novel_flow` (with `--game=novel_template --new --with-mods=editor_tools`; + `--shots=<dir>`) | an author's session through the editors' UI only: the toolbar opens the scene editor, a new scene is made, its scenario (a way back) and a way to it in the town's scenario are typed in the scenario editor, Go to scene, then town → new scene → town by clicking answers; restores the game's files (windowed) |
| `rpg__walk` | `--itest=rpg_walk` (with `--game=rpg_plus --new`) | the RPG world played with real key events: walking, collision, signs and NPCs on E/F, a door teleport, events flipped by a story flag, a battle that returns to the same cell with its win flag, a pickup the suite adds through its own `placements` file that stays gone after leaving the map and after loading a save |
| `rpg__plain_walk` | `--itest=rpg_plain_walk` (with `--game=novel_template --new --with-mods=rpg,tile_topdown_base`) | the world without any battle mod: a minimal map is walked with WASD, a wall stops the player, a step trigger runs its commands and sets its flag, three plain steps on a map with `Encounters` start no battle |
| `rpg__party_walk` | `--itest=rpg_party_walk` (with `--game=rpg_plus --new`; `rpg_party_walk_iso` adds `--tile-renderer=isometric`; + `--shots=<dir>`) | the party behind the hero: a villager joins through his dialog (`party.add`) and walks behind; the follower walks the hero's trail one step behind in both projections, turns with the path, the hero cannot step onto it, gathers on the hero after a teleport and after a map load, and a follower without a walking sheet logs an ERROR without a crash (windowed) |
| `rpg__render` | `--itest=tile_render` (with `--game=rpg_plus --new`; + `--shots=<dir>`) | the RPG maps drawn by the tile renderer, both projections |
| `rpg__snapshots` | `--itest=rpg_snapshots` (windowed; not in `tests/tests.manifest.json`, run explicitly) | pixel snapshots of every `rpg_map` in every tile renderer, a game look and a probe pass; without its reference set it fails by design |
| `rpg_plus_game__play` | `--itest=rpg_plus` (with `--game=rpg_plus --new`; + `--shots=<dir>`) | the RPG Plus sample end to end: the prologue (rogue, the charisma check), the sheet and bound battle attributes, gear and a tonic through the inventory item actions, conditions, the merchant's stock, a formula skill on the wolf that has its own stats, the training yard level-up and class skill, Take off in the sheet panel, a save/load round trip |
| `rpg_plus_game__woodland` | `--itest=woodland` (with `--game=rpg_plus --new`; `--ward-ending=pact` for the second ending) | the woodland quest chain, the loot-to-crafting loop, both shrine choices, repeat-safe rewards, save/load |
| `rpg_plus_game__desert` | `--itest=desert` (with `--game=rpg_plus --new`) | west/east transitions, one-time barrel loot, both desert battles, save/load |
| `rpg_plus_game__winter` | `--itest=winter` (with `--game=rpg_plus --new`; renderer variants in the manifest) | desert/winter transitions, one-time loot, snow profiles, planar fences in both renderers |
| `equipment__sheet_equip` | `--itest=sheet_equip` (with `--game=rpg_plus --new`; + `--shots=<dir>`) | the character sheet's equipment through its own buttons: a second hero joins the rpg_plus party, the party list shows with two heroes, marks the leader and switches between them; an empty weapon slot's Equip opens the bag's picker (only what fits this slot and this hero), a pick equips it and the stats follow; Replace puts the longer sword on and the worn one back into the bag; an empty trinket slot says so; Take off empties the slot; the other hero's sheet is unchanged (windowed) |
| `schema_tools__edit` | `--itest=schema_tools_edit` (with `--game=rpg_plus --new`) | the Data Editor window adds an item with an equip type and a trait (fields of two mods' schemas), the game sees the gear after Save; the command palette writes and checks a line and inserts it into an open scenario; the new file and the locale edits are removed |
| `trading__trade` | `--itest=trade` (with `--game=rpg_plus --new`; + `--shots=<dir>`) | gathering and trading in the rpg_plus world: walking onto brushwood, stones and a mushroom picks them up, the mushroom heals, the merchant's dialog opens the trade window, junk is sold and an elixir bought by clicking the two inventories, no walking while trading, Esc closes |
| `cthangover_game__site_shots` | `--itest=site_shots` (with `--game=cthangover --new`; + `--shots=<dir>`) | the frames of the site: prologue dialog, locations entered by scene switch, the inventory/characters/map/journal/skills panels (inventory seeded, Murakami in the party), a card battle at the town entry; refresh the site with `site/make-shots.ps1` |
| `rpg_editor__edit` | `--itest=rpg_editor_edit` (with `--game=rpg_plus --new`; + `--editor-renderer=isometric` for the iso variant) | RPG map editor (model v2): the palette lists the four shapes, the projection toggles during editing, a surface and a segment painted, a sprite placed, dragged and deleted, Ctrl+Z / Ctrl+Y, a collision cell and the spawn point; a new map made with "+" and saved is a map and a scene the game knows at once; the demo map is never saved, every mod file is compared afterwards and the new map's files are deleted |
| `rpg_editor__tutorial` | `--itest=tutorial_first_game` (with `--frontend`; + `--shots=<dir>`) | tutorial "Your first game in 10 minutes", step 1, clicked: the Mods & Collections editor of RPG Plus, "New mod" with a title (the id follows), base `rpg_plus_game`, "Create mod"; the mod joins the game. Leaves the mod and the user copy of the collection for `rpg_editor__tutorial_play` (a marker in the mod folder keeps the collections as they were) |
| `rpg_editor__tutorial_play` | `--itest=tutorial_first_game_play` (with `--game=rpg_plus --new`; run after `rpg_editor__tutorial`; + `--shots=<dir>`) | tutorial steps 2 to 10 of "Your first game in 10 minutes", clicked: the map editor's "+" makes the map, Paint of grass and a forest path, Place of a cottage and three trees; a chest gives the mantle (no dependency is added: the mod reaches village_pack through rpg_plus_game); a trigger on the edge leads to rpg_village with the target picked on the canvas; the scenario elder_welcome and a villager with its dialog; the character Lina ("Joins the party", her scenario lina_hello, placed from the Characters tab); "Play from here" with real keys: the chest, the villager, Lina's line and her join, then the door; the character sheet's Equip puts the mantle on. Removes the mod and restores the user collections from the marker; rpg_plus_game files are compared with their text and list |
| `items_editor__edit` | `--itest=items_editor_edit` (with `--game=rpg_plus --new`) | item editor: a demo item's name in the form, New + name/price/type typed in the form, Copy undone with Ctrl+Z, Save; the game knows the item and its name at once, other item files untouched; the new file and locale edits are removed; "Place on map" opens the RPG map editor with the item's pickup chosen |
| `items_editor__foreign_patch` | `--itest=editor_foreign_patch` (with `--game=rpg_plus --new`) | a foreign item (a `classic_rpg_pack` dagger) edited in the item editor is saved as a patch in `rpg_plus_game` with only the changed field; the source stays byte-identical; reset removes the patch file |
| `rpg_object_editor__edit` | `--itest=rpg_object_editor_edit` (with `--game=rpg_plus --new --with-mods=rpg_object_editor`) | RPG object editor: the anchor of an own sprite typed in the form and saved (the game knows it at once, the preview draws it in both projections); the anchor of a foreign barrel (village_pack) saved as a patch in rpg_plus_game while the source file stays byte-identical; the projection toggle (isometric, top-down, both); every touched file is written back afterwards |
| `characters_editor__edit` | `--itest=characters_editor_edit` (with `--game=rpg_plus --new`) | character editor: New + name/health/charset typed in the form, Save; a character with `Stats` has its health, attack and defence read-only; the game knows the character with its stats and name at once; the new file and locale edits are removed |
| `scenario_studio__live` | `--itest=studio_live` with `--studio-write=layer --studio-layer=<dir>` (+ `--shots=<dir>`) | studio dock on the real kitchen scenario: current line, pause/continue, novice edits, apply with rollback restart into a test layer (the mod stays untouched), breakpoint hold, dragging the ▶ marker to set the next line, resizing/moving the dock, other tabs, a background dropped into the layer and reloaded (new scene on it, replaced file shown, exported); removes the layer (windowed) |
| `mod_manager__help` | `--itest=mod_help` (with `--frontend`) | every visible button, toggle and list of the Mods & Collections hub explains itself with a tooltip, on every level |
| `mod_manager__documentation` | `--itest=mod_documentation` (with `--frontend`) | the mod manager's documentation pages |
| `mod_manager__collection_settings` | `--itest=collection_settings` (with `--frontend`) | collection settings in the mod manager |
| `mod_manager__new_mod` | `--itest=new_mod` (with `--frontend`) | the Mods & Collections editor as a player uses it: "New mod" writes the mod folder (manifest, locale stub, no `.git`) and adds it to a fixture game (`new_mod_itest_game`, a copy of rpg_plus); "Start scene" picks a scene of the game's mods; Save writes both. Leaves the fixture and the mod for `new_mod_check` |
| `mod_manager__new_mod_check` | `--itest=new_mod_check` (with `--game=new_mod_itest_game --new`; run after `new_mod`) | the wizard's mod is loaded by the next launch and New game enters the picked start scene; removes the fixture and the mod |
| `locale_editor__window` | `--itest=locale_window` (with `--game=rpg_plus --new`) | translations window: lists the probe key missing in Korean (the suite's own locale files), types the line, Save writes it into the suite's `locale/ko.properties`, the atlas lists one gap less, the game shows the text; the Korean file is removed |
| `scenario_editor__new_scenario` | `--itest=new_scenario` (with `--game=rpg_plus --new`) | scenario editor without the dev tools: "New" makes `scenarios/<name>.scenario` in the game's mod (a bad or existing name is refused), the command palette inserts `action stat.add` into the open scenario, save writes it; the file is removed |
| `tools_base__isolation` | `--itest=tools_isolation` (with `--frontend --with-mods=<one tool>,tools_base`; `--expected-tools=`, `--absent-mods=`) | each tool alone: the exact window registry, forbidden mods absent, every window opens and closes, insertion targets released |
| `village_pack__snow_enemies` | `--itest=snow_enemies` (with `--game=rpg_plus --new`) | the four snow enemies load, battles run, defeat persists, both renderers |
| `village_pack__biome_expansion` | `--itest=biome_expansion` (with `--game=rpg_plus --new`) | expanded outdoor maps, furniture collision, building door round trips in both projections |
| `cthangover_game__marao_avatars` | `--itest=marao_avatars --run-tests --mod=cthangover_game__marao_avatars` (windowed: it reads pixels; in `tests/tests.manifest.json`) | ten reaction presets of the layered avatar renderer |
| `avatar_base__contracts` | `--itest=avatar_contracts --run-tests --mod=avatar_base__contracts` (in `tests/tests.manifest.json`) | contract checks of the avatar engine |
| `avatar_layers__contracts` | `--itest=avatar_layers_contracts --run-tests --mod=avatar_layers__contracts` (windowed: rendered pixels; in `tests/tests.manifest.json`) | contract checks of the layered avatar renderer |

### Integrations (`integrations`)

Glue that works only together with partner mods lives in `mods/<host>/integrations/<id>/` (its `manifest.json` with
`sources`; its own `depends` may be left out). The host declares it in its manifest:
`"integrations": [{"id": "battles", "with": ["turn_battle_base"]}]`. `Core/Mods/ModIntegrations.cs` mounts it as the
virtual mod `<host>__<id>` only when the host and every `with` partner are in the mod set; otherwise it is left out
(log `MODS_REGISTRY`). An integration is never part of a collection, the disabled list or a mod manager card, and holds
no content and no save data.

| Integration | Mounted when | Gives |
|---|---|---|
| `rpg__battles` (`mods/rpg/integrations/battles`) | `rpg` + `turn_battle_base` | the `rpg.battle` action, random encounters (`Encounters` of a map), win flags |
| `turn_battle_base__quests` (`mods/turn_battle_base/integrations/quests`) | `turn_battle_base` + `quests_base` | a battle started with `quest_id` notifies that quest; a won battle adds `new_tag` |
| `test_integrations__probe` (fixture) | `test_integrations` + `quests_base` | used by the suites `integrations` (`--with-mods=test_integrations --run-tests --mod=test_integrations`) and `integrations_frontend` |


A new GUI integration test copies the `items_base/test_suites/inventory` pattern: an initializer that adds a driver
(`ModRoot.AddDeferred`, never a raw `CallDeferred("add_child")`) — a Node ticking on a `Timer`, ending with `[TEST_RESULT] name=<name> passed=N failed=M` and `Quit(0|1)`, with its own
watchdog (60 s by default). Do not combine `--itest=` with `--timeout=` unless you also want the dialog autoplay
suite mounted. Add the test to `tests/tests.manifest.json`.
If a mod of an `--itest=` run does not compile, the boot logs the errors, prints a failing `[TEST_RESULT]`
and quits 1 at once (`ItestCompileGuard`) instead of hanging until the timeout.

```powershell
godot --path . --headless -- --scene=home_kitchen --itest=inventory
```

### Smoke run of a scene with dialog autoplay

```powershell
# Logical scene (the "name" field of mods/*/scenes/*.json) + autoplay
dotnet run --project tests/Cthangover.TestRunner -- --switcher home_kitchen --choices 0,1 --timeout 30

# A concrete .tscn (passed to Godot positionally as the main scene; prefer logical names)
dotnet run --project tests/Cthangover.TestRunner -- --switcher res://<path>/<scene>.tscn --timeout 30

# Directly through godot (no runner)
godot --path . -- --scene=home_kitchen --choices=0 --timeout=30
```

Details and log analysis — skill `skill-test`.

### Mod unit tests (`--run-tests`)

A mod declares `"test_sources": ["tests/*.cs"]` in `manifest.json` (currently `audio_base`, `avatar_base`, `avatar_layers`, `locale_editor`, `battle_formulas`, `cooking`, `dialog_base`, `effects_base`, `equipment`, `expressions_base`, `items_base`, `locale_atlas`, `map_base`, `mod_manager`, `quests_base`, `resource_atlas`, `rpg`, `rpg/integrations/battles` (the integration `rpg__battles`), `rpg_editor`, `rpg_object_editor`, `schema_base`, `schema_tools`, `stat_checks`, `stats_base`, `test_integrations`, `tile_isometric_base`, `tile_render_base`, `tile_topdown_base`, `tools_base`, `trading`, `turn_battle_base`, `validator`). Methods
`[ModTest("group/case")]` (static, `void`/`Task`, parameter `ModTestContext`; see `Core/Mods/Testing/`) are compiled
into a separate assembly `<mod>__tests` and loaded into their own collectible context (they see the types of the
mod and its dependencies).

```powershell
godot --path . -- --run-tests                 # all mods with test_sources
godot --path . -- --run-tests --mod=cooking   # one mod; unknown/no tests → exit 1
dotnet run --project tests/Cthangover.TestRunner -- --test mod_units
```

- A failure shows the `ModAssert`/exception message and the test stack (file:line).
- Async tests may await frames (`await tree.ToSignal(tree, "process_frame")`); between tests the recovery removes
  only `/root` nodes added by the test and reports ERROR if the test removed a core node. `TimeoutSeconds` applies
  to async tests only.
- The test assembly cache tracks the production assemblies of the mod and its dependencies: a mod API change
  recompiles the tests (compile error with file:line instead of a runtime failure).
- Skip is not a failure: `[TEST_RESULT] name=mod_units passed=N failed=M skipped=K`.
- Report: `user://mod_tests/last-report.json` (+ `runs/`, `history.jsonl`).

### Debugging mod code (PDB + debugger)

Mod assemblies are compiled with a portable PDB next to the dll in the cache (`user://mod_cache/assemblies/` and
`.../test_assemblies/`) and loaded **from disk** — a debugger (Rider/VS: Attach to Process → `godot.exe`) binds
breakpoints to mod sources, stacks contain file:line. A dll without its PDB is treated as stale and rebuilt.
`--wait-for-debugger` lets you attach before mods are compiled.

Logs: `user://logs/cthangover.log` is authoritative (stdout captured through pipes may be truncated); errors are
also duplicated to `user://logs/cthangover_errors.log`. Useful categories: `TEST` (drivers), `MODTEST` (mod unit
tests), `MODS_COMPILE` (compile errors with file:line), `MODS_REGISTRY` (test suite mounting).

## Games (collections): launch, menu and saves

A game is a collection of `kind: game`. A collection is a **package of its own next to the mods**, never a file inside
a mod: `collections/<id>/` (`collection.json`, `cover.png`, optional `locale/*.properties` with the collection's texts)
in the game directory, or `user://collections/<id>/` for the player's edited, duplicated and downloaded ones (folder
or zip; `Core/Mods/Collections/ModCollectionStore.cs`). The mod set is **not frozen**: on every launch it is
recomputed from the current collection definition (`Core/Games/GameModSet.cs`) plus all `kind: frontend` collections
(menu, mod manager — `collections/frontend/`). The player may change mechanics; the game's saves stay.

| Launch | What is loaded |
|---|---|
| no arguments (normal run, Godot editor) | only the shell: menu "Continue / Play / Load / Mods / Settings" |
| `-- --game=cthangover [--new \| --load=slot_1]` | game + shell, straight into the game (entry point `entry.scene`) |
| `-- --all-mods` or dev flags (`--scene=`, `--run-tests`, `--itest=`, `--choices=`, `--timeout=`, `--resource-atlas`) | all mods |
| `-- --frontend` | the shell even with dev flags (menu tests) |

Switching games replaces the mod set in-process (`GameSession.SwitchToAsync` → `ModSetReloader`): the new set is
compiled in the background, mods that leave it are unloaded through `ModUnloader` (their nodes in `/root` are freed,
their load contexts released), shared mods stay loaded. The main menu warms every game's set up into the assembly
cache in the background (`GameSession.WarmUp`), so picking a game normally only loads cached assemblies.

- A collection defines `entry.scene` (otherwise `start_scene`), `settings` (`ActiveGame.GetSetting`, e.g.
  `battle.core`) and `cover` (image inside the collection's mod, shown in the game picker).
- Menu (`mods/menu_base`): "Play" → `GamePickerPanel` (game cards from `GameCatalog`: readiness, installing missing
  mods via `ModCollectionInstaller`, saves, "Configure" → editor); "Continue" — last session (`LastSessionStore`).
  Entry logic — `GameFlow`.
- Saves — `user://saves/<gameId>/slot_N.json`, unlimited slots. Saves of a game are listed per game; the
  menu does not adopt saves made before games existed (there is no compatibility with old formats).
- Loading into a changed mod set shows a diff (`SaveCompositionDiff`) and backs up to `<game>/backup/`. Data of mods
  missing from the game (or older than installed) is kept as dormant slots (`SavePackage.Dormant`, `SaveDormancy`)
  and returns with the mod; a mod without data gets `IModSaveable.OnJoinedExistingGame`.
- Collection editor: "Duplicate" (moves saves along), "Restore built-in", editable JSON; editing inside an included
  block copies it for this game (`<block>.<game>`) without changing other games.
- Built-in collection packages (`collections/`):
  - `cthangover` — the main game: story and engine mods, characters, quests, items, cooking, recruiting, Live2D, the battles (`turn_battle_base` with `card_battle` and `ff_battle`), the interface and the `dev_tools` toolset;
  - `novel_template` — the modder's starting point: a town with a bar and a home, nothing but text, backgrounds and
    scene switches, on the bare 2D engine mods. It shows the intended split: the game's own content in
    `my_novel_game`, reusable backgrounds/sound in `town_pack` (shared with `cthangover`), engine mods;
  - the toolset `dev_tools` — the editors, checks and atlases: `scenario_editor`, `scene_editor`, `scene_builder`, `map_editor`, `interactive_editor`, `light_editor`, `catalog_export`, the studio (`scenario_studio`: a game with it gets the Studio button on its toolbar), `validator`, `resource_atlas`, `locale_atlas`, `mod_manager`, the data editors (`schema_tools`, `items_editor`, `characters_editor`), the RPG editors (`rpg_editor`, `rpg_object_editor`) and `effects_editor`;
  - `rpg_plus` — the RPG Maker style sample game (it replaced the former demo collection): a novel-style prologue picks the class, then the tile village and forest walked with WASD, events on E/F, gathering, a merchant and battles. Mod set: `rpg_plus_game` (all content), `rpg` (the world; its `rpg__battles` integration mounts with `turn_battle_base`), `village_pack` and `classic_rpg_pack` (art and rules as data), `stats_base`, `stat_checks`, `equipment`, `battle_formulas`, `items_base`, `trading`, `crafting`, `turn_battle_base` with `ff_battle` (`card_battle` is the alternative battle mechanic), the views `tile_topdown_base` and `tile_isometric_base`, `dialog_base`, `characters_base`, `audio_base`, `ui_base`, `menu_base`, and the tools `schema_tools`, `items_editor`, `characters_editor`, `effects_editor`, `rpg_editor`, `rpg_object_editor`. Settings: `battle.core=ff_battle`, `stats.player=rpg_hero`, `checks.dice=d20`, `tile.renderer=topdown`. Entry scene `rpg_plus_crossroads`;
  - `frontend` — the shell: menu, settings, mod manager.
- Strings a mod shows live in its own localization (or its dependencies'): menu/settings — `menu_base`, journal —
  `quests_base`.
- Tests: `menu_flow`, `collection_edit`, `rpg_object_editor_edit` (the RPG object editor's own suite), `new_game_cthangover` (menu → New game → in-process switch → prologue, end to end), `game_cthangover`, `game_novel_template`, `rpg_walk`, `rpg_plain_walk`, `game_rpg_plus`, `rpg_plus`, `schema_tools_edit`, `rpg_editor_edit`, `items_editor_edit`, `characters_editor_edit`, `trade`, `save_dormancy`,
  `resource_atlas_<game>` / `resource_atlas_frontend`.

## RPG world (`rpg`)

The RPG world is the engine mod `rpg` (model v2: contract `docs/analysis/rpg-model-v2.md`; how the code reads it and
where it bites: `docs/concepts/rpg.md`). It holds no content: the story and maps are in `rpg_plus_game`, the art in
`village_pack`, the rules in `classic_rpg_pack`; the picture rules for new art are at the top of this file.

- `rpg` alone walks, collides, talks and teleports; it does not depend on `turn_battle_base`. Battles, random
  encounters and `rpg.battle` come with the integration `rpg__battles` (see Integrations).
- The map editors are `rpg_editor` (maps; tool `rpg_map_editor`) and `rpg_object_editor` (objects). Both edit another
  mod's entries as patches in their own mod; the source file stays byte-identical.
- Tests: unit tests `--run-tests --mod=rpg` (and `rpg/integrations/battles`); suites `rpg__walk`, `rpg__plain_walk`, `rpg__party_walk`,
  `rpg__render`, `rpg__snapshots`, `rpg_plus_game__play`; the editors' suites `rpg_editor__edit` and
  `rpg_object_editor__edit`.


## Adding vs replacing another mod's content

Overriding is a mod's right, never a duty. **Add** with your own name (a new scenario for a foreign scene, a new item,
a new background) — it lives next to the existing content. **Replace** by shipping the **same name** (texture, file
path, entity `Kind`+`Id`, scenario file name) **and** declaring `depends` on the owner: mods load dependencies first,
so the dependent one wins (`FileListResolver`, `EntityResolver`, `ScenarioResolver.GroupReplacing`); the owner's files
stay untouched. Scene definitions merge instead (later mods add backgrounds, change ambient/default scenario). The
same name in mods not linked by `depends` has no defined order — the resource atlas reports it as an error (scenarios
included, key kind `scenario`). The studio layer (`ModOverlays`) wins over every mod.

## Resource map (`resource_atlas`) — read before editing mods

`mods/resource_atlas` (tools, built on the `validator` graph) shows which mods take resources from each other,
override or patch them, and whether that is declared in `depends`. Report:
`tests/artifacts/resource_atlas/report.md` (+ `report.json`).

```powershell
dotnet run --project tests/Cthangover.TestRunner -- --test resource_atlas
godot --path . -- --resource-atlas --atlas-out=res://tests/artifacts/resource_atlas --with-test-suites   # without the runner
```

- Keys match the runtime resolvers: textures — by file name (one namespace across all `texture_groups`),
  sounds/music/shaders/scenes — path inside the group, entities — `Kind`+`Id`, localization — `lang:key`, plus
  scenario actions, entity kinds, `res://mods/<id>/` and patches (`patches/scenes/**`, `patches/<kind>.json`).
  Requests come from data and scenarios (validator graph) and from C# literals:
  `PlaySound`/`PlayMusic`/`PlayAmbient`/`Textures.Resolve`/`Shaders.Resolve`, `Translate("…")`/`Tr("…")`/`Locale = "…"`.
- **Errors (step red):** a foreign resource without a dependency in the transitive `depends`; one key provided by
  mods not linked by `depends` (the winner depends on random load order); a patch of a foreign resource without a
  dependency. **Warnings:** a key no mod provides.
- Fix: declare the dependency, move the resource to the owning mod (shared UI sounds go to an engine mod, not
  content) or give the resource its own key. C# dependencies are checked by the compiler separately: a mod compiles
  only against the closure of its `depends` (`ModCompiler.DependencyClosure`).

## Localization

### Key map (`locale_atlas`) — read before editing strings

`mods/locale_atlas` (tools, no dependencies) maps all localization keys: declarations in `locale/*.properties` of
every installed mod per language, and references — `key=` in `.scenario`, `Translate("…")`/`Tr("…")`/`Locale = "…"`
in C#, JSON text fields (`Name`, `Description`, `locale`, `title`…), `"@key"` in collections, auto-translated
`text`/`tooltip_text` in `.tscn`, manifest `mod_<id>_name/_desc`. Report: `tests/artifacts/locale_atlas/report.md`
(+ `report.json`).

```powershell
dotnet run --project tests/Cthangover.TestRunner -- --test locale_atlas
godot --path . -- --locale-atlas --locale-out=res://tests/artifacts/locale_atlas --with-test-suites   # without the runner
```

- Project languages = every language with at least one file (now `ru`, `en`, `es`, `zh`, `ja`, `ko`, `vi`, the site's languages and `LanguageCode.Supported`). Each problem lists the file and line
  to fix (sibling file of the same mod: `ui_en` → `ui_ru`, `scenarios_en` → `scenarios_ru`).
- **Errors** (a real breakage, the atlas test fails): reference to an undeclared key; empty value; one key twice
  in one mod with different values; a file in a `locale/` subfolder (the loader does not read it); encoding
  problems (non-UTF-8, U+FFFD, control characters, mojibake, CR-only line breaks).
- **Warnings** (translation gaps: reported with the file and line to fill, the game falls back to a language that
  has the text, nothing fails): a key missing in some language; collection text (`name`/`description` without `@`)
  without a `locale` entry for non-`en`; plus BOM, mixed LF/CRLF, duplicate key with the same value, a mod
  overriding a key only in some languages, `{0}` placeholder mismatch between languages, Cyrillic in `en`, `???`
  in a value, missing `mod_<id>_name/_desc`.

  The atlas is a helper: it answers "what is not translated yet, and where", so a gap in a language never reads as
  a broken build — only structural problems and broken text do.

### Reusable packs

Reusable packs use English source literals and technical identifiers. User-visible names and messages use locale
keys with English and translated entries in the pack's flat `locale/` files. Never put game-specific rewards,
currency or Russian-only object definitions into a reusable art pack.

### Scenario strings

Russian text is the literal inside `.scenario`; translations go to `locale/scenarios_<lang>.properties` (including
`scenarios_ru`). Exception: the template game `my_novel_game` is what modders copy, so its literals are **English** and
Russian lives only in `scenarios_ru.properties`. An empty `select ""` is written without `key=`.

## Logging

`GameLogger.Log("CATEGORY", "message"[, LogLevel])` — `Core/Utils/GameLogger.cs`.

Line format: `[2026-05-31 12:00:00.000] [CATEGORY] message`. Output goes to stdout and to
`user://logs/cthangover.log` (errors also to `cthangover_errors.log`).

Each file sink retains at most three files by default: active `cthangover.log`,
`cthangover.log.1` (newer archive), `cthangover.log.2` (older archive). The errors
sink and `--log-file=<path>` use the same suffix scheme and independent budgets.
`LoggingSection.MaxFileSizeMb=10` (`max_file_size_mb`, MiB) and `MaxFiles=3`
(`max_files`, including active) are configurable; values below 1 clamp to 1.
An existing full/oversized file rotates at startup. Later checks use a UTF-8 byte
counter and rotate before the next record exceeds the budget. A single oversized
record stays intact, then rotates before the following record; failed rotation
continues in the current file. I/O failures never abort gameplay.

`RollingLogWriter` owns a persistent UTF-8 writer without BOM, LF, AutoFlush and
`FileShare.ReadWrite | FileShare.Delete`. GameLogger serializes both sinks with
its lock and closes them on `ProcessExit` or explicit `Shutdown()` (idempotent).
The full log includes `.1`/`.2`: read `.2`, `.1`, then the active file for chronology.
TestRunner copies the entire `logs` directory; failure diagnostics take recent
errors from the current errors file. RetainedDiagnostics also includes `.log.N`
archives when evicting retained workspaces, subject to its existing tail/budget limits.


The category is mandatory and is a free-form UPPER_SNAKE tag. Reuse the existing tag of the subsystem you touch
(grep `GameLogger.Log("` in its mod) instead of inventing a new one. Frequent ones: `TEST`, `MODS`, `MODS_REGISTRY`,
`MODS_COMPILE`, `MODTEST`, `BOOT`, `SCENE`, `DIALOG`, `CHAIN`, `EVENT`, `INTERACTIVE`, `BATTLE`, `FF_BATTLE`,
`AUDIO`, `SAVE`, `FACTORY`, `UI`, `WIDGET`, `TSCN`, `MAP`, `LIVE2D`, `QUEST`, `SHADER`, `LANG`, `GAME`,
`RESOURCE_ATLAS`, `LOCALE_ATLAS`.

## Code conventions

- Line endings are LF everywhere (`.bat`/`.cmd` stay CRLF): every repository has `.gitattributes` with
  `* text=auto eol=lf` and `core.autocrlf=false`, `.editorconfig` and the JetBrains project code style say LF.
  New files and scripts that write files must write LF; when a Python edit reads a file, keep its bytes
  (`newline=''`).
- All `using` directives go inside `namespace`.
- Asset paths — only `res://` or relative, never absolute (`C:\...`, `/home/...`); the reference case must match the
  file. Mod file providers treat a wrong case as a miss on every OS and log ERROR `path case mismatch` with the correct
  spelling (`Core/Mods/ModPathCase.cs`); repository check — `tests/Cthangover.Tests/Arch/PathPortabilityTests.cs`.
- Pure C# classes without Godot dependencies are tested with xUnit; Godot-dependent classes (Node, Resource) are
  verified by running a scene or a mod test suite.
- `Core/Autotest/` holds only the editor autotest entry points (`SceneBuilderAutotest`, `MapEditorAutotest`,
  `TscnTestEntryPoint`).
- A new battle core must implement `IBattleCore` + `IBattleAutoPlayable` and be added to
  `BattleCoverageTestSetup.DeclaredTests` and `tests/tests.manifest.json` (check: `--test coverage`).

## Architecture: the core is a mod player

Game logic lives in engine mods (`mods/<name>`) compiled at startup by `Core/Mods/ModCompiler.cs` (Roslyn →
assembly loaded from disk). The core assembly contains only the player infrastructure: loading and compiling mods,
registries, bridges and transitional contracts.

- **Godot source generators in mods:** `ModCompiler` runs the Godot generators over mod sources through
  `GodotGeneratorHost` only when `Godot.SourceGenerators.dll` is found (otherwise a `MODS_COMPILE` warning
  "mod scripts will lack lifecycle binding" and engine callbacks of mod nodes are silently never called). With the
  generators, `_Ready`/`_Process` overrides in mods work; the forwarders `ModNode`, `InputHandlerNode`, `ModWidget`
  from `ui_base` remain the common base classes — see `skill-code`.
- **KernelPurity** (`tests/Cthangover.Tests/Arch/KernelPurityTests.cs`): subsystem namespaces
  (`Cthangover.Core.Items/.Quests/.Characters/.Interactive/.Skills/.Audio/.Scenarios`,
  `Core.UI.Lights/.Dialog/.Executable/.Base/.Menu/.Tool/.Event/.Messages/.Theming/.View`) are closed by
  `Enforced: true` rules — a subsystem cannot "grow back" into the core; exceptions are listed in `AllowedTypes`.
  New gameplay code goes into a mod, not into `Core/`.
- **ui_base** (`mods/ui_base`) — the UI engine mod: widget toolkit (`Widget`/`ModWidget`, lists, `ModNode`,
  `InputHandlerNode`), `ViewBox`, `ToolBox`/`ToolWindow`, `GameMenu`/`SaveIcon`, `MessagesHelper`/`NotificationPanel`,
  `DeviceInput`, `UiKit`. The core talks to it only through `IWidget`, `IBackgroundView`, `IToolbarHost`,
  `ISaveIconHost`, `IUiLazyConstruct` and the bridges `UiThemeBridge`/`NotificationBridge` (`Cthangover.Core.UI`).
  A mod using `ui_base` types must declare `ui_base>=0.1.0`. (Its C# namespace stays `Cthangover.Mods.Interface`.)
- **Boot window:** scripts needed before mods compile stay in the core — `Launcher` (main scene), `BootLogPanel`,
  `ModSpriteLoader`, autoload `SceneEventController`, theme `CthangoverTheme`/`UiTokens`.
- Mod dependencies (`manifest.json → depends`) define compile and load order.

### Mod folders

Game-directory mods live in `mods/` (public) or optional `mods_private/` (private, including 3D mods).
Both have equal discovery priority; at equal priority a strictly newer version wins. In runtime,
`user://mods/` has higher priority than either. Read and edit existing mods through their
`IModFileProvider`; `ModRoots.ModsPath` is a primary write target for new mods, never an owner lookup.
Publishing (`sync_mods.py`, `sync_mod_repos.py`, `build_mod_releases.py`,
`site/build-mods-catalog.py`, `build_release.sh`) deliberately reads only `mods/`, preventing accidental
publication of private mods.

Each game-directory mod folder may supply `tests.manifest.json` with `integration` and `manualSuites`
entries; the runner merges these with `tests/tests.manifest.json` before validation and copies every mod
folder into its isolated workspace. To add a folder, update `Core/Mods/ModRootFolders.cs`, the runner's
`ModFolders.cs`, the compile exclusions/content entries in `Cthangover.csproj`, its `.gdignore`, and
`.gitignore` when private. Folder names must agree in the core and runner (checked by unit tests).

### Mod map

| Mod | Role |
|---|---|
| `ui_base` | UI toolkit (see above) |
| `shell2d_base` | 2D shell scene: view box, toolbar, panel slots, notifications; parent of 2D mods |
| `time_base`, `effects_base`, `audio_base`, `map_base` | engine subsystems: clock + lights, visual effects, audio, world map |
| `interactive_base`, `dialog_base` | scene interactives; dialog UI + queue runtime + `.scenario` DSL + event chains (incl. `when "<condition>" -> :label` and `option "…" if=<condition>`) |
| `characters_base`, `items_base`, `quests_base`, `skills_base` | characters/avatars, items/inventory/recipes, quests, skills |
| `turn_battle_base` → `card_battle`, `ff_battle` | turn-based battle platform and its two battle cores |
| `cthangover_game` | Main-game content and the starter-party initializer (`src/StarterPartyInitializer.cs`); game = collection package `collections/cthangover`. There is no separate installed `cthangover_party` mod. |
| `cthangover_murakami` | Murakami's story line (content of the `cthangover` game) |
| `cooking`, `recruiting` | cooking mechanic, recruiting defeated characters |
| `trading` | shops as data (`"Kind": "shop"`: goods sold, junk bought back, prices), the trade window of two inventories (merchant and player), money as the item `money/coin`; action `trade.open shop=<id>` |
| `live2d_base` | Live2D Cubism runtime |
| `avatar_base` | Renderer-independent avatar entities, named preset groups, independent instance state and renderer factory discovery; see `mods/avatar_base/docs/en.md` |
| `avatar_layers` | PNG layer implementation of the avatar renderer: ordering, placement, visibility, opacity and resource ownership; see `mods/avatar_layers/docs/en.md` |
| `menu_base`, `mod_manager` | main menu/settings/saves, mod manager (the shell) |
| `tools_base`, `scenario_tools_base` | Shared editor widgets, entity/schema forms and neutral file/scene services; scenario files/syntax/graph models in the dialog-specific library. Neither registers windows. |
| `scenario_editor`, `scene_editor`, `scene_builder`, `map_editor`, `interactive_editor`, `light_editor`, `catalog_export` | Independent authoring windows opened from the in-game toolbar. `editor_tools` remains a source-free compatibility package installing all seven. |
| `validator`, `resource_atlas`, `locale_atlas` | Independent static mod validation and diagnostic atlases. |
| `locale_editor` | Translations window on `locale_atlas` (its scanner as a library): lists the texts a language lacks and writes each one into the locale file of its owning mod; in `cthangover` and `rpg_plus` (`docs/en.md`) |
| `my_novel_game` | content of the template game (collection `novel_template`): town, bar, home scenes and scenarios only |
| `town_pack` | reusable content: town entrance, home vestibule, bar backgrounds, bar ambience (used by `cthangover_game` and `my_novel_game`) |
| `music_pack` | shared soundtrack: 22 tracks, default exploration/combat and main menu playlists; used by the frontend, Cthangover, the novel template and RPG games; scene-specific `town_entry` playlist remains in `cthangover_game` |
| `rpg` | RPG Maker style tile world over the 2D shell, model v2: `rpg_object` (surfaces, segments, sprites, actors), `rpg_placement` (one object on one map, id `<map>/<local id>`), `rpg_map` (legend, ground and structure rows, collision, strokes, encounters), pictures in `rpg_images`; grid walking (WASD), interactions on E/F or by stepping, states and `self.` flags, teleports (`rpg.teleport`); a map is a logical scene (`docs/concepts/rpg.md`). No battles here and no dependency on `turn_battle_base`: battles, random encounters and `rpg.battle` come with the integration `rpg__battles`. The map editors are `rpg_editor` and `rpg_object_editor` (see their `docs/en.md`) |
| `rpg_object_editor` | editor of RPG objects (`rpg_object`, model v2): a form by shape (surface, segment, sprite, actor), the first picture, footprint, anchor, interaction, extends, other fields as JSON; a preview in the top-down and the isometric projection drawn by the game's own renderer; a foreign object is edited as a patch in the own mod (`patches/rpg_object/<mod>.json`); documentation `mods/rpg_object_editor/docs/en.md` |
| `tile_render_base`, `tile_topdown_base`, `tile_isometric_base` | Tile maps: `tile_render_base` turns a prepared map description into a picture (depends on `ui_base`, `effects_base`); its two views are `tile_topdown_base` (square grid, from above) and `tile_isometric_base` (2:1 diamonds). The view is the game's `tile.renderer` setting, overridden by `--tile-renderer=` |
| `rpg__battles`, `turn_battle_base__quests` | Integrations (virtual mods `<host>__<id>`, see "Integrations" under the test suites): `rpg` + `turn_battle_base` gives battles on the map; `turn_battle_base` + `quests_base` makes a won battle tag a quest. Neither holds content or save data |
| `rpg_editor` | map editor of the `rpg` mod for model v2 (plan section 12.4): the document of a map (grids with an automatic legend, placements, strokes, patches of foreign maps and placements, undo, byte-stable save) and the window: a palette by shape, the map in the game's own top-down and isometric projections, selection and moves, collision, spawn, teleport links, the encounters panel when `rpg__battles` is mounted; user docs `mods/rpg_editor/docs/en.md` (7 languages) |
| `items_editor`, `characters_editor` | data editors of items and characters on the `tools_base` `EntityEditorWindow` base: searchable list, form with texts in every language of the mod, pictures, other fields as JSON, live update, entries of other mods edited as patches in the own mod (`docs/concepts/tools.md`; user docs `mods/items_editor/docs/en.md`, `mods/characters_editor/docs/en.md`). `characters_editor` shows the party leader and the computed battle numbers (`stats_base`); `items_editor` has "Place on map" (`IMapPickupEditor`, implemented by `rpg_editor`) |
| `village_pack` | reusable art for RPG maps, model v2: objects (`objects/*.json`, `rpg_object`), pictures in `rpg_images`, charsets, battle art, atmosphere profiles (`atmospheres/`); the White Pass scene in `rpg_plus_game` exercises winter art |
| `expressions_base` | shared expression parser/evaluator, function and operator strategies, strict dialog assignments; source-compatible formula APIs reused by stats |
| `stats_base` | genre-neutral stats: `stat`/`archetype` kinds, traits on any entity (`"Traits"`), shared expressions from `expressions_base`, sheets, levels and class skills, pools, bound stats written into the battle attributes, conditions `stat()`/`level()`/`archetype()`, actions `stat.*`, the character sheet panel with `ISheetSection` blocks (`docs/concepts/stats.md`) |
| `stat_checks` | DSL `check <stat> <dc> -> :label` (stat + dice vs difficulty) |
| `equipment` | `equip_slot` kind, gear by item keys (`Equip`, `Traits`, `Archetypes`, `StatUp`), item actions `action/equip` and `action/stat_up`, `equip.*` actions, `equipped()` condition, the sheet's Equipment block |
| `battle_formulas` | battle actions with `Formula=` in their values for every core (damage/heal, cost, pierce, status), enemies' sheets |
| `classic_rpg_pack` | reusable classic fantasy rules as data: attributes, combat stats, warrior/mage/rogue, slots, gear, formula skills |
| `rpg_plus_game` | all content of `rpg_plus`: class-choice prologue, village, house, forest, shrine, desert, training yard, characters, items, shops, quests and scenarios |
| `schema_base` | the schema model shared by tools: `schemas/*.json` of any mod describe actions, commands and entity fields (`SchemaCatalog`, `SchemaChecker`, `SchemaLine`, `SchemaReferences`); read by `validator` (references, unknown params, known kinds) and by the `tools_base` entity editors (fields of other mods' schemas) |
| `schema_tools` | editors and checks from annotations: `schemas/*.json` of any mod describe actions, commands and entity fields (model in `schema_base`); the Data Editor window (other mods' entries edited as patches) and the command palette (`self.` flags) (`docs/concepts/schemas.md`) |
| `scenario_studio` | in-game scenario debugger/live editor dock: pause/step/breakpoints, restarts with state rollback, edits into folder mods or the per-game layer `user://studio/<game>` (core `ModOverlays`), export as a mod, read-only `self.` flags of the running object; `--studio` opens it (style guide section `#studio`) |
| `test_defaults_and_patches` | test fixture of the property patch system |
| `crafting` | Workbench stations that turn materials into items: stations and recipes are plain data, and any scenario opens a station with one command |
| `effects_editor` | Toolbar editor of atmosphere particle and ripple profiles (`effects_base`): preview, placement, and a button opening the RPG map editor, whose Effects tool paints profile masks on cells |
| `npc_barks_base` | Concurrent ambient `.scenario` hosts, actor reservations, speech cues, attention and spatial conditions; background authoring/debugging through `dialog_base` contracts. See `site/docs/mods/scenes/scenarios/background.md`. |
| `junior_reaper_paper_grief` | Investigation game content: RPG outdoors, mouse-driven interiors, map-owned events and ambient NPCs; collection of the same id. |
| `test_integrations` | Test fixture, not content: its integration `probe` joins a mod set only together with `quests_base`. Never part of a game |

### Mod naming

A mod id says what role the mod plays, by a fixed **suffix or prefix**, in lowercase `snake_case`. The folder name,
the `id` in `manifest.json`, the repository (`git.cthangover.ru/cthangover/mod_<id>`) and the `mod_<id>_name/_desc`
locale keys always match.

A new mod starts with `"version": "0.1.0"`; its dependencies are written `>=0.1.0`.

When creating a new mod, set up its repository immediately, before adding content:

1. Copy `.gitignore` and `.gitattributes` from an existing mod into the new mod's root
   (for example, `mods/cthangover_game/.gitignore` and `.gitattributes`).
2. Copy the directory `mods/.git-template` to `mods/<id>/.git`.
3. Update `mods/<id>/.git/config`: replace `{name}` in the template's `origin` URL with
   the exact mod id. Keep the server, namespace and authentication from the template;
   the repository path is `cthangover/mod_<id>` on `git.cthangover.ru`.
   Never print or copy credentials into documentation or logs.

Creating a mod authorizes this template setup; it does not authorize commits or pushes.
Never overwrite an existing mod's `.git` directory.

| Role | Pattern | Examples |
|---|---|---|
| Engine subsystem other mods build on (library, shell, UI) | `<subject>_base` — `base` only as a suffix | `dialog_base`, `ui_base`, `shell2d_base`, `menu_base`, `turn_battle_base`, `live2d_base` |
| Mechanic on top of the engine, optional for a game | plain noun, or `<variant>_<mechanic>` | `cooking`, `recruiting`, `card_battle`, `ff_battle` |
| The game itself: its scenes, story, entry point | `<game>_game` | `cthangover_game` |
| Further content of one game (a story line, the party) | `<game>_<part>` | `cthangover_murakami`, `cthangover_party` |
| Reusable content any game may take (backgrounds, ambience, characters) | `<theme>_pack` | `town_pack` |
| Template a modder copies to start | `<what>_template` (a template game is a collection package; its own mod is named as a game) | collection `novel_template` → `my_novel_game` |
| Developer tool | `<subject>_<tool>` | `editor_tools`, `scenario_studio`, `resource_atlas`, `locale_atlas`, `mod_manager`, `validator` |
| Test fixture | `test_<what>` | `test_defaults_and_patches` |

- Never `core` in a mod id: "core" is the C# kernel (`Core/`). Never `base` as a prefix.
- A test suite is the virtual mod `<host>__<suite>` and follows its host's name.
- C# namespaces are not renamed with a mod (`ui_base` keeps `Cthangover.Mods.Interface`); new mods use
  `Cthangover.Mods.<Subject>`.
- Renaming a mod: folder, `id`, every `depends`/collection entry, `res://mods/<id>/` paths, `<id>__suite` ids,
  locale keys, tests manifest, docs (`AGENTS.md`, skills, `site/docs`, quickstarts and their zips) and the
  repository's `origin` URL.
