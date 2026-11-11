"""Refresh two catalogue collections and their remapped meshes without rerendering the other fourteen."""
from pathlib import Path
import sys
import tempfile
import uuid
import bpy

sys.path.insert(0,str(Path(__file__).resolve().parent))
import travelling_valuables as catalogue
import station_heritage as s

pair=("singing_bird_box","rose_egg")
master=catalogue.ROOT/"Source/Pass/TravellingValuables.blend"
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version=0
s.OUT=catalogue.MODELS
s.stable_guid=lambda key:uuid.uuid5(uuid.NAMESPACE_URL,"winterbourne/valuables/"+key).hex
catalogue.palette()
for key in pair:
    s.collection=bpy.data.collections.new(key)
    bpy.context.scene.collection.children.link(s.collection)
    s.parts=[]
    catalogue.ITEMS[key]()
    i=list(catalogue.ITEMS).index(key)
    s.export_group(key,((i%4-1.5)*.32,(i//4-1.5)*.23,0))
with tempfile.TemporaryDirectory() as work:
    library=Path(work)/"Workshop.blend"
    bpy.data.libraries.write(str(library),{bpy.data.collections[key] for key in pair},compress=True)
    catalogue.portraits(bpy.context.scene,pair)
    bpy.ops.wm.open_mainfile(filepath=str(master))
    bpy.context.preferences.filepaths.save_version=0
    for key in pair:
        collection=bpy.data.collections[key]
        for obj in list(collection.objects):bpy.data.objects.remove(obj,do_unlink=True)
        bpy.data.collections.remove(collection)
    bpy.ops.outliner.orphans_purge(do_recursive=True)
    with bpy.data.libraries.load(str(library),link=False) as (src,target):
        target.collections=list(pair)
    for col in target.collections:
        bpy.context.scene.collection.children.link(col)
        for obj in col.all_objects:
            if obj.type!="MESH":continue
            for slot in obj.material_slots:
                mat=slot.material
                if len(mat.name)>4 and mat.name[-4]=='.' and mat.name[-3:].isdigit():
                    old=bpy.data.materials.get(mat.name[:-4])
                    if old:slot.material=old
    bpy.ops.outliner.orphans_purge(do_recursive=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(master),compress=True)
print("CABINET PAIR complete",master)
