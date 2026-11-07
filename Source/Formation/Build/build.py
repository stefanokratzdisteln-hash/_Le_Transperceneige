"""Build metric vehicles, portable Blender masters, FBXs and Unity authoring data.

blender -b --factory-startup --python-exit-code 1 -P Source/Formation/Build/build.py -- --cars 6,5 --photos
Use --photos-only to render the saved sources without exporting any model.
"""
import argparse
import importlib
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Vector

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[2]
sys.dont_write_bytecode=True
sys.path[:0]=[str(HERE),str(ROOT/'Source/Car07/Build')]

from geometry import Geometry
import glazing
from layout import Layout, TITLES
import stock


def empty(name,collection,parent=None,point=(0,0,0)):
    o=bpy.data.objects.new(name,None)
    collection.objects.link(o)
    o.parent=parent
    o.location=point
    o.empty_display_size=.17
    return o


def setup_scene(scene,l):
    coll=bpy.data.collections.new('Interior photography')
    scene.collection.children.link(coll)
    for name,pos,watts,colour,_ in l.lights:
        data=bpy.data.lights.new(name,'POINT')
        data.energy,data.color,data.shadow_soft_size=watts,colour,.095
        obj=bpy.data.objects.new(name,data)
        coll.objects.link(obj)
        obj.location=pos
    pos,target,lens=l.camera
    data=bpy.data.cameras.new('Passenger view')
    data.lens,data.clip_start=lens,.025
    eye=bpy.data.objects.new('Passenger view',data)
    coll.objects.link(eye)
    eye.location=pos
    eye.rotation_euler=(Vector(target)-eye.location).to_track_quat('-Z','Y').to_euler()
    scene.camera=eye
    scene.world=bpy.data.worlds.new('Northern light')
    scene.world.color=(.27,.32,.42)
    scene.render.engine='CYCLES'
    scene.cycles.samples=40
    scene.cycles.use_denoising=True
    scene.render.resolution_x,scene.render.resolution_y,scene.render.resolution_percentage=1480,900,100
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=='VIEW_3D':
                space=area.spaces.active
                space.region_3d.view_perspective='CAMERA'
                space.overlay.show_overlays=False
                space.shading.type='MATERIAL'


def assign_origins(objects,l,root):
    by_name={o.name:o for o in objects}
    for o in objects:
        name=o.name
        o.parent=root
        if name.startswith(('Glass_Door_','DoorPane_')):
            leaf=name.split('_',1)[1]
            o.data.transform(Matrix.Translation(-Vector(l.pivots[leaf])))
            o.parent=by_name[leaf]
        elif name in l.pivots:
            o.location=l.pivots[name]
            o.data.transform(Matrix.Translation(-o.location))
        elif l.number>2 and name.startswith(('Bogie_','Wheelset_')):
            end=name.split('_')[1][0]
            pivot=Vector((-5.9 if end=='A' else 5.9,0,1.1))
            absolute=pivot.copy()
            if name.startswith('Wheelset_'):
                absolute.x+=-1.03 if name.endswith('1') else 1.03
                absolute.z=.48
                o.parent=by_name['Bogie_'+end]
                o.location=absolute-pivot
            else:
                o.location=absolute
            o.data.transform(Matrix.Translation(-absolute))
    for door in l.doors:
        if door['name'] not in by_name:
            raise ValueError('Missing sliding leaf '+door['name'])


def export_fbx(collection,path):
    quarter=Matrix.Rotation(math.pi/2,4,'Z')
    objects=list(collection.all_objects)
    for o in objects:
        o.location=quarter @ o.location
        if o.type=='MESH':
            o.data.transform(quarter)
        o.select_set(True)
    bpy.context.view_layer.update()
    try:
        bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH','EMPTY'},
            axis_forward='-Z',axis_up='Y',use_triangles=True,apply_unit_scale=True,
            apply_scale_options='FBX_SCALE_UNITS',bake_anim=False,add_leaf_bones=False,
            path_mode='RELATIVE',mesh_smooth_type='FACE',use_custom_props=False)
    finally:
        for o in objects:
            o.location=quarter.inverted() @ o.location
            if o.type=='MESH':
                o.data.transform(quarter.inverted())
            o.select_set(False)


def build(n):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene=bpy.context.scene
    scene.name=f'{n:02} | {TITLES[n]}'
    scene.unit_settings.system='METRIC'
    scene.unit_settings.length_unit='METERS'
    collection=bpy.data.collections.new(scene.name)
    scene.collection.children.link(collection)
    root=empty(f'Car{n:02}_Root',collection)
    l=Layout(n)
    g=Geometry()
    if n>2:
        stock.build(g,l)
    module='traction' if n<3 else 'service' if n in (3,9) else 'passenger'
    importlib.import_module(module).build(n,g,l)
    manifest=l.serialize()
    source=ROOT/'Source/Car07/Car07.blend'
    with bpy.data.libraries.load(str(source),link=False) as (_,loaded):
        loaded.materials=['Car07_Livery','Car07_CabinGlass']
    textures=ROOT/'Assets/Transperceneige/Car07/Textures'
    for image in bpy.data.images:
        if image.packed_file:
            image.filepath=str(textures/image.name)
    objects=g.finish(loaded.materials[0],collection)
    if n>2:
        objects+=glazing.build(collection,None,loaded.materials[1])
    if n==4:
        for name,(cx,y,_) in l.pivots.items():
            if not name.startswith('DrawingRoomDoor_'):
                continue
            mesh=bpy.data.meshes.new('DoorPane_'+name)
            mesh.from_pydata([(cx+dx,y,2.837+dz) for dx,dz in
                [(-.348,-.350),(.348,-.350),(.348,.350),(-.348,.350)]],[],[(0,1,2,3)])
            uv=mesh.uv_layers.new(name='Clear glass')
            for loop in mesh.loops:
                uv.data[loop.index].uv=[(.08,.08),(.92,.08),(.92,.92),(.08,.92)][loop.vertex_index]
            mesh.materials.append(loaded.materials[1])
            obj=bpy.data.objects.new(mesh.name,mesh)
            collection.objects.link(obj)
            objects.append(obj)
    assign_origins(objects,l,root)
    if n==5:
        import travellers
        from wardrobe import fabric, SWATCHES
        crowd=Geometry()
        travellers.build(crowd)
        clothing=fabric(ROOT/'Assets/Transperceneige/Formation/Textures')
        people=crowd.finish(clothing,collection,SWATCHES)
        if len(people)!=32:
            raise ValueError('The occupied saloon requires one passenger per seat.')
        cabin=empty('Travellers',collection,root)
        for person in people:
            person.parent=cabin
        objects+=people
        manifest['seatedTravellers']=len(people)
    for name,x in zip(('Socket_A','Socket_B'),l.ends):
        empty(name,collection,root,(x,0,.97))
    models=ROOT/'Assets/Transperceneige/Formation/Models'
    models.mkdir(parents=True,exist_ok=True)
    export_fbx(collection,models/f'Car{n:02}.fbx')
    # Collections also serve as removable layers for the overhead section drawings.
    sections={}
    for o in objects:
        if o.name.startswith(('Roof','Coved_headlining','Cut_','Ceiling_')):
            key='Canopy and headlining'
        elif o.name.startswith(('Bogie','Wheelset','Underframe','Coupling','Drive','Axle')):
            key='Chassis and machinery'
        elif o.name.startswith('Traveller_'):
            key='Seated travellers'
        elif 'Starboard' in o.name or 'Port' in o.name or 'lining' in o.name:
            key='Sides and glazing'
        else:
            key='Interior and fittings'
        if key not in sections:
            sections[key]=bpy.data.collections.new(key)
            collection.children.link(sections[key])
        collection.objects.unlink(o)
        sections[key].objects.link(o)
    setup_scene(scene,l)
    bpy.context.preferences.filepaths.save_version=0
    bpy.context.view_layer.objects.active=root
    for image in bpy.data.images:
        if image.packed_file:
            kit='Formation' if image.name=='Travellers.png' else 'Car07'
            image.filepath=f'//../../Assets/Transperceneige/{kit}/Textures/'+image.name
    source_file=HERE.parent/f'Car{n:02}.blend'
    bpy.ops.wm.save_as_mainfile(filepath=str(source_file),compress=True,relative_remap=False)
    for o in objects:
        o.data.calc_loop_triangles()
    manifest['meshes']=len(objects)
    manifest['triangles']=sum(len(o.data.loop_triangles) for o in objects)
    folder=HERE.parent/'Layouts'
    folder.mkdir(exist_ok=True)
    (folder/f'Car{n:02}.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(f'BUILT Car{n:02}: {len(objects)} meshes, {manifest["triangles"]} triangles, {len(l.boxes)} collisions.',flush=True)


if __name__=='__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('--cars',default='6,5,8,9,4,3,1,2')
    parser.add_argument('--photos',action='store_true')
    parser.add_argument('--photos-only',action='store_true')
    parser.add_argument('--draft',action='store_true')
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    for number in map(int,args.cars.split(',')):
        if number==7 or number not in TITLES:
            raise ValueError('Formation builder accepts new units 01–06, 08 and 09.')
        if args.photos_only:
            bpy.ops.wm.open_mainfile(filepath=str(HERE.parent/f'Car{number:02}.blend'))
        else:
            build(number)
        if args.photos_only or args.photos:
            from photography import photograph
            photograph(number,ROOT,args.draft)
