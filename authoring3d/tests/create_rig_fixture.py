"""Create an editable, independently named skinned fixture for bone/socket authoring contracts."""

import argparse
from pathlib import Path
import sys

import bpy


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", required=True)
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    armature = bpy.data.armatures.new("UnrelatedArmatureData")
    rig = bpy.data.objects.new("ArbitraryRigObject", armature)
    bpy.context.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    root = armature.edit_bones.new("DifferentRoot")
    root.head = (0, 0, 0)
    root.tail = (0, 0, 1)
    tip = armature.edit_bones.new("AccessoryAnchor")
    tip.head = (0, 0, 1)
    tip.tail = (0, 0, 2)
    tip.parent = root
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, 1))
    mesh = bpy.context.object
    mesh.name = "SkinnedSample"
    mesh.parent = rig
    group = mesh.vertex_groups.new(name="AccessoryAnchor")
    group.add(list(range(len(mesh.data.vertices))), 1, "REPLACE")
    modifier = mesh.modifiers.new(name="IndependentDeform", type="ARMATURE")
    modifier.object = rig
    material = bpy.data.materials.new("Independent rig blue")
    material.diffuse_color = (0.15, 0.4, 0.8, 1)
    mesh.data.materials.append(material)
    output = Path(args.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(output))


if __name__ == "__main__":
    main()
