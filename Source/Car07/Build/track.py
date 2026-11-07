"""The separate photographic track section used in the Car 07 studio renders."""

from math import cos, pi, sin
from random import Random

import bpy


def finish(name, color, roughness, metal=0, variation=None, scale=(1, 1, 1), relief=0):
    mat = bpy.data.materials.new("Studio | " + name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    shader = next(node for node in nodes if node.type == "BSDF_PRINCIPLED")
    shader.inputs["Base Color"].default_value = (*color, 1)
    shader.inputs["Roughness"].default_value = roughness
    shader.inputs["Metallic"].default_value = metal
    if variation is None:
        return mat
    texcoord = nodes.new("ShaderNodeTexCoord")
    stretch = nodes.new("ShaderNodeVectorMath")
    stretch.operation = "MULTIPLY"
    stretch.inputs[1].default_value = scale
    links.new(texcoord.outputs["Object"], stretch.inputs[0])
    noise = nodes.new("ShaderNodeTexNoise")
    noise.inputs["Scale"].default_value = 1
    noise.inputs["Detail"].default_value = 2.7
    links.new(stretch.outputs[0], noise.inputs["Vector"])
    ramp = nodes.new("ShaderNodeValToRGB")
    for stop, value, position in zip(ramp.color_ramp.elements, (color, variation), (0.22, 0.78)):
        stop.position = position
        stop.color = (*value, 1)
    links.new(noise.outputs["Fac"], ramp.inputs[0])
    links.new(ramp.outputs["Color"], shader.inputs["Base Color"])
    if relief:
        bump = nodes.new("ShaderNodeBump")
        bump.inputs["Strength"].default_value = 0.19
        bump.inputs["Distance"].default_value = relief
        links.new(noise.outputs["Fac"], bump.inputs["Height"])
        links.new(bump.outputs["Normal"], shader.inputs["Normal"])
    return mat


class Batch:
    def __init__(self, collection, name, materials):
        self.collection, self.name, self.materials = collection, name, materials
        self.verts, self.faces, self.indices = [], [], []

    def mesh(self, verts, faces, material=0):
        offset = len(self.verts)
        self.verts.extend(verts)
        for face in faces:
            self.faces.append(tuple(offset + i for i in face))
            self.indices.append(material)

    def stack(self, rings, material=0):
        n = len(rings[0])
        verts = [v for ring in rings for v in ring]
        faces = [tuple(reversed(range(n))), tuple(range(len(verts)-n, len(verts)))]
        for i in range(len(verts) - n):
            j = i - i % n + (i + 1) % n
            faces.append((i, j, j + n, i + n))
        self.mesh(verts, faces, material)

    def casting(self, x, y, width_x, width_y, bottom, top, cut, mat=0, lip=0):
        def ring(z, shrink):
            a, b = width_x * 0.5 - shrink, width_y * 0.5 - shrink
            c = min(cut, a * .8, b * .8)
            corners = ((-a+c,-b), (a-c,-b), (a,-b+c), (a,b-c),
                       (a-c,b), (-a+c,b), (-a,b-c), (-a,-b+c))
            return [(x+dx, y+dy, z) for dx, dy in corners]
        rings = [ring(bottom,0)]
        if lip:
            rings.append(ring(top-lip,0))
        rings.append(ring(top,lip))
        self.stack(rings, mat)

    def finish(self):
        mesh = bpy.data.meshes.new(self.name)
        mesh.from_pydata(self.verts, [], self.faces)
        mesh.update()
        for material in self.materials:
            mesh.materials.append(material)
        mesh.polygons.foreach_set("material_index", self.indices)
        obj = bpy.data.objects.new(self.name, mesh)
        self.collection.objects.link(obj)
        return obj


def rails(batch):
    # The rail head is 69 mm across. Inside faces are 1.435 m apart.
    profile = [
        (-.074,-.164), (.074,-.164), (.074,-.153), (.017,-.140), (.009,-.130),
        (.009,-.060), (.014,-.050), (.032,-.041), (.0345,-.032), (.0345,-.006),
        (.028,0), (-.028,0), (-.0345,-.006), (-.0345,-.032), (-.032,-.041),
        (-.014,-.050), (-.009,-.060), (-.009,-.130), (-.017,-.140), (-.074,-.153),
    ]
    n = len(profile)
    for sign in (-1, 1):
        center = sign * .752
        # Staggered joints keep both rail seams from falling on the same tie.
        joint = sign * 10.18
        for a,b in ((-12.02,joint-.004),(joint+.004,12.02)):
            verts = [(x, center+y, z) for x in (a,b) for y,z in profile]
            batch.mesh(verts, [tuple(reversed(range(n))),tuple(range(n,2*n))])
            for i in range(n):
                j = (i+1)%n
                batch.mesh([verts[i],verts[j],verts[n+j],verts[n+i]], [(0,1,2,3)],
                           1 if 9 <= i <= 11 else 0)
        for flank in (-1, 1):
            y = center + flank*.021
            batch.casting(joint,y,.55,.024,-.126,-.065,.007)
            for step in (-.205,-.075,.075,.205):
                head = [(joint+step+.018*cos(i*pi/3), y+flank*.017,
                         -.093+.018*sin(i*pi/3)) for i in range(6)]
                if flank > 0:
                    head.reverse()
                batch.mesh(head, [tuple(range(6))], 1)


def sleepers_and_chairs(ties, fittings, powder, rng):
    for index in range(-18,19):
        x = .64*index
        y = rng.uniform(-.032,.032)
        ties.casting(x, y, .244, 2.53, -.302, -.188, .042, index%3, lip=.013)
        for sign in (-1,1):
            rail_y = sign*.752
            fittings.casting(x,rail_y,.190,.291,-.188,-.165,.020,0,lip=.004)
            for hand in (-1,1):
                bolt_y = rail_y+hand*.111
                bolt_x = x+hand*.046
                fittings.casting(bolt_x,bolt_y-hand*.015,.066,.075,-.166,-.143,.011,0,lip=.004)
                for radius,z1,z2,mat in ((.025,-.144,-.138,0),(.019,-.138,-.122,1)):
                    rings = [[(bolt_x+radius*cos(i*pi/3),bolt_y+radius*sin(i*pi/3),z)
                              for i in range(6)] for z in (z1,z2)]
                    fittings.stack(rings,mat)
            if rng.random() < .46:
                # Remnant of compacted powder sheltered beyond the chair.
                mid = y+sign*rng.uniform(1.087,1.15)
                rx,ry = rng.uniform(.052,.099),rng.uniform(.060,.101)
                outline = []
                for i in range(9):
                    t = i*2*pi/9
                    radius = rng.uniform(.72,1)
                    outline.append((x+rx*cos(t)*radius,mid+ry*sin(t)*radius,-.186))
                powder.mesh(outline+[(x,mid,-.169)],[(i,(i+1)%9,9) for i in range(9)])


def rounded_base(half_x, half_y, radius, z):
    pts = []
    for sx, sy, angle in ((1,1,0),(-1,1,pi/2),(-1,-1,pi),(1,-1,3*pi/2)):
        for i in range(6):
            theta = angle+pi*i/10
            pts.append((sx*(half_x-radius)+radius*cos(theta),
                        sy*(half_y-radius)+radius*sin(theta),z))
    return pts


def stone_chips(ballast,rng):
    # Crushed granite along the shoulders and at intermittent exposed tie ends.
    for _ in range(1750):
        x = rng.uniform(-11.99,11.99)
        y = rng.choice((-1,1))*rng.uniform(.99,1.68)
        if abs(y) < 1.27 and abs(x-round(x/.64)*.64) < .142:
            continue
        if abs(y) < 1.36:
            surface = -.267
        else:
            surface = -.267 - (abs(y)-1.36)*.53
        rx = rng.uniform(.022,.062)
        ry = rx*rng.uniform(.50,.91)
        theta = rng.uniform(-pi,pi)
        outline=[]
        for i in range(5):
            t=i*2*pi/5
            r=rng.uniform(.72,1.14)
            a,b=rx*cos(t)*r,ry*sin(t)*r
            outline.append((x+a*cos(theta)-b*sin(theta),y+a*sin(theta)+b*cos(theta),surface-.005))
        verts=outline+[(x+rx*.24,y+ry*.16,surface+rx*rng.uniform(.23,.56))]
        ballast.mesh(verts,[(i,(i+1)%5,5) for i in range(5)],rng.randint(1,3))


def build():
    collection = bpy.data.collections.new("Studio | Permanent way")
    bpy.context.scene.collection.children.link(collection)
    rng = Random(701435)
    timber = []
    for label,factor in (("Graphite",1.0),("Creosote",.84),("Silvered",1.17)):
        timber.append(finish(label, tuple(factor*c for c in (.068,.074,.073)), .84,
                             variation=tuple(factor*c for c in (.156,.166,.156)),
                             scale=(90,1.8,26),relief=.020))
    iron = finish("Oxide on the rail web",(.082,.083,.079),.65,.55,
                  variation=(.149,.136,.115), scale=(2,23,20),relief=.004)
    polish = finish("Running surface",(.42,.45,.46),.28,.82)
    chair = finish("Cast chairs",(.076,.085,.086),.62,.63)
    snow = finish("Powder",(.70,.77,.81),.86,variation=(.77,.84,.87),
                  scale=(72,72,72),relief=.009)
    ground = finish("Cyclorama",(.38,.434,.477),.86)
    roadbed = finish("Cold granite",(.145,.181,.190),.93,
                     variation=(.41,.46,.47),scale=(23,23,23),relief=.043)
    aggregate = [finish("Granite "+str(i),color,.89) for i,color in enumerate(
        ((.21,.25,.26),(.31,.35,.36),(.47,.51,.51)),1)]

    stage=Batch(collection,"Stage • cool grey floor",[ground])
    stage.mesh([(-160,-160,-.445),(160,-160,-.445),(160,160,-.445),(-160,160,-.445)],[(0,1,2,3)])
    stage.finish()
    bed=Batch(collection,"Ballast • dressed shoulders",[roadbed,*aggregate])
    bed.stack([rounded_base(12.41,1.70,.51,-.45),rounded_base(12.40,1.69,.51,-.433),
               rounded_base(12.11,1.38,.39,-.272),rounded_base(12.09,1.36,.38,-.265)])
    stone_chips(bed,rng)
    bed.finish()
    rails_batch=Batch(collection,"Track • rolled steel",[iron,polish])
    rails(rails_batch)
    rails_batch.finish()
    ties=Batch(collection,"Track • sleepers",timber)
    fittings=Batch(collection,"Track • chairs and clips",[chair,iron])
    powder=Batch(collection,"Track • sheltered snow",[snow])
    sleepers_and_chairs(ties,fittings,powder,rng)
    for batch in (ties,fittings,powder):
        batch.finish()
    return collection
