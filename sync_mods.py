#!/usr/bin/env python3
"""Full sync of the mods with Gitea and GitHub: repositories, code, releases, catalogs.

One run compares mods/ with both hosts, prints every difference, asks once and
then brings both hosts in line with the local mods:

  1. builds every mod's release archive and catalog entry into releases/
     (build_mod_releases.py);
  2. creates the missing mod_<id> repositories (sync_mod_repos.py); GitHub lets
     an account create about 8 repositories at a time and then blocks for half
     an hour, so creation goes in batches of --github-batch with a pause of
     --github-pause minutes between them (the script waits, then continues);
  3. pushes a mod's branch when the host is behind (fast-forward only; a host
     that is ahead or has diverged is reported and left alone);
  4. publishes the archive as the asset of release v<version> on both hosts:
     an existing release of that version is reused - its archive is replaced
     and copies of the mod's archive under other names are removed, a draft is
     made public - and only a version without a release gets a new one; earlier
     versions keep their releases. A host that already has a later version than
     the local one (a release tag or its catalog entry) is never given the older
     one;
  5. updates catalog.json in both catalog repositories
     (cthangover/catalog on git.cthangover.ru and on github.com, branch main).
  6. publishes the game itself: the packages build_release left in releases/
     (cthangover-<config/version>-windows.zip, -linux.tar.gz) as release
     v<version> of this repository (cthangover/cthangover) on both hosts, with the
     same rules - one release per version, packages replaced when they differ
     (their SHA-256 is written into the release text), older-named copies in
     that release (cthangover-0.1.0.0-*) removed, never over a later version.
     The main repository is not pushed: the tag is made on the host branch.
     --no-game skips it.

What counts as a difference, per mod and host:
  repo     the mod_<id> repository is missing;
  push     the host's branch is behind the local commit (or missing);
  ahead!   the host has commits the local repository lacks / diverged!: both
           have own commits - nothing is pushed, pull first;
  release  release v<version> or its asset is missing / replace: its asset differs
           from the local archive (size, GitHub's digest, or the catalog's hash),
           the release holds copies of the archive or is a draft / older!: the
           host has a later version - nothing is released, the catalog keeps it;
  notes    the release text differs from the generated one (a mod: the
           manifest's name and description, id, category, depends, how to install, SHA-256; the game:
           platforms and how to run, SHA-256) - only the text is updated;
  catalog  the online catalog lacks the mod's current entry or describes another
           archive.
A mod with uncommitted changes is reported and neither pushed nor released:
commit it first. A catalog keeps its collections and the older-version downloads
of every entry; entries of mods that have no local folder are dropped, and a mod
that could not be released keeps its previous entry.

    python sync_mods.py                    report, ask, apply (no input = report only)
    python sync_mods.py --yes              apply without asking
    python sync_mods.py --only=rpg,trading only these mods (others keep their catalog entries)
    python sync_mods.py --delete           also delete online mod_* repositories without a
                                           local mod (asks per mod, like sync_mod_repos.py)

Hosts, owners and tokens come from this repository's remotes `origin` (Gitea) and
`github`, or GITEA_TOKEN / GITHUB_TOKEN and the options of sync_mod_repos.py.
Running it again is safe: every step checks the host first. Exit code: 0 - in
sync; 1 - an error or something left to do.
"""

import argparse
import json
import re
import sys
import time
import types
import urllib.error
import urllib.parse
import urllib.request
import uuid
from datetime import datetime, timedelta
from pathlib import Path

import build_mod_releases as builder
import sync_mod_repos as repos

ROOT = Path(__file__).resolve().parent
CATALOG_REPO = "catalog"
CATALOG_BRANCH = "main"
CATALOG_FILE = "catalog.json"
UPLOAD_TIMEOUT = 1800
PROXY_WAIT = 300


class Pending(Exception):
    """A step left for a later run (a host answered with a rate limit, ...)."""


# ------------------------------------------------------------------ host access


def sanitize(host, text):
    return (text or "").replace(host.token, "***") if host.token else (text or "")


def raw_request(host, method, url, data=None, content_type=None, timeout=UPLOAD_TIMEOUT):
    headers = host.headers()
    if content_type:
        headers["Content-Type"] = content_type
    request = urllib.request.Request(url, data=data, headers=headers, method=method)
    try:
        with urllib.request.urlopen(request, timeout=timeout) as response:
            body = response.read()
            return json.loads(body) if body else {}
    except urllib.error.HTTPError as exc:
        body = exc.read().decode("utf-8", "replace").strip()
        raise repos.ApiError("%s: %s %s -> HTTP %d%s" % (host.name, method, url.split("?")[0], exc.code,
                                                       (": " + body[:300]) if body else ""), exc.code, body) from None
    except urllib.error.URLError as exc:
        raise repos.ScriptError("%s: cannot reach %s: %s" % (host.name, url.split("?")[0], exc.reason)) from None


def is_rate_limit(error):
    text = (error.body or "").lower()
    return error.status in (403, 429) and ("rate limit" in text or "too quickly" in text or "abuse" in text) \
        or (error.status == 422 and "too quickly" in text)


def owner_path(host, repo):
    return "/repos/%s/%s" % (urllib.parse.quote(host.owner), repo)


def list_releases(host, repo):
    """Every release of the repository with its assets (drafts included for the owner's token)."""
    try:
        releases = host.api("GET", "%s/releases?per_page=100&limit=50" % owner_path(host, repo))
    except repos.ApiError as exc:
        if exc.status == 404:
            return []
        raise
    return releases if isinstance(releases, list) else []


def version_key(version):
    """1.10.0 > 1.9.2; a missing or non-numeric part counts as 0."""
    parts = re.split(r"[.+-]", (version or "").lstrip("vV"))[:3]
    return tuple(int(p) if p.isdigit() else 0 for p in parts + ["0"] * (3 - len(parts)))


def release_of(releases, version):
    """The release of this version: tag v<version>, or an earlier spelling of it (v0.1.0.0 for 0.1.0)."""
    exact = next((r for r in releases if r.get("tag_name") == "v" + version), None)
    return exact or next((r for r in releases if version_key(r.get("tag_name")) == version_key(version)), None)


def mod_assets(release, repo):
    """The release's archives of this mod: the current one and copies under other names."""
    return [a for a in release.get("assets") or []
            if a.get("name", "").endswith(".zip")
            and (a["name"].startswith(repo + "-") or a["name"] == repo + ".zip")]


def create_release(host, repo, tag, branch, title, notes):
    payload = {"tag_name": tag, "target_commitish": branch, "name": title, "body": notes, "draft": False,
               "prerelease": False}
    return host.api("POST", "%s/releases" % owner_path(host, repo), payload)


def update_notes(host, repo, release, title, notes):
    """Title and text of an existing release (a draft found under the tag goes public)."""
    return host.api("PATCH", "%s/releases/%s" % (owner_path(host, repo), release["id"]),
                    {"name": title, "body": notes, "draft": False})


def same_notes(release, notes):
    return (release.get("body") or "").replace("\r\n", "\n").strip() == notes.strip()


# ---------------------------------------------------------------- release notes

SHA_LINE = re.compile(r"^([0-9a-f]{64})\s+\*?(\S+)\s*$", re.M)
SOURCE_NOTE = ("> The **Source code** archives the host adds below are automatic archives of the repository "
               "at this tag — the source tree, not ready-to-run files. Download the %s above instead.")


def listed_hashes(release):
    """file name -> SHA-256 from the release text's `sha256sum` block."""
    return {name: digest for digest, name in SHA_LINE.findall(release.get("body") or "")}


def sha_block(hashes):
    return "### SHA256\n\n```\n%s\n```" % "\n".join("%s  %s" % (hashes[n], n) for n in sorted(hashes))


def mod_title(state):
    return "%s %s" % (state.entry.get("name") or state.mod.mod_id, state.version)


def mod_notes(state):
    entry = state.entry
    depends = ", ".join("`%s`" % d for d in entry.get("depends") or []) or "-"
    archive = state.archive.name
    return "\n\n".join([
        "# " + mod_title(state),
        # The manifest's own English description: the full text, not the short locale one.
        entry.get("description") or "",
        "| | |\n|---|---|\n| Mod id | `%s` |\n| Category | %s |\n| Depends on | %s |"
        % (state.mod.mod_id, entry.get("category") or "other", depends),
        "### Install\n\nIn the game: **Mods → Network** lists it from the catalog. By hand: put `%s` as it is "
        "into the game's `mods/` folder, or unpack it into `mods/%s/`." % (archive, state.mod.mod_id),
        sha_block({archive: state.download["archive_sha256"]})
        + "\n\nContent hash (`install_sha256` of the catalog): `%s`" % state.download["install_sha256"],
        SOURCE_NOTE % ("`%s`" % archive),
    ]) + "\n"


def delete_asset(host, repo, release, asset):
    if host.name == "github":
        host.api("DELETE", "%s/releases/assets/%s" % (owner_path(host, repo), asset["id"]))
    else:
        host.api("DELETE", "%s/releases/%s/assets/%s" % (owner_path(host, repo), release["id"], asset["id"]))


def upload_asset(host, repo, release, path):
    name = urllib.parse.quote(path.name)
    data = path.read_bytes()
    kind = "application/gzip" if path.name.endswith(".gz") else "application/zip"
    if host.name == "github":
        url = "https://uploads.github.com%s/releases/%s/assets?name=%s" % (owner_path(host, repo), release["id"], name)
        return raw_request(host, "POST", url, data, kind)
    boundary = uuid.uuid4().hex
    body = (("--%s\r\nContent-Disposition: form-data; name=\"attachment\"; filename=\"%s\"\r\n"
             "Content-Type: %s\r\n\r\n") % (boundary, path.name, kind)).encode("utf-8") \
        + data + ("\r\n--%s--\r\n" % boundary).encode("utf-8")
    url = "%s%s/releases/%s/assets?name=%s" % (host.api_base, owner_path(host, repo), release["id"], name)
    return raw_request(host, "POST", url, body, "multipart/form-data; boundary=" + boundary)


def upload_checked(host, repo, release, path):
    """upload_asset that survives a proxy timeout: nginx may give up (502/503/504) while the
    host is still storing a large file, so the release is watched for the asset to appear."""
    try:
        return upload_asset(host, repo, release, path)
    except repos.ApiError as exc:
        if exc.status not in (502, 503, 504):
            raise
        size = path.stat().st_size
        print("  %s: %s answered HTTP %d for %s, checking whether it arrived ..." % (host.name, repo, exc.status, path.name))
        deadline = time.time() + PROXY_WAIT
        while time.time() < deadline:
            time.sleep(15)
            current = host.api("GET", "%s/releases/%s" % (owner_path(host, repo), release["id"]))
            if any(a.get("name") == path.name and a.get("size") == size for a in current.get("assets") or []):
                print("  %s: %s arrived after all" % (host.name, path.name))
                return current
        raise repos.ScriptError("%s: %s did not arrive (HTTP %d; the proxy in front of the host may limit "
                                "upload time or size)" % (host.name, path.name, exc.status)) from None


def remote_head(host, mod, branch):
    result = repos.run_git(mod.path, "ls-remote", host.git_url(mod.repo), "refs/heads/" + branch)
    if result.returncode != 0:
        raise repos.ScriptError("%s: ls-remote %s: %s" % (host.name, mod.repo, sanitize(host, result.stderr).strip()))
    line = result.stdout.strip().split("\n")[0] if result.stdout.strip() else ""
    return line.split()[0] if line else None


def push_state(host, mod, branch, local):
    """ok / push (behind or no branch) / ahead! / diverged!"""
    remote = remote_head(host, mod, branch)
    if remote is None:
        return "push"
    if remote == local:
        return "ok"
    if repos.run_git(mod.path, "cat-file", "-e", remote + "^{commit}").returncode != 0:
        repos.run_git(mod.path, "fetch", "--quiet", host.git_url(mod.repo), branch)
    if repos.run_git(mod.path, "merge-base", "--is-ancestor", remote, local).returncode == 0:
        return "push"
    if repos.run_git(mod.path, "merge-base", "--is-ancestor", local, remote).returncode == 0:
        return "ahead!"
    return "diverged!"


# --------------------------------------------------------------------- catalogs


def catalog_clone(host, out):
    """Up-to-date clone of the host's catalog repository in releases/.catalogs/<host>."""
    path = out / ".catalogs" / host.name
    url = host.git_url(CATALOG_REPO)
    if (path / ".git").is_dir():
        for args in (("remote", "set-url", "origin", url), ("fetch", "--quiet", "origin", CATALOG_BRANCH),
                     ("checkout", "--quiet", "-B", CATALOG_BRANCH, "origin/" + CATALOG_BRANCH),
                     ("reset", "--quiet", "--hard", "origin/" + CATALOG_BRANCH)):
            result = repos.run_git(path, *args)
            if result.returncode != 0:
                raise repos.ScriptError("%s catalog: git %s: %s" % (host.name, args[0], sanitize(host, result.stderr).strip()))
    else:
        path.parent.mkdir(parents=True, exist_ok=True)
        result = repos.run_git(path.parent, "clone", "--quiet", "--branch", CATALOG_BRANCH, url, host.name)
        if result.returncode != 0:
            raise repos.ScriptError("%s catalog: clone: %s" % (host.name, sanitize(host, result.stderr).strip()))
    return path


def read_catalog(path):
    file = path / CATALOG_FILE
    if not file.is_file():
        return {"mods": []}
    return json.loads(file.read_text(encoding="utf-8-sig"))


def same_archive(a, b):
    keys = ("version", "size_bytes", "archive_sha256", "install_sha256", "urls")
    return a is not None and b is not None and all(a.get(k) == b.get(k) for k in keys)


def merged_entry(local, remotes):
    """The local entry plus the older-version downloads the online catalogs list."""
    entry = json.loads(json.dumps(local))
    by_version = {d["version"]: d for d in entry["downloads"]}
    for remote in remotes:
        for download in (remote or {}).get("downloads") or []:
            version = download.get("version")
            if version not in by_version:
                by_version[version] = json.loads(json.dumps(download))
                entry["downloads"].append(by_version[version])
            elif by_version[version] is not entry["downloads"][0]:
                urls = by_version[version].setdefault("urls", [])
                urls.extend(u for u in download.get("urls") or [] if u not in urls)
    return entry


def commit_identity():
    name = repos.run_git(ROOT, "config", "user.name").stdout.strip()
    email = repos.run_git(ROOT, "config", "user.email").stdout.strip()
    return ["-c", "user.name=" + (name or "cthangover"), "-c", "user.email=" + (email or "noreply@cthangover.ru")]


def publish_catalog(host, path, document, summary):
    builder.write_json(path / CATALOG_FILE, document)
    for step, args in (("add", ("add", CATALOG_FILE)),
                       ("commit", (*commit_identity(), "commit", "--quiet", "-m", summary)),
                       ("push", ("push", "--quiet", "origin", CATALOG_BRANCH))):
        result = repos.run_git(path, *args)
        if result.returncode != 0:
            raise repos.ScriptError("%s catalog: git %s: %s" % (host.name, step,
                                                               sanitize(host, (result.stderr or result.stdout)).strip()))


# ----------------------------------------------------------------------- state


class ModState:
    def __init__(self, folder, entry, archive):
        self.mod = repos.Mod(folder.name, folder)
        self.entry = entry
        self.archive = archive
        self.version = entry["version"]
        self.tag = "v" + self.version
        self.download = entry["downloads"][0]
        self.branch = repos.git_branch(self.mod)
        self.head = None
        if self.branch and repos.git_has_commits(self.mod):
            self.head = repos.run_git(folder, "rev-parse", "HEAD").stdout.strip()
        self.dirty = repos.git_uncommitted(self.mod)
        self.hosts = {}       # host name -> {"repo", "push", "release", "catalog"} states
        self.releases = {}    # host name -> release JSON (or None)
        self.published = {}   # host name -> True once the host serves this archive
        self.newer = {}       # host name -> a later version the host already has

    @property
    def blocked(self):
        if self.head is None:
            return "not a git repository with commits"
        if self.dirty:
            return "%d uncommitted change(s): commit first" % self.dirty
        return None


def asset_state(host, state, release, catalog_entry):
    if release is None:
        return "release"
    assets = mod_assets(release, state.mod.repo)
    asset = next((a for a in assets if a.get("name") == state.archive.name), None)
    if asset is None:
        return "release" if not assets else "replace"
    if len(assets) > 1 or release.get("draft"):
        return "replace"
    if asset.get("size") != state.download["size_bytes"]:
        return "replace"
    digest = asset.get("digest")
    if digest:
        return "ok" if digest == "sha256:" + state.download["archive_sha256"] else "replace"
    # Gitea gives no digest: trust the archive this host's catalog says it serves.
    listed = next((d for d in (catalog_entry or {}).get("downloads") or [] if d.get("version") == state.version), None)
    return "ok" if listed and listed.get("archive_sha256") == state.download["archive_sha256"] else "replace"


def survey(hosts, states, online, catalogs):
    for state in states:
        for host in hosts:
            names = online[host.name]
            cells = {"repo": "ok" if state.mod.repo in names else "repo", "push": "-", "release": "-"}
            remote_entry = next((m for m in catalogs[host.name].get("mods") or [] if m.get("id") == state.mod.mod_id), None)
            listed = next((d for d in (remote_entry or {}).get("downloads") or [] if d.get("version") == state.version), None)
            cells["catalog"] = "ok" if same_archive(listed, state.download) else "catalog"
            if cells["repo"] == "ok" and state.head:
                cells["push"] = push_state(host, state.mod, state.branch, state.head)
                releases = list_releases(host, state.mod.repo)
                release = release_of(releases, state.version)
                state.releases[host.name] = release
                newest = max([r.get("tag_name") for r in releases] + [(remote_entry or {}).get("version")],
                             key=version_key)
                if version_key(newest) > version_key(state.version):
                    # The host already serves a later version: never publish an older one over it.
                    cells["release"] = "older!"
                    cells["catalog"] = "-"
                    state.newer[host.name] = newest.lstrip("vV")
                else:
                    cells["release"] = asset_state(host, state, release, remote_entry)
                    if cells["release"] == "ok" and not same_notes(release, mod_notes(state)):
                        cells["release"] = "notes"
            elif cells["repo"] != "ok":
                cells["push"] = "push" if state.head else "-"
                cells["release"] = "release"
            state.hosts[host.name] = cells
            state.published[host.name] = cells["release"] in ("ok", "notes")


def needs(cells):
    return [v for k, v in cells.items() if v not in ("ok", "-")]


def report(hosts, states, catalogs, local_ids):
    print()
    rows = [s for s in states if s.blocked or any(needs(s.hosts[h.name]) for h in hosts)]
    width = max([len("mod")] + [len(s.mod.mod_id) for s in rows])
    if rows:
        print("Differences (%d of %d mods):" % (len(rows), len(states)))
        print("  %-*s  %-8s  %s" % (width, "mod", "version", "  ".join("%-30s" % h.name for h in hosts)))
        for state in rows:
            cells = ["%-30s" % (", ".join(needs(state.hosts[h.name])) or "ok") for h in hosts]
            line = "  %-*s  %-8s  %s" % (width, state.mod.mod_id, state.version, "  ".join(cells))
            if state.blocked:
                line += "  [%s]" % state.blocked
            print(line.rstrip())
    else:
        print("All %d mods are in sync on %s." % (len(states), " and ".join(h.name for h in hosts)))

    for host in hosts:
        ids = [m.get("id") for m in catalogs[host.name].get("mods") or []]
        extra = sorted(i for i in ids if i not in local_ids)
        if extra:
            print("%s catalog lists mods without a local folder (dropped on update): %s" % (host.name, ", ".join(extra)))
    return rows


# ------------------------------------------------------------------------ apply


class GitHubPacer:
    """At most `batch` new GitHub repositories, then a pause of `minutes`."""

    def __init__(self, batch, minutes):
        self.batch = batch
        self.minutes = minutes
        self.created = 0

    def wait(self, reason):
        until = datetime.now() + timedelta(minutes=self.minutes)
        print("  %s: waiting %d min, until %s (Ctrl+C stops; a new run continues)"
              % (reason, self.minutes, until.strftime("%H:%M")))
        sys.stdout.flush()
        time.sleep(self.minutes * 60)
        self.created = 0

    def create(self, host, name, branch):
        if host.name != "github":
            return host.create_repo(name, branch)
        if self.created >= self.batch:
            self.wait("GitHub allows about %d new repositories at a time" % self.batch)
        for _ in range(3):
            try:
                url = host.create_repo(name, branch)
                if url is not None:
                    self.created += 1
                return url
            except repos.ApiError as exc:
                if not is_rate_limit(exc):
                    raise
                self.wait("GitHub rate limit")
        raise Pending("GitHub still limits repository creation")


def apply_mod(host, state, pacer):
    cells = state.hosts[host.name]
    if state.blocked:
        return
    if cells["repo"] == "repo":
        url = pacer.create(host, state.mod.repo, state.branch)
        print("  %s: created %s" % (host.name, url or state.mod.repo))
        cells["repo"] = "ok"
    if cells["push"] == "push":
        repos.push_mod(host, state.mod, state.branch)
        print("  %s: pushed %s %s" % (host.name, state.mod.repo, state.branch))
        cells["push"] = "ok"
    if cells["push"] in ("ahead!", "diverged!"):
        print("  %s: %s is %s - not pushed, not released" % (host.name, state.mod.repo, cells["push"].rstrip("!")))
        return
    if cells["release"] == "older!":
        print("  %s: %s has %s, newer than local %s - not released" % (host.name, state.mod.repo,
                                                                     state.newer[host.name], state.version))
        return
    if cells["release"] in ("release", "replace"):
        # One release per version: an existing one gets its archive replaced, never a second release.
        release = state.releases.get(host.name)
        if release is None:
            release = release_of(list_releases(host, state.mod.repo), state.version)
        if release is None:
            release = create_release(host, state.mod.repo, state.tag, state.branch, mod_title(state), mod_notes(state))
            print("  %s: release %s %s created" % (host.name, state.mod.repo, state.tag))
        else:
            release = update_notes(host, state.mod.repo, release, mod_title(state), mod_notes(state))
        for asset in mod_assets(release, state.mod.repo):
            delete_asset(host, state.mod.repo, release, asset)
            if asset.get("name") != state.archive.name:
                print("  %s: %s %s: removed the copy %s" % (host.name, state.mod.repo, state.tag, asset.get("name")))
        upload_checked(host, state.mod.repo, release, state.archive)
        print("  %s: %s %s <- %s (%d bytes)" % (host.name, state.mod.repo, state.tag, state.archive.name,
                                                state.download["size_bytes"]))
        cells["release"] = "ok"
    if cells["release"] == "notes":
        update_notes(host, state.mod.repo, state.releases[host.name], mod_title(state), mod_notes(state))
        print("  %s: %s %s: release text updated" % (host.name, state.mod.repo, state.tag))
        cells["release"] = "ok"
    state.published[host.name] = cells["release"] == "ok"


def apply_all(hosts, states, pacer):
    failed = 0
    for state in states:
        for host in hosts:
            if not needs(state.hosts[host.name]) or state.blocked:
                continue
            try:
                try:
                    apply_mod(host, state, pacer)
                except repos.ApiError as exc:
                    if not is_rate_limit(exc):
                        raise
                    pacer.wait("%s rate limit" % host.name)
                    apply_mod(host, state, pacer)
            except (repos.ScriptError, Pending) as exc:
                print("  %s: error %s: %s" % (host.name, state.mod.repo, sanitize(host, str(exc))))
                failed += 1
    return failed


def update_catalogs(hosts, states, catalogs, clones, local_ids, apply):
    """Write the merged catalog to every host whose catalog differs; returns the failures."""
    by_id = {s.mod.mod_id: s for s in states}
    remote_entries = {h.name: {m.get("id"): m for m in catalogs[h.name].get("mods") or []} for h in hosts}
    entries = {}
    for mod_id in sorted(local_ids):
        olds = [remote_entries[h.name].get(mod_id) for h in hosts]
        state = by_id.get(mod_id)
        if state and all(state.published.get(h.name) for h in hosts):
            entries[mod_id] = merged_entry(state.entry, olds)
        else:
            old = next((o for o in olds if o), None)
            if old:
                entries[mod_id] = old
    collections = next((catalogs[h.name].get("collections") for h in hosts if catalogs[h.name].get("collections")), None)
    document = builder.catalog_document([entries[i] for i in sorted(entries)], collections)

    failed = 0
    for host in hosts:
        current = catalogs[host.name]
        if current.get("mods") == document["mods"] and current.get("collections") == document.get("collections"):
            print("  %s catalog: up to date (%d mods)" % (host.name, len(document["mods"])))
            continue
        if not apply:
            print("  %s catalog: would be updated (%d mods)" % (host.name, len(document["mods"])))
            continue
        try:
            publish_catalog(host, clones[host.name], document, "Update catalog: %d mods" % len(document["mods"]))
            print("  %s catalog: pushed (%d mods)" % (host.name, len(document["mods"])))
        except repos.ScriptError as exc:
            print("  %s catalog: error: %s" % (host.name, exc))
            failed += 1
    return failed


def confirm(question):
    try:
        answer = input(question + " [y/N] ")
    except EOFError:
        answer = ""
    return answer.strip().lower() in ("y", "yes", "д", "да")


# ------------------------------------------------------------------------- game


GAME_PLATFORMS = ("windows.zip", "linux.tar.gz")
GAME_PACKAGE = re.compile(r"^cthangover-.+-(windows\.zip|linux\.tar\.gz)$")
GAME_RUN = {
    "windows.zip": ("Windows 10/11 (x86_64)", "unpack, run `cthangover.exe`"),
    "linux.tar.gz": ("Linux (x86_64)", "`tar -xzf %s`, then `./cthangover.x86_64` (the +x bit is preserved)"),
}


class GameState:
    """The game's own release: the packages of build_release in releases/, in the repository of this checkout."""

    def __init__(self, out):
        text = (ROOT / "project.godot").read_text(encoding="utf-8-sig")
        match = re.search(r'^config/version="([^"]+)"', text, re.M)
        if not match:
            raise repos.ScriptError("project.godot has no config/version")
        self.version = match.group(1)
        self.tag = "v" + self.version
        origin = repos.remote_url(ROOT, "origin")
        self.repo = urllib.parse.urlsplit(origin).path.rstrip("/").rsplit("/", 1)[-1].removesuffix(".git") or "cthangover"
        self.mod = types.SimpleNamespace(mod_id="game", path=ROOT, repo=self.repo)
        self.branch = repos.git_branch(self.mod) or "main"
        self.head = repos.run_git(ROOT, "rev-parse", "HEAD").stdout.strip()
        self.packages = [out / ("cthangover-%s-%s" % (self.version, p)) for p in GAME_PLATFORMS]
        self.packages = [p for p in self.packages if p.is_file()]
        self.hashes = {p.name: builder.file_sha256(p) for p in self.packages}
        self.hosts = {}
        self.releases = {}
        self.newer = {}

    @property
    def title(self):
        return "Cthangover " + self.version

    def body(self):
        rows = []
        for path in self.packages:
            platform, run = GAME_RUN[next(p for p in GAME_PLATFORMS if path.name.endswith(p))]
            rows.append("| %s | `%s` | %s |" % (platform, path.name, run.replace("%s", path.name)))
        files = " and ".join("`%s`" % p.name for p in self.packages)
        return "\n\n".join([
            "# " + self.title,
            "Portable builds — unpack and run. Mods, collections and config ship next to the executable.",
            "| Platform | File | How to run |\n|---|---|---|\n" + "\n".join(rows),
            sha_block(self.hashes),
            SOURCE_NOTE % files,
        ]) + "\n"


def game_survey(hosts, game):
    for host in hosts:
        cells = {"push": push_state(host, game.mod, game.branch, game.head), "release": "ok"}
        releases = list_releases(host, game.repo)
        release = release_of(releases, game.version)
        game.releases[host.name] = release
        newest = max([r.get("tag_name") for r in releases], key=version_key, default=None)
        if newest and version_key(newest) > version_key(game.version):
            cells["release"] = "older!"
            game.newer[host.name] = newest.lstrip("vV")
        elif release is None:
            cells["release"] = "release"
        else:
            listed = listed_hashes(release)
            assets = [a for a in release.get("assets") or [] if GAME_PACKAGE.match(a.get("name", ""))]
            names = {a.get("name") for a in assets}
            for path in game.packages:
                asset = next((a for a in assets if a.get("name") == path.name), None)
                digest = (asset or {}).get("digest")
                if asset is None or asset.get("size") != path.stat().st_size \
                        or (digest and digest != "sha256:" + game.hashes[path.name]) \
                        or (not digest and listed.get(path.name) != game.hashes[path.name]):
                    cells["release"] = "replace"
            if names - {p.name for p in game.packages} or release.get("draft"):
                cells["release"] = "replace"
            if cells["release"] == "ok" and not same_notes(release, game.body()):
                cells["release"] = "notes"
        game.hosts[host.name] = cells


def game_report(hosts, game):
    print()
    if not game.packages:
        print("Game %s: no packages cthangover-%s-*.{zip,tar.gz} in releases/ - run build_release first."
              % (game.version, game.version))
        return False
    cells = []
    for host in hosts:
        c = game.hosts[host.name]
        todo = [v for v in (c["release"],) if v != "ok"]
        if c["push"] != "ok":
            todo.append("not pushed: " + c["push"].rstrip("!"))
        cells.append("%s: %s" % (host.name, ", ".join(todo) or "ok"))
    print("Game %s %s (%s): %s" % (game.repo, game.tag, ", ".join(p.name for p in game.packages), "; ".join(cells)))
    if any(game.hosts[h.name]["push"] != "ok" for h in hosts):
        print("  The release tag is made on the host's %s; this checkout's commits that are not there stay out "
              "of it (the main repository is never pushed by this script)." % game.branch)
    return any(game.hosts[h.name]["release"] in ("release", "replace", "notes") for h in hosts)


def game_apply(host, game):
    cells = game.hosts[host.name]
    if cells["release"] == "older!":
        print("  %s: %s has %s, newer than local %s - not released" % (host.name, game.repo,
                                                                     game.newer[host.name], game.version))
        return
    if cells["release"] == "notes":
        update_notes(host, game.repo, game.releases[host.name], game.title, game.body())
        print("  %s: %s %s: release text updated" % (host.name, game.repo, game.tag))
        cells["release"] = "ok"
        return
    if cells["release"] not in ("release", "replace"):
        return
    release = game.releases.get(host.name)
    if release is None:
        release = create_release(host, game.repo, game.tag, game.branch, game.title, game.body())
        print("  %s: release %s %s created" % (host.name, game.repo, game.tag))
    for asset in release.get("assets") or []:
        if GAME_PACKAGE.match(asset.get("name", "")):
            delete_asset(host, game.repo, release, asset)
    for path in game.packages:
        upload_checked(host, game.repo, release, path)
        print("  %s: %s %s <- %s (%d bytes)" % (host.name, game.repo, game.tag, path.name, path.stat().st_size))
    update_notes(host, game.repo, release, game.title, game.body())
    cells["release"] = "ok"


# ------------------------------------------------------------------------- main


def main(argv=None):
    parser = argparse.ArgumentParser(description="Sync the mods with Gitea and GitHub: repositories, code, "
                                                 "releases and catalogs.")
    parser.add_argument("--yes", "-y", action="store_true", help="apply without asking")
    parser.add_argument("--only", help="comma-separated mod ids to sync")
    parser.add_argument("--skip", help="comma-separated mod ids to leave out")
    parser.add_argument("--delete", action="store_true",
                        help="delete online mod_* repositories without a local mod (asks per mod)")
    parser.add_argument("--out", default=str(ROOT / "releases"), help="archive folder (default: releases/)")
    parser.add_argument("--no-game", action="store_true", help="leave the game's own release alone")
    parser.add_argument("--github-batch", type=int, default=8, help="new GitHub repositories per batch (default 8)")
    parser.add_argument("--github-pause", type=int, default=30, help="minutes between GitHub batches (default 30)")
    parser.add_argument("--hosts", help="gitea,github (default: both; the order of the download mirrors)")
    parser.add_argument("--gitea-url")
    parser.add_argument("--gitea-owner")
    parser.add_argument("--gitea-token")
    parser.add_argument("--github-owner")
    parser.add_argument("--github-token")
    args = parser.parse_args(argv)

    hosts = repos.discover_hosts(ROOT, args)
    out = Path(args.out).resolve()
    folders = builder.select_mods(set(builder.split_list(args.only)), set(builder.split_list(args.skip)))
    if not folders:
        raise repos.ScriptError("no mods to sync")
    local_ids = {p.name for p in (ROOT / "mods").iterdir() if p.is_dir() and (p / "manifest.json").is_file()}

    entries = builder.build_all(out, folders, [h.name for h in hosts])
    states = [ModState(folder, entry, builder.archive_path(out, entry)) for folder, entry in zip(folders, entries)]

    print()
    print("Reading the hosts ...")
    online = {}
    for host in hosts:
        online[host.name] = {n for n in host.list_repos() if n.startswith(repos.REPO_PREFIX)}
    clones = {h.name: catalog_clone(h, out) for h in hosts}
    catalogs = {name: read_catalog(path) for name, path in clones.items()}
    survey(hosts, states, online, catalogs)
    rows = report(hosts, states, catalogs, local_ids)
    stale = repos.stale_by_host(hosts, online, repos.local_names(ROOT, [s.mod for s in states]))
    stale_names = sorted({n for h in hosts for n in stale.get(h.name) or []})
    if stale_names:
        print("Online repositories without a local mod: %s%s" % (", ".join(stale_names),
                                                                 "" if args.delete else " (--delete removes them)"))

    game = None if args.no_game else GameState(out)
    game_todo = False
    if game:
        game_survey(hosts, game)
        game_todo = game_report(hosts, game)

    todo = [s for s in rows if not s.blocked]
    print()
    print("Catalogs:")
    update_catalogs(hosts, states, catalogs, clones, local_ids, apply=False)
    catalog_diff = any(s.hosts[h.name]["catalog"] != "ok" for s in states for h in hosts) or \
        any(m.get("id") not in local_ids for h in hosts for m in catalogs[h.name].get("mods") or [])

    code = 1 if any(s.blocked for s in rows) else 0
    if todo or catalog_diff or game_todo:
        print()
        if not (args.yes or confirm("Apply: create, push, release and update the catalogs?")):
            print("Nothing changed.")
            return 1
        print()
        failed = apply_all(hosts, states, GitHubPacer(args.github_batch, args.github_pause))
        if game_todo:
            for host in hosts:
                try:
                    game_apply(host, game)
                except repos.ScriptError as exc:
                    print("  %s: error %s: %s" % (host.name, game.repo, sanitize(host, str(exc))))
                    failed += 1
        print()
        print("Catalogs:")
        failed += update_catalogs(hosts, states, catalogs, clones, local_ids, apply=True)
        code = max(code, 1 if failed else 0)
        print()
        print("Done%s." % (", %d error(s)" % failed if failed else ""))

    if args.delete and stale_names:
        print()
        code = max(code, repos.delete_stale(hosts, stale))
    return code


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (repos.ScriptError, builder.ScriptError) as error:
        print("error: %s" % error, file=sys.stderr)
        sys.exit(1)
    except KeyboardInterrupt:
        print("\nStopped; run again to continue.", file=sys.stderr)
        sys.exit(1)
