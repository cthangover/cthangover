# Logical avatars and PNG layers

`avatar_base` provides renderer-independent avatar entities, preset groups and independent instance state. `avatar_layers` implements the `layers` renderer with transparent PNGs. These are optional C# APIs: existing `first=`/`second=` dialog portraits continue to use image paths. There is no automatic dialog integration, scenario command, avatar editor or automatic save registration.

## Definition and images

Depend on `avatar_layers>=1`. Put images in `avatar_layers/<prefix>/` and an ordinary `Kind: avatar` entity in your mod, for example `avatars/hero.json`:

```json
{
  "Kind": "avatar",
  "Items": [{
    "Id": "my_game/hero", "Renderer": "layers",
    "BaseValues": {
      "canvas": {"Width": 512, "Height": 1024},
      "layers/body": {"Image": "hero/body.png", "Order": 0}
    },
    "Groups": {
      "eyes": {
        "Default": "open", "Priority": 100,
        "Presets": {
          "open": {"Values": {"layers/eyes": {"Image": "hero/eyes_open.png", "Order": 10}}},
          "closed": {"Values": {"layers/eyes": {"Image": "hero/eyes_closed.png", "Order": 10}}}
        }
      }
    }
  }]
}
```

Image paths include `.png` and are relative to `avatar_layers/`, resolved through mod providers, including ZIPs. They do not go through the dialog portrait resolver. Use a content-specific prefix.

## Preset composition

One preset is selected per group. Start from `BaseValues`, then apply selected groups by ascending `Priority`, then ordinal group name. Later values replace the whole binding value; they do not deep-merge its object. Group and preset names are case-sensitive and author-defined. An empty preset contributes nothing; `auto` is not a reserved animation mode.

`ApplyPresets` validates every supplied pair before changing any selection. Unknown names throw; unmentioned groups keep their choices. Each instance snapshots its definition and has its own selections. `Resolve()` returns a complete target: bindings omitted from the next target must disappear rather than retain old values.

## Layer fields

| Binding or field | Meaning |
|---|---|
| `canvas` | Positive finite `Width` and `Height`; defaults 512×1024 |
| `layers/<id>` string | PNG path filling the canvas, order 0 |
| `layers/<id>` object | Settings below |
| `layers/<id>` null | No layer; avatar binding null is a literal target, distinct from entity patch null |
| `Image` | Required for a visible layer |
| `Order` | Ascending drawing order; ties use ordinal binding ID |
| `X`, `Y`, `Width`, `Height` | Canvas pixels; zero dimensions use the corresponding canvas dimension |
| `Visible`, `Opacity` | Default true and 1; alpha range 0–1 |
| `FlipH`, `FlipV` | Mirror the image in its rectangle |

Unknown bindings/properties and invalid bounds are rejected. Positive transition durations are accepted but PNG changes snap immediately. Blinking, skeletal deformation, clipping masks and per-layer animation are not implemented.

## Hosting and saving

```csharp
IAvatar avatar = AvatarCatalog.Create("my_game/hero");
IAvatarRenderer renderer = AvatarRenderers.Create(avatar);
host.AddChild(renderer.View);
avatar.ApplyPresets(new Dictionary<string, string> { ["eyes"] = "closed" });
renderer.Apply(avatar.Resolve(), transitionSeconds: 0.15);
AvatarState state = avatar.CaptureState();
// Before freeing the host or unloading the renderer mod:
renderer.Dispose();
```

Use `Cthangover.Mods.Avatars` for the base APIs. The owning subsystem stores `AvatarState` in its own save blob and restores it with `AvatarCatalog.Create(state.AvatarId, state)`. Missing groups use defaults; mismatched avatar IDs and unknown selections are rejected. Migrate removed presets explicitly.

Run visual operations on the Godot main thread. The renderer owns its nodes and loaded textures and must be disposed before its host. Failed visual validation leaves the previous rendered target intact; the owner must still handle any logical selection change it already made. Factories implementing `IAvatarRendererFactory` are discovered through the mod type-consumer pipeline and removed on unload.

Implementation and full contracts: `mods/avatar_base/src/`, `mods/avatar_layers/src/`; the `avatar_layers__contracts` suite verifies rendered colours and resource ownership.
