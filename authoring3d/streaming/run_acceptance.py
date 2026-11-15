"""Host-only bounded 3D streaming acceptance; physical isolated source snapshot."""
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
from make_fixture import generate


def snapshot(source,target,editor=False,water=False):
    ignore=shutil.ignore_patterns('.git','.git-template','__pycache__','test_suites','tests','docs','bin','obj')
    target.mkdir(parents=True,exist_ok=False)
    for folder in ['Core','scenes','config']:
        shutil.copytree(source/folder,target/folder,ignore=ignore)
    shutil.copy2(source/'project.godot',target/'project.godot')
    shutil.copytree(source/'.godot/mono/temp/bin/Debug',target/'.godot/mono/temp/bin/Debug')
    for f in source.glob('*.csproj'):shutil.copy2(f,target/f.name)
    names=set();pending=['world_stream3d_editor' if editor else 'world_stream3d_base']
    if water:pending.append('water3d_base')
    while pending:
        name=pending.pop()
        if name in names:continue
        names.add(name)
        path=next(p for p in [source/'mods'/name,source/'mods_private'/name] if p.exists())
        manifest=json.loads((path/'manifest.json').read_text(encoding='utf-8-sig'))
        shutil.copytree(path,target/path.parent.name/name,ignore=ignore)
        pending += [re.split('[<>=]',d)[0] for d in manifest.get('depends',[])]
    shutil.copytree(source/'mods_private/world_stream3d_base/test_suites/contracts',target/'mods_private/world_stream3d_base/test_suites/contracts')
    if water:shutil.copytree(source/'mods_private/world_stream3d_base/test_suites/water_checks',target/'mods_private/world_stream3d_base/test_suites/water_checks')
    if editor:shutil.copytree(source/'mods_private/world_stream3d_editor/test_suites/contracts',target/'mods_private/world_stream3d_editor/test_suites/contracts')
    if editor:
        work=target/'mods_private/test_stream_edit_work';work.mkdir()
        (work/'manifest.json').write_text(json.dumps(dict(id='test_stream_edit_work',name='Region editor destination',version='0.1.0',depends=['world_stream3d_editor>=0.1.0','test_stream_island>=0.1.0'])))
    folder=target/'collections/frontend';folder.mkdir(parents=True)
    collection_mods=['world_stream3d_base>=0.1.0','test_stream_island>=0.1.0']
    if water:collection_mods+=['water3d_base>=0.1.0']
    if editor:collection_mods+=['world_stream3d_editor>=0.1.0','test_stream_edit_work>=0.1.0']
    (folder/'collection.json').write_text(json.dumps(dict(id='frontend',kind='frontend',version='0.1.0',name='Continuous world acceptance',mechanics=[dict(id='world',role='engine',mods=collection_mods)])))
    for game,entry in [('test_stream_game','stream_island'),('test_stream_other','stream_empty')]:
        path=target/'collections'/game;path.mkdir(parents=True)
        (path/'collection.json').write_text(json.dumps(dict(id=game,kind='game',version='0.1.0',name=game,entry=dict(scene=entry),mechanics=[dict(id='world',role='story',mods=['test_stream_island>=0.1.0'])])))
    hashes={}
    for name in names:
        folder=next(p for p in [target/'mods'/name,target/'mods_private'/name] if p.exists())
        for f in folder.rglob('*'):
            if f.is_file():hashes[str(f.relative_to(target))]=hashlib.sha256(f.read_bytes()).hexdigest()
    return dict(mods=sorted(names),sha256=hashes)


def diagnostics(text):
    errors=[];environment=[];lines=text.splitlines()
    for i,line in enumerate(lines):
        if any(s in line.lower() for s in ['error cs','leaked at exit','still in use at exit','objectdb instances leaked','rid allocations','leaked instance','lack lifecycle binding']):errors.append(line)
        if line.startswith('ERROR:'):
            if line=='ERROR: GENERAL - Message Id Number: 0 | Message Id Name: Loader Message' and i+1<len(lines) and 'Failed to open JSON file ' in lines[i+1] and 'EOSOverlayVkLayer-Win64.json' in lines[i+1]:environment.append('\n'.join(lines[i:i+5]))
            else:errors.append(line)
    return errors,environment


def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--editor',action='store_true');p.add_argument('--editor-play',action='store_true');p.add_argument('--water',action='store_true');p.add_argument('--reuse-project',type=Path);p.add_argument('--monitor-host',action='store_true');p.add_argument('--godot',required=True);p.add_argument('--out',required=True,type=Path);p.add_argument('--seconds',type=float,default=30);p.add_argument('--backends',nargs='+',choices=['DEFAULT','Jolt'],default=['DEFAULT','Jolt']);a=p.parse_args();a.editor=a.editor or a.editor_play
    source=Path.cwd();out=a.out.resolve();out.mkdir(parents=True,exist_ok=False)
    if a.reuse_project:
        shutil.copytree(a.reuse_project,out/'project',ignore=shutil.ignore_patterns('.git','__pycache__'))
        original=a.reuse_project.parent/'source.json'
        record=json.loads(original.read_text(encoding='utf-8-sig'))
        record['reused_physical_project']=str(a.reuse_project.resolve())
    else:
        record=snapshot(source,out/'project',a.editor,a.water);generate(out/'project',out/'provenance',hole=a.water)
    (out/'source.json').write_text(json.dumps(record,indent=2))
    from stream_capture import save_client
    report=[]
    for backend in a.backends:
        run=out/backend;run.mkdir();env=os.environ.copy()
        for key,leaf in [('APPDATA','roaming'),('LOCALAPPDATA','local'),('TMP','temp'),('TEMP','temp')]:
            env[key]=str(run/leaf);Path(env[key]).mkdir(exist_ok=True)
        env['CTHANGOVER_TEST_CACHE']=str(out/'compile-cache')
        override=out/'project/override.cfg'
        engine='Jolt Physics' if backend=='Jolt' else 'GodotPhysics3D'
        override.write_text('[physics]\n3d/physics_engine="'+engine+'"\n')
        command=[a.godot,'--path',str(out/'project'),'--verbose','--resolution',('1920x1080' if a.editor else '1280x720'),'--',('--game=test_stream_game' if a.editor_play else '--frontend'),'--itest='+('world_stream3d_water' if a.water else 'world_stream3d_editor_contracts' if a.editor else 'world_stream3d_contracts'),'--stream-out='+str(run/'captures'),'--stream-seconds='+str(a.seconds)]
        if a.editor_play:command+=['--new','--stream-editor-play=1']
        started=time.monotonic();compared=False;capture_error=None;monitor_rows=[];monitor_error=None
        with (run/'stdout.log').open('wb') as log:
            child=subprocess.Popen(command,cwd=out/'project',env=env,stdout=log,stderr=subprocess.STDOUT)
            monitor=None
            if a.monitor_host:
                from host_monitor import HostMonitor
                monitor=HostMonitor(child)
            while child.poll() is None and time.monotonic()-started<a.seconds+240:
                if (run/'captures/display-check.ready').exists() and not compared:
                    try:save_client(child.pid,run/'captures/display-client.png')
                    except Exception as error:capture_error=str(error)
                    compared=True
                if monitor:
                    try:monitor_rows.append(monitor.sample())
                    except Exception as error:monitor_error=str(error)
                paths=list((run/'roaming/cthangover/logs').glob('*.log*'))
                compile_failure=any('error CS' in f.read_text(encoding='utf-8-sig',errors='replace') for f in paths)
                if compile_failure:
                    subprocess.run(['taskkill','/PID',str(child.pid),'/T','/F'],capture_output=True);break
                time.sleep(.25)
            if child.poll() is None:subprocess.run(['taskkill','/PID',str(child.pid),'/T','/F'],capture_output=True)
            code=child.wait(timeout=20)
        if a.monitor_host:(run/'host_samples.json').write_text(json.dumps(dict(samples=monitor_rows,error=monitor_error),indent=2))
        text=(run/'stdout.log').read_text(encoding='utf-8',errors='replace');errors,environment=diagnostics(text)
        for log in sorted((run/'roaming/cthangover/logs').glob('*.log*')):
            content=log.read_text(encoding='utf-8-sig',errors='replace');e,d=diagnostics(content);errors+=e;environment+=d
            if 'errors.log' in log.name:errors += [line for line in content.splitlines() if line.strip()]
        if a.monitor_host and (monitor_error or not monitor_rows):errors.append('Host telemetry missing: '+str(monitor_error))
        terminal=re.findall(r'\[TEST_RESULT\] name='+('world_stream3d_water' if a.water else 'world_stream3d_editor' if a.editor else 'world_stream3d')+r' passed=(\d+) failed=(\d+)',text)
        if code or len(terminal)!=1 or terminal[0]!=('1','0'):errors.append('Exit/terminal failure: '+str(terminal))
        measurement=run/'captures/row.json'
        if measurement.exists() and json.loads(measurement.read_text(encoding='utf-8-sig')).get('physics')!=engine:errors.append('Wrong native physics engine')
        if capture_error or not compared:errors.append('Display comparison missing: '+str(capture_error))
        row=dict(backend=backend,exit_code=code,terminal=terminal,seconds=time.monotonic()-started,errors=errors,environment_diagnostics=environment,command=command)
        report.append(row);(out/'result.json').write_text(json.dumps(report,indent=2));print(json.dumps(row),flush=True)
        if errors:raise RuntimeError('World streaming acceptance failed: '+str(run))


if __name__=='__main__':main()
