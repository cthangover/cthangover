"""Original curved blades, flowers and fronds, generated only in an isolated test project."""
import hashlib
import json
import math
from pathlib import Path
import random
import shutil
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1]/'terrain/stage7a'))
from create_fixture import glb, entity


def foliage(seed, height, blades, flower=False, fern=False):
    rng=random.Random(seed)
    positions=[];normals=[];uv=[];indices=[]
    for blade in range(blades):
        angle=blade*math.tau/blades+rng.random()*.5
        direction=(math.cos(angle),math.sin(angle));side=(-direction[1],direction[0])
        h=height*rng.uniform(.65,1);width=(.12 if fern else .045)*rng.uniform(.7,1.2)
        base=len(positions)
        for segment in range(5):
            t=segment/4;spread=t*t*(.42 if fern else .22);w=width*(math.sin(t*math.pi)*.8+.2)*(1-t*.95)
            for sign in [-1,1]:
                positions.append((direction[0]*spread+side[0]*w*sign,h*t,direction[1]*spread+side[1]*w*sign))
                normals.append((0,.65,.76));uv.append(((sign+1)*.5,t))
        for segment in range(4):
            at=base+segment*2;indices.extend([at,at+1,at+2,at+1,at+3,at+2])
    result=[(positions,normals,uv,indices,0)]
    if flower:
        p=[];n=[];u=[];ix=[]
        for petal in range(6):
            a=petal*math.tau/6;at=len(p);cx=.15*math.cos(a);cz=.15*math.sin(a)
            p.extend([(0,height,0),(cx-.04,height+.025,cz),(cx,height+.04,cz+.05),(cx+.04,height+.025,cz)])
            n.extend([(0,1,0)]*4);u.extend([(0,0),(1,0),(1,1),(0,1)]);ix.extend([at,at+1,at+2,at,at+2,at+3])
        result.append((p,n,u,ix,1))
    return result


def generate(project, provenance):
    root=project/'mods_private/test_meadow';root.mkdir(parents=True,exist_ok=True)
    (root/'manifest.json').write_text(json.dumps(dict(id='test_meadow',name='Procedural meadow',version='0.1.0',depends=['terrain_base>=0.1.0','scene3d_base>=0.1.0'])))
    source=project/'mods_private/terrain_base/test_suites/contracts'
    for folder in ['terrain','terrains/tiles','terrains/regions','terrains/materials']:
        shutil.copytree(source/folder,root/folder,dirs_exist_ok=True)
    tiles=json.loads((root/'terrains/tiles/fixture.json').read_text())['Items']
    for tile in tiles:
        name=tile['Id'].split('/')[-1];x0,z0=map(int,name.split('_')[1:]);weights=bytearray();density=bytearray()
        for z in range(z0,z0+33):
            for x in range(x0,x0+33):
                bare=max(0,1-abs(z-24)/2);flowers=.4*max(0,math.sin(x*.2+z*.17));ferns=.7*max(0,math.sin(x*.07-1)*math.sin(z*.1+.7))
                values=[max(.05,1-bare-flowers-ferns),bare,flowers,ferns];total=sum(values);w=[round(v/total*255) for v in values];w[max(range(4),key=w.__getitem__)]+=255-sum(w);weights.extend(w);density.append(255)
        (root/'terrain'/tile['Weights']).write_bytes(weights);(root/'terrain'/tile['Density']).write_bytes(density)
    material=json.loads((root/'terrains/materials/fixture.json').read_text())
    for layer in material['Items'][0]['Layers']:
        if layer['Id'] in ['sand','mud']:
            layer['Albedo']='terrain_fixture/grass.png';layer['Normal']='terrain_fixture/grass_normal.png';layer['Orm']='terrain_fixture/grass_orm.png'
    (root/'terrains/materials/fixture.json').write_text(json.dumps(material))
    assets=[];profiles=[];objects=[];species=[]
    for i,(name,h,count,density,layer) in enumerate([
        ('fine_grass',.42,7,12,0),('broad_grass',.65,5,3,0),('yellow_flower',.58,3,.22,2),('white_flower',.5,3,.24,2),('fern',.95,10,.12,3)]):
        key='meadow/'+name;path=root/'models3d'/('meadow/'+name+'.glb')
        green=[.20,.40,.065,1] if i==0 else [.11,.27,.045,1]
        petal=[1,.72,.06,1] if i==2 else [.95,.94,.83,1]
        materials=[dict(doubleSided=True,pbrMetallicRoughness=dict(baseColorFactor=c,roughnessFactor=1,metallicFactor=0)) for c in [green,petal]]
        glb(path,foliage(41+i,h,count,i in [2,3],i==4),materials)
        assets.append(dict(Id=key,Source=key+'.glb',Options=dict(GenerateLods=True)))
        profiles.append(dict(Id=key,Asset=key));objects.append(dict(Id=key,Profile=key))
        species.append(dict(Id=key,Model=key,Seed=81+i,Density=density,MaxInstances=500000,GroundCover=True,Flexibility=.65 if i==4 else 1.2,MaterialLayers=[layer],MinHeight=-5,MaxHeight=10,MaxSlope=.55,MeshDistance=20 if i<2 else 28,EndDistance=55 if i<2 else 70,Wind=.22,ImpostorSize=64))
    entity(root,'definitions/assets','model3d_asset',assets);entity(root,'profiles3d','model3d_profile',profiles);entity(root,'objects3d','object3d',objects);entity(root,'terrains/scatter','terrain_scatter',species)
    # Independent view of the existing analytic tile fixture; the source's bytes are never patched.
    world=json.loads((project/'mods_private/terrain_base/test_suites/contracts/terrains/worlds/fixture.json').read_text())['Items'][0]
    world.update(Id='meadow/world',Location='meadow/location',Scatter=[dict(Id=s['Id'],Asset=s['Id']) for s in species],ViewDistance=100)
    entity(root,'terrains/worlds','terrain_world',[world])
    entity(root,'locations3d','location3d',[dict(Id='meadow/location',Scene='meadow',Entry='spawn',Camera=dict(Position=[0,5,14],RotationDegrees=[-16,0,0],Far=250),Points=[dict(Id='spawn',Transform=dict(Position=[0,3,0]),Capacity=100)],Environment=dict(FogDensity=.002),Sky=dict(Preset='sky3d/temperate'))])
    (root/'scenes').mkdir(exist_ok=True);(root/'scenes/meadow.json').write_text(json.dumps(dict(Name='meadow',Shell='3d',SaveAllowed=True)))
    combat=project/'vegetation_combat_source'
    if combat.exists():
        for rel in ['characters3d/independent.json','definitions/actions.json','definitions/animation.json','definitions/controller.json','definitions/library.json','fighters3d/profiles.json']:
            target=root/rel;target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(combat/rel,target)
        weapons=json.loads((combat/'weapons3d/weapons.json').read_text());weapons['Items']=[v for v in weapons['Items'] if v['Id'] in ['combat3d_tests/sword','combat3d_tests/pistol']]
        (root/'weapons3d').mkdir(exist_ok=True);(root/'weapons3d/weapons.json').write_text(json.dumps(weapons))
        shutil.copytree(combat/'models3d',root/'models3d',dirs_exist_ok=True)
    entity(root,'weather','weather',[dict(Id='meadow/calm',Wind=[0,0],Rain=0),dict(Id='meadow/windy',Wind=[18,7],Rain=0),dict(Id='meadow/rain',Wind=[12,5],Rain=1,Wetness=1,Clouds=1)])
    actors=[];fighters=[];characters=[]
    for i in range(25):
        for team in range(2):
            key=f'meadow/fighter_{team}_{i}';pistol=i%5==0
            # Explicit recipes are used by the driver; no Location means the automatic scene owners do not spawn them early.
            actors.append(dict(Id=key,Presentation='combat3d_tests/named',Point='spawn',VisualYawDegrees=180))
            fighters.append(dict(Id=key,Actor=key,Character=key,Team='red' if team==0 else 'blue',Profile='combat3d_tests/shooter' if pistol else 'combat3d_tests/sword',Weapon='combat3d_tests/pistol' if pistol else 'combat3d_tests/sword',Health=100000,Attack=10,ReserveAmmo=40))
            characters.append(dict(Id=key,Name=f'Боец {team}-{i}',Level=1,Health=100000,Attack=10,Defence=0,Point=100,Actions=[]))
    entity(root,'actors3d','actor3d',actors);entity(root,'combat3d','combat3d_fighter',fighters);entity(root,'characters','character',characters)
    provenance.mkdir(parents=True,exist_ok=True)
    (provenance/'models.json').write_text(json.dumps(dict(generator_sha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),models={str(p.relative_to(root)):hashlib.sha256(p.read_bytes()).hexdigest() for p in (root/'models3d').rglob('*.glb')},source='Original analytic ribbons and six-petal flowers; one source model per species; all LODs/cache impostors derived at runtime.'),indent=2))
