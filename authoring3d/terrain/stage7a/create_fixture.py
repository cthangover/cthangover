"""Independent procedural runtime-only fixtures. Host watcher owns generation; no Godot editor import/Blender/source art enters a mod."""
import argparse
import json
import math
from pathlib import Path
import struct
import hashlib
from PIL import Image


def entity(root, folder, kind, items):
    path = root / folder / 'fixture.json'
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps({'Kind': kind, 'Items': items}, indent=2) + '\n', encoding='utf-8', newline='\n')


def height(x, z):
    # Near flat ground, an exposed ridge farther away and continuous negative-coordinate samples.
    return 2 + 2*math.sin(x*.035)*math.sin(z*.025) + 22*math.exp(-((x-42)/9)**2-((z+22)/28)**2)


def glb(path, meshes, materials):
    binary = bytearray()
    views, accessors, primitives = [], [], []
    for positions, normals, uvs, indices, material in meshes:
        primitive = {'attributes': {}, 'material': material}
        for semantic, values, components in [('POSITION', positions, 3), ('NORMAL', normals, 3), ('TEXCOORD_0', uvs, 2), ('indices', indices, 1)]:
            while len(binary) % 4:
                binary.append(0)
            start = len(binary)
            fmt = 'I' if semantic == 'indices' else 'f'
            flat = values if components == 1 else [v for row in values for v in row]
            binary.extend(struct.pack('<' + fmt*len(flat), *flat))
            views.append({'buffer': 0, 'byteOffset': start, 'byteLength': len(binary)-start})
            acc = {'bufferView': len(views)-1, 'componentType': 5125 if components == 1 else 5126, 'count': len(values), 'type': {1:'SCALAR',2:'VEC2',3:'VEC3'}[components]}
            if semantic == 'POSITION':
                acc['min'] = [min(v[i] for v in positions) for i in range(3)]
                acc['max'] = [max(v[i] for v in positions) for i in range(3)]
            accessors.append(acc)
            if semantic == 'indices':
                primitive['indices'] = len(accessors)-1
            else:
                primitive['attributes'][semantic] = len(accessors)-1
        primitives.append(primitive)
    data = {'asset': {'version':'2.0','generator':'cthangover independent terrain fixture'}, 'buffers':[{'byteLength':len(binary)}],
            'bufferViews':views,'accessors':accessors,'materials':materials,'meshes':[{'primitives':primitives}],
            'nodes':[{'mesh':0,'name':'TerrainFixtureModel'}],'scenes':[{'nodes':[0]}],'scene':0}
    encoded = json.dumps(data, separators=(',',':')).encode()
    encoded += b' ' * (-len(encoded) % 4)
    binary.extend(b'\0' * (-len(binary) % 4))
    result = struct.pack('<4sII',b'glTF',2,12+8+len(encoded)+8+len(binary))+struct.pack('<I4s',len(encoded),b'JSON')+encoded+struct.pack('<I4s',len(binary),b'BIN\0')+binary
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(result)


def sphere(cx,cy,cz,rx,ry,rz,material):
    p,n,uv,indices=[],[],[],[]
    rings, segments = 18, 32
    for j in range(rings+1):
        v=j/rings
        for i in range(segments+1):
            u=i/segments
            x=math.sin(v*math.pi)*math.cos(u*2*math.pi); y=math.cos(v*math.pi); z=math.sin(v*math.pi)*math.sin(u*2*math.pi)
            p.append((cx+x*rx,cy+y*ry,cz+z*rz)); normal=(x/rx,y/ry,z/rz); length=math.sqrt(sum(a*a for a in normal));n.append(tuple(a/length for a in normal));uv.append((u,v))
    for j in range(rings):
        for i in range(segments):
            a=j*(segments+1)+i;indices.extend([a,a+1,a+segments+1,a+1,a+segments+2,a+segments+1])
    return p,n,uv,indices,material


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project',required=True,type=Path)
    parser.add_argument('--provenance',required=True,type=Path)
    args=parser.parse_args();root=args.project/'mods_private/terrain_base/test_suites/contracts'
    terrain=root/'terrain/terrain_fixture';terrain.mkdir(parents=True,exist_ok=True)
    tiles=[];regions=[];region_refs=[]
    # Four 64 m regions / sixteen independently patchable 32 m tiles.
    for rz in [-1,0]:
        for rx in [-1,0]:
            links=[]
            for tz in [0,32]:
                for tx in [0,32]:
                    x0=rx*64+tx;z0=rz*64+tz;name=f't_{x0}_{z0}'
                    heights=[];holes=bytearray();weights=bytearray();density=bytearray()
                    for z in range(z0,z0+33):
                        for x in range(x0,x0+33):
                            h=height(x,z);heights.append(round((h+16)/.001))
                            hole=9<=x<=15 and 5<=z<=11;holes.append(int(hole))
                            rock=max(0,min(1,(h-5)/10));sand=max(0,min(1,(-x-35)/25));mud=max(0,1-abs(z-24)/3)*.6;grass=max(0,1-rock-sand-mud)
                            total=rock+sand+mud+grass;w=[round(v/total*255) for v in [grass,rock,sand,mud]];w[max(range(4),key=w.__getitem__)]+=255-sum(w);weights.extend(w)
                            density.append(0 if hole or abs(z-24)<3 else 255)
                    (terrain/(name+'.r16')).write_bytes(struct.pack('<'+'H'*len(heights),*heights))
                    (terrain/(name+'.holes')).write_bytes(holes);(terrain/(name+'.weights')).write_bytes(weights);(terrain/(name+'.density')).write_bytes(density)
                    tiles.append({'Id':'terrain_fixture/'+name,'Heights':'terrain_fixture/'+name+'.r16','Encoding':'r16','Origin':-16,'Step':.001,'Holes':'terrain_fixture/'+name+'.holes','Weights':'terrain_fixture/'+name+'.weights','Density':'terrain_fixture/'+name+'.density'})
                    links.append({'Id':name,'Asset':'terrain_fixture/'+name,'X':tx,'Z':tz})
            rid=f'terrain_fixture/r_{rx}_{rz}';regions.append({'Id':rid,'X':rx,'Z':rz,'Tiles':links});region_refs.append({'Id':rid,'Asset':rid})
    patch_dir=args.project/'mods_private/terrain_base/test_suites/patch/terrain/terrain_patch';patch_dir.mkdir(parents=True,exist_ok=True)
    patched=[]
    for z in range(33):
        for x in range(33):
            value=-16+round((height(x,z)+16)/.001)*.001
            if x==4 and z==4:value+=2
            patched.append(value)
    (patch_dir/'raised.r32').write_bytes(struct.pack('<'+'f'*len(patched),*patched))
    entity(root,'terrains/tiles','terrain_tile',tiles);entity(root,'terrains/regions','terrain_region',regions)
    base={'Id':'terrain_fixture/world','Location':'terrain_fixture/location','Material':'terrain_fixture/land','RegionCells':64,'TileCells':32,'PatchCells':16,'MaxLod':2,'ViewDistance':100,'CollisionRadius':12,'Regions':region_refs,'Scatter':[{'Id':'trees','Asset':'terrain_fixture/trees'},{'Id':'grass','Asset':'terrain_fixture/grass'}]}
    entity(root,'terrains/worlds','terrain_world',[base])
    for slot,color in [('grass',(69,105,37)),('rock',(109,107,99)),('sand',(160,144,103)),('mud',(83,65,45))]:
        image=Image.new('RGBA',(128,128));pixels=image.load()
        for y in range(128):
            for x in range(128):
                noise=((x*73856093^y*19349663)%31)-15;wave=7*math.sin(x*.21)*math.cos(y*.19)
                pixels[x,y]=tuple(max(0,min(255,round(c+noise+wave))) for c in color)+(255,)
        image.save(terrain/(slot+'.png'));Image.new('RGBA',(128,128),(128,128,255,255)).save(terrain/(slot+'_normal.png'));Image.new('RGBA',(128,128),(255,230,0,255)).save(terrain/(slot+'_orm.png'))
    layers=[{'Id':slot,'Albedo':'terrain_fixture/'+slot+'.png','Normal':'terrain_fixture/'+slot+'_normal.png','Orm':'terrain_fixture/'+slot+'_orm.png'} for slot in ['grass','rock','sand','mud']]
    entity(root,'terrains/materials','terrain_material',[{'Id':'terrain_fixture/land','TextureSize':128,'Layers':layers,'TriplanarSlope':.9,'FarDistance':100}])
    model_dir=root/'models3d/terrain_fixture'
    glb(model_dir/'tree.glb',[sphere(0,2.0,0,.19,2,.19,0),sphere(0,4,0,1.4,1.8,1.4,1),sphere(.8,4.4,.2,1.0,1.2,1.0,1)],
        [{'pbrMetallicRoughness':{'baseColorFactor':[.24,.13,.05,1],'roughnessFactor':1,'metallicFactor':0}}, {'pbrMetallicRoughness':{'baseColorFactor':[.12,.30,.06,1],'roughnessFactor':1,'metallicFactor':0}}])
    p=[];n=[];uv=[];idx=[]
    for i in range(3):
        angle=i*math.pi/3;dx=.18*math.cos(angle);dz=.18*math.sin(angle);a=len(p)
        p.extend([(-dx,0,-dz),(dx,0,dz),(dx,.6,dz),(-dx,.6,-dz)]);n.extend([(math.sin(angle),0,math.cos(angle))]*4);uv.extend([(0,1),(1,1),(1,0),(0,0)]);idx.extend([a,a+1,a+2,a,a+2,a+3])
    glb(model_dir/'grass.glb',[(p,n,uv,idx,0)],[{'doubleSided':True,'pbrMetallicRoughness':{'baseColorFactor':[.18,.36,.08,1],'roughnessFactor':1,'metallicFactor':0}}])
    entity(root,'definitions/assets','model3d_asset',[{'Id':'terrain_fixture/tree','Source':'terrain_fixture/tree.glb','Options':{'GenerateLods':True}},{'Id':'terrain_fixture/grass','Source':'terrain_fixture/grass.glb','Options':{'GenerateLods':True}}])
    entity(root,'terrains/scatter','terrain_scatter',[{'Id':'terrain_fixture/trees','Model':'terrain_fixture/tree','Seed':31,'Density':.012,'MaxInstances':500,'MeshDistance':32,'EndDistance':130,'Wind':.1,'ImpostorSize':128}, {'Id':'terrain_fixture/grass','Model':'terrain_fixture/grass.glb','Seed':32,'Density':2,'MaxInstances':40000,'MeshDistance':22,'EndDistance':75,'Wind':.12,'ImpostorSize':64}])
    (root/'scenes/terrain_fixture.json').write_text('{"Name":"terrain_fixture_scene","Shell":"3d","SaveAllowed":true}\n', newline='\n')
    (root/'scenes/terrain_empty.json').write_text('{"Name":"terrain_fixture_empty","Shell":"3d","SaveAllowed":true}\n', newline='\n')
    entity(root,'locations3d','location3d',[{'Id':'terrain_fixture/location','Scene':'terrain_fixture_scene','Entry':'spawn','Camera':{'Position':[40,30,48],'RotationDegrees':[-28,40,0]},'Points':[{'Id':'spawn','Transform':{'Position':[0,5,0]},'Capacity':100}], 'Environment':{'FogDensity':.003}}, {'Id':'terrain_fixture/location_empty','Scene':'terrain_fixture_empty','Entry':'spawn','Points':[{'Id':'spawn','Capacity':1}]}])
    args.provenance.mkdir(parents=True,exist_ok=True)
    (args.provenance/'fixture-provenance.json').write_text(json.dumps({'generator':str(Path(__file__).resolve()),'description':'Independent procedural terrain/material/static single-model GLBs; no external reference art', 'height_formula':'2+2*sin(.035*x)*sin(.025*z)+22*exp(-((x-42)/9)^2-((z+22)/28)^2)','sha256':{str(p.relative_to(root)):hashlib.sha256(p.read_bytes()).hexdigest() for p in root.rglob('*') if p.is_file()}},indent=2))
    print('Terrain fixture generated:',len(tiles),'tiles, 4 layers, 2 single-model scatter prototypes')


if __name__=='__main__':main()
