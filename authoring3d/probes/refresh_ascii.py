"""Re-serialize the existing independent FBX without regenerating the source or other exports."""
import argparse
import hashlib
import json
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parent))
from create_fixture import ascii_element
from io_scene_fbx import parse_fbx


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--directory', type=Path, required=True)
    directory = parser.parse_args(sys.argv[sys.argv.index('--') + 1:]).directory
    tree, version = parse_fbx.parse(str(directory / 'probe.fbx'))
    output = directory / 'probe_ascii.fbx'
    output.write_text('; FBX 7.4.0 project file\n' + '\n'.join(ascii_element(element) for element in tree.elems), encoding='utf-8')
    provenance_path = directory / 'provenance.json'
    provenance = json.loads(provenance_path.read_text(encoding='utf-8'))
    provenance['files']['probe_ascii.fbx'] = hashlib.sha256(output.read_bytes()).hexdigest()
    provenance['ascii_conversion'] = 'Blender binary parser; FBX ASCII scalar C=T/F, binary Content=base64'
    provenance_path.write_text(json.dumps(provenance, indent=2) + '\n', encoding='utf-8')
    (directory / 'ascii-provenance.json').write_text(json.dumps({'binary_sha256': hashlib.sha256((directory / 'probe.fbx').read_bytes()).hexdigest(), 'ascii_sha256': hashlib.sha256(output.read_bytes()).hexdigest(), 'version': version, 'raw_property_encoding': 'base64; Content empty first field; scalar C uses T/F'}, indent=2) + '\n', encoding='utf-8')


if __name__ == '__main__':
    main()
