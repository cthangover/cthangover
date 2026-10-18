# Nivel 2 — Modder: Escena en DSL

<div class="qs-meta">
<span>JSON + DSL</span>
<span>15 minutos</span>
<span>Sin código</span>
</div>

Vas a crear un mod con tu propia escena — un bar con diálogo, opciones y transiciones. Y además añadirás una entrada al bar en la escena existente del pueblo, sin tocar los archivos originales.

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
    │   ├── my_alko_bar.scenario          # Escenario dentro del bar, para que el jugador interactúe con la nueva ubicación
    │   └── town_entry_override.scenario  # Ampliamos la escena del pueblo para añadir la transición a nuestra ubicación
    ├── backgrounds/
    │   └── my_alko_bar/
    │       └── bg.png                    # Imagen del interior del bar
    └── sounds/
        └── locations/
            └── alkobar/
                └── bar_ambient.ogg       # Sonido de fondo del bar
```

---

## Paso 2: manifest.json

`mods/quickstart_modder/manifest.json`:

```jsonc
{
  "id": "quickstart_modder",                         // ID del mod
  "name": "Mi bar",                                  // Nombre del mod que se muestra en el menú de mods
  "description": "Añade una escena de bar al pueblo", // Descripción del mod que se muestra en el menú de mods
  "author": "modder",                                // Autor del mod
  "depends": ["cthangover_game"]                   // Dependencia del juego principal (así no hay que ordenar a mano la carga del mod)
}
```

---

## Paso 3: Definición de la escena

`mods/quickstart_modder/scenes/my_alko_bar.json`:

```jsonc
{
  "name": "my_alko_bar",                              // Un identificador de escena nuevo y único (se usa en switch_scene): es nuestra futura escena
  "defaultBackground": "my_alko_bar/bg",              // Ruta al fondo PNG dentro de backgrounds/ (sin extensión)
  "defaultAmbient": "locations/alkobar/bar_ambient",  // ID del sonido ambiente de esta ubicación (ruta dentro de sounds/ sin extensión), p. ej. «ruido del bar», que suena al entrar
  "defaultScenario": "scenarios/my_alko_bar.scenario" // Escenario por defecto que se ejecuta en esta escena si no hay otro
}
```

| Campo | Significado |
|---|---|
| `name` | ID único de la escena. Se usa en `switch_scene` |
| `defaultBackground` | Ruta al fondo dentro de `backgrounds/` (sin extensión) |
| `defaultAmbient` | Sonido ambiente: ruta dentro de `sounds/` sin extensión. Cadena vacía — sin sonido |
| `defaultScenario` | Ruta al archivo `.scenario` que se ejecuta al entrar |

---

## Paso 4: Fondo y sonido

Coloca una imagen en `mods/quickstart_modder/backgrounds/my_alko_bar/bg.png` y el sonido ambiente en `mods/quickstart_modder/sounds/locations/alkobar/bar_ambient.ogg`.

Sirve cualquier imagen PNG. El juego admite iluminación por shaders y hora del día — el fondo se iluminará automáticamente.

> Si no tienes una imagen propia, copia temporalmente `mods/cthangover_game/backgrounds/home/kitchen.png` → `mods/quickstart_modder/backgrounds/my_alko_bar/bg.png`. También puedes prescindir del sonido: pon `"defaultAmbient": ""`.

---

## Paso 5: Escenario DSL

`mods/quickstart_modder/scenarios/my_alko_bar.scenario`:

```scenario
scene: my_alko_bar
priority: 10
---
text "Entras en un bar medio vacío."
text "Tras la barra, un camarero se aburre."

:menu
select "[¿Qué vas a hacer?]"
option "Acercarse a la barra" -> :bar
option "Salir" -> :leave

:bar
text "El camarero te saluda con la cabeza."
text "— ¿Qué le pongo?"
goto -> :menu

:leave
switch_scene town_entry
end
```

### Qué está pasando aquí

- **`scene:`** / **`priority:`** — la cabecera: a qué escena pertenece el escenario y en qué orden se ejecuta. Se separa del cuerpo con una línea `---`.
- **`text`** — muestra una línea de diálogo. El parámetro `key=` es una clave de localización (aquí no hace falta: sin ella se muestra el propio texto).
- **`select`** — abre un menú de opciones. El texto entre comillas es el enunciado.
- **`option`** — una opción. `-> :label` — a dónde saltar al elegirla.
- **`:menu`**, **`:bar`**, **`:leave`** — etiquetas de salto.
- **`goto -> :menu`** — salto incondicional: después de hablar con el camarero, el menú se abre de nuevo.
- **`switch_scene`** — lleva al jugador a otra escena.
- **`end`** — termina el escenario.

---

## Paso 6: Extender el pueblo (prioridad de escena)

Ahora añadiremos una entrada al bar en la escena del pueblo, **sin tocar** los archivos originales de `cthangover_game`.

`mods/quickstart_modder/scenarios/town_entry_override.scenario`:

```scenario
scene: town_entry
priority: 1
---
select "Por la puerta entreabierta del bar sale música..."
option "Entrar en el bar" -> :alko_bar
option "Pasar de largo" -> :pass

:alko_bar
switch_scene my_alko_bar
end

:pass
end
```

### Por qué funciona

La escena `town_entry` tiene ahora varios escenarios:

| priority | Escenario | Mod |
|---|---|---|
| `1` | nuestro `town_entry_override` | `quickstart_modder` |
| `10`–`30` | el encuentro con Murakami (solo mientras se cumplen sus `condition:`) | `cthangover_murakami` |
| `40` | el menú estándar del pueblo | `cthangover_game` |

Al entrar en una escena, el motor ejecuta el primer escenario apto en orden ascendente de `priority` — empezando por el **menor**. Cuando un escenario termina sin cambiar de escena, se inicia el siguiente. Por eso:

- «Entrar en el bar» — `switch_scene` lleva al jugador al bar;
- «Pasar de largo» — nuestro escenario simplemente termina (`end`) y todo sigue como en el juego original: la escena de la historia de Murakami o el menú estándar del pueblo.

Así el mod añade algo nuevo sin romper nada. Si nuestro escenario dibujara él mismo el menú del pueblo, taparía tanto la historia como otros mods.

> Más información: [Scene Priority System](site/docs/mods/scenes/).

---

## Paso 7: Conectar el mod al juego

El juego no carga todos los mods instalados, solo los que forman parte de la colección del juego. Añade tu mod (la interfaz del juego está en inglés y ruso):

1. En el menú principal abre **«Mods & Collections»** → pestaña **«Collections»**.
2. En la tarjeta del juego **Cthangover** pulsa **«Edit scheme»**.
3. Selecciona cualquier mecánica del esquema (por ejemplo, **«Story»**) y pulsa **«Add mod»**.
4. Busca tu mod en la lista (por el nombre de `manifest.json`) y pulsa **«Add»**.
5. Pulsa **«Save»** y confirma. Los cambios se aplican la próxima vez que se inicie el juego.

---

## Paso 8: Comprobarlo

Reinicia el juego:

1. Ve al pueblo — lo primero que verás es la elección «Entrar en el bar» / «Pasar de largo».
2. Pulsa «Entrar en el bar» — estás en tu escena `my_alko_bar` con fondo, ruido de bar, diálogo y opciones.
3. Elige «Salir» — vuelves al pueblo.
4. Elige «Pasar de largo» — el juego continúa como siempre.

> **Para desarrolladores.** Desde el código fuente puedes abrir la escena directamente, sin jugar el prólogo: `godot --path . -- --game=cthangover --new --with-mods=quickstart_modder --scene=town_entry`.

---

## ¿Qué sigue?

| Quieres | Ve a |
|---|---|
| Dominar todos los comandos DSL | [DSL Command Reference](site/docs/mods/scenes/scenarios/dsl/) |
| Añadir objetos interactivos | [Interactive Objects](site/docs/mods/interactives/) |
| Escribir un mod en C# | [Nivel 3 — Programador](#quickstart/programmer) |

---

## Descargar el ejemplo terminado

<a href="examples/quickstart_modder.zip" class="qs-download" download>
<svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><path d="M12 5v14"/><path d="M5 12l7 7 7-7"/><path d="M4 22h16"/></svg>
Descargar quickstart_modder.zip
</a>

El texto de los diálogos del mod descargable está en ruso. Descomprime el archivo en `mods/quickstart_modder/` o coloca el `.zip` en `user://mods/` y después conecta el mod al juego (paso 7).
