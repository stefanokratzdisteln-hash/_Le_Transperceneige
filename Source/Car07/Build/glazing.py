"""Thin glazing in front of the recessed parlour bays, with frost in the corners."""
from math import cos, pi, sin
import bpy
import numpy as np

from carbody import _rounded
from finishes import fields, write_png


def make_material(folder):
    n = 512
    rng = np.random.default_rng(1907)
    yy, xx = np.mgrid[0:n,0:n]/n
    noise = fields(rng,n,36,28)
    edge = np.minimum.reduce((xx,1-xx,yy*.68,(1-yy)*1.12))
    frost = np.clip((.053 + .014*noise + .014*np.sin(18*xx) - edge)*7,0,.57)
    # Dendrites grow inward from the bottom corners and taper at their tips.
    def stroke(a,b,width,opacity):
        dx,dy = b[0]-a[0],b[1]-a[1]
        t = np.clip(((xx-a[0])*dx+(yy-a[1])*dy)/(dx*dx+dy*dy),0,1)
        dist = np.hypot(xx-a[0]-t*dx,yy-a[1]-t*dy)
        np.maximum(frost,opacity*np.clip(1-dist/width,0,1),out=frost)
    for k in range(19):
        hand = 1 if k%2 else -1
        a = np.array([rng.uniform(.0,.24) if hand==1 else rng.uniform(.77,1),rng.uniform(-.035,.013)])
        angle = rng.uniform(.65,1.49)
        length = rng.uniform(.06,.20)
        direction = np.array([hand*cos(angle),sin(angle)])
        b = a+length*direction
        stroke(a,b,.0019,.40)
        for j in range(1,7):
            base = a+direction*length*j/8
            for branch in (-1,1):
                theta = angle+branch*pi/4.8
                tip=base+np.array([hand*cos(theta),sin(theta)])*length*.30*(1-j/9)
                stroke(base,tip,.0014,.34)
    rgb = np.zeros((n,n,4),np.uint8)
    rgb[:,:,:3] = (170,198,203)
    reflection = np.exp(-((yy-.40*xx-.66)/.045)**2)*.025
    rgb[:,:,3] = np.clip((.032 + frost + reflection)*255,0,255)
    path = folder/'Car07_Glazing.png'
    write_png(path,rgb)
    image = bpy.data.images.load(str(path))
    image.pack()
    mat = bpy.data.materials.new('Car07_CabinGlass')
    mat.use_nodes = True
    mat.diffuse_color = (.54,.64,.67,.09)
    nodes = mat.node_tree.nodes
    shader = next(node for node in nodes if node.type == 'BSDF_PRINCIPLED')
    tex = nodes.new('ShaderNodeTexImage')
    tex.image = image
    tex.location = (-570,200)
    mat.node_tree.links.new(tex.outputs['Color'],shader.inputs['Base Color'])
    mat.node_tree.links.new(tex.outputs['Alpha'],shader.inputs['Alpha'])
    shader.inputs['Roughness'].default_value = .25
    shader.inputs['Metallic'].default_value = .07
    return mat


def build(collection,folder,mat=None,number=7):
    from extension import CUT, INSERT, OUTER
    if mat is None:
        mat = make_material(folder)
    result = []
    for side, suffix in [(-1,'Port'),(1,'Starboard')]:
        verts,faces,coords = [],[],[]
        def pane(cx,cz,w,h,r,depth,flip):
            face=[]
            for x,z in _rounded(cx,cz,w,h,r):
                face.append(len(verts))
                verts.append((x,side*depth,z))
                u,v=(x-cx)/w+.5,(z-cz)/h+.5
                coords.append((1-u if flip else u,v))
            faces.append(face if side==-1 else face[::-1])
        def batch(name):
            mesh=bpy.data.meshes.new(name)
            mesh.from_pydata(verts,[],faces)
            uv=mesh.uv_layers.new(name='Glazing')
            for loop in mesh.loops:
                uv.data[loop.index].uv=coords[loop.vertex_index]
            mesh.materials.append(mat)
            obj=bpy.data.objects.new(mesh.name,mesh)
            collection.objects.link(obj)
            result.append(obj)
            verts.clear()
            faces.clear()
            coords.clear()
        for wing in (-1,1):
            for i in range(4):
                cx = wing*(2.37 + i*1.38)
                pane(cx,2.980,1.060,.770,.079,1.505,(i+side)%2)
        batch('Window_glass_'+suffix)
        for wing in (-1,1):
            centre=wing*(CUT+INSERT/2)
            for i in range(4):
                pane(centre+(i-1.5)*INSERT/4,2.951,INSERT/4-.059,.91,.012,OUTER-.06,(i%2)==0)
        batch('Gallery_glass_'+suffix)
        if number in (3,9) and side==-1:
            length=CUT-2.82+.03
            for wing in (-1,1):
                centre=wing*(CUT+2.82)/2
                for i in range(4):
                    pane(centre+(i-1.5)*length/4,2.893,length/4-.055,1.03,.012,OUTER-.06,i%2)
            batch('Dispatch_aisle_glass')
        for wing in (-1,1):
            pane(wing*(8.04+INSERT),2.994,.605,.720,.092,1.477,wing==side)
            batch('Glass_Door_'+suffix+('_A' if wing<0 else '_B'))
    if number==4:
        x=CUT+INSERT/2+1.62
        verts=[(x,hand*1.64+y,z) for hand in (-1,1) for y,z in
               ((-.277,2.21),(.277,2.21),(.277,2.88),(-.277,2.88))]
        mesh=bpy.data.meshes.new('Drawing room glazed screens')
        mesh.from_pydata(verts,[],[(0,1,2,3),(4,5,6,7)])
        uv=mesh.uv_layers.new(name='Glazing')
        for p in mesh.polygons:
            for k,j in enumerate(p.loop_indices):uv.data[j].uv=((.04,.04),(.96,.04),(.96,.96),(.04,.96))[k]
        mesh.materials.append(mat)
        obj=bpy.data.objects.new(mesh.name,mesh);collection.objects.link(obj);result.append(obj)
    return result
