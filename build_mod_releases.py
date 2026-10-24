#!/usr/bin/env python3
"""Build a release archive of every mod and the catalog files that publish them.

Does for all mods at once what the game's "Export to Catalog" does for one
(Core/Mods/ModCatalogExporter.cs, editor_tools CatalogExportWindow):

  * releases/mod_<id>-v<version>.zip - the mod folder's content files at the
    archive root, hidden entries (.git/, .gitignore, ...) left out
    (Core/Mods/ModContentFiles.cs);
  * releases/mod_<id>-v<version>.json - the mod's catalog entry next to it;
  * releases/<host>/catalog.json - the catalog envelope with every entry, one per
    host whose catalog repository serves it (git.cthangover.ru/cthangover/catalog,
    github.com/cthangover/catalog), each with that host's download and icon URLs.

Entries take id, name, author, version, description, category, depends and
conflicts_with from manifest.json; locale from the mod's own
mod_<id>_name / mod_<id>_desc keys in locale/*.properties; tags from the id's
words and the categories. archive_sha256 is the zip's SHA-256, install_sha256
the content hash of Core/Mods/ModHasher.cs (per file SHA-256(path + bytes) in
case-insensitive path order, then SHA-256 of the concatenation), with the
catalog_ignore_patterns of config/mod_config.json. Archives are reproducible:
entries are sorted and carry a fixed date, so an unchanged mod keeps its hash.

    python build_mod_releases.py                     all mods in mods/
    python build_mod_releases.py --only=rpg,trading  only these mods (the catalogs list only them)
    python build_mod_releases.py --skip=test_defaults_and_patches
    python build_mod_releases.py --hosts=gitea       one catalog only
    python build_mod_releases.py --out=D:/releases   another output folder

Only files the script writes are replaced; nothing else in the output folder is
deleted. Mods with uncommitted changes are built from the working tree with a
warning. Exit code: 0 - every mod built; 1 - an error.
"""

import argparse
import hashlib
import json
import re
import subprocess
import sys
import zipfile
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parent
MODS = ROOT / "mods"
CONFIG = ROOT / "config" / "mod_config.json"
OWNER = "cthangover"
REPO_PREFIX = "mod_"
CATALOG_VERSION = 1
# LanguageCode.Supported, English first: the catalog's fallback language.
LANGUAGES = ["en", "ru", "es", "zh", "ja", "ko", "vi"]
ICON_NAMES = ["mod.jpg", "mod.png"]
ZIP_DATE = (1980, 1, 1, 0, 0, 0)


def gitea_urls(repo, version, archive, branch, icon):
    base = "https://git.cthangover.ru/%s/%s" % (OWNER, repo)
    return (
        "%s/releases/download/v%s/%s" % (base, version, archive),
        "%s/raw/branch/%s/%s" % (base, branch, icon) if icon else None,
    )


def github_urls(repo, version, archive, branch, icon):
    return (
        "https://github.com/%s/%s/releases/download/v%s/%s" % (OWNER, repo, version, archive),
        "https://raw.githubusercontent.com/%s/%s/%s/%s" % (OWNER, repo, branch, icon) if icon else None,
    )


HOSTS = {"gitea": gitea_urls, "github": github_urls}


class ScriptError(Exception):
    """A user-facing error: printed without a traceback."""


# --------------------------------------------------------------------- content


def is_hidden(name):
    return name.startswith(".")


def content_files(folder):
    """Relative '/' paths of the mod's content files; hidden directories are not walked."""
    result = []
    pending = [folder]
    while pending:
        for entry in pending.pop().iterdir():
            if is_hidden(entry.name):
                continue
            if entry.is_dir():
                pending.append(entry)
            else:
                result.append(entry.relative_to(folder).as_posix())
    return result


def ordinal_ignore_case(path):
    # .NET OrdinalIgnoreCase: per-character upper case, no expansions (ß stays ß).
    return "".join(c.upper() if len(c.upper()) == 1 else c for c in path)


def wildcard(pattern):
    """ModHasher.MatchesWildcard: '*' any run (slashes included), '?' one character, whole path."""
    return re.compile("".join(".*" if c == "*" else "." if c == "?" else re.escape(c) for c in pattern), re.S)


def ignored(path, patterns):
    if any(part and is_hidden(part) for part in path.split("/")):
        return True
    return any(p.fullmatch(path) for p in patterns)


def install_sha256(folder, files, patterns):
    hashes = b""
    for path in sorted((f for f in files if not ignored(f, patterns)), key=ordinal_ignore_case):
        hashes += hashlib.sha256(path.encode("utf-8") + (folder / path).read_bytes()).digest()
    return hashlib.sha256(hashes).hexdigest()


def file_sha256(path):
    digest = hashlib.sha256()
    with open(path, "rb") as stream:
        for chunk in iter(lambda: stream.read(1 << 20), b""):
            digest.update(chunk)
    return digest.hexdigest()


def write_zip(folder, files, target):
    temp = target.with_suffix(".zip.tmp")
    with zipfile.ZipFile(temp, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        for path in sorted(files, key=ordinal_ignore_case):
            info = zipfile.ZipInfo(path, ZIP_DATE)
            info.compress_type = zipfile.ZIP_DEFLATED
            info.external_attr = 0o644 << 16
            archive.writestr(info, (folder / path).read_bytes(), compresslevel=9)
    temp.replace(target)


# -------------------------------------------------------------------- metadata


def read_properties(text):
    """LocaleLoader's parser: key = value per line, '#' comments, first '=' splits."""
    result = {}
    for raw in text.replace("\r\n", "\n").split("\n"):
        line = raw.strip()
        if not line or line.startswith("#") or "=" not in line:
            continue
        key, value = line.split("=", 1)
        if key.strip():
            result[key.strip()] = value.strip()
    return result


def mod_locale(folder, mod_id):
    """[{lang, name, description}] from locale/<lang>.properties and locale/*_<lang>.properties."""
    locale_dir = folder / "locale"
    entries = []
    for lang in LANGUAGES:
        texts = {}
        if locale_dir.is_dir():
            for file in sorted(locale_dir.iterdir()):
                if file.is_file() and (file.name == lang + ".properties" or file.name.endswith("_%s.properties" % lang)):
                    texts.update(read_properties(file.read_text(encoding="utf-8-sig")))
        name = texts.get("mod_%s_name" % mod_id)
        description = texts.get("mod_%s_desc" % mod_id)
        if name or description:
            entry = {"lang": lang}
            if name:
                entry["name"] = name
            if description:
                entry["description"] = description
            entries.append(entry)
    return entries


def categories(manifest):
    raw = manifest.get("category") or "other"
    if isinstance(raw, list):
        raw = ",".join(raw)
    return [c.strip().lower() for c in str(raw).split(",") if c.strip()] or ["other"]


def tags(mod_id, cats):
    result = []
    for tag in mod_id.split("_") + cats:
        if tag and tag not in result:
            result.append(tag)
    return result


def git_branch(folder):
    try:
        out = subprocess.run(["git", "-C", str(folder), "branch", "--show-current"],
                             capture_output=True, text=True, timeout=30)
        return out.stdout.strip() or "master"
    except (OSError, subprocess.SubprocessError):
        return "master"


def git_dirty(folder):
    try:
        out = subprocess.run(["git", "-C", str(folder), "status", "--porcelain"],
                             capture_output=True, text=True, timeout=30)
        return out.returncode == 0 and bool(out.stdout.strip())
    except (OSError, subprocess.SubprocessError):
        return False


def entry_for(manifest, mod_id, locale, cats, download, icon_url):
    """The field order of CatalogExportEntry; null fields are left out like the game's writer does."""
    entry = {
        "id": mod_id,
        "name": manifest.get("name") or mod_id,
        "author": manifest.get("author"),
        "version": manifest["version"],
        "description": manifest.get("description"),
        "locale": locale or [{"lang": "en", "name": manifest.get("name") or mod_id,
                              "description": manifest.get("description")}],
        "category": ", ".join(cats),
        "tags": tags(mod_id, cats),
        "icon_url": icon_url,
        "depends": list(manifest.get("depends") or []),
        "conflicts_with": list(manifest.get("conflicts_with") or []),
        "downloads": [download],
        "status": "stable",
    }
    return {k: v for k, v in entry.items() if v is not None}


# ------------------------------------------------------------------------ main


def load_patterns():
    patterns = [".*"]
    if CONFIG.is_file():
        config = json.loads(CONFIG.read_text(encoding="utf-8-sig"))
        patterns = config.get("catalog_ignore_patterns") or patterns
    return [wildcard(p) for p in patterns]


def select_mods(only, skip):
    folders = sorted((d for d in MODS.iterdir() if d.is_dir() and (d / "manifest.json").is_file()),
                     key=lambda d: d.name)
    if only:
        missing = sorted(set(only) - {d.name for d in folders})
        if missing:
            raise ScriptError("no mod folder for: " + ", ".join(missing))
        folders = [d for d in folders if d.name in only]
    return [d for d in folders if d.name not in skip]


def build_mod(folder, out, patterns, hosts):
    manifest = json.loads((folder / "manifest.json").read_text(encoding="utf-8-sig"))
    mod_id = manifest.get("id")
    if mod_id != folder.name:
        raise ScriptError("%s: manifest id '%s' differs from the folder name" % (folder.name, mod_id))
    if not manifest.get("version"):
        raise ScriptError("%s: manifest has no version" % mod_id)
    if git_dirty(folder):
        print("  warning: %s has uncommitted changes, built from the working tree" % mod_id)

    version = manifest["version"]
    repo = REPO_PREFIX + mod_id
    archive_name = "%s-v%s.zip" % (repo, version)
    archive = out / archive_name
    files = content_files(folder)
    write_zip(folder, files, archive)

    locale = mod_locale(folder, mod_id)
    cats = categories(manifest)
    icon = next((n for n in ICON_NAMES if (folder / n).is_file()), None)
    branch = git_branch(folder)
    hashes = {
        "size_bytes": archive.stat().st_size,
        "archive_sha256": file_sha256(archive),
        "install_sha256": install_sha256(folder, files, patterns),
    }

    entries = {}
    for host in hosts:
        download_url, icon_url = HOSTS[host](repo, version, archive_name, branch, icon)
        download = {"version": version, "urls": [download_url], **hashes}
        entries[host] = entry_for(manifest, mod_id, locale, cats, download, icon_url)

    write_json(archive.with_suffix(".json"), entries[hosts[0]])
    print("  %-28s %-10s %4d files %10d bytes" % (mod_id, version, len(files), hashes["size_bytes"]))
    return entries


def write_json(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as stream:
        json.dump(data, stream, ensure_ascii=False, indent=2)
        stream.write("\n")


def split_list(value):
    return [v.strip() for v in (value or "").split(",") if v.strip()]


def main():
    parser = argparse.ArgumentParser(description="Build release archives of the mods and their catalogs.")
    parser.add_argument("--out", default=str(ROOT / "releases"), help="output folder (default: releases/)")
    parser.add_argument("--only", help="comma-separated mod ids to build")
    parser.add_argument("--skip", help="comma-separated mod ids to leave out")
    parser.add_argument("--hosts", default="gitea,github", help="catalogs to write: gitea, github")
    args = parser.parse_args()

    hosts = split_list(args.hosts)
    unknown = [h for h in hosts if h not in HOSTS]
    if not hosts or unknown:
        raise ScriptError("--hosts takes gitea and/or github, got: " + args.hosts)

    out = Path(args.out).resolve()
    out.mkdir(parents=True, exist_ok=True)
    patterns = load_patterns()
    mods = select_mods(set(split_list(args.only)), set(split_list(args.skip)))
    if not mods:
        raise ScriptError("no mods to build")

    print("Building %d mods into %s" % (len(mods), out))
    catalogs = {host: [] for host in hosts}
    for folder in mods:
        for host, entry in build_mod(folder, out, patterns, hosts).items():
            catalogs[host].append(entry)

    updated = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
    for host, entries in catalogs.items():
        path = out / host / "catalog.json"
        write_json(path, {"catalog": {"version": CATALOG_VERSION, "updated_at": updated}, "mods": entries})
        print("Catalog %s: %d mods -> %s" % (host, len(entries), path))
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except ScriptError as error:
        print("error: %s" % error, file=sys.stderr)
        sys.exit(1)
