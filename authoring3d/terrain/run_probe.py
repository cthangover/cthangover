"""Windows watcher only. Bounded minimal physical project, private data, sequential native processes.
No editor import, no all-mods, no regression profiles. Failed rows and full shutdown logs are evidence.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import time

CASES = ['precision', 'chunks', 'heightmap', 'navigation', 'bare', 'material8', 'triplanar_pbr4', 'grass200k', 'grass500k',
         'shadows_off', 'occlusion', 'bodies_heightmap', 'bodies_box', 'crowd_fixture_high', 'crowd_private_low', 'crowd_private_medium', 'crowd_private_high']


def copy_project(source, dest):
    ignore = shutil.ignore_patterns('.git', '.git-template', 'bin', 'obj', '__pycache__', 'test_suites', 'tests', 'docs')
    for folder in ['Core', 'scenes', 'config']:
        shutil.copytree(source / folder, dest / folder, ignore=ignore)
    shutil.copy2(source / 'project.godot', dest / 'project.godot')
    shutil.copytree(source / '.godot/mono/temp/bin/Debug', dest / '.godot/mono/temp/bin/Debug')
    for path in source.glob('*.csproj'):
        shutil.copy2(path, dest / path.name)
    names = {'test_terrain_probe', 'menu_base'}
    remaining = list(names)
    paths = {}
    while remaining:
        name = remaining.pop()
        path = next(p for p in [source / 'mods' / name, source / 'mods_private' / name] if p.exists())
        paths[name] = path
        for dep in json.loads((path / 'manifest.json').read_text(encoding='utf-8-sig')).get('depends', []):
            dep = re.split(r'[><=]', dep)[0]
            if dep not in names:
                names.add(dep)
                remaining.append(dep)
    for name, path in paths.items():
        shutil.copytree(path, dest / path.parent.name / name, ignore=ignore)
    frontend = dest / 'collections/frontend'
    frontend.mkdir(parents=True)
    (frontend / 'collection.json').write_text(json.dumps({'id': 'frontend', 'kind': 'frontend', 'version': '0.1.0',
        'name': 'Probe shell', 'mods': ['ui_base', 'menu_base']}), encoding='utf-8')
    return sorted(names)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project', required=True, type=Path)
    parser.add_argument('--godot', required=True)
    parser.add_argument('--out', required=True, type=Path)
    parser.add_argument('--physics-engine', choices=['default', 'jolt'], default='default')
    parser.add_argument('--native-only', action='store_true', help='Omit unused rig catalogues in the disposable copy; do not accept adapter cases')
    parser.add_argument('--case', action='append', dest='cases')
    args = parser.parse_args()
    args.out.mkdir(parents=True, exist_ok=False)
    scratch = Path(tempfile.mkdtemp(prefix='cthangover-terrain-probe-'))
    rows, errors = [], []
    signatures = {str(p.relative_to(args.project)): hashlib.sha256(p.read_bytes()).hexdigest()
                  for p in (args.project / 'mods_private/test_terrain_probe').rglob('*') if p.is_file() and '.git' not in p.parts}
    try:
        project = scratch / 'project'
        project.mkdir()
        mods = copy_project(args.project, project)
        omitted_rigs = []
        if args.native_only:
            if any(c.startswith('crowd_') and not c.startswith('crowd_native_') for c in (args.cases or CASES)):
                raise ValueError('native-only isolation cannot run the animation adapter')
            for path in project.glob('mods*/**/rigs3d'):
                omitted_rigs.append(str(path.relative_to(project)))
                shutil.rmtree(path)
        input_hashes = {str(p.relative_to(project)): hashlib.sha256(p.read_bytes()).hexdigest()
                        for name in ['mods', 'mods_private'] for p in (project / name).rglob('*.cs')}
        input_hashes['core_dll'] = hashlib.sha256((project / '.godot/mono/temp/bin/Debug/Cthangover.dll').read_bytes()).hexdigest()
        if args.physics_engine == "jolt":
            (project / "override.cfg").write_text('[physics]\n3d/physics_engine="Jolt Physics"\n', encoding="utf-8")
        (args.out / 'environment.json').write_text(json.dumps({'mods': mods, 'suite_sha256': signatures,
            'cpu': os.environ.get('PROCESSOR_IDENTIFIER'), 'physics_override': args.physics_engine, 'omitted_unused_rig_directories': omitted_rigs, 'input_sha256': input_hashes, 'cases': args.cases or CASES}, indent=2), encoding='utf-8')
        for case in args.cases or CASES:
            output = args.out / case
            output.mkdir()
            env = os.environ.copy()
            env['APPDATA'] = str(scratch / 'roaming')
            env['LOCALAPPDATA'] = str(scratch / 'local')
            # Small persistent compiler cache across these sequential cases, not across unrelated tasks.
            env['CTHANGOVER_TEST_CACHE'] = str(scratch / 'compile-cache')
            env['TMP'] = env['TEMP'] = str(scratch / 'temp')
            for var in ['APPDATA', 'LOCALAPPDATA', 'TMP']:
                Path(env[var]).mkdir(exist_ok=True)
            command = [args.godot, '--path', str(project), '--verbose', '--resolution', '1280x720', '--',
                '--frontend', '--with-mods=test_terrain_probe', '--itest=terrain_probe',
                '--terrain-case=' + case, '--terrain-out=' + str(output)]
            start = time.monotonic()
            with (output / 'stdout.log').open('wb') as log:
                process = subprocess.Popen(command, env=env, stdout=log, stderr=subprocess.STDOUT, cwd=project)
                try:
                    deadline = time.monotonic() + (600 if case.startswith("crowd") else 180)
                    while process.poll() is None and time.monotonic() < deadline:
                        folder = scratch / 'roaming/cthangover/logs'
                        if folder.exists():
                            shutil.copytree(folder, output / 'game-logs', dirs_exist_ok=True)
                            authoritative = folder / 'cthangover.log'
                            if authoritative.exists() and 'error CS' in authoritative.read_text(encoding='utf-8-sig', errors='replace'):
                                subprocess.run(['taskkill', '/PID', str(process.pid), '/T', '/F'], capture_output=True)
                                break
                        time.sleep(1)
                    code = process.wait(timeout=max(1, deadline-time.monotonic()))
                except subprocess.TimeoutExpired:
                    subprocess.run(['taskkill', '/PID', str(process.pid), '/T', '/F'], capture_output=True)
                    process.wait(timeout=20)
                    code = 124
            for folder in [scratch / 'roaming/cthangover/logs', scratch / 'roaming/Godot/app_userdata/cthangover/logs']:
                if folder.exists():
                    shutil.copytree(folder, output / 'game-logs', dirs_exist_ok=True)
            stdout = (output / 'stdout.log').read_text(encoding='utf-8', errors='replace')
            row = json.loads((output / 'row.json').read_text()) if (output / 'row.json').exists() else {'case': case, 'status': 'MISSING'}
            row.update(exit_code=code, host_seconds=time.monotonic()-start, log=str(output / 'stdout.log'))
            problems = []
            if code != 0 or row['status'] != 'OK' or '[TEST_RESULT] name=terrain_probe passed=1 failed=0' not in stdout:
                problems.append('exit/status/marker failure')
            for line in stdout.splitlines():
                lower = line.lower()
                if any(t in lower for t in ['leaked at exit', 'still in use at exit', 'objectdb instances leaked', 'lack lifecycle binding', 'error cs', 'was leaked', 'were leaked', 'rid allocations', 'leaked instance']):
                    problems.append(line)
                if line.startswith('ERROR:') and 'Loader Message' not in line:
                    problems.append(line)
            authoritative = output / 'game-logs/cthangover_errors.log'
            if authoritative.exists():
                row['authoritative_error_lines'] = authoritative.read_text(encoding='utf-8-sig', errors='replace').splitlines()
                unexpected = [line for line in row['authoritative_error_lines'] if 'CollectSceneDefinitions — no scene definitions found' not in line]
                problems.extend(unexpected)
            row['harness_errors'] = problems
            rows.append(row)
            errors.extend(case + ': ' + p for p in problems)
            (args.out / 'matrix.json').write_text(json.dumps({'rows': rows, 'harness_errors': errors}, indent=2), encoding='utf-8')
            print(json.dumps(row), flush=True)
            if problems:
                break  # Investigate before repeating a faulty native fixture.
    finally:
        shutil.rmtree(scratch, ignore_errors=True)
    if errors:
        raise RuntimeError('\n'.join(errors))


if __name__ == '__main__':
    main()
