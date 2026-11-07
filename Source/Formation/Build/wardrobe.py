"""Shared cloth and complexion swatches for the optional seated travelling party."""
import bpy
import numpy as np
from finishes import write_png


SWATCHES={
    'skin_rose':'c8947e','skin_honey':'bc875f','skin_umber':'895d47','lip':'a3605b',
    'hair_dark':'332c29','hair_chestnut':'654a3b','hair_silver':'a69f91','hair_fair':'a18762',
    'navy':'2d3b4f','moss':'555f49','tweed':'857461','oxblood':'723f40',
    'ochre':'ab8553','plum':'66495e','slate':'657178','cream':'d9c8ab',
    'linen':'e7dbbf','black':'2d2b2d','leather':'78543e','brass':'b09667',
    'paper':'dad4bf','ink':'383c3b','white':'eee9d8','teal':'426769'
}


def fabric(folder):
    folder.mkdir(parents=True,exist_ok=True)
    n=128
    pixels=np.zeros((4*n,8*n,3),np.uint8)
    rng=np.random.default_rng(1932)
    y,x=np.mgrid[:n,:n]
    for i,(name,colour) in enumerate(SWATCHES.items()):
        base=np.array(list(bytes.fromhex(colour)))
        grain=rng.normal(0,.95,(n,n))
        if name in ('moss','tweed','navy','slate','oxblood','plum','teal'):
            warp=np.where((x//16)%2,x+y,x-y)
            grain+=3*np.cos(warp*np.pi/3)+np.sin(x*np.pi/2)
            if name=='tweed':
                grain+=8*((y%28)<2)-5*((x%26)<2)
        c,r=i%8*n,i//8*n
        pixels[r:r+n,c:c+n]=np.clip(base+grain[:,:,None],0,255)
    path=folder/'Travellers.png'
    write_png(path,pixels)
    tex=bpy.data.images.load(str(path))
    tex.pack()
    mat=bpy.data.materials.new('Travellers_Cloth')
    mat.use_nodes=True
    nodes=mat.node_tree.nodes
    shader=next(node for node in nodes if node.type=='BSDF_PRINCIPLED')
    shader.inputs['Roughness'].default_value=.72
    shader.inputs['Metallic'].default_value=.06
    image=nodes.new('ShaderNodeTexImage')
    image.image=tex
    image.location=(-480,160)
    nodes.active=image
    mat.node_tree.links.new(image.outputs['Color'],shader.inputs['Base Color'])
    return mat
