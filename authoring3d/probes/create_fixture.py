"""Host-only independent format fixture; never opens prototype files or runs embedded scripts."""
import argparse
import base64
import hashlib
import json
from pathlib import Path
import sys

import bpy
from io_scene_fbx import parse_fbx


def ascii_property(value, kind):
    if kind == ord('C'):
        return 'T' if value[0] else 'F'
    if kind == ord('S'):
        value = value.decode('utf-8')
        if '\x00\x01' in value:
            name, category = value.split('\x00\x01', 1)
            value = category + '::' + name
        return json.dumps(value, ensure_ascii=True)
    if kind == ord('R'):
        return '"' + base64.b64encode(value).decode('ascii') + '"'
    return str(int(value)) if isinstance(value, bool) else str(value)


def ascii_element(element, depth=0):
    indent = '\t' * depth
    props = []
    for value, kind in zip(element.props, element.props_type):
        if kind in b'fdilbc':
            props.append('*' + str(len(value)) + ' {\n' + indent + '\ta: ' + ','.join(map(str, value)) + '\n' + indent + '}')
        else:
            props.append(ascii_property(value, kind))
    line = indent + element.id.decode() + ': ' + (', ' if element.id == b'Content' else '') + ', '.join(props)
    if element.elems:
        return line + ' {\n' + '\n'.join(ascii_element(child, depth + 1) for child in element.elems) + '\n' + indent + '}'
    return line


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--out', type=Path, required=True)
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
    out = args.out.resolve()
    out.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    image = bpy.data.images.new('ProbeChecker', width=8, height=8)
    image.pixels = [v for y in range(8) for x in range(8) for v in ((0.9, 0.15, 0.05, 1) if (x + y) % 2 else (0.05, 0.8, 0.2, 1))]
    image.filepath_raw = str(out / 'checker.png')
    image.file_format = 'PNG'
    image.save()
    image.pack()
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, 1.5))
    mesh = bpy.context.object
    mesh.name = 'ProbeBeam'
    mesh.dimensions = (1, 2, 3)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    material = bpy.data.materials.new('ProbeMaterial')
    material.use_nodes = True
    tex = material.node_tree.nodes.new('ShaderNodeTexImage')
    tex.image = image
    material.node_tree.links.new(tex.outputs['Color'], material.node_tree.nodes.get('Principled BSDF').inputs['Base Color'])
    mesh.data.materials.append(material)
    bpy.ops.object.armature_add(location=(0, 0, 0))
    rig = bpy.context.object
    rig.name = 'ProbeRig'
    bpy.ops.object.mode_set(mode='EDIT')
    root = rig.data.edit_bones[0]
    root.name = 'ProbeRoot'
    root.head, root.tail = (0, 0, 0), (0, 0, 1.5)
    hinge = rig.data.edit_bones.new('ProbeHinge')
    hinge.head, hinge.tail, hinge.parent = (0, 0, 1.5), (0, 0, 3), root
    bpy.ops.object.mode_set(mode='OBJECT')
    for name in ('ProbeRoot', 'ProbeHinge'):
        mesh.vertex_groups.new(name=name)
    for vertex in mesh.data.vertices:
        mesh.vertex_groups['ProbeRoot' if vertex.co.z < 0 else 'ProbeHinge'].add([vertex.index], 1, 'REPLACE')
    mesh.parent = rig
    modifier = mesh.modifiers.new('ProbeSkin', 'ARMATURE')
    modifier.object = rig
    rig.animation_data_create()
    for name, axis in [('ProbeWave', 0), ('ProbeLean', 1)]:
        action = bpy.data.actions.new(name)
        rig.animation_data.action = action
        bone = rig.pose.bones['ProbeHinge']
        bone.rotation_mode = 'XYZ'
        for frame, angle in [(1, 0), (13, 0.35), (25, 0)]:
            bone.rotation_euler = (0, 0, 0)
            bone.rotation_euler[axis] = angle
            bone.keyframe_insert(data_path='rotation_euler', frame=frame)
        track = rig.animation_data.nla_tracks.new()
        track.name = name
        track.strips.new(name, 1, action)
    rig.animation_data.action = None
    bpy.context.scene.frame_end = 25
    bpy.context.scene.frame_set(1)
    bpy.ops.wm.save_as_mainfile(filepath=str(out / 'probe.blend'))
    bpy.ops.export_scene.gltf(filepath=str(out / 'probe.glb'), export_format='GLB', export_animations=True, export_animation_mode='NLA_TRACKS')
    bpy.ops.export_scene.gltf(filepath=str(out / 'probe.gltf'), export_format='GLTF_SEPARATE', export_animations=True, export_animation_mode='NLA_TRACKS')
    bpy.ops.export_scene.fbx(filepath=str(out / 'probe.fbx'), use_selection=False, add_leaf_bones=False, path_mode='COPY', embed_textures=True, bake_anim=True)
    bpy.ops.wm.obj_export(filepath=str(out / 'probe.obj'), export_materials=True, export_animation=False)
    tree, version = parse_fbx.parse(str(out / 'probe.fbx'))
    (out / 'probe_ascii.fbx').write_text('; FBX 7.4.0 project file\n' + '\n'.join(ascii_element(element) for element in tree.elems), encoding='utf-8')
    record = {'blender': bpy.app.version_string, 'source': 'probe.blend', 'dimensions_blender_z_up': [1, 2, 3], 'bones': 2, 'clips': ['ProbeWave', 'ProbeLean'], 'ascii_conversion': 'Blender binary parser; same FBX elements serialized as text', 'embedded_scripts': False, 'files': {path.name: hashlib.sha256(path.read_bytes()).hexdigest() for path in out.iterdir() if path.is_file()}}
    (out / 'provenance.json').write_text(json.dumps(record, indent=2) + '\n', encoding='utf-8')


if __name__ == '__main__':
    main()
