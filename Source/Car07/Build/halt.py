"""Render-only winter surroundings for the Fellbridge exterior plate.

Call build() after track.build(). The returned collection can be hidden for
the detail photograph without altering the model or the photographic track.
"""

from math import cos, exp, pi, sin
from pathlib import Path
from random import Random

import bpy
from mathutils import Vector
from mathutils.noise import noise

from track import Batch, finish


def _tube(batch, a, b, radius_a, radius_b, index=0, sides=7):
    a, b = Vector(a), Vector(b)
    axis = (b - a).normalized()
    u = axis.orthogonal().normalized()
    v = axis.cross(u)
    rings = []
    for p, radius in ((a, radius_a), (b, radius_b)):
        rings.append([p + radius * (u * cos(i * 2 * pi / sides) + v * sin(i * 2 * pi / sides))
                      for i in range(sides)])
    batch.stack(rings, index)


def _turned(batch, x, y, levels, index=0, sides=12, angle=0):
    rings = [[(x + r * cos(angle + i * 2 * pi / sides),
               y + r * sin(angle + i * 2 * pi / sides), z) for i in range(sides)]
             for z, r in levels]
    batch.stack(rings, index)


def _snowfield(collection, snow):
    field = Batch(collection, 'Fellbridge | wind-packed snow', [snow])
    # Denser samples around the track follow the actual sleeper and ballast
    # heights. Beyond the formation, long shallow undulations catch side light.
    ys = [-60, -42, -32, -25, -20, -16, -13]
    ys += [-12 + i * .38 for i in range(26)]
    ys += [-2.32 + i * .116 for i in range(41)]
    ys += [2.57 + i * .38 for i in range(26)]
    ys += [13, 15, 17, 20, 23, 27, 32, 38, 46, 60]
    nx = 245
    for j, y in enumerate(ys):
        for i in range(nx):
            x = -61 + i * .5
            broad = noise(Vector((x * .17, y * .34, 4.21)))
            ripple = noise(Vector((x * .48, y * 1.83, .52)))
            formation = exp(-(abs(y) / 1.77) ** 8)
            # Meandering banks wrap naturally over the edge of the rail bed.
            front = exp(-((y + 1.59 + .13 * sin(x * .78)) / .50) ** 2)
            back = exp(-((y - 1.72 + .12 * cos(x * .55)) / .62) ** 2)
            z = -.403 + .043 * broad + .016 * ripple
            z += formation * (.152 + .033 * broad)
            z += front * (.171 + .061 * broad) + back * (.199 + .052 * ripple)
            z += .17 * exp(-((y - 10 - sin(x * .12) * 2.6) / 3.5) ** 2)
            field.verts.append((x, y, z))
            if i and j:
                q = j * nx + i
                field.faces.append((q - nx - 1, q - nx, q, q - 1))
                field.indices.append(0)
    obj = field.finish()
    for face in obj.data.polygons:
        face.use_smooth = True


def _lantern(collection, metal, x, y, index, glass):
    base = .65
    _turned(metal, x, y, [(base, .145), (.71, .145), (.73, .118), (.83, .105),
                          (.86, .094), (1.03, .080), (1.08, .087), (1.12, .068),
                          (3.78, .047), (3.82, .075), (3.85, .057), (3.95, .052)])
    # Square lantern with a flared light box and a folded sheet-metal hood.
    phi = pi / 4
    _turned(metal, x, y, [(3.89, .071), (3.98, .152), (4.008, .148)], sides=4, angle=phi)
    _turned(glass, x, y, [(4.00, .132), (4.39, .214)], sides=4, angle=phi)
    for i in range(4):
        dx, dy = cos(phi + i * pi / 2), sin(phi + i * pi / 2)
        _tube(metal, (x + .137 * dx, y + .137 * dy, 3.99),
              (x + .221 * dx, y + .221 * dy, 4.395), .012, .013, sides=6)
    _turned(metal, x, y, [(4.385, .225), (4.407, .251), (4.439, .253),
                          (4.441, .225), (4.581, .085), (4.605, .082)], sides=4, angle=phi)
    _turned(metal, x, y, [(4.595, .056), (4.651, .055),
                          (4.679, .076), (4.731, .017), (4.77, .005)], sides=8)
    light = bpy.data.lights.new('Fellbridge | lantern ' + str(index), 'POINT')
    light.energy = 120
    light.color = (1.0, .62, .29)
    light.shadow_soft_size = .22
    obj = bpy.data.objects.new(light.name, light)
    collection.objects.link(obj)
    # The downward spill comes from just underneath the opaque lamp foot.
    obj.location = (x, y - .08, 3.86)


def _sign(collection, hardware, materials):
    name = 'FELLBRIDGE'
    x, y = -15.6, 3.12
    snow, timber, paint, cream = materials
    parts = Batch(collection, 'Fellbridge | station nameboard', [timber, paint, cream, snow])
    for dx in (-1.15, 1.15):
        parts.casting(x + dx, y, .108, .14, .65, 2.62, .008, 0)
        parts.casting(x + dx, y, .126, .155, .69, .91, .009, 1)
        _turned(hardware, x + dx, y, [(2.6, .071), (2.64, .066),
                                      (2.685, .038), (2.721, .003)], sides=8)
    parts.casting(x, y - .043, 2.56, .116, 1.94, 2.52, .018, 0)
    parts.casting(x, y - .115, 2.46, .037, 1.987, 2.473, .008, 1)
    for z in (2.027, 2.431):
        parts.casting(x, y - .139, 2.38, .012, z, z + .012, .003, 2)
    parts.casting(x, y - .039, 2.58, .147, 2.515, 2.548, .018, 3, .009)
    parts.finish()
    curve = bpy.data.curves.new('Fellbridge | raised type', 'FONT')
    for path in ('C:/Windows/Fonts/georgiab.ttf',
                 '/usr/share/fonts/truetype/dejavu/DejaVuSerif-Bold.ttf'):
        if Path(path).exists():
            curve.font = bpy.data.fonts.load(path, check_existing=True)
            break
    curve.body = name
    curve.align_x, curve.align_y = 'CENTER', 'CENTER'
    curve.size, curve.space_character, curve.extrude = .29, 1.13, .002
    text = bpy.data.objects.new('Fellbridge | station lettering', curve)
    collection.objects.link(text)
    text.location, text.rotation_euler = (x, y - .15, 2.244), (pi / 2, 0, 0)
    curve.materials.append(cream)


def _winter_trees(collection, bark):
    # Open winter crowns let the background retain its pale atmospheric depth.
    trees = Batch(collection, 'Fellbridge | bare hawthorns and birches', [bark])
    rng = Random(1933)
    def branch(start, direction, length, radius, depth):
        tip = start + direction.normalized() * length
        _tube(trees, start, tip, radius, radius * .68, sides=5)
        if depth == 0:
            return
        lean = Vector((rng.uniform(-.37, .37), rng.uniform(-.35, .35), .17))
        branch(tip, (direction + lean).normalized(), length * rng.uniform(.60, .76),
               radius * .66, depth - 1)
        angle = rng.uniform(-pi, pi)
        side = Vector((cos(angle), sin(angle), rng.uniform(.16, .69)))
        attach = start.lerp(tip, rng.uniform(.56, .88))
        branch(attach, (direction * .31 + side).normalized(), length * rng.uniform(.55, .73),
               radius * .54, depth - 1)
    for i in range(40):
        x = -39 + i * 1.44 + rng.uniform(-.66, .66)
        y = rng.uniform(13.6, 20.0) + 1.6 * sin(x * .3)
        height = rng.uniform(3.0, 6.9)
        for _ in range(rng.choice((1, 1, 2, 3))):
            start = Vector((x + rng.uniform(-.24, .24), y, -.38))
            d = Vector((rng.uniform(-.21, .21), rng.uniform(-.18, .18), 1)).normalized()
            branch(start, d, height * .31, height * .018, 5)
    trees.finish()


def _mist(collection):
    material = bpy.data.materials.new('Fellbridge | distant ground mist')
    material.use_nodes = True
    nodes, links = material.node_tree.nodes, material.node_tree.links
    nodes.clear()
    out = nodes.new('ShaderNodeOutputMaterial')
    scatter = nodes.new('ShaderNodeVolumeScatter')
    scatter.inputs['Color'].default_value = (.72, .80, .88, 1)
    scatter.inputs['Anisotropy'].default_value = .18
    coords = nodes.new('ShaderNodeTexCoord')
    split = nodes.new('ShaderNodeSeparateXYZ')
    links.new(coords.outputs['Generated'], split.inputs['Vector'])
    fade = nodes.new('ShaderNodeValToRGB')
    for element, value, position in zip(fade.color_ramp.elements, (.034, 0), (.13, .94)):
        element.position = position
        element.color = (value, value, value, 1)
    links.new(split.outputs['Z'], fade.inputs[0])
    links.new(fade.outputs[0], scatter.inputs['Density'])
    links.new(scatter.outputs[0], out.inputs['Volume'])
    box = Batch(collection, 'Fellbridge | mist behind the platform', [material])
    box.casting(-6, 18.4, 98, 25, -.44, 5.8, .01)
    box.finish().display_type = 'WIRE'


def _fells(collection):
    # Two real ridgelines break the horizon behind the leafless hawthorns.
    # Distance haze lifts the far bank and softens its contrast at blue hour.
    for layer,(base,crest,tint) in enumerate(((29,6.7,(.19,.259,.342)),(46,11.0,(.23,.31,.40)))):
        mat = finish('Snow over the fells '+str(layer),tint,.99)
        p = next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
        p.inputs['Emission Color'].default_value = (*tint,1)
        p.inputs['Emission Strength'].default_value = .31 + .06*layer
        hill=Batch(collection,'Fellbridge | distant fell '+str(layer),[mat])
        width=151
        for j in range(14):
            t=j/13
            for i in range(width):
                x=(i-75)*1.16
                v=noise(Vector((x*.052,layer*2.15,.6)))
                low=noise(Vector((x*.13,t*1.6,3.76+layer)))
                peak=crest+2.15*sin(x*.103+layer*2.61)+1.25*v
                y=base+12*t+v*1.9
                z=-.45+(t*t*(3-2*t))*peak + low*.39*sin(t*pi)
                hill.verts.append((x,y,z))
                if i and j:
                    k=j*width+i
                    hill.faces.append((k-width-1,k-width,k,k-1))
                    hill.indices.append(0)
        obj=hill.finish()
        for f in obj.data.polygons:
            f.use_smooth=True


def _flurries(collection):
    rng = Random(2701)
    flake = finish('Snow in the air',(.69,.77,.83),.9)
    p = next(n for n in flake.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    p.inputs['Emission Color'].default_value = (.44,.56,.71,1)
    p.inputs['Emission Strength'].default_value = .53
    fall = Batch(collection,'Fellbridge | windborne powder',[flake])
    for _ in range(3400):
        x,y,z=rng.uniform(-32,32),rng.uniform(-18,17),rng.uniform(.18,13)
        width=rng.uniform(.0025,.0085)
        tail=rng.uniform(.008,.033)
        fall.mesh([(x-width,y,z),(x+tail*.29,y,z+tail*.45),
                   (x+tail*.38,y,z+tail),(x+width,y,z)],[(0,1,2,3)])
    fall.finish()


def build():
    scene = bpy.context.scene
    halt = bpy.data.collections.new('Studio | Fellbridge halt')
    scene.collection.children.link(halt)
    source = bpy.data.collections.get('Studio | Permanent way')
    if source:
        for obj in source.objects:
            if obj.name.startswith('Track'):
                span = 24.04 if 'rolled steel' in obj.name else 23.68
                for side in (-2, -1, 1, 2):
                    linked = bpy.data.objects.new(obj.name + ' | continuation', obj.data)
                    halt.objects.link(linked)
                    linked.location.x = span * side
    snow = finish('Fellbridge snow', (.67, .76, .82), .88,
                  variation=(.88, .91, .94), scale=(2.7, 8, 9), relief=.019)
    stone = finish('Fellbridge flags', (.23, .25, .25), .86,
                   variation=(.37, .37, .34), scale=(3, 6, 6), relief=.023)
    coping = finish('Fellbridge coping', (.36, .39, .40), .82)
    iron = finish('Fellbridge cast iron', (.045, .057, .056), .46, .56)
    timber = finish('Fellbridge oak', (.16, .112, .073), .8)
    paint = finish('Fellbridge enamel', (.029, .071, .075), .43, .14)
    cream = finish('Fellbridge cream', (.93, .84, .66), .52)
    bark = finish('Fellbridge distant bark', (.22, .285, .32), .94)
    glow = finish('Fellbridge lamp glass', (1.0, .695, .37), .33)
    shader = next(n for n in glow.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    shader.inputs['Emission Color'].default_value = (1.0, .526, .16, 1)
    shader.inputs['Emission Strength'].default_value = 3.7
    _snowfield(halt, snow)
    stage = Batch(halt, 'Fellbridge | low stone platform', [stone, coping, snow])
    stage.casting(0, 5.02, 60, 5.54, -.47, .509, .13, 0)
    stage.casting(0, 5.11, 60, 5.31, .51, .651, .12, 2, .032)
    for i in range(-31, 32):
        x = i * .95
        stage.casting(x, 2.40, .939, .42, .466, .611, .019, 1, .008)
        stage.casting(x, 2.42, .941, .40, .609, .644, .028, 2, .012)
    stage.finish()
    metal = Batch(halt, 'Fellbridge | ironwork', [iron])
    glass = Batch(halt, 'Fellbridge | lamp panes', [glow])
    for index, x in enumerate((-13.85, -2.65, 9.85), 1):
        _lantern(halt, metal, x, 3.54, index, glass)
    _sign(halt, metal, (snow, timber, paint, cream))
    metal.finish()
    glass.finish()
    _winter_trees(halt, bark)
    _fells(halt)
    _flurries(halt)
    _mist(halt)
    return halt
