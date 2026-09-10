# Avatars

Character portrait images displayed during dialogue scenes, positioned via `first=` (left, player) and `second=` (right, NPC) parameters.

## Directory structure

```filestree
avatars/
└── {character_name}/
    └── {expression}.png
```

## Real example — Murakami

From `mods/murakami/avatars/murakami/`:

```filestree
murakami/
├── normal_1.png      # neutral expression
├── normal_2.png      # neutral variant
├── fear_1.png        # scared
└── fear_2.png        # terrified
```

The compile-time catalog (`Avatars.cs`) additionally lists `murakami/normal_3`. Expressions are referenced as `<character>/<expression>` without the `.png` extension in DSL parameters (`first=` / `second=`).

## Usage in scenarios

Avatars appear in dialogue lines through the `text` command:

```scenario
text "- Help me..." key=murakami/help second=murakami/fear_1
text "- What are you doing here alone?" key=murakami/alone first=marao/think
```

- `first=` — left-side speaker (usually the player)
- `second=` — right-side speaker (usually the NPC)
- The value is `{character}/{expression}` — no `.png` extension

## Adding a new avatar set

1. Create `avatars/{character_name}/` in your mod
2. Add `.png` files for each expression you need
3. The character's JSON definition sets `"Image"` to the base path:

```jsonc
{
  "Id": "Murakami",                                        // Unique character identifier
  "Image": "characters/player/murakami"                    // Base path to character textures
}
```

4. Use in scenarios: `first=murakami/normal_1` or `second=murakami/fear_1`

## Multiple characters in dialogue

The same expression naming convention works for any character:

```filestree
avatars/
├── marao/
│   ├── sad.png
│   ├── smile.png
│   └── think.png
└── murakami/
    ├── normal_1.png
    ├── normal_2.png
    ├── fear_1.png
    └── fear_2.png
```

Note that the on-disk set and the compile-time catalog may differ slightly: `Avatars.cs` lists additional marao expressions (`angry`, `what`, `relax`) and a murakami `normal_3`, while marao's `sad` exists only as a file. Always check the mod's avatar directory for what is actually available.

## Format requirements

- **Format:** PNG with transparency
- **Resolution:** 512×512 recommended, but any square power-of-two works
- **Naming:** lowercase, underscores for multi-word expressions (`fear_1`, `very_happy`)
- The extension is omitted in scenario files — the engine resolves `{character}/{expression}` to `avatars/{character}/{expression}.png`
