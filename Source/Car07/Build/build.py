"""Rebuild the Blender master, Unity FBX, finish maps and optional studio renders.

blender -b --factory-startup -P Source/Car07/Build/build.py [-- --render]
"""
import math
import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Vector

HERE = Path(__file__).resolve().parent
sys.dont_write_bytecode = True
sys.path.insert(0, str(HERE))
import carbody
import cabins
import finishes
import glazing
import inspection
import interior
import markings
import roofends
import runninggear
from geometry import Geometry

ROOT = HERE.parents[2]
ASSET = ROOT / 'Assets' / 'Transperceneige' / 'Car07'


def empty(name, collection, parent=None, location=(0,0,0)):
    obj = bpy.data.objects.new(name, None)
    obj.empty_display_type = 'PLAIN_AXES'
    obj.empty_display_size = .24
    collection.objects.link(obj)
    obj.parent, obj.location = parent, location
    return obj


def build():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.name = 'Winterbourne | Coachworks'
    scene.unit_settings.system = 'METRIC'
    scene.unit_settings.length_unit = 'METERS'
    collection = bpy.data.collections.new('Winterbourne | Sleeping Car 07')
    scene.collection.children.link(collection)
    root = empty('Sleeper_07', collection)
    g = Geometry()
    for module in (carbody, roofends, runninggear, markings, interior, cabins):
        module.build(g)
    textures = ASSET / 'Textures'
    textures.mkdir(parents=True, exist_ok=True)
    material = finishes.material(finishes.make_atlas(textures))
    base_tex = next(n for n in material.node_tree.nodes if n.type == 'TEX_IMAGE' and n.image.name.startswith('Car07_Livery'))
    material.node_tree.nodes.active = base_tex
    objects = g.finish(material, collection) + glazing.build(collection, textures)
    frames = {o.name:o for o in objects if o.name.startswith('Bogie_')}
    door_pivots={name:Vector(start) for name,start,_,_ in cabins.door_poses()}
    for sign,tag in [(-1,'A'),(1,'B')]:
        door_pivots['EndDoor_'+tag]=Vector((sign*8.91,.68,1.39))
        for side,flank in [(-1,'Port'),(1,'Starboard')]:
            door_pivots['Door_'+flank+'_'+tag]=Vector((sign*(8.04-.492),side*1.475,1.38))
    for obj in objects:
        name = obj.name
        obj.parent = root
        if name.startswith(('Bogie_','Wheelset_')):
            end = name.split('_')[1][0]
            pivot = Vector((-5.9 if end=='A' else 5.9,0,1.1))
            absolute = pivot.copy()
            if name.startswith('Wheelset_'):
                absolute.x += -1.03 if name.endswith('1') else 1.03
                absolute.z = .48
                obj.parent = frames['Bogie_'+end]
                obj.location = absolute - pivot
            else:
                obj.location = absolute
            obj.data.transform(Matrix.Translation(-absolute))
        elif name in door_pivots:
            obj.location=door_pivots[name]
            obj.data.transform(Matrix.Translation(-obj.location))
        elif name.startswith('Glass_Door_'):
            leaf=name.removeprefix('Glass_')
            obj.data.transform(Matrix.Translation(-door_pivots[leaf]))
            obj.parent=bpy.data.objects[leaf]
    for name, sign in [('A',-1),('B',1)]:
        empty('Socket_'+name, collection, root, (sign*9.83,0,.97))
    bpy.context.view_layer.objects.active = root
    root.select_set(True)
    inspection.organise(collection,objects)
    inspection.cameras_and_lights(scene)
    # Keep the authoring view free of the studio floor, rails and camera objects.
    view = Vector((-11,24,-7)).to_track_quat('-Z','Y')
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == 'VIEW_3D':
                space = area.spaces.active
                space.region_3d.view_rotation = view
                space.region_3d.view_distance = 23
                space.region_3d.view_location = (0,0,2)
                space.region_3d.view_perspective = 'ORTHO'
                space.shading.type = 'MATERIAL'
                space.overlay.show_overlays = False
    # Export Y-up and align the car along Unity Z. Rotate the local geometry and
    # pivot translations together so the prefab can be placed with identity rotation.
    quarter = Matrix.Rotation(math.pi/2,4,'Z')
    for obj in collection.all_objects:
        obj.location = quarter @ obj.location
        if obj.type == 'MESH':
            obj.data.transform(quarter)
        obj.select_set(True)
    bpy.context.view_layer.update()
    bpy.ops.export_scene.fbx(filepath=str(ASSET/'Car07.fbx'), use_selection=True,
        object_types={'MESH','EMPTY'}, axis_forward='-Z', axis_up='Y', use_mesh_modifiers=True,
        use_triangles=True, apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
        bake_anim=False, add_leaf_bones=False, path_mode='RELATIVE', mesh_smooth_type='FACE',
        use_custom_props=False)
    for obj in collection.all_objects:
        obj.location = quarter.inverted() @ obj.location
        if obj.type == 'MESH':
            obj.data.transform(quarter.inverted())
        obj.select_set(False)
    root.select_set(True)
    if g.font and g.font.users == 0:
        bpy.data.fonts.remove(g.font)
    bpy.context.view_layer.update()
    for image in bpy.data.images:
        if image.packed_file:
            image.filepath = '//../../Assets/Transperceneige/Car07/Textures/' + image.name
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(HERE.parent/'Car07.blend'), compress=True, relative_remap=False)
    for obj in objects:
        obj.data.calc_loop_triangles()
    print('CAR07:', len(objects),'meshes,',sum(len(o.data.loop_triangles) for o in objects),'triangles.')
    return scene


if __name__ == '__main__':
    scene = build()
    if '--unity-data' in sys.argv:
        import integration
        integration.write(sys.argv[sys.argv.index('--unity-data')+1])
    if '--render' in sys.argv:
        from studio import render
        render(ROOT)
