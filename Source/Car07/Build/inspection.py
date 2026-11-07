"""Blender viewing positions and actual interior practical lights."""
import bpy
from mathutils import Vector
from interior import light_positions
from extension import CUT, INSERT, point


def cameras_and_lights(scene):
    coll=bpy.data.collections.new('Photography | practical lights and cameras')
    scene.collection.children.link(coll)
    lamps=[(name,point(center,7),color,watts) for name,center,color,_,_,watts in light_positions()]
    for sign,label in ((-1,'Morning room'),(1,'Steward linen')):
        lamps.append((label+' ceiling lamp',(sign*(CUT+INSERT/2),0,3.18),(1,.79,.50),32))
    for name,center,color,watts in lamps:
        data=bpy.data.lights.new(name,'POINT')
        data.energy=watts
        data.color=color
        data.shadow_soft_size=.085
        obj=bpy.data.objects.new(name,data)
        coll.objects.link(obj)
        obj.location=center
    def camera(name,location,target,lens):
        data=bpy.data.cameras.new(name)
        data.lens=lens
        data.clip_start=.025
        obj=bpy.data.objects.new(name,data)
        coll.objects.link(obj)
        obj.location=location
        obj.rotation_euler=(Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler()
        return obj
    scene.camera=camera('View | The passage',(-7.11,-.92,2.909),(-1.6,-.69,2.73),19.5)
    camera('View | Sleeping compartment',(-4.83,-.455,2.865),(-6.09,.89,2.341),15.5)
    camera('View | Writing bureau',(-1.53,-1.18,2.88),(-.56,1.09,2.30),25)
    scene.render.engine='CYCLES'
    scene.cycles.samples=48
    scene.cycles.use_denoising=True
    world=bpy.data.worlds.new('Cold light through the windows')
    scene.world=world
    world.use_nodes=True
    bg=next(node for node in world.node_tree.nodes if node.type=='BACKGROUND')
    bg.inputs[0].default_value=(.5,.63,.79,1)
    bg.inputs[1].default_value=.36
    scene.render.resolution_percentage=100
    scene.render.resolution_x,scene.render.resolution_y=1440,960


def organise(collection,objects):
    sections={}
    for obj in objects:
        name=obj.name
        if name.startswith(('Roof','Coved_headlining')):
            key='01 | Roof and headlining'
        elif name.startswith(('Bogie','Wheelset','Underframe','Underslung','Coupling','Floorpan')):
            key='02 | Frame and running gear'
        elif ('Port' in name or 'Starboard' in name or name.startswith(('Gangway','EndDoor'))):
            key='03 | Coachwork and glazing'
        elif name.startswith(('Steward','Lavatory','Interior_Brass','Passage','Interior_Parquet')):
            key='04 | Passage and service rooms'
        else:
            key='05 | Compartment joinery'
        if key not in sections:
            sections[key]=bpy.data.collections.new(key)
            collection.children.link(sections[key])
        collection.objects.unlink(obj)
        sections[key].objects.link(obj)
