"""Photograph the vehicle itself. Studio scenery is never selected for FBX export."""
from pathlib import Path
import tempfile

import bpy
from mathutils import Vector
from extension import cutaway_wings

import track


def photograph(number,root,draft=False,cutaway_only=False):
    scene=bpy.context.scene
    output=root/'Previews/Formation'
    output.mkdir(parents=True,exist_ok=True)
    review=Path(tempfile.gettempdir())/'Winterbourne-visual-review'
    review.mkdir(exist_ok=True)
    area_lights=[]
    def area(name,pos,target,watts,colour,size):
        data=bpy.data.lights.new(name,'AREA')
        data.energy,data.color,data.shape,data.size=watts,colour,'DISK',size
        obj=bpy.data.objects.new(name,data)
        scene.collection.objects.link(obj)
        obj.location=pos
        obj.rotation_euler=(Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler()
        area_lights.append(obj)
    track.build()
    area('Winter skylight',(-4,6,12),(0,0,1.5),2200,(.82,.90,1),11)
    area('South light',(4,-6,9),(0,0,1.8),1900,(1,.88,.70),10)
    area('Rim',(-2,-2,14),(0,0,0),1000,(.82,.91,1),7)
    scene.world.use_nodes=True
    bg=next(n for n in scene.world.node_tree.nodes if n.type=='BACKGROUND')
    bg.inputs[0].default_value=(.55,.67,.79,1)
    bg.inputs[1].default_value=.42
    scene.view_settings.view_transform='AgX'
    scene.view_settings.look='AgX - Medium High Contrast'
    scene.cycles.samples=8 if draft else 40
    scene.cycles.max_bounces=5
    prefs=bpy.context.preferences.addons['cycles'].preferences
    try:
        prefs.compute_device_type='OPTIX'
        prefs.refresh_devices()
        if any(d.type=='OPTIX' for d in prefs.devices):
            for d in prefs.devices:
                d.use=d.type=='OPTIX'
            scene.cycles.device='GPU'
    except (RuntimeError,TypeError):
        pass
    eye=scene.camera
    preview=bpy.data.objects.new('Section camera',bpy.data.cameras.new('Section'))
    scene.collection.objects.link(preview)
    preview.data.clip_start=.025
    def snap(name,cam,w,h,folder):
        scene.camera=cam
        scene.render.resolution_x,scene.render.resolution_y=w,h
        scene.render.resolution_percentage=50 if draft else 100
        scene.render.image_settings.file_format='PNG'
        scene.render.image_settings.color_mode='RGB'
        scene.render.filepath=str(folder/name)
        bpy.ops.render.render(write_still=True)
    hidden=[]
    for obj in scene.objects:
        if (obj.name.startswith(('Cut_','Roof','Coved_headlining','Ceiling_'))
                or 'Starboard' in obj.name or obj.name=='Interior_Cabin_lining'):
            obj.hide_render=True
            hidden.append(obj)
    preview.location=(-2.1,14,22) if number>2 else (-3,16,11)
    preview.rotation_euler=(Vector((0,0,1.72))-preview.location).to_track_quat('-Z','Y').to_euler()
    preview.data.type='ORTHO'
    preview.data.ortho_scale={1:20.2,2:13.6}.get(number,32.3)
    restore=cutaway_wings(scene)
    snap(f'Car{number:02}_Cutaway.png',preview,1760,980 if number<3 else 800,output if not draft else review)
    restore()
    for obj in hidden:
        obj.hide_render=False
    if cutaway_only:
        return
    if number==1:
        preview.location=(-9,18,7.5)
        preview.rotation_euler=(Vector((0,0,2.05))-preview.location).to_track_quat('-Z','Y').to_euler()
        preview.data.ortho_scale=20.2
        snap('Car01_Pacific.png',preview,1760,900,output if not draft else review)
    # Actual night exterior visible through the open windows and footplate.
    import halt
    halt.build()
    for obj,factor in zip(area_lights,(.37,.42,.72)):
        obj.data.energy*=factor
    bg.inputs[0].default_value=(.37,.49,.70,1)
    bg.inputs[1].default_value=.35
    scene.view_settings.exposure=.12
    snap(f'Car{number:02}_PassengerView.png',eye,1440,900,
         output if number in (1,6) and not draft else review)
    if number==6:
        preview.data.type='PERSP'
        preview.data.lens=18.5
        preview.location=(-6.92,.14,2.986)
        preview.rotation_euler=(Vector((-3.28,1.02,2.340))-preview.location).to_track_quat('-Z','Y').to_euler()
        snap('Car06_Galley.png',preview,1440,900,review)
