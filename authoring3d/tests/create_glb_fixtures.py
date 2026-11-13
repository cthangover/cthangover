"""Reproduce two tiny textured GLBs with independent hierarchies and upward-facing geometry."""

import json
from pathlib import Path
import struct
import zlib


def png_chunk(kind, data):
    return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data) & 0xFFFFFFFF)


def texture_png():
    pixels = b"\x00" + bytes((40, 150, 210, 255)) * 2
    return (b"\x89PNG\r\n\x1a\n" + png_chunk(b"IHDR", struct.pack(">2I5B", 2, 2, 8, 6, 0, 0, 0))
            + png_chunk(b"IDAT", zlib.compress(pixels * 2)) + png_chunk(b"IEND", b""))


def write_fixture(path, nodes):
    positions = struct.pack("<9f", -1, 0, -1, 0, 0, 1, 1, 0, -1)
    uv = struct.pack("<6f", 0, 0, 0.5, 1, 1, 0)
    normals = struct.pack("<9f", 0, 1, 0, 0, 1, 0, 0, 1, 0)
    image = texture_png()
    binary = positions + uv + normals + image
    image_length = len(image)
    binary += b"\x00" * (-len(binary) % 4)
    document = {
        "asset": {"version": "2.0", "generator": "cthangover independent resource contract"},
        "scene": 0, "scenes": [{"name": "Fixture", "nodes": [0]}], "nodes": nodes,
        "meshes": [{"primitives": [{"attributes": {"POSITION": 0, "TEXCOORD_0": 1, "NORMAL": 2}, "material": 0}]}],
        "materials": [{"pbrMetallicRoughness": {"baseColorTexture": {"index": 0}, "metallicFactor": 0, "roughnessFactor": 1}}],
        "textures": [{"source": 0}], "images": [{"bufferView": 3, "mimeType": "image/png"}],
        "buffers": [{"byteLength": len(binary)}],
        "bufferViews": [
            {"buffer": 0, "byteOffset": 0, "byteLength": 36, "target": 34962},
            {"buffer": 0, "byteOffset": 36, "byteLength": 24, "target": 34962},
            {"buffer": 0, "byteOffset": 60, "byteLength": 36, "target": 34962},
            {"buffer": 0, "byteOffset": 96, "byteLength": image_length}],
        "accessors": [
            {"bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3", "min": [-1, 0, -1], "max": [1, 0, 1]},
            {"bufferView": 1, "componentType": 5126, "count": 3, "type": "VEC2"},
            {"bufferView": 2, "componentType": 5126, "count": 3, "type": "VEC3"}],
    }
    encoded = json.dumps(document, separators=(",", ":")).encode()
    encoded += b" " * (-len(encoded) % 4)
    packed = (struct.pack("<5I", 0x46546C67, 2, 28 + len(encoded) + len(binary), len(encoded), 0x4E4F534A)
              + encoded + struct.pack("<2I", len(binary), 0x004E4942) + binary)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(packed)


def main():
    root = Path(__file__).resolve().parents[2] / "mods_private/world3d_base/test_suites/contracts/models3d/world3d_tests"
    write_fixture(root / "alpha.glb", [
        {"name": "Hull", "children": [1]}, {"name": "UpperDeck", "mesh": 0, "translation": [0, 2, 0]}])
    write_fixture(root / "beta.glb", [
        {"name": "Geometry", "children": [1]}, {"name": "Interior", "children": [2]},
        {"name": "Platform", "mesh": 0, "translation": [0, 2, 0]}])


if __name__ == "__main__":
    main()
