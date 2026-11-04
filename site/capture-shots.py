"""Capture the site's real gameplay in a private project and player profile.

Run on the Windows host via site/make-shots.ps1. Raw PNGs, routes, logs and
the physical project copy stay in --output, outside the game repository.
The project renderer, HDR and operating-system display settings are preserved.
"""
import argparse
import datetime
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess

ROUND_OBSERVER = '''extends Node
var elapsed := 0.0
var states: Array = []
var last_round := 0

func _process(delta: float) -> void:
    elapsed += delta
    if elapsed < 0.25:
        return
    elapsed = 0.0
    for label in get_tree().root.find_children("RoundValue", "Label", true, false):
        if label.is_visible_in_tree() and label.text.is_valid_int():
            var round_number := int(label.text)
            if round_number != last_round:
                last_round = round_number
                states.append(round_number)
                var health: Dictionary = {}
                for value in get_tree().root.find_children("*", "Label", true, false):
                    var location := str(value.get_path())
                    if value.is_visible_in_tree() and ("HpChip" in location or "HpValue" in location):
                        health[location] = value.text
                var file := FileAccess.open(OS.get_environment("SITE_CAPTURE_EVIDENCE"), FileAccess.WRITE)
                file.store_string(JSON.stringify({"observed_round":round_number, "rounds":states, "health":health}))
'''


def checked(command, cwd, log, env=None, timeout=300):
    with log.open('w', encoding='utf-8', newline='') as output:
        result = subprocess.run(command, cwd=cwd, env=env, stdout=output,
                                stderr=subprocess.STDOUT, timeout=timeout)
    text = log.read_text(encoding='utf-8-sig')
    if result.returncode:
        raise RuntimeError(f'{log.name}: exit {result.returncode}\n{text[-2500:]}')
    return text


def prepare(source, archive):
    project = archive / 'project'
    if project.exists():
        return project
    checked(['dotnet', 'build', 'Cthangover.csproj', '--no-restore'], source,
            archive / 'build.log')
    project.mkdir()
    ignored = shutil.ignore_patterns('.git', 'obj', '__pycache__', 'node_modules')
    for name in ('Core', 'mods', 'collections', 'config', 'scenes'):
        shutil.copytree(source / name, project / name, ignore=ignored)
    integration = 'tests/Cthangover.Tests.Integration'
    shutil.copytree(source / integration, project / integration, ignore=ignored)
    for file in source.iterdir():
        if file.is_file() and (file.suffix in ('.godot', '.csproj', '.json', '.cfg') or file.name.endswith('.uid')):
            if file.name != 'override.cfg':
                shutil.copy2(file, project / file.name)
    for relative in ('.godot/mono/temp/bin/Debug', '.godot/imported'):
        if (source / relative).exists():
            shutil.copytree(source / relative, project / relative)
    return project


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, default=Path.cwd())
    parser.add_argument('--output', type=Path)
    parser.add_argument('--plan', type=Path)
    parser.add_argument('--only', action='append')
    args = parser.parse_args()
    source = args.source.resolve()
    archive = (args.output or source.parent / 'art_sources/cthangover/site/documentation' /
               datetime.datetime.now(datetime.timezone.utc).strftime('capture_%Y%m%dT%H%M%SZ')).resolve()
    archive.mkdir(parents=True, exist_ok=True)
    plan_path = args.plan or source / 'site/capture-plan.json'
    plan = json.loads(plan_path.read_text(encoding='utf-8-sig'))
    runs = [run for run in plan['runs'] if not args.only or run['id'] in args.only]
    if not runs:
        raise RuntimeError('No capture runs selected')
    project = prepare(source, archive)
    godot = os.environ.get('GODOT_BIN') or shutil.which('godot.exe') or shutil.which('godot')
    if not godot:
        raise RuntimeError('Godot executable not found')
    result_path = archive / 'capture-results.json'
    results = json.loads(result_path.read_text()) if result_path.exists() else []
    for run in runs:
        name = run['id']
        user_relative = 'cthangover-site-gallery/' + archive.name + '/' + name
        user_dir = Path(os.environ['APPDATA']) / user_relative
        user_dir.mkdir(parents=True, exist_ok=True)
        (user_dir / 'settings.json').write_text(json.dumps({'Language':run.get('language', 'en')}), encoding='utf-8')
        overrides = '[application]\nconfig/use_custom_user_dir=true\nconfig/custom_user_dir_name="' + user_relative + '"\n'
        if 'minimum_round' in run:
            (project / 'site_capture_evidence.gd').write_text(ROUND_OBSERVER, encoding='utf-8')
            overrides += '\n[autoload]\nSiteCaptureEvidence="*res://site_capture_evidence.gd"\n'
        (project / 'override.cfg').write_text(overrides, encoding='utf-8')
        env = os.environ.copy()
        env.update(CTHANGOVER_TEST_DATA=str(user_dir), CTHANGOVER_TEST_PROJECT=str(project),
                   CTHANGOVER_TEST_CACHE=str(archive / 'cache'))
        evidence = archive / (name + '.rounds.json')
        env['SITE_CAPTURE_EVIDENCE'] = str(evidence)
        if evidence.exists():
            evidence.unlink()
        output = archive / 'captures' / name
        output.mkdir(parents=True, exist_ok=True)
        command = [godot, '--path', str(project), '--windowed', '--resolution', '1920x1024',
                   '--audio-driver', 'Dummy', '--']
        command += run['args'] if 'args' in run else ['--game=' + run['game'], '--new']
        if 'steps' in run:
            route = archive / (name + '.steps')
            route.write_text('\n'.join(run['steps']) + '\n', encoding='utf-8')
            command += ['--itest=doc_shots', '--doc-steps=' + str(route)]
        if 'investigation_prefix' in run:
            definition = json.loads((project / 'mods/junior_reaper_paper_grief/test_suites/investigation/data/flow.json').read_text())
            definition['steps'] = definition['steps'][:run['investigation_prefix']]
            route = archive / (name + '.json')
            route.write_text(json.dumps(definition, ensure_ascii=False, indent=2), encoding='utf-8')
            command += ['--itest=jrp_investigation', '--investigation-steps=' + str(route)]
        command += ['--shots=' + str(output)]
        print('START', name, flush=True)
        log = archive / (name + '.log')
        text = checked(command, project, log, env, run.get('timeout', 330))
        markers = re.findall(r'\[TEST_RESULT\][^\n]+', text)
        if not markers or any(re.search(r'failed=[1-9]', marker) for marker in markers):
            raise RuntimeError(f'{name}: missing or failing terminal result; see {log}')
        diagnostics = [line for line in text.splitlines()
                       if re.search(r'ERROR:|\[ERROR\]|error CS\d|RID.*leak|ObjectDB.*leak|resources still in use', line, re.I)]
        if diagnostics:
            raise RuntimeError(f'{name}: engine diagnostics\n' + '\n'.join(diagnostics[:12]))
        files = [{'file':str(p.relative_to(archive)), 'sha256':hashlib.sha256(p.read_bytes()).hexdigest()}
                 for p in sorted(output.glob('*.png'))]
        if not files:
            raise RuntimeError(f'{name}: no frames saved')
        battle = None
        if 'minimum_round' in run:
            battle = json.loads(evidence.read_text())
            if battle['observed_round'] < run['minimum_round'] or battle['rounds'][:4] != [1, 2, 3, 4]:
                raise RuntimeError(f'{name}: capture rejected, round {battle["observed_round"]} is below {run["minimum_round"]}')
        results = [entry for entry in results if entry['id'] != name]
        results.append({'id':name, 'command':command, 'results':markers, 'files':files})
        if battle:
            results[-1]['battle'] = battle
        result_path.write_text(json.dumps(results, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
        print('PASS', name, len(files), 'frames', markers[-1], flush=True)
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
