"""Build docs-index.json from all .md files in docs/.

Output:
    {
      "_tree": [ ... ],    // nested tree mirroring filesystem
      "slug": "...",       // flat slug -> content map
      ...
    }
"""

import json
import os
import sys

DOCS_DIR = "docs"
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


def walk_dir(dir_path, rel_prefix):
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
            children, sub_docs = walk_dir(full, f"{rel_prefix}{name}/")
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

    tree, docs = walk_dir(DOCS_DIR, "")
    output = {"_tree": tree, **docs}

    with open(OUT_FILE, "w", encoding="utf-8") as f:
        json.dump(output, f, ensure_ascii=False)

    file_count = len(docs)
    total_bytes = sum(len(v) for v in docs.values())
    print(f"OK  {OUT_FILE}  ({file_count} files, {total_bytes} bytes, tree depth built)")


if __name__ == "__main__":
    main()
