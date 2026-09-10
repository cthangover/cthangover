# Item Actions

A GDScript that has both `get_item_action_id()` and `use(item)` is registered as an **item action**. The ID must match an `ItemAction` field in the item's JSON definition.

```gdscript
extends RefCounted

func get_item_action_id():
    return "my.heal_potion"

func use(item):
    print("[ITEM] Using: " + item.Name + " (id=" + item.Id + ")")
    # Perform healing logic here
    return true
```

## GDItem fields

| Field | Type | Description |
|---|---|---|
| `Id` | `string` | Item identifier |
| `Name` | `string` | Display name |
| `Description` | `string` | Tooltip text |
| `Cost` | `int` | Purchase price |
| `ItemType` | `int` | Item category flags |
