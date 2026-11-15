"""Host-only bounded physical snapshots, sequential 3D cases, private user/temp data, full log audit and failure retention."""
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


def snapshot(source, project):
    ignore=shutil.ignore_patterns('.git','.git-template','obj','bin','__pycache__','test_suites','tests','docs')
    for folder in ['Core','scenes','config']:
        shutil.copytree(source/folder,project/folder,ignore=ignore)
    shutil.copy2(source/'project.godot',project/'project.godot')
    shutil.copytree(source/'.godot/mono/temp/bin/Debug',project/'.godot/mono/temp/bin/Debug')
    for path in source.glob('*.csproj'):shutil.copy2(path,project/path.name)
    names={'terrain_base','scene3d_base','menu_base','schema_tools'};pending=list(names);paths={}
    while pending:
        name=pending.pop()
        if '__' in name:
            host=name.split('__')[0]
            if host not in names:names.add(host);pending.append(host)
            continue
        path=next(p for p in [source/'mods'/name,source/'mods_private'/name] if p.exists());paths[name]=path
        for dep in json.loads((path/'manifest.json').read_text(encoding='utf-8-sig')).get('depends',[]):
            dep=re.split('[><=]',dep)[0]
            if dep not in names:names.add(dep);pending.append(dep)
    for name,path in paths.items():shutil.copytree(path,project/path.parent.name/name,ignore=ignore)
    shutil.copytree(source/'mods_private/terrain_base/test_suites/contracts',project/'mods_private/terrain_base/test_suites/contracts')
    shutil.copytree(source/'mods_private/terrain_base/test_suites/patch',project/'mods_private/terrain_base/test_suites/patch')
    folder=project/'collections/frontend';folder.mkdir(parents=True)
    (folder/'collection.json').write_text(json.dumps({'id':'frontend','kind':'frontend','version':'0.1.0','name':'3D terrain acceptance shell','mods':['ui_base','menu_base']}))
    return sorted(names)


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project',required=True,type=Path);parser.add_argument('--out',required=True,type=Path);parser.add_argument('--godot',required=True)
    parser.add_argument('--physics',choices=['default','jolt'],default='default');parser.add_argument('--case',action='append',dest='cases')
    args=parser.parse_args();args.out.mkdir(parents=True,exist_ok=False);scratch=Path(tempfile.mkdtemp(prefix='cthangover-terrain-7a-'))
    rows=[];errors=[]
    try:
        project=scratch/'project';project.mkdir();mods=snapshot(args.project,project)
        if args.physics=='jolt':(project/'override.cfg').write_text('[physics]\n3d/physics_engine="Jolt Physics"\n')
        hashes={str(p.relative_to(project)):hashlib.sha256(p.read_bytes()).hexdigest() for prefix in ['mods','mods_private'] for p in (project/prefix).rglob('*') if p.is_file() and p.suffix in ['.cs','.json','.gdshader','.r16','.png','.glb']}
        (args.out/'inputs.json').write_text(json.dumps({'mods':mods,'sha256':hashes,'physics':args.physics},indent=2))
        for name in args.cases or ['smoke']:
            out=args.out/name;out.mkdir();private=scratch/name;private.mkdir();env=os.environ.copy()
            for key,folder in [('APPDATA','roaming'),('LOCALAPPDATA','local'),('TMP','temp'),('TEMP','temp')]:
                env[key]=str(private/folder);Path(env[key]).mkdir(exist_ok=True)
            env['CTHANGOVER_TEST_CACHE']=str(scratch/'compiler-cache')
            command=[args.godot,'--path',str(project),'--verbose','--resolution','1280x720','--','--frontend','--with-mods=terrain_base,scene3d_base','--itest=terrain_contracts','--terrain-case='+name,'--terrain-out='+str(out)]
            start=time.monotonic()
            with (out/'stdout.log').open('wb') as stream:
                process=subprocess.Popen(command,env=env,cwd=project,stdout=stream,stderr=subprocess.STDOUT);deadline=time.monotonic()+240
                while process.poll() is None and time.monotonic()<deadline:
                    logs=private/'roaming/cthangover/logs'
                    if logs.exists():
                        shutil.copytree(logs,out/'game-logs',dirs_exist_ok=True)
                        log=logs/'cthangover.log'
                        if log.exists() and 'error CS' in log.read_text(encoding='utf-8-sig',errors='replace'):
                            subprocess.run(['taskkill','/PID',str(process.pid),'/T','/F'],capture_output=True);break
                    time.sleep(1)
                if process.poll() is None:subprocess.run(['taskkill','/PID',str(process.pid),'/T','/F'],capture_output=True)
                code=process.wait(timeout=20)
            logs=private/'roaming/cthangover/logs'
            if logs.exists():shutil.copytree(logs,out/'game-logs',dirs_exist_ok=True)
            text=(out/'stdout.log').read_text(encoding='utf-8',errors='replace')
            row=json.loads((out/'row.json').read_text()) if (out/'row.json').exists() else {'case':name,'status':'MISSING'}
            problems=[]
            if code!=0 or row['status']!='OK' or '[TEST_RESULT] name=terrain_acceptance passed=1 failed=0' not in text:problems.append('exit/status/marker failure')
            for line in text.splitlines():
                if any(token in line.lower() for token in ['error cs','leaked at exit','still in use at exit','objectdb instances leaked','lack lifecycle binding','rid allocations','leaked instance']):problems.append(line)
                if line.startswith('ERROR:') and 'Loader Message' not in line:problems.append(line)
            error_log=out/'game-logs/cthangover_errors.log'
            if error_log.exists():problems.extend(line for line in error_log.read_text(encoding='utf-8-sig',errors='replace').splitlines() if line.strip())
            row.update(exit_code=code,seconds=time.monotonic()-start,log=str(out/'stdout.log'),harness_errors=problems);rows.append(row);errors.extend(problems)
            (args.out/'matrix.json').write_text(json.dumps({'rows':rows,'harness_errors':errors},indent=2))
            print(json.dumps(row),flush=True)
            if problems:break
    finally:shutil.rmtree(scratch,ignore_errors=True)
    if errors:raise RuntimeError('\n'.join(errors))


if __name__=='__main__':main()
