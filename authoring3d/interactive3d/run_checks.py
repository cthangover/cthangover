"""Host-only isolated interactive3d acceptance. Copies selected 3D mods and dependencies, never runs 2D/full regression."""
import argparse, hashlib, json, os, re, shutil, subprocess, tempfile, time, sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'actor3d'))
from capture_window import save_client

CASES={
 'land_assets':['--with-mods=proto_land_pack','--itest=proto_land_contracts','--run-tests','--mod=proto_land_pack__contracts','--test-filter=proto_land/load_assets_*'],
 'land_clips':['--with-mods=proto_land_pack','--itest=proto_land_contracts','--run-tests','--mod=proto_land_pack__contracts','--test-filter=proto_land/all_clips_*'],
 'contact_pose':['--with-mods=proto_land_pack','--itest=proto_land_contracts','--run-tests','--mod=proto_land_pack__contracts','--test-filter=proto_land/contact_pose_*'],
 'navigation_contacts':['--with-mods=interactive3d_editor,combat3d_base,navigation3d_base','--itest=interactive3d_contracts','--run-tests','--mod=interactive3d_base__contracts','--test-filter=interactive3d/contact_actor_*'],
 'land_lifecycle':['--with-mods=proto_land_pack','--itest=proto_land_contracts','--run-tests','--mod=proto_land_pack__contracts','--test-filter=proto_land/contact_lifecycle_*'],
 'physical_contacts':['--with-mods=proto_land_pack','--itest=proto_land_contracts','--run-tests','--mod=proto_land_pack__contracts','--test-filter=proto_land/contact_physical_calibration'],
 'land_acceptance':['--with-mods=proto_land_pack','--itest=proto_land_contracts','--run-tests','--mod=proto_land_pack__contracts','--test-filter=proto_land/contact_physical_acceptance'],
 'action_units':['--with-mods=actor3d_base','--run-tests','--mod=actor3d_base','--test-filter=actor3d/action_*'],
 'action_contracts':['--with-mods=actor3d_base,proto_sea_pack','--itest=actor3d_contracts','--run-tests','--mod=actor3d_base__contracts','--test-filter=actor3d/action_*'],
 'legacy_actor_units':['--with-mods=actor3d_base','--run-tests','--mod=actor3d_base','--test-filter=actor3d/orders_*'],
 'legacy_actor_navigation':['--with-mods=actor3d_base,proto_sea_pack','--itest=actor3d_contracts','--run-tests','--mod=actor3d_base__contracts','--test-filter=actor3d/npc_move_follow_distance_manual_priority_unreachable'],
 'legacy_actor_activity':['--with-mods=actor3d_base,proto_sea_pack','--itest=actor3d_contracts','--run-tests','--mod=actor3d_base__contracts','--test-filter=actor3d/external_role_seek_facing_and_release'],
 'anim_seek_contract':['--with-mods=anim3d_base','--itest=anim3d_base_contracts','--run-tests','--mod=anim3d_base__contracts','--test-filter=anim3d/blend_interrupt_seek*'],
 'contact_draft':['--with-mods=proto_land_pack','--itest=proto_land_contracts','--run-tests','--mod=proto_land_pack__contracts','--test-filter=proto_land/contact_draft*'],
 'editor_contact_contracts':['--with-mods=interactive3d_editor','--itest=interactive3d_contracts','--itest=interactive3d_editor_contracts','--run-tests','--mod=interactive3d_editor__contracts','--test-filter=interactive3d/editor_contact_*'],
 'contact_contracts':['--with-mods=interactive3d_editor,combat3d_base','--itest=interactive3d_contracts','--run-tests','--mod=interactive3d_base__contracts','--test-filter=interactive3d/contact_*'],
 'pose_contracts':['--with-mods=characters3d_base,proto_sea_pack','--itest=anim3d_base_contracts','--itest=characters3d_base_contracts','--run-tests','--mod=characters3d_base__contracts','--test-filter=characters3d/contact_*'],
 'contact_probe':['--with-mods=proto_land_pack','--itest=proto_land_contracts','--run-tests','--mod=proto_land_pack__contracts','--test-filter=proto_land/contact_calibration*'],
 'contact_units':['--with-mods=interactive3d_editor,combat3d_base','--run-tests','--mod=interactive3d_base','--test-filter=interactive3d/contact_*'],
 'characters_units':['--with-mods=characters3d_base','--run-tests','--mod=characters3d_base'],
 'fitting_contracts':['--with-mods=characters3d_base,proto_sea_pack','--itest=anim3d_base_contracts','--itest=characters3d_base_contracts','--run-tests','--mod=characters3d_base__contracts','--test-filter=characters3d/exclusive_fitting*'],
 'units':['--with-mods=interactive3d_editor,combat3d_base','--run-tests','--mod=interactive3d_base'],
 'editor_units':['--with-mods=interactive3d_editor','--run-tests','--mod=interactive3d_editor'],
 'contracts':['--with-mods=interactive3d_editor,combat3d_base','--itest=interactive3d_contracts','--run-tests','--mod=interactive3d_base__contracts'],
 'editor_contracts':['--with-mods=interactive3d_editor','--itest=interactive3d_contracts','--itest=interactive3d_editor_contracts','--run-tests','--mod=interactive3d_editor__contracts'],
 'actor_units':['--with-mods=actor3d_base','--run-tests','--mod=actor3d_base'],
 'actor_contracts':['--with-mods=actor3d_base,proto_sea_pack','--itest=actor3d_contracts','--run-tests','--mod=actor3d_base__contracts'],
}
def dump(path,data):path.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def snapshot(root,target):
 target.mkdir(); ignore=shutil.ignore_patterns('.git','.git-template','bin','obj','docs','__pycache__')
 for folder in ['Core','scenes','config']:shutil.copytree(root/folder,target/folder,ignore=ignore)
 for name in ['Cthangover.csproj','global.json','Directory.Build.props','Directory.Build.targets','project.godot']:shutil.copy2(root/name,target/name)
 shutil.copytree(root/'.godot/mono/temp/bin/Debug',target/'.godot/mono/temp/bin/Debug')
 names={'navigation3d_base','proto_land_pack','menu_base','interactive3d_editor','combat3d_base','proto_sea_pack','dialog_base','items_base','schema_tools','mod_manager'};pending=list(names)
 while pending:
  name=pending.pop();source=next(p for p in [root/'mods'/name,root/'mods_private'/name] if p.exists());shutil.copytree(source,target/source.parent.name/name,ignore=ignore)
  for dep in json.loads((source/'manifest.json').read_text(encoding='utf-8-sig')).get('depends',[]):
   dep=re.split('[><=]',dep)[0]
   if dep not in names:names.add(dep);pending.append(dep)
 return sorted(names)
def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--godot',default=r'C:\godot\godot.exe');p.add_argument('--out',required=True,type=Path);p.add_argument('--case',choices=CASES,action='append');p.add_argument('--filter');args=p.parse_args()
 root=Path.cwd();out=args.out.resolve();out.mkdir(parents=True,exist_ok=False);report=[]
 with tempfile.TemporaryDirectory(prefix='cthangover-interactive3d-') as scratch:
  project=Path(scratch)/'source';mods=snapshot(root,project)
  hashes={str(q.relative_to(project)):hashlib.sha256(q.read_bytes()).hexdigest() for part in ['Core','mods','mods_private'] for q in (project/part).rglob('*') if q.is_file() and q.suffix in ['.cs','.json','.tscn','.glb','.properties']}
  dump(out/'inputs.json',dict(snapshot=str(project),mods=mods,hashes=hashes))
  with (out/'build.log').open('wb') as log:subprocess.run(['dotnet','build','Cthangover.csproj','--nologo','-v','minimal'],cwd=project,stdout=log,stderr=subprocess.STDOUT,check=True,timeout=180)
  for case in args.case or ['units','editor_units','contracts','editor_contracts']:
   directory=out/case;directory.mkdir();env=os.environ.copy()
   for name,part in [('APPDATA','roaming'),('LOCALAPPDATA','local'),('XDG_DATA_HOME','data')]:env[name]=str(Path(scratch)/case/part);Path(env[name]).mkdir(parents=True)
   env['INTERACTIVE3D_SHOTS']=str(directory/'captures');env['LAND_PACK_EVIDENCE']=str(directory);command=[args.godot,'--path',str(project),'--verbose','--','--frontend',*CASES[case]]
   if args.filter:command.append('--test-filter='+args.filter)
   capture_errors=[]
   with (directory/'stdout.log').open('wb') as log:
    process=subprocess.Popen(command,cwd=scratch,env=env,stdout=log,stderr=subprocess.STDOUT)
    dump(directory/'process.json',dict(pid=process.pid,command=command))
    try:
     started=time.monotonic();compared=False
     while process.poll() is None:
      if time.monotonic()-started>300:raise subprocess.TimeoutExpired(command,300)
      if (directory/'captures/display-check.ready').exists() and not compared:
       try:save_client(process.pid,directory/'captures/display-client.png')
       except Exception as error:capture_errors.append('Display comparison failed: '+str(error))
       (directory/'captures/display-check.done').write_text('failed' if capture_errors else 'captured');compared=True
      try:process.wait(timeout=.1)
      except subprocess.TimeoutExpired:pass
     code=process.returncode
    except subprocess.TimeoutExpired:subprocess.run(['taskkill','/PID',str(process.pid),'/T','/F'],capture_output=True);process.wait(timeout=20);code=124
    except BaseException:
     if process.poll() is None:subprocess.run(['taskkill','/PID',str(process.pid),'/T','/F'],capture_output=True);process.wait(timeout=20)
     raise
   for source,name in [(Path(env['APPDATA'])/'cthangover/logs','game-logs'),(Path(env['APPDATA'])/'cthangover/mod_tests','mod-tests')]:
    if source.exists():shutil.copytree(source,directory/name)
   text=(directory/'stdout.log').read_text(encoding='utf-8',errors='replace');complete=text+'\n'+'\n'.join(q.read_text(encoding='utf-8',errors='replace') for q in (directory/'game-logs').glob('*.log'));lines=complete.splitlines()
   bad=capture_errors+[line for line in lines if any(t in line.lower() for t in ['error cs','lack lifecycle binding','leaked at exit','still in use at exit','objectdb instances leaked','was leaked','were leaked','rid allocations','leaked instance'])]
   for i,line in enumerate(lines):
    if line.startswith('ERROR:') and not (line.startswith('ERROR: GENERAL - Message Id Number: 0 | Message Id Name: Loader Message') and i+1<len(lines) and 'EOSOverlayVkLayer-Win64.json' in lines[i+1]):bad.append(line)
   matches=re.findall(r'\[TEST_RESULT\] name=mod_units passed=(\d+) failed=(\d+)',text)
   if not matches or int(matches[-1][0])<1 or int(matches[-1][1])!=0:bad.append('Missing successful nonempty terminal marker: '+str(matches))
   row=dict(case=case,exit_code=code,terminal=matches,errors=list(dict.fromkeys(bad)),command=command);report.append(row);dump(out/'checks.json',report);print(json.dumps(row,ensure_ascii=False),flush=True)
  if any(row['exit_code'] or row['errors'] for row in report):raise RuntimeError('Selected components failed; see '+str(out/'checks.json'))
if __name__=='__main__':main()
