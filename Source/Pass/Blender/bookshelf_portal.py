"""Create the editable metric portal master from the exact shipped Unity prefab.

First run Editor/PassPortalMeshExport.cs in the Unity project to create the
temporary JSON gzip, then run this file with Blender:
blender -b --factory-startup --python-exit-code 1 -P bookshelf_portal.py -- snapshot.json.gz
"""
import gzip
import json
import math
import sys
from collections import defaultdict
from pathlib import Path
import bpy
from mathutils import Matrix, Vector, Quaternion

ROOT = Path(__file__).resolve().parents[3]
OUTPUT = ROOT/'Source/Pass/BookshelfPortal.blend'
BASIS = Matrix(((1,0,0),(0,0,1),(0,1,0))).to_4x4()


def xyz(p): return (p['x'],p['y'],p['z'])
def point(p): return (p['x'],p['z'],p['y'])
def trans(p):
    pos = Vector(xyz(p['position']))
    q = p['rotation']
    rot = Quaternion((q['w'],q['x'],q['y'],q['z']))
    return BASIS @ Matrix.LocRotScale(pos,rot,Vector(xyz(p['scale']))) @ BASIS


def linear(v): return v/12.92 if v <= .04045 else ((v+.055)/1.055)**2.4
def colour(c): return (*(linear(c[a]) for a in ('r','g','b')), c.get('a',1))


def portal_shader(material, shader):
    # The Unity effect is real-time HLSL; these editable nodes provide its
    # ice-blue, vertically striated preview in a portable Blender material.
    tree=material.node_tree
    uv=tree.nodes.new('ShaderNodeTexCoord')
    mapping=tree.nodes.new('ShaderNodeVectorMath');mapping.operation='MULTIPLY'
    mapping.inputs[1].default_value=(3.4,.55,1)
    tree.links.new(uv.outputs['UV'],mapping.inputs[0])
    noise=tree.nodes.new('ShaderNodeTexNoise')
    noise.inputs['Scale'].default_value=3.5
    noise.inputs['Detail'].default_value=4
    noise.inputs['Roughness'].default_value=.67
    tree.links.new(mapping.outputs[0],noise.inputs['Vector'])
    ramp=tree.nodes.new('ShaderNodeValToRGB')
    for i,(v,col) in enumerate([(0.22,(.015,.045,.114,1)),(.72,(.27,.60,.94,1)),(.9,(.63,.90,1,1))]):
        e=ramp.color_ramp.elements[i] if i<2 else ramp.color_ramp.elements.new(v)
        e.position=v;e.color=col
    tree.links.new(noise.outputs['Fac'],ramp.inputs[0])
    tree.links.new(ramp.outputs['Color'],shader.inputs['Base Color'])
    tree.links.new(ramp.outputs['Color'],shader.inputs['Emission Color'])
    shader.inputs['Emission Strength'].default_value=1.3
    shader.inputs['Alpha'].default_value=.83
    material.surface_render_method='DITHERED'
    uv.location=(-620,10);mapping.location=(-430,10);noise.location=(-220,10);ramp.location=(0,10);shader.location=(300,20)
    tree.nodes.get('Material Output').location=(615,20)


def surface(data,images):
    mat=bpy.data.materials.new(data['name'])
    mat.use_nodes=True
    shader=mat.node_tree.nodes.get('Principled BSDF')
    col=colour(data['colour'])
    mat.diffuse_color=col
    shader.inputs['Base Color'].default_value=col
    shader.inputs['Metallic'].default_value=data['metal']
    shader.inputs['Roughness'].default_value=max(.04,1-data['smooth'])
    if data['shader']=='Winterbourne/Portal glass':
        portal_shader(mat,shader)
        return mat
    shader.inputs['Emission Color'].default_value=colour(data['glow'])
    shader.inputs['Emission Strength'].default_value=1.0
    if not data['colourMap'] and not data['normalMap']: return mat
    tree=mat.node_tree;nodes=tree.nodes
    uv=nodes.new('ShaderNodeTexCoord');uv.location=(-880,5)
    mapping=nodes.new('ShaderNodeMapping');mapping.location=(-645,20)
    mapping.inputs['Scale'].default_value=(data['tiling']['x'],data['tiling']['y'],1)
    mapping.inputs['Location'].default_value=(data['offset']['x'],data['offset']['y'],0)
    tree.links.new(uv.outputs['UV'],mapping.inputs['Vector'])
    for key,normal in [('colourMap',False),('normalMap',True)]:
        relative=data[key]
        if not relative: continue
        if (relative,normal) not in images:
            source=ROOT/relative
            if not source.is_file(): raise FileNotFoundError(source)
            loaded=bpy.data.images.load(str(source),check_existing=False)
            if normal: loaded.colorspace_settings.name='Non-Color'
            loaded.pack()
            loaded.filepath='//../../'+relative
            images[(relative,normal)]=loaded
        texture=nodes.new('ShaderNodeTexImage');texture.image=images[(relative,normal)]
        texture.location=(-412,-264 if normal else 90)
        tree.links.new(mapping.outputs['Vector'],texture.inputs['Vector'])
        if normal:
            nm=nodes.new('ShaderNodeNormalMap');nm.location=(-95,-240)
            nm.inputs['Strength'].default_value=data['bump']
            tree.links.new(texture.outputs['Color'],nm.inputs['Color'])
            tree.links.new(nm.outputs['Normal'],shader.inputs['Normal'])
        elif any(abs(v-1)>.0001 for v in col[:3]):
            tint=nodes.new('ShaderNodeMixRGB');tint.blend_type='MULTIPLY';tint.location=(-91,100)
            tint.inputs[0].default_value=1;tint.inputs[2].default_value=col
            tree.links.new(texture.outputs['Color'],tint.inputs[1])
            tree.links.new(tint.outputs['Color'],shader.inputs['Base Color'])
        else: tree.links.new(texture.outputs['Color'],shader.inputs['Base Color'])
    return mat


def faces(indexes):
    # The mesh author emits four vertices for a quad and three for a triangle.
    # Restore its editable quads, preserving both winding variants and UV seams.
    faces_out=[]
    i=0
    while i<len(indexes):
        a,b,c=indexes[i:i+3]
        if i+6<=len(indexes):
            d,e,f=indexes[i+3:i+6]
            if a==d and len({a,b,c,d,e,f})==4:
                if c==e: faces_out.append((a,f,c,b));i+=6;continue
                if b==f: faces_out.append((a,c,b,e));i+=6;continue
        faces_out.append((a,c,b));i+=3
    return faces_out


def make_mesh(label, tagged_faces, data, slots, parent, centre=False):
    me=bpy.data.meshes.new(label)
    obj=bpy.data.objects.new(label,me)
    bpy.context.collection.objects.link(obj);obj.parent=parent
    points=[];indices=[];lookup={}
    for _,face in tagged_faces:
        corners=[]
        for k in face:
            p=point(data['positions'][k])
            key=tuple(round(v,7) for v in p)
            if key not in lookup:
                lookup[key]=len(points)
                points.append(p)
            corners.append(lookup[key])
        indices.append(corners)
    if centre:
        pivot=Vector(tuple((min(p[i] for p in points)+max(p[i] for p in points))*.5 for i in range(3)))
        obj.location=pivot;points=[Vector(p)-pivot for p in points]
    me.from_pydata(points,[],indices)
    used=sorted(set(slot for slot,face in tagged_faces))
    for slot in used:me.materials.append(slots[slot])
    layer=me.uv_layers.new(name='Unity UV0')
    for poly,(slot,face) in zip(me.polygons,tagged_faces):
        poly.material_index=used.index(slot)
        for loop,src in zip(poly.loop_indices,face):
            uv=data['uv'][src]
            layer.data[loop].uv=(uv['x'],uv['y'])
    me.update()
    return obj


def objects_for_mesh(node,owner,marks,materials):
    data=node['mesh'];fixed=[];book_count=0
    for section in data['parts']:
        slot=section['material'];quads=faces(section['triangles'])
        if marks[slot]['colourMap'].endswith('/LibraryBindings.png'):
            # Each bound volume contains ten faces for its boards, three paper
            # edges and three facets across its spine, authored consecutively.
            assert len(quads)%16==0 and all(len(f)==4 for f in quads),'Unexpected book binding topology'
            for i in range(0,len(quads),16):
                name='Library volume %02d'%(book_count+1)
                make_mesh(name,[(slot,f) for f in quads[i:i+16]],data,materials,owner,centre=True)
                book_count+=1
        else: fixed.extend((slot,f) for f in quads)
    if fixed: make_mesh(node['name']+' geometry',fixed,data,materials,owner)
    return book_count


def make(snapshot):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene=bpy.context.scene;scene.unit_settings.system='METRIC'
    scene.unit_settings.length_unit='METERS'
    marks=snapshot['materials'];images={};materials=[surface(m,images) for m in marks]
    nodes=snapshot['nodes'];needed={snapshot['leaf']}
    for idx,node in enumerate(nodes):
        if (node.get('mesh') or {}).get('positions') or node.get('lettering'):
            needed.add(idx)
    for idx in list(needed):
        ancestor=nodes[idx]['parent']
        while ancestor>=0:
            needed.add(ancestor);ancestor=nodes[ancestor]['parent']
    instances={};book_count=0
    for idx,node in enumerate(nodes):
        if idx not in needed: continue
        obj=bpy.data.objects.new(node['name'],None);bpy.context.collection.objects.link(obj)
        obj.empty_display_size=.035;instances[idx]=obj
        if node['parent']>=0:obj.parent=instances[node['parent']]
        obj.matrix_basis=trans(node)
        if (node.get('mesh') or {}).get('positions'):
            book_count+=objects_for_mesh(node,obj,marks,materials)
        if node.get('lettering'):
            label=bpy.data.curves.new('Replaceable destination lettering','FONT')
            label.body=node['lettering'];label.align_x='CENTER';label.align_y='CENTER'
            label.size=node['textSize'];label.extrude=.00015
            text=bpy.data.objects.new('Destination lettering',label);bpy.context.collection.objects.link(text)
            text.parent=obj;text.rotation_euler.x=math.pi/2
            lettering=bpy.data.materials.new('Enamel lettering');lettering.diffuse_color=colour(node['textColour'])
            label.materials.append(lettering)
    gate=instances[0];leaf=instances[snapshot['leaf']]
    leaf['Slide distance (m)']=Vector(point(snapshot['closedOffset'])).length
    leaf['Travel time (s)']=snapshot['duration']
    scene.render.fps=24
    stop=1+round(snapshot['duration']*scene.render.fps)
    open_pose=leaf.location.copy()
    leaf.keyframe_insert('location',frame=1)
    leaf.location=open_pose+Vector(point(snapshot['closedOffset']))
    leaf.keyframe_insert('location',frame=stop)
    leaf.animation_data.action.name='Slide the library bookcase across the passage'
    scene.timeline_markers.new('OPEN  |  bookcase left',frame=1)
    scene.timeline_markers.new('CLOSED  |  bookcase covers passage',frame=stop)
    scene.frame_start=1;scene.frame_end=stop;scene.frame_set(1)
    gate['Source']='Assets/Transperceneige/Pass/Waygate.prefab'
    leaf['Editing']='Move this parent to slide the cabinet, books, trim and rollers together.'
    assert book_count>0 and {'LibraryBindings.png'} <= {Path(p).name for p,n in images}
    assert all(im.packed_file for im in images.values())
    bpy.context.view_layer.objects.active=leaf;leaf.select_set(True)
    target=Vector((-.75,0,1.30));view=Vector((.85,-6.8,2.15))
    for scr in bpy.data.screens:
        for area in scr.areas:
            if area.type=='VIEW_3D':
                v=area.spaces.active;v.region_3d.view_rotation=(target-view).to_track_quat('-Z','Y')
                v.region_3d.view_distance=5.95;v.region_3d.view_location=target;v.lens=40
                v.overlay.show_overlays=False;v.shading.type='MATERIAL'
    bpy.context.preferences.filepaths.save_version=0
    OUTPUT.parent.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT),compress=True,relative_remap=False)
    print('PORTAL SOURCE BLEND:',book_count,'individual books;',len(images),'packed source textures; open',open_pose, 'closed', open_pose+Vector(point(snapshot['closedOffset'])))


if __name__=='__main__':
    path=sys.argv[sys.argv.index('--')+1]
    with gzip.open(path,'rt',encoding='utf-8-sig') as stream: make(json.load(stream))
