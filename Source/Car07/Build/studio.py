"""Photograph the saved master without adding studio objects to the game asset."""
import sys
from pathlib import Path

import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
sys.dont_write_bytecode = True
sys.path.insert(0,str(HERE))
import track
from extension import cutaway_wings


def render(root):
    scene = bpy.context.scene
    track.build()
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = 48
    scene.cycles.use_denoising = True
    scene.cycles.max_bounces = 5
    # CPU fallback keeps the same studio usable on a machine without an NVIDIA GPU.
    prefs = bpy.context.preferences.addons['cycles'].preferences
    try:
        prefs.compute_device_type = 'OPTIX'
        prefs.refresh_devices()
        if any(d.type == 'OPTIX' for d in prefs.devices):
            for d in prefs.devices:
                d.use = d.type == 'OPTIX'
            scene.cycles.device = 'GPU'
    except (RuntimeError, TypeError):
        pass
    world = scene.world or bpy.data.worlds.new('Overcast sky')
    scene.world = world
    world.use_nodes = True
    bg = next(node for node in world.node_tree.nodes if node.type == 'BACKGROUND')
    bg.inputs[0].default_value = (.47,.58,.73,1)
    bg.inputs[1].default_value = .42
    scene.view_settings.view_transform = 'AgX'
    scene.view_settings.look = 'AgX - Medium High Contrast'
    lights = []
    def area(name, point, target, watts, colour, width, height):
        light = bpy.data.lights.new(name, 'AREA')
        light.energy, light.color = watts, colour
        light.shape, light.size,light.size_y = 'RECTANGLE',width,height
        obj = bpy.data.objects.new(name, light)
        scene.collection.objects.link(obj)
        obj.location = point
        obj.rotation_euler = (Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler()
        lights.append(light)
    area('Softbox | north light', (-5,-8,12),(0,0,1),2900,(.87,.94,1),12,6)
    area('Softbox | end fill', (12,-6,6.8),(4,0,2),1350,(1,.85,.72),8,5)
    area('Softbox | snow rim', (-1,7,11),(0,0,2),3900,(.75,.88,1),14,5)
    area('Softbox | wheel fill', (-3,-9,2.2),(0,0,.6),330,(.73,.85,1),9,2.5)
    camera = bpy.data.cameras.new('Plate camera')
    camera.clip_start = .025
    cam = bpy.data.objects.new('Plate camera',camera)
    scene.collection.objects.link(cam)
    scene.camera = cam
    output = root/'Previews'
    output.mkdir(exist_ok=True)
    def frame(name,position,target,width,resolution,orthographic):
        cam.location = position
        cam.rotation_euler = (Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler()
        camera.type = 'ORTHO' if orthographic else 'PERSP'
        if orthographic:
            camera.ortho_scale = width
        else:
            camera.lens = width
        scene.render.resolution_x,scene.render.resolution_y = resolution
        scene.render.resolution_percentage = 50 if '--draft' in sys.argv else 100
        if '--draft' in sys.argv:
            scene.cycles.samples = 8
        scene.render.image_settings.file_format = 'PNG'
        scene.render.image_settings.color_mode = 'RGB'
        scene.render.filepath = str(output / (name+'.png'))
        bpy.ops.render.render(write_still=True)
    hidden=[]
    for obj in scene.objects:
        if (obj.name.startswith(('Roof_','Coved_headlining','Interior_Brass_and_Opaline'))
                or 'Starboard' in obj.name or obj.name=='Interior_Cabin_lining'):
            obj.hide_render=True
            hidden.append(obj)
    restore=cutaway_wings(scene)
    frame('Car07_Cutaway', (-1.8,13,22),(0,0,1.70),32.3,(2000,880),True)
    restore()
    for obj in hidden:
        obj.hide_render=False
    if '--cutaway-only' in sys.argv:
        return
    # Blue hour on a remote halt. The two scenes photograph the same master.
    import halt
    halt.build()
    for light,watts,colour in zip(lights, (810,330,2550,90),
            ((.62,.75,1), (1,.75,.50), (.53,.70,1), (.60,.72,1))):
        light.energy,light.color = watts,colour
    bg.inputs[0].default_value = (.24,.37,.62,1)
    bg.inputs[1].default_value = .32
    if '--interiors' not in sys.argv:
        camera.dof.use_dof = True
        camera.dof.focus_distance = 37.0
        camera.dof.aperture_fstop = 1.4
        frame('Car07', (20,-43,9.0),(.63,0,2.05),55,(2000,1000),False)
    camera.dof.use_dof=False
    # Trees on the opposite bank are seen through the passage windows.
    from mathutils import Matrix
    from math import pi
    horizon=bpy.data.collections['Studio | Fellbridge halt']
    for obj in list(horizon.objects):
        if 'hawthorns' in obj.name or 'distant fell' in obj.name or 'mist behind' in obj.name:
            opposite=bpy.data.objects.new(obj.name+' | south bank',obj.data)
            horizon.objects.link(opposite)
            opposite.matrix_world=Matrix.Rotation(pi,4,'Z') @ obj.matrix_world
    area('Sky | through compartment 1',(-5.4,4.3,4.8),(-6.2,.6,2.25),180,(.49,.71,1),3.2,3.2)
    scene.view_settings.exposure=.23
    for title,file in [('The passage','Passage'),('Sleeping compartment','Compartment'),
                       ('Writing bureau','Bureau')]:
        view=bpy.data.objects['View | '+title]
        direction=view.rotation_euler.to_quaternion() @ Vector((0,0,-1))
        frame('Car07_'+file,view.location.copy(),view.location+direction,
              view.data.lens,(1600,1000),False)


if __name__ == '__main__':
    render(HERE.parents[2])
