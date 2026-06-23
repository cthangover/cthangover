# Тесты

cthangover использует два уровня тестирования:

- **xUnit-тесты** — чистая бизнес-логика, запускаются через `dotnet test`, не требуют Godot
- **Автотесты (интеграционные)** — прогон сцен в Godot с автоматическим драйвером, проверяют работу систем в движке

---

## Структура тестов

```
tests/
├── CThangover.Tests/               # xUnit-тесты (бизнес-логика)
│   ├── Actions/                    # Тесты фабрики экшенов
│   ├── Items/                      # Тесты предметов
│   ├── Quests/                     # Тесты квестов
│   ├── Scenarios/                  # Тесты парсера и мод-слияния
│   ├── Utils/                      # Тесты утилит
│   └── cthangover.Tests.csproj
├── CThangover.Tests.Integration/   # Автотесты в Godot
│   ├── Autotest/                   # Драйверы автотестов
│   │   ├── ConsoleTestRunner.cs    # Диспетчер тестов по --test=
│   │   ├── DialogAutoDriver.cs     # Авто-прокликивание диалогов
│   │   ├── ModTestSetup.cs         # Интеграционные тесты модов
│   │   └── MainMenuTestSetup.cs    # Авто-клик «Новая игра»
│   ├── Battle/
│   │   ├── BattleTestDriver.cs     # Авто-проводка ходов боя
│   │   └── BattleTestSetup.cs      # Настройка тестового боя
│   ├── Audio/
│   │   └── MusicInitializerTest.cs # Авто-загрузка Audio.tscn
│   └── Scenes/
│       ├── ModTest.tscn           # Сцена для тестов модов
│       ├── BattleTestScene.tscn   # Сцена для тестов боя
│       └── MainMenuTestScene.tscn # Сцена для тестов главного меню
├── run_test.ps1                    # Запуск автотестов в Godot
└── run_tests.ps1                   # Запуск xUnit-тестов
```

Тестовые моды (`test_mod`, `test_mod_a`, `test_mod_b`) лежат в `mods/` и используются `ModTestSetup` и `ScenarioModMergeTests`.

---

## xUnit-тесты

Запуск из корня проекта:

```powershell
dotnet test tests/cthangover.Tests/cthangover.Tests.csproj
```

Или через скрипт:

```powershell
.\tests\run_tests.ps1
```

**Что покрывают:**
- Парсер сценариев (`ScenarioParserTests`) — 37 тестов, все команды DSL
- Слияние сценариев через моды (`ScenarioModMergeTests`) — 8 тестов, кросс-мод локализация
- Фабрика экшенов (`ScenarioActionFactoryTests`) — 10 тестов, рефлексивный поиск
- Квесты (`QuestBaseTests`, `QuestDataTests`) — 11 тестов, статусы, тэги
- Предметы (`ItemTypeTests`, `ItemContainerTests`) — 4 теста, флаги, контейнеры
- Утилиты (`StringParsingTests`, `ListsTests`) — 12 тестов, парсинг строк, списки

**Итого:** 82 теста, ~0.5 секунд на прогон.

Требования: .NET 8 SDK.

---

## Автотесты в Godot

Запускаются через PowerShell-скрипт:

```powershell
.\tests\run_test.ps1
```

Без параметров запускает тесты модов (`ModTest.tscn`).

### Параметры

| Параметр | Описание | По умолчанию |
|----------|----------|-------------|
| `-Test` | Тип теста: `mod`, `battle`, `menu`, `scene` | `mod` |
| `-Scene` | Произвольная сцена (только с `-Test scene`) | — |
| `-LogFile` | Путь к файлу лога | `user://logs/cthangover.log` |
| `-Choices` | Индексы выборов в диалогах через запятую | — |
| `-Help` | Показать справку | — |

### Примеры

```powershell
# Тесты модов (по умолчанию)
.\tests\run_test.ps1

# Тест боя
.\tests\run_test.ps1 -Test battle

# Тест главного меню
.\tests\run_test.ps1 -Test menu

# Прогон произвольной сцены с драйвером диалогов
.\tests\run_test.ps1 -Test scene -Scene "res://Scenes/Battle.tscn" -Choices "0,1,2"

# С сохранением лога в файл
.\tests\run_test.ps1 -Test battle -LogFile C:\logs\game.log
```

### Тестовые сцены

Каждая специализированная сцена инстанциирует **релизную сцену без изменений** и добавляет тестовые драйверы:

| Сцена | Что инстанциирует | Драйверы |
|-------|------------------|----------|
| `BattleTestScene.tscn` | `Scenes/Battle.tscn` | `BattleTestSetup` (настройка GameData) + `BattleTestDriver` (авто-ходы) |
| `MainMenuTestScene.tscn` | `Scenes/MainMenu.tscn` | `MainMenuTestSetup` (авто-клик «Новая игра») |
| `ModTest.tscn` | — | `ModTestSetup` (тесты мод-системы) |

Оригинальные сцены ничем не затронуты — тестовые ноды добавляются как дети к инстанциированному экземпляру.

### Как работают драйверы

1. **`BattleTestSetup`** — в `_Ready()` добавляет персонажа в группу, инициализирует бой с конкретными врагами. Срабатывает первым, до загрузки боевой сцены.
2. **`BattleTestDriver`** — в процессе боя отсчитывает кадры, продвигает ходы, завершает тест при окончании боя или превышении лимита (10 ходов).
3. **`DialogAutoDriver`** — с заданным интервалом кликает `NextAction()`, при появлении `AnswerBox` выбирает вариант согласно `--choices=`.
4. **`MainMenuTestSetup`** — ждёт 0.5 сек и эмулирует нажатие кнопки «Новая игра» (активируется через `--test=menu`).
5. **`ModTestSetup`** — проверяет загрузку модов (`test_mod`, `test_zip_mod`), операции с файлами, include-резолюцию, JSON-десериализацию.
6. **`ConsoleTestRunner`** — читает `--test=` из CLI, может настраивать бой и переключать сцены для ручного режима.
7. **`MusicInitializerTest`** — если в сцене нет music_player, загружает `Audio.tscn`.

Все драйверы обёрнуты в `#if TOOLS` и компилируются только в debug-режиме.

---

## Полное удаление тестов

Для сборки без тестов достаточно удалить папки:

```
tests/
mods/test_mod/
mods/test_mod_a/
mods/test_mod_b/
```

Сборка игры не пострадает:
- `#if TOOLS` исключает код драйверов из компиляции
- `<Compile Remove>` в `.csproj` исключает xUnit-тесты
- `MainMenu.tscn` не ссылается на тестовые скрипты
