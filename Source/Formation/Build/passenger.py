"""Passenger accommodation for the Winterbourne formation. Blender metres."""
from math import cos, pi, sin


FLOOR = 1.382
CEILING = 3.61


class Fixture:
    def __init__(self, geometry, layout, name):
        self.g, self.l, self.name = geometry, layout, name

    def box(self, mat, pos, dims, bevel=0):
        self.g.box(self.name, mat, pos, dims, bevel)

    def solid(self, label, mat, pos, dims, bevel=0):
        self.box(mat, pos, dims, bevel)
        self.l.box(self.name + '_' + label, pos, dims)

    def cyl(self, mat, a, b, radius, n=12):
        self.g.cyl(self.name, mat, a, b, radius, n)

    def text(self, text, pos, facing, size, mat='gilt'):
        # Baseline is horizontal, normal points along the requested axis.
        angles = {'+x': pi/2, '-x': -pi/2, '-y': 0, '+y': pi}
        self.g.text(self.name, mat, text, pos, (pi/2, 0, angles[facing]), size)

    def shell(self, mat, rings, smooth=False, capped=True):
        n = len(rings[0])
        vertices = [p for ring in rings for p in ring]
        faces = [tuple(reversed(range(n))), tuple(range(len(vertices)-n, len(vertices)))] if capped else []
        for k in range(len(rings)-1):
            for i in range(n):
                j = (i+1) % n
                faces.append((k*n+i, k*n+j, (k+1)*n+j, (k+1)*n+i))
        self.g.mesh(self.name, mat, vertices, faces, smooth)

    def turn(self, mat, x, y, z, profile, n=16, fluting=0, capped=True):
        rings = []
        for height, radius in profile:
            ring = []
            for i in range(n):
                theta = 2*pi*i/n
                r = radius*(1-fluting if i%2 else 1)
                ring.append((x+r*cos(theta), y+r*sin(theta), z+height))
            rings.append(ring)
        self.shell(mat, rings, not fluting, capped)


def seat(f, x, y, width, facing, fabric, high=False):
    """Carved shell, plump cushion and swept back. Width includes both arms."""
    depth = .54
    f.box('wood', (x, y, 1.757), (depth, width, .114), .027)
    f.box('gilt', (x, y, 1.796), (depth+.014, width+.005, .012), .004)
    for xx in (x-.162, x+.162):
        for yy in (y-width*.33, y+width*.33):
            f.cyl('wood', (xx,yy,1.390), (xx,yy,1.719), .030, 8)
            f.cyl('brass', (xx,yy,1.388), (xx,yy,1.423), .032, 8)
    def pad(mat, length, span, layers):
        rings = []
        for height, scale, shift in layers:
            ring = []
            for i in range(16):
                angle = i*pi/8
                dx, dy = cos(angle), sin(angle)
                sx = (abs(dx)**.43) * (1 if dx>=0 else -1)
                sy = (abs(dy)**.43) * (1 if dy>=0 else -1)
                ring.append((x+shift+sx*length*.5*scale,y+sy*span*.5*scale,height))
            rings.append(ring)
        f.shell(mat, rings, True)
    pad(fabric,.524,width-.042,[(1.799,.91,0),(1.826,1,0),(1.880,.98,0),(1.901,.88,0)])
    rear = -facing*.226
    top = 2.566 if high else 2.472
    f.box('wood',(x+rear-facing*.039,y,(top+1.805)/2),(.094,width-.025,top-1.805),.035)
    pad(fabric,.133,width-.060,[(1.847,.90,rear),(1.916,1,rear),(top-.082,1,rear-facing*.037),
                                (top-.026,.88,rear-facing*.041),(top-.010,.72,rear-facing*.041)])
    for sign in (-1, 1):
        yy=y+sign*(width/2-.027)
        f.box('wood',(x+rear*.35,yy,2.086),(.396,.050,.054),.021)
        f.cyl('brass',(x+facing*.103,yy,1.790),(x+facing*.103,yy,2.064),.013,10)
    for dy in (-width*.19,width*.19):
        f.cyl('brass',(x+rear+facing*.032,y+dy,top-.158),
              (x+rear+facing*.042,y+dy,top-.158),.011,8)


def pendant(f,x,y,z,name,watts=36,range_m=3.6):
    f=Fixture(f.g,f.l,'Ceiling_'+name)
    f.turn('brass',x,y,z,[(0,.12),(.022,.134),(.036,.10),(.124,.060),(.153,.081)],16)
    f.turn('shade',x,y,z-.103,[(0,.062),(.017,.098),(.046,.130),(.090,.135),(.11,.122)],20)
    f.l.lamp(name,(x,y,z-.151),watts,(1,.81,.61),range_m)


def table_lamp(f,x,y,z):
    f.turn('gilt',x,y,z,[(0,.069),(.018,.073),(.029,.041),(.043,.021),
                         (.163,.012),(.174,.025),(.185,.021)],16)
    f.turn('shade',x,y,z,[(.176,.109),(.19,.112),(.285,.067),(.295,.060)],24,.095)
    f.turn('brass',x,y,z,[(.174,.11),(.182,.112)],24,.095)


def case(f,x,y,z,length=.59,width=.30):
    f.box('hide',(x,y,z+.103),(length,width,.206),.036)
    f.box('petrol',(x,y,z+.128),(length+.002,width+.003,.015),.005)
    for off in (-.29,.29):
        f.box('petrol',(x+off*length,y,z+.105),(.027,width+.009,.213),.008)
        f.box('gilt',(x+off*length,y-width*.5-.006,z+.131),(.047,.012,.042),.004)
    for dx in (-.067,.067):
        f.cyl('brass',(x+dx,y,z+.206),(x+dx,y,z+.245),.008,8)
    f.cyl('hide',(x-.070,y,z+.245),(x+.070,y,z+.245),.011,10)


def rack(f, x, y, length, height=3.256):
    hand = 1 if y>0 else -1
    for xx in (x-length*.44,x+length*.44):
        f.cyl('brass',(xx,hand*1.411,height+.062),(xx,y-hand*.147,height+.062),.014,10)
        f.cyl('brass',(xx,hand*1.411,height-.171),(xx,y-hand*.125,height+.055),.014,10)
    for dy in (-.145,-.044,.063,.153):
        f.cyl('brass',(x-length/2,y+dy,height+.072),(x+length/2,y+dy,height+.072),.014,10)
    f.cyl('wood',(x-length/2,y-hand*.158,height+.135),(x+length/2,y-hand*.158,height+.135),.016,10)


def saloon(g,l):
    for i in range(8):
        x = -6.40 + i*1.83
        f = Fixture(g,l,f'Saloon_Row_{i+1:02}')
        facing = 1 if i%2==0 else -1
        for side in (-1,1):
            for y in (.724,1.176):
                seat(f,x,side*y,.450,facing,'ink' if i%4<2 else 'velvet',True)
            l.box(f'Saloon_Row{i+1}_{side}',(x,side*.953,1.976),(.603,.909,1.182))
            rack(f,x,side*1.213,1.338)
            if (i+(side==1))%3==0:
                case(f,x+(.12 if i%2 else -.15),side*1.21,3.345,.595,.261)
            # A scalloped brass reading lamp at the window end of each pair.
            f.cyl('gilt',(x-facing*.271,side*1.386,2.815),
                  (x-facing*.271,side*1.286,2.815),.024,12)
            f.turn('shade',x-facing*.271,side*1.273,2.755,[(0,.065),(.022,.070),(.108,.043)],16)
        if i%2==0:
            pendant(f,x+.60,0,3.462,'SaloonLight_'+str(i),48,4.0)
    l.route('Saloon central aisle',[(-8.71,0),(-6.62,0),(6.62,0),(8.71,0)])
    for i,x in enumerate((-5.56,-1.90,1.76,5.42)):
        l.route('Paired seating bay '+str(i+1),[(x,0),(x,.84)])
    l.view((-6.99,-.15,2.982),(.15,.27,2.51),22)


def build(number,g,l):
    if number == 5:
        saloon(g,l)
    elif number == 6:
        from passenger_dining import dining
        dining(g,l)
    elif number in (4,8):
        from passenger_rooms import accommodation
        accommodation(number,g,l)
    else:
        raise ValueError('Unsupported passenger car: '+str(number))
