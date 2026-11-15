"""Publish only measured staged terrain files, rejecting concurrent shared-workspace edits."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil


def signature(path):
    return dict(sha256=hashlib.sha256(path.read_bytes()).hexdigest(),mtime_ns=path.stat().st_mtime_ns)


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--staging',required=True,type=Path);parser.add_argument('--baseline',required=True,type=Path)
    parser.add_argument('--evidence',action='append',required=True,type=Path);parser.add_argument('--receipt',required=True,type=Path);args=parser.parse_args()
    baseline=json.loads(args.baseline.read_text());evidence=[];matrix=set();editor=False
    for folder in args.evidence:
        rows=json.loads((folder/'result.json').read_text())
        if not rows or any(r['exit_code'] or r['errors'] or r['measurement'].get('status')!='OK' for r in rows):raise RuntimeError('Unsuccessful evidence: '+str(folder))
        hashes={k.replace('\\','/'):v for k,v in json.loads((folder/'sources.json').read_text())['sha256'].items()}
        evidence.append(hashes)
        for row in rows:
            if row['case']=='crowd':matrix.add((row['backend'],row['quality']))
            elif row['case']=='editor' and row['measurement'].get('editor_checks',0)>=18:editor=True
    expected={(backend,tier) for backend in ['DEFAULT','Jolt'] for tier in ['low','medium','high']}
    if matrix!=expected or not editor:raise RuntimeError('Six crowd rows and the complete editor contract are required')
    root=Path.cwd();changes=[]
    for name in ['terrain_base','terrain_editor']:
        for path in sorted((args.staging/'mods_private'/name).rglob('*')):
            if not path.is_file() or '.git' in path.parts:continue
            rel=path.relative_to(args.staging).as_posix();shared=root/rel;digest=signature(path)['sha256']
            if '/docs/' not in rel:
                candidates=[e.get(rel) for e in evidence]
                if '/test_suites/' not in rel and any(value!=digest for value in candidates):raise RuntimeError('Production source differs across evidence: '+rel)
                if digest not in candidates:raise RuntimeError('Staged source has no successful native receipt: '+rel)
            if shared.exists():
                actual=signature(shared)
                if actual!=baseline.get(rel):raise RuntimeError('Concurrent shared source edit: '+rel)
                if actual['sha256']==digest:continue
            elif rel in baseline:raise RuntimeError('Shared source removed by another owner: '+rel)
            changes.append((path,shared,rel,digest))
    # All comparisons complete before any shared source is changed.
    receipt=[]
    for source,target,rel,digest in changes:
        target.parent.mkdir(parents=True,exist_ok=True);temporary=target.with_name('.'+target.name+'.vegetation.tmp');shutil.copyfile(source,temporary);temporary.replace(target)
        receipt.append(dict(path=rel,sha256=digest,mtime_ns=target.stat().st_mtime_ns))
    args.receipt.write_text(json.dumps(receipt,indent=2));print('Published '+str(len(receipt))+' validated files')


if __name__=='__main__':main()
