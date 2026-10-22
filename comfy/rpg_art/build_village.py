"""Builds the village_pack tileset and charsets from the ComfyUI renders in gen/comfy/village.

Runs on the HOST (needs Pillow + numpy, e.g. the ComfyUI venv), cwd = project root:
    python comfy/rpg_art/build_village.py [--install]
Output goes to gen/village_build/ (tileset, charsets, item icons, enlarged previews); --install also
copies the tileset and charsets into mods/village_pack and the item icons to their mods (ICONS).

How a render becomes pixel art:
- objects and characters are drawn on magenta: the background colour is taken from the image border and
  keyed out, the object is cropped to its mask, fitted into its cells and downscaled with alpha-weighted
  box filtering (no magenta fringe), the alpha is thresholded, colours are reduced to a small palette and
  a 1 px dark outline is added;
- textures are downscaled, a 32x32 crop is made seamless by the offset-blend trick (the crop rolled by
  half a tile wraps seamlessly at its edges; the original is kept in the middle) and quantized.
"""
import glob
import json
import os
import shutil
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(__file__))
from assets import ASSETS  # noqa: E402

T = 32
SRC = "gen/comfy/village"
OUT = "gen/village_build"
MOD = "mods/village_pack"

# texture downscale factor (render px per tile px), per texture
TEX_SCALE = {"default": 32}
# textures drawn as one whole panel per tile: no offset-blend (it would wash out the frame at the edges)
PANELS = {"wall", "stone_floor"}
# mean brightness (0..255) some textures are scaled to, hue kept (the render came out too dark)
TEX_LUMA = {"dark_grass": 72}
# charset frame sizes (w, h) and the sprite height inside a frame
CHAR_FRAME = {"c_slime": (32, 32, 22), "c_wolf": (32, 32, 28)}
CHAR_DEFAULT = (32, 48, 44)


def latest(name):
    files = sorted(glob.glob(os.path.join(SRC, f"va_{name}__*.png")), key=os.path.getmtime)
    if not files:
        raise SystemExit(f"no render for {name} in {SRC}")
    return np.asarray(Image.open(files[-1]).convert("RGB")).astype(np.float32)


def key_mask(img):
    border = np.concatenate([img[0], img[-1], img[:, 0], img[:, -1]])
    bg = np.median(border, axis=0)
    dist = np.linalg.norm(img - bg, axis=2)
    r, g, b = img[..., 0], img[..., 1], img[..., 2]
    lo = np.minimum(r, b)
    # magenta and its shadows on the background: red == blue, green far below
    magenta = (lo > 60) & (g < 0.55 * lo) & (np.abs(r - b) < 0.3 * np.maximum(r, b))
    return (dist > 70) & ~magenta


def big_parts(mask, keep=0.15, step=4):
    """Keeps connected parts of the mask with at least `keep` of the largest part's area.

    Drops the small detached figures Qwen likes to add next to a prop. Labelling runs on a grid
    subsampled by `step` (plain BFS, no scipy needed).
    """
    small = mask[::step, ::step]
    h, w = small.shape
    labels = np.zeros((h, w), np.int32)
    areas = [0]
    for sy, sx in zip(*np.nonzero(small)):
        if labels[sy, sx]:
            continue
        n = len(areas)
        labels[sy, sx] = n
        stack, area = [(sy, sx)], 0
        while stack:
            y, x = stack.pop()
            area += 1
            for ny, nx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
                if 0 <= ny < h and 0 <= nx < w and small[ny, nx] and not labels[ny, nx]:
                    labels[ny, nx] = n
                    stack.append((ny, nx))
        areas.append(area)
    good = np.array([a >= keep * max(areas) and a > 0 for a in areas])
    full = np.repeat(np.repeat(good[labels], step, axis=0), step, axis=1)[:mask.shape[0], :mask.shape[1]]
    return mask & full


def downscale(img, mask, w, h):
    """Alpha-weighted box downscale of the masked region to w x h; returns RGBA uint8."""
    a = mask.astype(np.float32)
    prem = img * a[..., None]
    rgb = np.stack([np.asarray(Image.fromarray(prem[..., c]).resize((w, h), Image.BOX)) for c in range(3)], axis=2)
    al = np.asarray(Image.fromarray(a).resize((w, h), Image.BOX))
    out = np.zeros((h, w, 4), np.uint8)
    on = al > 0.5
    out[..., :3] = np.clip(rgb / np.maximum(al, 1e-4)[..., None], 0, 255).astype(np.uint8)
    out[..., 3] = np.where(on, 255, 0)
    out[~on, :3] = 0
    return out


def quantize(rgba, colors):
    im = Image.fromarray(rgba[..., :3].copy())
    q = np.asarray(im.quantize(colors=colors, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE).convert("RGB"))
    out = rgba.copy()
    out[..., :3] = q
    return out


def outline(rgba, factor=0.35):
    """Adds a 1 px dark outline around opaque pixels (into transparent neighbours)."""
    a = rgba[..., 3] > 0
    out = rgba.copy()
    h, w = a.shape
    for dy, dx in ((0, 1), (0, -1), (1, 0), (-1, 0)):
        src = np.zeros_like(a)
        col = np.zeros_like(rgba[..., :3])
        ys, yd = (slice(0, h - dy), slice(dy, h)) if dy >= 0 else (slice(-dy, h), slice(0, h + dy))
        xs, xd = (slice(0, w - dx), slice(dx, w)) if dx >= 0 else (slice(-dx, w), slice(0, w + dx))
        src[yd, xd] = a[ys, xs]
        col[yd, xd] = rgba[ys, xs, :3]
        add = src & ~(out[..., 3] > 0)
        out[add, :3] = (col[add] * factor).astype(np.uint8)
        out[add, 3] = 255
    return out


def sprite(img, mask, box_w, box_h, align="bottom", fill=False, colors=20, line=True):
    """Fits the masked object into a box_w x box_h canvas (1 px kept for the outline)."""
    ys, xs = np.nonzero(mask)
    y0, y1, x0, x1 = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
    img, mask = img[y0:y1, x0:x1], mask[y0:y1, x0:x1]
    pad = 1 if line else 0
    aw, ah = box_w - 2 * pad, box_h - 2 * pad
    if fill:
        w, h = aw, ah
    else:
        s = min(aw / img.shape[1], ah / img.shape[0])
        w, h = max(1, round(img.shape[1] * s)), max(1, round(img.shape[0] * s))
    small = quantize(downscale(img, mask, w, h), colors)
    canvas = np.zeros((box_h, box_w, 4), np.uint8)
    x = (box_w - w) // 2
    y = box_h - pad - h if align == "bottom" else (box_h - h) // 2
    canvas[y:y + h, x:x + w] = small
    return outline(canvas) if line else canvas


def obj(name, box_w=T, box_h=T, scattered=False, **kw):
    img = latest(name)
    mask = key_mask(img)
    return sprite(img, mask if scattered else big_parts(mask), box_w, box_h, **kw)


def texture(name, colors=24):
    img = latest(name)
    s = TEX_SCALE.get(name, TEX_SCALE["default"])
    n = img.shape[0] // s
    small = np.asarray(Image.fromarray(img.astype(np.uint8)).resize((n, n), Image.BOX)).astype(np.float32)
    c = n // 2 - T // 2
    t = small[c:c + T, c:c + T]
    rolled = np.roll(t, (T // 2, T // 2), axis=(0, 1))
    i = np.arange(T)
    edge = np.minimum(i, T - 1 - i)
    wgt = np.clip(np.minimum(edge[:, None], edge[None, :]) / 4.0, 0, 1)[..., None]
    tile = t if name in PANELS else wgt * t + (1 - wgt) * rolled
    if name in TEX_LUMA:
        tile = np.clip(tile * (TEX_LUMA[name] / (tile @ np.array([0.299, 0.587, 0.114])).mean()), 0, 255)
    rgba = np.dstack([tile.astype(np.uint8), np.full((T, T), 255, np.uint8)])
    return quantize(rgba, colors)


def to_cell(t, height=T):
    """Pads a smaller sprite to T wide and `height` high: centred, standing on the bottom edge."""
    if t.shape[:2] == (height, T):
        return t
    out = np.zeros((height, T, 4), np.uint8)
    x = (T - t.shape[1]) // 2
    out[height - t.shape[0]:, x:x + t.shape[1]] = t
    return out


def over(base, top):
    top = to_cell(top)
    out = base.copy()
    m = top[..., 3] > 0
    out[m] = top[m]
    return out


def shade(rgba, factor):
    out = rgba.copy()
    out[..., :3] = (out[..., :3] * factor).astype(np.uint8)
    return out


def build_tileset():
    tex = {n: texture(n) for n, s in ASSETS.items() if s[0] == "texture"}
    grass, dark = tex["grass"], tex["dark_grass"]
    tree = obj("tree", T, 2 * T)
    lamp = obj("lamp", T, 2 * T)

    eave = tex["roof"].copy()
    eave[-6:] = shade(eave[-6:], 0.55)[...]
    eave[-6] = (40, 25, 20, 255)

    shore = grass.copy()
    water = tex["water"]
    sand = tex["sand"]
    for x in range(T):
        e = 16 + int(round(1.5 * np.sin(x / T * 2 * np.pi * 2)))
        shore[e:e + 3, x] = sand[e:e + 3, x]
        shore[e + 3:, x] = water[e + 3:, x]
        shore[e + 3, x] = (225, 240, 250, 255)

    wall = tex["wall"]
    window = obj("window", 22, 18, align="center")
    win = np.zeros((T, T, 4), np.uint8)
    win[6:24, 5:27] = window
    door = obj("door", 22, 30)
    dr = np.zeros((T, T, 4), np.uint8)
    dr[2:32, 5:27] = door
    cave = over(tex["rock_wall"], obj("cave", T, T, fill=True, line=False))

    tiles = [
        grass, over(grass, obj("tufts", align="center", line=False, scattered=True)), over(grass, obj("flowers", align="center", line=False, scattered=True)),
        tex["path"], tex["sand"], water, tex["stone_floor"], tex["wood_floor"],
        tree[T:], tree[:T], obj("bush"), obj("rock"),
        obj("fence", fill=True), obj("sign", 28, 30), obj("chest", 28, 26), obj("chest_open", 28, 28),
        wall, over(wall, win), over(wall, dr), tex["roof"],
        eave, tex["bridge"], obj("tall_grass", T, 24, line=False), obj("well"),
        dark, obj("stump", 26, 22), over(dark, obj("mushrooms", 24, 20, line=False)), cave,
        lamp[T:], lamp[:T], shore, obj("barrel", 24, 28),
        # 32-34: things lying on the ground, picked up as map events
        obj("branches", 28, 20), obj("stones", 26, 20), obj("mushroom", 18, 22),
    ]
    sheet = np.zeros(((len(tiles) + 7) // 8 * T, 8 * T, 4), np.uint8)
    for i, t in enumerate(tiles):
        y, x = (i // 8) * T, (i % 8) * T
        sheet[y:y + T, x:x + T] = to_cell(t)
    return sheet


# item icons: render name -> path under mods/ (sprite of an "item" entity, 256x256)
ICONS = {
    "branches": "rpg_demo_game/items/rpg_demo/branch_bundle.png",
    "stones": "rpg_demo_game/items/rpg_demo/stone_pile.png",
    "mushroom": "rpg_demo_game/items/rpg_demo/forest_mushroom.png",
    "elixir": "rpg_demo_game/items/rpg_demo/healing_elixir.png",
    "coin": "trading/items/money/gold_coin.png",
}


def icon(name):
    """A 64 px pixel-art sprite scaled x4 with nearest filtering: the chunky look of the other item icons."""
    small = obj(name, 64, 64, align="center", colors=24)
    return np.asarray(Image.fromarray(small, "RGBA").resize((256, 256), Image.NEAREST))


def split_views(img, mask):
    """Splits a turnaround render into its views by empty columns; returns the 3 widest, left to right."""
    cols = mask.sum(axis=0) > 2
    spans, start = [], None
    for x, on in enumerate(list(cols) + [False]):
        if on and start is None:
            start = x
        elif not on and start is not None:
            spans.append((start, x))
            start = None
    spans = [s for s in spans if s[1] - s[0] > mask.shape[1] * 0.04]
    spans = sorted(sorted(spans, key=lambda s: s[1] - s[0])[-3:])
    if len(spans) < 3:
        raise SystemExit(f"turnaround: found {len(spans)} views")
    return [(img[:, a:b], mask[:, a:b]) for a, b in spans]


def walk(frame, side):
    """Idle frame -> two step frames: body bobs up 1 px, one leg (or foot pair in profile) lifts."""
    a = frame[..., 3] > 0
    ys = np.nonzero(a.any(axis=1))[0]
    top, bottom = ys.min(), ys.max() + 1
    legs = bottom - max(2, (bottom - top) * 3 // 10)
    xs = np.nonzero(a.any(axis=0))[0]
    mid = (xs.min() + xs.max() + 1) // 2
    steps = []
    for k in (0, 1):
        f = np.zeros_like(frame)
        f[top - 1:legs - 1] = frame[top:legs]
        f[legs - 1:] = frame[legs - 1:]  # the hip row is doubled, so the bob leaves no gap
        half = slice(None, mid) if k == 0 else slice(mid, None)
        if side:
            f[legs:, :] = np.roll(frame[legs:, :], 1 if k == 0 else -1, axis=1)
        else:
            f[legs - 1:bottom - 1, half] = frame[legs:bottom, half]
            f[bottom - 1, half] = 0
        steps.append(f)
    return steps


def build_charset(name):
    fw, fh, sh = CHAR_FRAME.get(name, CHAR_DEFAULT)
    img = latest(name)
    views = split_views(img, big_parts(key_mask(img)))
    tallest = max(m.any(axis=1).sum() for _, m in views)
    frames = []
    for vi, (v, m) in enumerate(views):
        rows = np.nonzero(m.any(axis=1))[0]
        hh = max(4, round(sh * (rows.max() + 1 - rows.min()) / tallest))
        spr = sprite(v, m, fw, hh + 2)
        f = np.zeros((fh, fw, 4), np.uint8)
        f[fh - spr.shape[0]:, :] = spr
        frames.append(f)
    front, left, back = frames
    right = left[:, ::-1]
    sheet = np.zeros((4 * fh, 3 * fw, 4), np.uint8)
    for row, (f, side) in enumerate(((front, False), (left, True), (right, True), (back, False))):
        s0, s1 = walk(f, side)
        for col, fr in enumerate((s0, f, s1)):
            sheet[row * fh:(row + 1) * fh, col * fw:(col + 1) * fw] = fr
    return sheet


def save(rgba, path, preview=0):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    Image.fromarray(rgba, "RGBA").save(path)
    if preview:
        big = Image.fromarray(rgba, "RGBA").resize((rgba.shape[1] * preview, rgba.shape[0] * preview), Image.NEAREST)
        bg = Image.new("RGBA", big.size, (255, 0, 255, 255))
        bg.alpha_composite(big)
        bg.convert("RGB").save(path.replace(".png", "_x%d.png" % preview))


def render_map(sheet, map_path, out_path, chars):
    m = json.load(open(map_path, encoding="utf-8"))["Items"][0]
    layers = {k: [r.split() for r in v] for k, v in m["Layers"].items()}
    h, w = len(layers["Back"]), len(layers["Back"][0])
    canvas = Image.new("RGBA", (w * T, h * T), (0, 0, 0, 255))
    tiles = Image.fromarray(sheet, "RGBA")

    def put(layer):
        for y, row in enumerate(layers.get(layer, [])):
            for x, c in enumerate(row):
                if c != ".":
                    i = int(c)
                    canvas.alpha_composite(tiles.crop(((i % 8) * T, (i // 8) * T, (i % 8 + 1) * T, (i // 8 + 1) * T)), (x * T, y * T))

    put("Back")
    put("Main")
    for ev in m.get("Events", []):
        spr = ev.get("Sprite")
        if spr and spr in chars:
            cs = Image.fromarray(chars[spr], "RGBA")
            fw, fh = cs.width // 3, cs.height // 4
            canvas.alpha_composite(cs.crop((fw, 0, 2 * fw, fh)), (ev["X"] * T + (T - fw) // 2, ev["Y"] * T + T - fh))
        elif "Tile" in ev:
            i = ev["Tile"]
            canvas.alpha_composite(tiles.crop(((i % 8) * T, (i // 8) * T, (i % 8 + 1) * T, (i // 8 + 1) * T)), (ev["X"] * T, ev["Y"] * T))
    put("Front")
    canvas.resize((canvas.width * 2, canvas.height * 2), Image.NEAREST).convert("RGB").save(out_path)


def main():
    sheet = build_tileset()
    save(sheet, f"{OUT}/tilesets/village/village.png", preview=4)
    chars = {}
    for name, spec in ASSETS.items():
        if spec[0] == "turnaround":
            cs = build_charset(name)
            chars[name[2:]] = cs
            save(cs, f"{OUT}/charsets/village/{name[2:]}.png", preview=4)
    for mp in glob.glob("mods/rpg_demo_game/maps/*.json"):
        render_map(sheet, mp, f"{OUT}/map_{os.path.basename(mp)[:-5]}.png", chars)
    for name, path in ICONS.items():
        save(icon(name), f"{OUT}/icons/{os.path.basename(path)}")
    if "--install" in sys.argv:
        for path in [f"tilesets/village/village.png"] + [f"charsets/village/{c}.png" for c in chars]:
            shutil.copyfile(f"{OUT}/{path}", f"{MOD}/{path}")
            print("installed", path)
        for path in ICONS.values():
            os.makedirs(os.path.dirname(f"mods/{path}"), exist_ok=True)
            shutil.copyfile(f"{OUT}/icons/{os.path.basename(path)}", f"mods/{path}")
            print("installed", path)
    print("ok")


if __name__ == "__main__":
    main()
