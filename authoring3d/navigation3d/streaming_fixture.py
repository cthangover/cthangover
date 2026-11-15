"""Generate original terrain data in a disposable project, then place the integration's actors at quantized source heights."""
import hashlib,json,sys
from pathlib import Path

def prepare(project,provenance):
 sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'streaming'))
 from make_fixture import generate,height
 generate(project,provenance)
 path=project/'mods_private/test_stream_island/locations3d/fixture.json';data=json.loads(path.read_text());points=[]
 for name,x,z in [('stream.source',-900,-895),('stream.goal',-700,-895),('stream.delayed',705,705)]:
  y=round((height(x,z)+32)*512)/512-32+.12;points.append(dict(Id=name,Transform=dict(Position=[x,y,z]),Capacity=1))
 data['Items'][0]['Points']+=points;path.write_text(json.dumps(data,indent=2)+'\n')
 (provenance/'navigation_fixture.json').write_text(json.dumps(dict(points=points,preparation_sha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest()),indent=2)+'\n')
