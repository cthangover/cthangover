"""Read-only source/path naming check. The owner-maintained vocabulary stays outside repositories."""
import argparse
import io
import json
import os
from pathlib import Path
import re
import sys
import time

TERMS_PATH = (Path(__file__).resolve().parents[3]/'art_sources/cthangover/private_tools/forbidden_terms.txt'
    if os.name == 'nt' else Path('/workspace/art_sources/cthangover/private_tools/forbidden_terms.txt'))
# Build products, captures, job logs and repository metadata are not authored source.
EXCLUDED_DIRECTORIES = frozenset({'.git', '.git-template', '.godot', '.idea', '.omo', '.claude', '.dsh',
    '__pycache__', '.pytest_cache', '.mypy_cache', 'node_modules', 'bin', 'obj'})
EXCLUDED_ROOT_DIRECTORIES = frozenset({'gen', 'jobs', 'releases', 'sdk'})
EXCLUDED_PATHS = frozenset({'tests/artifacts'})
SOURCE_SUFFIXES = frozenset({'.cs', '.txt', '.md', '.py', '.json', '.jsonc', '.properties',
    '.tscn', '.tres', '.gd', '.gdshader', '.xml', '.csproj', '.props', '.targets', '.cfg',
    '.godot', '.scenario', '.yaml', '.yml', '.toml', '.ps1', '.sh', '.bat', '.html', '.css', '.js', '.ts'})
ALLOWED_CORE_COMMENT = re.compile(r'\bevent storm\b', re.IGNORECASE)
COMMENT_LINE = re.compile(r'^\s*(?://|/\*|\*)')


def load_terms():
    """No command-line override, fallback vocabulary, or copy of the external list."""
    with TERMS_PATH.open(encoding='utf-8-sig') as source:
        return list(dict.fromkeys(line.strip().casefold() for line in source
            if line.strip() and not line.lstrip().startswith('#')))


def compile_terms(terms):
    if not terms:
        return None
    return re.compile('|'.join(re.escape(term.casefold()) for term in sorted(terms, key=len, reverse=True)))


def finding(path, line, column, kind, match):
    return dict(path=path, line=line, column=column, kind=kind, match=match)


def term_matches(text, pattern):
    folded = text.casefold()
    offsets = None
    if len(folded) != len(text):
        offsets = []
        for index, character in enumerate(text):
            offsets.extend([index] * len(character.casefold()))
    for match in pattern.finditer(folded):
        start = offsets[match.start()] if offsets is not None else match.start()
        end = offsets[match.end()-1]+1 if offsets is not None else match.end()
        yield start, text[start:end]


def line_findings(relative, line, number, pattern):
    # The exception applies to Core C# comment lines, never literals, IDs or other mods.
    if relative.startswith('Core/') and relative.endswith('.cs') and COMMENT_LINE.match(line):
        line = ALLOWED_CORE_COMMENT.sub(mask_comment, line)
    return [finding(relative, number, start+1, 'content', match)
        for start, match in term_matches(line, pattern)]


def mask_comment(match):
    return ' ' * len(match.group())


def scan_file(path, relative, pattern, terms):
    """Stream text; UTF BOMs precede binary detection. Native/binary assets still have their names checked."""
    rows = []
    with path.open('rb') as source:
        prefix = source.read(8192)
        if prefix.startswith((b'\xff\xfe\x00\x00', b'\x00\x00\xfe\xff')):
            encoding = 'utf-32'
        elif prefix.startswith((b'\xff\xfe', b'\xfe\xff')):
            encoding = 'utf-16'
        elif b'\x00' in prefix:
            if path.suffix.lower() in SOURCE_SUFFIXES:
                raise UnicodeError('NUL byte in an authored text file')
            return [], True
        else:
            encoding = 'utf-8-sig'
        source.seek(0)
        try:
            with io.TextIOWrapper(source, encoding=encoding) as text:
                # A literal preflight avoids running a multi-word regex on every geometry/source line.
                # Keep a tail so a forbidden substring cannot hide at a chunk boundary.
                overlap = max(map(len, terms)) - 1
                tail = ""
                candidate = False
                while True:
                    chunk = text.read(262144)
                    if not chunk:
                        break
                    folded = (tail + chunk).casefold()
                    if any(term in folded for term in terms):
                        candidate = True
                        break
                    tail = chunk[-overlap:] if overlap else ""
                if candidate:
                    text.seek(0)
                    for number, line in enumerate(text, 1):
                        rows.extend(line_findings(relative, line, number, pattern))
        except UnicodeError:
            if path.suffix.lower() in SOURCE_SUFFIXES:
                raise
            return [], True
    return rows, False


def linked(path):
    return path.is_symlink() or (hasattr(path, 'is_junction') and path.is_junction())


def scan(root, terms):
    """Check ignored/untracked source too, without git and without excluding active workstreams."""
    root = Path(root).resolve()
    terms = list(dict.fromkeys(term.casefold() for term in terms))
    pattern = compile_terms(terms)
    result = dict(status='passed', root=str(root), terms_file=str(TERMS_PATH),
        findings=[], errors=[], files_scanned=0, paths_scanned=0, binary_files=0,
        symlinks_skipped=0, excluded_directories=sorted(EXCLUDED_DIRECTORIES),
        excluded_root_directories=sorted(EXCLUDED_ROOT_DIRECTORIES), excluded_paths=sorted(EXCLUDED_PATHS))
    if not root.is_dir():
        result['errors'].append(dict(path=str(root), message='Project root is not a directory'))
    elif pattern is not None:
        walk_errors = []
        for directory, directories, files in os.walk(root, followlinks=False, onerror=walk_errors.append):
            parent = Path(directory)
            kept = []
            for name in sorted(directories):
                path = parent/name
                relative = path.relative_to(root).as_posix()
                if name in EXCLUDED_DIRECTORIES or relative in EXCLUDED_PATHS or (parent == root and name in EXCLUDED_ROOT_DIRECTORIES):
                    continue
                result['paths_scanned'] += 1
                for start, match in term_matches(name, pattern):
                    result['findings'].append(finding(relative, 0, start+1, 'path', match))
                if linked(path):
                    result['symlinks_skipped'] += 1
                else:
                    kept.append(name)
            directories[:] = kept
            for name in sorted(files):
                path = parent/name
                relative = path.relative_to(root).as_posix()
                if name in EXCLUDED_DIRECTORIES or path.suffix.lower() == '.log':
                    continue
                result['paths_scanned'] += 1
                for start, match in term_matches(name, pattern):
                    result['findings'].append(finding(relative, 0, start+1, 'path', match))
                if linked(path):
                    result['symlinks_skipped'] += 1
                    continue
                try:
                    rows, binary = scan_file(path, relative, pattern, terms)
                    result['files_scanned'] += 1
                    result['binary_files'] += int(binary)
                    result['findings'].extend(rows)
                except (OSError, UnicodeError) as error:
                    result['errors'].append(dict(path=relative, message=str(error)))
        for error in walk_errors:
            result['errors'].append(dict(path=str(error.filename), message=str(error)))
    if result['errors']:
        result['status'] = 'error'
    elif result['findings']:
        result['status'] = 'failed'
    return result


def run(root):
    started = time.perf_counter()
    try:
        terms = load_terms()
    except FileNotFoundError:
        return dict(status='skipped', warning='External naming vocabulary is missing; check skipped',
            terms_file=str(TERMS_PATH), findings=[], errors=[])
    except (OSError, UnicodeError) as error:
        return dict(status='error', errors=[dict(path=str(TERMS_PATH), message=str(error))], findings=[])
    if not terms:
        return dict(status='skipped', warning='External naming vocabulary is empty; check skipped',
            terms_file=str(TERMS_PATH), findings=[], errors=[])
    result = scan(root, terms)
    result['elapsed_seconds'] = round(time.perf_counter() - started, 3)
    return result


def exit_code(result):
    return 2 if result['status'] == 'error' else 1 if result['status'] == 'failed' else 0


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('project', nargs='?', type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument('--json', action='store_true', help='Write structured results to stdout; no repository output files')
    args = parser.parse_args()
    result = run(args.project)
    if result.get('warning'):
        print('WARNING: '+result['warning']+' ('+str(TERMS_PATH)+')', file=sys.stderr)
    if args.json:
        print(json.dumps(result, ensure_ascii=False, indent=2))
    else:
        for row in result['findings']:
            print(f"{row['path']}:{row['line']}:{row['column']}: {row['kind']}: {row['match']}")
        for error in result['errors']:
            print(f"{error['path']}:0: error: {error['message']}", file=sys.stderr)
        print(f"Naming check: {result['status']}; {len(result['findings'])} finding(s), {len(result['errors'])} error(s)")
    return exit_code(result)


if __name__ == '__main__':
    sys.exit(main())
