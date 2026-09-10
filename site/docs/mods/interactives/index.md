# Interactive Objects

Interactive objects are clickable scene elements with hover effects and custom actions. They are added to scenes from scenario scripts via `interactive_add` and defined as JSON files.

## JSON structure

```jsonc
{
  "Items": [{                                              // Array of interactive object definitions
    "id": "kitchen_door",                                  // Unique identifier for interactive_add / interactive_remove commands
    "texture": "interactives/kitchen_door",                // Texture key resolved via ModManager.ResolveTexture
    "layer": "foreground",                                 // Visual layer: background, foreground, or ui
    "zIndex": 10,                                          // Z-ordering within the layer (higher = on top)
    "enabled": true,                                       // Whether the object responds to input
    "visible": true,                                       // Whether the object is rendered
    "cursor": "PointingHand",                              // Mouse cursor override (Godot CursorShape enum name)
    "hitArea": {                                           // Collider shape and position in normalised 0..1 coordinates
      "type": "rect",                                      // Shape type: rect, circle, or polygon
      "x": 0.7,                                            // X position (top-left for rect, centre for circle)
      "y": 0.3,                                            // Y position
      "width": 0.15,                                       // Width (rect type only)
      "height": 0.5                                        // Height (rect type only)
    },
    "highlight": {                                         // Visual highlight settings on hover
      "color": "#FFFF0033",                                // Modulate colour as hex string
      "scale": 1.02,                                       // Pulse scale multiplier on hover (1.02 = 2% bigger)
      "duration": 0.15                                     // Animation duration in seconds for hover/unhover
    },
    "actions": {                                           // Actions triggered by pointer events
      "onClick": {                                         // Action on mouse click
        "scenario": "scenarios/home/kitchen_door.scenario" // Path to scenario file to run on click
      },
      "onHoverEnter": "",                                     // Inline DSL commands executed on mouse enter
      "onHoverLeave": ""                                   // Inline DSL commands executed on mouse leave
    }
  }]
}
```

## Field reference

### Top-level fields

| Field | Type | Default | Description |
|---|---|---|---|
| `id` | string | *(required)* | Unique identifier used in `interactive_add` / `interactive_remove` commands |
| `texture` | string | *(required)* | Texture key resolved via `ModManager.ResolveTexture`. Full-screen with alpha |
| `layer` | string | `"foreground"` | Visual layer: `"background"`, `"foreground"`, or `"ui"` |
| `zIndex` | int | 0 | Z-ordering within the layer (higher = on top) |
| `enabled` | bool | `true` | Whether the object responds to input |
| `visible` | bool | `true` | Whether the object is rendered |
| `cursor` | string | — | Mouse cursor override (Godot `CursorShape` enum name, e.g. `"PointingHand"`) |
| `hitArea` | object | `{ type: "rect" }` | Collider shape and position |
| `highlight` | object | — | Visual highlight settings on hover |
| `actions` | object | — | Actions triggered by pointer events |

### `hitArea` fields

Coordinates are in **normalised viewport coordinates (0..1)**, where `(0,0)` = top-left, `(1,1)` = bottom-right.

| Field | Type | Default | Description |
|---|---|---|---|
| `type` | string | `"rect"` | Shape type: `"rect"`, `"circle"`, or `"polygon"` |
| `x` | float | 0 | X position (top-left for rect, centre for circle) |
| `y` | float | 0 | Y position |
| `width` | float | 0.1 | Width for rect type |
| `height` | float | 0.1 | Height for rect type |
| `radius` | float | 0.05 | Radius for circle type |
| `vertices` | array | — | `[{x, y}, ...]` polygon vertices (normalised) |

### `highlight` fields

| Field | Type | Default | Description |
|---|---|---|---|
| `color` | string | `"#FFFF0033"` | Modulate colour as a hex string (e.g. `"#FF000044"`) |
| `scale` | float | 1.02 | Pulse scale multiplier on hover (e.g. 1.02 = 2% bigger) |
| `duration` | float | 0.15 | Animation duration in seconds for hover/unhover |

### `actions` fields

| Field | Description |
|---|---|
| `onClick` | `ClickAction` — path to `.scenario` and/or inline DSL commands |
| `onHoverEnter` | Inline DSL commands executed on mouse enter |
| `onHoverLeave` | Inline DSL commands executed on mouse leave |

### `onClick` sub-fields

| Field | Description |
|---|---|
| `scenario` | Path to a `.scenario` file to run on click |
| `commands` | Array of inline DSL command strings to execute on click |

## Scenario commands

| Command | Description |
|---|---|
| `interactive_add object_id` | Instantiate an interactive object by its definition ID |
| `interactive_remove object_id` | Remove an interactive object |
| `interactive_set object_id enabled=true\|false` | Enable/disable or show/hide an object. Named params: `enabled`, `visible` |
| `interactive_clear` | Remove all interactive objects from the scene |

## Directory structure

```filestree
interactives/
├── kitchen_door.json
└── treasure_chest.json
```

There is no index file. `InteractiveResolver.CollectInteractiveDefsRecursive` scans every `*.json` file under the mod's `interactives/` folder recursively (including subfolders) and reads each one as a `{"Items": [...]}` envelope.

## Adding a new interactive object

1. Create `interactives/{id}.json` with the full definition — it will be picked up automatically
2. In a scenario, activate it: `interactive_add my_object`
