"""Build the 1:1 rigged North Fell detective; run in Blender 5 in background."""
import sys,json,re,os,tempfile
from pathlib import Path
import numpy as np
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree
import bmesh
from math import sin,cos,pi,tau
HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[2]
sys.path.insert(0,str(HERE));sys.path.insert(0,str(ROOT/'Source/Pass/Blender'))
import station_heritage as craft
import tailoring as tailor

OUT=ROOT/'Assets/Transperceneige/Pass/Detective'
MASTER=ROOT/'Source/Pass/WinterbourneDetective.blend'
SKETCH=Path(tempfile.gettempdir())/'HolmesArtbench'
import ast
tree=ast.parse((HERE/'detective_finishes.py').read_text(encoding='utf8'))
SPEC=next(ast.literal_eval(node.value) for node in tree.body if isinstance(node,ast.Assign) and getattr(node.targets[0],'id',None)=='SPEC')
from uuid import uuid5,NAMESPACE_URL


def native_materials():
    craft.OUT=OUT
    craft.stable_guid=lambda key:uuid5(NAMESPACE_URL,"winterbourne/detective/"+key).hex
    for name,(hue,metal,rough,texture) in SPEC.items():
        craft.mat(name,hue,metal,rough,OUT/(name+'.mat'),OUT/texture if texture else None)
        bsdf=craft.materials[name].node_tree.nodes.get("Principled BSDF")
        if name==tailor.SKIN:
            bsdf.inputs["Subsurface Weight"].default_value=.065
            bsdf.inputs["Subsurface Scale"].default_value=.00155
        elif name in (tailor.TWEED,tailor.GLOVE,tailor.TROUSER,tailor.VEST,tailor.SHIRT):
            bsdf.inputs["Sheen Weight"].default_value=.125
        elif name in (tailor.EYE,tailor.IRIS):
            bsdf.inputs["Coat Weight"].default_value=.43;bsdf.inputs["Coat Roughness"].default_value=.145


class GarmentPanel(craft.Part):
    def face(self,points,material,smooth=False,uvs=None):
        if uvs is None and material in (tailor.TWEED,tailor.TROUSER):
            ps=[Vector(p) for p in points]
            n=(ps[1]-ps[0]).cross(ps[2]-ps[0])
            ax=max(range(3),key=lambda i:abs(n[i]))
            # Warp follows the drop of the coat. On the crown it continues over the brim.
            pair=(1,2) if ax==0 else (0,2) if ax==1 else (0,1)
            uvs=[(p[pair[0]]*10.8+.23,p[pair[1]]*10.8+.37) for p in ps]
        return super().face(points,material,smooth,uvs)


def new_mesh(name,coords,tris,material):
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(coords,[],tris);mesh.materials.append(craft.materials[material])
    mesh.polygons.foreach_set("use_smooth",[True]*len(tris));mesh.update()
    obj=bpy.data.objects.new(name,mesh);craft.collection.objects.link(obj);return obj


def add_armature(joints):
    ar=bpy.data.armatures.new("Winterbourne detective skeleton")
    rig=bpy.data.objects.new("DetectiveRig",ar);craft.collection.objects.link(rig)
    bpy.context.view_layer.objects.active=rig;rig.select_set(True);bpy.ops.object.mode_set(mode="EDIT")
    for name,(head,tail,parent) in joints.items():
        bone=ar.edit_bones.new(name);bone.head=head;bone.tail=tail
        if (bone.tail-bone.head).length<.001:bone.tail=bone.head+Vector((0,0,.015))
    for name,(_,__,parent) in joints.items():
        if parent:ar.edit_bones[name].parent=ar.edit_bones[parent]
    bpy.ops.object.mode_set(mode="OBJECT");ar.display_type="STICK"
    rig.show_in_front=False
    return rig


def attach(obj,rig):
    obj.parent=rig
    mod=obj.modifiers.new("Performer deformation","ARMATURE");mod.object=rig


def head_and_hands(data,rig):
    coords=data['coords'];faces=data['tris'];weights=json.loads(str(data['weights']));skin_tree=None
    for region,name,mat in ((0,"Sculpted features, ears and neck",tailor.SKIN),(1,"Anatomically fitted pale suede gloves",tailor.GLOVE)):
        chosen=data['mats']==region
        if region==1:
            # Keep the hand and a tailored wrist under each sleeve.
            chosen &= np.mean(coords[faces,2],axis=1)<.906
        selected=faces[chosen];used=np.unique(selected);indices={int(old):i for i,old in enumerate(used)}
        newidx=np.searchsorted(used,selected);points=coords[used]
        obj=new_mesh(name,points.tolist(),newidx.tolist(),mat)
        uv=obj.data.uv_layers.new(name='Anatomical UV')
        uv.data.foreach_set('uv',data['uv'][data['uvtris'][chosen]].reshape(-1).tolist())
        for bone,values in weights.items():
            subset=[(indices[index],value) for index,value in values if index in indices and value>.0009]
            if not subset:continue
            grp=obj.vertex_groups.new(name=bone)
            for idx,val in subset:grp.add([idx],val,'REPLACE')
        attach(obj,rig)
        if region==0:
            skin_tree=BVHTree.FromPolygons(points.tolist(),newidx.tolist(),all_triangles=True)
            # A fitted hairline derived from the brow, temples and nape. Don't
            # occlude the sculpted ear rim with a generic ellipsoid hair mesh.
            center=points[newidx].mean(axis=1);x,y,z=center.T
            angle=np.arctan2(x,-(y+.060))
            hairline=1.515+.155*np.clip((np.cos(angle)+.21)/1.21,0,1)
            hairline+=.013*np.cos(3*angle+.37)
            chosen_hair=(z>hairline)&((np.abs(x)<.082)|(z>1.646)|(y>-.012))
            hp=points.copy()
            outward=hp-np.array([0,-.060,1.638]);outward/=np.maximum(.0001,np.linalg.norm(outward,axis=1))[:,None]
            hp+=outward*.0024
            attached=new_mesh("Shaped scalp and sideburns",hp.tolist(),newidx[chosen_hair].tolist(),tailor.HAIR)
            grp=attached.vertex_groups.new(name="Head");grp.add(list(range(len(hp))),1,'REPLACE');attach(attached,rig)
    return skin_tree


def section_weights(section,p):
    x,y,z=p
    def pair(first,second,blend):
        t=max(0,min(1,blend));return {first:1-t,second:t}
    if section=="trunk":
        if z<.90:
            if abs(x)>.035:
                side="Left" if x>0 else "Right"
                return pair("Hips",side+"UpperLeg",(.90-z)*1.7)
            return {"Hips":1}
        if z<1.07:return pair("Hips","Spine",(z-.90)/.17)
        if z<1.19:return pair("Spine","SpineMid",(z-1.07)/.12)
        return pair("SpineMid","Chest",(z-1.19)/.12)
    if section.endswith(" sleeve"):
        side=section.split()[0];return pair(side+"LowerArm",side+"UpperArm",(z-1.065)/.112)
    if section.endswith(" trouser"):
        side=section.split()[0];return pair(side+"LowerLeg",side+"UpperLeg",(z-.430)/.125)
    return {section:1.}


def bind_garment(obj,section,rig):
    bm=bmesh.new();bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.000007);bm.to_mesh(obj.data);bm.free();obj.data.update()
    groups={}
    for i,vert in enumerate(obj.data.vertices):
        for key,wt in section_weights(section,vert.co).items():
            if wt<.001:continue
            if key not in groups:groups[key]=obj.vertex_groups.new(name=key)
            groups[key].add([i],wt,'REPLACE')
    attach(obj,rig)


def portraits(scene,mode):
    scene.render.engine="CYCLES";scene.cycles.samples=26;scene.cycles.use_denoising=True
    scene.world=bpy.data.worlds.new("Portrait atelier");scene.world.use_nodes=True
    bg=scene.world.node_tree.nodes.get("Background");bg.inputs['Color'].default_value=(.30,.34,.35,1);bg.inputs['Strength'].default_value=.30
    scene.view_settings.look="AgX - Medium High Contrast"
    scene.render.film_transparent=True;scene.render.image_settings.color_mode="RGBA"
    cam=bpy.data.cameras.new("Portrait study");eye=bpy.data.objects.new(cam.name,cam);scene.collection.objects.link(eye);scene.camera=eye
    for xyz,size,power,col in (((-1.25,-1.65,2.5),1.48,83,(1,.89,.76)),((1.1,-1.12,1.85),.97,45,(.78,.86,1)),((.8,.81,2.3),.7,145,(.9,.89,.8))):
        d=bpy.data.lights.new("Softbox","AREA");d.energy=power;d.size=size;d.color=col
        ob=bpy.data.objects.new("Softbox",d);scene.collection.objects.link(ob);ob.location=xyz
        ob.rotation_euler=(Vector((0,0,1.25))-ob.location).to_track_quat("-Z","Y").to_euler()
    for name,pos,look,scale,res in (("full",(.563,-3.45,1.55),(0,0,.939),2.02,(620,840)),("face",(.36,-1.85,1.64),(0,-.061,1.627),.52,(690,800))):
        cam.type="ORTHO";cam.ortho_scale=scale;eye.location=pos;eye.rotation_euler=(Vector(look)-eye.location).to_track_quat("-Z","Y").to_euler()
        scene.render.resolution_x,scene.render.resolution_y=res;scene.render.resolution_percentage=100
        scene.render.filepath=str(SKETCH/(name+"-detective.png"));bpy.ops.render.render(write_still=True)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version=0
    scene=bpy.context.scene;scene.unit_settings.system="METRIC";scene.name="Winterbourne | Sherlock Holmes"
    craft.collection=bpy.data.collections.new("The consulting detective")
    scene.collection.children.link(craft.collection);native_materials();craft.Part=GarmentPanel
    data=np.load(HERE/'Holmes anatomy.npz');joints=json.loads(str(data['joints']))
    rig=add_armature(joints)
    skin=head_and_hands(data,rig)
    def front(x,z):
        co,_,__,___=skin.ray_cast(Vector((x,-.45,z)),Vector((0,1,0)),.70)
        return co.y if co else -.066
    tailor.boots_and_trousers();tailor.coat();tailor.cap();tailor.eyes_and_brows(joints,front)
    for side,name in ((1,'Left'),(-1,'Right')):
        tailor.part(name+" three-point stitched kid seams",name+'Hand')
        c=Vector(joints[name+'Hand'][0]);know=np.asarray(data['coords'])
        for j in (-1,0,1):
            pts=[]
            for i in range(12):
                zz=c.z-.027-.061*i/11; yy=c.y-.002+j*.007-.007*i/11
                cand=know[(know[:,0]*side>.175)&(np.abs(know[:,2]-zz)<.010)&(np.abs(know[:,1]-yy)<.008)]
                if len(cand):pts.append((side*(np.max(cand[:,0]*side)+.0007),yy,zz))
            if len(pts)>2:tailor.roll(tailor.WELT,pts,.00068)
    craft.group.flush();craft.group=None
    for ob in craft.parts:bind_garment(ob,tailor.SECTIONS[ob.name],rig)
    for img in bpy.data.images:
        if img.packed_file:img.filepath="//../../"+(OUT/img.name).relative_to(ROOT).as_posix()
    for area in bpy.context.screen.areas:
        if area.type=='VIEW_3D':
            v=area.spaces.active;v.region_3d.view_distance=2.75;v.region_3d.view_location=(0,0,.97)
            v.region_3d.view_rotation=Vector((-.13,1,-.09)).to_track_quat('-Z','Y');v.shading.color_type='MATERIAL'
    bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig
    bpy.ops.wm.save_as_mainfile(filepath=str(MASTER),compress=True)
    # Batch renderables into two skins, retaining named joints and every material
    # slot. The independently editable pieces stay unmerged in the Blender master.
    objects=[o for o in craft.collection.objects if o.type=='MESH']
    garments=[o for o in objects if o.name not in ("Sculpted features, ears and neck","Anatomically fitted pale suede gloves")]
    bpy.ops.object.select_all(action='DESELECT')
    for ob in garments:ob.select_set(True)
    bpy.context.view_layer.objects.active=garments[0];bpy.ops.object.join();bpy.context.active_object.name="Tailoring, hair and eyes"
    bpy.ops.object.select_all(action='SELECT')
    dest=OUT/'Detective.fbx'
    bpy.ops.export_scene.fbx(filepath=str(dest),use_selection=True,object_types={'ARMATURE','MESH'},axis_forward='-Z',axis_up='Y',
        use_triangles=True,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_anim=False,
        use_mesh_modifiers=True,add_leaf_bones=False,path_mode='STRIP',mesh_smooth_type='FACE',use_custom_props=False)
    used={slot.material.name for obj in bpy.context.scene.objects if obj.type=='MESH' for slot in obj.material_slots if slot.material}
    craft.remap_fbx(dest,used)
    meta=dest.with_suffix('.fbx.meta');text=meta.read_text().replace("animationType: 0","animationType: 2")
    meta.write_text(text,encoding='utf8');print("DETECTIVE FBX EXPORTED",len(rig.data.bones),len(used),flush=True)
    portraits(scene,None)

if __name__=="__main__":main()
