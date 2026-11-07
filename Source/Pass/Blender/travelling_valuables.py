"""Metric Winterbourne valuables. Run in Blender with --factory-startup -P.

The game mesh, editable dealer's tray and inventory portraits share the same
geometry. Blender +Z is up. The sole of each object sits on the owner's shelf.
"""
import re
import sys
import uuid
from pathlib import Path

import bpy
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
import station_heritage as craft

ROOT = Path(__file__).resolve().parents[3]
PASS = ROOT / "Assets/Transperceneige/Pass"
MODELS = PASS / "Valuables"
ICONS = PASS / "Resources/WinterbourneLootIcons"


from museum_antiques import ITEMS


def palette():
    station=craft.STATION
    for name,colour,metal,rough,path in [
        ("Brass","b78e4a",.58,.31,"Ticket brass"),("Walnut","6d4133",.04,.41,"Reading room walnut"),
        ("Ivory","f4e3b9",.03,.44,"Unbleached silk"),("Ebony","242321",.08,.32,"Heritage/Ebony"),
        ("Leather","40554b",.04,.60,"Heritage/Leather"),("Steel","83898b",.63,.36,"Heritage/Steel")]:
        craft.mat(name,colour,metal,rough,station/(path+".mat"))
    for name,colour,metal,rough in [
            ("Silver","d4dfe8",.78,.23), ("Gold","dfad50",.71,.23),
            ("Ruby","932338",.25,.15), ("Sapphire","394a95",.24,.15), ("Crystal","c5e9f2",.37,.10),
            ("Ink","202531",.08,.31), ("Carnelian","994b39",.18,.24), ("Pearl","eedfbe",.14,.23),
            ("Emerald","198858",.30,.16), ("Cobalt","25477b",.32,.22), ("Peacock","176b66",.32,.21),
            ("Blued","24394c",.67,.25), ("Ice","a5cfd4",.35,.18), ("Amethyst","82549b",.26,.16),
            ("Rose","95566c",.29,.20), ("Turquoise","218889",.24,.22)]:
        craft.mat(name,colour,metal,rough,craft.finish_material(name,colour,metal,rough))
    plate=MODELS/"Enamel miniatures.png"
    craft.copied_meta(station/"StationHouseEnamel.png.meta",plate.with_suffix(".png.meta"),plate.name)
    paint=craft.finish_material("Painted miniature","ffffff",.03,.29,texture=True)
    paint.write_text(paint.read_text().replace(craft.stable_guid("Stationery.png"),craft.stable_guid(plate.name)),encoding="utf-8")
    craft.mat("Miniature","ffffff",.03,.29,paint,plate)
    for name in ("Sapphire","Emerald","Ruby","Ice","Amethyst","Crystal","Rose","Cobalt","Turquoise"):
        finish=craft.materials[name].node_tree.nodes.get("Principled BSDF")
        finish.inputs["Coat Weight"].default_value=.46
        finish.inputs["Coat Roughness"].default_value=.12
        if name in ("Sapphire","Emerald","Ruby","Ice","Amethyst","Crystal"):
            finish.inputs["IOR"].default_value=1.55 if name=="Ice" else 1.76
            finish.inputs["Transmission Weight"].default_value=.78 if name=="Ice" else .29
    # The crystal bowl has its own clear material in the game and in the master.
    # The brass claws and rim remain opaque in the same FBX submeshes.
    glass=(PASS.parent/"Car07/Car07_CabinGlass.mat").read_text(encoding="utf-8")
    glass=glass.replace("m_Name: Car07_CabinGlass","m_Name: Ice")
    glass=re.sub(r"\{fileID: 2800000, guid: \w+, type: 3\}","{fileID: 0}",glass)
    glass=glass.replace("{r: 1, g: 1, b: 1, a: 1}","{r: 0.64, g: 0.85, b: 0.90, a: 0.48}")
    (MODELS/"Ice.mat").write_text(glass,encoding="utf-8")


def portraits(scene):
    scene.render.engine="CYCLES"
    scene.cycles.samples=28
    scene.cycles.use_denoising=True
    scene.world=bpy.data.worlds.new("Silversmith studio")
    scene.world.use_nodes=True
    scene.world.node_tree.nodes["Background"].inputs["Color"].default_value=(.46,.50,.55,1)
    scene.world.node_tree.nodes["Background"].inputs["Strength"].default_value=.21
    scene.view_settings.look="AgX - Medium High Contrast"
    scene.render.film_transparent=True
    scene.render.resolution_x=scene.render.resolution_y=384
    scene.render.resolution_percentage=100
    scene.render.image_settings.color_mode="RGBA"
    cam=bpy.data.cameras.new("Catalogue lens");eye=bpy.data.objects.new("Catalogue lens",cam)
    scene.collection.objects.link(eye);scene.camera=eye;cam.type="ORTHO";cam.clip_start=.001
    lights=[]
    for vector,energy,size,tint in [((-.55,-.66,.97),26,.71,(1,.91,.76)),
                                    ((.63,.37,.65),44,.54,(.79,.88,1)),((.30,-.75,.23),6,.28,(1,1,1))]:
        data=bpy.data.lights.new("Softbox","AREA");data.energy=energy;data.shape="RECTANGLE"
        data.size=size;data.size_y=size*.46;data.color=tint
        obj=bpy.data.objects.new("Softbox",data);scene.collection.objects.link(obj)
        lights.append((obj,Vector(vector)))
    for name in ITEMS:
        col=bpy.data.collections[name]
        for other in ITEMS:bpy.data.collections[other].hide_render=other!=name
        bpy.context.view_layer.update()
        ps=[o.matrix_world@Vector(v) for o in col.objects if o.type=="MESH" for v in o.bound_box]
        low=Vector([min(p[k] for p in ps) for k in range(3)])
        hi=Vector([max(p[k] for p in ps) for k in range(3)])
        center=(low+hi)*.5
        for light,delta in lights:
            light.location=center+delta
            light.rotation_euler=(-delta).to_track_quat("-Z","Y").to_euler()
        direction=(.46,-1.2,.95)
        if name in {"cameo","astrolabe","pocket_watch","portrait_snuffbox","jewelled_fan","pearl_aigrette"}:
            direction=(.12,-.60,1.4)
        if name=="emerald_tiara":direction=(.27,-1.48,.65)
        eye.location=center+Vector(direction)
        eye.rotation_euler=(center-eye.location).to_track_quat("-Z","Y").to_euler()
        rot=eye.rotation_euler.to_matrix().inverted()
        projected=[rot@(p-center) for p in ps]
        cam.ortho_scale=max(max(p[k] for p in projected)-min(p[k] for p in projected) for k in (0,1))*1.15
        scene.render.filepath=str(ICONS/(name+".png"));bpy.ops.render.render(write_still=True)
        craft.copied_meta(craft.STATION/"StationHouseEnamel.png.meta",ICONS/(name+".png.meta"),"icon-"+name)
    for name in ITEMS:bpy.data.collections[name].hide_render=False
    bpy.data.objects.remove(eye,do_unlink=True)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene=bpy.context.scene;scene.unit_settings.system="METRIC"
    bpy.context.preferences.filepaths.save_version=0
    craft.OUT=MODELS
    craft.stable_guid=lambda key:uuid.uuid5(uuid.NAMESPACE_URL,"winterbourne/valuables/"+key).hex
    for folder in (MODELS,ICONS.parent,ICONS):
        folder.mkdir(parents=True,exist_ok=True)
        if not folder.with_suffix(".meta").exists():
            craft.copied_meta(craft.STATION.with_suffix(".meta"),folder.with_suffix(".meta"),str(folder.relative_to(PASS)))
    palette()
    for i,(name,build) in enumerate(ITEMS.items()):
        craft.collection=bpy.data.collections.new(name)
        scene.collection.children.link(craft.collection)
        craft.parts=[]
        build()
        craft.export_group(name,((i%4-1.5)*.32,(i//4-1.5)*.23,0))
    bpy.ops.object.select_all(action="DESELECT")
    for area in bpy.context.screen.areas:
        if area.type=="VIEW_3D":
            view=area.spaces.active;view.region_3d.view_distance=1.5
            view.shading.color_type="MATERIAL"
    bpy.ops.outliner.orphans_purge(do_recursive=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/"Source/Pass/TravellingValuables.blend"),compress=True)
    portraits(scene)


if __name__=="__main__": main()
