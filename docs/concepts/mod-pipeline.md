# Mod pipeline (`Core/Mods`: Pipeline, Discovery, Providers, Compilation, Consumers, Unload, HotReload)

The core is a mod player. Everything the game does is compiled from the mod folders at startup, and this
pipeline turns folders and zips into loaded, registered assemblies plus readable data. Later it
replaces that set inside the running process (game switch, hot reload).

## Boot: three phases with a thread contract

`ModBootPipeline` (the `ModRegistry` facade delegates to it):

| Phase | Thread | Does |
|---|---|---|
| `PreparePaths` | main (Godot API allowed) | discovery registry (scanner/loader reflection), scanner roots via `GlobalizePath`, cache roots, warm-up of every store that needs `user://` paths |
| `DiscoverAndCompile` | background during launcher boot (pure .NET) | scan roots → providers → manifests → dedupe → mount test suites → filter (disabled, active game, dependency validation) → native libs → precompiled `dll/` → Roslyn compile in dependency order |
| `FinalizeGodotInit` | main | `RegisterAssembly` fan-out, `ScriptTypeRegistry`, GDScript mods, load order, `InitializedFlag` (volatile, published last) |

`Boot.RunSync` runs the phases synchronously for tests and tools. The launcher drives them one at a time
and keeps the boot window closed until `Boot.ModsReady`: any outside `Initialize` inside the window
is refused with an error naming the boot-order bug.

## Discovery and data access

- **Roots.** Two scanners: game-directory folders from `ModRootFolders.Names` (`mods/` and optional
  `mods_private/`, both priority 1 in runtime or 0 outside Godot) and `user://mods/` (priority 0).
  The lower value wins, so a user copy overrides the game's in runtime. At equal priority only a
  strictly newer version replaces; ties retain the first discovered copy. Every losing provider is
  disposed. Optional game-directory roots are returned only when present, so hot reload does not
  create absent private folders. `mods/` remains the public publishing source; release/sync/site
  scripts deliberately do not scan private roots.
- **Paths and writes.** Existing mods are addressed through `IModFileProvider.RootPath`, including
  editors, documentation and export. `ModRootFolders.Contains`/`StripModPrefix` recognize editor
  project paths from either folder. `ModRoots.ModsPath` is the primary write target for new mods;
  downloads and studio exports use the user root in runtime. Adding a game-directory folder also
  requires the runner's `ModFolders`, csproj exclusions/content entries, `.gdignore` and (for private
  content) `.gitignore`. Per-folder `tests.manifest.json` parts add integration/manual suite entries
  to the runner; see `tests/Tests.md`.
- **Providers.** `IModFileProvider` has three storage implementations: `FolderModFileProvider`,
  `ZipModFileProvider` and `SevenZipModFileProvider`. They use one canonical path form (`ModPath`: forward slashes, no escape from
  the root) and one case policy (`ModPathCase`): a wrong-case reference is a miss on every OS, and it
  is logged once as `path case mismatch`.
- **Category metadata.** `categories` is an ordered array of arbitrary authored strings. Trim, empty removal and `OrdinalIgnoreCase` deduplication preserve first spelling; the first is primary and an empty list displays as `other`. There is no client format/length/count limit or unknown-name warning. A non-array/non-string element is a manifest JSON type error. `CatalogEntryListConverter` skips just the erroneous catalog record, keeping the response loadable.
  Saving unchanged categories preserves the manifest's bytes and modification time, so opening a publish/export form does not change package checksums. Explicit spelling or order changes still save normally.
  Русский: неизменные категории не перезаписывают манифест и его время; форма публикации/экспорта сохраняет контрольные суммы пакета. Изменения написания и порядка записываются.
- **Manifest problems.** A `manifest.json` that is empty, `null`, not JSON is
  not a mod: discovery logs `Skipping '<folder>': invalid manifest.json: <reason>` (`no manifest found`
  when the file is absent). `ModDiscoveryService.ReadManifest` returns the reason
  (`ModDiscoveryServiceTests`).
- **Hidden entries.** Names starting with `.` are not mod content (`ModContentFiles`). Listings, the
  install hash and catalog export all skip them, because every mod folder is its own git repository.
- **Install hash.** `ModInfo.InstallSha256` is computed lazily on first read, through
  `InstallHashCache` (a stamp of path, size and mtime). Only the mod manager's update check and the
  validator report need it.

## Filtering

`ModSetFilter`:
1. Disabled mods are removed, together with mods depending on them (cascading). Mods named in
   `--with-mods` and their required dependencies are exempt for that run (`DisabledFor`); the stored list stays.
2. The set is reduced to the active game (`GameModSet`: collection plus frontend plus
   `--with-mods` plus mounted test suites). With `--all-mods`, test fixtures are removed unless named.
3. `ValidateDependencies` removes mods with malformed, missing or version-mismatched required
   dependencies. The pass repeats until nothing more is removed, so dependents of a removed mod go too.

`ModDependency` syntax: `id`, `id>=0.1.0`, `<=`, `>`, `<`, `=` (exact, zero-padded), `^1.2` (prefix
match), and a trailing `?` for optional.

## Compilation

- `ModCompiler.CompileAll` compiles in dependency order. A mod is compiled against its **dependency
  closure** only (`DependencyClosure`), so an undeclared C# dependency is a compile error.
- Godot source generators run through `GodotGeneratorHost` when available. Without them, mod
  lifecycle callbacks silently never fire, and a `MODS_COMPILE` warning says so.
- Runtime mod assemblies use Release optimization when no managed debugger is attached.
  Attaching a C# debugger before compilation selects Debug; `--wait-for-debugger` offers that
  choice before boot. Portable PDBs and real source documents are emitted in both modes.
  Optimization joins the cache identity, so a Debug artifact cannot satisfy a Release request
  or vice versa. Attaching after a mod has loaded leaves that assembly as compiled; reload it
  or restart with the debugger attached to inspect unoptimized locals. Ad-hoc `CompileString`
  and mod test assemblies retain their existing Debug policy. This choice is pure .NET and
  does not query Godot from the background compilation phase.
- English: installed user folders and ZIP/7z packages have no project C# resource path.
  Their compilation keeps Godot's method, property and signal generators, but omits
  `ScriptPathAttributeGenerator`. Godot creates a managed script bridge directly from
  the type, while `ScriptTypeRegistry` resolves prefab references. Loose project mods
  retain real `res://` bindings. The cache signature includes this binding location,
  so identical source bytes moved from a project folder to a user installation cannot
  reuse an assembly containing obsolete filesystem paths. PDBs still use the selected
  provider's real source directory when it exists.
- Русский: пользовательские папки и архивы ZIP/7z не имеют проектного пути C#-ресурса.
  Генераторы методов, свойств и сигналов сохраняются; только генератор `ScriptPath`
  отключается. Godot связывает тип непосредственно, а ссылки префабов разрешает
  `ScriptTypeRegistry`. У модов внутри проекта остаются настоящие `res://`-привязки.
  Место привязки входит в ключ кэша; перенос тех же исходников в пользовательскую
  установку не переиспользует DLL с устаревшими путями. PDB хранит фактический путь
  выбранной папки исходников, если она существует.
- The cache file is `user://mod_cache/assemblies/<id>_<hash>.dll` plus its PDB. The hash covers:
  - the sources;
  - the dependency signature: each dependency's id, version and current artifact name, so a
    dependency change propagates transitively;
  - the core assembly MVID;
  - the codegen, PDB and refs markers;
  - net10.0 and the version/content hash of the shipped SDK reference pack.

  A DLL without its PDB counts as stale.
- Mod-owned precompiled libraries use content-addressed cache names too. `PrecompiledAssemblyCache`
  holds the same per-key process lease, verifies existing bytes, and publishes a flushed temporary
  file with atomic rename. No publisher overwrites a library another context may have mapped.
- Assemblies load from disk into one collectible `ModLoadContext` per mod. Resolution order: own
  assemblies → the dependencies' contexts (so inter-mod types are reference-equal) → kernel/GodotSharp.

## CLR requirement / Требование CLR

Compilation and execution use SDK 10/net10.0. The SDK reference pack is shipped
beside the core (`mod_reference_assemblies/net10.0`), including exports and xUnit
outputs. `FrameworkReferencePack` keeps framework implementation DLLs out of
Roslyn references; the pack version/hash invalidates production and test caches.
The precompiled namespace also records net10.0. Godot byte-loads the development
core, so the physical pack directory is resolved through the main-thread warmed
build output when Assembly.Location is empty. Players do not need an SDK.
CLR 10 is required. CLR 8.0.32–8.0.34 can
leave a deleted DomainAssembly in the domain list when related collectible
contexts unload (dotnet/runtime#116953, fixed upstream by #119196 in .NET 10).
Do not retain dependency assemblies on unloading contexts: cross-context generic
instantiations can create reverse allocator edges and turn that workaround into
permanent roots. The normal unload protocol remains unchanged. The runtime is
selected before Godot starts; boot checks it, and self-contained exports ship the
servicing runtime pinned only for exports in Cthangover.csproj.

Компиляция и выполнение — SDK 10/net10.0. Reference pack поставляется с ядром
и экспортом; SDK игрокам не нужен. Ключи кэшей включают target/version/hash pack.
При byte-load ядра в Godot каталог берётся из заранее разрешённого физического
build output. Удержание зависимостей в
выгружаемом контексте недопустимо: обратные связи LoaderAllocator у cross-context
generic приводят к утечкам. Исправляется runtime, протокол выгрузки сохраняется.
Релиз содержит CLR 10; системный Godot запускается через run_godot.* или TestRunner.

## Registration fan-out

`ModAssemblyLoader.RegisterAssembly` is the single entry point. `AssemblyConsumerRegistry` holds the
ordered `IAssemblyConsumer`s:
- initializers;
- scenario actions;
- scene events;
- condition cache;
- section parsers;
- tool factories;
- tscn build consumers;
- typed `ModTypeConsumer` registries.

Mods may add consumers, but a mod-provided one never shadows a kernel one. One consumer's failure is
logged and the others still run. `ModInitializerRegistry` does a two-way catch-up: an initializer that
arrives late still gets `OnModLoaded` for every earlier mod.

## In-process replacement

English: menu warm-up tasks remain tracked after cancellation until their worker exits.
Leaving the title screen or entering gameplay cancels them; shutdown joins all outstanding
workers before disposing engine state. Starting/loading gameplay also joins the cancelled
workers before changing game/save state. Cancellation sources are released after task completion.
The native-wrapper GC limitation is documented in
[application shutdown](ui-and-shell.md#application-shutdown).

Русский: отменённые задачи прогрева меню остаются под учётом до завершения рабочих потоков.
Выход из меню или вход в игру отменяет прогрев, а завершение приложения ожидает все такие
задачи до освобождения состояния движка. Источники отмены освобождаются после завершения задач.
Вход в новую или сохранённую игру тоже дожидается отменённых задач до изменения состояния игры.
Ограничение GC/native-объектов описано в [завершении приложения](ui-and-shell.md#application-shutdown).

- `ModSetReloader.Prepare` (background) discovers, filters and compiles the target set without touching
  the running one; a compile error simply discards it.
- `Apply` (main thread) computes a `ModReloadPlan` from code identities (artifact paths). Mods that
  leave or changed unload, together with their dependents, cascading. Kept mods keep their
  contexts, statics and nodes.
- The unload protocol (`ModUnloader`):
  1. Reverse consumer fan-out.
  2. Nodes of mod types still waiting in `ModRoot.AddDeferred` are dropped; Godot objects of mod types
     are freed and signals disconnected.
  3. Event bus subscriptions are removed by code ownership. The new-game `IResettable` owners of the
     dying assemblies are dropped by `AppServices.RemoveOwnedResettables`, so a new game never calls
     unloaded code; a reloaded mod registers its owner again from its new assembly, and that replaces the
     old entry of the same type (`ResettableUnloadTests`, itest `resettables`).
  4. Static reference sweep and System.Text.Json cache reset. The sweep is the fallback: a line
     `... AppServices.resettables: removed N item(s)` in the `MODS_UNLOAD` report names an owner that did not
     unregister itself.
  5. Each context unloads.

  `ModUnloadTicket` later verifies the collection, and any survivor is a leak to fix.
- Initializers add root nodes with `ModRoot.AddDeferred(node)`, never `root.CallDeferred("add_child", node)`:
  a synchronous `ModManager.Reload()` can unload the mod before a raw deferred call runs, the sweep frees the
  waiting node and Godot gets a dead object (`rp_child is null`, heap corruption at exit on Windows).
  `ModRoot` skips nodes that are gone, already parented or whose name the root already has.
- `ModHotReloadService` watches the mod roots (editor runs only; automated runs and the TestRunner
  switch it off, and `--hot-reload` forces it on). It debounces 0.6 s and reloads; `Ctrl+Shift+R`
  reloads by hand.

## Integrations (`"integrations"`)

A mod can ship integrations that work only together with partner mods: the folder
`mods/<host>/integrations/<id>/` (its `manifest.json` with `sources`; its own `depends` may be left out) and,
in the host manifest, `"integrations": [{"id": "quests", "with": ["quests_base"]}]`.

- The integration is the virtual mod `<host>__<id>`. It depends on the host and on every `with` partner, and it
  mounts only when all partners are in the set (`ModIntegrations.IsWanted`, `ModIntegrations.Mount`). Mounting
  happens after the set filter (`ModBootPipeline` and `ModSetReloader`), before dependency validation. Without a
  partner nothing is mounted, so the host must not need the integration's types.
- It is not in `AllDiscoveredMods` (no card, no toggle) and not in the game's `GameModSet`. The host's card shows
  "Works together with" (`ModQueries.GetIntegrations`).
- Tests: `ModIntegrationsTests`; test suites `integrations` (every partner present: mounted) and
  `integrations_frontend` (partner absent: not mounted), both on the load path and in `ModSetReloader`.

## Tests

xUnit (`tests/Cthangover.Tests/Mods`) covers compiler, cache, load order, dependency parsing, filter,
reload plan, providers, path policy, manifests, integrations and unload (`tests/Cthangover.Tests/Boot/ResettableUnloadTests`).
The integration tests `mod`, `hot_reload`, `mod_units`, `resettables` (itest) and every suite that boots,
cover the rest.

## Fixed in the 2026-10-21 review

- `ValidateDependencies` was a single pass. A mod depending on a mod dropped in the same pass stayed
  in the set with its required dependency gone.
- `ModSetFilter`'s translation guard called `TranslationServer` outside the Godot runtime, which crashed
  the xUnit host on any failure path.
- Boot speed on slow file systems: lazy install hash, case-verified listings, pruning of hidden
  entries, hot reload off in test runs. The full test run went from 61 min to 12 min.

## ZIP/7z mounting, validation and passwords (2026-10-31)

`ZipModFileProvider` and `SevenZipModFileProvider` share the managed `ArchiveModFileProvider`.
PKWare/WinZip AES ZIP and AES/header-encrypted 7z work on Windows and Linux without native 7-Zip.
Independent streams own their decoders. Bulk hashing uses one metadata/decode pass: ZIP entries
are visited directly; 7z uses its sequential reader so solid blocks are not restarted per file.
Explicit buffered reads share a 16 MiB process-wide cache (at most 4 MiB per file). Returned arrays
are private copies; cache entries are released on provider disposal. OpenStream cache misses decode
without buffering an entire entry. Non-solid packages remain preferable for random resource access.

Discovery retains unreadable packages by absolute path. The manager shows their filenames even when
headers hide the manifest, and offers **Open package**. The dialog is linked from the manager's
`.tscn` via an exported PackedScene. It selects the clicked package, masks the password, checks
manifest and encrypted content off the UI thread, and rediscoveries/applies the current mod selection
through the existing reload pipeline. A valid package becomes visible without restarting. Incorrect
passwords leave the running set unchanged. Validation cancellation and abandoned preparations
release their providers. Encrypted corruption can be indistinguishable from an incorrect password.

Passwords are session-only by default. **Remember password on this device** explicitly opts into
plaintext `user://archive_passwords.json`; Unix files are created with mode 0600 and Windows inherits
the user directory ACL. Nothing logs the password. **Forget** removes session/persisted credentials;
already opened streams and providers remain usable until unload. Programmatic callers can pass
passwords directly to providers or `ModPackageInstaller.Install` without persistence.

`ModFileAccess.OpenSeekable` returns an existing seekable stream or copies a single entry with a
bounded RAM budget and a private temporary-file spill deleted on disposal. `Materialize` streams
one file into an atomic, content-compared disk-cache update; shader caching uses it instead of
buffering the entire file. Plain OpenStream still promises readability, not seekability.

`ModPackageInstaller` recognizes ZIP/7z by content, checks supplied archive SHA256, extracts into a
unique sibling staging directory, validates root/wrapper manifest and id, and checks supplied install
SHA256 before replacing anything. Traversal, links and case-ambiguous files are refused. The previous
directory is renamed to a backup and restored if publication fails. This is rollback on runtime
errors, not a crash-atomic directory exchange: a power-loss recovery journal remains future work.
The catalog downloader uses this same installer. Content hashing streams bounded chunks and preserves
the existing digest format, so published install_sha256 values remain comparable.

The hub's **Catalogs (N)** button opens the scene-backed `CatalogServersDialog`.
`CatalogServerStore` seeds the editable list from shipped config until the first
player change, then `user://catalog_servers.json` becomes authoritative, including
an empty list. Disabled entries stay editable but do not enter the live URL list.
Boot applies that override in `ModConfig.CreateInstance`; the UI applies it and
refreshes catalogs immediately after saving. Keys pasted into the masked field
are kept in separate owner-only files under `user://catalog_keys/`; the list
contains paths only. Replacement and removal delete only store-owned files,
never borrowed token files from shipped config. An endpoint change drops the
old key unless the player explicitly enters it again. Duplicate URLs and unsafe
private transports are rejected before publication.

The `catalog_servers` runtime suite enters the hub through the real main-menu
button, edits sources through the form, verifies counters, enables/disables a
source, replaces its key, installs a real package, and confirms source deletion
without deleting that package. It restores the original source list and cache.
The sibling keyserver harness runs it with `--suite catalog_servers --windowed`;
raw UI captures live in `../art_sources/cthangover/mod_manager/documentation/`.

Private catalogs use `ModConfig.CatalogTokenFiles` (`catalog_token_files` in
`config/mod_config.json`): an exact catalog URL maps to a separate token file.
`CatalogRequest` reads and validates the bearer for each request; the private
HTTP client disables redirects and accepts HTTPS or literal loopback HTTP only.
The downloader never receives the bearer and does not log temporary URLs.
`CatalogService` persists only public source data before merging it with private
entries in memory. A successful empty response removes old private entries;
transport failures are reported as refresh errors. Private installations refresh
the catalog first to replace expired links and recheck access. Installed copies
are independent of later catalog permissions.

The explicit `--frontend --itest=private_catalog` suite requires a real local
keyserver and a public fixture endpoint. The sibling project's
`tests/integration_windows.py` starts both and supplies their addresses through
environment variables. It drives the manager's install button, checks installed
bytes, denied access, public-source independence, disk-cache isolation and logs,
then removes its probe and restores the player's cache and runtime configuration.

Folder export always prepares a fresh ZIP and obtains both checksums from that ZIP. JSON is prepared
before publication; archive/catalog publication restores previous files on runtime failure. Each file
replacement is atomic, but publishing two fixed URLs is not a single cross-file transaction. Web
release publication should use immutable artifact URLs before changing a live catalog pointer.

Tests cover installation failure preserving previous bytes, repeated export, legacy digest compatibility,
stream-only providers, memory/disk seekable access, materialization, solid reads, encrypted ZIP/7z,
session-only and opt-in persistence, and discovery after unlock. The `archive_password` runtime suite
exercises a real locked card, bad and good passwords, live rediscovery, unchanged credential files and
closing. It uses no forced GC. Fixture generation alone uses the official external 7-Zip tool.

## Archive modes roadmap (2026-10-31)

Current delivery supports managed ZIP/7z mounting and verified extraction, encrypted ZIP/7z and
session passwords with live unlock. `ModCatalogExporter` still writes ZIP; collection packages
dispatch folders/ZIP directly. A new mod loader does not automatically
extend these paths. Separate container/compression/encryption from resource access policy:

| Access policy | ZIP | Non-solid 7z | Solid 7z | Resource contract |
|---|---|---|---|---|
| Stream (current mount) | Independent entry reads | Independent entry reads | May repeatedly decode preceding block data | Readable stream, seek not guaranteed; no extracted paths |
| Memory | Bounded LRU of requested entries | Same | Retain selected entries or bounded decoded blocks | Seekable memory streams; per-file and total limits |
| Entry disk cache | Materialize requested files | Same | Reuse sequential block decoding | Seekable file streams and real resource paths |
| Fully extracted install | Stage, verify, publish a directory | Same | Prefer sequential extraction | Folder provider; Godot/native filesystem resources work |
| Auto | Choose by consumer needs and resource size | Same | Account for solid blocks and reuse | Deterministic selection within RAM/disk budgets |

Implementation order:

1. **Capabilities and contracts.** Add archive open options: policy, memory/disk limits and
   cancellation, preserving the default stream behavior. Expose seekability, materialization,
   encryption and solid-block information. Separate opening a readable stream from requesting a
   seekable stream or a cached file. Audit consumers: JSON/textures/source compilation accept bytes;
   some scenes, shaders, media and native libraries need real paths. Share these contracts between
   discovery, the manager and collections.
2. **Shared backend and encrypted ZIP.** Preserve canonical paths, exact-case lookup and shallow
   listings across ZIP/7z. Add ZIP PKWare and WinZip AES through the managed backend, with regression
   tests for current unencrypted ZIP. Detect formats by signature because downloaded URLs and temp
   files may have no extension. Recognize header encryption before requesting a manifest. Represent
   locked packages explicitly in the UI with retry. Distinguish cancellation, invalid format and
   unreadable encrypted data; 7z cannot always distinguish corruption from a wrong password.
3. **Bounded caches and materialization.** Implement memory and entry disk caches before full
   extraction. Key by archive content/version, normalized entry path and credential context without
   plaintext passwords in keys/logs. Coalesce concurrent reads. Reuse sequential solid-block decoding
   under controlled ownership. Enforce budgets even for large blocks. Publish cache files atomically,
   verify complete-read integrity, prevent traversal/symlink writes and limit decompressed output.
   Specify eviction, unload, credential-forget cleanup and decrypted-file permissions. Test Windows
   open-file behavior separately from Linux unlink behavior.
4. **Install, export and catalogs.** Declare ZIP/7z and retain-archive/extract policy in catalog
   metadata. Stage downloads, verify archive SHA256, obtain credentials, validate content and install
   hash, then atomically publish. Flatten wrapper directories consistently. Extend collection
   discovery and overwrite/delete behavior to 7z. Add export presets: ZIP Store/Deflate and 7z LZMA2
   non-solid/solid, plus passwords/encrypted names where the selected writer supports them. Verify
   writer encryption capabilities before promising encrypted export; read support is insufficient.
5. **Controls and credentials.** Offer clear user choices: read from archive, cache files, extract on
   install, with disk/RAM estimates. Support session-only passwords and opt-in persistence via an OS
   credential-store abstraction; any local plaintext fallback must remain explicit. Password prompts
   must work before mod compilation and in headless tools. Propagate cancellation through downloads,
   KDF/decode, cache population and UI shutdown.
6. **Cross-platform acceptance and performance.** Run identical fixtures on Windows and Linux. Cover
   ZIP Store/Deflate/Zip64/PKWare/AES and 7z LZMA/LZMA2/solid/header AES in every access policy. Include
   large files, many small files, Unicode/case, partial/concurrent reads, truncation/corruption,
   missing volumes, cancellation and archive replacement. Measure first-resource/repeated-read
   latency, boot time, peak RAM and disk use; select Auto thresholds from those results. Test actual
   filesystem-only Godot resources and unloading; byte-read tests do not establish that compatibility.

Acceptance gates: existing stream packages keep working; seekability is promised only when provided;
limits/cancellation are enforced; secrets and decrypted content do not leak to logs/catalogs; both
OSes pass the archive matrix before changing defaults. Network transport is separate: the indexed
provider currently needs a seekable local archive, so forward-only downloads are staged before
mounting, especially for 7z.

## Author publishing to private catalogs (2026-11-04)

The installed mod's detail action opens the scene-backed `ModPublishDialog`.
`CatalogPublisher` lives in `mods/mod_manager`, reuses `CatalogRequest` for the
exact private endpoint and disables redirects. Permissions are fetched from
`GET /v1/publishing`; no identity is inferred from local manifest credits.
The offline issuer's optional `author_id` makes a key writable. A missing claim
preserves legacy read-only access. Reissued keys must retain this identity.

Folder mods are exported into a private temporary ZIP without hidden files.
Catalog metadata is read from the packaged manifest, so a manifest edited while
the game runs does not publish a stale version. Existing ZIP/7z packages retain
their format and encryption; their snapshot hash is checked before upload.
Multipart upload streams `section`, catalog `entry`, then `archive` to
`POST /v1/publish`, rereading the token. The server sets verified author metadata,
computes the archive checksum, and commits ownership, immutable file registration,
release history and catalog changes under one SQLite write lock after rechecking
revocation and expiry. The permanent owner is independent of the particular token
or master key; foreign IDs are reserved across all sections. Old imported IDs need
a local administrator's `assign-author` command before author updates are allowed.

The sibling `cthangover-keyserver/tests/integration_windows.py --suite publishing`
uses temporary keys and a database. It checks the real detail action and publishing
form, folder upload, downloaded bytes, both hashes, hidden-file exclusion, release
history and an update with another token for the same author. Its manual-suite
entry records the required external HTTP fixture. Permissions DTOs have a mod-unit
JSON roundtrip test. A windowed run captures the form via `ViewportCapture` without
changing HDR, renderer or display settings.
