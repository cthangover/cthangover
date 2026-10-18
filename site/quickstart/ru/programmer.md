# Уровень 3 — Программист: C#-мод

<div class="qs-meta">
<span>C#</span>
<span>20 минут</span>
<span>Roslyn-компиляция</span>
</div>

Вы напишете C#-мод, который добавляет в игру кастомный DSL-экшен и настройки в меню. Код компилируется на лету через Roslyn при запуске игры — ни Godot, ни `dotnet build` не нужны.

---

## Шаг 1: Структура мода

```filestree
mods/
└── quickstart_programmer/
    ├── manifest.json
    ├── src/
    │   ├── MyModSettings.cs
    │   ├── MyMod.cs
    │   └── MyEffectAction.cs
    ├── scenarios/
    │   └── my_effect_demo.scenario
    └── locale/
        ├── ru.properties
        └── en.properties
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
  "depends": ["cthangover_game"]                  // Зависимость от основной игры (чтобы не требовалось вручную располагать порядок загрузки мода)
}
```

Поле `sources` указывает, какие C#-файлы нужно скомпилировать через Roslyn. Ошибки компиляции (с файлом и строкой) пишутся в лог в категории `MODS_COMPILE`.

---

## Шаг 3: Точка входа — IModInitializer

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

Интерфейс `IModInitializer` — точка входа жизненного цикла C#-кода мода; реализации обнаруживаются через reflection. `OnModLoaded` вызывается при загрузке каждого мода (не только вашего — в `modId` приходит id загруженного мода), `OnModResourcesReady` — один раз после загрузки всех модов и их ресурсов.

> Используйте `GameLogger.Log(...)` вместо `GD.Print(...)`, если хотите работать с логами.

---

## Шаг 4: Настройки в меню — IModSettings

`mods/quickstart_programmer/src/MyModSettings.cs`:

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

`GetDefinitions` описывает элементы управления, `WriteValues`/`ReadValues` — симметричная запись и чтение значений; сохранение на диск и UI берёт на себя ядро. После загрузки мода в меню «Настройки» → вкладка «Настройки модов» появится ваш мод с ползунком и переключателем.

---

## Шаг 5: Кастомный DSL-экшен — IScenarioAction

Добавим новый экшен `my_effect`, доступный в `.scenario` файлах. `mods/quickstart_programmer/src/MyEffectAction.cs`:

```csharp
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

Движок найдёт ваш класс через reflection и вызовет `Run`. Через `context` доступны переменные диалога (`GetParam`) и сервисы игры: квесты, персонажи, освещение, сцена.

---

## Шаг 6: Сценарий для проверки

Чтобы увидеть экшен в деле, вызовем его при выходе из дома. `mods/quickstart_programmer/scenarios/my_effect_demo.scenario`:

```scenario
scene: home_outside
priority: 0
---
action my_effect
end
```

`priority: 0` — наш сценарий запускается раньше стандартного (`priority: 10`), а после `end` игра продолжается как обычно: на сцене открывается её обычное меню.

---

## Шаг 7: Локализация

Подписи настроек — это ключи локализации из `Name`. `mods/quickstart_programmer/locale/ru.properties`:

```properties
mymod/effect_volume = Громкость эффекта
mymod/enable_mod = Включить мод
```

`mods/quickstart_programmer/locale/en.properties`:

```properties
mymod/effect_volume = Effect volume
mymod/enable_mod = Enable mod
```

---

## Шаг 8: Подключаем мод к игре

Игра загружает не все установленные моды, а только те, что входят в коллекцию игры:

1. В главном меню откройте **«Моды и коллекции»** → вкладка **«Коллекции»**.
2. На карточке игры **Cthangover** нажмите **«Изменить схему»**.
3. Выделите на схеме любую механику (например, **«Сюжет»**) и нажмите **«Добавить мод»**.
4. Найдите в списке свой мод (по имени из `manifest.json`) и нажмите **«Добавить»**.
5. Нажмите **«Сохранить»** и подтвердите. Изменения вступят в силу при следующем запуске игры.

> **Для разработчиков.** Из исходников без меню: `godot --path . -- --game=cthangover --new --with-mods=quickstart_programmer --scene=home_outside`.

---

## Шаг 9: Проверяем

Перезапустите игру:

1. Откройте «Настройки» → вкладка «Настройки модов» → ваш мод с ползунком и переключателем.
2. В логе игры (`user://logs/cthangover.log`, на Windows — `%APPDATA%\cthangover\logs\cthangover.log`) после загрузки есть строка `[MY_MOD] Мой мод загружен!`.
3. Выйдите из дома на улицу — в логе появляется `[MY_MOD] Новое действие выполнилось!`.

---

## Что дальше?

| Хотите | Идите |
|---|---|
| Подписаться на вход/выход из сцены | [Scene Subscriptions](site/docs/mods/configs/) |
| Создать свой боевой режим | [Battle System](site/docs/mods/battle/) |
| Сделать окно инструмента | [Creating a Custom Tool](site/docs/mods/editor_tools/tutorial) |
| Писать моды на GDScript | [GDScript in Mods](site/docs/mods/src/gdscript/) |
| Все доступные интерфейсы | [C# Source Code in Mods](site/docs/mods/src/) |

---

## Скачать готовый пример

<a href="examples/quickstart_programmer.zip" class="qs-download" download>
<svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><path d="M12 5v14"/><path d="M5 12l7 7 7-7"/><path d="M4 22h16"/></svg>
Скачать quickstart_programmer.zip
</a>

Сообщения в логе скачиваемого мода — на английском языке. Распакуйте архив в `mods/quickstart_programmer/` или положите `.zip` в `user://mods/`, затем подключите мод к игре (шаг 8).
