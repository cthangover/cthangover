"""Host-only terrain contract and direct ancestor-exit verification on explicit physics backends."""
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


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--godot',required=True);parser.add_argument('--out',required=True,type=Path);parser.add_argument('--terrain-source',type=Path);args=parser.parse_args()
    source=Path.cwd();out=args.out.resolve();out.mkdir(parents=True,exist_ok=False);project=out/'project';project.mkdir();mods=snapshot(source,project,extra_mods=['perf_overlay'])
    if args.terrain_source:
        target=project/'mods_private/terrain_base';shutil.rmtree(target);shutil.copytree(args.terrain_source/'mods_private/terrain_base',target,ignore=shutil.ignore_patterns('.git','__pycache__','docs'))
    shutil.copytree(source/'mods/perf_overlay/test_suites/test_monitor',project/'mods/perf_overlay/test_suites/test_monitor')
    signature={str(p.relative_to(project)):hashlib.sha256(p.read_bytes()).hexdigest() for folder in ['mods','mods_private'] for p in (project/folder).rglob('*') if p.is_file()}
    (out/'sources.json').write_text(json.dumps(dict(mods=mods,sha256=signature),indent=2));rows=[]
    for backend,engine in [('DEFAULT','GodotPhysics3D'),('Jolt','Jolt Physics')]:
        (project/'override.cfg').write_text('[physics]\n3d/physics_engine="'+engine+'"\n')
        for case in ['contracts','vegetation_lifetime','location']:
            run=out/(backend+'_'+case);run.mkdir();env=os.environ.copy()
            for key,leaf in [('APPDATA','roaming'),('LOCALAPPDATA','local'),('TMP','temp'),('TEMP','temp')]:env[key]=str(run/leaf);Path(env[key]).mkdir(exist_ok=True)
            env['CTHANGOVER_TEST_CACHE']=str(out/'compile-cache');captures=run/'captures'
            command=[args.godot,'--path',str(project),'--verbose','--resolution','1280x720','--','--frontend','--with-mods=terrain_base,scene3d_base,perf_overlay','--itest=terrain_contracts','--terrain-case='+case,'--terrain-out='+str(captures)]
            started=time.monotonic()
            with (run/'stdout.log').open('wb') as log:
                child=subprocess.Popen(command,cwd=project,env=env,stdout=log,stderr=subprocess.STDOUT)
                while child.poll() is None and time.monotonic()-started<240:
                    if 'error CS' in (run/'stdout.log').read_text(errors='replace'):
                        subprocess.run(['taskkill','/PID',str(child.pid),'/T','/F'],capture_output=True);break
                    time.sleep(.2)
                if child.poll() is None:subprocess.run(['taskkill','/PID',str(child.pid),'/T','/F'],capture_output=True)
                code=child.wait(timeout=15)
            text=(run/'stdout.log').read_text(encoding='utf-8-sig',errors='replace');errors,environment=diagnostics(text)
            for path in sorted((run/'roaming/cthangover/logs').glob('*.log*')):
                content=path.read_text(encoding='utf-8-sig',errors='replace');e,d=diagnostics(content);errors+=e;environment+=d
                if 'errors.log' in path.name:errors += [line for line in content.splitlines() if line.strip()]
            terminal=re.findall(r'\[TEST_RESULT\] name=terrain_acceptance passed=(\d+) failed=(\d+)',text)
            if code or terminal!=[('1','0')]:errors.append('Exit/terminal failure: '+str(terminal))
            data=json.loads((captures/'row.json').read_text()) if (captures/'row.json').exists() else {}
            if data.get('status')!='OK' or data.get('physics')!=engine:errors.append('Missing result or wrong backend')
            if case=='vegetation_lifetime' and data.get('vegetation_lifetime_checks',0)<6:errors.append('Direct ancestor teardown was not exercised')
            row=dict(backend=backend,case=case,exit_code=code,errors=errors,environment_diagnostics=environment,seconds=time.monotonic()-started,measurement=data)
            rows.append(row);(out/'result.json').write_text(json.dumps(rows,indent=2));print(json.dumps(row),flush=True)
            if errors:raise RuntimeError('Terrain repair case failed: '+str(run))


if __name__=='__main__':main()
