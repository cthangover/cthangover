# Nivel 2 — Modder: Escena en DSL

<div class="qs-meta">
<span>JSON + DSL</span>
<span>15 minutos</span>
<span>Sin código</span>
</div>

Crearás un mod con tu propia escena — una sala con diálogo, elecciones y transiciones. Además añadirás una opción "Ir a la taberna" a la escena de la cocina sin tocar los archivos originales.

---

## Paso 1: Estructura del mod

Crea la carpeta y los archivos base:

```filestree
mods/
└── quickstart_modder/
    ├── manifest.json
    ├── scenes/
    │   └── my_alko_bar.json              # ¡Nuestra nueva ubicación — el bar!
    ├── scenarios/
    │   ├── my_alko_bar.scenario          # Escenario dentro del bar, permite al jugador interactuar con la nueva ubicación
    │   └── town_entry_override.scenario  # Sobrescribe la escena del pueblo para añadir una transición a nuestra nueva ubicación
    ├── backgrounds/
    │   └── my_alko_bar/
    │       └── bg.png                    # Imagen del interior del bar
    ├── backgrounds/
    │   └── sounds/
    │       └── alko_bar_ambient.ogg      # Sonido ambiente de ruido de bar
    └── locale/
        └── ru.properties                 # Localización para ruso
```

---

## Paso 2: manifest.json

`mods/quickstart_modder/manifest.json`:

```jsonc
{
  "id": "alko_bar",                         // ID del mod
  "name": "Mi Bar",                         // Nombre del mod mostrado en el menú de mods
  "description": "Añade un bar al pueblo",  // Descripción del mod mostrada en el menú de mods
  "author": "modder",                       // Autor del mod
  "depends": ["core"]                       // Dependencia del juego base (así no se requiere ordenar manualmente la carga de mods)
}
```

---

## Paso 3: Definición de la escena

`mods/quickstart_modder/scenes/my_alko_bar.json`:

```jsonc
{
  "name": "my_alko_bar",                              // Crea un nuevo identificador de escena único (usado en switch_scene), esta será la nueva escena.
  "defaultBackground": "my_alko_bar/bg",              // Ruta al fondo PNG dentro de backgrounds/ (sin extensión)
  "defaultAmbient": "locations/alkobar/bar_ambient",  // ID del sonido ambiente para esta ubicación (sin extensión), ej. "ruido de bar" que suena al entrar en la escena
  "defaultScenario": "scenarios/my_alko_bar.scenario" // Escenario por defecto que se ejecutará en esta escena si no se especifica otra cosa
}
```

| Campo | Descripción |
|---|---|
| `name` | ID único de la escena. Usado en `switch_scene` |
| `defaultBackground` | Ruta al fondo dentro de `backgrounds/` (sin extensión) |
| `defaultAmbient` | Sonido ambiente. Cadena vacía — sin sonido |
| `defaultScenario` | Ruta al archivo `.scenario` que se ejecuta al entrar en la escena |

---

## Paso 4: Fondo

Coloca una imagen en `mods/quickstart_modder/backgrounds/my_alko_bar/bg.png`.

Cualquier imagen PNG servirá. El juego soporta iluminación por shader y hora del día — el fondo se iluminará automáticamente.

> Si no tienes una imagen, copia temporalmente `mods/core/backgrounds/home/kitchen.png` → `mods/quickstart_modder/backgrounds/my_alko_bar/bg.png`.

---

## Paso 5: Escenario DSL

`mods/quickstart_modder/scenarios/my_alko_bar.scenario`:

```scenario
scene: my_alko_bar
priority: 10
---
text "Entras en un bar medio vacío."
text "Un camarero aburrido está tras la barra."

select "¿Qué harás?"
option "Acercarte a la barra" -> :bar
option "Irte" -> :leave

:bar
text "El camarero te hace un gesto con la cabeza."
text "— ¿Qué te pongo?"
end

:leave
switch_scene town_entry
end
```

### Qué está pasando aquí

- **`text`** — muestra una línea de diálogo. El parámetro `key=` es una clave de localización.
- **`select`** — abre un menú de elección. El texto entre comillas es el aviso.
- **`option`** — una opción de elección. `-> :label` — a dónde saltar al seleccionar.
- **`:bar`**, **`:leave`** — etiquetas para saltos.
- **`switch_scene`** — mueve al jugador a otra escena.
- **`end`** — finaliza el escenario (el jugador permanece en la escena).

---

## Paso 6: Extender el pueblo (prioridad de escena)

Ahora añadimos una opción "Ir al bar" a la escena del pueblo, **sin tocar** el archivo original de `core`.

`mods/quickstart_modder/scenarios/town_entry_override.scenario`:

```scenario
scene: town_entry
priority: 1
---
select "El pueblo huele diferente hoy..."
option "Volver a casa" -> :home
option "Ir al bar" -> :alko_bar

:home
switch_scene home_outside
end

:alko_bar
switch_scene my_alko_bar
end
```

### Por qué funciona

La escena `town_entry` ahora tiene dos escenarios:
- `priority: 10` — por defecto (del mod `core`)
- `priority: 1` — el nuestro (de `quickstart_modder`)

El motor ejecuta el escenario con la **menor** `priority`. Nuestra `priority: 1` gana — el jugador ve la nueva opción.

> Más información: [Sistema de Prioridad de Escenas](site/docs/mods/scenes/).

---

## Paso 7: Comprobarlo

Inicia el juego:

1. Entra en el pueblo — ves la opción "Ir al bar".
2. Haz clic — estás en tu escena `my_alko_bar` con diálogo y elecciones.
3. Elige "Irte" — vuelves al pueblo.

---

## ¿Qué sigue?

| Quieres | Ve aquí |
|---|---|
| Dominar todos los comandos DSL | [Referencia de Comandos DSL](site/docs/mods/scenes/scenarios/dsl/) |
| Añadir objetos interactivos | [Objetos Interactivos](site/docs/mods/interactives/) |
| Escribir un mod en C# | [Nivel 3 — Programador](#quickstart/programmer) |

---

## Descargar el ejemplo terminado

<a href="examples/quickstart_modder.zip" class="qs-download" download>
<svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><path d="M12 5v14"/><path d="M5 12l7 7 7-7"/><path d="M4 22h16"/></svg>
Descargar quickstart_modder.zip
</a>

El mod descargable está en inglés.
