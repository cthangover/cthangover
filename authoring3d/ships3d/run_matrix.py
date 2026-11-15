"""Host-only ship boarding matrix. Every physics/preset combination uses a disposable minimal physical project; source project settings are never changed."""
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
import sys
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'actor3d'))
from capture_window import save_client


def copy_project(source,dest):
    ignore=shutil.ignore_patterns('.git','.git-template','bin','obj','__pycache__','test_suites','tests','docs')
    for folder in ['Core','scenes','config']:
        shutil.copytree(source/folder,dest/folder,ignore=ignore)
    shutil.copy2(source/'project.godot',dest/'project.godot')
    shutil.copytree(source/'.godot/mono/temp/bin/Debug',dest/'.godot/mono/temp/bin/Debug')
    for path in source.glob('*.csproj'):shutil.copy2(path,dest/path.name)
    remaining=['ships3d_base','combat3d_base','proto_sea_pack','menu_base','dialog_base','water3d_editor'];paths={}
    while remaining:
        name=remaining.pop()
        if name in paths:continue
        path=next(p for p in [source/'mods'/name,source/'mods_private'/name] if p.exists());paths[name]=path
        remaining += [re.split(r'[><=]',x)[0] for x in json.loads((path/'manifest.json').read_text(encoding='utf-8-sig')).get('depends',[])]
    for name,path in paths.items():shutil.copytree(path,dest/path.parent.name/name,ignore=ignore)
    suite=Path('mods_private/combat3d_base/test_suites/contracts')
    shutil.copytree(source/suite,dest/suite,ignore=shutil.ignore_patterns('.git','tests','__pycache__'))
    ship_suite=Path('mods_private/ships3d_base/test_suites/contracts')
    shutil.copytree(source/ship_suite,dest/ship_suite,ignore=shutil.ignore_patterns('.git','tests','__pycache__'))
    frontend=dest/'collections/frontend';frontend.mkdir(parents=True)
    (frontend/'collection.json').write_text(json.dumps({'id':'frontend','kind':'frontend','version':'0.1.0','name':'Ship probe shell','mods':['ui_base','menu_base']}))
    return sorted(paths)


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--godot',required=True);parser.add_argument('--out',required=True,type=Path)
    parser.add_argument('--scenario',choices=['boarding','crowd'],default='boarding')
    parser.add_argument('--physics-engine',choices=['default','jolt'],action='append')
    parser.add_argument('--waves',choices=['low','high'],action='append')
    args=parser.parse_args();source=Path.cwd();out=args.out.resolve();out.mkdir(parents=True,exist_ok=False);report=[]
    original_config=(source/'project.godot').read_bytes();source_signature={str(p.relative_to(source)):hashlib.sha256(p.read_bytes()).hexdigest() for p in (source/'mods_private/ships3d_base').rglob('*.cs') if '.git' not in p.parts}
    with tempfile.TemporaryDirectory(prefix='cthangover-ships-matrix-') as temp:
        scratch=Path(temp);project=scratch/'project';project.mkdir();mods=copy_project(source,project)
        snapshot_signature={}
        for category in ['mods','mods_private']:
            for path in (project/category).rglob('*'):
                if path.is_file() and path.suffix.lower() in ['.cs','.json','.tscn','.glb','.gdshader']:
                    snapshot_signature[str(path.relative_to(project))]=hashlib.sha256(path.read_bytes()).hexdigest()
        (out/'environment.json').write_text(json.dumps({'mods':mods,'source_sha256':source_signature,'snapshot_sources_and_models_sha256':snapshot_signature,'core_sha256':hashlib.sha256((project/'.godot/mono/temp/bin/Debug/Cthangover.dll').read_bytes()).hexdigest(),'cpu':os.environ.get('PROCESSOR_IDENTIFIER'),'project_config_sha256':hashlib.sha256(original_config).hexdigest(),'note':'Only disposable override.cfg selects physics and disables vsync; HDR and renderer remain original.'},indent=2))
        for physics in args.physics_engine or ['default','jolt']:
            # Explicit both sides: no reliance on a global user default or a previous probe.
            (project/'override.cfg').write_text('[physics]\n3d/physics_engine="'+('Jolt Physics' if physics=='jolt' else 'Godot Physics')+'"\n[display]\nwindow/vsync/vsync_mode=0\n[application]\nrun/max_fps=0\n',encoding='utf-8')
            for tier in args.waves or ['low','high']:
                for case in [args.scenario]:
                    name=physics+'_'+tier+'_'+case;directory=out/name;directory.mkdir();captures=directory/'captures';home=scratch/name
                    env=os.environ.copy()
                    for key,part in [('APPDATA','roaming'),('LOCALAPPDATA','local'),('XDG_DATA_HOME','data'),('TEMP','temp'),('TMP','temp')]:env[key]=str(home/part);Path(env[key]).mkdir(parents=True,exist_ok=True)
                    env['CTHANGOVER_TEST_CACHE']=str(scratch/'compile-cache');env['SHIPS3D_SHOTS']=str(captures);env['SHIPS3D_WAVES']=tier;env['SHIPS3D_CASE']=args.scenario;env['SHIPS3D_CAPTURE_ONLY']='false'
                    command=[args.godot,'--path',str(project),'--verbose','--resolution','1280x720','--','--frontend','--with-mods=ships3d_base,combat3d_base,proto_sea_pack','--itest=combat3d_base_contracts','--itest=ships3d_contracts','--ships3d-demo']
                    started=time.monotonic();comparison_error=None
                    with (directory/'stdout.log').open('wb') as log:
                        process=subprocess.Popen(command,cwd=project,env=env,stdout=log,stderr=subprocess.STDOUT);compared=False;comparison_started=None
                        try:
                            while process.poll() is None:
                                if time.monotonic()-started>420:raise subprocess.TimeoutExpired(command,420)
                                if (captures/'display-check.ready').exists() and not compared:
                                    if comparison_started is None:comparison_started=time.monotonic()
                                    try:
                                        window=json.loads((captures/'display-window.json').read_text())
                                        save_client(window['pid'],captures/'display-client.png',window['handle']);comparison_error=None;compared=True
                                        (captures/'display-check.complete').write_text('Captured the unobscured running client')
                                    except Exception as error:
                                        comparison_error=str(error)
                                        if time.monotonic()-comparison_started>=10:compared=True
                                try:process.wait(timeout=.1)
                                except subprocess.TimeoutExpired:pass
                            code=process.returncode
                        except subprocess.TimeoutExpired:
                            subprocess.run(['taskkill','/PID',str(process.pid),'/T','/F'],capture_output=True);process.wait(timeout=20);code=124
                    logs=home/'roaming/cthangover/logs'
                    if logs.exists():shutil.copytree(logs,directory/'game-logs')
                    text=(directory/'stdout.log').read_text(encoding='utf-8-sig',errors='replace');complete=text+'\n'+'\n'.join(p.read_text(encoding='utf-8-sig',errors='replace') for p in (directory/'game-logs').glob('*.log'))
                    errors=[]
                    if code or text.count('[TEST_RESULT] name=ships3d_demo passed=1 failed=0')!=1:errors.append('Exit or terminal marker failed')
                    if comparison_error:errors.append(comparison_error)
                    all_lines=complete.splitlines()
                    for number,line in enumerate(all_lines):
                        if any(t in line.lower() for t in ['error cs','lack lifecycle binding','leaked at exit','still in use at exit','objectdb instances leaked','rid allocations','leaked instance','was leaked','were leaked','ship acceptance failed','unsupported type']):errors.append(line)
                        if line.startswith('ERROR:') and not (line.startswith('ERROR: GENERAL - Message Id Number: 0 | Message Id Name: Loader Message') and number+1<len(all_lines) and 'EOSOverlayVkLayer-Win64.json' in all_lines[number+1]):errors.append(line)
                    row=json.loads((captures/'row.json').read_text()) if (captures/'row.json').exists() else {'status':'MISSING'}
                    row.update(name=name,exit_code=code,host_seconds=time.monotonic()-started,harness_errors=list(dict.fromkeys(errors)),log=str(directory/'stdout.log'))
                    expected='Jolt Physics' if physics=='jolt' else 'Godot Physics'
                    if row.get('physics')!=expected:row['harness_errors'].append('Physics A/B mismatch: '+str(row.get('physics')))
                    if row.get('case')!=args.scenario:row['harness_errors'].append('Scenario mismatch')
                    if args.scenario=='boarding' and not row.get('crossed_support'):row['harness_errors'].append('Physical boarding not verified')
                    if args.scenario=='boarding' and not row.get('boarding_route_complete'):row['harness_errors'].append('Complete route onto the deck not verified')
                    if physics=='jolt' and not row.get('physics_space_class','').startswith('Jolt'):row['harness_errors'].append('Native Jolt server not verified')
                    if physics=='default' and (not row.get('physics_space_class') or 'Jolt' in row['physics_space_class']):row['harness_errors'].append('Native default server not verified')
                    if row.get('fighters')!=50:row['harness_errors'].append('Not 50 actual fighters')
                    if row.get('status')!='OK':row['harness_errors'].append('Incomplete native acceptance')
                    for key in ['wall_ms','main_thread_cpu_ms','render_cpu_ms','gpu_ms','physics_ms']:
                        if row.get(key,{}).get('n',0)<1000 or 'p99' not in row.get(key,{}):row['harness_errors'].append('Incomplete percentiles: '+key)
                    report.append(row);(out/'matrix.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps({k:v for k,v in row.items() if k!='fighter_states'}),flush=True)
                    if row['harness_errors']:raise RuntimeError(name+' failed; inspect '+str(directory))
                    shutil.rmtree(home)
    if (source/'project.godot').read_bytes()!=original_config:raise RuntimeError('Source project configuration changed during probe')


if __name__=='__main__':main()
