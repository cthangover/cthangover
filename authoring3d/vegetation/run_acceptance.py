"""Host-only vegetation verification from a separate physical terrain/editor staging tree."""
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

sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'terrain/stage7a'))
from run_acceptance import snapshot
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'perf_overlay'))
from run_contract import diagnostics
from make_fixture import generate


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--godot',required=True);parser.add_argument('--out',required=True,type=Path);parser.add_argument('--terrain-source',required=True,type=Path)
    parser.add_argument('--matrix',action='store_true');parser.add_argument('--editor',action='store_true');parser.add_argument('--walk',action='store_true');args=parser.parse_args()
    source=Path.cwd();out=args.out.resolve();out.mkdir(parents=True,exist_ok=False);project=out/'project';project.mkdir()
    mods=snapshot(source,project,extra_mods=['terrain_editor','weather_base','sky3d_base','actor3d_base','combat3d_base','perf_overlay'])
    for name in ['terrain_base','terrain_editor']:
        target=project/'mods_private'/name;shutil.rmtree(target)
        shutil.copytree(args.terrain_source/'mods_private'/name,target,ignore=shutil.ignore_patterns('.git','__pycache__','docs'))
    shutil.copytree(source/'mods/perf_overlay/test_suites/test_monitor',project/'mods/perf_overlay/test_suites/test_monitor')
    combat=source/'mods_private/combat3d_base/test_suites/contracts';target=project/'vegetation_combat_source'
    for rel in ['characters3d/independent.json','definitions/actions.json','definitions/animation.json','definitions/controller.json','definitions/library.json','fighters3d/profiles.json','weapons3d/weapons.json']:
        p=target/rel;p.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(combat/rel,p)
    shutil.copytree(combat/'models3d',target/'models3d')
    generate(project,out/'provenance')
    work=project/'mods_private/test_meadow_work';work.mkdir();(work/'manifest.json').write_text(json.dumps(dict(id='test_meadow_work',name='Isolated meadow authoring destination',version='0.1.0',depends=['test_meadow>=0.1.0'])))
    signature={str(p.relative_to(project)):hashlib.sha256(p.read_bytes()).hexdigest() for folder in ['mods','mods_private'] for p in (project/folder).rglob('*') if p.is_file()}
    (out/'sources.json').write_text(json.dumps(dict(mods=mods,terrain_source=str(args.terrain_source.resolve()),sha256=signature),indent=2));rows=[]
    cases=[('DEFAULT','GodotPhysics3D','medium','walk' if args.walk else 'editor' if args.editor else 'crowd')]
    if args.walk and args.editor:cases.append(('DEFAULT','GodotPhysics3D','medium','editor'))
    if args.matrix:
        cases=[('DEFAULT','GodotPhysics3D','medium','crowd')]
        if args.editor:cases.append(('DEFAULT','GodotPhysics3D','medium','editor'))
        cases += [(backend,engine,tier,'crowd') for backend,engine in [('DEFAULT','GodotPhysics3D'),('Jolt','Jolt Physics')] for tier in ['low','medium','high'] if (backend,tier)!=('DEFAULT','medium')]
    for backend,engine,tier,case in cases:
        (project/'override.cfg').write_text('[physics]\n3d/physics_engine="'+engine+'"\n');run=out/(backend+'_'+tier+'_'+case);run.mkdir();captures=run/'captures';env=os.environ.copy()
        for key,leaf in [('APPDATA','roaming'),('LOCALAPPDATA','local'),('TMP','temp'),('TEMP','temp')]:env[key]=str(run/leaf);Path(env[key]).mkdir(exist_ok=True)
        env['CTHANGOVER_TEST_CACHE']=str(out/'compile-cache')
        command=[args.godot,'--path',str(project),'--verbose','--resolution','1280x720','--','--frontend','--with-mods=terrain_editor,scene3d_base,weather_base,sky3d_base,actor3d_base,combat3d_base,perf_overlay,test_meadow,test_meadow_work','--itest=vegetation','--vegetation-case='+case,'--vegetation-quality='+tier,'--vegetation-out='+str(captures)]
        started=time.monotonic()
        with (run/'stdout.log').open('wb') as log:
            child=subprocess.Popen(command,cwd=project,env=env,stdout=log,stderr=subprocess.STDOUT)
            while child.poll() is None and time.monotonic()-started<240:
                if 'error CS' in (run/'stdout.log').read_text(errors='replace'):
                    subprocess.run(['taskkill','/PID',str(child.pid),'/T','/F'],capture_output=True);break
                time.sleep(.25)
            if child.poll() is None:subprocess.run(['taskkill','/PID',str(child.pid),'/T','/F'],capture_output=True)
            code=child.wait(timeout=15)
        text=(run/'stdout.log').read_text(encoding='utf-8-sig',errors='replace');errors,environment=diagnostics(text)
        for path in sorted((run/'roaming/cthangover/logs').glob('*.log*')):
            content=path.read_text(encoding='utf-8-sig',errors='replace');e,d=diagnostics(content);errors+=e;environment+=d
            if 'errors.log' in path.name:errors += [line for line in content.splitlines() if line.strip()]
        terminal=re.findall(r'\[TEST_RESULT\] name=vegetation passed=(\d+) failed=(\d+)',text)
        if code or terminal!=[('1','0')]:errors.append('Exit/terminal failure: '+str(terminal))
        data=json.loads((captures/'row.json').read_text()) if (captures/'row.json').exists() else {}
        if data.get('status')!='OK' or data.get('physics')!=engine:errors.append('Missing result or wrong backend')
        row=dict(backend=backend,quality=tier,case=case,exit_code=code,errors=errors,environment_diagnostics=environment,seconds=time.monotonic()-started,measurement=data)
        rows.append(row);(out/'result.json').write_text(json.dumps(rows,indent=2));print(json.dumps(row),flush=True)
        if errors:raise RuntimeError('Vegetation case failed: '+str(run))


if __name__=='__main__':main()
