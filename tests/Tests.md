# Тестирование Cthangover

Единая точка входа — `tests/Cthangover.TestRunner` (SDK 10, цель `net10.0`, CLR 10 и Godot .NET).
Обёртки `run_tests.bat` и `run_tests.sh` передают ей все аргументы.
В `/workspace` длительные запуски выполняются через существующий файловый мост
`watch/run_watch.bat`: инструкция — `../watch/README.md`. Второй вотчер не нужен.

## Запуск и выбор проверок

```powershell
# Полный обязательный набор
dotnet run --project tests/Cthangover.TestRunner

# Быстрые xUnit-проверки без общей компиляции модов + runtime-контракты раннера
dotnet run --project tests/Cthangover.TestRunner -- --profile fast

# Один или несколько прогонов; --test поддерживает * и ?
dotnet run --project tests/Cthangover.TestRunner -- --test battle_ff
dotnet run --project tests/Cthangover.TestRunner -- --test "rpg_walk*" --test trade

# Конкретные кейсы одного мода; запятая объединяет шаблоны
dotnet run --project tests/Cthangover.TestRunner -- --mod rpg --filter "followers/*"

# Фильтр xUnit передаётся в VSTest без изменения синтаксиса
dotnet run --project tests/Cthangover.TestRunner -- --test unit --filter "FullyQualifiedName~SaveServiceTests"

# Все проверки владельца или выбранной игры/проекции
dotnet run --project tests/Cthangover.TestRunner -- --mod rpg_editor
dotnet run --project tests/Cthangover.TestRunner -- --game rpg_plus --renderer isometric

# Каталог и план: без сборки игры и запуска Godot
dotnet run --project tests/Cthangover.TestRunner -- --list
dotnet run --project tests/Cthangover.TestRunner -- --catalog tests/artifacts/catalog.json
dotnet run --project tests/Cthangover.TestRunner -- --test new_mod_check --plan

# Повтор неуспешных прогонов; подготовка зависимостей выполняется заново
dotnet run --project tests/Cthangover.TestRunner -- --failed tests/artifacts/runs/<id>/report.json
```

Другие фильтры: `--kind unit|mod-unit|integration|manual`, `--tag <tag>`.
Профили: `full`, `fast`, `headless`, `gui`, `visual`, `docs`.

Перед выходом приложение ожидает отменённые фоновые задачи и финализаторы при живом движке.
Успешный `[TEST_RESULT]` с последующим native-падением по-прежнему означает ошибку. Политика
GC и параллелизм раннера не меняются; отдельная гонка interop Godot 4.6.3 остаётся ограничением
движка, проверяемым отдельным стресс-примером.

English: shutdown joins cancelled workers and drains finalizers before engine teardown.
A passing `[TEST_RESULT]` followed by a native crash still fails the launch. Runner concurrency
and CLR GC policy remain unchanged; the separate Godot 4.6.3 interop race remains an engine
limitation requiring its standalone stress reproduction.
`full` сохраняет весь обязательный набор. Индивидуальные `mod_units:<id>` видны
в каталоге и выбираются явно; полный прогон не дублирует ими общий `mod_units`.

`--filter` с `--mod` выбирает только `[ModTest]` указанного мода. Для тестов
виртуальной интеграции используйте её id, например `--mod rpg__battles`.
Фильтр кейсов не сокращает объявленный состав загружаемых модов.
Фильтр в прямом запуске Godot: `--run-tests --mod=rpg --test-filter=followers/*`.
Пустой выбор, неизвестный тест/мод/профиль и фильтр без совпадений — ошибка.

Сборка выполняется в подготовленной версии входов один раз для совпадающего compile identity.
`--no-build` проверяет receipt и хеши всех объявленных бинарных outputs, включая xUnit,
если он выбран. Отсутствующая или устаревшая сборка даёт явный отказ, а не запуск старого DLL.

В host-задаче можно собрать runner заранее и вызвать его DLL без неявной сборки `dotnet run`:

```powershell
dotnet tests/Cthangover.TestRunner/bin/Debug/net10.0/Cthangover.TestRunner.dll --test inventory --input-cache <cache> --artifacts <outputs>
```

Обёртка не должна перед этим заново копировать весь проект. `--input-cache` находится вне
исходного проекта; по умолчанию это `<temp>/cthangover-tests/cache`. Он хранит проверенные
входы отдельно от per-launch результатов. English: invoke the built runner from the designated
source root with a stable input cache; do not add a full-project copy in the host wrapper.
`--fresh-cache` даёт запуску отдельный пустой кеш компиляции; без него используется
постоянный `<temp>/cthangover-tests/cache/assemblies` и соседний `test_assemblies`.
Кеш сохраняется после очистки окружения; при старте удаляются пары, не использованные 14 дней.
English: `--fresh-cache` selects a disposable cold cache. By default compilation artifacts persist
across runs; unused pairs expire after fourteen days, while active compilation leases are skipped.
`--godot` имеет приоритет над `GODOT_BIN` и `PATH`.
Оконные проверки требуют дисплея; Linux-обёртка использует `xvfb-run`, если он есть.

## Слои

| Вид | Источник | Выполнение |
|---|---|---|
| xUnit | `Cthangover.Tests/` | отдельный .NET-процесс; включая архитектуру, пути и общую компиляцию модов |
| Модовые кейсы | `mods/<id>/tests/`, `test_sources` | отдельная сборка `<id>__tests` внутри Godot, `[ModTest]` и `ModAssert` |
| Интеграционные сцены | `Cthangover.Tests.Integration/`, `Core/Autotest/` | отдельный Godot-процесс на прогон |
| Игровые наборы | `mods/<host>/test_suites/<id>/` | виртуальный мод, активируемый аргументами; UI, редакторы, игровые цепочки |
| Проверки данных | resource/locale atlas | проверка ресурсов, зависимостей и локализации |
| Визуальные проверки | RPG snapshots, render suites | окно, реальные изображения; snapshots требуют собственного набора эталонов |
| Документация | doc_shots, site_shots | воспроизведение шагов и снимки; наличие картинки не заменяет проверку результата |

`coverage` и `coverage_subsystems` проверяют наличие объявленных проверок для
зарегистрированных систем. Это не процент покрытия строк кода.

## Mod-folder manifest parts

The runner loads `tests/tests.manifest.json`, then optional `<folder>/tests.manifest.json` parts
in `ModFolders.Names` order (`mods`, `mods_private`). Each part contributes only `integration` and
`manualSuites`; it needs no `unit` block. Other sections, including `manualScenes`, are ignored.
Names and manual-suite policy keys must be unique across all files. Dependencies may refer to tests
in other parts or the base manifest; the merged dependency graph and result contracts are validated
before execution. Keep private mods' tests in their folder's part. The runner discovers suites and
mod unit tests in every configured folder and includes those folders in the content-fenced prepared inputs.
A suite with `requiresExplicitSelection: true` runs only under its exact `--test <name>` selector;
default/profile/glob selection does not admit it, including through a prerequisite.

## Контракт результата

Манифест `tests.manifest.json`, `schemaVersion: 2`, задаёт ожидаемый терминальный
результат каждого запуска. Пример варианта боевого ядра:

```json
{
  "name": "battle_ff",
  "scene": "res://tests/Cthangover.Tests.Integration/Scenes/battle_test_scene.tscn",
  "args": ["--core=ff_battle"],
  "timeoutSeconds": 180,
  "kind": "integration",
  "owners": ["ff_battle"],
  "profiles": ["full", "headless"],
  "tags": ["integration", "headless"],
  "dependsOn": [],
  "result": { "name": "battle", "core": "ff_battle" }
}
```

Драйвер печатает ровно один терминальный маркер:

```text
[TEST_RESULT] name=battle core=ff_battle passed=1 failed=0
```

Обязательны `name`, `passed`, `failed`; необязательны `core`, `skipped`.
Счётчики — неотрицательные целые в пределах Int32, без повторов и неизвестных
полей. Нулевое число выполненных и пропущенных проверок не является успехом.
Ожидаемое имя и ядро должны совпасть, процесс должен завершиться с кодом 0 без
таймаута. Отсутствие маркера, повторный маркер, чужое имя и более ранний FAIL
не могут быть перекрыты последующим успешным результатом. Ошибки `ERROR:`,
`SCRIPT ERROR:` и необработанные исключения, включая ошибки завершения после
маркера, делают прогон неуспешным. Предупреждения завершения об утечках ObjectDB,
RID и ресурсах, оставшихся в использовании, также нарушают контракт. Намеренно вызываемая ошибка задаётся в
`result.expectedErrors` конкретным regex `pattern` и точным `count`; лишняя или
не возникшая ожидаемая ошибка нарушает контракт. В существующих прогонах
исключения для ошибок движка не добавлялись.

Для модовых кейсов `result.childPrefix: "mod_units:"` разрешает промежуточные
итоги модов; общий `name=mod_units` завершает их. Имена детей уникальны,
результаты детей предшествуют общему, их счётчики совпадают с общими.
Дополнительно проверяется соответствие подробного JSON-отчёта модов.

Для xUnit обязательны TRX и согласованные индивидуальные результаты: одного
exit=0 недостаточно. Пропуски `NotExecuted` сохраняются с причиной. Состояния
отчёта: `pass`, `fail`, `timeout`, `skipped`, `blocked`. Пропуск не увеличивает
число прошедших проверок; блокировка из-за зависимости не является успехом.

## Зависимости и окружение

`dependsOn` содержит имена обязательных предшественников. Неизвестные ссылки
и циклы отклоняются до выполнения. Раннер автоматически включает предшественников
даже при выборе только второго этапа. Неуспешная подготовка даёт зависимому
этапу `blocked`, не запуская его процесс.

Текущие цепочки: `new_mod` → `new_mod_check` и
`tutorial_first_game` → `tutorial_first_game_play`.
Связанные зависимости образуют компонент, выполняемый последовательно с общими
данными. Независимые компоненты получают разные пользовательские каталоги.

По умолчанию `--jobs N` равно половине логических ядер, с ограничением 2..8.
`--gui-jobs G` ограничивает окна (2), `--write-jobs W` — постоянные изолированные write lanes (2).
Окно занимает и общий слот процесса. Компоненты стартуют от долгих к коротким
по `seconds` в манифесте; без оценки используется четверть таймаута.
Порядок результатов остаётся порядком плана. xUnit занимает обычный слот процесса.
При `N > 1` его коллекции идут последовательно через сохранённый `unit.runsettings`:
внешний раннер уже обеспечивает параллелизм, а вложенная конкуренция задерживала
короткие асинхронные проверки. Все тесты и их проверки сохраняются; `--jobs 1`
оставляет прежние настройки xUnit.

`project` каждой записи задаёт доступ к дереву: `read`, `output` (только каталоги
из собственных аргументов `res://tests/artifacts/...`) или `write` (также значение
при отсутствии поля). Пересекающиеся выходные каталоги разрешены только внутри
одного компонента зависимостей. Неизвестное значение и `output` без каталога — ошибка.

Во всех режимах, включая `--jobs 1`, `read`/`output` используют подготовленное неизменяемое
дерево под межпроцессными read leases. `--jobs 1` меняет расписание, а не способ изоляции.
Fingerprint включает содержимое выбранных исходных файлов/каталогов, preparation policy и toolchain.
Исходники проверяются до/после materialization и перед seal: текущие правки агентов не смешиваются
в одной опубликованной версии. Изменение версии при живом читателе даёт `prepared-revision-busy`.
Первая подготовка засчитывает реальное заполнение baseline; следующая синхронизирует только
отличающиеся файлы и удаления. Повтор той же версии проверяет хеши и не копирует входы.

`ready.json` описывает transformed prepared inputs, включая фиксированный `override.cfg`,
и отдельно build outputs. Не копируются `.git`, developer SDK executables, старые артефакты,
`node_modules` и общее состояние редактора Godot. Native imported resources и выбранные
UID/class/scene/extension metadata входят в хеши; бинарные outputs сборки тоже проверяются.
Исключён только именованный build `obj` scratch, а не всё `.godot` или `tests/artifacts`.
Изменение с прежними длиной/mtime обнаруживается по содержимому. Неожиданные пустые каталоги
также видны fence; объявленные каталоги и родители build outputs сохраняются.

`write` занимает ограниченный persistent physical slot до конца компонента. Его файлы физически
независимы от source/baseline; writable hardlinks не создаются. Первое заполнение учитывается
явно. На следующем Acquire/границе компонента восстанавливаются только изменённые/удалённые
файлы, а добавления и посторонние пустые каталоги удаляются. Legacy объявленные каталоги вывода
допустимы пустыми только в write workspace; их содержимое и посторонние дочерние каталоги
не исключаются из проверки. Цепочка зависимостей сохраняет свой DATA между запусками.

Quarantine не исправляется молча: обычный Acquire отказывает `prepared-input-mutated`.
`--repair-inputs` явно восстанавливает generation под exclusive lease и сохраняет причину/счётчики
исправления. Repair с `--no-build` по-прежнему отказывает при несовпадении бинарных outputs.

Подготовленный `override.cfg` один раз задаёт `cthangover-tests/data`; его не переписывают
перед каждым запуском. Компоненты получают разные OS homes: `XDG_DATA_HOME` на Linux/BSD,
`APPDATA` на Windows, `HOME` на macOS. `CTHANGOVER_TEST_DATA` — последовательный DATA компонента,
`CTHANGOVER_TEST_PROJECT` — настоящий res-root, `CTHANGOVER_TEST_CACHE` — runtime compilation cache.
Каждый launch получает `CTHANGOVER_TEST_RUN_ID`, `CTHANGOVER_TEST_ID`,
`CTHANGOVER_TEST_ATTEMPT_ID`, `CTHANGOVER_TEST_LAUNCH_ID`, `CTHANGOVER_TEST_WORKER_ID`,
а также `CTHANGOVER_TEST_ARTIFACTS`, `CTHANGOVER_TEST_LOGS`, `CTHANGOVER_TEST_PREPARED_REVISION`,
`CTHANGOVER_TEST_INPUT_IDENTITY`, `CTHANGOVER_TEST_LEASE_ID`, `CTHANGOVER_TEST_ACCESS`
и свои TEMP/TMP/TMPDIR. Core проверяет фактические res://, user:// и temp до Settings.
Неполный/несовпадающий контекст — отказ до пользовательских записей; собственного `--jobs 1`
обхода этой проверки нет. Прямой Godot/dotnet test вне runner такой контекст не создаёт.

`--log-file <launch>/logs/godot.log` передаётся Godot до `--`. Предстартовый `launch.json`
фиксирует реальные engine arguments; Core либо видит native flag, либо проверяет этот receipt.
Core main/error, native log, stdout/stderr, mod-test JSON, screens, sandbox и temp принадлежат
конкретному launch. Аргументные `res://tests/artifacts/...` outputs отображаются наружу в его
пространство. ArtifactCollector перечисляет только этот namespace, не соседние запуски.

Успех удаляет run-owned homes/temp; `--keep-workspace` и ошибка сохраняют их. Prepared baseline
и write lanes живут по отдельному cache retention. Отчёты/launch outputs сохраняются независимо.
Ctrl+C останавливает дочерние деревья и оставляет частичный `complete=false` report; успешный
marker не отменяет timeout, native exit, context или immutable fence failure.

Retention ограничивает неактивные логические run homes и self-marked prepared stores по возрасту,
количеству и байтам. Defaults:3 дня,3 владельца,20 ГиБ; `--retention-days/count/gib` перекрывают
`CTHANGOVER_TEST_RETENTION_DAYS/COUNT/GIB`. Ноль допустим. Подготовленные stores рассматриваются
только в выбранном `--input-cache`, run homes — в корне temp runner. Активные OS leases и PID/start
markers защищают чтение/запись/компоненты; чужие неразмеченные каталоги не присваиваются.
`--keep-workspace` не отменяет retention. `retentionDecisions` сохраняет причины сохранения/удаления.

English: all scheduler modes reuse leased content-verified inputs; warm same-revision launches copy
zero input files/bytes. Writes use bounded independent physical lanes with delta restoration.
One fixed prepared override combines with private component OS homes and per-launch artifact/log/temp
namespaces. Core verifies actual native paths before Settings. NoBuild checks receipts and hashes;
quarantine requires explicit repair. Scoped retention never removes an active leased input/run.

Файловые логи GameLogger включают текущий файл и архивы: `cthangover.log`,
`cthangover.log.1`, `cthangover.log.2`; errors-лог использует ту же
схему. Native Godot `--log-file` — отдельный engine sink, не writer GameLogger. По умолчанию 10 МиБ на файл и 3 файла на лог, включая текущий;
`logging.max_file_size_mb` / `logging.max_files` имеют минимум 1. При старте
большой файл сразу ротируется; далее размер учитывается счётчиком байт UTF-8.
Ротация происходит перед следующей строкой, превышающей бюджет; одна слишком
длинная строка остаётся целой. Ошибка ротации не мешает дописывать текущий файл.
Writer постоянный, UTF-8 без BOM, LF, AutoFlush; закрывается на ProcessExit или
Shutdown. Для хронологии читайте `.2`, затем `.1`, затем текущий файл.
Контекстный ArtifactCollector сохраняет ссылки на собственные launch logs, включая архивы,
и берёт последние ошибки из своего `cthangover_errors.log`, не копируя соседние namespaces.
Legacy RetainedDiagnostics сохраняет архивы `.log.N` из удаляемого run-owned состояния
с обычным лимитом хвоста 8 МиБ.
Проверки чистого writer и
конфигурации: `--filter "FullyQualifiedName~RollingLogWriterTests|FullyQualifiedName~LoggingSectionTests|FullyQualifiedName~RetainedDiagnosticsTests.RollingLogArchives"`.

Перед удалением пользовательские logs/`.result`/mod reports попадают в
`retained-diagnostics/<copy-id>/` внутри отчёта; проект туда не копируется. У длинного лога сохраняются
последние 8 МиБ, общий бюджет дополнительных диагностик 64 МиБ на копию; при ошибке архивации
копия остаётся и причина выводится. Основные stdout/stderr уже пишутся непосредственно в отчёт.
Старые каталоги без owner-маркера runner не присваивает себе: их убирает контейнерный очиститель
с проверкой процессов. `--keep-workspace` также подчиняется retention, не даёт бессрочного хранения.

English: inactive retained runs default to 3 days, newest 3 logical runs and 20 GiB of file data;
all lanes count together, oldest runs are removed first. CLI `--retention-days/count/gib` overrides
`CTHANGOVER_TEST_RETENTION_DAYS/COUNT/GIB`. Zero is allowed. Owner leases plus PID/start-time markers
protect active runs and recognise dead owners after SIGKILL. Reports survive eviction; bounded log
tails/results are archived before run-home removal. Archive failures preserve the run state and print
the reason. Persistent assembly caches and unmarked legacy trees are outside this run-home policy;
prepared input stores have their own self-marked lease-aware retention decisions.

## Модовые тесты и восстановление

`[ModTest("group/case")]` разрешён у статического метода с возвратом `void` или
`Task`, без параметров либо с одним `ModTestContext`. `async void`, generic-методы,
неверные параметры, пустые/повторные имена и неположительный таймаут — ошибки
обнаружения. Статические классы поддерживаются. Ошибки загрузки типов не
превращаются в частичный успешный набор.

Контекст предоставляет sandbox для файлов и узлов, доступ к файлам мода, лог
и токен отмены. После кейса восстанавливаются resettables и реестры, TimeScale,
удаляются добавленные корневые узлы; раннер ждёт применения отложенной очистки
перед следующим кейсом. Нарушение восстановления останавливает набор.
Таймаут асинхронной задачи также останавливает набор: отмена кооперативная,
поэтому запуск следующего теста был бы небезопасен. Синхронное зависание
ограничивает внешний таймаут процесса.

## Отчёты и диагностика

Каждый запуск имеет уникальный каталог `tests/artifacts/runs/<run-id>/`:

- `plan.json` — порядок, причины выбора, фильтры и компоненты окружения;
- `catalog.json` — каталог, использованный этим запуском;
- `report.json` — версия схемы, полнота запуска, общие и индивидуальные результаты;
- `report.html` — сводка, сообщения, стеки и ссылки на логи;
- `report.junit.xml` — экспорт для CI;
- `build.log` и `tests/<id>/process.log` — вывод сборки и процессов;
- `tests/<id>/unit.trx`, подробные модовые JSON, игровые логи, sandbox и снимки;
- `tests/<id>/changed-project/` — изменённые и добавленные файлы неуспешного
  прогона, плюс список удалённых файлов, до восстановления следующей группы.

Логи пишутся во время выполнения, а не только после завершения. Общий отчёт
обновляется атомарно после каждого прогона. До завершения `complete=false`,
`success=false`, ожидающие проверки помечены как не начатые. `--json <path>`
сохраняет дополнительную копию этого же отчёта; обёртки используют
`tests/artifacts/test-results.json` как указатель на последний результат.

Атомарный `report.json` обновляет один поток после каждого завершения; HTML и JUnit
формируются только при полном завершении. English: JSON checkpoints have one writer;
interrupted runs retain a partial JSON, and HTML/JUnit are complete-run exports.

Поля JSON (секунды, если не указано иное):

| Поле / Field | Значение / Meaning |
|---|---|
| `wallSeconds` | Полное время до снимка результата / elapsed run time to the final snapshot |
| `childSeconds` | Сумма длительностей тестовых процессов без начальной сборки / sum of test subprocess durations, excluding initial build |
| `phases.buildSeconds`, `workspaceCopySeconds` | Проверенная сборка и подготовка baseline/lanes / checked build and input/lane preparation |
| `phases.restoreSeconds`, `restoreCount` | Восстановление проекта и число попыток / project restoration time and attempt count |
| `phases.copies`, `laneBusySeconds` | Число первых полных заполнений и время детей на lane / initial full populations and per-lane child time |
| `phases.launchSeconds`, `parallelism` | От подготовки первого запуска до последнего сбора артефактов и childSeconds / launchSeconds; фазы могут перекрываться / first-launch preparation through final collection and achieved concurrency, overlapping phases |
| `phases.artifactCollectionSeconds`, `reportWritingSeconds`, `cleanupSeconds` | Сбор артефактов, отчёты, очистка / collection, reporting and cleanup |
| `results[].overheadSeconds` | Восстановление перед запуском плюс сбор артефактов после / preceding restore plus following collection |
| `results[].bootSeconds` | До первой категории `[TEST]` или `[MODTEST]` в stdout; иначе `null` / first stdout driver category, otherwise null |
| `results[].cpuSeconds`, `peakMemoryMb` | CPU и пиковая резидентная память корневого дочернего процесса, MiB; `null`, если недоступны / best-effort root-process CPU and peak resident MiB, not descendant totals |
| `results[].logLines` | Число строк stdout/stderr / captured line count |
| `inputPreparation.sourceFingerprint`, `preparedInputId`, `buildIdentity` | Проверенная source/preparation/build версия / checked input and compilation identities |
| `inputPreparation.filesCopied/bytesCopied/reusedInputs/fullProjectCopies` | Реальные input transfers; warm той же версии0/0 / actual transfers, same-revision warm0/0 |
| `inputPreparation.prepareSeconds/cpuSeconds/peakMemoryMb` | Подготовка и root-process ресурсы / preparation and sampled root resources |
| `workspaceMetrics[]` | Действительные transfer/removal/restore counters каждого prepared-read/write workspace |
| `results[].execution.inputsCopied` | Input transfers конкретного launch, отдельно от baseline/restore / per-launch transfers |
| `results[].execution` | Run/test/attempt/launch/lease IDs и реальные output/log/data/temp roots |
| `launch.json.fenceChanges`, `immutableFenceChanges`, `fenceClassification` | Write-lane delta отдельно от нарушения immutable baseline |
| `retentionDecisions[]` | Область, владелец, leases и причина retention / scoped retention ownership/reasons |

`reportWritingSeconds` в снимке не включает последнюю запись самого этого JSON;
HTML/консоль показывают ту же разбивку и десять самых долгих запусков. Сборка
показывается отдельно; разница wall и суммы фаз/процессов включает планирование
и разбор результатов. CPU/память опрашиваются во время работы процесса, поскольку
после завершения ОС может их не предоставить. `bootSeconds` — граница первого
сообщения драйвера, а не отдельная инструментированная фаза загрузки Godot.
English: report timing excludes its own final JSON persistence. Phase totals exclude parsing/planning;
the first driver line is a boot proxy. CPU/memory sampling targets the launched process root only.

Сборки публикуются под межпроцессной блокировкой ключа: сначала PDB, затем DLL.
Готовая пара неизменна. Инвалидация меняет поколение ключа, не удаляя файлы,
которые другой процесс может загружать. Пути исходников внутри тестовой копии
нормализуются относительно проекта для хеша; PDB сохраняет абсолютный путь
копии, впервые скомпилировавшей эту пару. При повторном использовании кеша отладчик
может показать этот старый путь; `--fresh-cache` создаёт символы для текущей копии.
English: per-key OS leases protect immutable PDB-first/DLL-last publication. Invalidation advances
the key generation. Relocatable hash inputs preserve cross-run hits; cached PDBs retain producer paths.

Ошибки движка и утечки с уровнем `ERROR:` нарушают контракт даже после успешного
маркера. Читайте также игровые логи и предупреждения завершения: внутренние
проверки драйвера не заменяют диагностику его окружения.

## Каталог и добавление проверки

Каталог объединяет записи манифеста, индивидуальные наборы `test_sources`,
интеграции модов и ручные наборы. Владельцы, профили, теги, игра, проекция,
источник, зависимости и результат доступны в JSON.

Для нового автоматического прогона добавьте драйвер/сцену и запись с явным
контрактом, владельцами и профилями. Для нового набора модов добавьте его
активацию в манифест хоста. Не включённый в автоматический набор suite обязан
иметь пояснение в `manualSuites`; вспомогательная сцена — в `manualScenes`.
Проверка каталога отклонит забытые наборы и сцены.

Snapshots оставлены ручными: без проверенного набора эталонов их нельзя
объявить успешными. `doc_shots` требует явного файла шагов; проверки приватных
каталогов требуют своего серверного fixture. Причины видны в каталоге.

Контрактные xUnit-проверки инфраструктуры находятся в `Cthangover.Tests/Testing/`.
`test_engine_contracts` дополнительно проверяет реальную очистку узлов Godot
и остановку набора после таймаута некооперативной задачи.

Пары `dependency_setup_positive` → `dependency_child_positive` проверяют передачу
данных между процессами. `dependency_child_negative` — отдельная намеренно
неуспешная проверка: её подготовка падает, а процесс потомка не должен стартовать.
Негативные фикстуры исключены из обычных профилей и выбираются точным именем
либо профилем `contract-negative`.


## .NET 10 / SDK и runtime

Все собственные проекты используют SDK 10 / `net10.0`, включая TestRunner, xUnit,
Live2D и вспомогательный проект пакетов Markdown. `global.json` принимает стабильный
SDK 10.0.201 и более новые feature bands 10.0 (`latestFeature`); SDK 8 не нужен.
Boot сохраняет проверку CLR >= 10 из-за дефекта collectible ALC в CLR 8.

Godot 4.6.3 проверен с net10.0: сборка через GodotTools, C# tool-script в редакторе,
запуск и self-contained Linux export. Его vendor tools остаются net8; runtimeconfig
GodotPlugins уже задаёт LatestMajor. Дополнительные `DOTNET_ROLL_FORWARD` и
`DOTNET_HOST_PATH` в раннере и запускателях не требуются.

`Directory.Build.targets` копирует SDK reference pack в
`mod_reference_assemblies/net10.0/` рядом с ядром, в xUnit output и экспорт.
Roslyn компилирует моды и их тесты по этим API, исключая runtime implementation DLL.
Версия/хеш pack входят в ключи production/test cache; precompiled cache имеет
новое пространство net10.0. Старые cache keys не переиспользуются.

В контейнере: `source /workspace/tools/env.sh; export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`.
На Windows: SDK 10, Godot 4.6.3 mono и его export templates. Существующий `.sln`
сохранён: экспорт Godot 4.6.3 ожидает этот формат; SDK 10 по умолчанию создаёт `.slnx`.
Python 3 нужен release verifier.

Общий RuntimeFrameworkVersion удалён. Только ExportDebug/ExportRelease закрепляют
runtime 10.0.15 в Cthangover.csproj, чтобы SDK 10.0.201 не понизил runtime игроков.
Release verifier проверяет net10.0, includedFrameworks/deps, пять critical runtime
DLL по SHA256, Godot.SourceGenerators.dll и весь reference pack по исходному SDK.
Self-contained игроку не нужны ни SDK, ни отдельная установка .NET.

`build_release.sh linux --no-archive --verify` (Windows: `build_release.bat windows --no-archive --verify`)
запускает свежую папку экспорта, даже если рядом остался архив старого релиза. Без `--no-archive`
проверяется архив, созданный текущим запуском. Перед игрой core/deps/runtimeconfig сравниваются
с текущим экспортом. Verifier также проверяет индекс PCK на рабочие/приватные каталоги и полный
набор loose-модов: фикстуры `test_*` исключены, игровой `stat_checks` сохраняется.

English: release verify plays only this invocation's artifact and compares its managed payload to
the new export. PCK development/private trees and loose fixture mods fail release validation.
`private_real_mods --windowed --shots <outside-repositories>` captures the first ready publish form,
the repeat's success, and the new version's refreshed detail/history. All screenshot uniqueness,
package checksum and render-contract assertions remain required.

English: owned projects use SDK 10/net10.0; global.json accepts SDK 10.0.201 and
newer stable 10.0 feature bands. Godot's own vendor assemblies retain their target,
and its plugin runtimeconfig already permits CLR 10. No injected host/roll-forward
workarounds remain in the runner. The shipped reference pack defines the runtime
mod compiler contract and invalidates old cache keys. Export servicing stays pinned
to 10.0.15; development tools use the runtime included in SDK 10. Keep the legacy
.sln for Godot export and install Python 3 for release verification.
