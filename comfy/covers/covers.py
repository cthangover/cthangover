"""Mod covers (mod.jpg, 960x512) generated in ComfyUI.

Usage (project root):
    python3 comfy/covers/covers.py jobs [mod ...] [--seed=N]  # queue jobs/pending/cover_<mod>.json
    python  comfy/covers/covers.py build [--install]  # HOST (Pillow): gen/covers/<mod>.jpg, --install -> mods/<mod>/mod.jpg

Every cover carries the mod's title. The cthangover game's own mods are not covered here. Mods whose author made a cover
(KEEP) only get a candidate in gen/covers; --install leaves their files alone.
"""
import glob
import json
import os
import sys

# A clean title on purpose: told "no text", Qwen still scatters pseudo-lettering over the props
# (cards, signs), while a requested title comes out crisp. The workflow runs at cfg=1, so all
# wishes go into the positive prompt.
PROMPT = ("wide banner cover art for a game mod, {scene}, with a large clean bold title text \"{title}\" "
          "in the center, cinematic lighting, rich colors, high quality anime illustration, "
          "no other text anywhere")

COVERS = {
    "audio_base": ("Audio", "glowing sound waves, speakers, music notes and headphones in a dark studio"),
    "characters_base": ("Characters", "a lineup of diverse anime fantasy heroes standing side by side"),
    "cooking": ("Cooking", "a cozy rustic kitchen workbench with a pot over the fire, meat and herbs"),
    "dialog_base": ("Dialogs", "two anime characters talking to each other, empty speech bubbles with only an ellipsis"),
    "editor_tools": ("Editor Tools", "a game developer desk with several monitors showing node graphs, maps and scripts"),
    "effects_base": ("Effects", "swirling magical particles, sparkles, smoke and bursts of light"),
    "ff_battle": ("FF Battle", "a classic JRPG side-view battle, a party of heroes facing monsters, a blue command menu window"),
    "interactive_base": ("Interactives", "a cozy room with glowing highlighted clickable objects: a door, a chest, a lamp"),
    "items_base": ("Items", "an open adventurer's backpack with potions, coins, herbs and a dagger laid out on a table"),
    "locale_atlas": ("Locale Atlas", "a globe surrounded by speech bubbles with letters of many alphabets"),
    "map_base": ("World Map", "an old parchment fantasy world map with zones, pins and a compass"),
    "menu_base": ("Main Menu", "an elegant game title screen with a column of blank glowing buttons without labels over a night city skyline"),
    "quests_base": ("Quests", "an open quest journal on a wooden table with a quill, checkmarks and a red wax seal"),
    "recruiting": ("Recruiting", "a defeated cute monster bowing and joining a young hero's party"),
    "resource_atlas": ("Resource Atlas", "a glowing network graph connecting boxes of textures, sounds and scripts"),
    "rpg_demo_game": ("RPG Demo", "a cozy top-down pixel art village with a hero, an elder and a merchant, a forest and a wolf near a cave"),
    "rpg_editor": ("Map Editor", "a pixel art tile map being painted on a grid with a big brush, tile palette on the side"),
    "scenario_studio": ("Scenario Studio", "a director's control room with script pages, play and pause buttons and a timeline"),
    "skills_base": ("Skills", "a glowing skill tree with icons of a sword, a flame and a shield"),
    "test_defaults_and_patches": ("Test Patches", "laboratory test tubes, gears and a small debugging robot"),
    "time_base": ("Time & Light", "a town street split from sunny day to night with glowing lamps, a big clock in the sky"),
    "town_pack": ("Town Pack", "a cozy anime town entrance in the evening with a bar sign and warm windows"),
    "trading": ("Trading", "a medieval merchant stall with potions and gold coins on the counter and brass scales"),
    "turn_battle_base": ("Turn Battle", "a battlefield with heroes and monsters facing each other, glowing turn order markers above them"),
    "ui_base": ("UI Kit", "floating game interface panels, buttons, sliders and icons in a dark and gold theme"),
    "validator": ("Validator", "a big classic magnifying glass with a round clear glass lens, a brass rim and a wooden handle, lying over a paper checklist with green checkmarks and a few red crosses"),
    "village_pack": ("Village Pack", "pixel art game assets laid out: little houses, trees, fences, chests and villagers"),
    # the author's own covers: candidates only
    "card_battle": ("Card Battle", "magic trading cards fanned out on a glowing golden table"),
    "rpg": ("RPG World", "a top-down pixel art tile world with a hero walking on a grid"),
    "shell2d_base": ("2D Engine", "a cute chibi anime girl line drawing on a light grey background"),
    "my_novel_game": ("My Novel", "a visual novel scene: a small town street, a bar and a home at dusk"),
    "live2d_base": ("Live2D", "an anime girl portrait with a visible mesh of deformation points, animated"),
    "mod_manager": ("Mod Manager", "a grid of colorful mod cards being installed with progress bars"),
}
KEEP = {"card_battle", "rpg", "shell2d_base", "my_novel_game", "live2d_base", "mod_manager"}


def jobs(names, seed_base):
    for i, mod in enumerate(sorted(COVERS)):
        if names and mod not in names:
            continue
        title, scene = COVERS[mod]
        job = {
            "workflow": "comfy/workflows/qwen_txt2img_api.json",
            "patch": {
                "159.inputs.prompt": PROMPT.format(title=title, scene=scene),
                "16.inputs.seed": seed_base + i,
                "16.inputs.steps": 20,
                "193.inputs.width": 1920,
                "193.inputs.height": 1024,
            },
            "out": "gen/comfy/covers",
        }
        with open(f"jobs/pending/cover_{mod}.json", "w", newline="\n") as f:
            f.write(json.dumps(job, indent=2) + "\n")
        print("queued", mod)


def build(install):
    from PIL import Image
    os.makedirs("gen/covers", exist_ok=True)
    mods = sorted(COVERS)
    sheet = Image.new("RGB", (6 * 320, (len(mods) + 5) // 6 * 170), (20, 20, 20))
    for index, mod in enumerate(mods):
        files = sorted(glob.glob(f"gen/comfy/covers/cover_{mod}__*.png"), key=os.path.getmtime)
        if not files:
            print("missing", mod)
            continue
        img = Image.open(files[-1]).convert("RGB").resize((960, 512), Image.LANCZOS)
        sheet.paste(img.resize((320, 170), Image.LANCZOS), (index % 6 * 320, index // 6 * 170))
        out = f"gen/covers/{mod}.jpg"
        img.save(out, quality=88)
        if install and mod not in KEEP:
            img.save(f"mods/{mod}/mod.jpg", quality=88)
            png = f"mods/{mod}/mod.png"
            if os.path.exists(png):  # mod.png would win over mod.jpg in ModPreviewLoader
                os.replace(png, f"gen/covers/{mod}_old_mod.png")
            print("installed", mod)
    sheet.save("gen/covers/_sheet.jpg", quality=85)


if __name__ == "__main__":
    if sys.argv[1] == "jobs":
        # --seed=N re-rolls a cover whose title came out misspelled
        seed = next((int(a[7:]) for a in sys.argv[2:] if a.startswith("--seed=")), 900)
        jobs({a for a in sys.argv[2:] if not a.startswith("--")}, seed)
    else:
        build("--install" in sys.argv)
