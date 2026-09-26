"""Build ai.html - all documentation on a single plain HTML page.

The page is a no-JS, no-CSS, no-styling dump of every known document,
meant to be used by AI agents (and humans) as one entry point:

    https://cthangover.ru/ai.html

Sources:
    docs/**/*.md            - the main documentation
    quickstart/en/*.md      - the English quick start guide
    ../mods/*/manifest.json - existing mods (manifest + file list)

Output:
    ai.html
"""

import html
import html.parser
import json
import os
import re
import sys

DOCS_DIR = "docs"
QUICKSTART_DIR = "quickstart"
QUICKSTART_LANGS = ("en",)
MODS_DIR = os.path.join("..", "mods")
OUT_FILE = "ai.html"
SITE_URL = "https://cthangover.ru"

FENCE_RE = re.compile(r"^(`{3,}|~{3,})\s*([\w+-]*)\s*$")
HEADING_RE = re.compile(r"^(#{1,6})\s+(.*?)(?:\s+#+)?\s*$")
LIST_RE = re.compile(r"^(\s*)([-*+]|\d+[.)])\s+(.*)$")
HR_RE = re.compile(r"^\s*([-*_]\s*){3,}$")
TABLE_SEP_RE = re.compile(r"^\s*\|?\s*:?-+:?\s*(\|\s*:?-+:?\s*)*\|?\s*$")
RAW_HTML_RE = re.compile(r"^\s*<([a-zA-Z][\w-]*)[\s/>]")

CODE_SPAN_RE = re.compile(r"(`+)(.+?)\1", re.DOTALL)
IMAGE_RE = re.compile(r"!\[([^\]]*)\]\(([^)\s]*)(?:\s+\"[^\"]*\")?\)")
LINK_RE = re.compile(r"\[([^\]]+)\]\(([^)\s]*)(?:\s+\"[^\"]*\")?\)")
AUTOLINK_RE = re.compile(r"&lt;((?:https?://|mailto:)[^<>\s]+)&gt;")
STRIKE_RE = re.compile(r"~~(?=[^\s~])([^~\n]+?)(?<=[^\s~])~~")
BOLD_RE = re.compile(r"\*\*(?=[^\s*])([^*\n]+?)(?<=[^\s*])\*\*")
ITALIC_RE = re.compile(r"(?<![\w*/])\*(?=[^\s*])([^*\n]+?)(?<=[^\s*])\*(?![\w*])")
PLACEHOLDER_RE = re.compile("\x00(\\d+)\x00")


# --------------------------------------------------------------------------
# inline markdown
# --------------------------------------------------------------------------

def inline(text):
    """Render inline markdown (code spans, links, emphasis) to HTML."""
    stash = []

    def keep(value):
        stash.append(value)
        return f"\x00{len(stash) - 1}\x00"

    text = CODE_SPAN_RE.sub(
        lambda m: keep(f"<code>{html.escape(m.group(2), quote=False)}</code>"), text
    )
    text = html.escape(text, quote=False)

    def image(m):
        alt, url = m.group(1), m.group(2)
        label = f"[image: {alt}]" if alt else "[image]"
        return keep(f'<a href="{url}">{label}</a>')

    text = IMAGE_RE.sub(image, text)

    def link(m):
        return keep(f'<a href="{m.group(2)}">{m.group(1)}</a>')

    text = LINK_RE.sub(link, text)
    text = AUTOLINK_RE.sub(lambda m: keep(f'<a href="{m.group(1)}">{m.group(1)}</a>'), text)
    text = STRIKE_RE.sub(r"<del>\1</del>", text)
    text = BOLD_RE.sub(lambda m: f"<strong>{m.group(1)}</strong>", text)
    text = ITALIC_RE.sub(lambda m: f"<em>{m.group(1)}</em>", text)

    while True:
        text, count = PLACEHOLDER_RE.subn(lambda m: stash[int(m.group(1))], text)
        if count == 0:
            return text


# --------------------------------------------------------------------------
# raw HTML blocks (quickstart pages embed layout markup)
# --------------------------------------------------------------------------

class _HtmlTextExtractor(html.parser.HTMLParser):
    """Turn a raw HTML fragment into plain text; links keep their URLs."""

    _BLOCK = {
        "div", "p", "h1", "h2", "h3", "h4", "h5", "h6", "ul", "ol", "li",
        "table", "tr", "td", "th", "pre", "blockquote", "section", "br",
        "details", "summary", "hr",
    }

    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.parts = []
        self._hrefs = []

    def _image(self, attrs):
        self.parts.append(f"[image: {dict(attrs).get('alt', '')}]")

    def handle_starttag(self, tag, attrs):
        if tag == "a":
            self._hrefs.append(dict(attrs).get("href", ""))
        elif tag == "img":
            self._image(attrs)
        elif tag in self._BLOCK:
            self.parts.append("\n")

    def handle_startendtag(self, tag, attrs):
        if tag == "img":
            self._image(attrs)
        elif tag in self._BLOCK:
            self.parts.append("\n")

    def handle_endtag(self, tag):
        if tag == "a":
            href = self._hrefs.pop() if self._hrefs else ""
            if href:
                self.parts.append(f" ({href})")
        elif tag in self._BLOCK:
            self.parts.append("\n")

    def handle_data(self, data):
        self.parts.append(data)


def raw_html_to_text(fragment):
    """Convert a raw HTML block to a plain paragraph without tags."""
    parser = _HtmlTextExtractor()
    parser.feed(fragment)
    parser.close()
    text = html.unescape("".join(parser.parts))
    lines = [line.strip() for line in text.splitlines()]
    lines = [line for line in lines if line]
    body = "<br>\n".join(html.escape(line, quote=False) for line in lines)
    return f"<p>{body}</p>"


# --------------------------------------------------------------------------
# block markdown
# --------------------------------------------------------------------------

def split_row(line):
    row = line.strip()
    if row.startswith("|"):
        row = row[1:]
    if row.endswith("|"):
        row = row[:-1]
    return [cell.strip() for cell in row.split("|")]


def is_table_start(lines, i):
    return (
        i + 1 < len(lines)
        and "|" in lines[i]
        and bool(TABLE_SEP_RE.match(lines[i + 1]))
    )


def render_table(lines, i):
    header = split_row(lines[i])
    i += 2
    rows = []
    while i < len(lines) and lines[i].strip() and "|" in lines[i]:
        rows.append(split_row(lines[i]))
        i += 1
    head = "".join(f"<th>{inline(cell)}</th>" for cell in header)
    body = "".join(
        "<tr>" + "".join(f"<td>{inline(cell)}</td>" for cell in row) + "</tr>"
        for row in rows
    )
    return f"<table><thead><tr>{head}</tr></thead><tbody>{body}</tbody></table>", i


def _indent_len(line):
    expanded = line.expandtabs(4)
    return len(expanded) - len(expanded.lstrip(" "))


def parse_list_items(lines):
    """Split raw list lines into items with their nested sub-blocks."""
    items = []
    i = 0
    while i < len(lines):
        match = LIST_RE.match(lines[i])
        if not match:
            i += 1
            continue
        indent = _indent_len(lines[i])
        item = {"ordered": match.group(2)[0].isdigit(), "text": match.group(3), "sub": []}
        i += 1
        while i < len(lines):
            line = lines[i]
            if not line.strip():
                j = i + 1
                while j < len(lines) and not lines[j].strip():
                    j += 1
                if j < len(lines) and _indent_len(lines[j]) > indent:
                    item["sub"].append("")
                    i += 1
                    continue
                break
            if _indent_len(line) > indent:
                item["sub"].append(line)
                i += 1
            else:
                break
        items.append(item)
    return items


def render_list_html(items):
    parts = []
    buffer = []
    ordered = None

    def flush():
        if not buffer:
            return
        tag = "ol" if ordered else "ul"
        parts.append(f"<{tag}>" + "".join(buffer) + f"</{tag}>")
        buffer.clear()

    for item in items:
        if ordered is not None and item["ordered"] != ordered:
            flush()
        ordered = item["ordered"]
        inner = inline(item["text"])
        if item["sub"]:
            inner += render_blocks(item["sub"])
        buffer.append(f"<li>{inner}</li>")
    flush()
    return "".join(parts)


def render_list(lines, i):
    raw = []
    while i < len(lines):
        line = lines[i]
        if LIST_RE.match(line):
            raw.append(line)
            i += 1
        elif line.strip() and _indent_len(line) > 0 and raw:
            raw.append(line)
            i += 1
        elif not line.strip():
            j = i + 1
            while j < len(lines) and not lines[j].strip():
                j += 1
            if j < len(lines) and (LIST_RE.match(lines[j]) or _indent_len(lines[j]) > 0):
                raw.append("")
                i += 1
            else:
                break
        else:
            break
    return render_list_html(parse_list_items(raw)), i


def block_start(lines, i):
    line = lines[i]
    stripped = line.strip()
    return (
        bool(FENCE_RE.match(stripped))
        or bool(HEADING_RE.match(stripped))
        or bool(HR_RE.match(stripped))
        or stripped.startswith(">")
        or bool(LIST_RE.match(line))
        or bool(RAW_HTML_RE.match(line))
        or is_table_start(lines, i)
    )


def render_blocks(lines):
    out = []
    i = 0
    while i < len(lines):
        line = lines[i]
        stripped = line.strip()
        if not stripped:
            i += 1
            continue

        match = FENCE_RE.match(stripped)
        if match:
            marker = match.group(1)[0]
            lang = match.group(2)
            i += 1
            code = []
            while i < len(lines):
                closing = lines[i].strip()
                if len(closing) >= 3 and set(closing) == {marker}:
                    i += 1
                    break
                code.append(lines[i])
                i += 1
            attr = f' class="language-{html.escape(lang, quote=True)}"' if lang else ""
            out.append(f"<pre><code{attr}>{html.escape(chr(10).join(code), quote=False)}</code></pre>")
            continue

        match = HEADING_RE.match(stripped)
        if match:
            level = min(len(match.group(1)) + 1, 6)
            out.append(f"<h{level}>{inline(match.group(2))}</h{level}>")
            i += 1
            continue

        if is_table_start(lines, i):
            table, i = render_table(lines, i)
            out.append(table)
            continue

        if HR_RE.match(stripped):
            out.append("<hr>")
            i += 1
            continue

        if stripped.startswith(">"):
            quote = []
            while i < len(lines) and lines[i].strip().startswith(">"):
                inner = lines[i].strip()[1:]
                quote.append(inner[1:] if inner.startswith(" ") else inner)
                i += 1
            out.append(f"<blockquote>{render_blocks(quote)}</blockquote>")
            continue

        if LIST_RE.match(line):
            list_html, i = render_list(lines, i)
            out.append(list_html)
            continue

        raw = RAW_HTML_RE.match(line)
        if raw:
            tag = raw.group(1).lower()
            fragment = [line]
            i += 1
            closed = re.search(r"</" + re.escape(tag) + r"\s*>\s*$", line, re.IGNORECASE)
            if not closed and not line.rstrip().endswith("/>"):
                close_re = re.compile(r"</" + re.escape(tag) + r"\s*>", re.IGNORECASE)
                while i < len(lines):
                    fragment.append(lines[i])
                    i += 1
                    if close_re.search(fragment[-1]):
                        break
            out.append(raw_html_to_text("\n".join(fragment)))
            continue

        para = []
        while i < len(lines) and lines[i].strip() and not (para and block_start(lines, i)):
            para.append(lines[i].strip())
            i += 1
        out.append("<p>" + inline(" ".join(para)) + "</p>")

    return "\n".join(out)


def render_markdown(text):
    return render_blocks(text.splitlines())


# --------------------------------------------------------------------------
# page assembly
# --------------------------------------------------------------------------

def read_md(path):
    """Return (title, content) from a .md file.

    The first level-1 heading becomes the section title and is removed
    from the content, because the page already prints it as the heading.
    """
    with open(path, encoding="utf-8") as f:
        lines = f.read().splitlines()
    title = os.path.splitext(os.path.basename(path))[0]
    in_fence = False
    for i, line in enumerate(lines):
        stripped = line.lstrip()
        if FENCE_RE.match(line.strip()):
            in_fence = not in_fence
            continue
        if in_fence:
            continue
        if stripped.startswith("# ") and not stripped.startswith("## "):
            title = stripped[2:].strip()
            del lines[i]
            break
    return title, "\n".join(lines)


def make_entry(group, key, path):
    title, content = read_md(path)
    return {"group": group, "key": key, "path": path, "title": title, "content": content}


def collect_entries():
    entries = []

    for root, dirs, files in os.walk(DOCS_DIR):
        dirs[:] = sorted(d for d in dirs if not d.startswith("."))
        for name in sorted(files):
            if not name.endswith(".md"):
                continue
            path = os.path.join(root, name)
            key = "docs/" + os.path.relpath(path, DOCS_DIR).replace("\\", "/")[:-3]
            entries.append(make_entry("Documentation", key, path))

    if os.path.isdir(QUICKSTART_DIR):
        for lang in QUICKSTART_LANGS:
            lang_dir = os.path.join(QUICKSTART_DIR, lang)
            if not os.path.isdir(lang_dir):
                continue
            for name in sorted(os.listdir(lang_dir)):
                if not name.endswith(".md"):
                    continue
                path = os.path.join(lang_dir, name)
                key = f"quickstart/{lang}/{name[:-3]}"
                entries.append(make_entry("Quick Start", key, path))

    return entries


def collect_mods():
    """Collect every mod directory from ../mods with its manifest and file list."""
    if not os.path.isdir(MODS_DIR):
        return []

    mods = []
    for name in sorted(os.listdir(MODS_DIR)):
        mod_dir = os.path.join(MODS_DIR, name)
        manifest_path = os.path.join(mod_dir, "manifest.json")
        if name.startswith(".") or not os.path.isfile(manifest_path):
            continue

        with open(manifest_path, encoding="utf-8") as f:
            manifest = f.read()

        title = name
        try:
            data = json.loads(manifest)
            if isinstance(data, dict) and data.get("name"):
                title = str(data["name"])
        except ValueError:
            pass

        files = []
        for root, dirs, names in os.walk(mod_dir):
            dirs[:] = sorted(d for d in dirs if not d.startswith("."))
            for file_name in sorted(names):
                if file_name.startswith("."):
                    continue
                path = os.path.join(root, file_name)
                files.append(os.path.relpath(path, mod_dir).replace("\\", "/"))

        mods.append({"name": name, "title": title, "manifest": manifest, "files": files})

    return mods


def anchor(key):
    return re.sub(r"[^0-9a-z]+", "-", key.lower()).strip("-")


def mod_anchor(name):
    return anchor("mods/" + name)


def build_page(entries, mods):
    lines = [
        "<!DOCTYPE html>",
        '<html lang="en">',
        "<head>",
        '<meta charset="utf-8">',
        "<title>cthangover - full documentation</title>",
        '<meta name="description" content="Complete cthangover documentation on a single page '
        'without JavaScript or CSS, for AI agents and offline reading.">',
        "</head>",
        "<body>",
        "<h1>cthangover - full documentation</h1>",
        "<p>Single-page documentation dump for AI agents. No JavaScript, no CSS, no styling. "
        f"Generated from {len(entries)} markdown files and {len(mods)} mods. Canonical location: "
        f'<a href="{SITE_URL}/ai.html">{SITE_URL}/ai.html</a></p>',
        "<hr>",
        "<h2>Contents</h2>",
    ]

    group = None
    for entry in entries:
        if entry["group"] != group:
            if group is not None:
                lines.append("</ul>")
            group = entry["group"]
            lines.append(f"<h3>{html.escape(group)}</h3>")
            lines.append("<ul>")
        lines.append(
            f'<li><a href="#{anchor(entry["key"])}">'
            f'{html.escape(entry["key"])} - {html.escape(entry["title"])}</a></li>'
        )
    if group is not None:
        lines.append("</ul>")

    if mods:
        lines.append("<h3>Mods</h3>")
        lines.append("<ul>")
        for mod in mods:
            lines.append(
                f'<li><a href="#{mod_anchor(mod["name"])}">'
                f'mods/{html.escape(mod["name"])} - {html.escape(mod["title"])}</a></li>'
            )
        lines.append("</ul>")

    for entry in entries:
        lines.append("<hr>")
        lines.append(f'<section id="{anchor(entry["key"])}">')
        lines.append(f'<h2>{html.escape(entry["title"])}</h2>')
        lines.append(
            f'<p>Source: <code>{html.escape(entry["key"])}.md</code> - '
            f'<a href="{SITE_URL}/{entry["key"]}">{SITE_URL}/{entry["key"]}</a></p>'
        )
        lines.append(render_markdown(entry["content"]))
        lines.append("</section>")

    if mods:
        lines.append("<hr>")
        lines.append('<h2 id="mods">Existing mods</h2>')
        lines.append(
            f"<p>Mods available in <code>mods/</code>: {len(mods)}. "
            "Each entry lists the mod's <code>manifest.json</code> and all files.</p>"
        )
        for mod in mods:
            lines.append(f'<section id="{mod_anchor(mod["name"])}">')
            lines.append(
                f'<h3>{html.escape(mod["name"])} - {html.escape(mod["title"])}</h3>'
            )
            lines.append(
                f'<p>Path: <code>mods/{html.escape(mod["name"])}</code> - '
                f'{len(mod["files"])} files</p>'
            )
            lines.append("<h4>manifest.json</h4>")
            lines.append(
                '<pre><code class="language-json">'
                f'{html.escape(mod["manifest"], quote=False)}</code></pre>'
            )
            lines.append("<h4>Files</h4>")
            lines.append("<ul>")
            for rel in mod["files"]:
                lines.append(f'<li><code>{html.escape(rel, quote=False)}</code></li>')
            lines.append("</ul>")
            lines.append("</section>")

    lines.append("</body>")
    lines.append("</html>")
    return "\n".join(lines) + "\n"


def main():
    if not os.path.isdir(DOCS_DIR):
        print(f"Error: {DOCS_DIR} not found", file=sys.stderr)
        sys.exit(1)

    entries = collect_entries()
    mods = collect_mods()
    page = build_page(entries, mods)

    with open(OUT_FILE, "w", encoding="utf-8", newline="\n") as f:
        f.write(page)

    total_bytes = os.path.getsize(OUT_FILE)
    print(f"OK  {OUT_FILE}  ({len(entries)} documents, {len(mods)} mods, {total_bytes} bytes)")


if __name__ == "__main__":
    main()
