"""Create a tiny independent .blend source; no assets or naming contracts from the pirate prototype."""

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
    bpy.ops.mesh.primitive_cube_add(size=2, location=(0, 0, 1))
    model = bpy.context.object
    model.name = "IndependentFixtureGeometry"
    model["author_note"] = "Model names are arbitrary; semantic roles live in profiles."
    material = bpy.data.materials.new("Fixture blue")
    material.diffuse_color = (0.15, 0.4, 0.8, 1)
    model.data.materials.append(material)
    model.keyframe_insert(data_path="rotation_euler", frame=1)
    model.rotation_euler.z = 0.5
    model.keyframe_insert(data_path="rotation_euler", frame=24)
    bpy.context.scene.frame_set(1)
    output = Path(args.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(output))


if __name__ == "__main__":
    main()
