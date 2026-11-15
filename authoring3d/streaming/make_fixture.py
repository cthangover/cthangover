"""Original analytic 2048 metre island, generated inside the isolated host project."""
import argparse
import hashlib
import json
import math
from pathlib import Path
import struct
import sys
from PIL import Image


def height(x, z):
    return 4 + 2*math.sin(x*.007)*math.sin(z*.009) + 42*math.exp(-((x-480)/190)**2-((z-350)/220)**2)


def entity(root, folder, kind, rows):
    target = root/folder/'fixture.json'
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(json.dumps(dict(Kind=kind, Items=rows)), encoding='utf-8')


def generate(project, provenance, hole=False):
    root = project/'mods_private/test_stream_island'
    root.mkdir(parents=True,exist_ok=True)
    (root/'manifest.json').write_text(json.dumps(dict(id='test_stream_island',name='Analytic island fixture',version='0.1.0',depends=['world_stream3d_base>=0.1.0'])))
    sources = root/'terrain/stream_island'
    sources.mkdir(parents=True, exist_ok=True)
    tiles, regions, refs = [], [], []
    for rz in range(-4, 4):
        for rx in range(-4, 4):
            links = []
            for dz in range(0, 256, 64):
                for dx in range(0, 256, 64):
                    x0, z0 = rx*256+dx, rz*256+dz
                    name = f't_{x0}_{z0}'
                    values = [round((height(x,z)+32)*512) for z in range(z0,z0+65) for x in range(x0,x0+65)]
                    if hole:(sources/(name+'.holes')).write_bytes(bytes(int(x==-880 and z==-880) for z in range(z0,z0+65) for x in range(x0,x0+65)))
                    (sources/(name+'.density')).write_bytes(bytes(255 if 550<x<1000 and -800<z<850 else 0 for z in range(z0,z0+65) for x in range(x0,x0+65)))
                    (sources/(name+'.r16')).write_bytes(struct.pack('<'+'H'*len(values), *values))
                    tiles.append(dict(Id='stream_island/'+name, Heights='stream_island/'+name+'.r16', Encoding='r16', Origin=-32, Step=1/512,Density='stream_island/'+name+'.density',Holes='stream_island/'+name+'.holes' if hole else None))
                    links.append(dict(Id=name, Asset='stream_island/'+name, X=dx, Z=dz))
            rid = f'stream_island/r_{rx}_{rz}'
            regions.append(dict(Id=rid, X=rx, Z=rz, Tiles=links))
            refs.append(dict(Id=rid, Asset=rid))
    entity(root,'terrains/tiles','terrain_tile',tiles)
    entity(root,'terrains/regions','terrain_region',regions)
    entity(root,'terrains/materials','terrain_material',[dict(Id='stream_island/land',TextureSize=32,Layers=[dict(Id='grass',Albedo='stream_island/grass.png')])])
    entity(root,'terrains/worlds','terrain_world',[dict(Id='stream_island/terrain', Material='stream_island/land', RegionCells=256,TileCells=64,PatchCells=32,MaxLod=3, ViewDistance=384, Regions=refs,Scatter=[dict(Id='forest',Asset='stream_island/forest')])])
    image=Image.new('RGBA',(32,32));pixels=image.load()
    for z in range(32):
        for x in range(32):
            noise=((x*73856093^z*19349663)%19)-9;pixels[x,z]=(68+noise,100+noise,43+noise,255)
    image.save(sources/'grass.png')
    sys.path.insert(0,str(Path(__file__).resolve().parent.parent/'terrain/stage7a'))
    from create_fixture import glb,sphere
    glb(root/'models3d/stream_island/tree.glb',[sphere(0,1.5,0,.16,1.5,.16,0),sphere(0,3.5,0,1.3,1.7,1.3,1)],[dict(pbrMetallicRoughness=dict(baseColorFactor=[.24,.13,.05,1],roughnessFactor=1,metallicFactor=0)),dict(pbrMetallicRoughness=dict(baseColorFactor=[.12,.30,.06,1],roughnessFactor=1,metallicFactor=0))])
    entity(root,'definitions/assets','model3d_asset',[dict(Id='stream_island/tree',Source='stream_island/tree.glb',Options=dict(GenerateLods=True))])
    entity(root,'objects3d','object3d',[dict(Id='stream_island/tree',Profile='stream_island/tree')])
    entity(root,'profiles3d','model3d_profile',[dict(Id='stream_island/tree',Asset='stream_island/tree')])
    entity(root,'terrains/scatter','terrain_scatter',[dict(Id='stream_island/forest',Model='stream_island/tree',Seed=47,Density=.003,MaxInstances=250,MeshDistance=55,EndDistance=300,Wind=.07,ImpostorSize=128)])
    entity(root,'worlds3d','world_stream3d',[dict(Id='stream_island/world',Location='stream_island/location',Terrain='stream_island/terrain',ResidentLimit=16,Regions=[dict(Id='stream_island/r_0_0',Pin=True,Priority=-5)])])
    entity(root,'locations3d','location3d',[
        dict(Id='stream_island/location', Scene='stream_island',Entry='spawn', Camera=dict(Position=[-850,35,-840], RotationDegrees=[-25,35,0],Far=1200), Points=[dict(Id='spawn', Transform=dict(Position=[-900,9,-900]),Capacity=100)], Environment=dict(FogDensity=.0015)),
        dict(Id='stream_island/empty',Scene='stream_empty',Entry='spawn',Points=[dict(Id='spawn')])])
    (root/'scenes').mkdir(exist_ok=True)
    for scene in ['stream_island','stream_empty']:
        (root/'scenes'/f'{scene}.json').write_text(json.dumps(dict(Name=scene,Shell='3d',SaveAllowed=True)))
    provenance.mkdir(parents=True, exist_ok=True)
    (provenance/'fixture.json').write_text(json.dumps(dict(formula='4+2*sin(.007*x)*sin(.009*z)+42*exp(-((x-480)/190)^2-((z-350)/220)^2)', bounds=[-1024,1024], spacing=1,step=1/512, tiles=len(tiles), regions=len(regions), bytes=sum(p.stat().st_size for p in sources.iterdir()), generator_sha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest()),indent=2))


if __name__ == '__main__':
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--project',type=Path,required=True)
    p.add_argument('--provenance',type=Path,required=True)
    a=p.parse_args()
    generate(a.project,a.provenance)
