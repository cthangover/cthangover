"""Builds the village pond's water autotile (RPG Maker A2 layout, 2x3 tiles) from the village atlas.

Water, the shore band and the grass beyond the rounded outer corners are taken pixel by pixel
from atlas tiles (water 5, sand 4, grass 0), so the block matches the tileset. Layout, in
tiles: (0,0) preview (an isolated pond), (1,0) inner corners, (0..1,1..2) the framed 2x2 patch
whose ring gives the outer corners and edges and whose middle the fill (see RpgAutotiles).

Usage (host): python make_water_autotile.py <village.png> <out.png>
"""
import math
import sys

from PIL import Image

TILE = 32
BAND = 5          # shore band width, px
RADIUS = 11       # rounding of outer corners, px
INNER_GRASS = 3   # grass radius in an inner corner, px
WATER, SAND, GRASS = 5, 4, 0
COLUMNS = 8


def tile_pixel(atlas, index, x, y):
    col, row = index % COLUMNS, index // COLUMNS
    return atlas.getpixel((col * TILE + x % TILE, row * TILE + y % TILE))


def shade(pixel, factor):
    r, g, b, a = pixel
    return (int(r * factor), int(g * factor), int(b * factor), a)


def rounded_distance(x, y, w, h, r):
    """Distance from (x, y) inside a w*h rectangle to its border with rounded corners; <0 outside."""
    cx = min(max(x, r), w - 1 - r)
    cy = min(max(y, r), h - 1 - r)
    if (x < r or x > w - 1 - r) and (y < r or y > h - 1 - r):
        return r - math.hypot(x - cx, y - cy)
    return min(x, y, w - 1 - x, h - 1 - y)


def patch_pixel(atlas, x, y, d):
    if d < 0:
        return tile_pixel(atlas, GRASS, x, y)
    if d < 1:
        return shade(tile_pixel(atlas, SAND, x, y), 0.7)
    if d < BAND:
        return tile_pixel(atlas, SAND, x, y)
    if d < BAND + 1:
        return shade(tile_pixel(atlas, WATER, x, y), 0.8)
    return tile_pixel(atlas, WATER, x, y)


def main(src, out):
    atlas = Image.open(src).convert("RGBA")
    block = Image.new("RGBA", (TILE * 2, TILE * 3))

    # Preview: one pond cell alone.
    for y in range(TILE):
        for x in range(TILE):
            block.putpixel((x, y), patch_pixel(atlas, x, y, rounded_distance(x, y, TILE, TILE, RADIUS)))

    # The framed 2x2 patch.
    size = TILE * 2
    for y in range(size):
        for x in range(size):
            d = rounded_distance(x, y, size, size, RADIUS)
            block.putpixel((x, TILE + y), patch_pixel(atlas, x, y, d))

    # Inner corners: water with land only at the very corner of each quarter.
    half = TILE // 2
    corners = [(0, 0), (TILE - 1, 0), (0, TILE - 1), (TILE - 1, TILE - 1)]
    for y in range(TILE):
        for x in range(TILE):
            qx, qy = (1 if x >= half else 0), (1 if y >= half else 0)
            cx, cy = corners[qy * 2 + qx]
            d = math.hypot(x - cx, y - cy) - INNER_GRASS
            block.putpixel((TILE + x, y), patch_pixel(atlas, x, y, d))

    block.save(out)
    print("wrote", out, block.size)


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
