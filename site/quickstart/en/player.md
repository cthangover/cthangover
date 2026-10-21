# Level 1 — Player: Patching a Character

<div class="qs-meta">
<span>Text editor</span>
<span>5 minutes</span>
<span>No code</span>
</div>

You'll buff the character Marao — increase health and attack, and add the "Stun" ability — using the patch system. Not a single line of code, just JSON.

---

## Step 1: Create the mod folder

Every mod lives in its own folder inside `mods/` (next to the game) or `user://mods/` (the user data folder; on Windows — `%APPDATA%\cthangover\mods`). Create:

```filestree
mods/
└── quickstart_player/
    ├── manifest.json
    └── patches/
        └── character.json
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
  "depends": ["cthangover_game"]  // Dependency on the main game's content (so manual mod load ordering is not required)
}
```

The manifest file itself is required — a mod without one is skipped — but no single field is mandatory: if `id` is omitted, the folder name is used as the mod id. Filling in `id` and `name` is still recommended. The game finds the mod folder by itself, but the mod only gets into the game after you connect it (step 4).

---

## Step 3: Character patch

Create `mods/quickstart_player/patches/character.json`:

```jsonc
{
  "Items": [
    {
      "Id": "Marao", // Character ID to patch (defined in the core mod)

      "Health": 5000, // New property values — overwrite the originals
      "Attack": 1200,
      "Point": 2,

      "Actions$add": ["physics/stun"] // $add appends elements to an array
    }
  ]
}
```

### How it works

- **`"Id": "Marao"`** — the patch target: character with identifier `Marao` (defined in the `cthangover_game` mod).
- **`"Health": 5000`**, **`"Attack": 1200`**, **`"Point": 2`** — overwrite the character's fields. New values replace old ones.
- **`"Actions$add": ["physics/stun"]`** — the `$add` suffix means "append to the array". The action `physics/stun` is added to the list. The default `physics/attack` and `physics/defence` remain. Stun costs 2 AP — that's why we raised `Point` too.

> Learn more about patches: [Patches](site/docs/mods/patches/) and [Array Operations](site/docs/mods/patches/arrays-and-identity).

---

## Step 4: Connect the mod to the game

The game does not load every installed mod — only the mods that belong to the game's collection. Add your new mod to it once:

1. In the main menu, open **"Mods & Collections"** → the **"Collections"** tab.
2. On the **Cthangover** game card, click **"Edit scheme"**.
3. Select any mechanic on the scheme (for example, **"Story"**) and click **"Add mod"**.
4. Find your mod in the list (by the name from `manifest.json`) and click **"Add"**.
5. Click **"Save"** and confirm. Changes apply the next time the game starts.

> **For developers.** When running from source, you can connect the mod without the menu, with a flag: `godot --path . -- --game=cthangover --new --with-mods=quickstart_player`.

---

## Step 5: Run and check

Restart the game and start a new one. Enter any battle — Marao now has:

- **5000 HP** instead of 16
- **1200 ATK** instead of 5
- **2 AP** instead of 1
- A third action, **"Stun"**, next to "Basic attack" and "Defence"

---

## What's next?

| Want to | Go to |
|---|---|
| Create your own scene with dialogue | [Level 2 — Modder](#quickstart/modder) |
| Understand patches in depth | [Patches](site/docs/mods/patches/) |
| Learn about characters | [Characters](site/docs/mods/characters/) |
| Write your first C# mod | [Level 3 — Programmer](#quickstart/programmer) |

---

## Download the finished example

<a href="examples/quickstart_player.zip" class="qs-download" download>
<svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><path d="M12 5v14"/><path d="M5 12l7 7 7-7"/><path d="M4 22h16"/></svg>
Download quickstart_player.zip
</a>

The downloadable mod is in English. To install it, unpack the archive into the game's `mods/quickstart_player/` folder — or put the `.zip` itself into `user://mods/`: the game reads mods straight from archives, and mods in `user://mods/` take precedence over built-in mods with the same id. After installing, connect the mod to the game (step 4).
