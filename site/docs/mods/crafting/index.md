# Crafting stations

`crafting` adds workbenches that exchange materials for products. It uses the `recipe` kind supplied by `items_base` and adds `craft_station`. A station offers its listed recipes without requiring the player to unlock them first. Cooking is a separate mechanic with its own UI and recipe availability.

```json
{
  "Kind": "craft_station",
  "Items": [{
    "Id": "my_game/workbench", "Name": "my_game/workbench/name",
    "Recipes": ["my_game/salve"]
  }]
}
```

Put this in `stations/`, and define the recipe in `recipes/`:

```json
{
  "Kind": "recipe",
  "Items": [{
    "Id": "my_game/salve", "Name": "my_game/salve/name",
    "Description": "my_game/salve/description", "Time": 5,
    "Input": [{"Id": "rpg_plus/mushroom", "Count": 2}],
    "Output": [{"Id": "rpg_plus/salve", "Count": 1}]
  }]
}
```

This example depends on `crafting` and the `rpg_plus_game` content defining the input/output items. Names and descriptions are locale keys. `Time` advances the game clock in minutes.

```scenario
action craft.open station=my_game/workbench
end
```

The window shows recipes, owned/required materials and products. Craft is available when the complete recipe is valid and ingredients are sufficient. The service validates all items and positive counts before removing anything. Materials are consumed only on Craft. The world cannot be walked while the window is open; Esc closes it.

Products are ordinary inventory entries saved by `items_base`; the station does not own another inventory or save slot. The Data Editor edits stations and recipes from the schemas provided by `crafting`.

Implementation: `mods/crafting/src/`, `mods/crafting/schemas/crafting.json`. The `woodland` game suite covers the collection-to-crafting loop.
