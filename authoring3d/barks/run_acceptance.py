"""Bounded host-only 3D speech acceptance. No 2D suite, profile or full regression."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import time


def snapshot(source, target):
    ignore = shutil.ignore_patterns('.git', '.git-template', '__pycache__', 'test_suites', 'tests', 'docs', 'bin', 'obj')
    target.mkdir(parents=True, exist_ok=False)
    for folder in ['Core', 'scenes', 'config']:
        shutil.copytree(source/folder, target/folder, ignore=ignore)
    shutil.copy2(source/'project.godot', target/'project.godot')
    shutil.copytree(source/'.godot/mono/temp/bin/Debug', target/'.godot/mono/temp/bin/Debug')
    for f in source.glob('*.csproj'):
        shutil.copy2(f, target/f.name)
    names = set()
    pending = ['barks3d_base']
    while pending:
        name = pending.pop()
        if name in names:
            continue
        names.add(name)
        path = next(p for p in [source/'mods'/name, source/'mods_private'/name] if p.exists())
        manifest = json.loads((path/'manifest.json').read_text(encoding='utf-8-sig'))
        shutil.copytree(path, target/path.parent.name/name, ignore=ignore)
        pending += [re.split('[<>=]', d)[0] for d in manifest.get('depends', [])]
    shutil.copytree(source/'mods_private/barks3d_base/test_suites/contracts', target/'mods_private/barks3d_base/test_suites/contracts', ignore=shutil.ignore_patterns('.git', '__pycache__'))
    folder = target/'collections/frontend'
    folder.mkdir(parents=True)
    (folder/'collection.json').write_text(json.dumps(dict(id='frontend', kind='frontend', version='0.1.0', name='Spatial speech acceptance', mechanics=[dict(id='speech', role='engine', mods=['barks3d_base>=0.1.0'])])))
    own = [source/'mods/npc_barks_base/src', source/'mods_private/barks3d_base', source/'authoring3d/barks']
    hashes = {}
    for folder in own:
        for f in folder.rglob('*'):
            if f.is_file() and '.git' not in f.parts and '__pycache__' not in f.parts:
                hashes[str(f.relative_to(source))] = hashlib.sha256(f.read_bytes()).hexdigest()
    return dict(mods=sorted(names), sha256=hashes)


def native_errors(text):
    """Preserve the known machine overlay diagnostic separately; every other native ERROR remains fatal."""
    errors, environment = [], []
    lines = text.splitlines()
    for index, line in enumerate(lines):
        if any(x in line.lower() for x in ['error cs', 'leaked at exit', 'still in use at exit', 'objectdb instances leaked', 'rid allocations', 'leaked instance', 'quaternion is not normalized', 'lack lifecycle binding']):
            errors.append(line)
        if line.startswith('ERROR:'):
            if line == 'ERROR: GENERAL - Message Id Number: 0 | Message Id Name: Loader Message' and index+1 < len(lines) and 'Failed to open JSON file ' in lines[index+1] and 'EOSOverlayVkLayer-Win64.json' in lines[index+1]:
                environment.append('\n'.join(lines[index:index+5]))
            else:
                errors.append(line)
    return errors, environment


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--godot', required=True)
    parser.add_argument('--out', type=Path, required=True)
    parser.add_argument('--languages', nargs='+', choices=['ru', 'en'], default=['ru', 'en'])
    args = parser.parse_args()
    source = Path.cwd()
    out = args.out.resolve()
    out.mkdir(parents=True, exist_ok=False)
    record = snapshot(source, out/'project')
    (out/'source.json').write_text(json.dumps(record, indent=2))
    # Capture helper is shared by the neighbouring 3D verification component.
    sys.path.insert(0, str(source/'authoring3d/actor3d'))
    from capture_window import save_client
    report = []
    for language in args.languages:
        run = out/language
        run.mkdir()
        env = os.environ.copy()
        for key, leaf in [('APPDATA', 'roaming'), ('LOCALAPPDATA', 'local'), ('TMP', 'temp'), ('TEMP', 'temp')]:
            env[key] = str(run/leaf)
            Path(env[key]).mkdir(exist_ok=True)
        env['BARKS3D_OUTPUT'] = str(run/'captures')
        env['BARKS3D_LANGUAGE'] = language
        env['CTHANGOVER_TEST_CACHE'] = str(out/'compile-cache')
        command = [args.godot, '--path', str(out/'project'), '--verbose', '--resolution', '1280x720', '--', '--frontend', '--itest=barks3d_contracts']
        capture_error = None
        started = time.monotonic()
        with (run/'stdout.log').open('wb') as log:
            child = subprocess.Popen(command, env=env, cwd=out/'project', stdout=log, stderr=subprocess.STDOUT)
            compared = False
            while child.poll() is None and time.monotonic()-started < 180:
                ready = run/'captures/display-check.ready'
                if ready.exists() and not compared:
                    try:
                        save_client(child.pid, run/'captures/display-client.png')
                    except Exception as error:
                        capture_error = str(error)
                    compared = True
                game = run/'roaming/cthangover/logs/cthangover.log'
                if game.exists() and 'error CS' in game.read_text(encoding='utf-8-sig', errors='replace'):
                    subprocess.run(['taskkill', '/PID', str(child.pid), '/T', '/F'], capture_output=True)
                    break
                time.sleep(.15)
            if child.poll() is None:
                subprocess.run(['taskkill', '/PID', str(child.pid), '/T', '/F'], capture_output=True)
            code = child.wait(timeout=20)
        text = (run/'stdout.log').read_text(encoding='utf-8', errors='replace')
        errors, environment = native_errors(text)
        game_log = run/'roaming/cthangover/logs/cthangover.log'
        if game_log.exists():
            game_errors, game_environment = native_errors(game_log.read_text(encoding='utf-8-sig', errors='replace'))
            errors += game_errors
            environment += game_environment
        error_log = run/'roaming/cthangover/logs/cthangover_errors.log'
        if error_log.exists():
            errors += [s for s in error_log.read_text(encoding='utf-8-sig').splitlines() if s.strip()]
        terminal = re.findall(r'\[TEST_RESULT\] name=barks3d passed=(\d+) failed=(\d+)', text)
        if code or len(terminal) != 1 or int(terminal[0][0]) < 1 or terminal[0][1] != '0':
            errors.append('Exit/terminal failure: '+str(terminal))
        if capture_error or not (run/'captures/display-client.png').exists():
            errors.append('Display comparison missing: '+str(capture_error))
        row = dict(language=language, exit_code=code, terminal=terminal, seconds=time.monotonic()-started, errors=errors, environment_diagnostics=environment, command=command)
        report.append(row)
        (out/'result.json').write_text(json.dumps(report, indent=2))
        print(json.dumps(row), flush=True)
        if errors:
            raise RuntimeError('Speech acceptance failed: '+str(run))


if __name__ == '__main__':
    main()
