"""Build the AI reference pages: documentation, code, tests and mod resources.

The content is split into four plain-text dumps, so an AI agent can load
only what it needs. Every page is a single <pre> block, so there is no
formatting markup beyond the bare HTML frame. Markdown links lose their
URLs, embedded HTML loses its tags, C# code is whitespace-compacted
(comments are kept).

Sources:
    docs/**/*.md            - the main documentation
    quickstart/en/*.md      - the English quick start guide
    ../**/*.cs              - C# sources of the project and the mods
    ../tests/**/*.cs        - unit and integration tests
    ../Core/Autotest/*.cs   - test tooling (SceneSwitcher, drivers)
    ../mods/*/manifest.json - existing mods (manifest + file list)
    ../mods/**/*.scenario   - scenario scripts of the mods

Output:
    ai_docs.html  - documentation
    ai_code.html  - C# sources, tests excluded
    ai_tests.html - C# test sources and test tooling
    ai_res.html   - mod manifests and scenario scripts
"""

import html
import json
import os
import re
import sys

DOCS_DIR = "docs"
QUICKSTART_DIR = "quickstart"
QUICKSTART_LANGS = ("en",)
ROOT_DIR = ".."
MODS_DIR = os.path.join("..", "mods")
TEST_DIR = "tests"
AUTOTEST_DIR = "Core/Autotest"
DOCS_OUT_FILE = "ai_docs.html"
CODE_OUT_FILE = "ai_code.html"
TESTS_OUT_FILE = "ai_tests.html"
RES_OUT_FILE = "ai_res.html"
LEGACY_OUT_FILE = "ai.html"
CS_EXT = ".cs"
SCENARIO_EXT = ".scenario"
SKIP_DIRS = {
    ".git", ".godot", ".idea", ".omo", ".opencode",
    "obj", "bin", "node_modules", "site",
}

FENCE_RE = re.compile(r"^(`{3,}|~{3,})\s*([\w+-]*)\s*$")
MD_LINK_RE = re.compile(r"(!?)\[([^\]]*)\]\(([^)\s]*)(?:\s+\"[^\"]*\")?\)")
AUTOLINK_RE = re.compile(r"<((?:https?://|mailto:)[^<>\s]+)>")
HTML_TAG_RE = re.compile(
    r"</?(?:a|b|br|blockquote|code|dd|del|details|div|dl|dt|em|h[1-6]|hr|i|"
    r"img|li|ol|p|path|pre|s|span|strong|summary|svg|table|tbody|td|th|"
    r"thead|tr|u|ul)\b[^>]*>",
    re.IGNORECASE,
)
HTML_COMMENT_RE = re.compile(r"<!--.*?-->")


# --------------------------------------------------------------------------
# C# whitespace compaction
# --------------------------------------------------------------------------

_CS_DROP = set("()[]{};,:")
_CS_OPER = set("+-*/%<>=!&|^~?")


def _cs_needs_space(prev, nxt):
    """Whether adjacent characters need a space to remain separate tokens."""
    if not prev or not nxt or prev == "\n":
        return False
    if prev == "." or nxt == ".":
        other = nxt if prev == "." else prev
        return other.isdigit()
    if prev in _CS_DROP or nxt in _CS_DROP:
        return False
    if prev in _CS_OPER and nxt in _CS_OPER:
        return True
    return (prev.isalnum() or prev == "_") and (nxt.isalnum() or nxt == "_")


def _cs_scan_quoted(text, i):
    """Skip a regular "..." or '...' literal; i points at the opening quote."""
    quote = text[i]
    j = i + 1
    n = len(text)
    while j < n:
        if text[j] == "\\":
            j += 2
            continue
        if text[j] == quote or text[j] == "\n":
            return j + 1
        j += 1
    return n


def _cs_scan_verbatim(text, i):
    """Skip a verbatim @"..." literal; i points at the opening quote."""
    j = i + 1
    n = len(text)
    while j < n:
        if text[j] == '"':
            if j + 1 < n and text[j + 1] == '"':
                j += 2
                continue
            return j + 1
        j += 1
    return n


def _cs_scan_raw(text, i, quotes):
    """Skip a raw string literal; i points at the opening quote run."""
    j = i + quotes
    n = len(text)
    while j < n:
        if text[j] == '"':
            run = 1
            while j + run < n and text[j + run] == '"':
                run += 1
            if run >= quotes:
                return j + run
            j += run
            continue
        j += 1
    return n


def _cs_scan_hole(text, i):
    """Skip an interpolation hole; i points right after the opening '{'."""
    depth = 1
    n = len(text)
    while i < n:
        literal_end = _cs_scan_literal(text, i)
        if literal_end is not None:
            i = literal_end
            continue
        if text.startswith("//", i):
            newline = text.find("\n", i)
            i = n if newline < 0 else newline + 1
            continue
        if text.startswith("/*", i):
            end = text.find("*/", i + 2)
            i = n if end < 0 else end + 2
            continue
        if text[i] == "{":
            depth += 1
        elif text[i] == "}":
            depth -= 1
            if depth == 0:
                return i + 1
        i += 1
    return n


def _cs_scan_interp(text, i, dollars, verbatim):
    """Skip an interpolated "..." or @"..." literal; i points at the quote."""
    j = i + 1
    n = len(text)
    while j < n:
        char = text[j]
        if char == '"':
            if verbatim and j + 1 < n and text[j + 1] == '"':
                j += 2
                continue
            return j + 1
        if char == "\\" and not verbatim:
            j += 2
            continue
        if char == "{":
            if dollars == 1 and j + 1 < n and text[j + 1] == "{":
                j += 2
                continue
            j = _cs_scan_hole(text, j + 1)
            continue
        if char == "}":
            if dollars == 1 and j + 1 < n and text[j + 1] == "}":
                j += 2
                continue
            j += 1
            continue
        j += 1
    return n


def _cs_scan_literal(text, i):
    """Return the index after a string or char literal at i, or None."""
    char = text[i]
    if char == "'":
        return _cs_scan_quoted(text, i)
    if char == '"':
        quotes = 1
        n = len(text)
        while i + quotes < n and text[i + quotes] == '"':
            quotes += 1
        if quotes >= 3:
            return _cs_scan_raw(text, i, quotes)
        return _cs_scan_quoted(text, i)
    if char not in "$@":
        return None

    j = i
    dollars = 0
    verbatim = False
    n = len(text)
    while j < n and text[j] in "$@":
        if text[j] == "$":
            dollars += 1
        else:
            verbatim = True
        j += 1
    if j >= n or text[j] != '"':
        return None

    quotes = 0
    while j + quotes < n and text[j + quotes] == '"':
        quotes += 1
    if quotes >= 3:
        return _cs_scan_raw(text, j, quotes)
    if verbatim:
        return _cs_scan_interp(text, j, dollars, True) if dollars else _cs_scan_verbatim(text, j)
    return _cs_scan_interp(text, j, dollars, False) if dollars else _cs_scan_quoted(text, j)


def compact_csharp(text):
    """Remove indentation, line breaks and redundant spaces from C# code.

    Strings and comments are preserved: multi-line and verbatim strings
    keep their newlines, runs of ``//`` comments are merged into one line,
    preprocessor directives keep a line of their own.
    """
    parts = []
    last = ""
    pending_ws = False
    after_line_comment = False
    at_line_start = True
    i = 0
    n = len(text)

    def put(chunk):
        nonlocal last
        parts.append(chunk)
        last = chunk[-1]

    def begin_token(first):
        nonlocal pending_ws, after_line_comment
        if after_line_comment:
            put("\n")
            after_line_comment = False
            pending_ws = False
        elif pending_ws and last and last != "\n":
            if _cs_needs_space(last, first):
                put(" ")
        pending_ws = False

    while i < n:
        char = text[i]

        if char in " \t\r\n":
            if parts:
                pending_ws = True
            if char == "\n":
                at_line_start = True
            i += 1
            continue

        if at_line_start and char == "#":
            newline = text.find("\n", i)
            if newline < 0:
                newline = n
            directive = re.sub(r"[ \t]+", " ", text[i:newline].strip())
            if parts and last != "\n":
                put("\n")
            put(directive + "\n")
            pending_ws = False
            after_line_comment = False
            at_line_start = True
            i = newline + 1
            continue

        if text.startswith("//", i):
            newline = text.find("\n", i)
            if newline < 0:
                newline = n
            comment = re.sub(r"[ \t]+", " ", text[i:newline].rstrip())
            if after_line_comment:
                body = comment.lstrip("/").strip()
                if body:
                    put(" " + body)
            else:
                if pending_ws and last == "/":
                    put(" ")
                put(comment)
            after_line_comment = True
            pending_ws = False
            at_line_start = False
            i = newline + 1
            continue

        if text.startswith("/*", i):
            end = text.find("*/", i + 2)
            if end < 0:
                comment = text[i:]
                i = n
            else:
                comment = text[i:end + 2]
                i = end + 2
            begin_token("/")
            put(re.sub(r"\s+", " ", comment))
            after_line_comment = False
            at_line_start = False
            pending_ws = False
            continue

        literal_end = _cs_scan_literal(text, i)
        if literal_end is not None:
            begin_token(char)
            put(text[i:literal_end])
            after_line_comment = False
            at_line_start = False
            pending_ws = False
            i = literal_end
            continue

        if char.isalnum() or char == "_":
            j = i + 1
            while j < n and (text[j].isalnum() or text[j] == "_"):
                j += 1
            begin_token(char)
            put(text[i:j])
            after_line_comment = False
            at_line_start = False
            pending_ws = False
            i = j
            continue

        begin_token(char)
        put(char)
        after_line_comment = False
        at_line_start = False
        pending_ws = False
        i += 1

    return "".join(parts).strip()


# --------------------------------------------------------------------------
# plain text conversion
# --------------------------------------------------------------------------

def _link_text(match):
    if match.group(1) == "!":
        alt = match.group(2)
        return f"[image: {alt}]" if alt else "[image]"
    return match.group(2)


def _outside_code(line, transform):
    """Apply a transform only to the parts of a line outside `code` spans."""
    parts = line.split("`")
    for k in range(0, len(parts), 2):
        parts[k] = transform(parts[k])
    return "`".join(parts)


def plain_markdown(text):
    """Turn markdown into plain text.

    Links and images keep only their text, embedded HTML loses its tags,
    fenced code blocks are passed through unchanged.
    """
    out = []
    in_fence = False
    for line in text.splitlines():
        if FENCE_RE.match(line.strip()):
            in_fence = not in_fence
            out.append(line)
            continue
        if in_fence:
            out.append(line)
            continue
        line = AUTOLINK_RE.sub(r"\1", line)
        line = MD_LINK_RE.sub(_link_text, line)
        line = _outside_code(
            line,
            lambda s: html.unescape(HTML_COMMENT_RE.sub("", HTML_TAG_RE.sub("", s))),
        )
        out.append(line.rstrip())
    return "\n".join(out)


def escape_pre(text):
    """Escape text for a <pre> block; '>' needs no escaping in HTML."""
    return text.replace("&", "&amp;").replace("<", "&lt;")


# --------------------------------------------------------------------------
# collection
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


def read_text(path):
    with open(path, encoding="utf-8-sig", errors="replace") as f:
        return f.read()


def is_test_source(rel):
    """Whether a repository-relative path is a test or test-tooling source."""
    return rel.startswith(TEST_DIR + "/") or rel.startswith(AUTOTEST_DIR + "/")


def collect_sources():
    """Collect every .cs file of the project and the mods.

    Returns (code_sources, test_sources) mapping a top-level directory (mods
    use "mods/<name>") to a sorted list of (repository-relative path, source).
    Tests are the files under tests/ and Core/Autotest/.
    """
    code_sources = {}
    test_sources = {}
    if not os.path.isdir(ROOT_DIR):
        return code_sources, test_sources

    for root, dirs, files in os.walk(ROOT_DIR):
        dirs[:] = sorted(
            d for d in dirs if d not in SKIP_DIRS and not d.startswith(".")
        )
        for name in sorted(files):
            if not name.endswith(CS_EXT):
                continue
            path = os.path.join(root, name)
            rel = os.path.relpath(path, ROOT_DIR).replace("\\", "/")
            parts = rel.split("/")
            if len(parts) > 2 and parts[0] == "mods":
                group = f"mods/{parts[1]}"
            else:
                group = parts[0] if len(parts) > 1 else "(root)"
            target = test_sources if is_test_source(rel) else code_sources
            target.setdefault(group, []).append((rel, read_text(path)))

    for files in (*code_sources.values(), *test_sources.values()):
        files.sort(key=lambda item: item[0])
    return code_sources, test_sources


def collect_scenarios():
    """Collect every .scenario file of the mods, grouped by mod directory."""
    scenarios = {}
    if not os.path.isdir(MODS_DIR):
        return scenarios

    for root, dirs, files in os.walk(MODS_DIR):
        dirs[:] = sorted(d for d in dirs if not d.startswith("."))
        for name in sorted(files):
            if not name.endswith(SCENARIO_EXT):
                continue
            path = os.path.join(root, name)
            rel = os.path.relpath(path, MODS_DIR).replace("\\", "/")
            if "/" not in rel:
                continue
            mod, inner = rel.split("/", 1)
            scenarios.setdefault("mods/" + mod, []).append((inner, read_text(path)))

    for files in scenarios.values():
        files.sort(key=lambda item: item[0])
    return scenarios


# --------------------------------------------------------------------------
# page assembly
# --------------------------------------------------------------------------

def _source_sections(sources):
    """Render the ===== FILE ... ===== sections of grouped C# sources."""
    body = []
    for group in sorted(sources):
        for rel, text in sources[group]:
            body.append("")
            body.append(f"===== FILE {rel} =====")
            body.append(compact_csharp(text))
    return body


def build_docs_page(entries):
    body = [
        "cthangover - documentation",
        f"Plain-text dump for AI agents: {len(entries)} markdown documents.",
        "",
        "===== DOCUMENTATION =====",
    ]
    for entry in entries:
        body.append("")
        body.append(f"===== DOC {entry['key']}.md: {entry['title']} =====")
        body.append(plain_markdown(entry["content"]).strip())
    return body


def build_code_page(sources):
    total = sum(len(files) for files in sources.values())
    body = [
        "cthangover - source code",
        f"Plain-text dump for AI agents: {total} C# files, tests excluded.",
        "",
        "===== SOURCE CODE (C#) =====",
    ]
    body.extend(_source_sections(sources))
    return body


def build_tests_page(sources):
    total = sum(len(files) for files in sources.values())
    body = [
        "cthangover - test code",
        f"Plain-text dump for AI agents: {total} C# files "
        "(tests and test tooling).",
        "",
        "===== TEST CODE (C#) =====",
    ]
    body.extend(_source_sections(sources))
    return body


def build_res_page(mods, scenarios):
    scenario_total = sum(len(files) for files in scenarios.values())
    body = [
        "cthangover - mod resources",
        f"Plain-text dump for AI agents: {len(mods)} mods, "
        f"{scenario_total} scenarios.",
    ]

    if mods:
        body.append("")
        body.append("===== EXISTING MODS =====")
        for mod in mods:
            body.append("")
            body.append(f"===== MOD mods/{mod['name']}: {mod['title']} =====")
            body.append("manifest.json:")
            body.append(mod["manifest"].strip())
            body.append(f"files ({len(mod['files'])}):")
            body.extend(mod["files"])

    if scenario_total:
        body.append("")
        body.append("===== SCENARIOS =====")
        for group in sorted(scenarios):
            for rel, text in scenarios[group]:
                body.append("")
                body.append(f"===== SCENARIO {group}/{rel} =====")
                body.append(text.strip())

    return body


def render_page(title, body):
    lines = [
        "<!DOCTYPE html>",
        '<html lang="en">',
        "<head>",
        '<meta charset="utf-8">',
        f"<title>{html.escape(title)}</title>",
        "</head>",
        "<body>",
        "<pre>",
        escape_pre("\n".join(body)),
        "</pre>",
        "</body>",
        "</html>",
    ]
    return "\n".join(lines) + "\n"


def main():
    if not os.path.isdir(DOCS_DIR):
        print(f"Error: {DOCS_DIR} not found", file=sys.stderr)
        sys.exit(1)

    entries = collect_entries()
    mods = collect_mods()
    code_sources, test_sources = collect_sources()
    scenarios = collect_scenarios()

    code_total = sum(len(files) for files in code_sources.values())
    test_total = sum(len(files) for files in test_sources.values())
    scenario_total = sum(len(files) for files in scenarios.values())

    docs_title = "cthangover - documentation"
    code_title = "cthangover - source code"
    tests_title = "cthangover - test code"
    res_title = "cthangover - mod resources"

    pages = (
        (
            DOCS_OUT_FILE,
            docs_title,
            build_docs_page(entries),
            f"{len(entries)} documents",
        ),
        (
            CODE_OUT_FILE,
            code_title,
            build_code_page(code_sources),
            f"{code_total} C# files",
        ),
        (
            TESTS_OUT_FILE,
            tests_title,
            build_tests_page(test_sources),
            f"{test_total} C# files",
        ),
        (
            RES_OUT_FILE,
            res_title,
            build_res_page(mods, scenarios),
            f"{len(mods)} mods, {scenario_total} scenarios",
        ),
    )

    for path, title, body, summary in pages:
        with open(path, "w", encoding="utf-8", newline="\n") as f:
            f.write(render_page(title, body))
        print(f"OK  {path}  ({summary}, {os.path.getsize(path)} bytes)")

    if os.path.isfile(LEGACY_OUT_FILE):
        os.remove(LEGACY_OUT_FILE)
        print(f"REMOVED  {LEGACY_OUT_FILE} (replaced by the split pages)")


if __name__ == "__main__":
    main()
