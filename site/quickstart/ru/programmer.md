# Уровень 3 — Программист: C#-мод

<div class="qs-meta">
<span>C#</span>
<span>20 минут</span>
<span>Roslyn-компиляция</span>
</div>

Вы напишете C#-мод, который добавляет в игру: кастомный DSL-экшен и настройки в меню. Код компилируется на лету через Roslyn — Godot не нужен.

---

## Шаг 1: Структура мода

```filestree
mods/
└── quickstart_programmer/
    ├── manifest.json
    ├── src/
    │   ├─── MyModSettings.cs
    │   ├─── MyMod.cs
    │   └─── MyEffectAction.cs
    └── locale/
        └─── ru.properties
```

---

## Шаг 2: manifest.json

`mods/quickstart_programmer/manifest.json`:

```jsonc
{
  "id": "quickstart_programmer",                    // ИД мода
  "name": "My Code Mod",                            // Имя мода, отображаемое в списке модов
  "description": "Новое действие и настройки мода", // Описание мода, отображаемое в списке модов
  "sources": ["src/*.cs"],                          // Паттерн по которому будут собираться исходники (указываем все ".cs" файлы)
  "author": "programmer",                           // Автор
  "depends": ["core"]                               // Зависимость от основной игры (чтобы не требовалось вручную располагать порядок загрузки мода)
}
```

Поле `sources` указывает, какие C#-файлы нужно скомпилировать через Roslyn.

---

## Шаг 3: Точка входа — IMod

`mods/quickstart_programmer/src/MyMod.cs`:

```csharp
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.MyCodeMod
{
    public class MyMod : IModInitializer
    {
        public void OnModLoaded(string modId) { }
        
        public void OnModResourcesReady()
        {
            
            GameLogger.Log("MY_MOD", "Мой мод загружен!", LogLevel.Message);
        }
        
    }
}
```

Интерфейс `IMod` — обязательная точка входа для любого C#-мода. Метод `Initialize` вызывается при загрузке.

> Используйте `GameLogger.Log(...)` вместо `GD.Print(...)`, если хотите работать с логами.

---

## Шаг 4: Настройки в меню — IModSettings

Добавим в `MyModSettings.cs`:

```csharp
using System.Collections.Generic;
using Cthangover.Core.Mods;
using Cthangover.Core.Settings;

public class MyModSettings : IModSettings
{
    private float _volume = 1.0f;
    private bool _enabled = true;

    public IReadOnlyList<ModSettingDefinition> GetDefinitions()
    {
        return new List<ModSettingDefinition>
        {
            new()
            {
                Key = "effect_volume",
                Name = "mymod/effect_volume",
                Type = SettingType.Slider,
                DefaultValue = "1.0",
                Min = 0f,
                Max = 2f,
                Step = 0.1f
            },
            new()
            {
                Key = "enable_mod",
                Name = "mymod/enable_mod",
                Type = SettingType.Bool,
                DefaultValue = "true"
            }
        };
    }

    public void WriteValues(DataBlob blob)
    {
        blob.SetFloat("effect_volume", _volume);
        blob.SetBool("enable_mod", _enabled);
    }

    public void ReadValues(DataBlob blob)
    {
        _volume = blob.GetFloat("effect_volume", 1.0f);
        _enabled = blob.GetBool("enable_mod", true);
    }
}
```

После загрузки мода в меню «Настройки → Mod Settings» появится ваш мод с ползунком громкости и переключателем.

---

## Шаг 5: Кастомный DSL-экшен — IScenarioAction

Добавим новый экшен `my_effect`, доступный в `.scenario` файл MyEffectAction.cs:

```csharp
using Cthangover.Core.Scenarios;
using Cthangover.Core.Actions;
using Cthangover.Core.Utils;

public class MyEffectAction : IScenarioAction
{
    public string Name => "my_effect";
    
    public void Run(IActionContext context)
    {
        GameLogger.Log("MY_MOD", "Новое действие выполнилось!", LogLevel.Message);
    }
}
```

Теперь в любом `.scenario` файле можно написать:

```scenario
action my_effect
```

Движок найдёт ваш класс через reflection и вызовет `Execute`.

---

## Шаг 6: Локализация

`mods/quickstart_programmer/locale/ru.properties`:

```properties
mymod/effect_volume = Громкость эффекта
mymod/enable_mod = Включить мод
```

---

## Шаг 7: Проверяем

Запустите игру:

1. Откройте меню настроек → вкладка «Mod Settings» → ваш мод с ползунком и чекбоксом.
2. Напишите тестовый `.scenario` с командой `action my_effect`.
3. В логах игры появляется `[MY_MOD] Новое действие выполнилось!`.

---

## Что дальше?

| Хотите | Идите |
|---|---|
| Подписаться на вход/выход из сцены | [Scene Subscriptions](site/docs/mods/configs/) |
| Создать свой боевой режим | [Battle System](site/docs/mods/battle/) |
| Сделать окно инструмента | [Creating a Custom Tool](site/docs/mods/tools/tutorial) |
| Писать моды на GDScript | [GDScript in Mods](site/docs/mods/src/gdscript/) |
| Все доступные интерфейсы | [C# Source Code in Mods](site/docs/mods/src/) |

---

## Скачать готовый пример

<a href="examples/quickstart_programmer.zip" class="qs-download" download>
<svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><path d="M12 5v14"/><path d="M5 12l7 7 7-7"/><path d="M4 22h16"/></svg>
Скачать quickstart_programmer.zip
</a>

Скачиваемый мод на английском языке.
