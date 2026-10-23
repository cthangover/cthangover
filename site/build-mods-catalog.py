"""Build site/data/mods.json — real catalog data for the home page.

Sources (read-only, live next to the site folder):
    ../mods/*/manifest.json          - mod catalog
    ../Core/**/*.cs                  - kernel size stats
    ../mods/dialog_base/src/Scenarios/Impls/*.cs - DSL command strategies (count)
    ../mods/**/ *.scenario           - a real DSL sample, its literals shown in English
                                       (locale/scenarios_en.properties of the same mod)

Output:
    data/mods.json
    {
      "generated": "YYYY-MM-DD",
      "stats": {"mods": N, "dslCommands": N,
                "coreFiles": N, "coreLoc": N,
                "modFiles": N, "modLoc": N},
      "battle": [{"id": "...", "version": "...", "depends": [...]}],
      "mods": [{"id", "name", "version", "desc", "cat": [...], "deps": [...], "test": bool}],
      "dslSample": {"file": "mods/<id>/scenarios/...", "text": "..."}
    }
"""

import json
import os
import re

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))  # repo root
MODS_DIR = os.path.join(ROOT, "mods")
CORE_DIR = os.path.join(ROOT, "Core")
OUT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "data")
OUT_FILE = os.path.join(OUT_DIR, "mods.json")

BATTLE_IDS = ["turn_battle_base", "card_battle", "ff_battle"]
TEST_RE = re.compile(r"^(gdscript_test|live2d_test|test_)")
SKIP_DIRS = {"obj", "bin", ".godot"}
DSL_MAX_LINES = 30
DSL_MAX_LINE_LEN = 140
# "literal" key=<key> — the Russian literal of a scenario line and its locale key
DSL_LITERAL_RE = re.compile(r'"((?:[^"\\]|\\.)*)"(\s+key=(\S+))')


def load_mods():
    mods = []
    for name in sorted(os.listdir(MODS_DIR)):
        manifest = os.path.join(MODS_DIR, name, "manifest.json")
        if not os.path.isfile(manifest):
            continue
        try:
            with open(manifest, encoding="utf-8-sig") as f:
                m = json.load(f)
        except (OSError, json.JSONDecodeError):
            continue
        cats = [c.strip() for c in (m.get("category") or "").split(",") if c.strip()]
        deps = [d.split(">")[0].split("<")[0].split("=")[0].strip()
                for d in (m.get("depends") or [])]
        deps = [d for d in deps if d]
        mods.append({
            "id": m.get("id", name),
            "name": m.get("name", name),
            "version": m.get("version", "?"),
            "desc": m.get("description", ""),
            "cat": cats,
            "deps": deps,
            "test": bool(TEST_RE.match(m.get("id", name))),
        })
    return mods


def cs_stats(base):
    files = 0
    lines = 0
    for root, dirs, names in os.walk(base):
        dirs[:] = [d for d in dirs if d not in SKIP_DIRS and not d.startswith(".")]
        for n in names:
            if n.endswith(".cs"):
                files += 1
                try:
                    with open(os.path.join(root, n), encoding="utf-8", errors="ignore") as f:
                        lines += sum(1 for _ in f)
                except OSError:
                    pass
    return files, lines


def dsl_commands():
    impls = os.path.join(MODS_DIR, "dialog_base", "src", "Scenarios", "Impls")
    if not os.path.isdir(impls):
        return 0
    return len([n for n in os.listdir(impls) if n.endswith("CommandStrategy.cs")])


def read_properties(path):
    """key = value lines of a .properties file (# comments, no continuations)."""
    out = {}
    if not os.path.isfile(path):
        return out
    with open(path, encoding="utf-8-sig", errors="ignore") as f:
        for line in f:
            line = line.strip()
            if not line or line.startswith("#") or "=" not in line:
                continue
            key, value = line.split("=", 1)
            out[key.strip()] = value.strip()
    return out


def english_line(line, english):
    """Swaps the literal of a `"..." key=K` line for the English text of K."""
    def repl(m):
        text = english.get(m.group(3))
        if text is None:
            return m.group(0)
        return '"' + text.replace('"', '\\"') + '"' + m.group(2)
    line = DSL_LITERAL_RE.sub(repl, line)
    if len(line) > DSL_MAX_LINE_LEN:
        line = line[:DSL_MAX_LINE_LEN - 1] + "\u2026"
    return line


def dsl_sample():
    """First short .scenario of a content mod (murakami preferred). Scenario literals are
    Russian; the site is English, so each literal is replaced by the mod's English text."""
    candidates = []
    for root, dirs, names in os.walk(MODS_DIR):
        dirs[:] = [d for d in dirs if d not in SKIP_DIRS]
        for n in sorted(names):
            if n.endswith(".scenario"):
                candidates.append(os.path.join(root, n))
    if not candidates:
        return None
    candidates.sort(key=lambda p: (0 if "murakami" in p else 1, p))
    path = candidates[0]
    mod_dir = os.path.join(MODS_DIR, os.path.relpath(path, MODS_DIR).split(os.sep)[0])
    english = read_properties(os.path.join(mod_dir, "locale", "scenarios_en.properties"))
    with open(path, encoding="utf-8", errors="ignore") as f:
        lines = []
        for line in f:
            lines.append(english_line(line.rstrip("\n"), english))
            if len(lines) >= DSL_MAX_LINES:
                break
    rel = os.path.relpath(path, ROOT).replace("\\", "/")
    return {"file": rel, "text": "\n".join(lines)}


def main():
    mods = load_mods()
    core_files, core_loc = cs_stats(CORE_DIR)
    mod_files, mod_loc = cs_stats(MODS_DIR)

    battle = []
    by_id = {m["id"]: m for m in mods}
    for bid in BATTLE_IDS:
        m = by_id.get(bid)
        if m:
            battle.append({"id": bid, "version": m["version"], "deps": m["deps"]})

    sample = dsl_sample()

    out = {
        "generated": __import__("datetime").date.today().isoformat(),
        "stats": {
            "mods": len(mods),
            "dslCommands": dsl_commands(),
            "coreFiles": core_files,
            "coreLoc": core_loc,
            "modFiles": mod_files,
            "modLoc": mod_loc,
        },
        "battle": battle,
        "mods": mods,
        "dslSample": sample,
    }

    os.makedirs(OUT_DIR, exist_ok=True)
    with open(OUT_FILE, "w", encoding="utf-8") as f:
        json.dump(out, f, ensure_ascii=False, indent=1)
    print(f"OK  {os.path.relpath(OUT_FILE, ROOT)}  "
          f"({len(mods)} mods, dsl={out['stats']['dslCommands']}, "
          f"core={core_loc} loc, mods={mod_loc} loc)")


if __name__ == "__main__":
    main()
