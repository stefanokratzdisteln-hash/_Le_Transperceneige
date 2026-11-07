"""Arched pressed roof, winter equipment and enclosed gangways of Car 07."""

from math import ceil, cos, pi, sin, sqrt


CROWN = (
    (0.0, 4.135), (0.34, 4.123), (0.72, 4.078), (1.02, 4.019),
    (1.26, 3.940), (1.43, 3.845), (1.53, 3.742), (1.567, 3.680),
)


def roof_z(y):
    y = abs(y)
    for (a, low), (b, high) in zip(CROWN, CROWN[1:]):
        if y <= b:
            return low + (high - low) * (y - a) / (b - a)
    return CROWN[-1][1]


def loft(g, group, material, sections):
    """Close a series of ascending X sections with CCW loops in the YZ plane."""
    count = len(sections[0][1])
    verts = [(x, y, z) for x, ring in sections for y, z in ring]
    faces = [list(reversed(range(count)))]
    for base in range(0, len(verts) - count, count):
        for i in range(count):
            a = base + i
            b = base + (i + 1) % count
            faces.append((a, b, b + count, a + count))
    faces.append(list(range(len(verts) - count, len(verts))))
    g.mesh(group, material, verts, faces)


def extrusion(g, group, mat, x1, x2, profiles):
    lo, hi = sorted((x1, x2))
    loft(g, group, mat, [(lo, profiles), (hi, profiles)])


def framed_opening(g, group, material, sections):
    """Skin continuous outer/inner loops around the open vestibule passage."""
    sections = sorted(sections, key=lambda s: s[0])
    n = len(sections[0][1])
    verts = []
    faces = []
    for x, outer, inner in sections:
        verts.extend((x, y, z) for y, z in outer + inner)
    span = 2 * n
    for row in range(len(sections)):
        row_start = row * span
        for i in range(n):
            p = row_start + i
            q = row_start + (i + 1) % n
            if row == 0:
                faces.append((q, p, p + n, q + n))
            if row == len(sections) - 1:
                faces.append((p, q, q + n, p + n))
            else:
                faces.append((p, q, q + span, p + span))
                faces.append((p + n + span, q + n + span, q + n, p + n))
    g.mesh(group, material, verts, faces)


def clipped(half, floor, top, cut, center=0):
    return [
        (center - half + cut, floor), (center + half - cut, floor),
        (center + half, floor + cut), (center + half, top - cut),
        (center + half - cut, top), (center - half + cut, top),
        (center - half, top - cut), (center - half, floor + cut),
    ]


def shell(g):
    group = "Roof_Structure"
    crest = list(reversed(CROWN[1:])) + [(-y, z) for y, z in CROWN]
    cross = [(-1.567, 3.641), (1.567, 3.641)] + crest
    extrusion(g, group, "dark", -9.018, 9.018, cross)
    for side in (-1, 1):
        g.box(group, "dark", (0, side * 1.558, 3.677), (18.04, 0.031, 0.07), 0.009)
        g.box(group, "steel", (0, side * 1.568, 3.709), (18.04, 0.034, 0.029), 0.007)
    for x in (-8.92, -7.50, -6.03, -4.54, -3.04, -1.54, -0.04, 1.46,
              2.96, 4.46, 5.96, 7.46, 8.92):
        # A low folded ridge of sheet, shaped to every segment of the crown.
        low = list(reversed(crest))
        high = [(y * 1.005, z + 0.027) for y, z in crest]
        extrusion(g, group, "steel", x - 0.014, x + 0.014, low + high)


def crust(g, lobes, furrows=(), phase=0.0, steps=(0.14, 0.105), surface=roof_z):
    """Contour one field of overlapping wind-packed tongues and scoured hollows.

    Each lobe gives its centre, half-length, half-width, lateral sweep and
    centre height. A clipped triangular grid preserves holes and forked tails.
    """
    stepx, stepy = steps
    margin = 1.8 * max(steps)
    xmin = min(x-rx for x,y,rx,ry,sweep,h in lobes) - margin
    xmax = max(x+rx for x,y,rx,ry,sweep,h in lobes) + margin
    ymin = min(y-ry-abs(sweep) for x,y,rx,ry,sweep,h in lobes) - margin
    ymax = max(y+ry+abs(sweep) for x,y,rx,ry,sweep,h in lobes) + margin
    nx, ny = ceil((xmax-xmin)/stepx)+1, ceil((ymax-ymin)/stepy)+1

    def sample(x, y):
        # Metre-space perturbations keep the broad depositional shapes coherent
        # while undercutting their exposed edge with finer, unequal scallops.
        ripple = (0.54*sin(5.5*x + 4.8*y + phase + .8*sin(6.4*y))
                  + .29*sin(13.0*x-5.9*y+2*phase) + .17*sin(22.3*x+10.4*y))
        level, depth = -100, 0
        for cx, cy, rx, ry, sweep, height in lobes:
            u = (x-cx)/rx
            v = (y-cy-sweep*u-.13*ry*sin(5*u+phase))/ry
            radius = sqrt(u*u + v*v)
            contour = (1-radius)*ry + ripple*min(.105, ry*.34)
            level = max(level, contour)
            # Leeward face is steeper, crosswind crest is off centre.
            shoulder = max(0, 1-((u+.13)**2+((v+.18)*.97)**2))
            depth = max(depth, height*shoulder**1.22)
        for cx,cy,rx,ry,sweep in furrows:
            u = (x-cx)/rx
            v = (y-cy-sweep*u)/ry
            level = min(level, (sqrt(u*u+v*v)-1)*ry + ripple*min(.025, ry*.25))
        if surface is roof_z:
            level = min(level, 8.965-abs(x), 1.48-abs(y))
        shoulder = max(0, min(1, level / max(.012, 0.77*stepy)))
        lift = (.012 + depth * (0.94+.06*cos(4.1*x+6.4*y))) * (.12 + .88*shoulder)
        return level, lift

    grid = []
    for j in range(ny):
        for i in range(nx):
            x, y = xmin+i*stepx, ymin+j*stepy
            level, lift = sample(x,y)
            grid.append((x,y,level if abs(level)>1e-9 else -1e-9,lift))
    verts, faces, cached = [], [], {}

    def vertex(a, b=None):
        key = (a,a) if b is None else tuple(sorted((a,b)))
        if key not in cached:
            x,y,value,lift = grid[a]
            if b is not None:
                bx,by,bvalue,_ = grid[b]
                t = value/(value-bvalue)
                x, y = x+(bx-x)*t, y+(by-y)*t
                lift = .002
            cached[key] = len(verts)
            verts.append((x,y,surface(y)+lift))
        return cached[key]

    def clip(triangle):
        poly = []
        for i,a in enumerate(triangle):
            b = triangle[(i+1)%3]
            in_a, in_b = grid[a][2] > 0, grid[b][2] > 0
            if in_a:
                poly.append(vertex(a))
            if in_a != in_b:
                poly.append(vertex(a,b))
        faces.extend((poly[0],poly[j],poly[j+1]) for j in range(1,len(poly)-1))

    for j in range(ny-1):
        for i in range(nx-1):
            a = j*nx+i
            b,c,d = a+1, a+nx+1, a+nx
            for triangle in (((a,b,c),(a,c,d)) if (i+j)%2 else ((a,b,d),(b,c,d))):
                clip(triangle)
    boundary = set()
    for face in faces:
        for a,b in zip(face,(face[1],face[2],face[0])):
            if (b,a) in boundary:
                boundary.remove((b,a))
            else:
                boundary.add((a,b))
    count = len(verts)
    underside = [(x,y,surface(y)-.004) for x,y,z in verts]
    back = [(a+count,c+count,b+count) for a,b,c in faces]
    walls = [(b,a,a+count,b+count) for a,b in sorted(boundary)]
    g.mesh("Roof_Snow", "snow", verts+underside, faces+back+walls)


def winter_roof(g):
    # Short, thick shoulder bank at the closed end, with two stripped tails.
    crust(g, [
        (-8.21,-.23,.69,.56,.04,.136), (-7.64,-.43,.87,.50,-.13,.153),
        (-7.06,-.77,.89,.22,-.19,.091), (-6.93,-.42,.96,.17,-.13,.066),
        (-7.72,.21,.70,.24,.06,.080), (-8.18,-.89,.42,.15,-.04,.058),
        (-7.43,-1.11,.54,.095,-.14,.045),
    ], [(-6.91,-.63,.75,.069,-.16),(-7.50,.12,.37,.081,.06)], .61)

    # Snow combed off the lantern sill into a broken ridge under its windows.
    crust(g, [
        (-5.53,-.69,.39,.10,-.03,.091),(-4.63,-.75,.73,.12,-.02,.095),
        (-3.65,-.77,.55,.145,-.042,.104),(-2.87,-.74,.75,.18,.025,.128),
        (-2.31,-.84,.63,.22,-.057,.147),(-1.69,-.92,.67,.14,-.070,.105),
        (-1.07,-1.12,.69,.118,-.12,.072),(-.42,-1.25,.35,.056,-.038,.031),
        (-1.08,-.73,.66,.099,.018,.079),(-.33,-.68,.53,.07,.032,.050),
        (-2.29,-1.17,.42,.076,-.074,.043),
    ], [(-1.26,-.91,.51,.061,-.085)], 2.45,steps=(.115,.072))

    # Little sheltered pockets intermittently catch on the far sill.
    crust(g, [(1.22,.67,.42,.11,.018,.064),(1.69,.72,.37,.066,.034,.039),
              (3.22,.66,.34,.109,.018,.055)], phase=1.8,steps=(.087,.068))

    # Short heaped drift off the rounded lantern end, another on the standing seam,
    # and dispersed diagonal leading edges descending the visible roof shoulder.
    crust(g, [
        (5.42,.11,.43,.28,-.05,.122),(5.94,-.15,.72,.31,-.23,.147),
        (6.21,-.45,.80,.30,-.20,.117),(6.96,-.60,.81,.17,-.10,.083),
        (7.49,-.66,.59,.086,-.05,.044),(6.38,.18,.51,.13,-.07,.076),
        (6.32,-.92,.43,.103,-.13,.049),
        (7.68,.27,.52,.35,-.06,.130),(8.09,.14,.55,.22,-.08,.096),
        (8.49,-.08,.38,.095,-.095,.055),(7.91,-.23,.46,.091,-.06,.058),
    ], [(6.47,-.26,.47,.082,-.09),(7.76,.48,.28,.067,-.03)], 4.22)

    # Wind-stripped veil across the far half of the lantern roof. Height is
    # measured above the cambered clerestory cap for a positive seat everywhere.
    crust(g, [(-3.89,.15,.66,.19,.08,.064),(-3.32,.21,.59,.12,-.035,.052),
              (-2.74,.13,.38,.066,-.06,.029)], phase=1.1,
          steps=(.115,.072),surface=lantern_z)
    crust(g, [(3.16,.10,.46,.15,-.02,.061),(3.68,.05,.39,.074,-.055,.034)],
          phase=3.7,steps=(.095,.065),surface=lantern_z)


LANTERN_ARCH = ((0.,4.553),(.22,4.544),(.43,4.515),(.57,4.473),(.64,4.433))


def lantern_z(y):
    y = abs(y)
    for (a,low),(b,high) in zip(LANTERN_ARCH,LANTERN_ARCH[1:]):
        if y <= b:
            return low+(high-low)*(y-a)/(b-a)
    return LANTERN_ARCH[-1][1]


def clerestory(g):
    group = "Roof_Clerestory"
    # A rolled lead crown and continuously curved turtleback ends. Their
    # lateral taper leaves room for the ventilators on each open vestibule bay.
    ends = [(-6.04,.50,.15),(-5.97,.66,.31),(-5.83,.85,.65),(-5.65,.97,.91),
            (-5.49,1.,1.),(5.49,1.,1.),(5.65,.97,.91),(5.83,.85,.65),
            (5.97,.66,.31),(6.04,.50,.15)]
    arch = list(reversed(LANTERN_ARCH[1:])) + [(-y,z) for y,z in LANTERN_ARCH]
    side_profile = [(-.603,4.054),(.603,4.054),(.603,4.411),(-.603,4.411)]
    top_profile = [(-.64,4.399),(.64,4.399)] + arch
    def sections(profile):
        return [(x,[(y*width,4.105+(z-4.105)*height) for y,z in profile])
                for x,width,height in ends]
    loft(g,group,"wood",sections(side_profile))
    loft(g,group,"dark",sections(top_profile))
    for side in (-1,1):
        # Faceted varnished sills, lower gilt fillet and broad cornice shadow.
        g.box(group,"petrol",(0.,side*.606,4.128),(10.96,.035,.067),.011)
        g.box(group,"gilt",(0.,side*.624,4.153),(10.97,.014,.016),.004)
        g.box(group,"steel",(0.,side*.638,4.415),(10.98,.025,.028),.006)
        for bay in range(15):
            x = (bay-7)*.717
            g.box(group,"gilt",(x,side*.605,4.281),(.635,.018,.223),.008)
            g.box(group,"ink",(x,side*.616,4.281),(.609,.016,.196),.007)
            if bay in (1,7,13):
                for leaf in (-.049,.012,.070):
                    g.box(group,"brass",(x,side*.628,4.275+leaf),(.554,.017,.021),.004)
            else:
                # Paired leaded lights, softly amber in the occupied bays.
                for hand in (-1,1):
                    paint = "warm" if bay in (3,9,10) else "glass"
                    g.box(group,paint,(x+hand*.143,side*.626,4.282),(.264,.008,.151),.003)
                g.box(group,"brass",(x,side*.631,4.281),(.022,.013,.172),.003)
    # Folded transverse joints interrupt the lead, sitting above the glazing.
    for x in (-4.99,-3.55,-2.13,-.70,.70,2.13,3.55,4.99):
        lower = list(reversed(arch))
        upper = [(y*1.007,z+.014) for y,z in arch]
        extrusion(g,group,"steel",x-.011,x+.011,lower+upper)


def roof_fittings(g):
    group = "Roof_Fittings"
    # A pair of low torpedo extractors. The longitudinal cowl has vent mouths
    # at both ends and a flanged throat seated on the rolled roof.
    for x, y in ((-6.83,.075),(6.81,.055)):
        base = roof_z(y)-.027
        g.cyl(group,"steel",(x,y,base-.008),(x,y,base+.052),.18,20)
        g.cyl(group,"dark",(x,y,base+.043),(x,y,base+.159),.110,20)
        axis_z = base+.213
        def ring(radius):
            return [(y+radius*cos(2*pi*i/16),axis_z+radius*sin(2*pi*i/16)) for i in range(16)]
        loft(g,group,"steel",[(x+offset,ring(rad)) for offset,rad in
             ((-.28,.066),(-.22,.092),(-.13,.111),(0,.121),(.13,.111),(.22,.092),(.28,.066))])
        for direction in (-1,1):
            end_x = x+direction*.28
            g.cyl(group,"brass",(end_x,y,axis_z),(end_x+direction*.012,y,axis_z),.067,16)
            g.cyl(group,"ink",(end_x+direction*.012,y,axis_z),
                  (end_x+direction*.015,y,axis_z),.050,16)
    # The thin steam fitting follows the far roof shoulder.
    y=.88
    z=roof_z(y)+.077
    points=[(-5.65,.69,roof_z(.69)+.026),(-5.37,y,z),(-.40,y,z),(-.10,.73,roof_z(.73)+.021)]
    for a,b in zip(points,points[1:]):
        g.cyl(group,"dark",a,b,.032,12)
    for x in (-5.11,-3.58,-2.10,-.59):
        g.box(group,"steel",(x,y,z-.035),(.060,.108,.104),.015)
        g.cyl(group,"brass",(x-.024,y,z),(x+.024,y,z),.041,12)


def open_end_door(g, sign):
    """Panelled door at ninety degrees, hinged inward along the vestibule wall."""
    group = 'EndDoor_A' if sign < 0 else 'EndDoor_B'
    pivot_x, pivot_y = sign * 8.91, .68
    def x(u):
        return pivot_x + sign * (u - .575)

    def plate(mat, t1, t2, profile):
        # The panel's drawn coordinates are along its broad face and upward.
        points = [(x(u), z) for u,z in profile]
        if sign < 0:
            points.reverse()
        n = len(points)
        verts = [(xx, pivot_y+t, z) for t in sorted((t1,t2)) for xx,z in points]
        faces = [tuple(range(n)), tuple(reversed(range(n, 2*n)))]
        for i in range(n):
            j = (i+1) % n
            faces.append((i,i+n,j+n,j))
        g.mesh(group,mat,verts,faces)

    plate('wood',-.027,.027,clipped(.575,1.407,3.464,.046))
    for face in (-1,1):
        def mould(mat,t1,t2,profile):
            plate(mat,face*t1,face*t2,profile)
        def oval(ry,rz):
            return [(ry*cos(i*2*pi/36),2.875+rz*sin(i*2*pi/36)) for i in range(36)]
        mould('petrol',.027,.036,oval(.326,.362))
        mould('gilt',.036,.044,oval(.306,.344))
        mould('brass',.044,.047,oval(.286,.324))
        mould('glass',.047,.049,oval(.274,.313))
        for hand in (-1,1):
            u = hand*.253
            mould('brass',.027,.031,clipped(.223,1.575,2.230,.034,u))
            mould('petrol',.031,.038,clipped(.214,1.586,2.218,.031,u))
            mould('wood',.038,.045,clipped(.178,1.628,2.177,.027,u))
        mould('brass',.027,.030,clipped(.529,1.431,1.521,.017))
        mould('brass',.027,.032,clipped(.186,2.262,2.455,.031))
        mould('ink',.032,.036,clipped(.171,2.274,2.443,.024))
        rotation = (pi/2,0,0 if face < 0 else pi)
        g.text(group,'gilt','07',(x(0),pivot_y+face*.039,2.302),rotation,.145)
        y = pivot_y+face*.030
        cx = x(-.459)
        g.cyl(group,'brass',(cx,y,2.461),(cx,y+face*.012,2.461),.048,16)
        g.cyl(group,'gilt',(cx,y+face*.012,2.461),(cx,y+face*.060,2.461),.019,10)
        g.cyl(group,'brass',(cx,y+face*.060,2.461),
              (x(-.302),y+face*.060,2.451),.018,10)
    for z in (1.70,2.35,3.18):
        g.cyl(group,'gilt',(pivot_x,pivot_y,z-.062),(pivot_x,pivot_y,z+.062),.026,12)
        g.box(group,'brass',(x(.534),pivot_y-.027,z),(.087,.016,.106),.006)


def end(g, sign):
    group = "Gangway_A" if sign < 0 else "Gangway_B"

    def box(mat, dist, y, z, dims, bevel=0):
        g.box(group, mat, (sign*dist, y, z), dims, bevel)

    def panel(mat, x1, x2, points):
        extrusion(g, group, mat, sign*x1, sign*x2, points)

    def disc(mat, x1, x2, y, z, radius, sides=16):
        g.cyl(group, mat, (sign*x1,y,z), (sign*x2,y,z), radius, sides)

    # The wing bulkheads, transom and lower toe enclose a physical opening.
    # No cap spans the doorway at eye or shoulder height.
    for hand in (-1,1):
        wing = [(hand*y,z) for y,z in ((.684,1.28),(1.38,1.28),(1.54,1.43),
                                        (1.54,3.67),(.684,3.67))]
        if hand < 0:
            wing.reverse()
        panel('petrol',8.965,9.019,wing)
    box('wood',8.992,0,3.598,(.054,1.368,.144),.004)
    box('dark',8.992,0,1.322,(.054,1.368,.084),.004)
    for hand in (-1, 1):
        y = hand * 1.205
        panel("brass",9.020,9.031,clipped(.235,1.446,3.522,.078,y))
        panel("wood",9.031,9.039,clipped(.221,1.46,3.509,.069,y))
        # Winged lozenge in the lower end panel, hand-lined to match the sides.
        for wing in (-1,1):
            yy = y+wing*.100
            box("gilt",9.043,yy,1.723,(.010,.126,.018),.004)
            box("gilt",9.043,yy-wing*.012,1.694,(.010,.085,.012),.003)
        panel("gilt",9.039,9.050,[(y-.058,1.72),(y,1.666),(y+.058,1.72),(y,1.774)])
        panel("petrol",9.050,9.055,[(y-.032,1.72),(y,1.689),(y+.032,1.72),(y,1.751)])
        # Circular ruby lens within a stepped polished brass escutcheon.
        disc("dark",9.038,9.074,y,3.205,.117,24)
        disc("gilt",9.072,9.091,y,3.205,.098,24)
        disc("brass",9.091,9.107,y,3.205,.080,24)
        disc("red",9.105,9.126,y,3.205,.061,24)
    inner = clipped(.651,1.368,3.515,.059)
    for mat,x1,x2,outer in (
        ('wood',8.912,8.967,clipped(.727,1.303,3.593,.089)),
        ('gilt',8.904,8.913,clipped(.705,1.320,3.562,.081)),
        ('wood',8.965,9.027,clipped(.718,1.303,3.589,.085)),
        ('brass',9.023,9.043,clipped(.699,1.315,3.561,.076)),
    ):
        framed_opening(g,group,mat,[(sign*x1,outer,inner),(sign*x2,outer,inner)])
    for hand in (-1,1):
        # Inner vestibule panelling remains visible to a passenger at either end.
        cy = hand*1.115
        for base,top in ((1.54,2.22),(2.39,3.36)):
            panel('brass',8.951,8.965,clipped(.349,base,top,.036,cy))
            panel('wood',8.942,8.952,clipped(.332,base+.017,top-.017,.032,cy))
        box('brass',8.938,cy,2.30,(.022,.748,.027),.005)
    open_end_door(g,sign)

    # Moulded gangway flanges form a hollow tunnel. The clear bore is constant.
    bellows = []
    for i in range(5):
        a = 9.033 + i*0.114
        for distance, w, floor, top in (
            (a,0.735,1.306,3.538),
            (a+0.051,0.872,1.259,3.687),
            (a+0.070,0.872,1.259,3.687),
        ):
            bellows.append((sign*distance,clipped(w,floor,top,0.208),inner))
    bellows.append((sign*9.603,clipped(0.738,1.306,3.538,0.208),inner))
    framed_opening(g, group, "rubber", bellows)
    framed_opening(g, group, "steel", [
        (sign*9.603,clipped(0.811,1.278,3.624,0.201),inner),
        (sign*9.650,clipped(0.811,1.278,3.624,0.201),inner),
    ])
    framed_opening(g, group, "dark", [
        (sign*9.648,clipped(0.775,1.291,3.579,0.184),inner),
        (sign*9.686,clipped(0.775,1.291,3.579,0.184),inner),
    ])
    # Sliding bridge plate extends to the coupling plane, so adjoining cars
    # placed at a 19.66 m pitch meet at the same running floor.
    box("steel",9.340,0,1.371,(.980,1.260,.038),.005)
    for x in (9.12,9.25,9.38,9.51,9.64,9.77):
        box("dark",x,0,1.391,(.012,1.153,.003),.001)
    # A slender vestibule handrail and the flexible steam connexion.
    for z in (2.055,2.813):
        disc("brass",9.031,9.066,-1.205,z,0.056,12)
        disc("steel",9.060,9.164,-1.205,z,0.025,12)
    g.cyl(group,"brass",(sign*9.158,-1.205,2.041),(sign*9.158,-1.205,2.824),0.025,12)
    for y in (1.11,1.32):
        disc("steel",9.034,9.130,y,2.598,0.058,12)
    hose = [(9.133,1.11,2.600),(9.182,1.103,2.461),(9.18,1.065,2.213),
            (9.16,1.087,2.033),(9.16,1.17,1.974),(9.16,1.263,2.024),
            (9.151,1.328,2.343),(9.133,1.32,2.600)]
    for a, b in zip(hose, hose[1:]):
        g.cyl(group,"rubber",(sign*a[0],a[1],a[2]),(sign*b[0],b[1],b[2]),0.025,10)
    for y in (1.11,1.32):
        disc("brass",9.122,9.147,y,2.598,0.040,12)


def build(g):
    shell(g)
    winter_roof(g)
    clerestory(g)
    roof_fittings(g)
    end(g, -1)
    end(g, 1)
