# Nivel 3 — Programador: Mod en C#

<div class="qs-meta">
<span>C#</span>
<span>20 minutos</span>
<span>Compilación Roslyn</span>
</div>

Escribirás un mod en C# que añade al juego: una acción DSL personalizada y ajustes en el menú. El código se compila al vuelo mediante Roslyn — no necesitas Godot.

---

## Paso 1: Estructura del mod

```filestree
mods/
└── quickstart_programmer/
    ├── manifest.json
    ├── src/
    │   ├─── MyModSettings.cs
    │   ├─── MyMod.cs
    │   └─── MyEffectAction.cs
    └── locale/
        └─── es.properties
```

---

## Paso 2: manifest.json

`mods/quickstart_programmer/manifest.json`:

```jsonc
{
  "id": "quickstart_programmer",                    // ID del mod
  "name": "My Code Mod",                            // Nombre del mod mostrado en la lista de mods
  "description": "Nueva acción y ajustes del mod",  // Descripción del mod mostrada en la lista de mods
  "sources": ["src/*.cs"],                          // Patrón glob para recopilar archivos fuente (incluye todos los archivos ".cs")
  "author": "programmer",                           // Autor
  "depends": ["core"]                               // Dependencia del juego base (así no se requiere ordenar manualmente la carga de mods)
}
```

El campo `sources` especifica qué archivos C# compilar mediante Roslyn.

---

## Paso 3: Punto de entrada — IMod

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

            GameLogger.Log("MY_MOD", "¡Mi mod cargado!", LogLevel.Message);
        }

    }
}
```

La interfaz `IMod` es el punto de entrada obligatorio para cualquier mod en C#. El método `Initialize` se llama cuando el mod se carga.

> Usa `GameLogger.Log(...)` en lugar de `GD.Print(...)` si quieres trabajar con logs.

---

## Paso 4: Ajustes del menú — IModSettings

Añade a `MyModSettings.cs`:

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

Después de cargar el mod, ve a "Ajustes → Mod Settings" — tu mod aparece con un deslizador de volumen y un interruptor.

---

## Paso 5: Acción DSL personalizada — IScenarioAction

Añadamos una nueva acción `my_effect`, usable en archivos `.scenario`. Crea `MyEffectAction.cs`:

```csharp
using Cthangover.Core.Scenarios;
using Cthangover.Core.Actions;
using Cthangover.Core.Utils;

public class MyEffectAction : IScenarioAction
{
    public string Name => "my_effect";

    public void Run(IActionContext context)
    {
        GameLogger.Log("MY_MOD", "¡Nueva acción ejecutada!", LogLevel.Message);
    }
}
```

Ahora en cualquier archivo `.scenario` puedes escribir:

```scenario
action my_effect
```

El motor encuentra tu clase mediante reflection y llama a `Execute`.

---

## Paso 6: Localización

`mods/quickstart_programmer/locale/es.properties`:

```properties
mymod/effect_volume = Volumen del efecto
mymod/enable_mod = Activar mod
```

---

## Paso 7: Comprobarlo

Inicia el juego:

1. Abre el menú de ajustes → pestaña "Mod Settings" → tu mod con deslizador y casilla.
2. Escribe un `.scenario` de prueba con el comando `action my_effect`.
3. Los logs del juego muestran `[MY_MOD] ¡Nueva acción ejecutada!`.

---

## ¿Qué sigue?

| Quieres | Ve aquí |
|---|---|
| Suscribirte a entrada/salida de escenas | [Suscripciones de Escena](site/docs/mods/configs/) |
| Crear tu propio modo de batalla | [Sistema de Batalla](site/docs/mods/battle/) |
| Hacer una ventana de herramienta | [Crear una Herramienta Personalizada](site/docs/mods/tools/tutorial) |
| Escribir mods en GDScript | [GDScript en Mods](site/docs/mods/src/gdscript/) |
| Todas las interfaces disponibles | [Código Fuente C# en Mods](site/docs/mods/src/) |

---

## Descargar el ejemplo terminado

<a href="examples/quickstart_programmer.zip" class="qs-download" download>
<svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><path d="M12 5v14"/><path d="M5 12l7 7 7-7"/><path d="M4 22h16"/></svg>
Descargar quickstart_programmer.zip
</a>

El mod descargable está en inglés.
