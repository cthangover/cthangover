"""Host watcher only: freeze a selected 3D dependency closure and keep raw logs/captures outside repositories."""
import argparse,hashlib,json,os,re,shutil,subprocess,tempfile,time,sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'actor3d'))
from capture_window import save_client
CASES={
 'actor_navigation':['--with-mods=npc_routines_editor,proto_sea_pack','--itest=actor3d_contracts','--itest=interactive3d_contracts','--itest=npc_routines_base_contracts','--run-tests','--mod=actor3d_base__contracts','--test-filter=actor3d/npc_move_follow_distance_manual_priority_unreachable'],
 'units':['--with-mods=npc_routines_editor','--itest=interactive3d_contracts','--itest=npc_routines_base_contracts','--run-tests','--mod=npc_routines_base'],
 'editor_units':['--with-mods=npc_routines_editor','--itest=interactive3d_contracts','--itest=npc_routines_base_contracts','--run-tests','--mod=npc_routines_editor'],
 'interactive_units':['--with-mods=npc_routines_editor','--itest=interactive3d_contracts','--itest=npc_routines_base_contracts','--run-tests','--mod=interactive3d_base'],
 'land':['--with-mods=npc_routines_editor','--itest=interactive3d_contracts','--itest=npc_routines_base_contracts','--itest=npc_routines_land_preview','--run-tests','--mod=npc_routines_base__land_preview'],
 'runtime':['--with-mods=npc_routines_editor','--itest=interactive3d_contracts','--itest=npc_routines_base_contracts','--run-tests','--mod=npc_routines_base__contracts'],
 'editor':['--with-mods=npc_routines_editor','--itest=interactive3d_contracts','--itest=npc_routines_base_contracts','--itest=npc_routines_editor_contracts','--run-tests','--mod=npc_routines_editor__contracts'],
 'interactive':['--with-mods=npc_routines_editor','--itest=interactive3d_contracts','--itest=npc_routines_base_contracts','--run-tests','--mod=interactive3d_base__contracts'],
}
def dump(path,value):path.write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def snapshot(root,target,land=False,sea=False):
 target.mkdir();ignore=shutil.ignore_patterns('.git','.git-template','bin','obj','docs','__pycache__')
 for name in ['Core','scenes','config']:shutil.copytree(root/name,target/name,ignore=ignore)
 for name in ['Cthangover.csproj','global.json','Directory.Build.props','Directory.Build.targets','project.godot']:shutil.copy2(root/name,target/name)
 shutil.copytree(root/'collections/frontend',target/'collections/frontend')
 shutil.copytree(root/'.godot/mono/temp/bin/Debug',target/'.godot/mono/temp/bin/Debug')
 names={'menu_base','npc_routines_editor','dialog_base','schema_tools','mod_manager','items_base'}
 if land:names.add('proto_land_pack')
 if sea:names.add('proto_sea_pack')
 pending=list(names)
 while pending:
  name=pending.pop();source=next(p for p in [root/'mods'/name,root/'mods_private'/name] if p.exists());shutil.copytree(source,target/source.parent.name/name,ignore=ignore)
  for dependency in json.loads((source/'manifest.json').read_text(encoding='utf-8-sig')).get('depends',[]):
   dependency=re.split('[><=]',dependency)[0]
   if dependency not in names:names.add(dependency);pending.append(dependency)
 return sorted(names)
def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--out',type=Path,required=True);p.add_argument('--case',action='append',choices=CASES);p.add_argument('--filter');p.add_argument('--godot',default=r'C:\godot\godot.exe');args=p.parse_args()
 root=Path.cwd();out=args.out.resolve();out.mkdir(parents=True,exist_ok=False);checks=[]
 with tempfile.TemporaryDirectory(prefix='cthangover-routines-') as temp:
  project=Path(temp)/'source';mods=snapshot(root,project,'land' in (args.case or []),'actor_navigation' in (args.case or []));dump(out/'inputs.json',dict(mods=mods,hashes={str(f.relative_to(project)):hashlib.sha256(f.read_bytes()).hexdigest() for part in ['Core','mods','mods_private'] for f in (project/part).rglob('*') if f.is_file() and f.suffix in ['.cs','.json','.glb','.properties','.tscn']}))
  with (out/'build.log').open('wb') as log:subprocess.run(['dotnet','build','Cthangover.csproj','--nologo','-v','minimal'],cwd=project,stdout=log,stderr=subprocess.STDOUT,timeout=180,check=True)
  for case in args.case or ['units']:
   folder=out/case;folder.mkdir();env=os.environ.copy()
   # The independent routine catalog supplies real asset/character definitions. Its
   # player override belongs only to its own room, not the earlier R2 acceptance room.
   actors=project/'mods_private/npc_routines_base/test_suites/contracts/definitions/actors.json'
   original_actors=actors.read_bytes()
   if case=='interactive':
    fixture=json.loads(original_actors);fixture['Items']=[row for row in fixture['Items'] if row['Id']!='player'];actors.write_text(json.dumps(fixture),encoding='utf-8');dump(folder/'fixture-preparation.json',dict(removed_own_actor_override='player',unchanged_test_owner='interactive3d_base__contracts'))
   for key,part in [('APPDATA','roaming'),('LOCALAPPDATA','local'),('XDG_DATA_HOME','data')]:env[key]=str(Path(temp)/case/part);Path(env[key]).mkdir(parents=True)
   env['NPC_ROUTINES_OUTPUT']=str(folder);env['NPC_ROUTINES_DISPLAY']='1'
   command=[args.godot,'--path',str(project),'--verbose','--','--frontend',*CASES[case]]
   if args.filter:command.append('--test-filter='+args.filter)
   with (folder/'stdout.log').open('wb') as log:
    child=subprocess.Popen(command,cwd=temp,env=env,stdout=log,stderr=subprocess.STDOUT)
    try:
     started=time.monotonic();compared=False
     while child.poll() is None:
      if time.monotonic()-started>300:raise subprocess.TimeoutExpired(command,300)
      if (folder/'display-check.ready').exists() and not compared:
       save_client(child.pid,folder/'display-client.png');(folder/'display-check.done').write_text('captured',encoding='utf-8');compared=True
      try:child.wait(timeout=.1)
      except subprocess.TimeoutExpired:pass
     code=child.returncode
    except subprocess.TimeoutExpired:subprocess.run(['taskkill','/PID',str(child.pid),'/T','/F'],capture_output=True);child.wait(timeout=20);code=124
    except Exception:
     subprocess.run(['taskkill','/PID',str(child.pid),'/T','/F'],capture_output=True);child.wait(timeout=20);raise
   actors.write_bytes(original_actors)
   for part,dest in [('logs','game-logs'),('mod_tests','mod-tests')]:
    source=Path(env['APPDATA'])/'cthangover'/part
    if source.exists():shutil.copytree(source,folder/dest)
   text=(folder/'stdout.log').read_text(encoding='utf-8',errors='replace');lines=text.splitlines();bad=[]
   for index,line in enumerate(lines):
    if any(term in line.lower() for term in ['error cs','parse error:','lack lifecycle binding','leaked at exit','still in use at exit','objectdb instances leaked','was leaked','were leaked','rid allocations','leaked instance']):bad.append(line)
    if line.startswith('ERROR:') and not ('Loader Message' in line and index+1<len(lines) and 'EOSOverlayVkLayer-Win64.json' in lines[index+1]):bad.append(line)
   for log_file in (folder/'game-logs').glob('*'):
    if not log_file.is_file():continue
    log_text=log_file.read_text(encoding='utf-8',errors='replace')
    for line in log_text.splitlines():
     if 'error cs' in line.lower() or 'lack lifecycle binding' in line.lower() or ('cthangover_errors.log' in log_file.name and line.strip()):bad.append(line)
   marker=re.findall(r'\[TEST_RESULT\] name=mod_units passed=(\d+) failed=(\d+)',text)
   if len(marker)!=1 or int(marker[0][0])==0 or int(marker[0][1])!=0:bad.append('Invalid terminal: '+str(marker))
   row=dict(case=case,exit_code=code,terminal=marker,errors=list(dict.fromkeys(bad)),command=command);checks.append(row);dump(out/'checks.json',checks);print(json.dumps(row,ensure_ascii=False),flush=True)
   if code or bad:raise RuntimeError(case+' failed: '+str(folder))
if __name__=='__main__':main()
