"""Export the original stage-4 fixture with real action/NLA duplicates; host Blender only."""
import argparse,sys,json,hashlib
from pathlib import Path
import bpy
p=argparse.ArgumentParser();p.add_argument('--out',required=True);out=Path(p.parse_args(sys.argv[sys.argv.index('--')+1:]).out)
bpy.ops.wm.open_mainfile(filepath=str(out/'independent.blend'))
rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
action=bpy.data.actions.get('Sword')
rig.animation_data_create();rig.animation_data.action=None
track=rig.animation_data.nla_tracks.new();track.name='Sword_NLA';strip=track.strips.new('Sword_NLA',1,action)
bpy.ops.export_scene.fbx(filepath=str(out/'library_fbx.fbx'),use_selection=False,object_types={'MESH','ARMATURE'},add_leaf_bones=False,bake_anim=True,bake_anim_use_nla_strips=True,bake_anim_use_all_actions=True,path_mode='STRIP',axis_forward='-Z',axis_up='Y')
file=out/'provenance.json';data=json.loads(file.read_text(encoding='utf-8-sig'));data['files']['library_fbx.fbx']=hashlib.sha256((out/'library_fbx.fbx').read_bytes()).hexdigest();data['fbx_note']='Same original rig; Sword NLA plus action exports for runtime dedup verification';file.write_text(json.dumps(data,indent=2)+'\n',encoding='utf-8')
print('ANIM3D_FBX_FIXTURE_OK')
