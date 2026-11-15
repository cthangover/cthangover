"""Write tiny independent decoder fixtures; the DXT1 block is green in both DDS and KTX1."""
import argparse
import hashlib
import json
from pathlib import Path
import struct
from PIL import Image


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--out', type=Path, required=True)
    out = parser.parse_args().out
    out.mkdir(parents=True, exist_ok=True)
    image = Image.new('RGB', (4, 4), (16, 204, 32))
    for ext, fmt in [('png', 'PNG'), ('jpg', 'JPEG'), ('webp', 'WEBP'), ('tga', 'TGA'), ('bmp', 'BMP')]:
        image.save(out / ('texture.' + ext), format=fmt)
    (out / 'texture.svg').write_text('<svg xmlns="http://www.w3.org/2000/svg" width="4" height="4"><path fill="#10cc20" d="M0 0h4v4H0z"/></svg>', encoding='utf-8')
    block = struct.pack('<HHI', 0x07E0, 0x001F, 0)
    # DDS_HEADER (124 bytes), DDS_PIXELFORMAT (32 bytes), one BC1/DXT1 block.
    header = [124, 0x81007, 4, 4, 8, 0, 1] + [0] * 11 + [32, 4, int.from_bytes(b'DXT1', 'little'), 0, 0, 0, 0, 0] + [0x1000, 0, 0, 0, 0]
    (out / 'texture.dds').write_bytes(b'DDS ' + struct.pack('<31I', *header) + block)
    ktx_header = [0x04030201, 0, 1, 0, 0x83F1, 0x1908, 4, 4, 0, 0, 1, 1, 0]
    (out / 'texture.ktx').write_bytes(b'\xABKTX 11\xBB\r\n\x1A\n' + struct.pack('<13I', *ktx_header) + struct.pack('<I', len(block)) + block)
    (out / 'textures-provenance.json').write_text(json.dumps({p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in out.glob('texture.*')}, indent=2) + '\n', encoding='utf-8')


if __name__ == '__main__':
    main()
