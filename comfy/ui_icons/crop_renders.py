"""Crops the icon renders to their object for pixelization (HOST, needs Pillow + numpy).

    python comfy/ui_icons/crop_renders.py <ComfyUI input dir>

Every render gen/comfy/ui_icons/ui_<name>_<k>__*.png is cut to a square around its object (magenta key of
build_village.py, a margin of one 64-grid cell so the outline fits) on magenta, scaled to 1024 px and saved as
<input dir>/ui_icons/<name>_<k>.png. With pixel_size 16 the pixel-art node then gives a 64 x 64 grid where
the object fills the icon instead of ~70 % of it.
"""
import glob
import os
import re
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "rpg_art"))
from build_village import big_parts, key_mask  # noqa: E402

SRC = "gen/comfy/ui_icons"
SIZE = 1024
MAGENTA = (255, 0, 255)


def main():
    out = os.path.join(sys.argv[1], "ui_icons")
    os.makedirs(out, exist_ok=True)
    latest = {}
    for path in sorted(glob.glob(os.path.join(SRC, "ui_*__*.png")), key=os.path.getmtime):
        m = re.match(r"ui_(.+_\d+)__", os.path.basename(path))
        if m:
            latest[m.group(1)] = path
    for key, path in sorted(latest.items()):
        img = np.asarray(Image.open(path).convert("RGB")).astype(np.float32)
        mask = big_parts(key_mask(img))
        ys, xs = np.nonzero(mask)
        y0, y1, x0, x1 = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
        side = max(y1 - y0, x1 - x0)
        side = int(side * 64 / 62)  # one grid cell of margin on each side
        canvas = Image.new("RGB", (side, side), MAGENTA)
        obj = Image.fromarray(np.where(mask[..., None], img, MAGENTA).astype(np.uint8)[y0:y1, x0:x1])
        canvas.paste(obj, ((side - (x1 - x0)) // 2, (side - (y1 - y0)) // 2))
        canvas.resize((SIZE, SIZE), Image.LANCZOS).save(os.path.join(out, key + ".png"))
        print("cropped", key, side)


if __name__ == "__main__":
    main()
