#!/usr/bin/env python3
"""Pack the selected CC0 photoscans into the demo's runtime terrain arrays."""
import argparse
import json
from pathlib import Path

from build_nature_biomes import DEMO, LAYER_NAMES, LOCATION
from terrain_photoscans import install_materials


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source_dir", type=Path, help="directory of unpacked 1K source maps")
    source = parser.parse_args().source_dir
    manifest = json.loads((DEMO / "ASSET_SOURCES.json").read_text(encoding="utf-8"))
    entries = manifest["terrain/" + LOCATION]
    output = DEMO / "terrain" / LOCATION
    install_materials(source, output, {name: entries[name] for name in LAYER_NAMES}, ("", "_n", "_orm"))


if __name__ == "__main__":
    main()
