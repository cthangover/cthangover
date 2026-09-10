# Backgrounds

Scene background images with optional depth maps for parallax effects and dynamic lighting.

## Directory structure

```filestree
backgrounds/
└── {scene_name}/
    ├── {image_name}.png
    └── {image_name}_depth.png   # optional depth map
```

## Real example — Murakami meeting

From `mods/murakami/backgrounds/town/meetingmurakami/`:

```filestree
meetingmurakami/
├── 00002-1920617752.png       # wolf tracks at the door
├── 00002-1920617752_depth.png # depth map for lighting
├── 00014-1724512349.png       # Murakami at her door
├── 00079-2783395178.png       # inside the house
├── 00078-2783395178.png       # inside, different angle
├── 00136-3647329618.png       # dark room
├── 00031-3535505844.png       # werewolf reveal
└── 00031-3535505844_depth.png
```

Each background has a matching `_depth.png` — a grayscale image where brightness encodes the Z-depth of each pixel. The `light_set` command uses it to cast dynamic shadows.

## Usage in scenarios

```scenario
background "town/meetingmurakami/00002-1920617752"
text "[Wolf tracks lead straight to the doorstep]" key=meeting/after_wolf/tracks

background "town/meetingmurakami/00014-1724512349"
text "- Dear lady!" key=meeting/after_wolf/greeting first=marao/think
```

The path is relative to `backgrounds/` — no `.png` extension needed.

## Scene default background

Each scene definition can specify a default background that loads when entering the scene:

```jsonc
{
  "name": "home_kitchen",                                  // Scene identifier
  "defaultBackground": "home/kitchen",                     // Background image loaded when entering the scene
  "defaultScenario": "scenarios/home/home_kitchen_default.scenario" // Default scenario to run on scene enter
}
```

Mods override backgrounds by setting a higher `priority` in their `.scenario` file.

## Adding a new background

1. Create `backgrounds/{scene}/{name}.png` in your mod
2. Optionally create a `{name}_depth.png` grayscale depth map
3. Reference in scenario: `background "scene/name"`
4. If you want it as the default, update the scene JSON's `defaultBackground`

## AI-generated backgrounds workflow

The project uses AI-generated (Stable Diffusion / Flux) images for backgrounds. Typical workflow:

1. Generate a scene description prompt
2. Get the base PNG
3. Generate a depth map variant using a depth estimator or ControlNet
4. Place both files in the appropriate folder
