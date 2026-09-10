# Characters

JSON-defined characters — both player-controlled and enemies — with attributes, actions, skills, and loot tables.

## JSON structure

```jsonc
{
  // Unique character identifier — used in scenario conditions and battle init
  "Id":          "unique_id",
  // Locale key resolved from .properties files for the display name
  "Name":        "locale_key",
  // Path to the portrait sprite inside the mod's characters/ group
  // (extension optional)
  "Image":       "path/in/characters/group",
  // AI behaviour strategy: "base" for random actions, "boss" or any custom AI type
  "Behaviour":   "base",

  // Character level (determines scaling)
  "Level":       1,
  // Action points available per turn
  "Point":       1,
  // Maximum hit points (coerced to minimum 1)
  "Health":      2000,
  // Base defence stat (reduces incoming damage)
  "Defence":     0,
  // Base attack stat (physical damage)
  "Attack":      500,
  // Experience points rewarded on defeat
  "Exp":         5,
  // % chance to recruit after defeating this character (0–100)
  "RecruitmentChance": 0,

  // Action IDs available to this character; supports $add, $remove, $set in patches
  "Actions":     ["physics/attack", "physics/defence"],
  // Loot table — items dropped on defeat; patches match by ItemId for per-field merge
  "Loot": [
    {
      // ID of the item to drop (references an item definition)
      "ItemId":      "food/wolf_meat",
      // Minimum number of this item to drop
      "MinCount":    1,
      // Maximum number of this item to drop
      "MaxCount":    1,
      // Drop probability (0–100)
      "Probability": 100
    }
  ],

  // Any other keys — e.g. custom stats — are captured as extension data
  // (see "Extra JSON keys" below). They do NOT become typed attributes.
  "MyMod/Luck":  7
}
```

## Field reference

| Field | Type | Description |
|---|---|---|
| `Id` | string | Unique character ID used in scenario conditions and battle init |
| `Name` | string | Locale key for display name (e.g. `characters/enemies/wolf`) |
| `Image` | string | Path to the portrait sprite **inside the mod's `characters` texture group** — extension optional |
| `Behaviour` | string | AI behaviour class: `"base"` for random actions |
| `Level` | int | Character level |
| `Point` | int | Action points per turn (coerced to minimum 1) |
| `Health` | int | Maximum HP (coerced to minimum 1) |
| `Defence` | int | Base defence stat |
| `Attack` | int | Base attack stat |
| `Exp` | int | XP reward on defeat |
| `RecruitmentChance` | int | % chance to recruit after defeat (0–100) |
| `Actions` | array of string | Action IDs (e.g. `["physics/attack", "physics/defence"]`). Supports `$add`, `$remove`, `$set` in patches. Unknown IDs are silently skipped |
| `Loot` | array of objects | Item drops. Identity key: **`ItemId`** — patches match by this field for per-field merge |
| *(any other key)* | any | Captured as extension data into the character's `Properties` bag — see below |

### Image resolution

`CharacterFactory.LoadImage` resolves the `Image` value via
`TextureUtils.LoadFromModGroup("characters", imagePath)` — the sprite must live
inside the mod's **`characters/`** group (e.g. `characters/enemies/wolf_1.png`
for `"Image": "enemies/wolf_1"`). The leading `characters/` prefix may be
included in the value; it is stripped during normalization, and the file
extension can be omitted.

The separate `avatars/` group belongs to `AvatarFactory` (dialog portraits) and
is **not** consulted for this field — see [Avatars](../avatars/) for that system.

### Extra JSON keys

Only `Health`, `Defence`, `Attack`, and `Point` are typed battle attributes
(wrapped in `Attribute` — see [Attribute model](#attribute-model)). Every other
JSON key that does not match a DTO field (`Strength`, `Magic`, `Fullness`,
`Depravity`, `Discipline`, or anything custom) lands in
`CharacterInfo.ExtraProperties` via `[JsonExtensionData]` and is imported into
the character's `CharacterAttributes.Properties` string-keyed property bag
(`PropertyData.ImportJsonProperties`).

Be honest with yourself about what this gives you:

- **Numbers** import as int (or float when fractional), **booleans** as bool,
  **strings** as strings; arrays and objects are stored as their raw JSON text.
- **Core code never reads `Properties`.** Keys such as `Strength` or `Fullness`
  are inert data unless your own mod code consumes them. They are *not* typed
  attributes and take no part in battle math, hunger, or recruiting unless you
  implement that logic yourself (e.g. in a scenario action or behaviour class).

## Real examples

### Player character — `core/characters/player/marao.json`

```jsonc
{
  // Player character ID — referenced throughout the game
  "Id": "Marao",
  // Locale key for the character's display name
  "Name": "characters/player/marao",
  // Portrait sprite under characters/player/marao.png
  "Image": "characters/player/marao",
  // Starting level
  "Level": 1,
  // Action points per turn
  "Point": 1,
  // Base health pool
  "Health": 16,
  // Base defence
  "Defence": 0,
  // Base attack damage
  "Attack": 5,
  // Available combat actions
  "Actions": ["physics/attack", "physics/defence"]
}
```

### Enemy — `core/characters/enemies/wolf_1.json`

```jsonc
{
  // Unique enemy ID for this wolf variant
  "Id": "wolf_1",
  // Locale key for the display name
  "Name": "characters/enemies/wolf",
  // Portrait sprite under characters/enemies/wolf_1.png
  "Image": "characters/enemies/wolf_1",
  // AI behaviour: "base" picks random actions
  "Behaviour": "base",
  // Enemy level
  "Level": 1,
  // Action points per turn
  "Point": 1,
  // Hit points (low — this is a weak enemy)
  "Health": 15,
  // Base defence
  "Defence": 0,
  // Base attack damage
  "Attack": 4,
  // XP granted on defeat
  "Exp": 5,
  // Cannot be recruited
  "RecruitmentChance": 0,
  // Combat actions
  "Actions": ["physics/attack", "physics/defence"],
  // Loot table — drops wolf meat and branches
  "Loot": [
    { "ItemId": "food/wolf_meat", "MinCount": 1, "MaxCount": 1, "Probability": 100 },
    { "ItemId": "resource/branches", "MinCount": 1, "MaxCount": 1, "Probability": 100 }
  ]
}
```

### Boss enemy — `core/characters/enemies/werewolf_girl_1.json`

```jsonc
{
  // Unique boss ID
  "Id": "werewolf_girl_1",
  // Locale key for display name
  "Name": "characters/enemies/werewolf_girl",
  // Boss level
  "Level": 1,
  // Hit points (higher than regular enemies)
  "Health": 25,
  // Attack damage (stronger than regular enemies)
  "Attack": 8,
  // XP reward (significantly higher than regular enemies)
  "Exp": 50,
  // 50% chance to recruit after defeat — see [Recruiting](../recruiting/)
  "RecruitmentChance": 50,
  // Combat actions
  "Actions": ["physics/attack", "physics/defence"],
  // Loot table — drops 2 wolf meat at 100% probability
  "Loot": [
    { "ItemId": "food/wolf_meat", "MinCount": 2, "MaxCount": 2, "Probability": 100 }
  ]
}
```

## Battle actions (ActionCharacter)

Every entry in a character's `Actions` array references an **ActionCharacter**
definition (`Core/Characters/ActionCharacter.cs`). Actions define what a
character can do in battle — attack, defend, stun, heal — together with their
targeting category and free-form parameters.

Actions are collected from the same **`characters/`** JSON group as characters
themselves (`ActionCharacterFactory` calls `JsonGroup.Collect<ActionCharacterInfo>("characters")`),
so action definitions live anywhere under your mod's `characters/` folder using
the usual `{ "Items": [...] }` envelope:

```jsonc
{
  // Wrapping array — action definitions are listed under Items
  "Items": [{
    // Unique action ID — referenced by characters' "Actions" arrays
    "Id": "mymod/fireball",
    // Locale key for the name shown in the battle action menu
    "Name": "characters/action/fireball/name",
    // Locale key for the tooltip text
    "Description": "characters/action/fireball/description",
    // Targeting category: "ToSelf", "ToAlias" (allies), or "ToEnemy"
    "Type": "ToEnemy",
    // Icon sprite path, resolved in the characters texture group
    "Image": "characters/actions/fireball",
    // Flat list of "Key=Value" strings parsed into the Properties bag;
    // everything before the first '=' is the key, the rest is the value
    "Values": [
      "RequiredPoint=2",
      "Attack=3.5",
      "AttackType=Fire"
    ]
  }]
}
```

| Field | Description |
|---|---|
| `Id` | Unique action ID, used as the factory lookup key |
| `Name` | Locale key for the display name in the battle action menu |
| `Description` | Locale key for the tooltip |
| `Type` | Targeting category — `ToSelf` (caster), `ToAlias` (allies), `ToEnemy` (opponents) |
| `Image` | Icon sprite resolved via `TextureUtils.LoadFromModGroup("characters", ...)` |
| `Values` | List of `"Key=Value"` strings populating the `Properties` bag |

There are no reserved parameter keys at the definition level — core executors
read whatever keys they care about (the stock physics actions use
`RequiredPoint`, `Attack`, `AttackType`, `Defence`, `Turn`), and custom C#
executors (`IActionExecutor`) can read their own via the `GetStr` / `GetInt` /
`GetFloat` / `GetBool` helpers on `ActionCharacter`.

The core mod keeps its actions in `characters/actions/*.json` registered through
a `characters/actions.json` index with `${}` includes:

```jsonc
{ "Items": [
    ${actions/physics_attack},
    ${actions/physics_defence},
    ${actions/physics_stun}
]}
```

`ActionCharacterFactory.Get(id)` returns a **copy** of the cached definition on
every call — action cards carry mutable battle state (cooldowns, modifiers), so
character instances never share one instance.

## Attribute model

The stat block (`CharacterAttributes`, `Core/Characters/CharacterAttributes.cs`)
has exactly four attributes, each wrapped in an `Attribute` object:

| Attribute | Meaning |
|---|---|
| `Health` | Hit points (`Value` = current, `BaseValue` = max) |
| `Defence` | Reduces incoming damage |
| `Attack` | Base damage dealt |
| `Point` | Action points consumed by abilities |

`Attribute` (`Core/Characters/Attribute.cs`) mechanics worth knowing when
writing battle code:

- `Value` — current value; setting it fires `OnChange`.
- `BaseValue` — unmodified maximum.
- `Percent` — `Value / BaseValue` ratio for health bars (0 when `BaseValue` is 0).
- `Init(int)` — sets `Value` and `BaseValue` to the same starting value.
- `Copy()` — clones `Value`/`BaseValue`; **`OnChange` subscribers are not copied**.
- `OnChange` — a `ChangeAttribute` delegate (`void(float value, float baseValue)`).
  It fires on **every** `Value` setter call, even when the value didn't change —
  treat it as a notification mechanism, not a diff detector (a fizzle-heal at
  full health still triggers UI feedback).

Battle actions report their outcome as a `ChangedAttributes` struct
(`Source` / `Target` — `CharacterSet` snapshots of both sides after the exchange,
plus a `bool Result` success flag). This is what `IActionExecutor.Execute(action, user, target)`
returns; the executor hub matches executors to actions by `ActionId`.

## Factory behavior

- **Minimum coercion** — `CharacterFactory.CreateCharacter` coerces `Health` and
  `Point` to a minimum of 1 (`Health > 0 ? Health : 1`, same for `Point`);
  `Defence` and `Attack` are taken as-is, including 0.
- **Copies on read** — `CharacterFactory.Get(id)` returns a deep copy per call
  (`Character.Copy()`): `Attributes` are cloned and `StatusEffectQueue` is
  re-owned by the new instance, so battle mutations never leak into templates.
- **Unknown action IDs** are dropped silently when resolving the `Actions` list.

## Runtime members worth knowing

| Member | Description |
|---|---|
| `Character.Frame` | Small portrait `Texture2D` for list views (party roster, battle HUD). Present on the model but **not populated from JSON** by the factory |
| `Character.Exp` | Current experience points, loaded from the template and carried into party data on recruitment |
| `Character.StatusEffectQueue` | Queue of active status effects owned by the character; `Copy()` re-binds the clone to its new owner |
| `Character.Copy()` | Deep clone used everywhere the engine hands out characters (see above) |
| `CharacterData.Create(characterType)` | Builds runtime `CharacterInfoData` from the factory template. For an **unknown ID** it does not throw — it returns a fallback entry with default attributes at level 0 |

## Character index files

The root `characters.json` registers characters using a `$ref` syntax:

```jsonc
{
  // $ref entries — each ${...} path expands to characters/{path}.json and its contents are merged in
  "Items": [
    "${player/marao}"
  ]
}
```

`${player/marao}` references `characters/player/marao.json` — its contents are merged in.

## Adding a new character from scratch

1. Create `characters/{type}/{id}.json` with all required fields
2. Add it to the index: `"${type/id}"` in `characters/characters.json`
3. Put the portrait sprite inside your mod's `characters/` group so the `Image` path resolves (extension optional); dialog portraits, if needed, go separately under `avatars/{id}/` — see [Avatars](../avatars/)
4. Register locale keys for the name in `locale/en.properties`:
```
characters/{type}/{id} = My Character Name
```
5. Reference in battle init: `action battle.init enemies=my_char_id`
