"""Mesh batching for the Car 07 master. All dimensions are in metres."""
import math
from collections import defaultdict
from functools import lru_cache
from pathlib import Path

import bmesh
import bpy
from mathutils import Euler, Vector

from finishes import FINISHES, COLS, ROWS


@lru_cache(maxsize=256)
def box_geometry(size, bevel):
    mesh = bmesh.new()
    bmesh.ops.create_cube(mesh, size=1)
    for v in mesh.verts:
        for axis in range(3):
            v.co[axis] *= size[axis]
    if bevel > 0:
        bmesh.ops.bevel(mesh, geom=list(mesh.edges), offset=min(bevel, min(size) * .46),
                        segments=1, affect='EDGES', profile=.5)
    mesh.verts.index_update()
    vertices = [tuple(v.co) for v in mesh.verts]
    faces = [tuple(v.index for v in f.verts) for f in mesh.faces]
    mesh.free()
    return vertices, faces


class Geometry:
    def __init__(self):
        self.batches = defaultdict(lambda: ([], [], [], []))
        self.font = bpy.data.fonts.get('Bfont Regular')
        for path in ('C:/Windows/Fonts/GARABD.TTF', '/System/Library/Fonts/Supplemental/Georgia Bold.ttf', '/usr/share/fonts/truetype/dejavu/DejaVuSerif-Bold.ttf'):
            if Path(path).exists():
                self.font = bpy.data.fonts.load(path)
                break

    def mesh(self, group, mat, verts, faces, smooth=False):
        vertices, polygons, paints, smooths = self.batches[group]
        offset = len(vertices)
        vertices.extend(tuple(v) for v in verts)
        for face in faces:
            polygons.append(tuple(i + offset for i in face))
            paints.append(mat)
            smooths.append(smooth and len(face) == 4)

    def box(self, group, mat, center, size, bevel=0):
        vertices, faces = box_geometry(tuple(size), bevel)
        self.mesh(group, mat, [tuple(a + b for a, b in zip(v, center)) for v in vertices], faces)

    def cyl(self, group, mat, a, b, r, n=16):
        a, b = Vector(a), Vector(b)
        unit = (b - a).normalized()
        u = unit.orthogonal().normalized()
        v = unit.cross(u)
        ring = [r * (u * math.cos(i * math.tau / n) + v * math.sin(i * math.tau / n)) for i in range(n)]
        verts = [base + p for base in (a, b) for p in ring]
        walls = [(i, (i+1) % n, n+(i+1) % n, n+i) for i in range(n)]
        self.mesh(group, mat, verts, walls + [tuple(reversed(range(n))), tuple(range(n, 2*n))], n >= 12)

    def beam(self, group, mat, a, b, width, depth=None):
        a, b = Vector(a), Vector(b)
        unit = (b-a).normalized()
        side = unit.cross(Vector((0, 0, 1)))
        if side.length < .001:
            side = Vector((1, 0, 0))
        side.normalize()
        up = unit.cross(side)
        ring = [side*s*width*.5 + up*t*(depth or width)*.5 for s,t in ((-1,-1),(1,-1),(1,1),(-1,1))]
        self.mesh(group, mat, [p + v for p in (a,b) for v in ring],
                  [(3,2,1,0),(4,5,6,7)] + [(i,(i+1)%4,4+(i+1)%4,4+i) for i in range(4)])

    def text(self, group, mat, body, location, rotation, size, align='CENTER'):
        curve = bpy.data.curves.new('Foundry lettering', 'FONT')
        curve.body, curve.size, curve.align_x = body, size, align
        if self.font:
            curve.font = self.font
        curve.resolution_u = 4
        obj = bpy.data.objects.new('Type template', curve)
        bpy.context.collection.objects.link(obj)
        evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
        letters = evaluated.to_mesh()
        pose, origin = Euler(rotation).to_matrix(), Vector(location)
        self.mesh(group, mat, [pose @ v.co + origin for v in letters.vertices],
                  [tuple(p.vertices) for p in letters.polygons])
        evaluated.to_mesh_clear()
        bpy.data.objects.remove(obj, do_unlink=True)
        bpy.data.curves.remove(curve)

    def finish(self, material, collection, palette=FINISHES):
        tile = {key: i for i, key in enumerate(palette)}
        objects = []
        for name, (verts, faces, paints, smooths) in self.batches.items():
            mesh = bpy.data.meshes.new(name)
            mesh.from_pydata(verts, [], faces)
            mesh.update()
            uv = mesh.uv_layers.new(name='FinishAtlas')
            for p, paint, smooth in zip(mesh.polygons, paints, smooths):
                p.use_smooth = smooth
                # Box projection in metric space, constrained to each finish's padded tile.
                dominant = max(range(3), key=lambda k: abs(p.normal[k]))
                axes = ((1,2),(0,2),(0,1))[dominant]
                coords = [[verts[i][a] for a in axes] for i in p.vertices]
                bounds = [(min(c[a] for c in coords), max(c[a] for c in coords)) for a in (0,1)]
                index = tile[paint]
                for li, co in zip(p.loop_indices, coords):
                    mapped = []
                    for axis,(lo,hi) in enumerate(bounds):
                        scale = min(.26, .95/max(.001,hi-lo))
                        start = ((lo * .26 + .17) % 1) * (1 - (hi-lo)*scale)
                        value = start+(co[axis]-lo)*scale
                        if paint in ('glass','room') and dominant != 2 and hi-lo > .1:
                            value = (co[axis]-lo)/(hi-lo)
                        if paint in ('carpet','parquet') and dominant == 2 and hi-lo > .1:
                            value = (co[axis]-lo)/(hi-lo)
                        mapped.append(.017 + .966*value)
                    uv.data[li].uv = ((index%COLS + mapped[0])/COLS, (index//COLS + mapped[1])/ROWS)
            mesh.validate(verbose=True)
            mesh.materials.append(material)
            obj = bpy.data.objects.new(name, mesh)
            collection.objects.link(obj)
            objects.append(obj)
        return objects
