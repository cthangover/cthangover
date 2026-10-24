"""Installs the DA3 depth renders of the rpg_plus novel backgrounds (HOST, Pillow; cwd = project root).

The lighting shader of the view box lights a background by its <name>_depth.png (near = white), like every
background of the project; renders come from the jobs rp_d2_<name>.json in gen/comfy/rpg_plus_depth.
"""
import glob
import os

from PIL import Image

for name in ("crossroads", "training_yard"):
    files = sorted(glob.glob(f"gen/comfy/rpg_plus_depth/rp_d2_{name}__*.png"), key=os.path.getmtime)
    if not files:
        raise SystemExit(f"no depth render for {name}")
    target = f"mods/rpg_plus_game/backgrounds/rpg_plus/{name}_depth.png"
    Image.open(files[-1]).convert("RGB").resize((1920, 1024), Image.LANCZOS).save(target)
    print("installed", target)
