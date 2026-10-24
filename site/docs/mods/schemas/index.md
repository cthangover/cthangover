# Schemas: editors from annotations

Mods `schema_base` (the model), `schema_tools` (the Data Editor window and the command palette); read by the
validator and the item and character editors.

RPG Maker's plugins describe their parameters in comments (`@param`, `@type`) and its editor builds forms
from them. Here the same idea is data: a mod ships `schemas/<mod>.json` that describes its scenario
actions, its DSL commands and the fields of entity kinds. Those can be its own kinds or anyone else's. The
tools merge every mod's schema and build forms, pickers and checks from it.

## A schema file

```jsonc
{
  "Actions": [{
    "Id": "stat.add", "Name": "stats/schema/stat_add", "Category": "stats",
    "Params": [
      { "Key": "stat", "Type": "ref", "Kind": "stat", "Required": true, "Name": "stats/schema/field/stat" },
      { "Key": "value", "Type": "formula" },
      { "Key": "who", "Type": "who" }
    ]
  }],
  "Commands": [{
    "Id": "check", "Arrow": "checks/schema/arrow_success",
    "Positional": [{ "Key": "stat", "Type": "ref", "Kind": "stat", "Required": true }, { "Key": "dc", "Type": "int", "Required": true }],
    "Params": [{ "Key": "else", "Type": "label" }, { "Key": "dice", "Type": "text" }]
  }],
  "Kinds": [{
    // Adds fields to someone else's kind: every item now has an equip type and traits in the editors
    "Kind": "item",
    "Fields": [
      { "Key": "Equip", "Type": "text" },
      { "Key": "Traits", "Type": "list", "Of": [{ "Key": "Stat", "Type": "ref", "Kind": "stat" }, { "Key": "Add", "Type": "number" }] }
    ]
  }]
}
```

Field types: `text`, `locale`, `int`, `number`, `bool`, `choice` (`Options`), `ref` / `ref_list` (`Kind`),
`who`, `picture`, `formula`, `condition`, `scene`, `flag`, `label`, `list` (`Of`), `values`, `map`, `json`.
`Name` and `Description` are locale keys of your own mod.

Schemas merge in mod load order. An action or command is replaced whole. A kind is merged field by field,
so two mods can each add their own fields to `item`.

Each engine mod describes its own actions next to its code: `dialog_base` its commands (`text`, `select`,
`option` with `if`, `when`, …), `items_base` `item.add`, `trading` `trade.open`, `rpg` `rpg.battle` and
`rpg.teleport`, `quests_base` the `quest.*` actions, and so on.

## What you get from it

- **Data Editor** window (tool `schema_data_editor`): the described kinds on the left, the entities of a mod
  in the middle, and a form of every mod's fields on the right. Pickers list real entities (stats, classes,
  items, scenes). Texts are entered in every language of the mod. Save writes JSON, and the running game
  sees the change at once.
- **Command palette** (tool `schema_palette`): find any action or command, fill in its form, see the
  finished line and its check, and insert it into the open scenario. The text stays the source.
- **Item and character editors** show other mods' fields of their kind in a "Fields of other mods"
  section. Install `equipment` and the item editor gains Equip and Traits.
- **Validator**: unknown and missing parameters, bad numbers and choices, references to entities that do
  not exist, unparsable conditions in `option … if=` and `when`. The resource atlas sees the references too.

## Your own mechanic

Write the C# action or the entity kind as usual, then add `schemas/<your_mod>.json` and its locale keys.
No editor code is needed: the Data Editor, the palette and the validator pick it up on the next start.
