"""Writes ComfyUI jobs (jobs/pending/va_<name>.json) for the assets named on the command line (default: all).

Usage (from the project root): python3 comfy/rpg_art/make_jobs.py [name ...] [--seed=N]
"""
import json
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
from assets import ASSETS, NEG, OBJECT, SIZES, TALL, TEXTURE, TURN

PROMPTS = {"texture": TEXTURE, "object": OBJECT, "turnaround": TURN}

args = [a for a in sys.argv[1:] if not a.startswith("--")]
seed = next((int(a[7:]) for a in sys.argv[1:] if a.startswith("--seed=")), 100)
for name in args or ASSETS:
    spec = ASSETS[name]
    kind = spec[0]
    w, h = TALL if kind == "object" and spec[2][1] > spec[2][0] else SIZES[kind]
    job = {
        "workflow": "comfy/workflows/qwen_txt2img_api.json",
        "patch": {
            "159.inputs.prompt": PROMPTS[kind].format(d=spec[1]),
            "159.inputs.negative_prompt": NEG,
            "16.inputs.seed": seed + sorted(ASSETS).index(name),
            "16.inputs.steps": 20,
            "184.inputs.strength_model": 0.0,
            "193.inputs.width": w,
            "193.inputs.height": h,
        },
        "out": "gen/comfy/village",
    }
    with open(f"jobs/pending/va_{name}.json", "w", newline="\n") as f:
        f.write(json.dumps(job, indent=2) + "\n")
    print("queued", name, w, h)
