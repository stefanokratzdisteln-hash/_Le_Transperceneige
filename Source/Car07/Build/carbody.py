"""Exterior bodywork for the polar sleeping car. Coordinates are in metres."""

from math import cos, pi, sin


WINDOW_DESK_BAYS = (1, 3)
WINDOW_DESK_SIZE = (.728, .389, .060)


def window_station(wing, i):
    return wing * (2.37 + i * 1.38)


def window_desk_center(station):
    return (station, 1.245, 2.560)


def _rounded(x, z, width, height, radius):
    """Counterclockwise outline in the XZ plane, beginning at the right edge."""
    points = []
    for dx, dz, angle in ((1, 1, 0), (-1, 1, 90), (-1, -1, 180), (1, -1, 270)):
        corner_x = x + dx * (width * 0.5 - radius)
        corner_z = z + dz * (height * 0.5 - radius)
        for step in (0, 30, 60, 90):
            theta = (angle + step) * pi / 180
            points.append((corner_x + radius * cos(theta), corner_z + radius * sin(theta)))
    return points


def _plate(g, group, mat, side, depth, outline):
    face = list(range(len(outline)))
    if side == 1:
        face.reverse()
    g.mesh(group, mat, [(x, side * depth, z) for x, z in outline], [face])


def _surround(g, group, mat, side, outer, inner, outer_depth, inner_depth):
    count = len(outer)
    vertices = [(x, side * depth, z)
                for profile, depth in ((outer, outer_depth), (inner, inner_depth))
                for x, z in profile]
    faces = []
    for i in range(count):
        j = (i + 1) % count
        face = [i, j, j + count, i + count]
        faces.append(list(reversed(face)) if side == 1 else face)
    g.mesh(group, mat, vertices, faces)


def _windrow(g, group, side, x, start_depth, base_z, length, depth, height, stations):
    """Tapered drift resting on a sill, with an irregular crest and a small lip."""
    vertices = [(x, side * (start_depth + depth * .23), base_z)]
    for t, width, h, lean in stations:
        outer = start_depth + depth * width
        crest = start_depth + depth * width * lean
        vertices.extend([(x + t*length, side*start_depth, base_z),
                         (x + t*length, side*outer, base_z-.003),
                         (x + t*length, side*(outer-.006), base_z+.24*h*height),
                         (x + t*length, side*crest, base_z+h*height)])
    vertices.append((x + length, side * (start_depth + depth * .08), base_z))
    faces = []
    for j in range(4):
        k = (j + 1) % 4
        faces.append((0, 1+k, 1+j))
        for i in range(len(stations)-1):
            v = 1 + 4*i
            faces.append((v+j, v+k, v+4+k, v+4+j))
        v = 1 + 4*(len(stations)-1)
        faces.append((v+j, v+k, len(vertices)-1))
    if side < 0:
        faces = [tuple(reversed(f)) for f in faces]
    g.mesh(group, 'snow', vertices, faces)


def _lamp(g, group, x, y, z):
    """Turned brass standard with a scalloped, glowing silk shade."""
    def turned(mat, profile, pleats=False):
        n = 36 if pleats else 16
        vertices = []
        for lift, radius in profile:
            for j in range(n):
                theta = j * 2*pi / n
                r = radius * (.89 if pleats and j % 2 else 1)
                vertices.append((x+r*cos(theta), y+r*sin(theta), z+lift))
        faces = [(j + i*n, (j+1)%n + i*n, (j+1)%n + (i+1)*n, j+(i+1)*n)
                 for i in range(len(profile)-1) for j in range(n)]
        faces.extend([list(reversed(range(n))), list(range(len(vertices)-n, len(vertices)))])
        g.mesh(group, mat, vertices, faces)
    turned('gilt', [(0,.084), (.013,.088), (.022,.074), (.033,.044),
                    (.05,.023), (.065,.020), (.079,.030), (.093,.017),
                    (.191,.012), (.207,.025), (.223,.022), (.232,.04)])
    turned('shade', [(.204,.153), (.218,.149), (.351,.085), (.36,.084)], True)
    turned('gilt', [(.199,.152), (.205,.155), (.212,.151)], True)
    turned('gilt', [(.358,.085), (.366,.085)], True)


def _curtain(g, group, side, x, handed, fullness, recess=0):
    # A gathered drop starts wide at the pelmet, then narrows at the tieback.
    # Each strip has continuous folds across the fabric, including the hem.
    levels = ((2.641,.149,.035), (2.741,.113,.025), (2.830,.089,.011),
              (2.932,.119,.028), (3.079,.184,.036), (3.260,.252,.038), (3.347,.272,.034))
    vertices = []
    n = 13
    for z, width, amplitude in levels:
        for j in range(n):
            f = j/(n-1)
            local_x = .492 - width*fullness*f
            distance = recess + 1.372 + amplitude*(.5 + .5*cos(f*6*pi)) + .015*f
            lift = .015 * cos(4*pi*f) if z == levels[0][0] else 0
            vertices.append((x+handed*local_x, side*distance, z+lift))
    faces = []
    for k in range(len(levels)-1):
        for j in range(n-1):
            v = k*n + j
            face = (v, v+1, v+n+1, v+n)
            faces.append(face if side == handed else tuple(reversed(face)))
    # Lined cloth has a real inside and outside, and closed hems. A thin
    # separated lining avoids coplanar faces under Unity's backface culling.
    count = len(vertices)
    vertices.extend([(u, v-side*.008, h) for u, v, h in vertices[:]])
    cloth_faces = list(faces) + [tuple(v+count for v in reversed(f)) for f in faces]
    boundary = {}
    for face in faces:
        for a,b in zip(face, face[1:]+face[:1]):
            if (b,a) in boundary:
                del boundary[b,a]
            else:
                boundary[a,b] = True
    cloth_faces.extend((b,a,a+count,b+count) for a,b in boundary)
    g.mesh(group, 'velvet', vertices, cloth_faces)
    center = x + handed * (.492-.045*fullness)
    g.box(group, 'gilt', (center, side*(1.418+recess), 2.834), (.098*fullness,.014,.016), .005)
    g.box(group, 'gilt', (center, side*(1.365+recess), 2.834), (.097*fullness,.012,.016), .005)
    g.cyl(group,'gilt', (center-handed*.036, side*(1.416+recess), 2.835),
          (center-handed*.051, side*(1.410+recess), 2.778), .004, 6)


def _cabin(g, group, side, wing, i, window_desks, curtains):
    x = window_station(wing, i)
    z = 2.98
    tile = _rounded(x,z,1.38,1.12,.004)
    outer = _rounded(x,z,1.185,.891,.112)
    bright = _rounded(x,z,1.145,.854,.106)
    wood = _rounded(x,z,1.113,.822,.098)
    aperture = _rounded(x,z,1.050,.763,.079)
    interior_group = 'Interior_' + ('Corridor_lining' if side < 0 else 'Cabin_lining')
    reveal = _rounded(x,z,1.076,.791,.085)
    mould = _rounded(x,z,1.126,.843,.096)
    bead = _rounded(x,z,1.150,.867,.100)
    casing = _rounded(x,z,1.230,.938,.118)
    _surround(g,group,'ivory',side,tile,outer,1.549,1.549)
    _surround(g,group,'petrol',side,outer,bright,1.549,1.565)
    _surround(g,group,'gilt',side,bright,wood,1.565,1.568)
    _surround(g,group,'wood',side,wood,aperture,1.568,1.528)
    _surround(g,interior_group,'wood',side,aperture,reveal,1.528,1.417)
    # Reverse the signed side/depth pair to construct faces towards the cabin.
    _surround(g,interior_group,'wallcloth',-side,tile,casing,-1.430,-1.430)
    _surround(g,interior_group,'petrol',-side,casing,bead,-1.430,-1.416)
    _surround(g,interior_group,'gilt',-side,bead,mould,-1.416,-1.412)
    _surround(g,interior_group,'wood',-side,mould,reveal,-1.412,-1.417)
    g.box(interior_group,'wood',(x,side*1.410,2.590),(1.170,.123,.041),.011)
    g.box(interior_group,'gilt',(x,side*1.352,2.593),(1.136,.011,.008),.003)
    g.box(group,'wood',(x,side*1.495,3.222),(1.056,.046,.041),.008)
    g.box(group,'gilt',(x,side*1.491,3.225),(1.033,.008,.010),.003)
    # The petite transom and curtain rail leave a clear line of sight.
    for offset in (-.220,.220):
        g.box(group,'gilt',(x+offset,side*1.495,3.290),(.013,.023,.096),.003)
    dressed=curtains(side,wing,i) if callable(curtains) else curtains
    if dressed:
        for dx in (-.545,.545):
            g.cyl(interior_group,'gilt',(x+dx,side*1.43,3.372),(x+dx,side*1.385,3.372),.018,12)
        g.cyl(interior_group,'gilt',(x-.567,side*1.385,3.372),(x+.567,side*1.385,3.372),.013,12)
        for handed in (-1,1):
            fullness = .82 + .075*((i+wing+side+handed)%4)
            if side < 0:
                fullness *= .55
            _curtain(g,interior_group,side,x,handed,fullness)
            for finger in (0,.45,.9):
                eye_x = x+handed*(.492-.252*fullness*finger)
                g.cyl(interior_group,'gilt',(eye_x,side*1.385,3.343),(eye_x,side*1.385,3.377),.006,8)

    if window_desks and side > 0 and i in WINDOW_DESK_BAYS:
        furniture = 'Interior_Window_desks'
        # The shallow writing shelf projects into the actual compartment.
        # Its two turned, wall-mounted brackets leave the floor unobstructed.
        g.box(furniture,'wood',window_desk_center(x),WINDOW_DESK_SIZE,.018)
        g.box(furniture,'gilt',(x,1.053,2.556),(.679,.010,.010),.003)
        for dx in (-.258,.258):
            g.box(furniture,'wood',(x+dx,1.411,2.34),(.086,.048,.370),.009)
            g.cyl(furniture,'gilt',(x+dx,1.392,2.202),(x+dx,1.136,2.527),.018,16)
            g.cyl(furniture,'wood',(x+dx,1.379,2.218),(x+dx,1.206,2.437),.026,16)
        _lamp(g,furniture,x,1.217,2.590)
    g.box(group,'gilt',(x,side*1.565,3.432),(1.187,.055,.026),.009)
    if (side,wing,i) in ((-1,-1,3),(1,1,0)):
        _windrow(g,group,side,x-.53,1.548,3.445,.79,.082,.052,
                 [(.08,.57,.36,.26),(.22,.83,1,.21),(.36,1,.69,.53),
                  (.52,.63,.56,.27),(.66,.59,.37,.35),(.87,.29,.19,.33)])
    elif (side,wing,i) == (-1,1,1):
        _windrow(g,group,side,x+.025,1.548,3.445,.52,.068,.041,
                 [(.20,.61,.48,.34),(.40,.84,.69,.39),(.59,1,1,.23),(.77,.72,.41,.33)])


def _framed_panel(g, group, side, x, z, width, height, front, lining=True):
    """Nested cabinetmaker's mouldings with a recessed veneer field."""
    outer = _rounded(x,z,width,height,.053)
    dark = _rounded(x,z,width-.034,height-.032,.048)
    crest = _rounded(x,z,width-.067,height-.065,.042)
    inner = _rounded(x,z,width-.100,height-.098,.035)
    _surround(g,group,'wood',side,outer,dark,front,front+.010)
    _surround(g,group,'gilt' if lining else 'petrol_light',side,dark,crest,front+.010,front+.012)
    _surround(g,group,'wood',side,crest,inner,front+.012,front)
    _plate(g,group,'wood',side,front,inner)


def _door(g,side,wing,name):
    group = 'Door_'+name
    jamb = 'Doorcase_'+name
    x = wing*8.04
    outer = _rounded(x,2.455,1.10,2.31,.103)
    lip = _rounded(x,2.455,1.055,2.260,.096)
    casing = _rounded(x,2.475,1.019,2.200,.080)
    leaf = _rounded(x,2.475,1.001,2.178,.078)
    sheet = _rounded(x,2.48,1.27,2.40,.004)
    _surround(g,jamb,'petrol',side,sheet,outer,1.549,1.549)
    _surround(g,jamb,'wood',side,outer,lip,1.549,1.568)
    _surround(g,jamb,'gilt',side,lip,casing,1.568,1.503)
    _surround(g,jamb,'wood',side,casing,casing,1.503,1.416)
    inner_sheet = _rounded(x,2.526,1.27,2.308,.004)
    inner_outer = _rounded(x,2.501,1.174,2.254,.10)
    _surround(g,jamb,'wood',-side,inner_sheet,inner_outer,-1.430,-1.430)
    _surround(g,jamb,'gilt',-side,inner_outer,casing,-1.430,-1.416)
    sight_outer = _rounded(x,2.994,.696,.810,.122)
    sight_lip = _rounded(x,2.994,.654,.769,.108)
    sight_glass = _rounded(x,2.994,.594,.709,.092)
    _surround(g,group,'wood',side,leaf,sight_outer,1.503,1.512)
    _surround(g,group,'gilt',side,sight_outer,sight_lip,1.512,1.538)
    _surround(g,group,'petrol',side,sight_lip,sight_glass,1.538,1.516)
    _surround(g,group,'wood',side,sight_glass,sight_glass,1.516,1.440)
    # Four independent real door leaves, including their interior mouldings,
    # lever handles and the continuous edge exposed when a door opens.
    _surround(g,group,'wood',-side,leaf,sight_outer,-1.448,-1.448)
    _surround(g,group,'gilt',-side,sight_outer,sight_lip,-1.448,-1.436)
    _surround(g,group,'wood',-side,sight_lip,sight_glass,-1.436,-1.440)
    _surround(g,group,'wood',-side,leaf,leaf,-1.503,-1.448)
    _framed_panel(g,group,-side,x,1.898,.803,.770,-1.440)
    _framed_panel(g,group,-side,x,2.455,.803,.188,-1.440)
    for dx in (-.129,.129):
        g.box(group,'gilt',(x+dx,side*1.534,3.216),(.013,.016,.187),.002)
    g.box(group,'gilt',(x,side*1.533,3.116),(.594,.021,.019),.004)
    _framed_panel(g,group,side,x,1.87,.811,.735,1.510)
    g.box(group,'gilt',(x,side*1.516,2.413),(.982,.017,.022),.004)
    g.box(group,'gilt',(x,side*1.518,2.387),(.982,.015,.008),.002)
    handle_x = x+wing*.355
    g.box(group,'gilt',(handle_x,side*1.518,2.285),(.074,.024,.148),.018)
    g.cyl(group,'gilt',(handle_x,side*1.518,2.312),(handle_x,side*1.568,2.312),.024,12)
    g.cyl(group,'gilt',(handle_x+wing*.018,side*1.560,2.312),
          (handle_x-wing*.102,side*1.560,2.302),.017,12)
    g.box(group,'gilt',(handle_x,side*1.431,2.317),(.066,.030,.169),.016)
    g.cyl(group,'gilt',(handle_x,side*1.423,2.339),(handle_x,side*1.372,2.339),.023,12)
    g.cyl(group,'gilt',(handle_x+wing*.016,side*1.373,2.339),
          (handle_x-wing*.112,side*1.373,2.330),.016,12)
    hinge_x = x-wing*.492
    for z in (1.670,2.399,3.270):
        g.cyl(group,'gilt',(hinge_x,side*1.475,z),(hinge_x,side*1.475,z+.108),.016,12)
    grab_x = x-wing*.592
    for grab_z in (1.777,2.413):
        g.cyl(jamb,'gilt',(grab_x,side*1.545,grab_z),(grab_x,side*1.606,grab_z),.029,12)
    g.cyl(jamb,'gilt',(grab_x,side*1.600,1.777),(grab_x,side*1.600,2.413),.018,12)
    # Brass-edged timber treads on forged brackets.
    step = 'Entrance_steps_'+name
    for dx in (-.399,.399):
        g.box(step,'dark',(x+dx,side*1.436,1.105),(.068,.115,.433),.012)
        for step_z,depth in ((1.165,1.497),(.932,1.559)):
            g.box(step,'dark',(x+dx,side*depth,step_z-.029),(.068,(depth-1.433)*2+.10,.061),.008)
    for step_z,depth in ((1.165,1.497),(.932,1.559)):
        g.box(step,'wood',(x,side*depth,step_z),(.960,.144,.04),.009)
        g.box(step,'gilt',(x,side*(depth+.073),step_z),(.946,.014,.027),.004)
        for rib in (-.030,.017):
            g.box(step,'rubber',(x,side*(depth+rib),step_z+.02),(.891,.013,.007),.002)
    if side == -1 and wing == 1:
        _windrow(g,step,side,x+.030,1.553,.951,.41,.087,.052,
                 [(.13,.65,.63,.44),(.29,.93,1,.31),(.50,1,.84,.35),(.73,.58,.48,.21)])


def _cladding(g,side,wing,group,window_desks,curtains):
    def box(mat,x,depth,z,width,thickness,height,bevel=0):
        g.box(group,mat,(wing*x,side*depth,z),(width,thickness,height),bevel)
    start,stop = 1.68,7.405
    cx,width = (start+stop)/2,stop-start
    box('petrol',cx,1.514,1.87,width,.070,1.10)
    box('ivory',cx,1.514,3.610,width,.070,.140)
    box('ivory',7.3025,1.514,2.980,.205,.070,1.120)
    box('petrol',8.8375,1.513,2.480,.325,.072,2.400)
    # Continuous, paired lining above and below the cream band.
    for h,t in ((3.592,.017),(3.555,.009),(2.42,.025),(2.376,.010)):
        box('gilt',cx,1.556,h,width,.014,t,.002)
    box('wood',cx,1.555,1.449,width,.028,.126,.008)
    box('gilt',cx,1.570,1.482,width,.008,.012,.002)
    box('gilt',cx,1.563,1.354,width,.015,.018,.004)
    if wing > 0:
        # Large, single veneer panel to carry the coach name.
        _framed_panel(g,group,side,4.49,1.918,5.31,.625,1.566)
    else:
        for i in range(4):
            _framed_panel(g,group,side,-(2.37+1.38*i),1.918,1.246,.654,1.556)
    for i in range(4):
        _cabin(g,'Cabin_windows_'+('Port' if side<0 else 'Starboard'),side,wing,i,window_desks,curtains)


def _inner_panelling(g,side,loading):
    group = 'Interior_' + ('Corridor_lining' if side < 0 else 'Cabin_lining')
    # These solid sections meet the return around each individual window or
    # door. The continuous chair rail, cornice and skirting stop at the jamb.
    def panel_box(mat,x,z,width,height,inner=1.430,thickness=.048,bevel=0):
        g.box(group,mat,(x,side*(inner+thickness/2),z),(width,thickness,height),bevel)

    def rails(left,right):
        x = (right+left)*.5
        width = right-left
        for mat,z,height,inner,thickness in (
                ('petrol',1.429,.114,1.404,.029),
                ('gilt',1.488,.009,1.402,.023),
                ('wood',2.390,.069,1.396,.040),
                ('gilt',2.409,.008,1.394,.014),
                ('wood',3.612,.092,1.395,.044),
                ('gilt',3.569,.013,1.390,.046)):
            panel_box(mat,x,z,width,height,inner,thickness,.002)

    wall_runs=((-7.405,-1.47),(1.47,7.405)) if loading else ((-7.405,7.405),)
    for a,b in wall_runs:
        panel_box('wood',(a+b)/2,1.896,b-a,1.048)
        rails(a,b)
    panel_box('wallcloth',0,3.610,14.810,.140)
    if not loading:
        panel_box('wallcloth',0,2.980,3.36,1.120)
    else:
        for hand in (-1,1):
            panel_box('wood',hand*1.575,2.980,.210,1.120)

    for wing in (-1,1):
        panel_box('wallcloth',wing*7.3025,2.980,.205,1.120)
        panel_box('wood',wing*8.8375,2.526,.325,2.308)
        rails(8.675,9.00) if wing == 1 else rails(-9.00,-8.675)
        for i in range(4):
            x = wing*(2.37+i*1.38)
            _framed_panel(g,group,-side,x,1.922,1.260,.739,-1.426,False)
            # A mitred inlay motif is placed at the centre of each lower panel.
            for direction in (-1,1):
                u = x+direction*.408
                diamond = [(u-.045,1.930),(u,1.984),(u+.045,1.930),(u,1.876)]
                _plate(g,group,'gilt',-side,-1.423,list(reversed(diamond)))
                core = [(u-.027,1.930),(u,1.961),(u+.027,1.930),(u,1.899)]
                _plate(g,group,'ink',-side,-1.421,list(reversed(core)))
            stile = wing*(1.68+i*1.38)
            panel_box('petrol',stile,2.981,.064,1.112,1.414,.022,.005)
            panel_box('gilt',stile,2.981,.009,1.026,1.411,.012,.002)

    # The central blind panels retain the decorative field on the exterior
    # and finish its back as a substantial walnut partition between rooms.
    for x in (() if loading else (-1.115,0,1.115)):
        _framed_panel(g,group,-side,x,1.921,1.015,.735,-1.426,False)
        outer = _rounded(x,2.980,1.019,.935,.086)
        edge = _rounded(x,2.980,.987,.903,.077)
        inner = _rounded(x,2.980,.954,.870,.070)
        _surround(g,group,'wood',-side,outer,edge,-1.428,-1.413)
        _surround(g,group,'gilt',-side,edge,inner,-1.413,-1.418)
        _plate(g,group,'wallcloth',-side,-1.425,inner)


def build(g, *, window_desks=True, curtains=True, loading=False):
    g.box('Floorpan','dark',(0,0,1.326),(18.0,2.991,.092),.025)
    for side in (-1,1):
        flank = 'Port' if side == -1 else 'Starboard'
        _inner_panelling(g,side,loading)
        group = 'Side_panels_'+flank
        if loading:
            for hand in (-1,1):
                g.box(group,'petrol',(hand*1.575,side*1.517,2.48),(.210,.078,2.40))
            g.box(group,'petrol',(0,side*1.517,3.597),(2.94,.078,.166))
        else:
            g.box(group,'petrol',(0,side*1.517,2.48),(3.36,.078,2.40))
            g.box(group,'wood',(0,side*1.555,1.449),(3.36,.028,.126),.008)
            g.box(group,'gilt',(0,side*1.570,1.482),(3.36,.008,.012),.002)
            g.box(group,'gilt',(0,side*1.563,1.354),(3.36,.015,.018),.004)
        for wing in (-1,1):
            _cladding(g,side,wing,group,window_desks,curtains)
            _door(g,side,wing,flank+('_A' if wing<0 else '_B'))
            for dx,line in ((1.618,.019),(1.577,.009)):
                g.box(group,'gilt',(wing*dx,side*1.559,2.89),(line,.012,1.544),.002)
