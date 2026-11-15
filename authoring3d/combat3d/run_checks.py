"""Host-only targeted 3D acceptance; no 2D or full regression selection."""
import argparse
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import time

import sys
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/"actor3d"))
from capture_window import save_client

CASES = {
 'combat3d_units': ['--with-mods=combat3d_base','--run-tests','--mod=combat3d_base'],
 'combat3d': ['--with-mods=combat3d_base,proto_sea_pack','--itest=combat3d_base_contracts','--run-tests','--mod=combat3d_base__contracts'],
 'combat3d_editor': ['--with-mods=combat3d_editor,proto_sea_pack','--itest=combat3d_base_contracts','--itest=combat3d_editor_contracts','--run-tests','--mod=combat3d_editor__contracts'],
 'combat3d_duel': ['--with-mods=combat3d_base,proto_sea_pack','--itest=combat3d_base_contracts','--combat3d-demo'],
 'combat3d_pistol': ['--with-mods=combat3d_base,proto_sea_pack','--itest=combat3d_base_contracts','--combat3d-demo'],
 'combat3d_fifty_terrain': ['--with-mods=combat3d_base,proto_sea_pack','--itest=combat3d_base_contracts','--combat3d-demo'],
 'combat3d_fifty_decks': ['--with-mods=combat3d_base,proto_sea_pack','--itest=combat3d_base_contracts','--combat3d-demo'],
 'combat3d_demo': ['--with-mods=combat3d_base,proto_sea_pack','--itest=combat3d_base_contracts','--combat3d-demo'],
}


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--godot',required=True);p.add_argument('--out',type=Path,required=True)
    p.add_argument('--case',action='append',choices=CASES);p.add_argument('--filter');args=p.parse_args()
    project=Path.cwd();out=args.out.resolve();out.mkdir(parents=True,exist_ok=True);report=[]
    for case in args.case or CASES:
        directory=out/case;directory.mkdir(exist_ok=False)
        comparison_error=None
        with tempfile.TemporaryDirectory(prefix='cthangover-actor3d-') as home:
            env=os.environ.copy()
            for name,part in [('APPDATA','roaming'),('LOCALAPPDATA','local'),('XDG_DATA_HOME','data')]:
                env[name]=str(Path(home)/part);Path(env[name]).mkdir()
            env['COMBAT3D_SHOTS']=str(directory/'captures')
            env['COMBAT3D_CASE']='boarding' if case=='combat3d_demo' else case.removeprefix('combat3d_')
            command=[args.godot,'--path',str(project),'--verbose','--','--frontend',*CASES[case]]
            if args.filter:command.append('--test-filter='+args.filter)
            with (directory/'stdout.log').open('wb') as log:
                process=subprocess.Popen(command,cwd=home,env=env,stdout=log,stderr=subprocess.STDOUT)
                try:
                    started=time.monotonic(); compared=False
                    while process.poll() is None:
                        if time.monotonic()-started>420:raise subprocess.TimeoutExpired(command,420)
                        ready=directory/'captures/display-check.ready'
                        if '--combat3d-demo' in CASES[case] and ready.exists() and not compared:
                            try:save_client(process.pid,directory/'captures/display-client.png')
                            except Exception as error:comparison_error='Display comparison failed: '+str(error)
                            compared=True
                        try:process.wait(timeout=0.1)
                        except subprocess.TimeoutExpired:pass
                    code=process.returncode
                except subprocess.TimeoutExpired:
                    subprocess.run(['taskkill','/PID',str(process.pid),'/T','/F'],capture_output=True);process.wait(timeout=20);code=124
            logs=Path(home)/'roaming/cthangover/logs'
            if logs.exists():shutil.copytree(logs,directory/'game-logs')
            results=Path(home)/'roaming/cthangover/mod_tests'
            if results.exists():shutil.copytree(results,directory/'mod-tests')
        text=(directory/'stdout.log').read_text(encoding='utf-8',errors='replace')
        complete=text+'\n'+'\n'.join(f.read_text(encoding='utf-8',errors='replace') for f in (directory/'game-logs').glob('*.log'))
        bad=[line for line in complete.splitlines() if any(t in line.lower() for t in ('error cs','lack lifecycle binding','leaked at exit','still in use at exit','objectdb instances leaked','was leaked','were leaked','rid allocations','leaked instance','combat3d acceptance failed','unsupported type'))]
        lines=complete.splitlines()
        if comparison_error:bad.append(comparison_error)
        for i,line in enumerate(lines):
            if line.startswith('ERROR:') and not (line.startswith('ERROR: GENERAL - Message Id Number: 0 | Message Id Name: Loader Message') and i+1<len(lines) and 'EOSOverlayVkLayer-Win64.json' in lines[i+1]):bad.append(line)
        marker='combat3d_demo' if '--combat3d-demo' in CASES[case] else 'mod_units'
        matches=re.findall(r'\[TEST_RESULT\] name='+marker+r' passed=(\d+) failed=(\d+)',text)
        if not matches or int(matches[-1][0])<1 or int(matches[-1][1])!=0:bad.append('Missing successful nonempty terminal marker: '+str(matches))
        row=dict(case=case,exit_code=code,terminal=matches,errors=list(dict.fromkeys(bad)),command=command);report.append(row)
        (out/'checks.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(row),flush=True)
        if code or bad:raise RuntimeError(case+' failed; see '+str(directory))


if __name__=='__main__':main()
