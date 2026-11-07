"""Underframe, heating plant, couplings and rolling gear of sleeping car 07.

Coordinates are metres. Y is the axle direction and Z = 0 is the rail datum.
"""

from math import cos, pi, sin


def _side_casting(g, group, material, points, y, width):
    """Extrude a counterclockwise XZ silhouette through the side of the car."""
    count = len(points)
    verts = [(x, y + dy, z) for dy in (-width / 2, width / 2)
             for x, z in points]
    faces = [tuple(range(count)), tuple(reversed(range(count, count * 2)))]
    for index in range(count):
        following = (index + 1) % count
        faces.append((index, index + count, following + count, following))
    g.mesh(group, material, verts, faces)


def _wheel(g, group, x, side):
    # Railway monobloc wheel. The brighter tyre is carried by a dished web.
    z = 0.48
    segments = 32
    profile = [
        (0.672, 0.110, 'steel'),
        (0.677, 0.172, 'dark'),
        (0.711, 0.264, 'steel'),
        (0.702, 0.388, 'dark'),
        (0.694, 0.461, 'steel'),
        (0.703, 0.480, 'bright'),
        (0.718, 0.480, 'steel'),
        (0.737, 0.464, 'bright'),
        (0.828, 0.460, 'bright'),
        (0.840, 0.446, 'steel'),
        (0.840, 0.392, 'steel'),
        (0.825, 0.376, 'dark'),
        (0.795, 0.360, 'steel'),
        (0.757, 0.263, 'steel'),
        (0.830, 0.174, 'steel'),
        (0.845, 0.140, 'bright'),
        (0.845, 0.085, 'dark'),
        (0.672, 0.110, 'steel'),
    ]
    def ring(station, radius):
        return [(x + radius * cos(2 * pi * index / segments), side * station,
                 z + radius * sin(2 * pi * index / segments))
                for index in range(segments)]

    previous = ring(*profile[0][:2])
    for station, radius, material in profile[1:]:
        current = ring(station, radius)
        faces = []
        for index in range(segments):
            following = (index + 1) % segments
            face = (index, index + segments, following + segments, following)
            faces.append(face if side == 1 else tuple(reversed(face)))
        g.mesh(group, material, previous + current, faces)
        previous = current


def _coil(g, group, x, y):
    # Helical wire between pressed spring seats. The gap between turns stays open.
    turns = 3.75
    steps, section = 38, 5
    verts, faces = [], []
    for index in range(steps + 1):
        fraction = index / steps
        angle = fraction * turns * 2 * pi
        for corner in range(section):
            phase = corner * 2 * pi / section
            radial = 0.103 + 0.017 * cos(phase)
            verts.append((x + radial * cos(angle), y + radial * sin(angle),
                          0.635 + fraction * 0.230 + 0.017 * sin(phase)))
    for row in range(steps):
        for corner in range(section):
            a = row * section + corner
            b = row * section + (corner + 1) % section
            faces.append((a, a + section, b + section, b))
    faces.extend([tuple(range(section)), tuple(reversed(range(steps * section,
                                                              (steps + 1) * section)))])
    g.mesh(group, 'steel', verts, faces)


def _bogie(g, x, identifier):
    frame = 'Bogie_' + identifier
    # A short transverse pivot transmits the carriage load into the side frames.
    g.box(frame, 'dark', (x, 0, 0.90), (0.59, 2.08, 0.20), 0.045)
    g.cyl(frame, 'rubber', (x, 0, 0.99), (x, 0, 1.085), 0.51, 16)
    g.cyl(frame, 'steel', (x, 0, 1.085), (x, 0, 1.18), 0.47, 16)
    for axle_index, offset in enumerate((-1.03, 1.03), 1):
        axle_x = x + offset
        wheels = f'Wheelset_{identifier}{axle_index}'
        g.cyl(wheels, 'steel', (axle_x, -0.96, 0.48), (axle_x, 0.96, 0.48), 0.10, 16)
        for side in (-1, 1):
            _wheel(g, wheels, axle_x, side)
            # An inboard brake disc and fixed caliper ride beside each wheel.
            g.cyl(wheels, 'steel', (axle_x, side * 0.52, 0.48),
                  (axle_x, side * 0.56, 0.48), 0.31, 20)
            g.box(frame, 'dark', (axle_x - 0.21, side * 0.545, 0.62),
                  (0.13, 0.14, 0.17), 0.024)

    for side in (-1, 1):
        y = side * 1.025
        # Upper casting: a depressed centre and rolled-over axle horns.
        profile = [
            (-1.45, .625), (-1.12, .665), (-.85, .775), (-.54, .855),
            (-.30, .855), (-.19, .775), (.19, .775), (.30, .855),
            (.54, .855), (.85, .775), (1.12, .665), (1.45, .625),
            (1.52, .72), (1.41, .84), (.92, .955), (.61, 1.02),
            (-.61, 1.02), (-.92, .955), (-1.41, .84), (-1.52, .72),
        ]
        _side_casting(g, frame, 'steel', [(x + dx, z) for dx, z in profile], y, .17)
        # Lower yoke gives the spring pockets their open triangular silhouette.
        outline = [(-1.14, .45), (-.94, .40), (-.62, .53), (.62, .53),
                   (.94, .40), (1.14, .45), (1.10, .53), (.62, .625),
                   (-.62, .625), (-1.10, .53)]
        _side_casting(g, frame, 'dark', [(x + dx, z) for dx, z in outline], y, .15)
        for direction in (-1, 1):
            axial = x + direction * 1.03
            g.box(frame, 'dark', (axial, y, .55), (.38, .26, .315), .055)
            g.cyl(frame, 'steel', (axial, y + side * .115, .49),
                  (axial, y + side * .185, .49), .139, 12)
            g.cyl(frame, 'petrol_light', (axial, y + side * .185, .49),
                  (axial, y + side * .199, .49), .097, 10)
            for index in range(4):
                phase = pi * .25 + index * pi * .5
                bx, bz = axial + .108 * cos(phase), .49 + .108 * sin(phase)
                g.cyl(frame, 'bright', (bx, y + side * .182, bz),
                      (bx, y + side * .199, bz), .015, 6)

            spring_x = x + direction * .43
            for z in (.625, .876):
                g.cyl(frame, 'dark', (spring_x, y, z - .022),
                      (spring_x, y, z + .022), .143, 12)
            _coil(g, frame, spring_x, y)
            # Axlebox damper, with a polished piston emerging from its sleeve.
            bottom = (axial - direction * .13, y + side * .095, .57)
            middle = (axial - direction * .26, y + side * .095, .74)
            top = (axial - direction * .40, y + side * .095, .91)
            g.cyl(frame, 'dark', bottom, middle, .045, 10)
            g.cyl(frame, 'bright', middle, top, .025, 8)
            for bx, by, bz in (bottom, top):
                g.cyl(frame, 'steel', (bx, by, bz),
                      (bx, by + side * .065, bz), .052, 10)
        for offset in (-.15, .15):
            g.cyl(frame, 'bright', (x + offset, y + side * .085, .93),
                  (x + offset, y + side * .105, .93), .032, 6)
        g.cyl(frame, 'steel', (x - 1.21, side * .60, .45),
              (x + 1.21, side * .60, .45), .028, 8)
        for offset in (-1.31, 1.31):
            g.box(frame, 'dark', (x + offset, side * .84, .60), (.12, .47, .09), .014)


def _equipment(g):
    group = 'Underslung_Services'
    # Insulated heat exchanger with a bolted service door and guarded louvers.
    g.box(group, 'dark', (-1.65, -.75, .815), (2.46, .86, .69), .075)
    g.box(group, 'petrol', (-1.65, -1.192, .82), (2.27, .065, .52), .042)
    g.box(group, 'dark', (-1.99, -1.229, .81), (1.27, .035, .355), .022)
    for index in range(6):
        g.box(group, 'steel', (-1.99, -1.259, .665 + index * .057),
              (1.17, .038, .021), .005)
    for x in (-2.65, -.67):
        g.box(group, 'steel', (x, -.75, 1.04), (.092, .93, .24), .013)
        g.box(group, 'brass', (x, -1.24, .96), (.075, .033, .028), .004)
    g.box(group, 'steel', (-.87, -1.24, .82), (.21, .035, .13), .009)
    g.box(group, 'ivory', (-.87, -1.261, .842), (.13, .008, .012))

    # Air reservoirs on longitudinal saddles. Bands, domes and drain cocks
    # remain visible from the inspection side of the carriage.
    for x, y, length in ((1.70, -.75, 1.74), (-.30, .80, 1.88), (2.18, .68, 1.35)):
        z, radius = .815, .268
        g.cyl(group, 'steel', (x - length / 2, y, z),
              (x + length / 2, y, z), radius, 20)
        for side in (-1, 1):
            end = x + side * length / 2
            g.cyl(group, 'steel', (end, y, z), (end + side * .057, y, z), radius * .91, 20)
            g.cyl(group, 'dark', (end + side * .057, y, z),
                  (end + side * .097, y, z), radius * .68, 16)
            strap = x + side * length * .31
            g.cyl(group, 'dark', (strap - .045, y, z),
                  (strap + .045, y, z), radius + .015, 20)
            g.box(group, 'steel', (strap, y, 1.08), (.09, .15, .19), .012)
        g.cyl(group, 'brass', (x, y, .51), (x, y, .58), .034, 8)
    for side in (-1, 1):
        runs = [(side * 3.95, side * 1.13, .94), (side * 3.50, side * 1.13, .94),
                (side * 3.22, side * 1.13, .75), (side * 3.02, side * .99, .75)]
        for a, b in zip(runs, runs[1:]):
            g.cyl(group, 'steel', a, b, .04, 10)
    for y in (.22, .32):
        g.cyl(group, 'steel', (-8.4, y, 1.055), (8.4, y, 1.055), .033, 8)


def _coupling(g, side):
    group = 'Coupling_A' if side == -1 else 'Coupling_B'
    # A sprung pair of side buffers and a screw shackle on the draw hook.
    g.box(group, 'dark', (side*8.94, 0, 1.02), (.29, 2.70, .32), .035)
    for hand in (-1, 1):
        y = hand*.94
        g.box(group, 'steel', (side*9.105,y,1.03), (.08,.40,.34), .027)
        for station, length, radius, finish in (
                (9.14,.09,.182,'dark'), (9.23,.23,.14,'dark'), (9.46,.065,.157,'steel'),
                (9.525,.22,.097,'bright'), (9.745,.045,.127,'dark'), (9.79,.04,.208,'steel')):
            g.cyl(group,finish,(side*station,y,1.03),(side*(station+length),y,1.03),radius,24)
        for dy in (-.15,.15):
            for dz in (-.125,.125):
                g.cyl(group,'bright',(side*9.148,y+dy,1.03+dz),(side*9.16,y+dy,1.03+dz),.021,6)
    g.box(group, 'steel', (side*9.115, 0, 1.03), (.15,.30,.27), .022)
    hook = [(9.15,1.07),(9.32,1.07),(9.48,.986),(9.56,1.018),
            (9.59,1.142),(9.53,1.179),(9.495,1.084),(9.42,1.123),(9.30,1.18),(9.15,1.18)]
    # The casting helper extrudes an XZ profile about Y.
    profile = [(side*x,z) for x,z in hook]
    if side < 0:
        profile.reverse()
    _side_casting(g, group, 'steel', profile, 0, .11)
    # Two flattened forged links, held apart by the screw and its long tommy bar.
    for zc,xc,rx,rz in ((.943,9.46,.091,.123),(.610,9.42,.075,.091)):
        points = [(side*(xc+rx*cos(t*pi/6)),-.064,zc+rz*sin(t*pi/6)) for t in range(12)]
        for a,b in zip(points,points[1:]+points[:1]):
            g.cyl(group,'dark',a,b,.024,8)
    g.cyl(group,'steel',(side*9.42,-.064,.682),(side*9.46,-.064,.847),.032,12)
    g.cyl(group,'bright',(side*9.407,-.175,.743),(side*9.46,.074,.775),.013,8)
    # Looping brake hoses flank the drawbar.
    for hand in (-1, 1):
        route = [(8.92, .47, 1.15), (9.06, .47, 1.04), (9.14, .45, .72),
                 (9.25, .42, .61), (9.34, .37, .63), (9.42, .34, .75)]
        hose = [(side * x, hand * y, z) for x, y, z in route]
        for a, b in zip(hose, hose[1:]):
            g.cyl(group, 'rubber', a, b, .034, 8)
        x, y, z = hose[-1]
        g.cyl(group, 'brass', (x, y, z - .018), (x, y, z + .065), .044, 8)


def build(g):
    group = 'Underframe'
    # Two I-section solebars carry the floor and leave shadow gaps between bays.
    for y in (-1.24, 1.24):
        g.box(group, 'dark', (0, y, 1.205), (17.9, .095, .145), .014)
        for z in (1.13, 1.267):
            g.box(group, 'steel', (0, y, z), (17.9, .18, .026), .008)
    for x in (-8.82, -6.95, -5.90, -4.85, -3.15, -.50, 2.45, 4.85, 5.90, 6.95, 8.82):
        g.box(group, 'dark', (x, 0, 1.206), (.130, 2.70, .14), .012)
    _bogie(g, -5.9, 'A')
    _bogie(g, 5.9, 'B')
    _equipment(g)
    _coupling(g, -1)
    _coupling(g, 1)
