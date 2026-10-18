# Shaders

Godot `.gdshader` and `.gdshaderinclude` files that provide visual effects: pixel-art rendering, scene transitions, sprite-sheet animation, interactive highlighting, and noise generation.

## Directory structure

```filestree
shaders/
├── pixel_art.gdshader
├── pixel_art_post_process.gdshader
├── scene_transition.gdshader
├── timed_sprite.gdshader
├── interactive_highlight.gdshader
├── avatar.gdshader
├── default.gdshader
├── effect_anim.gdshader
└── noise_simplex.gdshaderinclude
```

## Real example shaders

From `mods/ui_base/shaders/`:

| Shader | Purpose |
|---|---|
| `pixel_art.gdshader` | Pixel-perfect rendering at low resolution, preserves sharp edges at any scale |
| `pixel_art_post_process.gdshader` | Full-screen post-process: applies pixel-art look to the entire viewport |
| `scene_transition.gdshader` | Animated transitions between scenes (fade, wipe, dissolve) |
| `timed_sprite.gdshader` | Sprite-sheet animation driven by shader uniforms (time-based frame selection) |
| `interactive_highlight.gdshader` | Glow/highlight effect when hovering over clickable objects |
| `avatar.gdshader` | Character portrait rendering with optional effects (emotion transitions, shadows) |
| `default.gdshader` | Base shader used for regular sprites and backgrounds |
| `effect_anim.gdshader` | Particle and effect animation driven by GPU |
| `noise_simplex.gdshaderinclude` | Simplex noise function — included by other shaders for procedural effects |

## How shaders are referenced

Shaders are referenced by filename (no extension) in the engine. They can be:

1. **Assigned to materials** in Godot scenes via the engine
2. **Overridden by mods** — if a mod provides a shader with the same name, it replaces the default
3. **Applied to specific nodes** via C# code, using the static `Shaders` facade
   (backed by `ShaderResolver`):

```csharp
var shader = Shaders.Resolve("pixel_art");
if (shader != null)
{
    var material = node.Material as ShaderMaterial;
    material.Shader = shader;
}
```

The `Shaders` facade exposes:
- `Collect()` — all `.gdshader` files across mods, keyed by logical name
- `Resolve(name)` — loads the `Shader` resource by name (`null` if not found)
- `TryGetCached(name)` — returns a previously resolved, still-valid instance

At runtime, `ShaderModReplacer.PatchScene(Node)` walks freshly instantiated
scenes and swaps every `ShaderMaterial` whose original resource path matches a
mod-provided shader name. The same pass also performs **texture overrides**:
`TextureRect` and `Sprite2D` nodes have their textures matched by resource
filename against the mod texture registry and replaced the same way — so
`PatchScene` is not limited to shaders.

## Engine-side consumers

The engine ships a dedicated `Control` node that consumes a mod-provided
shader: `PixelArtPostProcess` (`Core/Shaders/PixelArtPostProcess.cs`). On
`_Ready` it builds a full-viewport `ColorRect`, loads the
`pixel_art_post_process` shader via the mod system, and exposes an
`[Export] float PixelSize` property that drives the shader's `pixel_size`
uniform — adjusting it from the Godot inspector updates the effect live.

## Shipped core shaders

Besides the interface shaders listed above, the `cthangover_game` mod ships its own shader
family in `mods/cthangover_game/shaders/`: a set of `2d_cubism_*.gdshader` files
(`mask`, plus the blend modes `mask_mul` / `mask_add` / `mask_mix`, each blend mode
also in an `_inv` inverted variant, and `norm_mul` / `norm_add` / `norm_mix`)
used for Cubism-style sprite masking and normal blending. The folder also contains
`SHADER_CONTRACT.md`, which documents the conventions these shaders follow.

## Minimal custom shader example

`shaders/my_grayscale.gdshader`:

```glsl
shader_type canvas_item;

uniform float intensity : hint_range(0.0, 1.0) = 1.0;

void fragment() {
    vec4 color = texture(TEXTURE, UV);
    float gray = dot(color.rgb, vec3(0.299, 0.587, 0.114));
    COLOR = mix(color, vec4(gray, gray, gray, color.a), intensity);
}
```

## Shader includes

`.gdshaderinclude` files can be shared across shaders:

`shaders/noise_simplex.gdshaderinclude`:
```glsl
float simplex_noise(vec2 p) {
    // ... noise implementation ...
}

float fbm_noise(vec2 p, int octaves, float lacunarity, float gain) {
    float value = 0.0;
    float amplitude = 1.0;
    float frequency = 1.0;
    for (int i = 0; i < octaves; i++) {
        value += amplitude * simplex_noise(p * frequency);
        frequency *= lacunarity;
        amplitude *= gain;
    }
    return value;
}
```

Used in another shader:
```glsl
shader_type canvas_item;
#include "shaders/noise_simplex.gdshaderinclude"

void fragment() {
    float n = fbm_noise(UV * 10.0, 4, 2.0, 0.5);
    COLOR = vec4(n, n, n, 1.0);
}
```

## Adding a new shader

1. Create `shaders/{name}.gdshader` in your mod
2. If it depends on shared functions, create the `.gdshaderinclude` file
3. Reference it in the Godot scene or from C# code
4. If replacing an existing shader, use the same filename — the mod's version takes priority
