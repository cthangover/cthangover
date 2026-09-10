# Items

JSON-defined items — consumables, resources, and recipe items — with localization, pricing, and crafting recipes.

## JSON structure

```jsonc
{
  // Unique item ID in path-style format (e.g. "food/ration")
  "Id":          "type/item_id",
  // Locale key for the item's display name
  "Name":        "locale_key_for_name",
  // Locale key for the item's tooltip/description
  "Description": "locale_key_for_description",
  // Base purchase price in the game economy
  "Cost":        5,
  // Path to the item icon sprite, resolved under the mod's sprites directory
  "Sprite":      "type/item_id",
  // Type flags (comma-separated): Food, Resource, Quest, Recipe, Used, TargetUsed
  "ItemType":    "Food,Resource",
  // Action ID for the item's use effect; resolved via IItemAction factory
  "ItemAction":  "action_id"
}
```

## Field reference

| Field | Description |
|---|---|
| `Id` | Unique item ID, path-style (`"food/ration"`) |
| `Name` | Locale key resolved from `.properties` files |
| `Description` | Locale key for tooltip/info text |
| `Cost` | Base price in the game economy |
| `Sprite` | Path to the item icon under the mod's sprites directory |
| `ItemType` | Type tag(s), comma-separated: `Food`, `Resource`, `Quest`, `Recipe`, `Used`, `TargetUsed` |
| `ItemAction` | Action ID resolved via `IItemAction` factory — can be a C# class or a GDScript with `get_item_action_id()` + `use(item)`. Defines the item's use effect |

### ItemType values

`ItemType` is a `[Flags]` enum — multiple values can be combined with a comma:

| Value | Hex | Description |
|---|---|---|
| `Quest` | `0x01` | Quest item — tracked in quest log |
| `Used` | `0x02` | Self-targeting use item |
| `TargetUsed` | `0x04` | Ally/enemy-targeting use item |
| `Food` | `0x08` | Edible (consumes hunger stat) |
| `Resource` | `0x10` | Crafting material |
| `Recipe` | `0x20` | Recipe item (unlocks in cookbook) |
| `CantDrop` | `0x80000000` | Cannot be discarded from inventory |

### Extra JSON keys

Any JSON key that does not match a DTO field is captured by
`ItemInfo.ExtraProperties` (`[JsonExtensionData]`) and imported by
`ItemFactory` into `Item.Properties` — a string-keyed `PropertyData` bag
(`Core/Items/ItemInfo.cs`, `Core/Items/Item.cs`). Numbers import as int/float,
booleans as bool, strings as strings; arrays and objects are stored as raw JSON
text. Core code never reads `Properties` — the bag exists so your own C# item
actions or scenario logic can attach custom data to items without schema
changes.

```jsonc
{
  "Id": "food/ration",
  "...": "...",
  // Captured into Properties — readable from your own code via item.Properties.GetInt("MyMod/Saturation")
  "MyMod/Saturation": 3
}
```

## Item actions (IItemAction)

The `ItemAction` field references an implementation of `IItemAction`
(`Core/Items/IItemAction.cs`) — the item's on-use behaviour:

```csharp
public class MyHealAction : IItemAction
{
    // Registry lookup key — must match the item's "ItemAction" field in JSON
    public string ID => "mymod/heal";

    public bool UseAction(IItem item)
    {
        // Returning FALSE aborts consumption — the item stays in the
        // inventory and the UI can show a failure feedback instead
        return TryHeal(item);
    }
}
```

Registration options:

- **Auto-discovery** — on first use, `ItemActionFactory` scans every loaded
  assembly for public `IItemAction` implementations and indexes them by `ID`.
  A mod DLL just needs to contain the class — no registration code required.
- **Manual registration** — `ItemActionFactory.Register(IItemAction)` adds a
  single instance; `ItemActionFactory.RegisterAssembly(assembly)` scans one
  assembly (used for hot-loaded mod assemblies).

The special ID `"null"` (or an empty value) is an explicit no-action marker:
`ItemActionFactory.Get` returns `null` for it, meaning the item has no use
effect.

## Real examples

### Consumable — `core/items/food/ration.json`

```jsonc
{
  // Unique item ID
  "Id": "food/ration",
  // Locale key → "Ration"
  "Name": "items/food/ration/name",
  // Locale key → "A daily ration unit for one person"
  "Description": "items/food/ration/desc",
  // Base price: 5 gold
  "Cost": 5,
  // Icon sprite path under the mod's sprites folder
  "Sprite": "food/ration",
  // Edible item (consumes hunger stat on use)
  "ItemType": "Food"
}
```

### Crafting material — `core/items/food/wolf_meat.json`

```jsonc
{
  // Unique item ID
  "Id": "food/wolf_meat",
  // Locale key → "Wolf cutlet"
  "Name": "items/food/wolf_meat/name",
  // Locale key → "Raw wolf meat"
  "Description": "items/food/wolf_meat/desc",
  // Base price: 1 gold — cheap, but important for recipes
  "Cost": 1,
  // Icon sprite path
  "Sprite": "food/wolf_meat",
  // Both a crafting ingredient (Resource) and edible (Food)
  "ItemType": "Resource,Food"
}
```

Note: `ItemType` can be multiple values — `"Resource,Food"` means it's both a crafting ingredient and edible.

### Recipe item

Recipe items are items that represent a craftable recipe in the player's inventory:

```jsonc
{
  // Unique recipe item ID
  "Id": "recipe/wolf_meat_to_ration",
  // Locale key for the recipe name
  "Name": "recipe/wolf_meat_to_ration/name",
  // Locale key for the recipe description
  "Description": "recipe/wolf_meat_to_ration/desc",
  // Icon sprite path
  "Sprite": "recipe/wolf_meat_to_ration",
  // Recipe type — unlocks in the cookbook when acquired
  "ItemType": "Recipe"
}
```

## Item index file

`items/items.json` registers items using `$ref` syntax:

```jsonc
{
  // $ref entries — each ${...} path expands to items/{path}.json and its contents are merged in
  "Items": [
    "${food/ration}",
    "${food/wolf_meat}",
    "${resource/branches}"
  ]
}
```

`${food/ration}` expands to `items/food/ration.json`.

## Recipes

Recipes are defined separately in `recipes/` and describe input→output transformations:

```jsonc
{
  // Wrapping array — recipe definitions are listed under Items
  "Items": [{
    // Unique recipe ID
    "Id": "recipe/wolf_meat_to_ration",
    // Locale key for the recipe display name
    "Name": "recipe/wolf_meat_to_ration/name",
    // Locale key for the recipe description text
    "Description": "recipe/wolf_meat_to_ration/desc",
    // Workbench type — "Cooking" is currently the only supported type
    "Type": "Cooking",
    // Crafting time in in-game minutes
    "Time": 15,
    // Required ingredients: Id references an item definition, Count is the quantity needed
    "Input": [
      { "Id": "food/wolf_meat", "Count": 1 },
      { "Id": "resource/branches", "Count": 2 }
    ],
    // Produced items on successful craft
    "Output": [
      { "Id": "food/ration", "Count": 1 }
    ]
  }]
}
```

| Field | Description |
|---|---|
| `Id` | Unique recipe ID |
| `Name` | Locale key for recipe display name |
| `Description` | Locale key for the recipe description text |
| `Type` | Workbench type (`WorkbenchType` enum). The enum currently declares a single value: `"Cooking"` |
| `Time` | Crafting time in in-game minutes |
| `Input` | Required ingredients: `Id` (item ID) + `Count` |
| `Output` | Produced items: `Id` (item ID) + `Count` |

## Inventory API

The player inventory (`Inventory` / `IInventory`, `Core/Items/Inventory.cs`)
is an ordered list of item slots:

- `Items` — `List<IItemContainer>`, each slot being `{ Item, Count }`. Slot
  order drives UI layout; empty containers are deleted when a stack hits zero.
- `Change` — C# event `Action<string itemId, int delta>` fired on every add/remove.

All query and mutation methods are overloaded by **item id string** or by
**`IItem` instance**:

| Method | Description |
|---|---|
| `HasItem(id \| item)` | Does any slot hold this item? |
| `TryGet(id \| item)` | Returns the `IItemContainer` slot, or null |
| `CheckCount(id \| item)` | Total count across slots (0 if absent) |
| `Add(id \| item, count = 1)` | Stacks onto an existing slot, or creates a new one by resolving the id through `ItemFactory`. Unknown ids are silently ignored |
| `Remove(id \| item, count = 1)` | Removes items; returns `false` without changes if the slot holds fewer than `count` |

Every successful mutation publishes an `InventoryChangedEvent` to the global
`ModEventBus` — published from `Inventory.PublishInventoryChanged`
(`Core/Items/Inventory.cs`). GDScript subscribers receive it under the key
`"inventory_changed"` with a snake_case payload:

| Payload key | Type | Meaning |
|---|---|---|
| `item_id` | string | Affected item ID |
| `delta` | int | Signed change amount (negative on removal) |
| `new_count` | int | Slot count after the mutation |

## GDScript bridge objects

GDScripts never touch `IInventory` directly — the engine hands them wrapper
objects:

### `GDInventoryService`

A `[GlobalClass]` `RefCounted` wrapping the inventory. Plain-id methods mirror
the C# API; the `*ByItem` variants take a `GDItem` instead of an id string:

| Method | Description |
|---|---|
| `HasItem(id)` / `HasItemByItem(gd_item)` | Item presence check |
| `CheckCount(id)` / `CheckCountByItem(gd_item)` | Stack size lookup |
| `Add(id, count = 1)` / `AddByItem(gd_item, count = 1)` | Add items |
| `Remove(id, count = 1)` / `RemoveByItem(gd_item, count = 1)` | Remove items; returns success |

### `GDItem`

A `[GlobalClass]` snapshot passed as the `item` argument to a GDScript's
`use(item)`:

| Field | Type | Description |
|---|---|---|
| `Id` | string | Item identifier |
| `Name` | string | Display name |
| `Description` | string | Tooltip text |
| `Cost` | int | Purchase price |
| `ItemType` | int | `ItemType` flags as int |
| *(internal)* | — | The wrapper keeps an internal reference to the real `IItem`; `Sprite` and `Properties` are deliberately not exposed |

See [GDScript in Mods → Items](site/docs/mods/src/gdscript/items) for the
authoring-side view.

## Locale keys

Item JSONs reference locale keys — you need to define the actual text in `locale/en.properties`:

```properties
items/food/ration/name = Ration
items/food/ration/desc = A daily ration unit for one person
items/food/wolf_meat/name = Wolf cutlet
items/food/wolf_meat/desc = Raw wolf meat
recipe/wolf_meat_to_ration/name = Ration from wolf meat
recipe/wolf_meat_to_ration/desc = A recipe for cooking wolf meat safely
```

## Adding a new item

1. Create `items/{type}/{id}.json` with all fields
2. Add it to `items/items.json`: `"${type/id}"`
3. Add locale keys for name and description
4. Optionally add a sprite icon
5. If the item has an active use, reference it via the factory — implement either an `IItemAction` C# class or a GDScript with `get_item_action_id()` + `use(item)`. See [GDScript in Mods](site/docs/mods/src/gdscript) for details.
