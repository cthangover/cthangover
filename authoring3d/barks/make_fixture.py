"""Original low-detail skinned box figure for spatial speech tests; no external model inputs."""
from pathlib import Path
import json
import struct


def main():
    binary = bytearray()
    views = []
    accessors = []

    def add(values, fmt, kind, count, bounds=None):
        while len(binary) % 4:
            binary.append(0)
        offset = len(binary)
        binary.extend(struct.pack('<'+fmt*len(values), *values))
        views.append(dict(buffer=0, byteOffset=offset, byteLength=len(binary)-offset))
        item = dict(bufferView=len(views)-1, componentType=5126 if fmt == 'f' else 5123, count=count, type=kind)
        if bounds:
            item.update(min=bounds[0], max=bounds[1])
        accessors.append(item)
        return len(accessors)-1

    positions, normals, joints, weights = [], [], [], []
    boxes = [((0,.75,0),(.7,1.1,.4),0), ((0,1.6,0),(.46,.46,.46),1), ((0,1.6,.29),(.14,.12,.18),1), ((.62,1,0),(.18,.18,.18),2)]
    faces = [((1,0,0), [(1,-1,-1),(1,1,-1),(1,1,1),(1,-1,1)]), ((-1,0,0),[(-1,-1,1),(-1,1,1),(-1,1,-1),(-1,-1,-1)]), ((0,1,0),[(-1,1,-1),(-1,1,1),(1,1,1),(1,1,-1)]), ((0,-1,0),[(-1,-1,1),(-1,-1,-1),(1,-1,-1),(1,-1,1)]), ((0,0,1),[(1,-1,1),(1,1,1),(-1,1,1),(-1,-1,1)]), ((0,0,-1),[(-1,-1,-1),(-1,1,-1),(1,1,-1),(1,-1,-1)])]
    for centre, size, bone in boxes:
        for normal, corners in faces:
            for index in [0,1,2,0,2,3]:
                positions += [centre[i]+corners[index][i]*size[i]/2 for i in range(3)]
                normals += normal
                joints += [bone,0,0,0]
                weights += [1,0,0,0]
    n = len(positions)//3
    pos = add(positions,'f','VEC3',n,([min(positions[i::3]) for i in range(3)],[max(positions[i::3]) for i in range(3)]))
    normal = add(normals,'f','VEC3',n)
    joint = add(joints,'H','VEC4',n)
    weight = add(weights,'f','VEC4',n)
    matrices = []
    for translation in [(0,0,0),(0,1.6,0),(.6,1,0)]:
        matrices += [1,0,0,0,0,1,0,0,0,0,1,0,-translation[0],-translation[1],-translation[2],1]
    inverse = add(matrices,'f','MAT4',3)
    times = add([0,1,2],'f','SCALAR',3,([0],[2]))
    idle = add([0,0,0]*3,'f','VEC3',3)
    talk = add([0,0,0,1,0,0,.3,.953939201,0,0,0,1],'f','VEC4',3)
    document = dict(asset=dict(version='2.0',generator='Spatial speech original fixture'), scene=0, scenes=[dict(nodes=[0])], nodes=[dict(name='Person',children=[1,4]),dict(name='Root',children=[2,3]),dict(name='Head',translation=[0,1.6,0]),dict(name='Hand',translation=[.6,1,0]),dict(name='Body',mesh=0,skin=0)], skins=[dict(joints=[1,2,3],skeleton=1,inverseBindMatrices=inverse)], meshes=[dict(name='SimpleFigure',primitives=[dict(attributes=dict(POSITION=pos,NORMAL=normal,JOINTS_0=joint,WEIGHTS_0=weight),material=0)])], materials=[dict(name='BlueCloth',pbrMetallicRoughness=dict(baseColorFactor=[.25,.55,.72,1],metallicFactor=0,roughnessFactor=.8))], animations=[dict(name='idle',samplers=[dict(input=times,output=idle)],channels=[dict(sampler=0,target=dict(node=1,path='translation'))]),dict(name='talk',samplers=[dict(input=times,output=talk)],channels=[dict(sampler=0,target=dict(node=3,path='rotation'))])], buffers=[dict(byteLength=len(binary))], bufferViews=views, accessors=accessors)
    encoded=json.dumps(document,separators=(',',':')).encode()
    encoded += b' '*(-len(encoded)%4)
    binary += b'\0'*(-len(binary)%4)
    result=struct.pack('<III',0x46546c67,2,12+8+len(encoded)+8+len(binary))+struct.pack('<II',len(encoded),0x4e4f534a)+encoded+struct.pack('<II',len(binary),0x004e4942)+binary
    root=Path('mods_private/barks3d_base/test_suites/contracts')
    (root/'models3d').mkdir(exist_ok=True)
    (root/'models3d/barks3d_dummy.glb').write_bytes(result)
    (root/'models3d/recipe.json').write_text(json.dumps(dict(Kind='model3d_asset',Items=[dict(Id='barks3d_test/dummy',Source='barks3d_dummy.glb')]),indent=2)+'\n')
    path=root/'rigs3d/data.json'
    rigs=json.loads(path.read_text())
    for rig in rigs['Items']:
        rig['Asset']='barks3d_test/dummy'
    path.write_text(json.dumps(rigs,indent=2)+'\n')
    import math
    import soundfile
    samples = [.14 * math.sin(2*math.pi*(210*(i/22050)+24*(i/22050)**2)) * math.sin(math.pi*(i/22050)/.24)**2 for i in range(int(22050*.24))]
    (root/'sounds').mkdir(exist_ok=True)
    soundfile.write(root/'sounds/speech_tick.ogg', samples, 22050, format='OGG', subtype='VORBIS')
    old=root/'scenes/barks3d_dummy.tscn' 
    if old.exists():
        old.unlink()


if __name__=='__main__':
    main()
