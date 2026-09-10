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
  "Rare": "Legendary"
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

## Skill types

### `Active`
Must be intentionally triggered during the player's turn, consuming an action resource and targeting a specific ally or enemy. Active skills appear as cards in the player's hand.

### `Passive`
Remains in effect continuously once acquired. Provides a persistent buff or aura without requiring player input. Passive skills do not appear in the hand — they are always active.

### `ForCharacter`
Applies directly to the owning character as a stat modifier or intrinsic trait. These are character-specific upgrades that modify base stats.

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
| `AddSkill(id)` | Add a skill to the player's collection |

## Skill index file

Skills are registered via index files, same pattern as items and characters:

```jsonc
{
  // $ref entries — ${slave_master} expands to skills/slave_master.json with its contents merged in
  "Items": [
    "${slave_master}"
  ]
}
```

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
