"""Builds the art of the rpg_plus sample (classic_rpg_pack, rpg_plus_game, stats_base) from ComfyUI renders.

Runs on the HOST (Pillow + numpy, e.g. the ComfyUI venv), cwd = project root:
    python comfy/rpg_plus/build_rpg_plus.py [--install]
Renders come from gen/comfy/rpg_plus (jobs va_rp_<name>.json, written by the session's make_art_jobs.js with the
village prompts). The pixel-art steps are build_village.py's own (magenta key, alpha-weighted downscale, palette,
outline, charset frames and walk cycle), only the source folder differs. Output goes to gen/rpg_plus_build/;
--install copies it into the mods.
"""
import os
import shutil
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "rpg_art"))
import build_village as bv  # noqa: E402

bv.SRC = "gen/comfy/rpg_plus"
OUT = "gen/rpg_plus_build"

# render name (without va_) -> installed path under mods/
ICONS = {
    "rp_wood_sword": "classic_rpg_pack/items/classic/wood_sword.png",
    "rp_iron_sword": "classic_rpg_pack/items/classic/iron_sword.png",
    "rp_oak_staff": "classic_rpg_pack/items/classic/oak_staff.png",
    "rp_dagger": "classic_rpg_pack/items/classic/dagger.png",
    "rp_leather_armor": "classic_rpg_pack/items/classic/leather_armor.png",
    "rp_robe": "classic_rpg_pack/items/classic/robe.png",
    "rp_clover_charm": "classic_rpg_pack/items/classic/clover_charm.png",
    "rp_silver_ring": "classic_rpg_pack/items/classic/silver_ring.png",
    "rp_strength_tonic": "classic_rpg_pack/items/classic/strength_tonic.png",
    "rp_sheet": "stats_base/ui/icons/sheet.png",
}
CARDS = {
    "rp_card_power_strike": "classic_rpg_pack/characters/actions/classic_power_strike.png",
    "rp_card_fireball": "classic_rpg_pack/characters/actions/classic_fireball.png",
    "rp_card_heal": "classic_rpg_pack/characters/actions/classic_heal.png",
    "rp_card_guard_break": "classic_rpg_pack/characters/actions/classic_guard_break.png",
}
CHARSETS = {
    "rp_c_trainer": "rpg_plus_game/charsets/rpg_plus/trainer.png",
    "rp_c_seer": "rpg_plus_game/charsets/rpg_plus/seer.png",
}
BACKGROUNDS = {
    "rp_bg_crossroads": "rpg_plus_game/backgrounds/rpg_plus/crossroads.png",
    "rp_bg_training_yard": "rpg_plus_game/backgrounds/rpg_plus/training_yard.png",
}
CARD_SIZE = (1024, 1920)
BACKGROUND_SIZE = (1920, 1024)


def card(name):
    img = Image.fromarray(bv.latest(name).astype(np.uint8), "RGB")
    return np.asarray(img.resize(CARD_SIZE, Image.LANCZOS).convert("RGBA"))


def background(name):
    img = Image.fromarray(bv.latest(name).astype(np.uint8), "RGB")
    return np.asarray(img.resize(BACKGROUND_SIZE, Image.LANCZOS).convert("RGBA"))


def main():
    built = {}
    for name, path in ICONS.items():
        built[path] = bv.icon(name)
    for name, path in CARDS.items():
        built[path] = card(name)
    for name, path in CHARSETS.items():
        built[path] = bv.build_charset(name)
    for name, path in BACKGROUNDS.items():
        built[path] = background(name)
    for path, rgba in built.items():
        preview = 4 if path in CHARSETS.values() else 0
        bv.save(rgba, f"{OUT}/{path}", preview=preview)
        print("built", path)
    if "--install" in sys.argv:
        for path in built:
            os.makedirs(os.path.dirname(f"mods/{path}"), exist_ok=True)
            shutil.copyfile(f"{OUT}/{path}", f"mods/{path}")
            print("installed", path)
    print("ok")


if __name__ == "__main__":
    main()
