"""Generate the shared finish atlas and its Unity metallic/smoothness channels."""
import struct
import zlib
import bpy
import numpy as np

# sRGB albedo, metallic, smoothness, sRGB emissive colour.
FINISHES = {
    'petrol':       ('533226', .20, .67, '000000'),
    'petrol_light': ('79503b', .23, .52, '000000'),
    'ivory':        ('edddb1', .17, .55, '000000'),
    'bone':         ('f8e6bb', .12, .50, '000000'),
    'orange':       ('be9551', .55, .56, '000000'),
    'dark':         ('353b3d', .48, .33, '000000'),
    'rubber':       ('262c32', .04, .22, '000000'),
    'steel':        ('66757d', .76, .43, '000000'),
    'bright':       ('aab8b9', .80, .61, '000000'),
    'glass':        ('405654', .38, .77, '0a171a'),
    'warm':         ('d29264', .14, .65, 'd08449'),
    'amber':        ('ffb860', .08, .66, 'ffac55'),
    'snow':         ('dce9ed', .02, .18, '000000'),
    'red':          ('8e2424', .18, .68, 'a72e23'),
    'brass':        ('b7985f', .65, .48, '000000'),
    'velvet':       ('702d32', .02, .25, '000000'),
    'wood':         ('73432e', .10, .59, '000000'),
    'shade':        ('ffdb99', .02, .37, 'ffc777'),
    'gilt':         ('edcb7e', .62, .53, '000000'),
    'ink':          ('1b3230', .24, .54, '000000'),
    'room':         ('ad7950', .04, .35, '74421f'),
    'linen':        ('eee4cb', .01, .25, '000000'),
    'carpet':       ('713f3d', .01, .18, '000000'),
    'hide':         ('8d5439', .04, .46, '000000'),
    'mirror':       ('d9e0e0', .98, .95, '000000'),
    'enamel':       ('efead8', .03, .74, '000000'),
    'parquet':      ('95623b', .04, .54, '000000'),
    'wallcloth':    ('beaf81', .02, .24, '000000'),
    'paper':        ('e9d9b6', .01, .17, '000000'),
}
COLS, ROWS = 8, 4


def rgb(value):
    return np.array(list(bytes.fromhex(value)), dtype=np.float32)


def write_png(path, pixels):
    """Write 8-bit RGB(A) with the origin used by Blender UVs at the lower left."""
    height, width, channels = pixels.shape
    def chunk(kind, data):
        return struct.pack('>I', len(data)) + kind + data + struct.pack('>I', zlib.crc32(kind+data))
    rows = np.pad(pixels[::-1].reshape(height, -1), ((0,0),(1,0)))
    path.write_bytes(b'\x89PNG\r\n\x1a\n' +
                     chunk(b'IHDR', struct.pack('>2I5B', width, height, 8, 2 if channels==3 else 6, 0, 0, 0)) +
                     chunk(b'IDAT', zlib.compress(rows.tobytes(), 7)) + chunk(b'IEND', b''))


def fields(rng, n, nx, ny):
    grid = rng.normal(0, 1, (ny+1, nx+1))
    x, y = np.meshgrid(np.arange(n)*nx/n, np.arange(n)*ny/n)
    ix, iy = x.astype(int), y.astype(int)
    fx, fy = x-ix, y-iy
    fx, fy = fx*fx*(3-2*fx), fy*fy*(3-2*fy)
    return ((grid[iy,ix]*(1-fx) + grid[iy,ix+1]*fx)*(1-fy) +
            (grid[iy+1,ix]*(1-fx) + grid[iy+1,ix+1]*fx)*fy)


def make_atlas(folder):
    n = 512
    w = n//2
    albedo = np.zeros((n*ROWS,w*COLS,3), np.uint8)
    surface = np.zeros((n*ROWS,w*COLS,4), np.uint8)
    emission = np.zeros((64*ROWS,32*COLS,3), np.uint8)
    rng = np.random.default_rng(707)
    y, x = np.mgrid[0:n, 0:n]/n
    for i, (key,(base,metal,smooth,glow)) in enumerate(FINISHES.items()):
        coarse, streak = fields(rng,n,6,7), fields(rng,n,76,4)
        speck, grain = fields(rng,n,88,90), rng.normal(0,1,(n,n))
        colour = rgb(base)[None,None,:] + (2.2*coarse + 1.6*streak + .9*speck + grain*1.2)[:,:,None]
        glowmask = np.ones((n,n))
        if key in ('petrol','petrol_light','orange','ivory','dark','steel','bright','brass'):
            patina = np.clip((coarse*.35 + streak*.48 + speck*.66 + grain*.30 - 1.4)*.32,0,.65)
            oxide = [105,79,58] if key in ('dark','steel','bright') else [110,112,101]
            colour = colour*(1-patina[:,:,None]) + np.array(oxide)*patina[:,:,None]
            # Draw isolated, fine horizontal scores and a few exposed metal flecks.
            for _ in range(55):
                row,col = rng.integers(0,n,2)
                length = rng.integers(1,21)
                colour[row:row+1, col:col+length] += rng.uniform(-18,21)
        if key in ('glass','warm','amber'):
            light = .94 + .26*y + .09*np.cos(6*x+2*y) + .025*streak
            colour = rgb(base)[None,None,:]*light[:,:,None] + grain[:,:,None]*1.3
        if key == 'glass':
            # Gradient of reflected cold sky, with crystalline condensation at the margins.
            margin = np.minimum(np.minimum(x,1-x)*1.25,np.minimum(y,1-y))
            ice = np.clip((.11 - margin + .021*coarse + .015*speck)*6.5,0,.62)
            colour = colour*(.53+.57*y[:,:,None])
            colour += np.exp(-((y-.36*x-.42)/.054)**2)[:,:,None]*np.array([6,10,11])
            colour = colour*(1-ice[:,:,None]) + rgb('b5cacf')*ice[:,:,None]
        if key == 'wood':
            figure = .67*np.sin(94*x + coarse*4.3 + np.sin(12*y)*2.1) + .32*np.sin(267*x+coarse*7)
            colour += figure[:,:,None]*np.array([8,5,3]) + streak[:,:,None]*np.array([4,3,2])
        if key == 'velvet':
            colour += (np.sin(175*x)*1.4 + .8*np.sin(162*y))[:,:,None]
        if key == 'room':
            light = .59 + .56*np.exp(-((x-.52)/.36)**2-((y-.54)/.50)**2) + .08*y
            colour *= light[:,:,None]
            glowmask = light**2
        if key in ('linen','hide','wallcloth','carpet'):
            colour += (1.7*np.sin(697*x) + 1.3*np.cos(512*y) + grain*1.3)[:,:,None]
        if key == 'carpet':
            # Original woven border and repeat of stepped medallions in ochre and sage.
            border = np.minimum(y,1-y)
            diamond = np.abs(x-.5)*1.7 + np.abs(y-.5)*1.45
            pattern = np.zeros((n,n),bool)
            for d in (.16,.205,.37):
                pattern |= np.abs(diamond-d)<.008
            pattern |= (np.abs(border-.072)<.010) | (np.abs(border-.142)<.006)
            buds = np.minimum(np.abs(x-.205),np.abs(x-.795))*.91+np.abs(y-.5)
            pattern |= (buds<.038) | ((border<.11)&(np.abs(np.sin(x*8*np.pi))<.17))
            colour[pattern] = rgb('b79c66') + speck[pattern,None]*6
            inside = (diamond<.15)&~pattern
            colour[inside] = rgb('4a5950') + speck[inside,None]*4
            sprig = (np.abs(x-.5)*1.7+np.abs(y-.5)<.048)
            colour[sprig] = rgb('d1b177')
        if key == 'wallcloth':
            aa,bb = (x*6)%1-.5,(y*7)%1-.5
            motif = (np.abs(np.abs(aa)+np.abs(bb)-.45)<.025) | ((np.abs(aa)*1.5+np.abs(bb))<.09)
            colour[motif] -= [13,11,7]
        if key == 'parquet':
            # Quarter-turn blocks of varnished finger parquet.
            tilex,tiley=np.floor(x*4),np.floor(y*4)
            flip=((tilex+tiley)%2)==1
            u,v=np.where(flip,y,x),np.where(flip,x,y)
            course=np.floor(u*16)
            shade = 7*np.sin(47*course+13*tilex+8*tiley)
            shade += 3.8*np.sin(354*u + 2*np.sin(16*v)+.5*coarse)
            shade -= 30*((u*16%1<.027)|(v*4%1<.009))
            colour += shade[:,:,None]
        if key == 'snow':
            colour += (coarse*3 + speck*2 + grain*2)[:,:,None]
        a,b = i%COLS*w,i//COLS*n
        albedo[b:b+n,a:a+w] = np.clip((colour[:,::2]+colour[:,1::2])*.5,0,255)
        tile = surface[b:b+n,a:a+w]
        tile[:,:,0] = int(metal*255)
        tile[:,:,1] = 255
        tile[:,:,3] = np.clip(smooth*255 + 4*coarse[:,::2] + 3*streak[:,::2],0,255)
        a,b = i%COLS*32,i//COLS*64
        emission[b:b+64,a:a+32] = np.clip(rgb(glow)*glowmask[::8,::16,None],0,255)
    result = []
    for name,pixels in [('Livery',albedo),('MetalSmooth',surface),('Lamps',emission)]:
        path = folder / ('Car07_'+name+'.png')
        write_png(path,pixels)
        image = bpy.data.images.load(str(path))
        if name == 'MetalSmooth':
            image.colorspace_settings.name = 'Non-Color'
            image.alpha_mode = 'CHANNEL_PACKED'
        image.pack()
        result.append(image)
    return result


def material(images):
    mat = bpy.data.materials.new('Car07_Livery')
    mat.diffuse_color = (.15,.25,.29,1)
    mat.use_nodes = True
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    shader = next(node for node in nodes if node.type == 'BSDF_PRINCIPLED')
    for index,(image,dest) in enumerate(zip(images,('Base Color',None,'Emission Color'))):
        tex = nodes.new('ShaderNodeTexImage')
        tex.location = (-700, 300-index*320)
        tex.image = image
        if dest:
            links.new(tex.outputs['Color'], shader.inputs[dest])
        else:
            sep = nodes.new('ShaderNodeSeparateColor')
            sep.location = (-400,0)
            links.new(tex.outputs['Color'],sep.inputs[0])
            links.new(sep.outputs['Red'],shader.inputs['Metallic'])
            inv = nodes.new('ShaderNodeMath')
            inv.operation = 'SUBTRACT'
            inv.location = (-400,-160)
            inv.inputs[0].default_value = 1.0
            links.new(tex.outputs['Alpha'],inv.inputs[1])
            links.new(inv.outputs[0],shader.inputs['Roughness'])
    shader.inputs['Emission Strength'].default_value = .85
    return mat
