# Nivel 3 — Programador: Mod en C#

<div class="qs-meta">
<span>C#</span>
<span>20 minutos</span>
<span>Compilación Roslyn</span>
</div>

Vas a escribir un mod en C# que añade al juego una acción DSL personalizada y ajustes en el menú. El código se compila sobre la marcha con Roslyn al iniciar el juego — no necesitas ni Godot ni `dotnet build`.

---

## Paso 1: Estructura del mod

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

## Paso 2: manifest.json

`mods/quickstart_programmer/manifest.json`:

```jsonc
{
  "id": "quickstart_programmer",                 // ID del mod
  "name": "My Code Mod",                         // Nombre del mod que se muestra en la lista de mods
  "description": "Nueva acción y ajustes del mod", // Descripción del mod que se muestra en la lista de mods
  "sources": ["src/*.cs"],                       // Patrón para recoger el código fuente (todos los archivos ".cs")
  "author": "programmer",                        // Autor
  "depends": ["cthangover_game"]               // Dependencia del juego principal (así no hay que ordenar a mano la carga del mod)
}
```

El campo `sources` indica qué archivos C# se compilan con Roslyn. Los errores de compilación (con archivo y línea) van al log en la categoría `MODS_COMPILE`.

---

## Paso 3: Punto de entrada — IModInitializer

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
            
            GameLogger.Log("MY_MOD", "My mod loaded!", LogLevel.Message);
        }
        
    }
}
```

La interfaz `IModInitializer` es el punto de entrada del ciclo de vida del código C# del mod; las implementaciones se descubren por reflexión. `OnModLoaded` se llama al cargar cada mod (no solo el tuyo: `modId` trae el id del mod cargado), `OnModResourcesReady` — una vez, después de cargar todos los mods y sus recursos.

> Usa `GameLogger.Log(...)` en lugar de `GD.Print(...)` si quieres trabajar con los logs.

---

## Paso 4: Ajustes del menú — IModSettings

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

`GetDefinitions` describe los controles, `WriteValues`/`ReadValues` escriben y leen los valores de forma simétrica; el núcleo se encarga del guardado en disco y de la interfaz. Una vez cargado, tu mod aparece en «Settings» → pestaña «Mod Settings» con un deslizador y un interruptor.

---

## Paso 5: Acción DSL personalizada — IScenarioAction

Añadamos una acción nueva, `my_effect`, disponible en los archivos `.scenario`. `mods/quickstart_programmer/src/MyEffectAction.cs`:

```csharp
using Cthangover.Core.Actions;
using Cthangover.Core.Utils;

public class MyEffectAction : IScenarioAction
{
    public string Name => "my_effect";
    
    public void Run(IActionContext context)
    {
        GameLogger.Log("MY_MOD", "New action executed!", LogLevel.Message);
    }
}
```

Ahora en cualquier archivo `.scenario` puedes escribir:

```scenario
action my_effect
```

El motor encontrará tu clase por reflexión y llamará a `Run`. A través de `context` tienes las variables del diálogo (`GetParam`) y los servicios del juego: misiones, personajes, iluminación, la escena.

---

## Paso 6: Un escenario para probarla

Para ver la acción funcionando, la llamaremos cuando el jugador salga de casa. `mods/quickstart_programmer/scenarios/my_effect_demo.scenario`:

```scenario
scene: home_outside
priority: 0
---
action my_effect
end
```

`priority: 0` — nuestro escenario se ejecuta antes que el estándar (`priority: 10`), y después de `end` el juego sigue como siempre: la escena abre su menú habitual.

---

## Paso 7: Localización

Las etiquetas de los ajustes son las claves de localización de `Name`. `mods/quickstart_programmer/locale/ru.properties`:

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

## Paso 8: Conectar el mod al juego

El juego no carga todos los mods instalados, solo los que forman parte de la colección del juego (la interfaz del juego está en inglés y ruso):

1. En el menú principal abre **«Mods & Collections»** → pestaña **«Collections»**.
2. En la tarjeta del juego **Cthangover** pulsa **«Edit scheme»**.
3. Selecciona cualquier mecánica del esquema (por ejemplo, **«Story»**) y pulsa **«Add mod»**.
4. Busca tu mod en la lista (por el nombre de `manifest.json`) y pulsa **«Add»**.
5. Pulsa **«Save»** y confirma. Los cambios se aplican la próxima vez que se inicie el juego.

> **Para desarrolladores.** Desde el código fuente, sin el menú: `godot --path . -- --game=cthangover --new --with-mods=quickstart_programmer --scene=home_outside`.

---

## Paso 9: Comprobarlo

Reinicia el juego:

1. Abre «Settings» → pestaña «Mod Settings» → tu mod con un deslizador y un interruptor.
2. El log del juego (`user://logs/cthangover.log`, en Windows — `%APPDATA%\cthangover\logs\cthangover.log`) contiene `[MY_MOD] My mod loaded!` tras la carga.
3. Sal de casa — en el log aparece `[MY_MOD] New action executed!`.

---

## ¿Qué sigue?

| Quieres | Ve a |
|---|---|
| Suscribirte a la entrada/salida de escenas | [Scene Subscriptions](site/docs/mods/configs/) |
| Crear tu propio modo de combate | [Battle System](site/docs/mods/battle/) |
| Hacer una ventana de herramienta | [Creating a Custom Tool](site/docs/mods/editor_tools/tutorial) |
| Escribir mods en GDScript | [GDScript in Mods](site/docs/mods/src/gdscript/) |
| Todas las interfaces disponibles | [C# Source Code in Mods](site/docs/mods/src/) |

---

## Descargar el ejemplo terminado

<a href="examples/quickstart_programmer.zip" class="qs-download" download>
<svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><path d="M12 5v14"/><path d="M5 12l7 7 7-7"/><path d="M4 22h16"/></svg>
Descargar quickstart_programmer.zip
</a>

El mod descargable está en inglés. Descomprime el archivo en `mods/quickstart_programmer/` o coloca el `.zip` en `user://mods/` y después conecta el mod al juego (paso 8).
