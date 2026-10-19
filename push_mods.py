"""Commit and push every mod repository under mods/.

Each mod in mods/<name>/ with its own .git is a separate repository
(remote: git.cthangover.ru/cthangover/mod_<name>). For each of them:

    1. if the working tree has changes: git add -A + git commit
    2. git push -u origin <current branch>

A mod without commits and without changes is skipped. Nothing is ever
force-pushed: a rejected push (the remote has commits the local repository
lacks) is reported, and the remaining mods are still processed.

The access token embedded in the remote URL is masked in all output.

Usage:
    python push_mods.py                          commit with the default message, push
    python push_mods.py -m "Fix dialogs"         commit message
    python push_mods.py --only cooking,tools     only these mods
    python push_mods.py --dry-run                show what would be done, change nothing
    python push_mods.py --no-commit              push existing commits only

Exit code: 0 when every mod succeeded, 1 otherwise.
"""

import argparse
import os
import re
import subprocess
import sys

MODS_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "mods")
DEFAULT_MESSAGE = "Update mod"
AUTHOR_NAME = "iisaev"
AUTHOR_EMAIL = "vpcoder@ya.ru"

TOKEN_RE = re.compile(r"(https?://)[^@/\s]+@")


def mask(text):
    return TOKEN_RE.sub(r"\1***@", text or "")


def git(repo, *args):
    """Runs git in repo; returns (exit code, combined output)."""
    proc = subprocess.run(
        ["git", "-c", f"user.name={AUTHOR_NAME}", "-c", f"user.email={AUTHOR_EMAIL}", *args],
        cwd=repo,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
    )
    return proc.returncode, mask((proc.stdout or "") + (proc.stderr or "")).strip()


def find_mod_repos(only):
    repos = []
    for name in sorted(os.listdir(MODS_DIR)):
        path = os.path.join(MODS_DIR, name)
        if not os.path.isdir(path) or not os.path.exists(os.path.join(path, ".git")):
            continue
        if only and name not in only:
            continue
        repos.append((name, path))
    return repos


def process(path, message, dry_run, commit):
    """Returns (ok, short status line)."""
    code, status = git(path, "status", "--porcelain")
    if code != 0:
        return False, f"git status failed:\n{status}"

    has_changes = bool(status)
    has_commits = git(path, "rev-parse", "--verify", "--quiet", "HEAD")[0] == 0
    code, branch = git(path, "symbolic-ref", "--short", "HEAD")
    if code != 0:
        return False, "detached HEAD - check out a branch first"

    if has_changes and commit:
        changed = len(status.splitlines())
        if dry_run:
            print(f"  would commit {changed} change(s): \"{message}\"")
        else:
            code, out = git(path, "add", "-A")
            if code != 0:
                return False, f"git add failed:\n{out}"
            code, out = git(path, "commit", "-m", message)
            if code != 0:
                return False, f"git commit failed:\n{out}"
            print(f"  committed {changed} change(s)")
            has_commits = True
    elif has_changes:
        print("  uncommitted changes left as is (--no-commit)")

    if not has_commits:
        return True, "skipped: no commits yet"

    if dry_run:
        return True, f"would push {branch} -> origin"

    code, out = git(path, "push", "-u", "origin", branch)
    if code != 0:
        return False, f"git push failed:\n{out}"
    return True, f"pushed {branch}"


def main():
    parser = argparse.ArgumentParser(description="Commit and push every mod repository under mods/.")
    parser.add_argument("-m", "--message", default=DEFAULT_MESSAGE, help="commit message")
    parser.add_argument("--only", default="", help="comma-separated mod folder names")
    parser.add_argument("--dry-run", action="store_true", help="show what would be done")
    parser.add_argument("--no-commit", action="store_true", help="do not commit, push existing commits only")
    args = parser.parse_args()

    only = {n.strip() for n in args.only.split(",") if n.strip()}
    repos = find_mod_repos(only)
    if not repos:
        print(f"No mod repositories found in {MODS_DIR}")
        return 1

    failed = []
    for name, path in repos:
        print(f"[{name}]")
        try:
            ok, line = process(path, args.message, args.dry_run, not args.no_commit)
        except FileNotFoundError:
            print("git not found on PATH")
            return 1
        print(f"  {line}")
        if not ok:
            failed.append(name)

    print()
    print(f"Done: {len(repos) - len(failed)}/{len(repos)} ok")
    if failed:
        print("Failed: " + ", ".join(failed))
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
