"""Re-export the independent Stage 1 source with provider-relative OBJ texture paths."""
import argparse
import hashlib
import json
from pathlib import Path
import sys
import bpy


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--source', type=Path, required=True)
    parser.add_argument('--out', type=Path, required=True)
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
    args.out.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=str(args.source.resolve()))
    bpy.ops.wm.obj_export(filepath=str((args.out / 'probe.obj').resolve()), export_materials=True, path_mode='STRIP')
    record = {'source_sha256': hashlib.sha256(args.source.read_bytes()).hexdigest(), 'blender': bpy.app.version_string, 'path_mode': 'STRIP', 'embedded_scripts': False, 'files': {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in args.out.iterdir() if p.is_file()}}
    (args.out / 'provenance.json').write_text(json.dumps(record, indent=2) + '\n', encoding='utf-8')


if __name__ == '__main__':
    main()
