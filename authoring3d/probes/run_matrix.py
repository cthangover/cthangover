"""Host-only sequential matrix. Each engine process has private data and a timeout; reports survive crashes.

Run via a watcher PS1. Capability FAIL is a result; missing rows, crashes, compile errors and leak diagnostics fail the job.
"""
import argparse
import json
import hashlib
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import time

CASES = ['model-glb', 'model-gltf-external', 'model-gltf-embedded', 'model-fbx', 'model-fbx-ascii', 'model-obj',
         'image-png', 'image-jpg', 'image-webp', 'image-tga', 'image-bmp', 'image-ktx', 'image-dds', 'image-svg',
         'image-mips-colour', 'image-compress', 'model-glb-worker', 'model-fbx-worker', 'retarget', 'voxel-gi',
         'navigation', 'navigation-worker', 'scene-cache', 'mesh-processing', 'model-large-ship', 'model-large-rig']


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--godot', required=True)
    parser.add_argument('--project', type=Path, required=True)
    parser.add_argument('--fixtures', type=Path, required=True)
    parser.add_argument('--external', type=Path, help='Legacy argument; packed large-model probes need no external directory')
    parser.add_argument('--out', type=Path, required=True)
    parser.add_argument('--runtime', choices=['editor-runtime', 'export-release'], required=True)
    parser.add_argument('--case', action='append', dest='cases')
    parser.add_argument('--timeout', type=int, default=120)
    args = parser.parse_args()
    out = args.out.resolve()
    out.mkdir(parents=True, exist_ok=True)
    source_root = Path(__file__).resolve().parents[2]
    signatures = {str(path.relative_to(source_root)): hashlib.sha256(path.read_bytes()).hexdigest() for path in (source_root / 'mods_private/world3d_base/test_suites/runtime_probe').rglob('*') if path.is_file()}
    report = []
    errors = []
    private = Path(tempfile.mkdtemp(prefix='cthangover-asset3d-probe-'))
    try:
        for case in args.cases or CASES:
            directory = out / case
            directory.mkdir(exist_ok=True)
            row_path = directory / 'row.json'
            if row_path.exists():
                raise RuntimeError('Refusing to overwrite an earlier experiment: ' + str(row_path))
            env = os.environ.copy()
            env['APPDATA'] = str(private / 'roaming')
            env['LOCALAPPDATA'] = str(private / 'local')
            env['XDG_DATA_HOME'] = str(private / 'data')
            if args.runtime == 'export-release':
                env['NUGET_PACKAGES'] = str(private / 'no-nuget')
                env['DOTNET_ROOT'] = str(private / 'no-sdk')
            for key in ['APPDATA', 'LOCALAPPDATA', 'XDG_DATA_HOME']:
                Path(env[key]).mkdir(parents=True, exist_ok=True)
            command = [args.godot]
            if args.runtime == 'editor-runtime':
                command += ['--path', str(args.project.resolve())]
            command += ['--verbose', '--', '--frontend', '--with-mods=world3d_base,proto_land_pack,proto_sea_pack', '--itest=asset3d_probe',
                        '--probe-case=' + case, '--probe-fixtures=' + str(args.fixtures.resolve()),
                        '--probe-output=' + str(row_path),
                        '--probe-runtime=' + args.runtime]
            started = time.monotonic()
            with (directory / 'stdout.log').open('wb') as stream:
                process = subprocess.Popen(command, cwd=private, env=env, stdout=stream, stderr=subprocess.STDOUT)
                try:
                    code = process.wait(timeout=args.timeout)
                except subprocess.TimeoutExpired:
                    subprocess.run(['taskkill', '/PID', str(process.pid), '/T', '/F'], capture_output=True, check=False)
                    process.wait(timeout=15)
                    code = 124
            logs = private / 'roaming' / 'cthangover' / 'logs'
            if logs.exists():
                shutil.copytree(logs, directory / 'game-logs', dirs_exist_ok=True)
            stdout = (directory / 'stdout.log').read_text(encoding='utf-8', errors='replace')
            if not row_path.exists():
                errors.append(case + ': missing row, exit=' + str(code))
                report.append({'case': case, 'runtime': args.runtime, 'status': 'FAIL', 'reason': errors[-1], 'host_seconds': time.monotonic() - started})
            else:
                row = json.loads(row_path.read_text(encoding='utf-8-sig'))
                row['host_seconds'] = time.monotonic() - started
                row['exit_code'] = code
                report.append(row)
                if code != 0:
                    errors.append(case + ': process exit=' + str(code))
                row['native_error_lines'] = [line for line in stdout.splitlines() if line.startswith('ERROR:')]
                row['known_host_overlay_warning'] = 'EOSOverlayVkLayer-Win64.json' in stdout
                if row['status'] == 'OK':
                    unexpected = [line for line in row['native_error_lines'] if not line.startswith('ERROR: GENERAL - Message Id Number: 0 | Message Id Name: Loader Message')]
                    errors.extend(case + ': unexpected ' + line for line in unexpected)
                if '[TEST_RESULT] name=asset3d_probe passed=1 failed=0' not in stdout:
                    errors.append(case + ': missing completion marker')
            for line in stdout.splitlines():
                if any(token in line.lower() for token in ['leaked at exit', 'still in use at exit', 'objectdb instances leaked', 'lack lifecycle binding', 'error cs', 'was leaked', 'were leaked', 'rid allocations', 'leaked instance']):
                    errors.append(case + ': ' + line)
            (out / 'matrix.json').write_text(json.dumps({'runtime': args.runtime, 'rows': report, 'harness_errors': errors, 'suite_sha256': signatures}, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')
            print(json.dumps(report[-1], ensure_ascii=False), flush=True)
            if not row_path.exists():
                break  # compilation/boot failure must be investigated before another experiment
    finally:
        shutil.rmtree(private, ignore_errors=True)
    if errors:
        raise RuntimeError('\n'.join(errors))


if __name__ == '__main__':
    main()
