#!/usr/bin/env python3
"""Rebuild the reference world's terrain, material bindings and grounded specimens."""
import json
import math
import struct
from pathlib import Path

import numpy as np
from PIL import Image

DEMO = Path(__file__).resolve().parents[2] / "mods_private/vegetation3d_base/test_suites/biomes"
WORLD = 256
TILE = 128
MARGIN = TILE
LOCATION = "nature/biomes"
MATERIAL_SIZE = 1024
LAYER_NAMES = ("meadow", "leaf_litter", "silt", "sand", "spruce_needles", "pine_needles", "moss", "peat")
ZONES = {
    "lake": ((166, 55), (81, 80), "ellipse", 4.4, (0.016, 0.048, 0.051), (.12, .15, .088), 2.3, (.06, .02)),
    "river": ((230, 176), (52, 160), "rectangle", 2.0, (.048, .109, .085), (.23, .25, .15), .86, (.03, .85)),
    "bog": ((156, 125), (49, 34), "ellipse", 1.0, (.063, .066, .031), (.17, .14, .079), 2.2, (0, 0)),
}


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8")


def catalog(folder, name, kind, values):
    write_json(DEMO / folder / (name + ".json"), {"Kind": kind, "Items": values})


def smoothstep(edge0, edge1, value):
    t = np.clip((value - edge0) / (edge1 - edge0), 0, 1)
    return t * t * (3 - 2 * t)


def landscape_noise(shape, spacing, seed):
    height, width = shape
    samples = np.random.default_rng(seed).random((math.ceil(height / spacing) + 1, math.ceil(width / spacing) + 1))
    return np.asarray(Image.fromarray(samples.astype(np.float32)).resize((width, height), Image.Resampling.BICUBIC))


def terrain(root):
    z, x = np.mgrid[-MARGIN:WORLD + MARGIN + 1, -MARGIN:WORLD + MARGIN + 1]
    coarse = .48 * np.sin(x * .034 + z * .019) + .34 * np.cos(z * .069 - x * .036)
    fine = .19 * np.sin(x * .12 + z * .18) + .10 * np.cos(x * .25 - z * .15)
    hills = 2.0 * np.exp(-((x - 26) / 38) ** 2 - ((z - 28) / 40) ** 2)
    hills += 2.5 * np.exp(-((x - 148) / 51) ** 2 - ((z - 217) / 34) ** 2)
    outside = np.hypot(np.maximum.reduce((-x, x - WORLD, np.zeros_like(x))),
                       np.maximum.reduce((-z, z - WORLD, np.zeros_like(z))))
    foothills = 13 + 8 * np.sin(x * .031 + z * .027) + 5.4 * np.cos(z * .068 - x * .033)
    hills += foothills * smoothstep(0, 62, outside) + 21 * smoothstep(40, 118, outside)
    height = 1.45 + coarse + fine + hills

    lake = np.hypot((x - 166) / 37, (z - 55) / 36)
    lake += .034 * np.sin(x * .22 - z * .16) + .027 * np.sin(z * .29)
    lake_bed = (-4.25 * (1 - smoothstep(.15, 1, lake)) + .18 * np.sin(x * .3 + z * .14))
    lake_bed += 2.0 * smoothstep(.76, 1.28, lake)

    river_x = 231 + 9 * np.sin((z - 145) * .035) + 2 * np.sin(z * .08)
    river = np.abs(x - river_x) / (6.9 + 1.3 * np.sin(z * .047))
    river += .08 * np.sin(z * .36 + x * .31)
    # The two channel ends meet high ground before the finite water rectangle ends.
    river += 1.7 * (1 - smoothstep(95, 115, z)) + 1.7 * smoothstep(240, 257, z)
    river_bed = -1.73 * (1 - smoothstep(.25, 1.14, river)) + 1.8 * smoothstep(.70, 1.60, river)

    bog = np.hypot((x - 156) / 22, (z - 125) / 15)
    bog += .11 * np.sin(x * .64 - z * .33) + .12 * np.cos(x * .24 + z * .67)
    bog_bed = -.83 * (1 - smoothstep(.38, 1.05, bog)) + .9 * smoothstep(.74, 1.3, bog)
    bog_bed += .53 * np.maximum(0, np.sin(x * .44 + z * .26) * np.cos(z * .38 - x * .15)) ** 2
    for distance, bed in [(lake, lake_bed), (river, river_bed), (bog, bog_bed)]:
        mix = 1 - smoothstep(1.0, 1.38, distance)
        height = height * (1 - mix) + bed * mix

    profiles = json.loads((DEMO / "biomes/profiles.json").read_text(encoding="utf-8"))
    areas = {}
    for area in profiles["Areas"]:
        cx, cz = area["Center"]
        rx, rz = area["Radius"]
        q = np.hypot((x - cx) / rx, (z - cz) / rz) + .06 * np.sin(x * .20 + z * .17) + .03 * np.sin(x * .43)
        areas[area["Id"]] = 1 - smoothstep(.79, 1.08, q)

    shore = np.maximum(1 - smoothstep(.91, 1.22, lake), (1 - smoothstep(.78, 1.37, river)) * .85)
    silt = (1 - smoothstep(-1.18, -.12, height)) * np.maximum(1 - smoothstep(.93, 1.10, lake), 1 - smoothstep(1, 1.22, river))
    broadleaf = np.maximum(areas["deciduous"], areas["north_wood"] * .64)
    leaves = broadleaf * (1 - shore) * (.88 + .12 * np.sin(x * .25 - z * .29))
    sand = shore * (1 - silt)
    grass = np.maximum(0, 1 - leaves - sand - silt)
    weights = np.stack(np.broadcast_arrays(grass, leaves, silt, sand), axis=-1)

    dry = smoothstep(.04, .34, height)
    spruce = np.maximum(areas["spruce"], areas["east_wood"] * .72) * dry
    pine = np.maximum(areas["pine"], areas["west_wood"] * .74) * (1 - spruce) * dry
    moss_noise = .62 * landscape_noise(x.shape, 12, 4384) + .38 * landscape_noise(x.shape, 2.8, 8531)
    moss = np.maximum(spruce, pine * .43) * smoothstep(.42, .66, moss_noise)
    peat = (1 - smoothstep(.74, 1.41, bog)) * (.85 + .15 * np.sin(x * .24 + z * .43))
    detail = np.stack([spruce * (1 - moss), pine * (1 - moss), moss, peat], axis=-1)

    root.mkdir(parents=True, exist_ok=True)
    Image.fromarray(np.uint8(np.clip(detail, 0, 1) * 255)).save(root / "coverage.png")
    tiles = []
    regions = []
    addresses = []
    for tz in range(-1, WORLD // TILE + 1):
        for tx in range(-1, WORLD // TILE + 1):
            x0, z0 = tx * TILE + MARGIN, tz * TILE + MARGIN
            area = np.s_[z0:z0 + TILE + 1, x0:x0 + TILE + 1]
            key = f"{tx}_{tz}"
            height[area].astype("<f4").tofile(root / f"{key}.r32")
            np.rint(weights[area] * 255).astype(np.uint8).tofile(root / f"{key}.weights")
            tiles.append({"Id": f"{LOCATION}/{key}", "Heights": f"{LOCATION}/{key}.r32", "Encoding": "r32",
                          "Weights": f"{LOCATION}/{key}.weights", "Layers": [0, 1, 2, 3]})
            region_id = f"{LOCATION}/region/{key}"
            regions.append({"Id": region_id, "X": tx, "Z": tz,
                            "Tiles": [{"Id": "tile", "Asset": f"{LOCATION}/{key}", "X": 0, "Z": 0}]})
            addresses.append({"Id": key, "Asset": region_id})
    catalog("terrains", "tiles", "terrain_tile", tiles)
    catalog("terrains", "region", "terrain_region", regions)
    catalog("terrains", "world", "terrain_world",
            [{"Id": LOCATION + "/terrain", "Location": LOCATION, "Material": LOCATION + "/surface",
              "RegionCells": TILE, "TileCells": TILE, "Spacing": 1, "PatchCells": 32, "MaxLod": 3,
              "UseGpuGrid": True, "LodErrorPixels": 4, "ViewDistance": 360, "CollisionRadius": 21,
              "Regions": addresses}])
    return height


def surface(root):
    # Keep photographed inputs out of the terrain bake. A rebuild consumes the installed runtime maps.
    layers = []
    for name in LAYER_NAMES:
        files = {"Albedo": f"{name}.png", "Normal": f"{name}_n.png", "Orm": f"{name}_orm.png"}
        for filename in files.values():
            with Image.open(root / filename) as image:
                if image.size != (MATERIAL_SIZE, MATERIAL_SIZE) or image.mode != "RGB":
                    raise ValueError(f"{filename} must be a {MATERIAL_SIZE}×{MATERIAL_SIZE} RGB map")
        layers.append({"Id": name, **{key: f"{LOCATION}/{filename}" for key, filename in files.items()}})
    detail = {"Mask": LOCATION + "/coverage.png", "Bounds": [-MARGIN, -MARGIN, WORLD + 2 * MARGIN, WORLD + 2 * MARGIN],
              "Layers": LAYER_NAMES[4:], "Scales": [.74, .50, .49, .44]}
    catalog("terrains", "material", "terrain_material", [{"Id": LOCATION + "/surface", "TextureSize": MATERIAL_SIZE,
            "UvScale": .52, "FarDistance": 145, "Layers": layers, "Detail": detail}])


def anthill(root):
    """Build an asymmetric mound with fine litter on its sloped sides."""
    segments = 72
    rng = np.random.default_rng(18063)
    rings = [(1.17, -.04), (1.11, .08), (1.01, .19), (.86, .31), (.73, .43), (.61, .55),
             (.49, .66), (.38, .77), (.26, .89), (.18, .99), (.11, 1.07), (.055, 1.13), (0, 1.17)]
    shell = []
    for radius, height in rings:
        ring = []
        for index in range(segments):
            angle = math.tau * index / segments
            r = radius * (1 + .14 * math.sin(angle * 3) + .065 * math.cos(angle * 7) + rng.uniform(-.047, .047))
            ring.append((r * math.cos(angle) + .24 * height, height + rng.uniform(-.037, .037) * radius,
                         .81 * r * math.sin(angle) + .13 * height))
        shell.append(ring)
    points = np.asarray(shell)
    smooth_normals = np.zeros_like(points)
    for j in range(len(rings) - 1):
        spoke = points[j + 1] - points[max(0, j - 1)]
        tangent = np.roll(points[j], -1, axis=0) - np.roll(points[j], 1, axis=0)
        normals = np.cross(spoke, tangent)
        smooth_normals[j] = normals / np.linalg.norm(normals, axis=1, keepdims=True)
    smooth_normals[-1, :, 1] = 1
    tints = rng.uniform(.73, 1.08, (len(rings), segments, 1)) * np.array([.9, .87, .79])
    vertices, normals, colors, uv = [], [], [], []
    for j in range(len(rings) - 1):
        for i in range(segments):
            k = (i + 1) % segments
            triangles = [((j, i), (j + 1, i), (j, k))]
            if j < len(rings) - 2:
                triangles.append(((j, k), (j + 1, i), (j + 1, k)))
            for triangle in triangles:
                for ring, index in triangle:
                    x, y, z = points[ring, index]
                    vertices.append((x, y, z))
                    normals.append(smooth_normals[ring, index])
                    colors.append(tints[ring, index])
                    uv.append((x * .82 + y * .14, z * .82 - y * .27))
    data = [np.array(channel, dtype="<f4") for channel in (vertices, normals, colors, uv)]
    binary = bytearray()
    views = []
    for channel in data:
        views.append({"buffer": 0, "byteOffset": len(binary), "byteLength": channel.nbytes, "target": 34962})
        binary.extend(channel.tobytes())
    texture = (root / "spruce_needles.png").read_bytes()
    views.append({"buffer": 0, "byteOffset": len(binary), "byteLength": len(texture)})
    binary.extend(texture)
    binary.extend(b"\0" * (-len(binary) % 4))
    model = {"asset": {"version": "2.0"}, "scene": 0, "scenes": [{"nodes": [0]}], "nodes": [{"mesh": 0}],
             "meshes": [{"primitives": [{"attributes": {"POSITION": 0, "NORMAL": 1, "COLOR_0": 2, "TEXCOORD_0": 3}, "material": 0}]}],
             "materials": [{"pbrMetallicRoughness": {"roughnessFactor": .97, "metallicFactor": 0, "baseColorTexture": {"index": 0}}}],
             "samplers": [{"magFilter": 9729, "minFilter": 9987, "wrapS": 10497, "wrapT": 10497}],
             "textures": [{"sampler": 0, "source": 0}], "images": [{"bufferView": 4, "mimeType": "image/png"}],
             "buffers": [{"byteLength": len(binary)}], "bufferViews": views,
             "accessors": [{"bufferView": i, "componentType": 5126, "count": len(vertices), "type": kind}
                           for i, kind in enumerate(("VEC3", "VEC3", "VEC3", "VEC2"))]}
    model["accessors"][0].update({"min": data[0].min(axis=0).tolist(), "max": data[0].max(axis=0).tolist()})
    metadata = json.dumps(model, separators=(",", ":")).encode()
    metadata += b" " * (-len(metadata) % 4)
    content = struct.pack("<I4s", len(metadata), b"JSON") + metadata + struct.pack("<I4s", len(binary), b"BIN\0") + binary
    path = DEMO / "models3d" / LOCATION / "anthill.glb"
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(struct.pack("<4sII", b"glTF", 2, 12 + len(content)) + content)
    key = LOCATION + "/anthill"
    catalog("definitions", "assets", "model3d_asset", [{"Id": key, "Source": key + ".glb",
            "Options": {"GenerateLods": False, "MaxTextureSize": 512}}])
    catalog("definitions", "profiles", "model3d_profile", [{"Id": key, "Asset": key}])
    catalog("definitions", "objects", "object3d", [{"Id": key, "Profile": key}])


def water():
    bodies, patches = [], []
    for index, (tag, (center, size, shape, depth, color, shore, absorption, flow)) in enumerate(ZONES.items()):
        bodies.append({"Id": tag, "Body": {"Shape": shape, "Center": center, "Size": size, "Level": 0, "BottomDepth": depth,
                       "Components": [
                           {"Part": "surface", "Color": color, "Opacity": .93, "Roughness": .23 + .14 * (tag == "bog")},
                           {"Part": "shore", "ShallowColor": shore, "Absorption": absorption, "FoamWidth": .16},
                           {"Part": "flow", "Velocity": flow},
                           {"Part": "underwater", "Color": color, "Density": .105},
                           {"Part": "waves", "Items": [{"Amplitude": .027 if tag == "lake" else .012,
                            "Length": 4.0 if tag == "lake" else 2.8, "Speed": .6, "Direction": 80 if tag == "river" else 35}]}]}})
        habitats = [("feathered_waterweed", "bottom_to_surface", .18, 310, .28, depth)]
        if tag != "river":
            habitats.append(("water_lily", "surface", .14 if tag == "lake" else .66, 240, .18, 1.45))
        for model, anchor, density, cap, minimum, maximum in habitats:
            patch = {"Id": f"{LOCATION}/{tag}/{model}", "Location": LOCATION, "Body": f"{LOCATION}/{tag}",
                     "Model": f"temperate_plants/{model}", "Anchor": anchor, "Center": center, "Size": size,
                     "Density": density, "MaxInstances": cap, "MinDepth": minimum, "MaxDepth": maximum,
                     "SurfaceGap": .14, "Seed": 1802 + index * 12 + len(patches), "EndDistance": 120}
            if tag == "lake" and anchor == "bottom_to_surface":
                # Keep a local bed for the underwater stop and open water toward the opposite bank.
                patch.update(Center=(151, 59), Size=(27, 22), Density=.16, MaxInstances=105,
                             MinDepth=1.35, SurfaceGap=.68, ScaleMin=.49, ScaleMax=.83)
            patches.append(patch)
    catalog("definitions", "aquatic", "aquatic_plant_patch", patches)
    return bodies


def specimens(height):
    def sample(x, z):
        i, j = math.floor(x), math.floor(z)
        u, v = x - i, z - j
        i, j = i + MARGIN, j + MARGIN
        a, b, c, d = height[j, i], height[j, i + 1], height[j + 1, i], height[j + 1, i + 1]
        return float(a + (b - a) * u + (c - a) * v if u + v <= 1 else d + (c - d) * (1 - u) + (b - d) * (1 - v))

    objects = []
    def add(name, model, x, z, scale, rotation=0, offset=0):
        objects.append({"Id": name, "Object": model if "/" in model else "temperate_plants/" + model,
                        "Transform": {"Position": [x, round(sample(x, z) + offset, 4), z],
                                      "Scale": [scale] * 3, "RotationDegrees": [0, rotation, 0]}})

    anthills = [(54, 174, 1.23), (149, 177, 1.16)]
    stumps = [(61, 172, 1.37), (144, 174, 1.48)]
    anchors = [(x, z) for x, z, _ in anthills + stumps]
    profiles = json.loads((DEMO / "biomes/profiles.json").read_text(encoding="utf-8"))
    rng = np.random.default_rng(profiles["Seed"] + 412)
    for area in profiles["Areas"]:
        if "Specimens" not in area:
            continue
        cx, cz = area["Center"]
        rx, rz = area["Radius"]
        for kind, result in (("Anthills", anthills), ("MushroomStumps", stumps)):
            count = area["Specimens"][kind]
            for _ in range(count):
                for attempt in range(250):
                    radius = .89 * math.sqrt(rng.random())
                    angle = rng.uniform(0, math.tau)
                    x = round(cx + rx * radius * math.cos(angle), 3)
                    z = round(cz + rz * radius * math.sin(angle), 3)
                    if (sample(x, z) < .32 or abs(sample(x + 1, z) - sample(x, z)) > .48
                            or abs(sample(x, z + 1) - sample(x, z)) > .48):
                        continue
                    if any((px - x) ** 2 + (pz - z) ** 2 < 24 for px, pz in anchors):
                        continue
                    anchors.append((x, z))
                    result.append((x, z, round(rng.uniform(.74, 1.22), 3)))
                    break
                else:
                    raise ValueError(f"Could not place {kind} in {area['Id']}")

    for i, (x, z, scale) in enumerate(anthills):
        add(f"anthill_{i}", LOCATION + "/anthill", x, z, scale, i * 102)
    for i, (x, z, scale) in enumerate(stumps):
        add(f"mushroom_stump_{i}", "forest_stump", x, z, scale, i * 91)
        for j, (dx, dz, mushroom_scale) in enumerate([(-.16, .02, 1.35), (.12, -.13, 1.05), (.21, .11, .86)]):
            # Seat the clustered bases inside the broken cap.
            tx, tz = x + dx * scale, z + dz * scale
            add(f"stump_mushroom_{i}_{j}", "porcini", tx, tz, mushroom_scale, j * 84,
                sample(x, z) - sample(tx, tz) + (.67 - .07 * j) * scale)
    add("view_snag", "broken_oak", 154, 172, 1.52, 34)
    add("view_log", "fallen_deadwood", 153, 181, 1.43, 78)
    return objects


def main():
    root = DEMO / "terrain" / LOCATION
    surface(root)
    height = terrain(root)
    anthill(root)
    bodies = water()
    catalog("definitions", "location", "location3d",
            [{"Id": LOCATION, "Scene": "nature_biomes", "Entry": "entry",
              "Points": [{"Id": "entry", "Transform": {"Position": [111, 3, 127]}}],
              "Camera": {"Position": [117, 81, 205], "RotationDegrees": [-33, 0, 0]},
              "Sky": {"Preset": "sky3d/temperate"},
              "PlantWind": {"UseWeather": False, "Strength": .42, "DirectionDegrees": 26, "Speed": 1},
              "Water": bodies, "Objects": specimens(height)}])


if __name__ == "__main__":
    main()
