# Nivel 1 — Jugador: Modificando un Personaje

<div class="qs-meta">
<span>Editor de texto</span>
<span>5 minutos</span>
<span>Sin código</span>
</div>

Mejorarás al personaje Marao — aumentarás su salud, ataque y añadirás la habilidad "Aturdir" — usando el sistema de parches. Cero código, solo JSON.

---

## Paso 1: Crear la carpeta del mod

Cada mod vive en su propia carpeta dentro de `mods/`. Crea:

```filestree
mods/
└── quickstart_player/
    ├── manifest.json
    └── patches/
        └── characters.json
```

---

## Paso 2: manifest.json

Todo mod necesita un manifiesto. Crea `mods/quickstart_player/manifest.json`:

```jsonc
{
  "id": "quickstart_player",        // Identificador único del mod, usa inglés
  "name": "Mejora de Marao",        // Nombre mostrado en el menú de gestión de mods
  "description": "¡Mi primer mod!", // Descripción visible en la lista de mods y catálogo
  "author": "player",               // Autor del mod (cadena arbitraria)
  "depends": ["core"]               // Dependencia del juego base (así no se requiere ordenar manualmente la carga de mods)
}
```

Un manifiesto mínimo solo necesita `name`, pero es mejor completar también `id`. El kernel detecta automáticamente la carpeta y la carga como mod.

---

## Paso 3: Parche de personaje

Crea `mods/quickstart_player/patches/characters.json`:

```jsonc
{
  "Items": [
    {
      "Id": "Marao", // ID del personaje a parchear (definido en el mod core)

      "Health": 5000, // Nuevos valores — sobrescriben los originales
      "Attack": 1200,
      "Points": 2,

      "Actions$add": ["physics/stun"] // $add añade elementos a un array
    }
  ]
}
```

### Cómo funciona

- **`"Id": "Marao"`** — el objetivo del parche: personaje con identificador `Marao` (definido en el mod `core`).
- **`"Health": 5000`**, **`"Attack": 1200`**, **`"Points": 2`** — sobrescriben los campos del personaje. Los nuevos valores reemplazan a los antiguos.
- **`"Actions$add": ["physics/stun"]`** — el sufijo `$add` significa "añadir al array". La acción `physics/stun` se agrega a la lista. Las acciones por defecto `physics/attack` y `physics/defence` permanecen.

> Más sobre parches: [Patches](site/docs/mods/patches/) y [Operaciones con Arrays](site/docs/mods/patches/arrays-and-identity).

---

## Paso 4: Ejecutar y comprobar

Inicia el juego. Entra en cualquier batalla — Marao ahora tiene:

- **5000 HP** en lugar de 10
- **1200 ATK** en lugar de 5
- **2 PA** en lugar de 1
- Una tercera acción **"Aturdir"** junto a "Atacar" y "Defender"

---

## ¿Qué sigue?

| Quieres | Ve aquí |
|---|---|
| Crear tu propia escena con diálogo | [Nivel 2 — Modder](#quickstart/modder) |
| Profundizar en el sistema de parches | [Patches](site/docs/mods/patches/) |
| Aprender sobre personajes | [Characters](site/docs/mods/characters/) |
| Escribir tu primer mod en C# | [Nivel 3 — Programador](#quickstart/programmer) |

---

## Descargar el ejemplo terminado

<a href="examples/quickstart_player.zip" class="qs-download" download>
<svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><path d="M12 5v14"/><path d="M5 12l7 7 7-7"/><path d="M4 22h16"/></svg>
Descargar quickstart_player.zip
</a>

El mod descargable está en inglés.
