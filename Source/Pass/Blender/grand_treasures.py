"""Build only the five grand consignments, their physical finishes and portraits.

Generate the five surface images once with ../Art/grand_surfaces.py, then run
blender -b -t 5 --factory-startup --python-exit-code 1 -P grand_treasures.py.
The live clock hands keep separate editable pivots through the FBX export.
"""
import re
import sys
import uuid
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector, Matrix
sys.path.insert(0,str(Path(__file__).resolve().parent))
import station_heritage as craft
import grand_objects as g

ROOT=Path(__file__).resolve().parents[3]
PASS=ROOT/"Assets/Transperceneige/Pass"
MODELS=PASS/"Valuables"
FINISHES=MODELS/"Grand finishes"
ICONS=PASS/"Resources/WinterbourneLootIcons"
MASTER=ROOT/"Source/Pass/GrandTreasures.blend"


def guid(key):
    return uuid.uuid5(uuid.NAMESPACE_URL,"winterbourne/grand-consignments/"+key).hex


def palette():
    craft.stable_guid=guid
    craft.OUT=FINISHES
    craft.copied_meta(MODELS.with_suffix(".meta"),FINISHES.with_suffix(".meta"),"Grand finishes")
    # Colour maps carry wood, shell and steel grain. Bevels, recesses and
    # gemstone facets stay in the mesh under the player's moving light.
    options=[
        (g.GOLD,"d4ab57",.90,.26,None),(g.GILT,"b69052",.78,.33,None),
        (g.SILVER,"d4d7d3",.90,.20,None),(g.DARK,"263248",.77,.36,None),
        (g.BLACK,"26292d",.11,.24,None),(g.PEARL,"e3d9ce",.19,.22,None),
        (g.RUBY,"931f3f",.21,.14,None),(g.GREEN,"2e7862",.19,.15,None),
        (g.BLUE,"204c71",.22,.15,None),(g.CLEAR,"c6d5dd",.25,.11,None),
        (g.GLASS,"e4edef",.02,.095,None),(g.IVORY,"deb977",.24,.48,None),
        ("Oxblood silk","623143",.025,.67,None),
        (g.WOOD,"ffffff",.02,.34,"Violin flamed maple"),(g.SPRUCE,"ffffff",.02,.37,"Varnished alpine spruce"),
        (g.STEEL,"ffffff",.82,.28,"Watered crucible steel"),(g.COVER,"ffffff",.16,.41,"Tooled celestial calf"),
        (g.SHELL,"ffffff",.11,.29,"Chambered shell"),("Aperture pearl","ffffff",.22,.18,"Aperture nacre")]
    for label,hue,metal,rough,texture in options:
        dest=craft.finish_material(label,hue,metal,rough,texture=texture is not None)
        path=None
        if texture:
            path=FINISHES/(texture+".png")
            craft.copied_meta(craft.STATION/"StationHouseEnamel.png.meta",path.with_suffix(".png.meta"),path.name)
            dest.write_text(dest.read_text().replace(guid("Stationery.png"),guid(path.name)),encoding="utf8")
        craft.mat(label,hue,metal,rough,dest,path)
        bsdf=craft.materials[label].node_tree.nodes.get("Principled BSDF")
        if label in (g.WOOD,g.SPRUCE,g.BLACK,g.SHELL,g.PEARL):
            bsdf.inputs["Coat Weight"].default_value=.20 if label in (g.WOOD,g.SPRUCE) else .38
            bsdf.inputs["Coat Roughness"].default_value=.23
        if label in (g.GREEN,g.BLUE,g.RUBY,g.CLEAR,g.GLASS):
            bsdf.inputs["IOR"].default_value=1.53 if label==g.GLASS else 1.78
            bsdf.inputs["Transmission Weight"].default_value=.89 if label==g.GLASS else .36
            bsdf.inputs["Coat Weight"].default_value=.27
        if label in (g.PEARL,"Aperture pearl"):
            bsdf.inputs["Thin Film Thickness"].default_value=355
            bsdf.inputs["Thin Film IOR"].default_value=1.38
        if label==g.GLASS:
            # At millimetre thickness a polished plate mostly transmits the case
            # lining. Keep a true transparent path in the RGBA portrait as well.
            tree=craft.materials[label].node_tree
            clear=tree.nodes.new("ShaderNodeBsdfTransparent")
            blend=tree.nodes.new("ShaderNodeMixShader")
            blend.inputs[0].default_value=.79
            tree.links.new(bsdf.outputs[0],blend.inputs[1])
            tree.links.new(clear.outputs[0],blend.inputs[2])
            tree.links.new(blend.outputs[0],tree.nodes.get("Material Output").inputs["Surface"])
            body=(PASS.parent/"Car07/Car07_CabinGlass.mat").read_text()
            body=body.replace("m_Name: Car07_CabinGlass","m_Name: "+g.GLASS)
            body=re.sub(r"\{fileID: 2800000, guid: \w+, type: 3\}","{fileID: 0}",body)
            body=body.replace("{r: 1, g: 1, b: 1, a: 1}","{r: 0.85, g: 0.93, b: 0.94, a: 0.24}")
            body=re.sub(r"(- _Smoothness: )[^\n]+",r"\g<1>0.925",body)
            dest.write_text(body,encoding="utf8")


def weld(obj):
    bm=bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.000007)
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()


def empty(name,collection,parent=None,coords=(0,0,0)):
    obj=bpy.data.objects.new(name,None)
    collection.objects.link(obj)
    obj.empty_display_size=.008
    obj.parent=parent
    obj.location=coords
    return obj


def export(name,offset):
    craft.group.flush()
    craft.group=None
    root=empty(name,bpy.context.scene.collection)
    exported=[root]
    statics=[]
    for part in craft.parts:
        weld(part)
        original_name=part.name
        part.name=name+" | "+original_name
        # Bake the hands' exact centres into both the authoring master and the
        # exported mesh; a rotation around either bearing describes a true arc.
        if original_name in ("HourHand","MinuteHand"):
            part.data.transform(Matrix.Translation(-Vector(g.PIVOT)))
            part.location=g.PIVOT
        duplicate=part.copy()
        duplicate.data=part.data.copy()
        bpy.context.scene.collection.objects.link(duplicate)
        duplicate.name=original_name
        duplicate.parent=root
        if original_name in ("HourHand","MinuteHand"):
            exported.append(duplicate)
        else:
            statics.append(duplicate)
    bpy.ops.object.select_all(action="DESELECT")
    for duplicate in statics:duplicate.select_set(True)
    bpy.context.view_layer.objects.active=statics[0]
    bpy.ops.object.join()
    combined=bpy.context.active_object
    combined.name="Body"
    exported.append(combined)
    if name=="mystery_clock":
        for label,delta in (("DialTwelve",(0,0,.06)),("DialThree",(.06,0,0))):
            marker=empty(label,bpy.context.scene.collection,root,Vector(g.PIVOT)+Vector(delta))
            exported.append(marker)
    for obj in exported:obj.select_set(True)
    bpy.context.view_layer.objects.active=combined
    path=MODELS/(name+".fbx")
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={"MESH","EMPTY"},
        axis_forward="-Z",axis_up="Y",use_triangles=True,apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",bake_anim=False,add_leaf_bones=False,
        path_mode="STRIP",mesh_smooth_type="FACE",use_custom_props=False)
    craft.remap_fbx(path,{m.name for obj in craft.parts for m in obj.data.materials})
    print("GRAND EXPORTED",name,len(craft.parts),"editable assemblies",len(combined.data.vertices),"static vertices",flush=True)
    for obj in reversed(exported):bpy.data.objects.remove(obj,do_unlink=True)
    base=empty(name,craft.collection)
    base.location=offset
    for obj in craft.parts:obj.parent=base


def render_icons(scene):
    scene.render.engine="CYCLES"
    scene.cycles.samples=32
    scene.cycles.use_denoising=True
    scene.world=bpy.data.worlds.new("Evening silversmith's cabinet")
    scene.world.use_nodes=True
    bg=scene.world.node_tree.nodes.get("Background")
    bg.inputs["Color"].default_value=(.40,.48,.55,1)
    bg.inputs["Strength"].default_value=.24
    scene.view_settings.look="AgX - Medium High Contrast"
    scene.render.film_transparent=True
    scene.render.resolution_x=scene.render.resolution_y=480
    scene.render.resolution_percentage=100
    scene.render.image_settings.color_mode="RGBA"
    cam=bpy.data.cameras.new("Catalogue plate")
    eye=bpy.data.objects.new(cam.name,cam)
    scene.collection.objects.link(eye)
    scene.camera=eye
    cam.type="ORTHO"
    cam.clip_start=.001
    lights=[]
    for vector,energy,size,tint in [((-.62,-.75,1.05),38,.69,(1,.86,.68)),((.80,.42,.80),48,.75,(.77,.88,1)),
                                     ((.30,-.83,.28),7,.29,(.98,1,1))]:
        data=bpy.data.lights.new("Strip reflection", "AREA")
        data.energy=energy;data.shape="RECTANGLE";data.size=size;data.size_y=size*.45;data.color=tint
        obj=bpy.data.objects.new(data.name,data)
        scene.collection.objects.link(obj)
        lights.append((obj,Vector(vector)))
    for name in g.BUILDERS:
        group=bpy.data.collections[name]
        for other in g.BUILDERS:bpy.data.collections[other].hide_render=other!=name
        bpy.context.view_layer.update()
        bounds=[obj.matrix_world@Vector(v) for obj in group.objects if obj.type=="MESH" for v in obj.bound_box]
        low=Vector([min(v[i] for v in bounds) for i in range(3)])
        high=Vector([max(v[i] for v in bounds) for i in range(3)])
        centre=(low+high)*.5
        for obj,delta in lights:
            obj.location=centre+delta
            obj.rotation_euler=(-delta).to_track_quat("-Z","Y").to_euler()
        direction={"touring_violin":(-.33,-.68,1.20),"falcon_dagger":(.10,-.48,1.30),
                   "nautilus_cup":(.42,-1.35,.47),"mystery_clock":(.17,-1.5,.425),"gilt_ephemeris":(.56,-.67,1.25)}[name]
        eye.location=centre+Vector(direction)
        eye.rotation_euler=(centre-eye.location).to_track_quat("-Z","Y").to_euler()
        inv=eye.rotation_euler.to_matrix().inverted()
        projected=[inv@(v-centre) for v in bounds]
        cam.ortho_scale=max(max(q[i] for q in projected)-min(q[i] for q in projected) for i in (0,1))*1.12
        scene.render.filepath=str(ICONS/(name+".png"))
        bpy.ops.render.render(write_still=True)
        craft.copied_meta(ICONS/"pocket_watch.png.meta",ICONS/(name+".png.meta"),"icon-"+name)
    for name in g.BUILDERS:bpy.data.collections[name].hide_render=False


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene=bpy.context.scene
    scene.name="North Fell | Registered valuables"
    scene.unit_settings.system="METRIC"
    scene.unit_settings.length_unit="METERS"
    bpy.context.preferences.filepaths.save_version=0
    palette()
    positions=((-0.59,0,0),(-.22,0,0),(.18,0,0),(-.10,.42,0),(.53,0,0))
    for (name,build),offset in zip(g.BUILDERS.items(),positions):
        craft.collection=bpy.data.collections.new(name)
        scene.collection.children.link(craft.collection)
        craft.parts=[]
        build()
        export(name,offset)
    bpy.ops.object.select_all(action="DESELECT")
    for area in bpy.context.screen.areas:
        if area.type=="VIEW_3D":
            view=area.spaces.active
            view.region_3d.view_distance=1.95
            view.region_3d.view_location=(0,.18,.08)
            view.region_3d.view_rotation=Vector((-.19,.90,-.61)).to_track_quat("-Z","Y")
            view.shading.color_type="MATERIAL"
    bpy.ops.outliner.orphans_purge(do_recursive=True)
    for img in bpy.data.images:
        if img.packed_file:img.filepath="//../../"+(FINISHES/img.name).relative_to(ROOT).as_posix()
    bpy.ops.wm.save_as_mainfile(filepath=str(MASTER),compress=True)
    render_icons(scene)


if __name__=="__main__":main()
