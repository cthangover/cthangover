"""Install 1K color, OpenGL normal and linear ORM maps from terrain photoscans."""
import shutil
import tempfile
from pathlib import Path

import numpy as np
from PIL import Image

PHOTOSCAN_SIZE = 1024


def rgb(path):
    with Image.open(path) as image:
        if image.size != (PHOTOSCAN_SIZE, PHOTOSCAN_SIZE):
            raise ValueError(f"{path} must be {PHOTOSCAN_SIZE}×{PHOTOSCAN_SIZE}")
        return image.convert("RGB")


def grayscale(path):
    with Image.open(path) as image:
        values = np.asarray(image)
        if values.shape != (PHOTOSCAN_SIZE, PHOTOSCAN_SIZE):
            raise ValueError(f"{path} must be a single-channel {PHOTOSCAN_SIZE}×{PHOTOSCAN_SIZE} image")
    if values.dtype == np.dtype("uint16"):
        return np.rint(values.astype(np.float32) / 257).astype(np.uint8)
    if values.dtype != np.dtype("uint8"):
        raise ValueError(f"Unexpected grayscale format for {path}")
    return values


def color_gain(image, gains):
    color = np.asarray(image, dtype=np.float32) / 255
    linear = np.where(color <= .04045, color / 12.92, ((color + .055) / 1.055) ** 2.4)
    linear = np.clip(linear * np.asarray(gains), 0, 1)
    color = np.where(linear <= .0031308, 12.92 * linear, 1.055 * linear ** (1 / 2.4) - .055)
    return Image.fromarray(np.rint(color * 255).astype(np.uint8))


def maps(folder, entry):
    if entry.get("format") == "ambientcg":
        prefix = entry["folder"] + "_1K-PNG_"
        color = rgb(folder / (prefix + "Color.png"))
        normal = rgb(folder / (prefix + "NormalGL.png"))
        ao = grayscale(folder / (prefix + "AmbientOcclusion.png"))
        roughness = grayscale(folder / (prefix + "Roughness.png"))
        orm = Image.fromarray(np.stack((ao, roughness, np.zeros_like(ao)), axis=-1))
    else:
        color = rgb(folder / "color.png")
        normal = rgb(folder / "normal.png")
        orm = rgb(folder / "orm.png")
    if "linear_color_gain" in entry:
        color = color_gain(color, entry["linear_color_gain"])
    return color, normal, orm


def install_materials(source, output, entries, suffixes):
    with tempfile.TemporaryDirectory(prefix="cthangover-terrain-materials-") as directory:
        staged = Path(directory)
        for layer, entry in entries.items():
            textures = maps(source / entry["folder"], entry)
            for suffix, image in zip(suffixes, textures, strict=True):
                image.save(staged / f"{layer}{suffix}.png")
        output.mkdir(parents=True, exist_ok=True)
        for path in sorted(staged.iterdir()):
            shutil.copyfile(path, output / path.name)
