# Level 1 — Player: Patching a Character

<div class="qs-meta">
<span>Text editor</span>
<span>5 minutes</span>
<span>No code</span>
</div>

You'll buff the character Marao — increase health, attack, and add the "Stun" ability — using the patch system. Zero code, just JSON.

---

## Step 1: Create the mod folder

Every mod lives in its own folder inside `mods/`. Create:

```filestree
mods/
└── quickstart_player/
    ├── manifest.json
    └── patches/
        └── characters.json
```

---

## Step 2: manifest.json

Every mod needs a manifest. Create `mods/quickstart_player/manifest.json`:

```jsonc
{
  "id": "quickstart_player",        // Unique mod identifier, use English
  "name": "Marao Buff",             // Display name shown in the mod management menu
  "description": "My first mod!",   // Description visible in the mod list and catalog
  "author": "player",               // Mod author (arbitrary string)
  "depends": ["core"]               // Dependency on the core game (so manual mod load ordering is not required)
}
```

A minimal manifest only needs `name`, but it's better to fill in `id` as well. The kernel auto-discovers the folder and loads it as a mod.

---

## Step 3: Character patch

Create `mods/quickstart_player/patches/characters.json`:

```jsonc
{
  "Items": [
    {
      "Id": "Marao", // Character ID to patch (defined in the core mod)

      "Health": 5000, // New property values — overwrite the originals
      "Attack": 1200,
      "Points": 2,

      "Actions$add": ["physics/stun"] // $add appends elements to an array
    }
  ]
}
```

### How it works

- **`"Id": "Marao"`** — the patch target: character with identifier `Marao` (defined in the `core` mod).
- **`"Health": 5000`**, **`"Attack": 1200`**, **`"Points": 2`** — overwrite the character's fields. New values replace old ones.
- **`"Actions$add": ["physics/stun"]`** — the `$add` suffix means "append to the array". The action `physics/stun` is added to the list. The default `physics/attack` and `physics/defence` remain.

> Learn more about patches: [Patches](site/docs/mods/patches/) and [Array Operations](site/docs/mods/patches/arrays-and-identity).

---

## Step 4: Run and check

Start the game. Enter any battle — Marao now has:

- **5000 HP** instead of 10
- **1200 ATK** instead of 5
- **2 AP** instead of 1
- A third action **"Stun"** alongside "Attack" and "Defence"

---

## What's next?

| Want to | Go here |
|---|---|
| Create your own scene with dialogue | [Level 2 — Modder](#quickstart/modder) |
| Understand the patch system deeper | [Patches](site/docs/mods/patches/) |
| Learn about characters | [Characters](site/docs/mods/characters/) |
| Write your first C# mod | [Level 3 — Programmer](#quickstart/programmer) |

---

## Download the finished example

<a href="examples/quickstart_player.zip" class="qs-download" download>
<svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><path d="M12 5v14"/><path d="M5 12l7 7 7-7"/><path d="M4 22h16"/></svg>
Download quickstart_player.zip
</a>

The downloadable mod is in English.
