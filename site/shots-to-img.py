#!/usr/bin/env python3
"""Converts the game screenshots taken by make-shots.ps1 into the site's images.

    python site/shots-to-img.py <shots dir> <out dir>

Only the shots the site uses are converted: the gallery list of js/main.js (`f: "<suite>__<name>"`) and the
/img/shots/ references of the pages. Each <shots dir>/<suite>/<name>.png becomes <out dir>/<suite>__<name>.jpg,
scaled to 1600 px wide (the widest the page shows them), plus <suite>__<name>.thumb.jpg 640 px wide for the
gallery grid. The test suites take more frames than the page shows; any other jpg in <out dir> is deleted, so a
shot dropped from the page does not linger in the site folder. Needs Pillow.
"""
import glob
import os
import re
import sys

from PIL import Image

FULL_WIDTH = 1600
THUMB_WIDTH = 640
SITE = os.path.dirname(os.path.abspath(__file__))
# `f: "site_shots__novel_wolf"` in the gallery list, `/img/shots/<shot>.jpg` / `.thumb.jpg` in the pages
USE_RE = re.compile(r'f:\s*"(\w+__\w+)"|/img/shots/(\w+__\w+?)(?:\.thumb)?\.jpg')


def used_shots():
    used = set()
    for path in [os.path.join(SITE, "js", "main.js")] + glob.glob(os.path.join(SITE, "*.html")):
        if os.path.basename(path).startswith("ai_"):
            continue
        with open(path, encoding="utf-8") as f:
            for m in USE_RE.finditer(f.read()):
                used.add(m.group(1) or m.group(2))
    return used


def save(img, width, path):
    h = round(img.height * width / img.width)
    img.resize((width, h), Image.LANCZOS).save(path, "JPEG", quality=84, optimize=True, progressive=True)


def main():
    if len(sys.argv) != 3:
        print(__doc__)
        return 2
    src, out = sys.argv[1], sys.argv[2]
    os.makedirs(out, exist_ok=True)
    used = used_shots()
    count = 0
    for shot in sorted(used):
        suite, name = shot.split("__", 1)
        png = os.path.join(src, suite, name + ".png")
        if not os.path.isfile(png):
            print("MISSING", png)
            continue
        img = Image.open(png).convert("RGB")
        save(img, FULL_WIDTH, os.path.join(out, shot + ".jpg"))
        save(img, THUMB_WIDTH, os.path.join(out, shot + ".thumb.jpg"))
        count += 1
    for path in glob.glob(os.path.join(out, "*.jpg")):
        if os.path.basename(path).split(".")[0] not in used:
            os.remove(path)
            print("removed", os.path.basename(path))
    print(f"OK  {count} of {len(used)} screenshots -> {out}")
    return 0 if count == len(used) else 1


if __name__ == "__main__":
    sys.exit(main())
