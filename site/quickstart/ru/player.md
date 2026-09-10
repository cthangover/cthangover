# Уровень 1 — Игрок: патчим персонажа

<div class="qs-meta">
<span>Текстовый редактор</span>
<span>5 минут</span>
<span>Без кода</span>
</div>

Вы усилите персонажа Marao — увеличите здоровье, атаку и добавите способность «Оглушение» — через систему патчей. Ни одной строчки кода, только JSON.

---

## Шаг 1: Создаём папку мода

Каждый мод живёт в своей папке внутри `mods/`. Создайте:

```filestree
mods/
└── quickstart_player/
    ├── manifest.json
    └── patches/
        └── characters.json
```

---

## Шаг 2: manifest.json

Каждому моду нужен манифест. Создайте `mods/quickstart_player/manifest.json`:

```jsonc
{
  "id": "quickstart_player",        // Уникальный идентификатор мода, пиши на английском языке
  "name": "Бафф Марао",             // Отображаемое имя, показываемое в меню управления модами
  "description": "Мой первый мод!", // Описание, видимое в списке модов и каталоге  
  "author": "player",               // Автор мода (произвольная строка)
  "depends": ["core"]               // Зависимость от основной игры (чтобы не требовалось вручную располагать порядок загрузки мода)
}
```

Минимальный манифест — только `name`, однако лучше заполнять и 'id'. Ядро само обнаружит папку и подключит её как мод.

---

## Шаг 3: Патч на персонажа

Создайте `mods/quickstart_player/patches/characters.json`:

```jsonc
{
  "Items": [
    {
      "Id": "Marao", // ИД персонажа, которого нужно пропатчить (определён в основном моде)
      
      "Health": 5000, // Новые значения свойств — перезаписывают оригинальные
      "Attack": 1200,
      "Points": 2,
      
      "Actions$add": ["physics/stun"] // $add добавляет элементы в массив
    }
  ]
}
```

### Как это работает

- **`"Id": "Marao"`** — цель патча: персонаж с идентификатором `Marao` (определён в моде `core`).
- **`"Health": 5000`** и **`"Attack": 1200`** и **`"Points": 2`** — перезаписывают поля персонажа. Новые значения заменяют старые.
- **`"Actions$add": ["physics/stun"]`** — суффикс `$add` означает «добавить элементы в массив». Действие `physics/stun` добавляется к списку. Стандартные `physics/attack` и `physics/defence` остаются.

> Подробнее о патчах: [Patches](site/docs/mods/patches/) и [Array Operations](site/docs/mods/patches/arrays-and-identity).

---

## Шаг 4: Запускаем и проверяем

Запустите игру. Войдите в любой бой — у Marao теперь:

- **5000 HP** вместо 10
- **1200 ATK** вместо 5
- **2 ОД** вместо 1
- Третье действие **«Оглушение»** рядом с «Атака» и «Защита»

---

## Что дальше?

| Хотите | Идите |
|---|---|
| Создать свою сцену с диалогом | [Уровень 2 — Моддер](#quickstart/modder) |
| Понять систему патчей глубже | [Patches](site/docs/mods/patches/) |
| Узнать про персонажей | [Characters](site/docs/mods/characters/) |
| Написать свой первый C#-мод | [Уровень 3 — Программист](#quickstart/programmer) |

---

## Скачать готовый пример

<a href="examples/quickstart_player.zip" class="qs-download" download>
<svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><path d="M12 5v14"/><path d="M5 12l7 7 7-7"/><path d="M4 22h16"/></svg>
Скачать quickstart_player.zip
</a>

Скачиваемый мод на английском языке.
