"""Bake valley coverage and install the photographed ground materials."""

from pathlib import Path
import argparse
import json

import numpy as np
from PIL import Image

from terrain_photoscans import PHOTOSCAN_SIZE, install_materials


ROOT = Path(__file__).resolve().parents[2]
PACK = ROOT / "mods_private" / "proto_land_pack"
MATERIAL_ROOT = "ognishchanin/valley/surface_detail"
OUTPUT = PACK / "terrain" / MATERIAL_ROOT


def noise(size, cells, rng):
    lattice = rng.random((cells, cells), dtype=np.float32)
    p = np.arange(size, dtype=np.float32) * (cells / size)
    i = p.astype(int)
    blend = p - i
    blend = blend * blend * (3 - 2 * blend)
    a = lattice[i[:, None], i[None, :]]
    b = lattice[i[:, None], (i[None, :] + 1) % cells]
    c = lattice[(i[:, None] + 1) % cells, i[None, :]]
    d = lattice[(i[:, None] + 1) % cells, (i[None, :] + 1) % cells]
    return (a + (b - a) * blend) * (1 - blend[:, None]) + (c + (d - c) * blend) * blend[:, None]


def smooth(lo, hi, x):
    t = np.clip((x - lo) / (hi - lo), 0, 1)
    return t * t * (3 - 2 * t)


def save(name, values):
    Image.fromarray(np.rint(np.clip(values, 0, 1) * 255).astype(np.uint8)).save(OUTPUT / f"{name}.png")


def heights_and_weights():
    heights = np.zeros((513, 513), dtype=np.float32)
    weights = np.zeros((513, 513, 4), dtype=np.uint8)
    tiles = json.loads((PACK / "terrains" / "tiles" / "ognishchanin.json").read_text(encoding="utf-8"))["Items"]
    for tile in tiles:
        x, z = map(int, tile["Id"].rsplit("_", 2)[1:])
        sx, sz = x + 256, z + 256
        heights[sz:sz + 65, sx:sx + 65] = np.fromfile(PACK / "terrain" / tile["Heights"], dtype="<f4").reshape(65, 65)
        weights[sz:sz + 65, sx:sx + 65] = np.fromfile(PACK / "terrain" / tile["Weights"], dtype="u1").reshape(65, 65, 4)
    return heights, weights


def coverage():
    heights, weights = heights_and_weights()
    size = 1024
    p = (np.arange(size, dtype=np.float32) + 0.5) / 2
    i = p.astype(int)
    t = p - i
    x, z = np.meshgrid(p - 256, p - 256)
    a = heights[i[:, None], i]
    b = heights[i[:, None], i + 1]
    c = heights[i[:, None] + 1, i]
    d = heights[i[:, None] + 1, i + 1]
    height = np.where(t + t[:, None] <= 1, a + t * (b - a) + t[:, None] * (c - a),
                      d + (1 - t) * (c - d) + (1 - t[:, None]) * (b - d))
    gz, gx = np.gradient(height, 0.5)
    slope = np.hypot(gx, gz)
    cfg = json.loads((PACK / "definitions" / "location.json").read_text(encoding="utf-8"))
    location = next(item for item in cfg["Items"] if item["Id"] == "proto_land/yard")
    lake = next(item["Body"] for item in location["Water"] if item["Id"] == "lake")
    cx, cz = lake["Center"]
    w, h = lake["Size"]
    lake_mask = 1 - smooth(0.985, 1.025, np.hypot((x - cx) / (w / 2), (z - cz) / (h / 2)))
    depth = lake["Level"] - height
    jitter = noise(size, 107, np.random.default_rng(1417)) - 0.5
    sediment = smooth(0.14, 1.05, depth + jitter * 0.17) * (1 - 0.57 * smooth(0.14, 0.42, slope)) * lake_mask
    wet_sand = smooth(-1.02, 0.12, depth + jitter * 0.12) * (1 - sediment) * lake_mask

    woods = np.maximum(1 - smooth(0.56, 1.0, np.hypot((x + 110) / 92, (z - 67) / 83)),
                       1 - smooth(0.53, 1.0, np.hypot((x + 124) / 97, (z + 158) / 67)))
    grass = weights[i[:, None], i, 0].astype(np.float32) / 255
    dry = smooth(0.35, 2.0, height - lake["Level"])
    forest = woods * dry * smooth(0.30, 0.85, grass) * (0.78 + 0.22 * jitter)

    # A small plowed plot east of the houses. Rounded corners preserve the original paths.
    field_radius = ((abs(x - 101) / 14) ** 4 + (abs(z - 27) / 14) ** 4) ** 0.25
    field = (1 - smooth(0.86, 1.04, field_radius + jitter * 0.05)) * dry * (1 - smooth(0.38, 0.65, slope))
    field *= smooth(0.30, 0.75, grass)
    save("coverage", np.stack((wet_sand, sediment, forest, field), axis=-1))
    # The plot fits within one tile. Derive its scatter density from the original authored mask.
    source = PACK / "terrain" / "ognishchanin" / "start_places" / "tile_64_0.density"
    density = np.fromfile(source, dtype="u1").reshape(65, 65)
    samples = np.arange(65) * 2
    field_samples = field[(512 + samples)[:, None], 640 + samples]
    density = np.rint(density * (1 - smooth(0.07, 0.75, field_samples))).astype(np.uint8)
    density.tofile(OUTPUT / source.name)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--materials-from", type=Path, help="directory of unpacked 1K source maps")
    source = parser.parse_args().materials_from
    entries = json.loads((PACK / "ASSET_SOURCES.json").read_text(encoding="utf-8"))["terrain/" + MATERIAL_ROOT]
    suffixes = ("_color", "_normal", "_orm")
    if source is not None:
        install_materials(source, OUTPUT, entries, suffixes)
    for name in entries:
        for suffix in suffixes:
            path = OUTPUT / f"{name}{suffix}.png"
            with Image.open(path) as image:
                if image.size != (PHOTOSCAN_SIZE, PHOTOSCAN_SIZE) or image.mode != "RGB":
                    raise ValueError(f"{path} must be an RGB photoscan map at {PHOTOSCAN_SIZE}×{PHOTOSCAN_SIZE}")
    coverage()


if __name__ == "__main__":
    main()
