"""Original geometric interactive room plus a two-bone chest. No external game resources/code."""
from pathlib import Path
import json,math,struct,shutil,hashlib
root=Path(__file__).resolve().parents[2];suite=root/'mods_private/interactive3d_base/test_suites/contracts'
def write(path,data):
 path.parent.mkdir(parents=True,exist_ok=True);path.write_text(json.dumps(data,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
def entity(kind,name,items):write(suite/'definitions'/name,{'Kind':kind,'Items':items})
def vector(values):return ', '.join(map(str,values))
def scene(name,parts):
 text=['[gd_scene format=3]'];nodes=[]
 for i,(label,pos,size,color,collide) in enumerate(parts):
  text += [f'[sub_resource type="BoxMesh" id="M{i}"]',f'size = Vector3({vector(size)})',f'[sub_resource type="StandardMaterial3D" id="C{i}"]',f'albedo_color = Color({vector(color)}, 1)','roughness = 0.8']
  if collide:text += [f'[sub_resource type="BoxShape3D" id="S{i}"]',f'size = Vector3({vector(size)})']
  nodes += [f'[node name="{label}" type="Node3D" parent="."]',f'position = Vector3({vector(pos)})',f'[node name="Mesh" type="MeshInstance3D" parent="{label}"]',f'mesh = SubResource("M{i}")',f'material_override = SubResource("C{i}")']
  if collide:nodes += [f'[node name="Body" type="StaticBody3D" parent="{label}"]',f'[node name="Shape" type="CollisionShape3D" parent="{label}/Body"]',f'shape = SubResource("S{i}")']
 path=suite/'scenes'/f'{name}.tscn';path.parent.mkdir(parents=True,exist_ok=True);path.write_text('\n\n'.join(text+['[node name="Furniture" type="Node3D"]']+nodes)+'\n')
wood=[.28,.12,.045];iron=[.13,.15,.18]
scene('floor',[('Floor',[0,-.2,0],[24,.4,20],[.28,.31,.28],True)])
scene('bench',[('Seat',[0,.48,0],[3,.18,.65],wood,True),('Back',[0,.9,-.28],[3,.7,.12],wood,True),('Left',[-1,.2,0],[.15,.4,.5],wood,True),('Right',[1,.2,0],[.15,.4,.5],wood,True)])
scene('door',[('Panel',[.6,1.15,0],[1.2,2.3,.16],wood,True),('Handle',[1,1.1,.13],[.2,.12,.12],[.8,.55,.1],False)])
scene('lever',[('Base',[0,.15,0],[.7,.3,.6],iron,True),('Handle',[0,.65,0],[.12,1,.12],wood,False)])
scene('anvil',[('Base',[0,.25,0],[1,.5,.7],wood,True),('Top',[0,.7,0],[1.3,.4,.6],iron,True)])
# Original two-joint skinned chest, with actual visible lid rotation.
blob=bytearray();views=[];access=[]
def acc(values,kind,component=5126,minimum=None,maximum=None):
 while len(blob)%4:blob.append(0)
 start=len(blob);width={'SCALAR':1,'VEC3':3,'VEC4':4,'MAT4':16}[kind];fmt={5126:'f',5123:'H'}[component];flat=[v for row in values for v in (row if isinstance(row,(list,tuple)) else [row])];blob.extend(struct.pack('<'+fmt*len(flat),*flat));vi=len(views);views.append({'buffer':0,'byteOffset':start,'byteLength':len(blob)-start});a={'bufferView':vi,'componentType':component,'count':len(values),'type':kind}
 if minimum is not None:a['min']=minimum;a['max']=maximum
 access.append(a);return len(access)-1
positions=[];normals=[];joints=[];weights=[];indices=[]
def cube(center,size,joint):
 faces=[((1,0,0),[(1,-1,-1),(1,1,-1),(1,1,1),(1,-1,1)]),((-1,0,0),[(-1,-1,1),(-1,1,1),(-1,1,-1),(-1,-1,-1)]),((0,1,0),[(-1,1,-1),(-1,1,1),(1,1,1),(1,1,-1)]),((0,-1,0),[(-1,-1,1),(-1,-1,-1),(1,-1,-1),(1,-1,1)]),((0,0,1),[(1,-1,1),(1,1,1),(-1,1,1),(-1,-1,1)]),((0,0,-1),[(-1,-1,-1),(-1,1,-1),(1,1,-1),(1,-1,-1)])]
 for n,corners in faces:
  start=len(positions)
  for corner in corners:positions.append([center[d]+corner[d]*size[d]/2 for d in range(3)]);normals.append(n);joints.append([joint,0,0,0]);weights.append([1,0,0,0])
  indices.extend([start,start+1,start+2,start,start+2,start+3])
cube([0,.25,0],[1,.5,.7],0);cube([0,.57,0],[1,.12,.7],1)
pa=acc(positions,'VEC3',minimum=[-.5,0,-.35],maximum=[.5,.63,.35]);na=acc(normals,'VEC3');ja=acc(joints,'VEC4',5123);wa=acc(weights,'VEC4');ia=acc(indices,'SCALAR',5123)
for ai in [pa,na,ja,wa]:views[access[ai]['bufferView']]['byteStride']={'VEC3':12,'VEC4':8 if access[ai]['componentType']==5123 else 16}[access[ai]['type']]
identity=[1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1];inverse=identity.copy();inverse[13]=-.51;inverse[14]=.35;ib=acc([identity,inverse],'MAT4');times=acc([0,.5,1],'SCALAR',minimum=[0],maximum=[1]);rot=acc([[0,0,0,1],[math.sin(-math.pi*.28),0,0,math.cos(math.pi*.28)],[math.sin(-math.pi*.28),0,0,math.cos(math.pi*.28)]],'VEC4')
gltf={'asset':{'version':'2.0','generator':'cthangover original interactive3d fixture'},'scene':0,'scenes':[{'nodes':[0,3]}],'nodes':[{'name':'ChestRig','children':[1]},{'name':'Root','children':[2]},{'name':'Lid','translation':[0,.51,-.35]},{'name':'Chest','mesh':0,'skin':0}],'meshes':[{'primitives':[{'attributes':{'POSITION':pa,'NORMAL':na,'JOINTS_0':ja,'WEIGHTS_0':wa},'indices':ia,'material':0}]}],'materials':[{'pbrMetallicRoughness':{'baseColorFactor':[.4,.17,.06,1],'metallicFactor':0,'roughnessFactor':.75}}],'skins':[{'joints':[1,2],'skeleton':1,'inverseBindMatrices':ib}],'animations':[{'name':'Open','samplers':[{'input':times,'output':rot,'interpolation':'LINEAR'}],'channels':[{'sampler':0,'target':{'node':2,'path':'rotation'}}]}],'buffers':[{'byteLength':len(blob)}],'bufferViews':views,'accessors':access}
j=json.dumps(gltf,separators=(',',':')).encode();j+=b' '*((-len(j))%4);blob+=b'\0'*((-len(blob))%4);glb=struct.pack('<III',0x46546c67,2,28+len(j)+len(blob))+struct.pack('<II',len(j),0x4e4f534a)+j+struct.pack('<II',len(blob),0x004e4942)+blob
models=suite/'models3d/interactive3d_fixture';models.mkdir(parents=True,exist_ok=True);(models/'chest.glb').write_bytes(glb)
for name in ['library_a.glb','target.glb']:shutil.copy2(root/'mods_private/actor3d_base/test_suites/contracts/models3d/actor3d_fixture'/name,models/name)
entity('character3d_body_type','bodies.json',[{'Id':'interactive3d_tests/chest_body','Root':'Root','Roles':[{'Id':'Root','Required':True},{'Id':'Lid','Parent':'Root','Required':True}]}])
entity('character3d_rig','rigs.json',[{'Id':'interactive3d_tests/source','Asset':'interactive3d_fixture/library_a.glb'},{'Id':'interactive3d_tests/target','Asset':'interactive3d_fixture/target.glb'},{'Id':'interactive3d_tests/chest_rig','Asset':'interactive3d_fixture/chest.glb','BodyType':'interactive3d_tests/chest_body','AutoMap':False,'Bones':[{'Id':'Root','Bone':'Root'},{'Id':'Lid','Bone':'Lid'}]}])
roles=['idle','walk','run','jump','use.enter','use','use.exit']
entity('anim3d_library','library.json',[{'Id':'interactive3d_tests/library','Rig':'interactive3d_tests/source','RootBone':'Hips','Clips':[{'Id':'idle','Source':'Walk','HoldSeconds':1,'Loop':True},{'Id':'walk','Source':'Walk','Loop':True}]+[{'Id':role,'Source':'Sword','Loop':role=='use','Events':[{'Id':'entered' if role=='use.enter' else 'confirm' if role=='use' else 'left','Kind':'interactive','Frame':12 if role=='use' else 6}]} for role in ['use.enter','use','use.exit']]},{'Id':'interactive3d_tests/chest_library','Rig':'interactive3d_tests/chest_rig','RootBone':'Root','Clips':[{'Id':'closed','Source':'Open','HoldSeconds':1,'Loop':True},{'Id':'open','Source':'Open','Events':[{'Id':'ready','Kind':'interactive','Frame':12}]},{'Id':'opened','Source':'Open','FirstFrame':24,'HoldSeconds':1,'Loop':True}]}])
entity('anim3d_controller','controllers.json',[{'Id':'interactive3d_tests/actor','Rig':'interactive3d_tests/target','Library':'interactive3d_tests/library','Roles':[{'Id':r,'Clip':r if r.startswith('use') else 'walk' if r in ['walk','run'] else 'idle'} for r in roles],'States':[{'Id':r.replace('.','_'),'Role':r} for r in roles]},{'Id':'interactive3d_tests/chest','Rig':'interactive3d_tests/chest_rig','Library':'interactive3d_tests/chest_library','Roles':[{'Id':'chest.closed','Clip':'closed'},{'Id':'chest.open','Clip':'open'},{'Id':'chest.opened','Clip':'opened'}],'States':[{'Id':'closed_node','Role':'chest.closed'},{'Id':'opening_node','Role':'chest.open'},{'Id':'opened_node','Role':'chest.opened'}]}])
entity('character3d_presentation','characters.json',[{'Id':'interactive3d_tests/actor','Rig':'interactive3d_tests/target','Controller':'interactive3d_tests/actor','Sockets':[{'Id':'weapon.hand','Bone':'RightHand'}],'Hitboxes':[{'Id':'body','Bone':'Spine','Shape':'capsule','Radius':.2,'Height':.6}],'Lods':[{'Id':'near','Distance':0},{'Id':'far','Distance':8,'AnimationHz':4,'SkeletonHz':4}]}])
objects=['floor','bench','door','lever','anvil']
entity('model3d_profile','profiles.json',[{'Id':'interactive3d_tests/'+name,'Asset':'scenes/'+name+'.tscn'} for name in objects])
entity('object3d','objects.json',[{'Id':'interactive3d_tests/'+name,'Profile':'interactive3d_tests/'+name} for name in objects])
points=[{'Id':'spawn','Parent':'deck','Capacity':8,'Spacing':1.2,'Transform':{'Position':[-7,0,5]}}]
for name,pos,capacity in [('bench',[-6,0,-.7],3),('chest',[-1,0,-.8],1),('door',[3,0,-.5],1),('lever',[5,0,2],1),('anvil',[0,0,3],1),('blocked',[3,0,-3],1)]:points.append({'Id':name,'Parent':'deck','Capacity':capacity,'Spacing':1,'Transform':{'Position':pos}})
location={'Id':'interactive3d_tests/room','Scene':'interactive3d_room','Entry':'spawn','Frames':[{'Id':'deck','Movable':True},{'Id':'chest.frame','Parent':'deck','Transform':{'Position':[-1,0,-2]}}],'Objects':[{'Id':name,'Object':'interactive3d_tests/'+name,'Parent':'deck','Transform':{'Position':pos}} for name,pos in [('floor',[0,0,0]),('bench',[-5,0,-2]),('door',[2.4,0,-2]),('lever',[5,0,.8]),('anvil',[0,0,1.7])]],'Points':points,'Surfaces':[{'Id':'walk','Parent':'deck','Vertices':[[-12,0,-10],[-12,0,10],[12,0,10],[12,0,-10]],'Polygons':[[0,1,2,3]]}],'Camera':{'Position':[12,12,15],'RotationDegrees':[-35,35,0]}}
entity('location3d','location.json',[location]);write(suite/'scenes/interactive3d_room.json',{'Name':'interactive3d_room','Shell':'3d','SaveAllowed':True})
entity('actor3d','actors.json',[{'Id':name,'Location':location['Id'],'Presentation':'interactive3d_tests/actor','Point':'spawn','Player':name=='player','VisualYawDegrees':180} for name in ['player','npc.a','npc.b']])
def action(id='use',from_='idle',to='idle',hold=False,commands=None):return {'Id':id,'From':from_,'To':to,'Hold':hold,'Commands':commands,'Enter':{'ActorRole':'use.enter','Event':'entered'},'Loop':{'ActorRole':'use','Event':'confirm'},'Exit':{'ActorRole':'use.exit','Event':'left'}}
bench=action(hold=True,commands='flag self.used true')
chest=action('open','closed','open',commands='flag self.open true');chest['Enter']={'ActorRole':'use.enter','ObjectRole':'chest.open','Event':'ready','Source':'object'};chest['Loop']['ObjectRole']='chest.opened';chest['Exit']['ObjectRole']='chest.opened';chest['Once']=True;chest['Commands']+='\naction item.add id=interactive3d_tests/token count=1'
chest['Requirements']=[{'Id':'key',"Condition":"item('interactive3d_tests/key') >= 1",'Reason':'interactive3d/reason/key'}]
door=action('open','closed','open',commands='flag self.open true');close=action('close','open','closed',commands='flag self.open false')
lever=action();lever['Scenario']='interactive3d_tests/lever'
scenario=suite/'scenarios/interactive3d_tests/lever.scenario';scenario.parent.mkdir(parents=True,exist_ok=True);scenario.write_text('flag self.scenario true\naction stat.add who=${interactive_actor} stat=interactive3d_smith value=1\naction interactive3d.state object=interactive3d_tests/door state=open\nend\n')
anvil=action(commands='action item.remove id=interactive3d_tests/ore count=1\naction item.add id=interactive3d_tests/ingot count=1')
anvil['Requirements']=[{'Id':'material','Condition':"item('interactive3d_tests/ore') >= 1",'Reason':'interactive3d/reason/material'}]
items=[]
for name,states,actions,shared in [('bench',['idle'],[bench],False),('chest',['closed','open'],[chest],True),('door',['closed','open'],[door,close],True),('lever',['idle'],[lever],True),('anvil',['idle'],[anvil],True)]:
 item={'Id':'interactive3d_tests/'+name,'Location':location['Id'],'Kind':name,'Parent':'deck','Initial':states[0],'Shared':shared,'States':[{'Id':s} for s in states],'Seats':[{'Id':'users','Point':name}],'Actions':actions}
 if name!='chest':item['Target']=name
 if name=='door':item['States'][1]['Transform']={'RotationDegrees':[0,90,0]}
 if name=='chest':
  item.update(Controller='interactive3d_tests/chest',Parent='chest.frame')
  for state in item['States']:state['ObjectRole']='chest.opened' if state['Id']=='open' else 'chest.closed'
 if name=='bench':item['Seats'][0].update(ApproachPoint='',Socket='')
 items.append(item)
items.append({'Id':'interactive3d_tests/blocked','Location':location['Id'],'Parent':'deck','Range':.01,'ApproachTimeout':2,'States':[{'Id':'idle'}],'Seats':[{'Id':'user','Point':'blocked'}],'Actions':[action()]})
entity('interactive3d','interactives.json',items)
entity('stat','stats.json',[{'Id':'interactive3d_smith','Default':0}])
entity('combat3d_profile','combat_profiles.json',[{'Id':'interactive3d_tests/fighter_profile'}])
entity('combat3d_weapon','combat_weapons.json',[{'Id':'interactive3d_tests/test_weapon','Moves':[{'Id':'feint','Kind':'feint','Role':'use.enter','Energy':0}]}])
entity('item','items.json',[{'Id':'interactive3d_tests/'+name,'Name':'interactive3d_tests/'+name,'Price':1} for name in ['token','ore','ingot','key']])
write(suite/'manifest.json',{'id':'interactive3d_base__contracts','name':'3D interactive contracts','version':'0.1.0','author':'ct','depends':['interactive3d_base>=0.1.0','dialog_base>=0.1.0','items_base>=0.1.0','combat3d_base>=0.1.0'],'sources':['src/**/*.cs'],'test_sources':['tests/*.cs']})
(suite/'tests').mkdir(exist_ok=True);(suite/'src').mkdir(exist_ok=True)
provenance=root.parent/'art_sources/cthangover/interactive3d/20261115/fixtures';provenance.mkdir(parents=True,exist_ok=True);shutil.copy2(__file__,provenance/'create_fixture.py');write(provenance/'provenance.json',{'origin':'Original procedural geometric furniture and two-joint chest. Humanoids reuse our original stage-4 fixture via actor3d; calibration clips, not final gameplay art.','assets':{str(p.relative_to(suite)):hashlib.sha256(p.read_bytes()).hexdigest() for p in (suite/'models3d').rglob('*.glb')}})
