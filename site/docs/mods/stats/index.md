# Stats, classes, checks and gear

Mods `stats_base`, `stat_checks`, `equipment`, `battle_formulas`; rules as data in `classic_rpg_pack`;
the sample game `rpg_plus`.

A stat is whatever your game counts: strength in an RPG, courage or trust in a visual novel. Nothing here
knows about tile maps, cards or novels, so the same character data works in a dialog, on a tile map and in
both battle cores. Everything below is JSON and scenario text; no C# is needed.

## Stats (`"Kind": "stat"`)

```jsonc
{
  "Kind": "stat",
  "Items": [
    // A base attribute: a number with a default and a lower bound
    { "Id": "str", "Name": "my/stat/str", "Short": "my/stat/str_short", "Group": "attributes", "Order": 1, "Default": 5, "Min": 1 },
    // A derived stat: a formula of other stats and the level, written into the battle's health
    { "Id": "hp", "Name": "my/stat/hp", "Group": "combat", "Order": 10, "Formula": "12 + vit * 3 + level * 2", "Min": 1, "Binds": "health" },
    // A pool: spent and refilled (mana). Battle actions pay with "Cost=mp:4"
    { "Id": "mp", "Name": "my/stat/mp", "Group": "combat", "Formula": "int * 2 + level * 2", "Pool": true }
  ]
}
```

`Binds` writes the stat into a battle attribute every core already reads: `health`, `attack`, `defence`,
`point`. `Group` and `Order` lay the stat out on the character sheet. `Hidden` keeps it off the sheet.

## Classes (`"Kind": "archetype"`)

An RPG class or a novel's temperament: stat values or formulas, traits, and actions learned on level-up.

```jsonc
{
  "Id": "classic/warrior",
  "Name": "classic/archetype/warrior",
  "Stats": { "str": "7 + level * 2", "vit": "6 + level * 2", "int": "2 + floor(level / 2)" },
  "Traits": [{ "Stat": "hp", "Percent": 10 }],
  "Learnings": [{ "Level": 1, "Action": "classic/power_strike" }, { "Level": 3, "Action": "classic/guard_break" }]
}
```

A character's own value wins over its class, the class over the stat's `Formula`, the formula over
`Default`. Then come permanent bonuses and traits: `(value + Add + Formula) × (1 + Percent / 100)`, clamped
to `Min`/`Max`.

## Traits on anything

Any entity can carry `"Traits"`: a character, an item, a class. The characters and items mods do not know
the key, so other mods' entities become stat-aware without changing their code. A patch gives stats to
someone else's character:

```jsonc
// patches/character.json of your mod: the wolf of another mod fights with a sheet
{ "Items": [{ "Id": "rpg_wolf", "Stats": { "str": 5, "vit": 5, "agi": 7 } }] }
```

## Formulas

`+ - * / % ^`, comparisons, `&& || !`, `cond ? a : b`, `min max floor ceil round abs sqrt clamp rand(lo, hi)
chance(p) if(c, a, b)`. Division by zero gives 0 and an unknown name reads 0, so a typo never crashes a
battle. The validator and the Data Editor window report it instead.

## Gear (`equipment`)

```jsonc
// Slots are data: what equip types a slot accepts
{ "Kind": "equip_slot", "Items": [{ "Id": "weapon", "Name": "classic/slot/weapon", "Accepts": ["sword", "staff", "dagger"] }] }

// An item becomes gear by its keys: "Equip" (its type), "Traits", "Archetypes" (who may wear it)
{ "Id": "classic/iron_sword", "Cost": 30, "ItemType": "TargetUsed", "ItemAction": "action/equip",
  "Equip": "sword", "Archetypes": ["classic/warrior", "classic/rogue"], "Traits": [{ "Stat": "atk", "Add": 5 }] }
```

`"ItemAction": "action/stat_up"` together with `"StatUp": { "str": 1 }` makes a tonic that adds permanent
bonuses.

## Skills with formulas (`battle_formulas`)

```jsonc
{ "Kind": "action", "Id": "classic/power_strike", "Type": "ToEnemy",
  "Values": ["RequiredPoint=1", "Formula=a.atk * 1.5 + rand(0, a.str / 2)"] }
```

`a.*` is the one acting and `b.*` the target: their stats plus `hp`, `hp_max`, `attack`, `defence`, `point`.
Other values: `Effect=damage|heal`, `Cost=mp:4`, `Pierce=1`, and `State=<status effect>` with
`StateTurns`/`StateChance`. Both battle cores run these skills.

## In scenarios

```text
action stat.add stat=cha value=1 who=player
action stat.archetype id=classic/rogue who=rpg_hero
action stat.exp value=20                       # the whole party by default
action equip.put item=classic/dagger who=rpg_hero give=1

check cha 10 who=rpg_hero else=snub -> :gift   # d20 + charisma against 10, shows the roll
when "level(rpg_hero) >= 3" -> :veteran else=rookie
select "What do you say?"
option "Bluff [Charisma]" "if=stat(rpg_hero, cha) >= 6" -> :bluff
option "Show the dagger" "if=equipped(rpg_hero, 'classic/dagger')" -> :threat
option "Leave" -> :leave
```

Conditions: `stat(who, stat) >= n`, `level(who) >= n`, `archetype(who, id)`, `equipped(who, item)`. They
work wherever conditions do: `when`, an `option`'s `if`, a scenario's `condition:`, map events. An option
whose condition is false is not offered. A choice with every option hidden is skipped.

`check` leaves `check_roll`, `check_total`, `check_dc`, `check_result` and more in the dialog variables for
your own lines. The dice default to the game setting `checks.dice`, else `d20`. `dice=none` makes a plain
threshold.

## Game settings (`collection.json` → `settings`)

| Setting | Meaning |
|---|---|
| `stats.player` | whose sheet `player` means |
| `stats.exp_curve` | exp needed for a level when the class has no `ExpCurve` |
| `stats.leveling` | `all` lets characters without a class level up too |
| `checks.dice` | default dice of `check` |

## The character sheet

The toolbar's book button shows the party, the class, the level and exp bar, the stats by group with the
part traits add, and blocks other mods add (the Equipment block with Take off). In C#, a mod adds a block by
implementing `ISheetSection`.

## See it working

`--game=rpg_plus --new`: a novel prologue picks the class and makes a charisma check, then the tile world
uses the same stats in gear, battles and the sheet. The content is in `mods/rpg_plus_game`, the rules in
`mods/classic_rpg_pack`.
