"""Blender-side export worker. Invoked only by export_blend.py on the authoring host."""

import argparse
import json
from pathlib import Path
import sys

import bpy


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--metadata", required=True)
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
    bpy.ops.wm.open_mainfile(filepath=args.source, load_ui=False, use_scripts=False)
    result = bpy.ops.export_scene.gltf(
        filepath=args.output,
        export_format="GLB",
        use_selection=False,
        export_extras=True,
        export_animations=True,
        export_cameras=False,
        export_lights=False,
    )
    if "FINISHED" not in result:
        raise RuntimeError("Blender did not finish the glTF export")
    Path(args.metadata).write_text(json.dumps({
        "blender_version": bpy.app.version_string,
        "scene": bpy.context.scene.name,
        "objects": len(bpy.context.scene.objects),
        "export_extras": True,
        "export_animations": True,
    }, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
