#!/usr/bin/env python3
"""Convert approved sRGB game captures into full and thumbnail site images.

Usage: python site/shots-to-img.py <captures directory> <output directory>
Optional --site-root selects a prepared site checkout for gallery discovery.
All referenced PNGs must exist before any existing site image is replaced.
"""
import argparse
from pathlib import Path
import re
import tempfile

from PIL import Image

FULL_WIDTH = 1600
THUMB_WIDTH = 640
SITE = Path(__file__).resolve().parent
USE_RE = re.compile(r'f:\s*"(\w+__\w+)"|/img/shots/(\w+__\w+?)(?:\.thumb)?\.jpg')


def used_shots(site=SITE):
    used = set()
    for path in [site / 'js/main.js'] + sorted(site.glob('*.html')):
        if path.name.startswith('ai_'):
            continue
        for match in USE_RE.finditer(path.read_text(encoding='utf-8')):
            used.add(match[1] or match[2])
    return used


def save(image, width, path):
    height = round(image.height * width / image.width)
    with image.resize((width, height), Image.Resampling.LANCZOS) as scaled:
        scaled.save(path, 'JPEG', quality=88, optimize=True, progressive=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('source', type=Path)
    parser.add_argument('output', type=Path)
    parser.add_argument('--site-root', type=Path, default=SITE)
    args = parser.parse_args()
    used = used_shots(args.site_root)
    sources = {}
    for shot in sorted(used):
        suite, name = shot.split('__', 1)
        sources[shot] = args.source / suite / (name + '.png')
    missing = [str(path) for path in sources.values() if not path.is_file()]
    if missing:
        print('Missing captures; output left unchanged:\n' + '\n'.join(missing))
        return 1
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='.site-shots-', dir=args.output.parent) as temporary:
        prepared = Path(temporary)
        for shot, path in sources.items():
            with Image.open(path) as original, original.convert('RGB') as image:
                save(image, FULL_WIDTH, prepared / (shot + '.jpg'))
                save(image, THUMB_WIDTH, prepared / (shot + '.thumb.jpg'))
        args.output.mkdir(parents=True, exist_ok=True)
        for path in prepared.iterdir():
            path.replace(args.output / path.name)
    for path in args.output.glob('*.jpg'):
        if path.name.split('.')[0] not in used:
            path.unlink()
    print(f'OK {len(sources)} screenshots and thumbnails -> {args.output}')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
