"""Toolbar icons of ui_base and the panel mods: what to generate in ComfyUI.

The icons share the look of the item icons (comfy/rpg_art): one object rendered on magenta, keyed out,
reduced to a 64 px pixel-art sprite with a dark outline and scaled x4 to 256 px.

Usage (from the project root): python3 comfy/ui_icons/icons.py [name ...] [--seeds=N] [--seed=S]
writes ComfyUI jobs jobs/pending/ui_<name>_<k>.json, k = 0..N-1 (one render per seed).
"""
import json
import sys

STYLE = "16-bit SNES JRPG pixel art, crisp pixels, limited palette, rich warm colors, bold readable silhouette"
# Same lesson as the village props: at cfg=1 the negative prompt is ignored, so the positive prompt keeps
# people and faces out and asks for one big object on magenta.
PROMPT = ("close-up of {d}, a single inanimate object that fills most of the image, fantasy game item icon, "
          "slight three-quarter view, " + STYLE + ", nothing else in the picture, no people, no animals, "
          "no faces, no text, isolated on a solid flat pure magenta background, no ground, no cast shadow")

# icon id (file name in ui/icons) -> (owning mod, subject)
ICONS = {
    "map":         ("ui_base", "an unrolled old parchment map with a dotted red path and a red X mark, curled edges"),
    "bag":         ("ui_base", "a plump brown leather drawstring pouch bag tied with a rope"),
    "cards":       ("ui_base", "a fan of three fantasy playing cards with dark red backs and golden ornaments"),
    "skills":      ("ui_base", "a glowing cyan blue magic crystal ball in a small golden claw stand"),
    "settings":    ("ui_base", "a big brass cogwheel gear with a smaller steel gear"),
    "journal":     ("ui_base", "a closed thick brown leather-bound journal with a golden corner clasp and a red ribbon bookmark"),
    "tools":       ("ui_base", "a crossed iron hammer and wrench with wooden handles"),
    "save":        ("ui_base", "an hourglass with a golden frame and glowing blue sand"),
    "lamp":        ("ui_base", "an old brass oil lantern with a warm glowing orange flame inside the glass"),
    "recruits":    ("recruiting", "a rolled parchment contract scroll with a red wax seal and a ribbon"),
    "studio":      ("scenario_studio", "a white feather quill pen standing in a round black ink bottle"),
    "mod_manager": ("mod_manager", "a small open wooden crate with a big green jigsaw puzzle piece sticking out"),
}

SIZE = 1024


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    seeds = next((int(a[8:]) for a in sys.argv[1:] if a.startswith("--seeds=")), 2)
    seed0 = next((int(a[7:]) for a in sys.argv[1:] if a.startswith("--seed=")), 500)
    for name in args or ICONS:
        for k in range(seeds):
            job = {
                "workflow": "comfy/workflows/qwen_txt2img_api.json",
                "patch": {
                    "159.inputs.prompt": PROMPT.format(d=ICONS[name][1]),
                    "16.inputs.seed": seed0 + 10 * sorted(ICONS).index(name) + k,
                    "16.inputs.steps": 20,
                    "184.inputs.strength_model": 0.0,
                    "193.inputs.width": SIZE,
                    "193.inputs.height": SIZE,
                },
                "out": "gen/comfy/ui_icons",
            }
            with open(f"jobs/pending/ui_{name}_{k}.json", "w", newline="\n") as f:
                f.write(json.dumps(job, indent=2) + "\n")
            print("queued", name, k)


if __name__ == "__main__":
    main()
