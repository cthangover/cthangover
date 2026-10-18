# Skills

Skills are card-based abilities that characters can acquire, equip, and use in battle. Defined as JSON data and rendered as skill cards in the UI.

## JSON structure

```jsonc
{
  // Unique skill ID — used for lookups and save data
  "Id": "slave_master",
  // Locale key for the skill's display name on the card
  "Name": "skills/slave_master/name",
  // Locale key for the skill's tooltip/description text
  "Description": "skills/slave_master/desc",
  // Path to card artwork sprite, resolved from the characters/skills mod group
  "Image": "slave_master",
  // Skill activation type: "Active", "Passive", or "ForCharacter"
  "Type": "Passive",
  // Rarity tier: "Common", "Uncommon", "Rare", "Epic", or "Legendary"
  "Rare": "Legendary",
  // Optional: action card granted to the player when the skill is acquired
  // (active skills hand over a card this way)
  "GrantsAction": "physics/attack",
  // Optional: numeric stat bonuses while the skill is owned
  "Effects": [
    { "Key": "characters/skills/effects/recruit", "Value": 10, "Percent": true }
  ]
}
```

## Field reference

| Field | Type | Description |
|---|---|---|
| `Id` | string | Unique skill ID used for lookups and save data |
| `Name` | string | Locale key for display name |
| `Description` | string | Locale key for tooltip/description text |
| `Image` | string | Path to skill card artwork, resolved from mod group `characters/skills` |
| `Type` | string | `"Active"`, `"Passive"`, or `"ForCharacter"` |
| `Rare` | string | `"Common"`, `"Uncommon"`, `"Rare"`, `"Epic"`, or `"Legendary"` |
| `GrantsAction` | string | Optional action card ID pushed into the action pool when the skill is granted |
| `Effects` | array of objects | Optional numeric bonuses granted while the skill is owned — see below |

## Skill types

### `Active`
Must be intentionally triggered during the player's turn, consuming an action resource and targeting a specific ally or enemy. Active skills appear as cards in the player's hand.

### `Passive`
Remains in effect continuously once acquired. Provides a persistent buff or aura without requiring player input. Passive skills do not appear in the hand — they are always active.

### `ForCharacter`
Applies directly to the owning character as a stat modifier or intrinsic trait. These are character-specific upgrades that modify base stats.

### `Effects` — collection bonuses

Each effect is one numeric bonus: `Key` is the locale key naming the affected
stat (e.g. `characters/skills/effects/recruit`), `Value` is the magnitude
(negative values are penalties), and `Percent` selects the display unit
(`+10%` vs `+10`). Effects sharing a `Key` are summed across every owned
skill by `SkillEffectAggregator` into the skills panel's global "collection
effect" plate — so one key must keep the same unit across all skills. The
reference definition `cthangover_game/characters/skills/slave_master.json`
ships one effect: `+10%` recruitment (`"Value": 10, "Percent": true`).

## Granting action cards

An active skill hands the player an action card instead of acting by itself: set `GrantsAction` to the action's `Id`. When the skill is granted, `SkillService` adds the skill to the owned set and appends the card to the action pool, where it can be assigned to a character in the character panel. The skills panel shows the pairing — opening the skill displays "grants an action card" with the card's icon and name.

Granting is idempotent: an already owned skill is ignored, so replaying a scenario event cannot duplicate the card. Action cards awarded this way are not destroyed by the character panel's reset button — reset restores template assignments and returns awarded cards to the pool.

## Rarity tiers

| Tier | Description |
|---|---|
| `Common` | Standard skill — highest drop rate, baseline power |
| `Uncommon` | Slightly improved stats, lower drop rate |
| `Rare` | Notably stronger, appears infrequently |
| `Epic` | Powerful skill with significant gameplay impact, very low drop chance |
| `Legendary` | Game-changing abilities, extremely rare acquisition |

The rarity tier drives the visual frame colour on the skill card via `SkillCardFrameBehaviour`.

## Skill data at runtime

Skill ownership is tracked in `SkillData`:

| Method | Description |
|---|---|
| `HasSkill(id)` | Check if the player owns a skill |
| `AddSkill(id)` | Add a skill to the player's collection (no card grants — prefer `SkillService`) |

Acquisitions go through `SkillService.GrantSkill(id)`, which pairs the skill with its `GrantsAction` card. From a scenario:

```scenario
action character.add_skill skill_id=slave_master
```

The scenario action rejects unknown skill IDs with a log warning and does nothing when the skill is already owned.

## Skill file format

Skill files are self-describing envelopes routed by `Kind`, with no index file:

```jsonc
{
  // Skills are a separate entity kind, not part of characters
  "Kind": "skill",
  "Items": [
    {
      "Id": "slave_master",
      "Name": "characters/skills/slave_master_name",
      "Description": "characters/skills/slave_master",
      "Image": "characters/skills/slave_master",
      "Type": 1,
      "Rare": 3
    }
  ]
}
```

A mod may place this file anywhere; the entity resolver finds it by kind.

## Locale keys

```properties
skills/slave_master/name = Slave Master
skills/slave_master/desc = Increases recruitment chance against defeated enemies by 25%
```

## UI components

| Class | Description |
|---|---|
| `SkillCardBehaviour` | Individual skill card rendering |
| `SkillCardFrameBehaviour` | Card frame with rarity-based colouring |
| `SkillWidget` | Compact skill display widget |
| `SkillsPanelBehaviour` | Skills management panel in game UI |
