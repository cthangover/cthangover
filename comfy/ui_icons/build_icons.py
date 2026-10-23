"""Builds the toolbar icons from the pixelized renders in gen/comfy/ui_icons_pixelart.

Runs on the HOST (needs Pillow + numpy, e.g. the ComfyUI venv), cwd = project root:
    python comfy/ui_icons/build_icons.py [--install] [name=k ...]
Every variant becomes gen/ui_icons_build/<name>_<k>.png (256 px) and all of them are laid out on
gen/ui_icons_build/sheet.png over the toolbar's dark background, to pick from. --install copies the
picked variant of each icon (PICK, overridden by name=k arguments) to mods/<mod>/ui/icons/<name>.png.

Pipeline: render (icons.py) -> crop to the object (crop_renders.py) -> AI Pixel Art Enhancer in ComfyUI
(comfy/workflows/pixelart_api.json, 16 px grain on 1024 px = a 64 grid) -> here: one sample per grain cell
gives the 64 px sprite exactly, the magenta is keyed out, small detached blobs dropped, a 1 px dark outline
added (as on the item icons of comfy/rpg_art) and the sprite scaled x4 nearest.
"""
import glob
import os
import re
import shutil
import sys

import numpy as np
from PIL import Image, ImageDraw

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "rpg_art"))
sys.path.insert(0, os.path.dirname(__file__))
from build_village import big_parts, outline  # noqa: E402
from icons import ICONS  # noqa: E402

SRC = "gen/comfy/ui_icons_pixelart"
GRAIN = 16
# colour distance (0..441) from the background colour that still counts as background
BG_TOLERANCE = 60
# min(red, blue) above which a green-poor cell is magenta seen through the object (see magenta_holes)
MAGENTA_MIN = 110
OUT = "gen/ui_icons_build"
# chosen render variant per icon (k of ui_<name>_<k>); 0 when missing
PICK = {"map": 1, "bag": 1, "cards": 1, "mod_manager": 1}
# toolbar background of the sheet (graphite of the HUD)
SHEET_BG = (14, 15, 21, 255)


def renders(name):
    """Latest render per variant k: {k: image}."""
    out = {}
    for path in sorted(glob.glob(os.path.join(SRC, f"uib_{name}_*__*.png")), key=os.path.getmtime):
        m = re.match(rf"uib_{re.escape(name)}_(\d+)__", os.path.basename(path))
        if m:
            out[int(m.group(1))] = path
    return {k: np.asarray(Image.open(p).convert("RGB")).astype(np.float32) for k, p in out.items()}


def object_mask(small):
    """Everything but the background reachable from the border.

    A global "looks magenta" key also ate the dark red card backs; here only cells connected to the
    image edge through background-coloured cells are background, so the same colour inside the object
    stays.
    """
    border = np.concatenate([small[0], small[-1], small[:, 0], small[:, -1]])
    bg = np.median(border, axis=0)
    near = np.linalg.norm(small - bg, axis=2) < BG_TOLERANCE
    h, w = near.shape
    seen = np.zeros_like(near)
    stack = [(y, x) for y in range(h) for x in (0, w - 1)] + [(y, x) for x in range(w) for y in (0, h - 1)]
    while stack:
        y, x = stack.pop()
        if seen[y, x] or not near[y, x]:
            continue
        seen[y, x] = True
        for ny, nx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
            if 0 <= ny < h and 0 <= nx < w and not seen[ny, nx]:
                stack.append((ny, nx))
    return ~seen & ~magenta_holes(small)


def magenta_holes(small):
    """Enclosed background: gear holes, lantern and hourglass glass show the magenta through.

    The flood fill above never reaches them; a strict magenta test (red and blue both high and close, green
    far below) takes them and the pink fringes at the edges without touching dark red card backs or seals.
    """
    r, g, b = small[..., 0], small[..., 1], small[..., 2]
    lo = np.minimum(r, b)
    return (lo > MAGENTA_MIN) & (np.abs(r - b) < 90) & (g < 0.6 * lo)


def icon(img):
    small = img[GRAIN // 2::GRAIN, GRAIN // 2::GRAIN]
    mask = big_parts(object_mask(small), step=1)
    rgba = np.zeros(small.shape[:2] + (4,), np.uint8)
    rgba[mask, :3] = small[mask].astype(np.uint8)
    rgba[mask, 3] = 255
    return np.asarray(Image.fromarray(outline(rgba), "RGBA").resize((256, 256), Image.NEAREST))


def main():
    picks = dict(PICK)
    for a in sys.argv[1:]:
        if "=" in a:
            n, k = a.split("=", 1)
            picks[n] = int(k)
    os.makedirs(OUT, exist_ok=True)
    built = {}
    for name in ICONS:
        for k, img in sorted(renders(name).items()):
            rgba = icon(img)
            Image.fromarray(rgba, "RGBA").save(f"{OUT}/{name}_{k}.png")
            built[(name, k)] = rgba

    # contact sheet: one row per icon, variants side by side at toolbar size (60 px) and at 128 px
    ks = sorted({k for _, k in built}) or [0]
    cell = 128 + 60 + 24
    sheet = Image.new("RGBA", (160 + cell * len(ks), 140 * len(ICONS)), SHEET_BG)
    draw = ImageDraw.Draw(sheet)
    for row, name in enumerate(ICONS):
        y = row * 140 + 6
        draw.text((8, y + 56), name, fill=(232, 229, 222, 255))
        for col, k in enumerate(ks):
            rgba = built.get((name, k))
            if rgba is None:
                continue
            x = 160 + col * cell
            im = Image.fromarray(rgba, "RGBA")
            sheet.alpha_composite(im.resize((128, 128), Image.NEAREST), (x, y))
            sheet.alpha_composite(im.resize((60, 60), Image.LANCZOS), (x + 136, y + 34))
            draw.text((x, y + 128), f"k={k}" + (" *" if picks.get(name, 0) == k else ""), fill=(217, 164, 65, 255))
    sheet.save(f"{OUT}/sheet.png")
    print("sheet", f"{OUT}/sheet.png", len(built), "variants")

    if "--install" in sys.argv:
        for name, (mod, _) in ICONS.items():
            k = picks.get(name, 0)
            src = f"{OUT}/{name}_{k}.png"
            if not os.path.exists(src):
                print("missing", src)
                continue
            dst = f"mods/{mod}/ui/icons/{name}.png"
            os.makedirs(os.path.dirname(dst), exist_ok=True)
            shutil.copyfile(src, dst)
            print("installed", dst)
    print("ok")


if __name__ == "__main__":
    main()
