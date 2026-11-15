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
    import importlib.util
    spec=importlib.util.spec_from_file_location('terrain7a_snapshot',source/'authoring3d/terrain/stage7a/run_acceptance.py')
    helper=importlib.util.module_from_spec(spec);spec.loader.exec_module(helper)
    mods=helper.snapshot(source,project,extra_mods=['terrain_editor'])
    shutil.copytree(source/'mods_private/terrain_editor/test_suites/contracts',project/'mods_private/terrain_editor/test_suites/contracts')
    fixture=project/'mods_private/test_terrain_editor_content'
    shutil.copytree(source/'mods_private/terrain_base/test_suites/contracts',fixture,ignore=shutil.ignore_patterns('src','manifest.json'))
    tile_file=fixture/'terrains/tiles/fixture.json'
    tiles=json.loads(tile_file.read_text(encoding='utf-8-sig'))
    for tile in tiles['Items']:tile['EditorProbeUnknown']={'keep':[1,2]}
    tile_file.write_text(json.dumps(tiles,indent=2)+'\n')
    (fixture/'manifest.json').write_text(json.dumps({'id':'test_terrain_editor_content','name':'Independent terrain editor fixture','version':'0.1.0','depends':['terrain_base>=0.1.0','scene3d_base>=0.1.0']}))
    patch=project/'mods_private/test_terrain_editor_patch'
    shutil.copytree(source/'mods_private/terrain_base/test_suites/patch',patch)
    (patch/'manifest.json').write_text(json.dumps({'id':'test_terrain_editor_patch','name':'Foreign baseline patch','version':'0.1.0','depends':['test_terrain_editor_content>=0.1.0']}))
    reset_work(project)
    return mods+['test_terrain_editor_content','test_terrain_editor_patch','test_terrain_editor_work']


def reset_work(project):
    work=project/'mods_private/test_terrain_editor_work'
    if work.exists():shutil.rmtree(work)
    work.mkdir(parents=True)
    (work/'manifest.json').write_text(json.dumps({'id':'test_terrain_editor_work','name':'Disposable writable destination','version':'0.1.0','depends':['terrain_editor>=0.1.0','test_terrain_editor_content>=0.1.0','test_terrain_editor_patch>=0.1.0']}))


def inputs(out):
    import struct
    from PIL import Image
    folder=out/'inputs';folder.mkdir()
    codes=[(x*503+y*911)&65535 for y in range(65) for x in range(65)]
    raw=struct.pack('<4225H',*codes)
    Image.frombytes('I;16',(65,65),raw).save(folder/'height16.png')
    Image.new('L',(65,65),128).save(folder/'height8.png')
    whole=[(x*503+y*911)&65535 for y in range(129) for x in range(129)]
    Image.frombytes('I;16',(129,129),struct.pack('<16641H',*whole)).save(folder/'whole16.png')
    (folder/'height.r16').write_bytes(raw)
    (folder/'height.r32').write_bytes(struct.pack('<4225f',*[-4+c*.0001 for c in codes]))
    (folder/'manifest.json').write_text(json.dumps({'formula':'code=(x*503+y*911)&65535','r32':'-4+code*0.0001','sha256':{p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in folder.iterdir() if p.is_file()}},indent=2))


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project',required=True,type=Path);parser.add_argument('--out',required=True,type=Path);parser.add_argument('--godot',required=True)
    parser.add_argument('--physics',choices=['default','jolt'],default='default');parser.add_argument('--case',action='append',dest='cases')
    args=parser.parse_args();args.out.mkdir(parents=True,exist_ok=False);scratch=Path(tempfile.mkdtemp(prefix='cthangover-terrain-editor-7b-'))
    rows=[];errors=[]
    try:
        project=scratch/'project';project.mkdir();mods=snapshot(args.project,project)
        if args.physics=='jolt':(project/'override.cfg').write_text('[physics]\n3d/physics_engine="Jolt Physics"\n')
        hashes={str(p.relative_to(project)):hashlib.sha256(p.read_bytes()).hexdigest() for prefix in ['mods','mods_private'] for p in (project/prefix).rglob('*') if p.is_file() and p.suffix in ['.cs','.json','.gdshader','.r16','.png','.glb']}
        (args.out/'inputs.json').write_text(json.dumps({'mods':mods,'sha256':hashes,'physics':args.physics},indent=2))
        for name in args.cases or ['smoke']:
            out=args.out/name;out.mkdir();inputs(out);reset_work(project);private=scratch/name;private.mkdir();env=os.environ.copy()
            for key,folder in [('APPDATA','roaming'),('LOCALAPPDATA','local'),('TMP','temp'),('TEMP','temp')]:
                env[key]=str(private/folder);Path(env[key]).mkdir(exist_ok=True)
            env['CTHANGOVER_TEST_CACHE']=str(scratch/'compiler-cache')
            command=[args.godot,'--path',str(project),'--verbose','--resolution','1280x720','--','--frontend','--with-mods=terrain_editor,test_terrain_editor_work','--itest=terrain_editor_contracts','--terrain-editor-case='+name,'--terrain-editor-out='+str(out)]
            start=time.monotonic()
            with (out/'stdout.log').open('wb') as stream:
                process=subprocess.Popen(command,env=env,cwd=project,stdout=stream,stderr=subprocess.STDOUT);deadline=time.monotonic()+300
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
            shutil.copytree(project/'mods_private/test_terrain_editor_work',out/'work-after',dirs_exist_ok=True)
            logs=private/'roaming/cthangover/logs'
            if logs.exists():shutil.copytree(logs,out/'game-logs',dirs_exist_ok=True)
            text=(out/'stdout.log').read_text(encoding='utf-8',errors='replace')
            row=json.loads((out/'row.json').read_text()) if (out/'row.json').exists() else {'case':name,'status':'MISSING'}
            problems=[]
            if code!=0 or row['status']!='OK' or '[TEST_RESULT] name=terrain_editor_acceptance passed=1 failed=0' not in text:problems.append('exit/status/marker failure')
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
