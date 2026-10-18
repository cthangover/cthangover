# Nivel 1 — Jugador: Modificando un Personaje

<div class="qs-meta">
<span>Editor de texto</span>
<span>5 minutos</span>
<span>Sin código</span>
</div>

Vas a potenciar al personaje Marao — aumentar su salud y ataque, y añadirle la habilidad «Aturdir» — mediante el sistema de parches. Ni una sola línea de código, solo JSON.

---

## Paso 1: Crear la carpeta del mod

Cada mod vive en su propia carpeta dentro de `mods/` (junto al juego) o de `user://mods/` (la carpeta de datos del usuario; en Windows — `%APPDATA%\cthangover\mods`). Crea:

```filestree
mods/
└── quickstart_player/
    ├── manifest.json
    └── patches/
        └── character.json
```

---

## Paso 2: manifest.json

Todo mod necesita un manifiesto. Crea `mods/quickstart_player/manifest.json`:

```jsonc
{
  "id": "quickstart_player",        // Identificador único del mod, escríbelo en inglés
  "name": "Marao Buff",             // Nombre que se muestra en el menú de gestión de mods
  "description": "¡Mi primer mod!", // Descripción visible en la lista de mods y en el catálogo
  "author": "player",               // Autor del mod (cadena arbitraria)
  "depends": ["cthangover_game"]  // Dependencia del juego principal (así no hay que ordenar a mano la carga del mod)
}
```

El archivo de manifiesto es obligatorio — un mod sin él se omite —, pero ningún campo es estrictamente obligatorio: si falta `id`, el nombre de la carpeta se usa como identificador. Aun así, se recomienda rellenar `id` y `name`. El juego encuentra la carpeta del mod por sí mismo, pero el mod solo entra en el juego después de conectarlo (paso 4).

---

## Paso 3: Parche de personaje

Crea `mods/quickstart_player/patches/character.json`:

```jsonc
{
  "Items": [
    {
      "Id": "Marao", // ID del personaje a parchear (definido en el mod principal)

      "Health": 5000, // Nuevos valores de propiedades — sobrescriben los originales
      "Attack": 1200,
      "Point": 2,

      "Actions$add": ["physics/stun"] // $add añade elementos a un array
    }
  ]
}
```

### Cómo funciona

- **`"Id": "Marao"`** — el objetivo del parche: el personaje con identificador `Marao` (definido en el mod `cthangover_game`).
- **`"Health": 5000`**, **`"Attack": 1200`** y **`"Point": 2`** — sobrescriben los campos del personaje. Los valores nuevos reemplazan a los antiguos.
- **`"Actions$add": ["physics/stun"]`** — el sufijo `$add` significa «añadir elementos al array». La acción `physics/stun` se añade a la lista. Las acciones por defecto `physics/attack` y `physics/defence` se mantienen. Aturdir cuesta 2 PA — por eso también subimos `Point`.

> Más sobre parches: [Patches](site/docs/mods/patches/) y [Array Operations](site/docs/mods/patches/arrays-and-identity).

---

## Paso 4: Conectar el mod al juego

El juego no carga todos los mods instalados, solo los que forman parte de la colección del juego. Añade tu nuevo mod una vez (la interfaz del juego está en inglés y ruso):

1. En el menú principal abre **«Mods & Collections»** → pestaña **«Collections»**.
2. En la tarjeta del juego **Cthangover** pulsa **«Edit scheme»**.
3. Selecciona cualquier mecánica del esquema (por ejemplo, **«Story»**) y pulsa **«Add mod»**.
4. Busca tu mod en la lista (por el nombre de `manifest.json`) y pulsa **«Add»**.
5. Pulsa **«Save»** y confirma. Los cambios se aplican la próxima vez que se inicie el juego.

> **Para desarrolladores.** Al ejecutar desde el código fuente puedes conectar el mod sin el menú, con un flag: `godot --path . -- --game=cthangover --new --with-mods=quickstart_player`.

---

## Paso 5: Ejecutar y comprobar

Reinicia el juego y empieza una partida nueva. Entra en cualquier combate — ahora Marao tiene:

- **5000 HP** en lugar de 16
- **1200 ATK** en lugar de 5
- **2 PA** en lugar de 1
- Una tercera acción, **«Stun»** (Aturdir), junto a «Basic attack» y «Defence»

---

## ¿Qué sigue?

| Quieres | Ve a |
|---|---|
| Crear tu propia escena con diálogo | [Nivel 2 — Modder](#quickstart/modder) |
| Entender los parches a fondo | [Patches](site/docs/mods/patches/) |
| Aprender sobre personajes | [Characters](site/docs/mods/characters/) |
| Escribir tu primer mod en C# | [Nivel 3 — Programador](#quickstart/programmer) |

---

## Descargar el ejemplo terminado

<a href="examples/quickstart_player.zip" class="qs-download" download>
<svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><path d="M12 5v14"/><path d="M5 12l7 7 7-7"/><path d="M4 22h16"/></svg>
Descargar quickstart_player.zip
</a>

El mod descargable está en inglés. Para instalarlo, descomprime el archivo en la carpeta `mods/quickstart_player/` del juego — o coloca el propio `.zip` en `user://mods/`: el juego lee los mods directamente desde archivos comprimidos, y los mods de `user://mods/` tienen prioridad sobre los mods integrados con el mismo id. Después de instalarlo, conecta el mod al juego (paso 4).
