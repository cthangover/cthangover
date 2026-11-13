# Scene Event Metadata

The `[SceneEvent]` attribute allows mod C# code to hook into scene lifecycle events declaratively, without needing manifest subscriptions.

## `[SceneEvent]` attribute

Place the attribute on a **class** in a mod assembly to register it for automatic execution. Only classes deriving from `ExecutableEvent` are registered (`Core/Scenes/SceneEventRegistry.cs` skips any other type):

```csharp
using Cthangover.Core.Scenes;
using Cthangover.Core.UI.Executable;

[SceneEvent("home_kitchen", Priority = 5)]
public class KitchenInitEvent : ExecutableEvent
{
    // Runs when home_kitchen scene loads, ordered within the
    // scene's event chain by its Priority / After metadata
}

[SceneEvent("town_entry", After = "other_event")]
public class TownEnterEvent : ExecutableEvent
{
    // Runs after the event whose ID is "other_event"
}
```

There is no method-based registration and no `SceneContext` parameter — the attribute cannot be applied to methods (`AttributeUsage(AttributeTargets.Class, AllowMultiple = false)`).

## Attribute parameters

| Parameter | Type | Description |
|---|---|---|
| `SceneName` | string | *(positional, required)* Target scene name |
| `Priority` | int | Execution order (lower = earlier). Default 0 |
| `After` | string | Name of another handler this should run after |
| `Condition` | string | Condition expression — handler only runs if expression is true |

## Registration

`SceneEventRegistry` uses reflection at assembly load time to discover all **classes** decorated with `[SceneEvent]`. Only types deriving from `ExecutableEvent` register; decorated classes without that base type are silently skipped. No manual registration is needed — the attribute itself serves as registration.

## Execution order

`[SceneEvent]` classes are not a separate phase: `SceneManager.ComposeEvents` composes them into the **same event chain** as `.scenario` definitions. The composition runs synchronously inside `SceneManager.SwitchScene` — it is **not** a bus subscriber — and the composed chain is enqueued right after the `scene_entered` lifecycle event is published. Engine subscribers of that event run at their own fixed priorities (background 0, ambient audio 5, music type 10, UI host 30), which are independent of scenario ordering. So the order on scene enter is:

1. `scene_entered` bus subscribers apply scene defaults, audio, and UI host changes
2. The composed chain executes: scenario + `[SceneEvent]` events interleaved and sorted by their own `priority` / `After` metadata

On scene exit, `scene_exited` is published on the bus; there are no per-scene exit hooks beyond its subscribers.

## vs manifest `ui` entries

| Feature | `[SceneEvent]` attribute | Manifest `ui` entry |
|---|---|---|
| Registration | Attribute on class (must derive from `ExecutableEvent`) | JSON entry in `manifest.json` |
| Purpose | Game logic running in the scene's event chain | UI panels/overlays anchored to shell slots |
| Scene binding | `SceneName` positional parameter (required) | Optional `scene` field — when set, the element is only available in that scene |
| Condition support | Yes (`Condition` param) | No |
| Priority ordering | Yes | Yes (order inside the anchor slot) |
| Dependencies (`After`) | Yes | No |

Use `[SceneEvent]` for game-logic hooks. Use manifest `ui` entries when you need to anchor a panel or overlay to a shell slot.

## Manifest `conflicts_with` — declarative only

The `conflicts_with` manifest field is **declarative only**: it is parsed (`Core/Mods/ModManifest.cs`) and exported to catalog entries, but the runtime does **not** enforce conflicts — no mods are disabled or blocked at load time. Treat it as informational metadata describing mutually exclusive mod pairs for catalogs and tooling.

## Naming your mod

The `id` tells other modders what role the mod plays, by a fixed suffix or
prefix, in lowercase `snake_case`. The folder name, `id`, the repository
(`mod_<id>`) and the locale keys `mod_<id>_name`/`mod_<id>_desc` match.

| Role | Pattern | Examples |
|---|---|---|
| Engine subsystem other mods build on | `<subject>_base` | `dialog_base`, `ui_base`, `shell2d_base` |
| Mechanic, optional for a game | plain noun or `<variant>_<mechanic>` | `cooking`, `card_battle` |
| The game itself: scenes, story, entry point | `<game>_game` | `cthangover_game` |
| Further content of one game | `<game>_<part>` | `cthangover_murakami` |
| Reusable content any game may take | `<theme>_pack` | `town_pack` |
| Template to copy | `<what>_template` | the collection `novel_template` (its own mod: `my_novel_game`) |
| Developer tool | `<subject>_<tool>` | `editor_tools`, `resource_atlas` |
| Test fixture | `test_<what>` | `test_defaults_and_patches` |

Never use `core` in an id and never `base` as a prefix. The game a player
picks is not a mod but a [collection package](../collections/) that lists
these mods.

## `manifest.json` field reference

Every mod's `manifest.json` is deserialized into `ModManifest` (`Core/Mods/ModManifest.cs`). All fields are optional:

| Field | Type | Description |
|---|---|---|
| `id` | string | Canonical mod identifier. Used for deduplication (when two mods declare the same `id`, the higher `version` wins) and in other mods' `depends`. Falls back to the filesystem entry name when absent |
| `name` | string | Human-readable mod name (shown in UI) |
| `version` | string | Semver mod version (e.g. `"0.1.0"`). Used by the save system to detect downgrades and trigger migrations |
| `author` | string | Author credit string |
| `description` | string | Short description of what the mod does |
| `categories` | string[] | Any authored category names. Trim/empty removal and case-insensitive deduplication preserve the first spelling and order; the first is primary. Empty/omitted means `other`. No client count or length limit. |
| `sources` | string[] | Glob-style paths to C# source files (e.g. `"src/"`, `"scripts/*.cs"`). Collected and compiled via Roslyn by `ModCompiler` |
| `gd_sources` | string[] | Glob-style paths to GDScript files (e.g. `"scripts/*.gd"`, `"gd/**/*.gd"`). `ModGDScriptLoader` collects them, compiles via Godot's GDScript runtime, and registers them as scenario actions or mod initializers |
| `test_sources` | string[] | Test-only C# globs, compiled separately in explicit mod-test mode |
| `test_suites` | array | Opt-in suites with `id`, optional `path` and activation prefixes |
| `integrations` | array | Conditional virtual mods: `id`, `with` partner IDs, optional `path`; see [Integrations](../src/integrations.md) |
| `fixture` | bool | Test fixture excluded from ordinary game/all-mod selection |
| `depends` | string[] | Versioned dependencies in format `modId[op][version][?]` — see below. Parsed into `ModDependency` entries via `ModDependencyListConverter`; affects topological compile order and version validation |
| `depends_native` | string[] | Glob patterns for native shared libraries handled by `ModNativeLoader` — see below |
| `conflicts_with` | string[] | Mutually exclusive mod IDs. Declarative only — see section above |
| `ui` | array | GUI declarations. Each entry is a toolbar-toggled panel (`"kind": "panel"`) or a code-driven overlay (`"kind": "overlay"`) anchored to a named shell slot (`slot`), with an optional `scene` field scoping it to one scene |
| `main_menu` | array | Buttons for the title screen; each entry opens a fullscreen mod scene |

### `depends` — version syntax

Each `depends` entry is a string `[modId][op][version][?]`, parsed by `ModDependency.Parse` (`Core/Mods/ModDependency.cs`):

```jsonc
{
    "depends": [
        "cthangover_game",           // bare id — any version
        "cthangover_game>=0.1.0",        // core 1 or newer
        "my_lib>=0.1.0",   // version starting with segments 1.2.0
        "extra_icons>=0.1.0",
        "soft_dep?"       // trailing ? — optional dependency
    ]
}
```

| Operator | Meaning |
|---|---|
| *(none)* | Any version — bare `"modId"` |
| `>=` / `<=` / `>` / `<` | Per-segment comparison against the dependency's actual version |
| `=` | Exact match (segment-wise) |
| `^` | Prefix match — required segments must equal the leading segments of the actual version |

- A trailing `?` marks an **optional** dependency: if the mod is missing, that is not an error; if present, its version is still checked.
- Comparison goes through `VersionHelper.CompareSegments` (`Core/Utils/VersionHelper.cs`): both version strings are split on `.`, both segment arrays are padded with `"0"` to equal length, and each segment compares numerically if both parse as int, otherwise via ordinal string comparison. Segments may contain letters (`1a`, `alpha-3`, `1b.2.0`).
- Entries are read from JSON by `ModDependencyListConverter`: malformed strings are logged as errors, kept with `IsValid = false`, and reported later during `ValidateDependencies`.
- Valid dependency IDs also drive load order: `ModRegistry.GetOrderedModIds` topologically sorts so dependencies always precede their dependents.

### `depends_native` — native libraries

`depends_native` lists glob patterns (e.g. `"bin/*.dll"`, `"native/*.so"`, `"lib/*.dylib"`) for native shared libraries shipped inside the mod. `ModNativeLoader.LoadNativeDlls` (`Core/Mods/ModNativeLoader.cs`), called from `ModRegistry.Initialize()` before managed mod code runs:

1. Collects platform-appropriate files (`.dll` on Windows, `.so` on Linux, `.dylib` on macOS)
2. Copies them to the cache directory (`user://mod_cache/native/`) so zip-based mods can serve native code
3. Registers a `DllImportResolver` so P/Invoke (`[DllImport]`) declarations resolve against the mod-provided native code

If multiple mods provide the same library name, the first one wins; duplicates are logged as warnings. On reload, `ModRegistry.Reload()` calls `ModNativeLoader.FreeAll()` to release all native handles so the files can be overwritten on the next load cycle.

### Precompiled DLLs (`dll/` directory)

A mod may ship precompiled .NET assemblies in a `dll/` directory. During `ModRegistry.Initialize()`, `ModAssemblyLoader.LoadPrecompiledDlls` (`Core/Mods/ModAssemblyLoader.cs`) scans that directory recursively, copies each `.dll` to the assembly cache (`{modId}_dll_{fileName}`), loads it, and runs the standard `RegisterAssembly` fan-out (the same registries listed on the [C# Source Code page](../src/)).

This runs **before** Roslyn compilation of `sources`, so source-based mods can reference types from precompiled API assemblies. Each loaded precompiled assembly also gets a `DllImportResolver` installed (`ModNativeLoader.InstallResolverOnAssembly`) for its own P/Invoke calls.

## Category names / Названия категорий

```json
"categories": ["RPG", "Weap ons! 世界", "Новая категория"]
```

Every string is a category. A previously unseen name creates a new category; there is no vocabulary or code-format validation.
The client trims surrounding whitespace, ignores empty names and removes duplicates using `OrdinalIgnoreCase`, retaining the first spelling and order.
It preserves the author's case and places no limit on category length or count. `RPG` and `rpg` are one category in filters, counts and groups.
The first category is primary; an omitted or empty list displays as `other`. The singular field has no alias.

The filter lists **all categories present in installed mods and every catalog**, with counts. It sorts by the displayed name
(case-insensitive ordinal order), keeping `other` last. Alphabetical order stays stable when counts change.
The sidebar scrolls; no categories are collapsed. Base categories are translated suggestions, not automatically added filters.
Other facets can reduce a category's count to zero without hiding a name still present in the data.

The shared editor offers free text, completion from all known and base categories, chips and a primary selector.
There is no client category count/length limit. Use **Add** or Enter to add the typed name.
In a development folder (`mods/` or `mods_private/`), **Save categories** writes the manifest without publishing.
Publication and folder export save it before packaging. Installed packages and archives show metadata read-only because updates replace their contents.

Ready translations use `mod_category/<name>` in `ui_base`. A mod can add, for example,
`mod_category/rpg = Role-playing gear` in English and `mod_category/rpg = Снаряжение RPG` in Russian.
Translation lookup also tries a lowercase key, while storage and untranslated display preserve the first authored spelling.
Without a translation, `Weap ons! 世界` is shown exactly as written.

A non-array field or a non-string element is a **JSON type error**: a manifest is rejected, and a catalog skips only that erroneous record.
The keyserver only bounds untrusted input: a JSON array of at most 32 strings,
at most 64 Unicode characters per string, without control characters. These are server input limits, not category-name rules.

Любая строка — категория. Ранее не встречавшееся название просто создаёт новую категорию; проверки формата и «неизвестных» категорий нет.
Клиент обрезает пробелы по краям, игнорирует пустые строки и убирает дубли через `OrdinalIgnoreCase`, сохраняя первое написание и порядок.
Регистр автора сохраняется; клиент не ограничивает длину и количество. `RPG` и `rpg` дают один фильтр, счётчик и группу.
Первая категория — основная; пустой или отсутствующий массив означает `other`. Старое одиночное поле не читается.

Фильтр показывает все категории установленных модов и всех каталогов, с количеством модов. Порядок — по отображаемому названию
без учёта регистра, `other` последняя. Такой алфавитный порядок остаётся стабильным при изменении счётчиков.
Список прокручивается, сворачивания нет. Базовые категории служат только готовыми переводами и подсказками.
Другие фильтры могут обнулить счётчик, но не скрывают категорию, пока она есть в данных.

Редактор позволяет ввести любое название, выбрать подсказку, убрать фишку и назначить основную категорию. Нажмите **Добавить** или Enter.
Кнопка **Сохранить категории** в карточке правит папки разработки `mods/` и `mods_private/` без публикации.
Публикация и экспорт папки сохраняют манифест перед упаковкой; установленные пакеты и архивы доступны для чтения.
Перевод задаётся ключом `mod_category/<название>` в локали мода; также пробуется ключ в нижнем регистре, но хранимое название не меняется.
Без перевода показывается первое авторское написание.

Не массив строк — ошибка типа JSON: манифест отклоняется, в каталоге пропускается только эта запись.
На keyserver действуют лишь технические пределы недоверенного ввода:
до 32 строк, до 64 символов Unicode в строке, без управляющих символов.
