"""Host-only scoped monitor contract, including the two shell kinds and colour-preserving capture exclusion."""
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
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'actor3d'))
from capture_window import save_client
from capture_overlay import hidden_overlay


def snapshot(source,target):
    ignore=shutil.ignore_patterns('.git','.git-template','__pycache__','test_suites','tests','docs','bin','obj')
    target.mkdir(parents=True,exist_ok=False)
    for folder in ['Core','scenes','config']:shutil.copytree(source/folder,target/folder,ignore=ignore)
    shutil.copy2(source/'project.godot',target/'project.godot')
    shutil.copytree(source/'.godot/mono/temp/bin/Debug',target/'.godot/mono/temp/bin/Debug')
    for path in list(source.glob('*.csproj'))+[source/'Directory.Build.props',source/'Directory.Build.targets',source/'global.json']:shutil.copy2(path,target/path.name)
    names=set();todo=['perf_overlay','scene3d_base','dialog_base']
    while todo:
        name=todo.pop()
        if name in names:continue
        names.add(name);path=next(p for p in [source/'mods'/name,source/'mods_private'/name] if p.exists());data=json.loads((path/'manifest.json').read_text(encoding='utf-8-sig'))
        shutil.copytree(path,target/path.parent.name/name,ignore=ignore);todo += [re.split('[<>=]',d)[0] for d in data.get('depends',[])]
    shutil.copytree(source/'mods/perf_overlay/test_suites',target/'mods/perf_overlay/test_suites',ignore=shutil.ignore_patterns('__pycache__','.git'))
    folder=target/'collections/frontend';folder.mkdir(parents=True);(folder/'collection.json').write_text(json.dumps(dict(id='frontend',kind='frontend',version='0.1.0',name='Monitor contract',mods=['dialog_base'])))
    return {str(p.relative_to(target)):hashlib.sha256(p.read_bytes()).hexdigest() for path in [target/'Core',target/'mods',target/'mods_private'] for p in path.rglob('*') if p.is_file()}


def diagnostics(text):
    errors=[];environment=[];lines=text.splitlines()
    for i,line in enumerate(lines):
        if any(s in line.lower() for s in ['error cs','leaked at exit','still in use at exit','objectdb instances leaked','rid allocations','leaked instance','lack lifecycle binding']):errors.append(line)
        if line.startswith('ERROR:'):
            if line=='ERROR: GENERAL - Message Id Number: 0 | Message Id Name: Loader Message' and i+1<len(lines) and 'Failed to open JSON file ' in lines[i+1] and 'EOSOverlayVkLayer-Win64.json' in lines[i+1]:environment.append('\n'.join(lines[i:i+5]))
            else:errors.append(line)
    return errors,environment


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--godot',required=True);parser.add_argument('--out',type=Path,required=True);args=parser.parse_args()
    source=Path.cwd();out=args.out.resolve();out.mkdir(parents=True,exist_ok=False);project=out/'project';receipt=snapshot(source,project);(out/'source.json').write_text(json.dumps(receipt,indent=2))
    with (out/'core-build.log').open('wb') as build_log:
        build=subprocess.run(['dotnet','build',str(project/'Cthangover.csproj'),'--nologo','--verbosity','quiet'],cwd=project,stdout=build_log,stderr=subprocess.STDOUT,timeout=180)
    if build.returncode:raise RuntimeError('Isolated current core build failed: '+str(out/'core-build.log'))
    (project/'override.cfg').write_text('[physics]\n3d/physics_engine="GodotPhysics3D"\n')
    env=os.environ.copy()
    for key,leaf in [('APPDATA','roaming'),('LOCALAPPDATA','local'),('TMP','temp'),('TEMP','temp')]:env[key]=str(out/leaf);Path(env[key]).mkdir(exist_ok=True)
    env['CTHANGOVER_TEST_CACHE']=str(out/'compile-cache');captures=out/'captures'
    command=[args.godot,'--path',str(project),'--verbose','--resolution','1280x720','--','--frontend','--itest=perf_overlay_contracts','--perf-out='+str(captures)]
    started=time.monotonic();capture_error=None;compared=False
    with (out/'stdout.log').open('wb') as log:
        process=subprocess.Popen(command,cwd=project,env=env,stdout=log,stderr=subprocess.STDOUT)
        while process.poll() is None and time.monotonic()-started<240:
            if (captures/'display-check.ready').exists() and not compared:
                try:
                    with hidden_overlay(process.pid) as active:
                        if not active:raise RuntimeError('Expected live monitor capture bridge')
                        (captures/'bridge-check.ok').write_text('PID pipe acknowledged a clean rendered frame')
                    save_client(process.pid,captures/'display-client.png')
                except Exception as error:capture_error=str(error)
                finally:compared=True;(captures/'display-check.done').write_text('Host capture finished')
            if 'error CS' in (out/'stdout.log').read_text(errors='replace'):
                subprocess.run(['taskkill','/PID',str(process.pid),'/T','/F'],capture_output=True);break
            time.sleep(.1)
        if process.poll() is None:subprocess.run(['taskkill','/PID',str(process.pid),'/T','/F'],capture_output=True)
        code=process.wait(timeout=15)
    combined=(out/'stdout.log').read_text(encoding='utf-8-sig',errors='replace');errors,environment=diagnostics(combined)
    for path in sorted((out/'roaming/cthangover/logs').glob('*.log*')):
        content=path.read_text(encoding='utf-8-sig',errors='replace');e,d=diagnostics(content);errors+=e;environment+=d
        if 'errors.log' in path.name:errors += [line for line in content.splitlines() if line.strip()]
    terminal=re.findall(r'\[TEST_RESULT\] name=perf_overlay passed=(\d+) failed=(\d+)',combined)
    if code or terminal!=[('1','0')]:errors.append('Exit/terminal failure: '+str(terminal))
    if not compared or capture_error:errors.append('Window capture failed: '+str(capture_error))
    result=dict(exit_code=code,terminal=terminal,errors=errors,environment_diagnostics=environment,seconds=time.monotonic()-started,command=command)
    (out/'result.json').write_text(json.dumps(result,indent=2));print(json.dumps(result),flush=True)
    if errors:raise RuntimeError('Monitor contract failed: '+str(out))


if __name__=='__main__':main()
