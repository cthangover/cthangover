# Developer and player tools (`editor_tools`, `scenario_studio`, `validator`, `resource_atlas`, `locale_atlas`, `mod_manager`, catalog)

Tools are mods like everything else. A game includes them through the `dev_tools` toolset, its own tool
mechanics or `--all-mods`. They build on the same registries the game uses, so what they show is
what the game will do.

Authoring windows are launched from the in-game toolbar's tools list, through
`IToolBoxButton.ToolId` -> `ToolFactory` -> `IToolProvider.CreateWindow`. They do not
add buttons to the title screen. Studio keeps its own panel button declared in
`manifest.ui`. A new window tool needs both a provider and a toolbar entry.

## Tool modules

`tools_base` owns common widgets, entity/schema forms, locale editing and neutral file/scene services; it depends only on `ui_base` and `schema_base` and registers no windows. The RPG editors are tool mods of the `rpg` engine: `rpg_object_editor` (objects, an entity editor on `tools_base`) and `rpg_editor` (maps: the document `RpgMapDocument` over `EntityDocument`, and the window; see `mods/rpg_editor/docs/en.md`). `scenario_tools_base` owns scenario files, syntax and graph models, with a `dialog_base` dependency.

Each window is independent: `scenario_editor`, `scene_editor`, `scene_builder`, `map_editor`, `interactive_editor`, `light_editor`, `catalog_export`. Existing item, character, RPG and schema tools depend on the libraries rather than the compatibility package. `editor_tools` has no production sources; it installs the seven original windows for old collections. `dev_tools` exposes separate selectable groups.

Scene creation/file access is also used by the RPG editor, so `SceneFileService` and `SceneFileEntry` are in `tools_base`. The light and RPG editors consume scenario syntax through `scenario_tools_base`.

`scene_editor` opens scenario resources through `IScenarioResourceEditor` on a tool window returned by `ToolFactory`; the Open button is unavailable without the scenario editor. The palette uses `ScenarioTextTargets`, not a concrete text-tab class. Document selection/focus promotes a target and tree exit unregisters it. Insertion is one undo operation.

## Authoring windows

- The tool windows (`ToolWindow` chrome, `ToolTheme`) open on the tree root and survive scene
  switches:
  - the scenario editor, with play-test inside a running game (`SceneManager.IsTestPlayActive`); Save
    also drops the game's scenario caches (`SceneManager.ReloadContent`), so a new or edited scenario
    plays on the next entry into its scene without a mod reload;
  - the scene editor (`SceneEditorWindow`): the logical scenes — the places of a novel. It creates a
    scene (name, mod, backgrounds; scenarios are optional — a place may hold only interactives) and changes one: backgrounds (one is shown at
    random on entry), ambient sound with a listen button, default scenario, and the scene's scenarios
    (open one in the scenario editor, create a new one). A scene is the merge of the
    `scenes/<name>.json` files of all mods; the editor writes the file of the mod chosen in "Save to"
    (`SceneFileService`), so changing a scene of a zip or foreign mod creates a file that adds to it,
    and the other files' backgrounds are shown locked. After saving it drops the scene and scenario
    caches (`SceneManager.ReloadContent`, the same kernel call the studio uses), so the scene can be entered at once.
    "Go to scene" aborts a running dialog first, as the studio's teleport does: otherwise the old
    dialog keeps the box and the entered scene's scenario waits behind it;
  - the scene builder (Godot `.tscn` scenes);
  - the map editor (zones, through `MapZoneService`);
  - the interactive editor;
  - the light editor;
  - catalog export (`ModCatalogExporter`: zip from the mod's content files plus a catalog JSON with
    archive and install SHA256).
- Data editors (`tools_base/src/EntityEditor/`): `EntityEditorWindow` is the base of the item and character
  editors, which live in their own tool mods so a game takes only the ones its data needs
  (`items_editor` → `items_base`, `characters_editor` → `characters_base`; both are in the
  `cthangover` dev tools and the optional `data_editor` mechanic of `rpg_plus`). One writable mod at a time: a
  searchable list of its entities of the kind (every JSON file of the mod whose `Kind` matches, like
  the entity resolver), a form built from the editor's `EntityField` list (text, number, picture from a
  file group with a picker, choice, flags, list of check boxes, locale text) and "other fields":
  every property the form does not know, as JSON, edited and written back as it is.
  - Beginner: a name is typed per language of the mod (`LocaleFileService`, the key is made from the
    id by the field's `KeyPattern`), pictures are picked from a grid, New/Copy/Delete, Ctrl+Z.
  - Professional: the locale key and the raw JSON are editable; data of other mods' C# survives.
  - `EntityDocument` keeps the files as JSON trees and writes only files whose text changed (2-space
    indent, letters unescaped, LF on every OS); new entities go to `<folder>/<mod>.json`. Texts go into
    the language's file that declares the key, else `<lang>.properties` (`PropertiesText` changes one
    line and leaves the rest byte for byte).
  - Save drops the entity and file caches and reloads the current language, so the game shows the
    change at once. It then publishes `EntitiesSavedEvent` (kind and mod) on the event bus: an open window
    that lists that kind rebuilds its list, so the map editor's Characters tab shows a character saved in
    the characters editor without a reopen (`RpgEditorWindow.OnEntitiesSaved`, plan 14.G17).
  - Other mods' entities (patches v2, plan 12.2): the list also shows the entities of the edited
    mod's dependencies (transitive `depends`, so the owner loads first), marked `↗ owner`; a check
    box hides them. The form shows what the game sees; an edit is written to the edited mod's
    `patches/<kind>/<mod>.json` as the minimal difference (`JsonPatchDiff`) from the value without
    that file (`EntityJson.Replay` over `EntityResolver.Explain`, `ForeignEntities.BaseWithout`) —
    the owner's file is never written. Values are compared as the DTO reads them
    (`EntityJson.Normalize`), so the form's spelling never makes a patch. Delete writes
    `"$delete": true`; "Reset to inherited" removes the entry (the file and its empty folders go
    with the last one); Copy copies a foreign entity into the edited mod under a free id. The patch
    file is part of the undo state.
  - `EntityField.Locked` (plan 12.5): a field shown but read-only for an entity, a function of its JSON.
    `characters_editor` locks health, attack and defence of a character with `Stats` and shows the
    computed sheet values under the form. The locked value is still written back as shown.
  - Renaming an id (plan 14.G6, `EntityRename` and `EntityRenameDialog`, the same in `EntityEditorWindow` and the
    Data Editor): a saved own entity's Id is read-only in the form (a new one is still typed); **Rename…** asks for
    the new id and lists the files of the edited mod that will change and the files of other mods that name the id
    (listed, never written), before anything is written. Apply saves the pending edits, then writes the changed
    files (`SchemaReferenceScanner.Plan`, `schema_base`); the window reloads, the game drops its caches and sees the
    new id. **Undo rename** writes back the files the last rename changed, from the texts read before it, so every file is
    byte for byte as it was (a separate step: the form's Ctrl+Z does not cover it).
  - Schema text fields with several texts (`stages`, `StageFieldEditor`) and locale fields implement `ITextFieldEditor`:
    the window gives them its document and the mod's languages when a mod opens, and the shown id before each load.
  - `IMapPickupEditor` (`tools_base/src/Contracts/`): the item editor's "Place on map" asks the RPG map
    editor (`rpg_editor`, tool id `rpg_map_editor`) to open a map of the item's mod with a pickup object
    chosen. The map editor writes no `Interact`, so a placed pickup gives the item only once its
    placement has `action item.add id=<item> count=1`.
  - Field badges (`FieldOriginBadge`, from `Explain`): the own patch's value, another mod's patch
    (a warning when it hides the own value: always on an own entity, on a foreign one when the
    patching mod is not a dependency), a value inherited through `Extends`; `↺` removes the own key
    from the file or the patch. Not yet: the studio layer as a write target and an id picker with
    thumbnails for reference fields (the list itself has them). Both are open items of plan 12.2/12.3.
- Tests: `rpg_object_editor_edit` (anchor of an own object and of a foreign barrel as a patch, projections), `rpg_editor_edit` (paint, place, move, delete, collision, spawn, undo/redo, a new map saved), `schema_tools_edit` (a foreign item's patch from the Data Editor, the `self.` flag in the palette), `novel_flow` (an author's whole session on the novel template through the editors' UI:
  a new scene, ways to it and back typed in the scenario editor, walked through the dialogs; the
  game's files are restored, also after an interrupted run), `tools_windows` (every window opens with no engine error; the scene editor creates a scene in
  the `test_defaults_and_patches` fixture through its UI, checks the game knows it and removes it), `ingame_play`,
  `map_editor`, `scene_builder`, `items_editor_edit` (each adds an entity through the form, saves, checks the game knows it with its name, restores the mod; the equip type is a picker; `rpg_plus/resin` is renamed through the dialog, the recipe and scenario name the new id, Undo restores every file of the mod), `characters_editor_edit` (each adds an entity through the form, saves, checks the game knows it with its name, restores the mod), `editor_foreign_patch` (`items_editor__foreign_patch`: a classic_rpg_pack item edited from rpg_plus_game becomes a patch, the owner's file stays byte for byte, `$delete`, reset removes the file), mod tests `tools_base` (`EntityDocument`, `EntityJson`, `PropertiesText`).

## Entity editor form ownership

`EntityEditorWindow` retains the document, selection, history and public authoring operations. Its utility control tree is
owned by `EntityEditorContent`, a ModWidget constructed at the same point as the previous form. Derived previews borrow
that root for typed control creation; the caller immediately attaches each returned node to its existing row or popup
owner. The root follows its body minimum size and unregisters that binding on exit. Existing control names, row order,
field visibility, keyboard routes and the pre-derived-constructor initialization order are preserved.

Named request objects retain the original mod, field or rename source id for deferred callbacks; lazy foreign lookups keep
their excluded patch identity. These contexts replace captured delegates without moving authoring state into static fields.
The optional `ValidateForSave` hook runs before serialization and writes. Its default accepts all existing editor kinds;
object-specific validation rejects a bad candidate without normalization, history or disk changes.

## scenario_studio

An in-game scenario debugger and live editor, docked at `/root`. `StudioLive` installs a debugger into
the dialog runtime: current line, pause, step, breakpoints, and dragging the next-line marker.
- It feeds a journal from the event bus.
- It edits scenarios into the folder mod itself, or into the per-game layer `user://studio/<game>`
  (`StudioLayer`, mounted through `ModOverlays`; it wins over every mod).
- It restarts a scenario with state rollback (`StudioSnapshot` = `SaveService.BuildPackage` /
  `ApplyPackage`).
- It exports the layer as a mod.
- The "Resources" button of the transport bar (every tab) re-reads backgrounds dropped into a mod or into the layer's
  `backgrounds/` while the game runs (`StudioBackgrounds.Reload`: clears `FileListResolver` and
  `BackgroundFactory`, shows the current background and its `_depth`/`_albedo` maps again). A new
  scene can then be created on any background (the pick list shows the source mod or the layer).

Test: `studio_live` (windowed).

## Static analysis

- `validator` runs in the background at start (settings: enabled, run on start, write report). It sees
  only `IModInfo`/providers, never Godot or resolver caches. Strategies run in (phase, order, id):
  contract strategies build a `ValidationGraph` (declarations and references), and reconciliation
  strategies report problems such as `depends`, entities and scenarios.
- `resource_atlas` builds on the validator graph, plus texture/shader/scene/locale keys and C#
  literals, and reports cross-mod use without `depends`, ambiguous overrides and undeclared patches.
  It runs per game. Tests: `resource_atlas*`.
- `locale_atlas`: see the localization concept.

## mod_manager and the catalog

- The hub's back button and Escape walk back one level at a time: an open mod card → the mods grid,
  the scheme editor → the games gallery, and only then to the menu (its label says where it leads).
- `ModManagerModel` merges local mods (`ModRegistry.AllDiscoveredMods`, enable state, deletability)
  with online catalog entries into cards, filters and dependency views.
  - Enabling or disabling cascades over **required** dependencies (`RequiredDependencyIds`).
  - Installs and removals are session overrides, with a restart banner.
- `CatalogService` fetches and merges the catalogs of `ModConfig.CatalogUrls` (the highest version
  wins) with a disk cache for public sources only. Player-managed sources live in `user://catalog_servers.json`; private bearer files are separate, private entries stay in memory and authenticated catalog requests disable redirects.
- `ModDownloader` downloads, checks the archive SHA256 and extracts to staging
  (`ZipFile.ExtractToDirectory`, which refuses entries outside the target), then verifies the install
  SHA256 and moves into `user://mods`.
- `ModUninstaller` removes mods.
- Test: `mod_manager`.

## Effects authoring (`effects_editor`)

The independent tool uses `EntityDocument` for profiles of kind `atmosphere`; unknown JSON stays intact.
`AtmosphereCatalog` owns temporary profile overrides and notifies existing `AtmosphereView` hosts. Editors
release overrides on close, mod change and undo; Save clears resolver caches before refreshing hosts.
Invalid form values must never silently save the previous valid data. `EffectsPreview` uses the actual
host and renderer projections.

RPG maps retain the legacy `Atmosphere` and add `Effects` (profile plus optional cell list). Null Cells
means whole map; an empty list means nothing. The Effects layer paints a grouped profile mask and erases
only the selected profile. Masks participate in document JSON snapshots, undo, save and resize clipping.
Definitions and placement have independent Save actions. The two editors launch each other through ToolFactory without depending on each other. The effects editor includes a default projection for its standalone preview.

## The shared 2D light editor on maps

`light_editor` retains its dialog-background/scenario backend and exposes `ILightEditorTargetProvider` / `ILightEditorTarget` for other 2D surfaces. The optional `rpg_editor__lights` integration mounts only with both editors. It supplies `RpgEditorCanvas` and the ordinary `RpgMapDocument`, while the existing window keeps its source list and position/radius/strength/colour controls. The registry removes providers and disposes active views before their owning assembly unloads.

`time_base.LightDef` remains the shared serialized source definition. Its optional lowercase `id` supplies map-local patch identity; unknown fields survive serialization. RPG maps store `Lights` keyed by `id`, with normalized map positions and radius in cells; `RpgMapLights` only adapts them to the existing tile renderer's cell coordinates. No new object, placement, kernel system or alternate patch writer is involved. `EntityDocument` writes foreign maps through its normal minimal keyed patches; owner updates to unpatched sources and fields continue to apply.

The night-preview toggle is local to the editor. The mode selector edits the saved `LightMode`; fixed day still saturates additional lighting, as in the game. Map source count is independent of the shader's nearest-light limit.

## Authored source projection in Studio

ScenarioDefs.ReadSource uses the same include reader as expanded runtime reads and returns an immutable raw root, expanded program, origins and consumed raw inputs. Studio binds this result to the exact event text and actual queue identities/coordinates. Runtime Action.SourceLine remains expanded; the dock projects direct rows to their authored root row and nested fragment rows to the root directive. An include breakpoint or SetNext targets only its first actual queue action. Empty or ambiguous rows have no target. FromLabel uses unique actual labels in the expanded active program.

The binding becomes stale after an observed provider/input/event/queue change and stays unavailable until a new execution rebinds. WorkingCopy preserves raw line endings and original row ownership; changed or new rows cannot address the already-running queue. The layer saver rereads the edited root through its original provider, checks repeated raw inputs for coherence and validates the planned destination with that same reader before writes. Existing dependency conflicts and lookup shadows refuse; new dependencies use CreateNew and the raw root is published last.

The current dock renders a valid empty include as a red no-executable-position row even under a current source binding. It is intentionally nontargetable and not an unknown-command syntax finding; treat the red styling as an observed explanatory UI limitation. Functional projection acceptance is separate from unrelated game-content validator findings.
