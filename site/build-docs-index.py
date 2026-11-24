"""Build docs-index.json from all .md files in docs/ and update the HTML footer count.

Output:
    {
      "_tree": [ ... ],    // nested tree mirroring filesystem
      "_ru": { ... },      // optional Russian titles and Markdown by canonical slug
      "slug": "...",       // flat slug -> content map
      ...
    }
"""

import json
import os
import sys

from build_html import update_html

SITE_DIR = os.path.dirname(os.path.abspath(__file__))
DOCS_DIR = os.path.join(SITE_DIR, "docs")
OUT_FILE = os.path.join(DOCS_DIR, "docs-index.json")


def read_md(path):
    """Return (title, content) from a .md file."""
    with open(path, encoding="utf-8") as f:
        content = f.read()
    title = os.path.splitext(os.path.basename(path))[0]
    for line in content.splitlines():
        stripped = line.lstrip()
        if stripped.startswith("# ") and not stripped.startswith("## "):
            title = stripped[2:].strip()
            break
    return title, content


def walk_dir(dir_path, rel_prefix, translations):
    """Walk directory recursively, return (child_nodes, flat_docs)."""
    nodes = []
    docs = {}
    try:
        entries = sorted(os.listdir(dir_path))
    except OSError:
        return nodes, docs

    for name in entries:
        if name.startswith("."):
            continue
        if name == "docs-index.json":
            continue
        full = os.path.join(dir_path, name)

        if os.path.isdir(full):
            children, sub_docs = walk_dir(full, f"{rel_prefix}{name}/", translations)
            docs.update(sub_docs)

            # Check for index.md in this directory
            index_path = os.path.join(full, "index.md")
            if os.path.isfile(index_path):
                title, content = read_md(index_path)
                slug = f"{rel_prefix}{name}/index"
                docs[slug] = content
                children = [c for c in children if c.get("slug") != slug]
                nodes.append({"title": title, "slug": slug, "children": children})
            elif children:
                nodes.append({"title": name, "slug": "", "children": children})

        elif name.endswith(".ru.md"):
            slug = f"{rel_prefix}{name[:-len('.ru.md')]}"
            title, content = read_md(full)
            translations[slug] = {"title": title, "markdown": content}

        elif name.endswith(".md"):
            title, content = read_md(full)
            slug = f"{rel_prefix}{name[:-3]}"
            docs[slug] = content
            nodes.append({"title": title, "slug": slug})

    return nodes, docs


def main():
    if not os.path.isdir(DOCS_DIR):
        print(f"Error: {DOCS_DIR} not found", file=sys.stderr)
        sys.exit(1)

    translations = {}
    tree, docs = walk_dir(DOCS_DIR, "", translations)
    missing = translations.keys() - docs.keys()
    if missing:
        sys.exit("Russian translations without an English page: " + ", ".join(sorted(missing)))
    output = {"_tree": tree, "_ru": translations, **docs}

    with open(OUT_FILE, "w", encoding="utf-8") as f:
        json.dump(output, f, ensure_ascii=False)

    file_count = len(docs) + len(translations)
    update_html(os.path.join(SITE_DIR, "docs.html"), {"builtFileCount": file_count})
    total_bytes = sum(len(v) for v in docs.values()) + sum(len(v["markdown"]) for v in translations.values())
    print(f"OK  {OUT_FILE}  ({file_count} files, {total_bytes} bytes, tree depth built)")


if __name__ == "__main__":
    main()
