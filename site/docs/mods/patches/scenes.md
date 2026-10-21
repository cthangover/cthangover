# Scene Patches

Scene patches let one mod modify a `.tscn` scene shipped by another mod or by the game itself — without copying and rewriting the whole file. A patch is a regular Godot **inherited scene**: it instances the target and lists only the changes. Godot's inheritance format already expresses property overrides, added nodes, sibling order, groups and connections; the few operations it cannot express (deleting nodes, deleting inherited properties, bulk edits, resource cleanup) are encoded with reserved `metadata/_patch_*` keys in the same file.

This is a separate subsystem from [entity patches](index.md): the target is a scene path, not an entity kind.

## Patch file

Patches live in a mod under `patches/scenes/`, recursively:

```filestree
mods/
└── my_mod/
    └── patches/
        └── scenes/
            └── menu/
                └── main_menu.tscn      ← patch for scenes/menu/main_menu.tscn
```

The target is declared in the file itself — an inherited-scene header plus a root node that instances the target:

```ini
[gd_scene format=3]

[ext_resource type="PackedScene" path="res://mods/my_mod/scenes/menu/main_menu.tscn" id="1_target"]

[node name="MainMenuScene" instance=ExtResource("1_target")]
```

All three spellings of the same scene resolve to one target: the resolver key (`scenes/menu/main_menu.tscn`), the native form (`res://scenes/menu/main_menu.tscn`) and the on-disk form of a mod scene (`res://mods/<mod>/scenes/...`).

## Layers

Patches are applied on top of the scene that won the override battle in `TscnSceneResolver`:

1. The scene's own file (later mods override earlier ones entirely).
2. Patch layers in **mod load order**.
3. Inside one mod, patch files apply in **path order** — use `00-`, `10-` prefixes when the order matters.

Every layer sees the result of the previous ones, so a later mod can override a value set by an earlier patch.

## Cross-mod base scene (shell2d_base + dialog_base + rpg)

`shell2d_base` owns the 2D shell scene; engine mods add their layers to it with a patch instead of shipping their own shell. Two mods of different genres extend the same shell:

```filestree
mods/
├── shell2d_base/
│   └── scenes/ui/base_scene.tscn           ← BaseScene / Scene / ViewBox / TopView / slots / notifications
├── dialog_base/
│   └── patches/scenes/ui/base_scene.tscn   ← DialogCanvas over the view box
└── rpg/
    └── patches/scenes/ui/base_scene.tscn   ← RpgWorldHost (the tile world) between the view box and the dialog
```

```ini
[gd_scene load_steps=3 format=3]

[ext_resource type="PackedScene" path="res://mods/shell2d_base/scenes/ui/base_scene.tscn" id="1_target"]
[ext_resource type="PackedScene" path="res://scenes/ui/dialog_canvas.tscn" id="2_dialog"]

[node name="BaseScene" instance=ExtResource("1_target")]

[node name="DialogCanvas" parent="Scene" index="2" instance=ExtResource("2_dialog")]
```

The base shell is a single flat scene, so `index="2"` inserts the dialog exactly between `ViewBox` and `TopView`. Patches cascade in load order: `rpg` depends on `dialog_base`, so its `RpgWorldHost` with the same `index="2"` lands before the `DialogCanvas`, and dialogs stay on top of the map. Later mods can override or delete shell nodes the same way. The shell contract mods rely on: root `BaseScene` in group `main_event_chain`, `Scene/ViewBox`, `Scene/TopView`, slots `ModFirstPanel` / `ModBeforeUIPanel` / `ModLastPanel`, and `Lastground` inside the view box. Both the target path and the added resource paths may use the editor's `res://mods/<mod>/...` form — it is normalised to the mod-relative key at load time.

## Operations

| Operation | How to write it | Editor |
|---|---|---|
| Override a property | node without `type`, `key = value` | yes |
| Override the root | properties on the root `instance=` node | yes |
| Add a node / subtree | node with `type=` | yes |
| Add a scene instance | node with `instance=ExtResource(...)` | yes |
| Replace a script | `script = ExtResource(...)` on an override node | yes |
| Reorder siblings | `index="N"` in the node heading | yes |
| Replace groups | `groups=[...]` in the node heading | yes |
| Add/remove groups | `metadata/_patch_add_groups` / `metadata/_patch_remove_groups` | no |
| Rebind exported node paths | `node_paths=[...]` + `NodePath(...)` values | yes |
| Add a connection | `[connection ...]` | yes |
| Delete a node | `metadata/_patch_delete = true` on the override node | no |
| Delete nodes by mask | root: `metadata/_patch_delete_nodes = PackedStringArray("Panel/*Btn")` | no |
| Delete a property | `metadata/_patch_remove = "tooltip_text"` | no |
| Delete properties by mask | `metadata/_patch_remove = PackedStringArray("theme_override_*")` | no |
| Delete a connection | root: `metadata/_patch_remove_connections = PackedStringArray("from=Panel/Btn;signal=pressed")` | no |
| Add resources | normal `[ext_resource]` / `[sub_resource]` sections | yes |
| Delete a sub_resource | `metadata/_patch_delete = true` in its body | no |
| Delete an ext_resource | root: `metadata/_patch_remove_ext_resources = PackedStringArray("1_tex")` | no |
| Change the entry point | `__entry_point = "My.Namespace.EntryPoint"` on the root | no |
| Resource garbage collection | root: `metadata/_patch_gc = true` | no |

A patch adds nodes when the node heading has `type=`/`instance=`; a heading without them overrides the existing node at that path. An override for a path that does not exist is logged and skipped.

## Deleting things

```ini
[node name="TelegramLink" parent="."]
metadata/_patch_delete = true

[node name="LoadBtn" parent="MenuContainer"]
metadata/_patch_remove = PackedStringArray("theme_override_*")
```

- `_patch_delete` removes the node, its descendants, connections touching the subtree and `NodePath` bindings that point into it.
- The scene root can never be deleted — the layer is rejected with an error.
- Paths are relative to the root and never include the root name (`MenuContainer/LoadBtn`, not `MainMenuScene/MenuContainer/LoadBtn`).
- Masks use `*` (any run of characters, including `/`) and `?` (one character). `_patch_delete_nodes = PackedStringArray("*")` removes every descendant and keeps the root.
- Deleting a property from the scene means the builder no longer applies it, so the Godot default remains. Godot has no real "unset".

## Connections

Add a connection with a normal section:

```ini
[connection signal="pressed" from="MenuContainer/LoadBtn" to="." method="_on_load_pressed"]
```

Remove one with a selector on the root — `from`, `to`, `signal`, `method` and `flags` are supported, omitted fields are wildcards, and every matching connection is removed:

```ini
[node name="MainMenuScene" instance=ExtResource("1_target")]
metadata/_patch_remove_connections = PackedStringArray(
    "from=MenuContainer/LoadBtn;signal=pressed",
    "to=.;signal=quit"
)
```

At least `signal` and one of `from`/`to` are required so a typo cannot silently wipe every connection.

## Resources

Patch resources are merged into the target:

- the same `type`+`path` already exists → the existing resource is reused;
- the id is free → it is copied as is;
- the id is taken by a different resource → the new resource gets a `_pN` suffix and all references in the patch are rewritten automatically.

`_patch_gc = true` removes ext/sub_resources that nothing references any more. GC runs **once after all layers**, so a resource that a later mod still references survives.

## Editor workflow

Create the patch with **File → New Inherited Scene**, pick the target, make your changes and save it as `.tscn` under `patches/scenes/`. Property overrides, added nodes, sibling order, groups, scripts and connections are authored normally. The operations Godot does not support in inherited scenes (deleting nodes, deleting properties, masks) are written by hand as the metadata keys above — adding metadata to an inherited root persists on save.

## Debugging

Patch layers are invisible in the editor: when several mods patch the same scene, each sees only the base scene. Use `ScenePatchDebug.DumpEffective(scene, label)` (or the `TSCN` log category) to print the effective tree the builder receives. Invalid patches are logged with `TSCN` errors/warnings and skipped, so one broken patch cannot break scene loading.

## Limitations

- **Renaming nodes is not supported.** Other mods address nodes by path; a rename would silently move every override to a non-existent path. Delete and re-add instead.
- Binary `.scn`/`.escn` scenes cannot be patched.
- Connections with `to` other than the scene root are filtered by the runtime builder, for patches as well.
- Array values are replaced as a whole; element-wise `$add`/`$remove` operations are not supported.
- Patching a native scene makes it build through the runtime TSCN pipeline from then on; scenes without patches keep the engine loader.
