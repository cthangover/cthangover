"""Build quickstart-index.json from all .md files in quickstart/.

Output:
    {
      "ru/index":   "...",
      "ru/player":  "...",
      "en/index":   "...",
      ...
    }
"""

import json
import os

QUICKSTART_DIR = "quickstart"
OUT_FILE = os.path.join(QUICKSTART_DIR, "quickstart-index.json")


def main():
    if not os.path.isdir(QUICKSTART_DIR):
        print(f"Error: {QUICKSTART_DIR} not found")
        return

    result = {}
    file_count = 0

    for lang in sorted(os.listdir(QUICKSTART_DIR)):
        lang_dir = os.path.join(QUICKSTART_DIR, lang)
        if not os.path.isdir(lang_dir):
            continue
        for fname in sorted(os.listdir(lang_dir)):
            if not fname.endswith(".md"):
                continue
            fpath = os.path.join(lang_dir, fname)
            with open(fpath, encoding="utf-8") as f:
                content = f.read()
            key = f"{lang}/{fname[:-3]}"
            result[key] = content
            file_count += 1

    with open(OUT_FILE, "w", encoding="utf-8") as f:
        json.dump(result, f, ensure_ascii=False)

    total_bytes = sum(len(v) for v in result.values())
    print(f"OK  {OUT_FILE}  ({file_count} pages, {total_bytes} bytes)")


if __name__ == "__main__":
    main()
