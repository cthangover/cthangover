# Runtime 3D assets / Импорт 3D в рантайме

## English

`mods_private/asset3d_base` owns raw model import; `world3d_base` delegates raw
loading and retains semantic profiles, objects and placements. Simple TSCN
assemblies still use the scene resolver. No kernel changes, Godot editor import
files, player-side Blender or archive extraction are required.

### Recipes, options and API

`model3d_asset` is an ordinary entity: Id, optional Extends, Source (a key in the
models3d file group), Options. Existing inheritance, patches and file overrides
apply. A raw model without a recipe loads with default options. Dependencies
stay in the selected source provider, including folder/ZIP/7z/overlay providers.

```json
{
  "Kind": "model3d_asset",
  "Items": [{
    "Id": "example/crate",
    "Source": "example/crate.glb",
    "Options": {
      "Units": 1, "Scale": 1, "Up": "Y", "Forward": "-Z",
      "Pivot": "floor", "PivotOffset": [0, 0, 0],
      "AlbedoColorSpace": "srgb", "NormalColorSpace": "linear",
      "OrmColorSpace": "linear", "NormalConvention": "gl",
      "Quality": "high", "MaxTextureSize": 2048,
      "GenerateLods": true, "ShadowMesh": true, "Collision": "box"
    }
  }]
}
```

Start `Assets3d.Resolver.Load(key)` or `LoadAsync(key, cancellation)` on the Godot
main thread. Provider overloads accept a provider, path, options and optional
limit snapshot. The result owns a detached Root; keep Model3dLoadResult while
it is displayed, then Dispose it. TakeRoot transfers ownership to an existing
host that already frees its own model. Key, CacheHit and structured Diagnostics
are exposed for tools. Import authoring is provided by world3d_editor (below).

Native GLTF/FBX metadata is normalized by the native parser first. Explicit
Units/Scale and Up/Forward override that native output (default metres,
Y-up/-Z-forward), without adding a hierarchy node that would break profile
paths. Pivot modes are origin/center/floor; PivotOffset is added in output
coordinates. Profile and placement transforms remain separate layers.

### Import editor (world3d_editor)

Open **3D model import** from the tools toolbar, or the import button in the
model-profile editor. Pick the writable destination mod, choose New or an
existing recipe (including dependency entries), then choose a Source from the
provider-backed models3d catalogue. The picker uses the currently registered
importers' extensions, including plugins; it does not extract archives or open
arbitrary operating-system paths. Refresh updates the source list after exports.
Saved recipes also appear in the profile editor's model picker.

The Import options tab presents effective units, axes, scale, pivot/Vector3dField,
per-slot colour spaces, GL/DX normals, quality/maximum texture size, LOD, shadow
mesh and collision. Only the option actually edited is written. A child recipe
keeps its untouched options inherited. Native metadata conversion precedes these
overrides; DDS/KTX policies are still the runtime policies described below.
Unknown recipe, option and clip properties survive DTO normalization through
JsonExtensionData, as well as the document's JSON-preserving edits.

Animation slices lists actual source animations with durations. New slice and
Apply write Source/Name/Fps/FirstFrame/LastFrame/Loop; selecting a row allows
replacing it or removing it. Empty Clips keeps all source animations. Clip
extensions and untouched rows survive edits. Runtime range and duplicate-name
errors remain visible as structured diagnostics, rather than engine log errors.

Before / after reuses Model3dPreview's orbit/framing in two isolated viewports.
Before means default runtime options **after native metadata conversion**, not
unconverted author-file coordinates. Each view frames its own mesh bounds;
compare the displayed dimensions/minimum in metres to assess scale/pivot. Yellow
lines show bounds, RGB lines X/Y/Z and cyan wires actual collision geometry.
Inspection renders a duplicate node tree without physics bodies; the owned
import result retains its collision data for inspection. Mesh/material resources
remain shared and immutable. Diagnostics has severity/code, path and both ru/en
messages; a failed normalized draft clears the previous after view.

All authoring uses EntityDocument/JsonPatchDiff: foreign edits become minimal
patches in the destination mod, with undo/redo, Save, Reset to inherited and
reopen. Other fields retains raw JSON for extensions and malformed-draft repair.
Numbers displayed/written by the dedicated controls use InvariantCulture.
Preview refresh uses the existing synchronous import API: native parse/build can
block the editor; no asynchronous progress or interruptible native build is
claimed here.

Force reimport invalidates the completed before/after keys in memory and on
disk, clears hash hints and rebuilds without restarting or modifying the recipe.
It preserves other live instance trees. The owner must have no pending imports;
a busy owner or disk error is diagnosed. On closing, the editor releases all
packed-scene memory entries it used before viewport teardown, while retaining
the immutable disk entries and other live instances. This avoids retaining
preview collision handles until native server shutdown. The corresponding
public service points are SupportedExtensions, Invalidate and ReleaseMemory.

Collision shapes created by the importer transfer their native reference to the
node; the creator's CLR wrapper is disposed immediately. Debug contours read the
borrowed shape through scoped native Variants and a fixed Expression, returning
an owned mesh copy. They create no physics shapes or CLR wrappers for cached
Shape3D resources, and never dispose shared geometry. This avoids late finalizer
releases after the physics server's last synchronization. Godot's
[Shape3D destructor](https://github.com/godotengine/godot/blob/4.6/scene/resources/3d/shape_3d.cpp)
frees its RID; the
[physics wrapper](https://github.com/godotengine/godot/blob/4.6/servers/physics_3d/physics_server_3d_wrap_mt.cpp)
flushes off-thread commands during synchronization. No shutdown delay or GC policy
change is used. The collision lifetime contract checks all three shape modes,
two previews, shared data and memory-cache eviction with another instance alive.

### Import boundary and validation

ModTypeConsumer discovers IModel3dImporter implementations and removes them
symmetrically at unload. Content/magic scoring wins before extension fallback.
Read resolves dependencies and validates the snapshot; Parse returns an owned
Model3dPrepared; only main-thread Build creates nodes. Custom importers must
honor these contracts and explicitly declare worker parsing safety.

- GLB/GLTF embeds provider buffers/images before GltfDocument.AppendFromBuffer,
  preserving GLB binary chunks. Raw models never use ResourceLoader.
- Binary/ASCII FBX receives bounded syntax parsing before FbxDocument/ufbx:
  Video images are embedded, author filesystem paths sanitized, geometry counts
  checked. Network/traversal image URIs are rejected. Texture records without
  a resolvable Video are diagnosed; animation .tak names are only metadata.
- OBJ supports v/vt/vn, objects/groups, material splits, negative indices and
  triangle fans of 3–64-corner polygons. MTL supports diffuse/alpha,
  roughness/metallic and albedo/normal/ORM maps. Map switches, spaces in map
  filenames and free-form OBJ curves are unsupported. Unknown MTL fields get
  non-error diagnostics. OBJ does not carry rigs or animation clips.

URI decoding precedes validation. Absolute/network paths, selectors, control
characters and empty/dot/parent segments are forbidden, even if a parent path
would stay inside the mod. Use source-local subdirectories. FBX backslashes are
normalized before checking. Existing providers enforce exact-case lookup.

Defaults bound source input to 64 MiB, combined dependencies to 128 MiB/128
files, source vertices to one million, bones to 256, scene nodes to 4096,
clips to 256 and animation keys to one million. Supported image headers are
checked before native decoding: at most 8192 per axis and an RGBA+mip byte
estimate within the configured texture budget. Generated geometry is checked
again. Model3dImportException carries code, path and ru/en messages; expected
unsupported compression is a non-error diagnostic.

### Textures and derived resources

Authored DDS/KTX stays compressed as supplied. Colour/normal convention,
resolution and mip chains must be authored correctly; diagnostics explain that
processing overrides were not applied. BC1 DDS/KTX1 is verified, not every
KTX2/Basis, array or cubemap variant. Other supported image buffers
(PNG/JPG/WebP/TGA/BMP/SVG) become RGBA with slot-aware colour conversion,
GL/DX normal-green conversion, quality resizing and mipmaps (renormalized for
normals). Albedo/emission sRGB samplers and linear normal/ORM slots differ.
High/medium/low quality uses MaxTextureSize divided by 1/2/4, at least 16 pixels.

ITexture3dEncoder has a symmetric reflection registry with no shipped
implementation. The default rgba_mip_fallback never calls the unavailable
export S3TC encoder. Encoder identities join cache signatures. Mesh LOD generation is enabled by default (GenerateLods=true), with an explicit
false opt-out. It uses ImporterMesh; shadow meshes use explicit position/index surfaces;
collision modes are none/convex/trimesh/box. Imported method/audio/animation-call
tracks are forbidden. Clips selects a Source and slices FirstFrame..LastFrame
at Fps into an explicit Name/Loop, sampling boundary keys. Retargeting,
AnimationTree and gameplay event/root-motion authority are implemented by
`anim3d_base`; character maps/representation belong to `characters3d_base`, and
physical movement belongs to `actor3d_base`. Raw import does not execute them.
Import slices retain both boundary poses and have duration
`(LastFrame - FirstFrame) / Fps`; there is no extra frame of playback at the end.

### Workers, ownership and cache

A mod-owned pump reads/preflights on bounded workers and coalesces content keys
before parsing. Native GLB/FBX append/parse runs on workers only without custom
glTF extensions: their callback thread safety is unproven. Node generation,
material processing, native cache loading/saving and instantiation stay on the
main thread. At most one ready request builds per pump frame; a native
GenerateScene/Pack call is indivisible. The synchronous API drains pending work
without waiting on a main-thread continuation; it deliberately blocks existing
editor callers.

Waiter cancellation is independent. With no waiters, parsing is cooperatively
cancelled. Reload/shutdown cancels and joins workers before removing providers
or extensions. A running native append cannot be interrupted inside its call.

The cache key covers source/dependency hashes, canonical options, importer
ID/version/assembly identity, encoder/extension identities and Godot version.
Folder size/mtime hints avoid rehashing; archive bytes are hashed. Reload clears
hints. Native .scn plus integrity/diagnostics JSON lives under
user://mod_cache/assets3d/<owner>/<key>.scn. Unsafe owner IDs use hashed directory
names. Unique temporary files, per-key OS leases and atomic renames protect
publication. Valid keys are immutable; incomplete or hash-mismatched pairs are
rebuilt before native loading. Disk failures retain a usable memory result
with diagnostics.

Disk LRU uses explicit last-access times. At ResourcesReady, absent owners are
reconciled against installed discoveries, active virtual mods and overlays;
switching games keeps installed mods' disk cache. The memory cache owns native
PackedScene resources only, with LRU based on source/decoded/cache byte
estimates. Fresh instance nodes share immutable Mesh/Material/Texture/Animation
resources; callers must not mutate shared resources. Live instances keep
native references after eviction. Cached trees cannot contain mod scripts or
objects from collectible assemblies. Reload frees tracked instance roots and
cache references. Native GltfDocumentExtensions unregister and dispose before
their assembly leaves; importer/encoder consumers remove their instances.

Budgets use the existing mod settings UI: concurrency 1 (range 1–4), decoded
textures 256 MiB per request (16–1024), disk cache 512 MiB (16–4096). N imports
permit N times the validated texture estimate; geometry/compiler/renderer/live
instances are additional memory. Stage 1 exported peaks were ~688 MiB for the
ship and ~937 MiB for the rig, so serial import is conservative. Budget changes
apply cache eviction immediately; in-flight requests keep their limit snapshots.

### Verification

The opt-in asset3d_base__contracts suite (--itest=asset3d_contracts) uses the
independent Stage 1 fixture; source/provenance stays outside repositories at
../art_sources/cthangover/asset3d_probe. Its provider-relative OBJ re-export and
provenance are at ../art_sources/cthangover/asset3d_base/fixture. Only runtime
files enter the suite; prototype models are not shipped.

Tests cover formats, axes/bounds, rigs/clips/textures, recipe patches, ZIP,
dependency invalidation, cache integrity/LRU/removal, dispatch, cancellation,
coalescing, texture policies, collision modes and clip slicing. A real
collectible plugin contributes an importer, encoder and native GLTF extension;
unload joins pending work, frees native trees, removes registrations and
verifies its ALC is collected. Existing world3d/editor tests are unchanged.
Run paths/counts are in WORLD3D_PLAN.md. Godot 4.6.3 FBX buffer parsing retains
Reading less data than requested warnings reproduced on unchanged Stage 1
inputs; these are investigated as baseline behavior and are not suppressed.

## Русский

asset3d_base загружает сырые модели через провайдеры папок, ZIP/7z и оверлеев.
world3d_base использует его для геометрии, сохраняя отдельные профили,
семантические привязки и размещения. Ядро и конвейер импорта редактора Godot
не меняются; Blender остаётся авторским инструментом вне игры.

Рецепт model3d_asset — обычная сущность с Id, Extends, Source и Options.
Наследование, патчи и замена файлов работают через существующие резолверы.
Без рецепта применяются настройки по умолчанию. Зависимости читаются только
из провайдера выбранного исходника. Опции и превью доступны в редакторе импорта world3d_editor (см. ниже).

Реестр импортёров использует ModTypeConsumer: сначала содержимое, затем
расширение. GLB/GLTF получают встроенные зависимости до native-разбора.
FBX binary/ASCII предварительно проверяется: картинки Video встраиваются,
авторские файловые имена заменяются; сетевые пути и выход из каталога запрещены.
Имена анимационных .tak не считаются картинками. OBJ имеет ограниченный парсер
групп, индексов, UV/нормалей, многоугольников и MTL; параметры map, пробелы
в именах карт и кривые не поддерживаются. Рига и анимаций в OBJ нет.

До декодирования ограничиваются байты, число зависимостей, вершин, костей,
узлов, клипов и ключей; по заголовкам картинок проверяются размеры и оценка
RGBA с mip-уровнями. Любой .. и сетевой/абсолютный URI отвергается после
декодирования пути. Ошибка содержит стабильный код, путь и текст ru/en.

Опции единиц, осей, масштаба и центра применяются после native-нормализации
метаданных. Иерархия сохраняет пути привязок. Центр — origin/center/floor плюс
PivotOffset; профильные трансформы независимы. DDS/KTX сохраняется авторским:
цвет, нормали, mip-цепочку и размер нужно подготовить заранее. Проверены BC1
DDS/KTX1; остальные варианты не объявлены проверенными. Другие картинки
получают RGBA, обработку по слоту (цвет, GL/DX normal), размер по качеству
и mip-уровни. Встроенного runtime-кодировщика нет, реестр ITexture3dEncoder
расширяемый. RGBA+mip fallback даёт диагностику, а не ошибку загрузки.

LOD мешей создаются по умолчанию (GenerateLods=true); false отключает генерацию.
Shadow mesh и коллизии convex/trimesh/box остаются опциональными.
Нарезка клипов задаётся кадрами/Fps, именем и циклом с граничными ключами.
Ретаргет, игровые события и AnimationTree реализованы в `anim3d_base`, карты и
представления персонажей — в `characters3d_base`, физическое движение — в
`actor3d_base`. Импорт их не выполняет. Граничные позы среза сохраняются,
длительность равна `(LastFrame - FirstFrame) / Fps`, без дополнительного кадра.

Чтение/проверка и подтверждённый разбор GLB/FBX идут в ограниченной очереди
рабочих потоков. Своё GLTF-расширение оставляет native-разбор на главном потоке.
Узлы, материалы, native-кэш и экземпляры создаются на главном потоке, не более
одного готового запроса за кадр. Один native-вызов неделим. Синхронный API
блокирует вызывающий код и обслуживает существующие редакторские вызовы.

Совпавшие ключи объединяются до разбора; клиенты отменяются независимо.
При выгрузке очередь отменяется, рабочие потоки завершаются до удаления
провайдеров и расширений. Начавшийся native-вызов заканчивается сам;
GLTF-расширения снимаются с native-регистрации перед выгрузкой сборки.

Кэш user://mod_cache/assets3d/<мод>/<ключ>.scn имеет JSON-квитанцию.
Ключ включает все зависимости, канонические опции, идентичности/версии
импортёра, расширений, кодировщиков и Godot. У папок применяется подсказка
size/mtime, у архивов хешируются байты; reload очищает подсказки. Публикация
использует временные файлы, OS lease и атомарные переименования. Повреждённые
пары перестраиваются до ResourceLoader. LRU ограничивает диск; ResourcesReady
удаляет кэш действительно отсутствующих владельцев, сохраняя установленные
неактивные моды. Ошибка диска даёт диагностику и результат в памяти.

Общие ресурсы неизменяемы и отделены от узлов экземпляров. Model3dLoadResult
хранится до конца показа, затем Dispose; TakeRoot передаёт владение контейнеру.
Живые экземпляры сохраняют native-ресурсы после вытеснения из кэша. Скрипты
и объекты collectible-сборок в кэше запрещены. Reload освобождает учтённые
узлы, ссылки кэша и регистрации расширений.

Настройки меню модов: один импорт одновременно, 256 МиБ декодированных текстур
на запрос, 512 МиБ дискового кэша. При N импортёрах оценка текстур достигает
N × лимит; геометрия, компилятор, renderer и живые экземпляры требуют ещё памяти.
Значения консервативны по пикам этапа 1 (~688/937 МиБ на весь процесс).
Изменения бюджета сразу применяют вытеснение; запросы сохраняют снимок лимитов.

Приёмочные тесты проверяют форматы, оси/габариты, JSON/патчи, ZIP, инвалидацию,
отмену, дедупликацию, текстуры, коллизии, нарезку и повреждённый кэш. Настоящий
плагин с GLTF-расширением выгружается с проверкой сборки ALC. Исходники Blender
и происхождение лежат вне репозиториев; в suite — только runtime-файлы.
Результаты и логи — в WORLD3D_PLAN.md. Предупреждение FBX Reading less data
than requested воспроизводится и на исходных пробах этапа 1, не подавляется.

### Редактор импорта (world3d_editor)

Откройте «Импорт 3D-моделей» на панели инструментов или кнопкой в редакторе
профиля. Выберите мод назначения, создайте рецепт или откройте существующий,
включая чужой из зависимостей. Исходник выбирается по ключу models3d из папки
или архива. Список учитывает расширения зарегистрированных импортёров, в том
числе плагинов; распаковка и системный файловый диалог не нужны. Обновление
перечитывает список после экспорта. Сохранённый рецепт доступен в выборе
модели редактора профиля.

Вкладка опций показывает эффективные единицы, оси, масштаб, центр и его вектор,
цветовое пространство каждого слота, GL/DX normal, качество/размер текстур,
LOD, геометрию тени и коллизию. Записывается только изменённое поле; остальные
настройки дочернего рецепта продолжают наследоваться. Метаданные формата
применяются первыми. Политика DDS/KTX остаётся авторской, как описано выше.
JsonExtensionData сохраняет неизвестные поля рецепта, опций и клипов при
нормализации для минимальных патчей.

В нарезке перечислены реальные анимации исходника с длительностью. Создайте
или выберите строку, задайте исходную анимацию, имя, FPS, границы кадров и цикл,
нажмите «Применить нарезку». Строку можно удалить; пустой список сохраняет все
анимации. Дополнительные поля строки сохраняются. Ошибки границ и имён
показываются структурированно.

«До / после» использует общее превью с орбитой. «До» — настройки runtime по
умолчанию уже после native-преобразования метаданных. Каждое окно вписывает
свои габариты; масштаб и центр сравниваются по числам в метрах. Жёлтые линии —
габариты, RGB — X/Y/Z, голубые — контуры реальной коллизии. Визуальное дерево
не содержит физических тел; исходный принадлежащий превью результат сохраняет
коллизии для инспектора. Общие ресурсы не меняются. Диагностика содержит
уровень, стабильный код, путь и обе версии текста ru/en. Ошибка убирает старое
превью «после», документ остаётся доступен для исправления.

EntityDocument/JsonPatchDiff обеспечивают минимальный патч чужого ассета,
undo/redo, сохранение, сброс к наследуемому и повторное открытие. Неизвестные
поля и неверный черновик можно править в блоке JSON. Числа — InvariantCulture.
Превью использует существующий синхронный API: native-разбор/сборка могут
блокировать редактор; асинхронный прогресс и прерывание native-вызова здесь
не заявлены.

Принудительный переимпорт удаляет записи двух превью из памяти и диска и
перечитывает исходник без перезапуска и правки рецепта. Другие живые экземпляры
сохраняются. При ожидающих запросах того же владельца или ошибке диска выводится
диагностика. При закрытии окно освобождает использованные записи памяти до
разрушения viewport, сохраняя дисковый кэш и другие экземпляры. Публичные точки
сервиса: SupportedExtensions, Invalidate, ReleaseMemory. Логи проверок —
WORLD3D_PLAN.md, раздел Stage 3.

Созданная импортёром форма передаётся native-узлу; creator-обёртка CLR сразу
освобождается. Контуры превью читают заимствованную форму через native Variant
и фиксированное Expression, получая отдельную принадлежащую превью mesh-копию.
Новые physics-формы и CLR-обёртки общих Shape3D для отрисовки не создаются,
общая геометрия не меняется и не Dispose-ится. Это убирает позднюю финализацию
форм после последней синхронизации physics server. Задержки выхода и политика GC
не менялись. Контракт проверяет box/convex/trimesh, два превью, общие данные и
вытеснение памяти кэша при другом живом экземпляре.


## Stage 6 cancellation replacement

An async caller may observe its cancellation before the shared job's worker has
finished. A new same-provider/path/options request must not join that irreversibly
cancelled job. Asset3dResolver gives the new request a fresh job and keeps the old
worker under a separate retirement key until normal Pump/shutdown joins and disposes
it. Pending-key coalescing still excludes stopped jobs. No worker or provider is
orphaned; other live waiters retain their existing independent cancellation policy.
The asset contract `new_waiter_after_cancel_does_not_join_retiring_worker` and rapid
private-sample location-preview replacement exercise this path (Stage 6 evidence in
WORLD3D_PLAN.md). Cache formats, GC and engine shutdown policy are unchanged.

### Generated mesh LOD verification (2026-11-15)

A resource contains one authored model; reduced triangle index buffers are derived
at import and bundled into the same cached .scn. Unindexed triangle surfaces get
explicit sequential indices before native GenerateLods (which otherwise skips
those surfaces); vertex attributes, weights and blend shapes are retained. The
asset module assembly identity plus canonical options invalidate older cache
entries. Model3dLods.Inspect returns detached per-mesh/surface/level triangle counts
and native simplification error without modifying shared resources.

Native renderer acceptance uses one procedurally authored OBJ and the exact same
mesh instance: 4224 base triangles, nine reduced levels ending at 8; near/far
visible primitives 4224/66, wide/narrow FOV 132/1056, bias 0/high 8/4224. It verifies
memory sharing, a disabled-generation key, and identical buffers/thresholds after
ClearCache and a disk hit without parsing. This proves real geometry selection,
not only that an API was called or a cache file exists. No LOD model files are
loaded. Viewport.MeshLodThreshold and GeometryInstance3D.LodBias use native
projected-size selection; biases do not select a fixed world-distance index.

The new character preview displays these stored levels. Character3dLod stores
Distance, AnimationHz, SkeletonHz, Visible and LodBias, plus the actor-facing
Simulation, Hysteresis and PlanningHz policy. Asset was removed before release.
See [character contract](characters3d.md) for visual scheduling and actor ownership.

### Warm synchronous requests

A validated folder-provider request can instantiate its still-live immutable native
scene without rereading and reparsing the source on every call. The hint is scoped
to provider identity, canonical options/limits and current importer/encoder/extension
signatures. All files read during validation contribute path, length and UTC write
time, captured before reading and checked again afterwards. Every dependency must
still match. Archives and opaque providers retain content validation on every load.
Native resource eviction, ReleaseMemory, Invalidate and ClearCache cannot revive a
stale hint: a missing resource forces ordinary validation. Metadata-preserving edits
require explicit invalidation, as with the existing file-hash memo. Instances remain
independent mutable trees. The optional `Asset3dResolver.LoadMeasured` **event**
reports wait/input/resource/instance time for profiling; it is not a load method.
No timers are read without an observer.

## Canonical imported topology and independent provider files

`Model3dLoadResult.Metadata` retains the existing import key and detached immutable post-import surface descriptors. `Model3dAssets.Load(asset, out metadata)` passes that evidence without a second load; TSCN explicitly returns null metadata. `Model3dTopology.Inspect(root, existingKey)` reads the same canonical domain on the main thread and never imports or modifies resources. Synthetic caller keys are not provenance.

Canonical domain v1 hashes ordered base indices/stream layout and values, imported model space, Skin bind order/poses, referenced original hierarchy/rests, and ordered blend-shape names/mode/layout/values. Surface paths/ordinals are scoped to that result; source ordinals and equal counts do not establish a mask match. Immutable `Morphs` retains every channel without guessing identity/expression/pose-corrective semantics or safe ranges. Presentation policy supplies versioned current shape/fit signatures and full customized anatomy without render holes. This API is not a face/body generator.

Stored LOD index buffers/errors and shadow geometry have separate descriptors. Inspection is not a promise that old LOD/shadows survive compaction: the derived owner must rebuild/validate them. Metadata is shared by memory-cache instances, reconstructed after verified disk load, and released with the existing cache. It retains no nodes/resources/providers or collectible callbacks.

`Asset3dResolver.ReadFile(group,key,maxBytes)` reads the independent winning FileEntry provider, including overlays, through a bounded stream (maximum16MiB). `Model3dProviderFile` contains detached bytes/hash/provider/path; `CopyBytes()` cannot modify its source. No RootPath/seek/extraction or native ResourceLoader is required. Specialised coverage decoding belongs to characters3d; model-local dependencies still belong to the selected model provider. Publish coordinates ordinary file-list invalidation and the consumer's derived generation.
