# Уровень 2 — Моддер: сцена на DSL

<div class="qs-meta">
<span>JSON + DSL</span>
<span>15 минут</span>
<span>Без кода</span>
</div>

Вы создадите мод с собственной сценой — комнату с диалогом, выборами и переходами. А заодно добавите опцию «В бар» в существующую сцену кухни, не трогая оригинальные файлы.

---

## Шаг 1: Структура мода

Создайте папку и базовые файлы:

```filestree
mods/
└── quickstart_modder/
    ├── manifest.json
    ├── scenes/
    │   └── my_alko_bar.json              # Наша новая локация - алко-бар!
    ├── scenarios/
    │   ├── my_alko_bar.scenario          # Сценари внутри алко-бара, дадим игроку возможность повзаимодействовать с новой локацией
    │   └── town_entry_override.scenario  # Переопределяем сцену в городе, чтобы добавить переход в нашу новую локацию
    ├── backgrounds/
    │   └── my_alko_bar/
    │       └── bg.png                    # Картинка интерьера бара
    ├── backgrounds/
    │   └── sounds/
    │       └── alko_bar_ambient.ogg      # Фоновый звук шума в баре
    └── locale/
        └── ru.properties                 # Локализация для русского языка
```

---

## Шаг 2: manifest.json

`mods/quickstart_modder/manifest.json`:

```jsonc
{
  "id": "alko_bar",                                    // ИД мода
  "name": "Мой алко-бар",                              // Имя мода, отображаемое  в меню модов
  "description": "Добавляет сцену алко-бара в городе", // Описание мода, отображаемое в меню модов
  "author": "modder",                                  // Автор мода
  "depends": ["core"]                                  // Зависимость от основной игры (чтобы не требовалось вручную располагать порядок загрузки мода)
}
```

---

## Шаг 3: Описание сцены

`mods/quickstart_modder/scenes/my_alko_bar.json`:

```jsonc
{
  "name": "my_alko_bar",                              // Придумываем новый, уникальный идентификатор сцены (используется в switch_scene), это будущая сцена.
  "defaultBackground": "my_alko_bar/bg",              // Путь к PNG-фону внутри backgrounds/ (без расширения)
  "defaultAmbient": "locations/alkobar/bar_ambient",  // ID фонового звука этой локации (без расширения), например "шум в баре", который будет запускаться при входе на локацию
  "defaultScenario": "scenarios/my_alko_bar.scenario" // Сценарий по умолчанию, котоырй будет запускаться в этой сцене, если ничего другого нет
}
```

| Поле | Значение                                              |
|---|-------------------------------------------------------|
| `name` | Уникальный ID сцены. Используется в `switch_scene`    |
| `defaultBackground` | Путь к фону внутри `backgrounds/` (без расширения)    |
| `defaultAmbient` | Фоновый звук. Пустая строка — без звука               |
| `defaultScenario` | Путь к `.scenario` файлу, который запустится при входе |

---

## Шаг 4: Фон

Положите изображение в `mods/quickstart_modder/backgrounds/my_alko_bar/bg.png`.

Подойдёт любое PNG-изображение. Игра поддерживает шейдерное освещение и время суток — фон будет автоматически освещаться.

> Если нет своего изображения, временно скопируйте `mods/core/backgrounds/home/kitchen.png` → `mods/quickstart_modder/backgrounds/my_alko_bar/bg.png`.

---

## Шаг 5: Сценарий на DSL

`mods/quickstart_modder/scenarios/my_alko_bar.scenario`:

```scenario
scene: my_alko_bar
priority: 10
---
text "Вы заходите в полупустой бар."
text "За стойкой скучает бармен."

select "[Что будете делать?]"
option "Подойти к стойке" -> :bar
option "Уйти" -> :leave

:bar
text "Бармен кивает вам."
text "— Чего желаете?"
end

:leave
switch_scene town_entry
end
```

### Что здесь происходит

- **`text`** — показывает строку диалога. Параметр `key=` — ключ локализации.
- **`select`** — открывает меню выбора. Текст в кавычках — подсказка.
- **`option`** — вариант выбора. `-> :label` — куда перейти при выборе.
- **`:bar`**, **`:leave`** — метки для переходов.
- **`switch_scene`** — переносит игрока в другую сцену.
- **`end`** — завершает сценарий (игрок остаётся на сцене).

---

## Шаг 6: Расширяем город (приоритет сцены)

Теперь добавим опцию «Зайти в бар» в сцену города, **не трогая** оригинальный файл `core`.

`mods/quickstart_modder/scenarios/town_entry_override.scenario`:

```scenario
scene: town_entry
priority: 1
---
select "В городе сегодня пахнет иначе..."
option "Вернуться к дому" -> :home
option "Зайти в бар" -> :alko_bar

:home
switch_scene home_outside
end

:alko_bar
switch_scene my_alko_bar
end
```

### Почему это работает

У сцены `town_entry` теперь два сценария:
- `priority: 10` — стандартный (из мода `core`)
- `priority: 1` — наш (из `quickstart_modder`)

Движок запускает сценарий с **наименьшим** `priority`. Наш `priority: 1` побеждает — игрок видит новую опцию.

> Подробнее: [Scene Priority System](site/docs/mods/scenes/).

---

## Шаг 7: Проверяем

Запустите игру:

1. Зайдите в город — видите опцию «Зайти в бар».
2. Нажмите — вы в своей сцене `my_alko_bar` с диалогом и выборами.
3. Выберите «Уйти» — возвращаетесь в город.

---

## Что дальше?

| Хотите | Идите |
|---|---|
| Освоить все DSL-команды | [DSL Command Reference](site/docs/mods/scenes/scenarios/dsl/) |
| Добавить интерактивные объекты | [Interactive Objects](site/docs/mods/interactives/) |
| Написать C#-мод | [Уровень 3 — Программист](#quickstart/programmer) |

---

## Скачать готовый пример

<a href="examples/quickstart_modder.zip" class="qs-download" download>
<svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><path d="M12 5v14"/><path d="M5 12l7 7 7-7"/><path d="M4 22h16"/></svg>
Скачать quickstart_modder.zip
</a>

Скачиваемый мод на английском языке.
