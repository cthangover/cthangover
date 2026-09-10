"""Build sitemap.xml from all .md files in docs/ and quickstart/.

The site is a single-page app served with a history-API fallback,
so every content page is exposed as a real path:

    https://<host>/docs/<slug>
    https://<host>/quickstart/<lang>/<page>

Output:
    <?xml version="1.0" encoding="UTF-8"?>
    <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
      <url><loc>...</loc><lastmod>...</lastmod></url>
      ...
    </urlset>
"""

import os
from datetime import datetime, timezone

SITE_URL = "https://cthangover.ru"
DOCS_DIR = "docs"
QUICKSTART_DIR = "quickstart"
OUT_FILE = "sitemap.xml"


def lastmod(path):
    """Return the file's modification date as YYYY-MM-DD (UTC)."""
    ts = os.path.getmtime(path)
    return datetime.fromtimestamp(ts, tz=timezone.utc).strftime("%Y-%m-%d")


def collect_docs():
    """Walk docs/ recursively and return (url, lastmod) for every .md file."""
    urls = []
    for root, dirs, files in os.walk(DOCS_DIR):
        dirs[:] = sorted(d for d in dirs if not d.startswith("."))
        for name in sorted(files):
            if not name.endswith(".md"):
                continue
            full = os.path.join(root, name)
            slug = os.path.relpath(full, DOCS_DIR).replace("\\", "/")[:-3]
            urls.append((f"{SITE_URL}/docs/{slug}", lastmod(full)))
    return urls


def collect_quickstart():
    """Walk quickstart/ and return (url, lastmod) for every language page."""
    urls = []
    if not os.path.isdir(QUICKSTART_DIR):
        return urls

    for lang in sorted(os.listdir(QUICKSTART_DIR)):
        lang_dir = os.path.join(QUICKSTART_DIR, lang)
        if not os.path.isdir(lang_dir):
            continue
        for name in sorted(os.listdir(lang_dir)):
            if not name.endswith(".md"):
                continue
            full = os.path.join(lang_dir, name)
            page = name[:-3]
            urls.append((f"{SITE_URL}/quickstart/{lang}/{page}", lastmod(full)))

    return urls


def main():
    urls = [(f"{SITE_URL}/", lastmod("index.html"))]
    urls += collect_docs()
    urls += collect_quickstart()

    lines = [
        '<?xml version="1.0" encoding="UTF-8"?>',
        '<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">',
    ]
    for loc, date in urls:
        lines.append("  <url>")
        lines.append(f"    <loc>{loc}</loc>")
        lines.append(f"    <lastmod>{date}</lastmod>")
        lines.append("  </url>")
    lines.append("</urlset>")

    with open(OUT_FILE, "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")

    print(f"OK  {OUT_FILE}  ({len(urls)} URLs)")


if __name__ == "__main__":
    main()
