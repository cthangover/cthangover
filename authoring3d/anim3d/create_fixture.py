"""Independent geometric humanoids and stub clips. Run in host Blender only."""
import argparse,sys,json,hashlib,math
from pathlib import Path
import bpy
from mathutils import Vector
args=argparse.ArgumentParser(); args.add_argument('--out',required=True); out=Path(args.parse_args(sys.argv[sys.argv.index('--')+1:]).out); out.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
# The fixture is original geometry; names intentionally exercise two common conventions.
bones=[('Hips',None,(0,0,1),(0,0,1.2)),('Spine','Hips',(0,0,1.2),(0,0,1.45)),('Chest','Spine',(0,0,1.45),(0,0,1.65)),('Neck','Chest',(0,0,1.65),(0,0,1.75)),('Head','Neck',(0,0,1.75),(0,0,2)),('LeftShoulder','Chest',(0,0,1.6),(.2,0,1.6)),('LeftUpperArm','LeftShoulder',(.2,0,1.6),(.55,0,1.6)),('LeftLowerArm','LeftUpperArm',(.55,0,1.6),(.85,0,1.6)),('LeftHand','LeftLowerArm',(.85,0,1.6),(1,0,1.6)),('RightShoulder','Chest',(0,0,1.6),(-.2,0,1.6)),('RightUpperArm','RightShoulder',(-.2,0,1.6),(-.55,0,1.6)),('RightLowerArm','RightUpperArm',(-.55,0,1.6),(-.85,0,1.6)),('RightHand','RightLowerArm',(-.85,0,1.6),(-1,0,1.6)),('LeftUpperLeg','Hips',(.13,0,1),(.13,0,.55)),('LeftLowerLeg','LeftUpperLeg',(.13,0,.55),(.13,0,.12)),('LeftFoot','LeftLowerLeg',(.13,0,.12),(.13,-.22,.08)),('RightUpperLeg','Hips',(-.13,0,1),(-.13,0,.55)),('RightLowerLeg','RightUpperLeg',(-.13,0,.55),(-.13,0,.12)),('RightFoot','RightLowerLeg',(-.13,0,.12),(-.13,-.22,.08))]
mix={'Chest':'Spine1','LeftUpperArm':'LeftArm','LeftLowerArm':'LeftForeArm','RightUpperArm':'RightArm','RightLowerArm':'RightForeArm','LeftUpperLeg':'LeftUpLeg','LeftLowerLeg':'LeftLeg','RightUpperLeg':'RightUpLeg','RightLowerLeg':'RightLeg'}
plain={'Hips':'pelvis','Spine':'spine_01','Chest':'spine_02','Neck':'neck','Head':'head','LeftShoulder':'clavicle.L','LeftUpperArm':'upper_arm.L','LeftLowerArm':'forearm.L','LeftHand':'hand.L','RightShoulder':'clavicle.R','RightUpperArm':'upper_arm.R','RightLowerArm':'forearm.R','RightHand':'hand.R','LeftUpperLeg':'thigh.L','LeftLowerLeg':'shin.L','LeftFoot':'foot.L','RightUpperLeg':'thigh.R','RightLowerLeg':'shin.R','RightFoot':'foot.R'}
def rig(mode):
 data=bpy.data.armatures.new('IndependentRig'); obj=bpy.data.objects.new('IndependentActor',data); bpy.context.collection.objects.link(obj); bpy.context.view_layer.objects.active=obj; obj.select_set(True); bpy.ops.object.mode_set(mode='EDIT')
 names={b[0]:('mixamorig:'+mix.get(b[0],b[0]) if mode=='mixamo' else plain[b[0]]) for b in bones}
 scale=.82 if mode=='blender' else 1
 for key,parent,head,tail in bones:
  bone=data.edit_bones.new(names[key]); bone.head=Vector(head)*scale; bone.tail=Vector(tail)*scale
  if parent: bone.parent=data.edit_bones[names[parent]]
 bpy.ops.object.mode_set(mode='OBJECT'); obj.select_set(False)
 material=bpy.data.materials.new('IndependentBlue'); material.diffuse_color=(.12,.48,.72,1)
 for key,parent,head,tail in bones:
  a,b=Vector(head)*scale,Vector(tail)*scale; center=(a+b)/2; length=(b-a).length
  bpy.ops.mesh.primitive_cube_add(size=1,location=center); mesh=bpy.context.object; mesh.name='Part_'+key; mesh.scale=(.09,.09,max(.04,length)); mesh.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler(); bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
  group=mesh.vertex_groups.new(name=names[key]); group.add(range(len(mesh.data.vertices)),1,'REPLACE'); mod=mesh.modifiers.new('Armature','ARMATURE'); mod.object=obj; mesh.parent=obj; mesh.data.materials.append(material); mesh.select_set(False)
 return obj,names
def actions(obj,names,kinds):
 for act in list(bpy.data.actions): bpy.data.actions.remove(act)
 obj.animation_data_clear(); obj.animation_data_create()
 for kind in kinds:
  obj.animation_data.action=None
  for pose in obj.pose.bones: pose.rotation_mode='XYZ'; pose.rotation_euler=(0,0,0); pose.location=(0,0,0)
  action=bpy.data.actions.new(kind); obj.animation_data.action=action; action.use_fake_user=True
  for frame in (1,7,13,19,25):
   phase=(frame-1)/24; arm=obj.pose.bones[names['RightUpperArm']]; arm.rotation_euler=(.65*math.sin(phase*math.pi),.2*math.sin(phase*math.pi),.1)
   arm.keyframe_insert('rotation_euler',frame=frame,group=arm.name)
   spine=obj.pose.bones[names['Spine']]; spine.rotation_euler=(0,.15*math.sin(phase*math.pi),0); spine.keyframe_insert('rotation_euler',frame=frame,group=spine.name)
   hip=obj.pose.bones[names['Hips']]; hip.location=(0,-phase if kind=='Walk' else 0,0); hip.keyframe_insert('location',frame=frame,group=hip.name)
  obj.animation_data.action=None
 bpy.context.scene.frame_start=1; bpy.context.scene.frame_end=25; bpy.context.scene.render.fps=24
 bpy.context.scene.frame_set(1)
def export(name):
 bpy.ops.export_scene.gltf(filepath=str(out/name),export_format='GLB',export_yup=True,export_skins=True,export_animations=True,export_animation_mode='ACTIONS',export_force_sampling=True)
obj,names=rig('mixamo'); actions(obj,names,['Walk','Sword','Pistol','Reload']); bpy.ops.wm.save_as_mainfile(filepath=str(out/'independent.blend'))
actions(obj,names,['Walk','Sword']); export('library_a.glb')
actions(obj,names,['Pistol','Reload','Sword']); export('library_b.glb')
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
obj,names=rig('blender'); actions(obj,names,['Idle']); export('target.glb')
manifest={'origin':'Original procedural geometric test fixture, authored for anim3d stage 4; no reference game geometry or code','blender':bpy.app.version_string,'clips':'Simple stub arm/spine rotation and walk translation; no gameplay animation quality claimed','files':{p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in out.iterdir() if p.is_file() and p.suffix in ('.glb','.blend')}}
(out/'provenance.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
print('ANIM3D_FIXTURE_OK',out)
