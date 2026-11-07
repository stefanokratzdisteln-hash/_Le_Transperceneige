"""Four single-occupancy compartments. Dimensions and collider data are metres."""
from math import cos, pi, sin


FLOOR = 1.372
TOP = 3.625
BAYS = ((-7.20, 1), (-1.68,-1), (1.68,1), (7.20,-1))
TRANSVERSE = (-7.20,-4.44,-1.68,1.68,4.44,7.20)
JAMB = (1.54,2.44)
SLIDER_SIZE = (.938,.046,2.001)
SLIDER_OPEN = 1.118
SLIDER_Y = -.234
SLIDER_Z = FLOOR + .010 + SLIDER_SIZE[2]/2


def door_poses():
    """Mesh name, open panel centre, closed panel centre, and solid box size."""
    return [(f"CabinDoor_{n:02}",(head+direction*SLIDER_OPEN,SLIDER_Y,SLIDER_Z),
             (head+direction*1.990,SLIDER_Y,SLIDER_Z),SLIDER_SIZE)
            for n,(head,direction) in enumerate(BAYS,1)]


def collision_boxes():
    """Static boxes in Blender world space. Sliding door boxes are in door_poses."""
    boxes = [(f"Partition_{n}",(x,.55,(FLOOR+TOP)/2),(.072,1.72,TOP-FLOOR))
             for n,x in enumerate(TRANSVERSE,1)]
    for n,(head,direction) in enumerate(BAYS,1):
        def box(name,x,y,z,size):
            boxes.append((f"Cabin{n:02}_{name}",(head+direction*x,y,z),size))
        for label,lo,hi in (("Partition",0,JAMB[0]+.017),("Post",JAMB[1]-.017,2.76)):
            box(label,(lo+hi)/2,-.31,(FLOOR+TOP)/2,(hi-lo,.091,TOP-FLOOR))
        box("Lintel",1.99,-.31,(3.395+TOP)/2,(.90,.110,TOP-3.395))
        box("Bed",1.072,1.012,(FLOOR+1.926)/2,(2.012,.796,1.926-FLOOR))
        box("Headboard",.089,1.015,2.133,(.087,.806,.657))
        box("Basin",2.437,1.091,(FLOOR+2.294)/2,(.502,.594,2.294-FLOOR))
        box("Rack",.246,.871,3.180,(.403,1.005,.077))
    return boxes


class Bay:
    """Local X grows from the bedhead towards the washstand."""
    def __init__(self,g,n,head,direction):
        self.g,self.n,self.head,self.direction = g,n,head,direction
        self.group=f"Cabin_{n:02}"

    def p(self,x,y,z):
        return (self.head+self.direction*x,y,z)

    def box(self,m,x,y,z,size,b=0):
        self.g.box(self.group,m,self.p(x,y,z),size,b)

    def cyl(self,m,a,b,r,n=12):
        self.g.cyl(self.group,m,self.p(*a),self.p(*b),r,n)

    def beam(self,m,a,b,width,depth=None):
        self.g.beam(self.group,m,self.p(*a),self.p(*b),width,depth)

    def mesh(self,m,verts,faces,smooth=False):
        winding = faces if self.direction > 0 else [tuple(reversed(f)) for f in faces]
        self.g.mesh(self.group,m,[self.p(*v) for v in verts],winding,smooth)

    def rings(self,m,rings,smooth=False):
        n=len(rings[0])
        verts=[v for ring in rings for v in ring]
        faces=[tuple(reversed(range(n))),tuple(range(len(verts)-n,len(verts)))]
        for base in range(0,len(verts)-n,n):
            for i in range(n):
                j=(i+1)%n
                faces.append((base+i,base+j,base+j+n,base+i+n))
        self.mesh(m,verts,faces,smooth)

    def text(self,body,x,y,z,size):
        self.g.text(self.group,"gilt",body,self.p(x,y,z),(pi/2,0,0),size)


def signed_pow(x, exponent):
    return (-1 if x<0 else 1)*abs(x)**exponent


def cushion(c,mat,cx,cy,base,length,width,profile,exponent=.35):
    """Bellied upholstery with a soft stitched edge and a shallow domed top."""
    rings=[]
    for z,scale in profile:
        ring=[]
        for i in range(24):
            theta=i*pi/12
            ring.append((cx+length*.5*scale*signed_pow(cos(theta),exponent),
                         cy+width*.5*scale*signed_pow(sin(theta),exponent),base+z))
        rings.append(ring)
    c.rings(mat,rings,True)


def transverse_trim(c):
    # Two framed faces, with a cream upper panel and a three-piece chair rail.
    for x,inside in ((.037,1),(2.723,-1)):
        def trim(mat,y,z,w,h,depth=.014):
            c.box(mat,x+inside*depth*.49,y,z,(depth,w,h))
        trim("ivory",.54,2.991,1.634,1.16)
        for inset in (.103,.969):
            trim("brass",inset,1.908,.739,.781,.021)
            trim("petrol",inset,1.908,.717,.759,.029)
            trim("wood",inset,1.908,.669,.713,.036)
        trim("petrol",.535,1.430,1.695,.092,.041)
        trim("wood",.535,2.347,1.68,.104,.040)
        trim("gilt",.535,2.405,1.678,.014,.044)
        trim("wood",.535,3.578,1.679,.066,.045)
        for y in (-.221,.520,1.316):
            trim("wood",y,2.994,.051,1.14,.027)
            trim("gilt",y,2.994,.009,1.11,.030)


def corridor_partition(c):
    for a,b in ((0,JAMB[0]),(JAMB[1],2.76)):
        mid=(a+b)/2
        c.box("wood",mid,-.31,(FLOOR+TOP)/2,(b-a,.072,TOP-FLOOR))
        for z,h in ((1.431,.103),(2.347,.075),(3.569,.069)):
            c.box("petrol",mid,-.311,z,(b-a,.095,h),.007)
        for y in (-.356,-.265):
            c.box("gilt",mid,y,2.391,(b-a,.011,.013))
    # Book-matched veneers and an inlaid fan on the corridor-facing field.
    for x in (.399,1.128):
        for z,h in ((1.886,.732),(2.939,.945)):
            c.box("brass",x,-.350,z,(.637,.013,h),.005)
            c.box("petrol",x,-.357,z,(.619,.012,h-.022),.005)
            c.box("wood",x,-.364,z,(.572,.010,h-.073),.004)
        for hand in (-1,1):
            c.beam("gilt",(x,-.372,2.785),(x+hand*.113,-.372,2.925),.011,.011)
            c.beam("gilt",(x,-.372,2.785),(x+hand*.169,-.372,2.876),.011,.011)
    # Gilt convex mirror over the inlaid fan. Wood and gilt rings have depth
    # and the face real specularity, so passing light picks up the oval profile.
    mirror_x=.399 if c.n in (1,3) else 1.128
    for m,front,rx,rz in (("petrol",-.377,.188,.250),("gilt",-.386,.176,.236),
                          ("mirror",-.392,.159,.214)):
        rings=[[(mirror_x+rx*cos(i*pi/16),yy,3.079+rz*sin(i*pi/16)) for i in range(32)]
               for yy in (front+.010,front)]
        c.rings(m,rings)
    for x in JAMB:
        c.box("petrol",x,-.310,2.391,(.050,.111,2.037),.009)
        c.box("brass",x,-.369,2.391,(.014,.012,2.033),.004)
    c.box("wood",1.99,-.31,3.415,(.949,.111,.040),.008)
    c.box("wood",1.99,-.31,3.581,(.949,.108,.068),.008)
    # Open ventilation transom: the slats have actual gaps between blades.
    for x in (1.691,1.990,2.289):
        for z in (3.456,3.498,3.535):
            c.box("brass",x,-.310,z,(.274,.042,.010),.003)
    for x in (1.841,2.139):
        c.box("wood",x,-.31,3.492,(.022,.070,.139),.004)
    c.box("brass",2.585,-.354,2.799,(.221,.016,.127),.008)
    c.box("petrol",2.585,-.365,2.799,(.204,.011,.109),.005)
    c.text(f"{c.n:02}",2.585,-.374,2.763,.095)
    c.box("brass",1.506,-.243,3.409,(1.992,.027,.029),.007)
    # Recessed pull and polished panel belong to the moving door.
    previous=c.group
    c.group=f"CabinDoor_{c.n:02}"
    mid=SLIDER_OPEN
    c.box("wood",mid,SLIDER_Y,SLIDER_Z,SLIDER_SIZE,.009)
    for y,face in ((-.257,-1),(-.211,1)):
        for z,w,h in ((1.89,.746,.766),(2.853,.746,.828)):
            c.box("brass",mid,y+face*.003,z,(w,.008,h),.003)
            c.box("petrol",mid,y+face*.009,z,(w-.019,.006,h-.019),.002)
            c.box("wood",mid,y+face*.014,z,(w-.075,.006,h-.071),.002)
        c.box("brass",mid+.423,y+face*.011,2.350,(.073,.016,.159),.005)
        c.box("ink",mid+.423,y+face*.020,2.355,(.044,.006,.115),.003)
    # The edge pull remains visible when the panel is parked; it is used to
    # draw the door out of its pocket. Its escutcheon sits flush with the stile.
    c.box("gilt",mid+.469,SLIDER_Y,2.353,(.009,.039,.137),.004)
    c.box("ink",mid+.474,SLIDER_Y,2.353,(.006,.023,.099),.003)
    c.group=previous


def bed(c):
    y=1.014
    c.box("petrol",1.072,y,1.540,(1.979,.769,.331),.019)
    c.box("wood",1.072,y,1.689,(2.012,.796,.132),.026)
    # Base drawers are usable from the aisle; drop pulls have a brass backplate.
    for s in (.596,1.543):
        c.box("wood",s,.621,1.542,(.877,.026,.215),.012)
        c.box("gilt",s,.602,1.546,(.113,.015,.038),.005)
        c.cyl("brass",(s-.034,.582,1.530),(s+.034,.582,1.530),.010,10)
    # A shallow arch in the walnut headboard, fielded and edged with gilt.
    def head_panel(x1,x2,w,bottom,top,mat):
        edge=[(y-w/2,bottom),(y+w/2,bottom),(y+w/2,top-.13),(y+w*.37,top-.073),
              (y+w*.15,top-.013),(y,top),(y-w*.15,top-.013),(y-w*.37,top-.073),(y-w/2,top-.13)]
        c.rings(mat,[[(x,yy,z) for yy,z in edge] for x in (x1,x2)])
    head_panel(.044,.115,.790,1.694,2.458,"wood")
    head_panel(.114,.122,.693,1.796,2.392,"gilt")
    head_panel(.122,.13,.672,1.808,2.378,"velvet")
    cushion(c,"linen",1.082,y,1.708,1.95,.78,
            ((.010,.95),(.034,.994),(.11,1),(.144,.985),(.158,.87)))
    cushion(c,"linen",.363,y,1.863,.448,.641,
            ((.000,.88),(.018,1),(.098,.89),(.133,.57),(.145,.22)),.52)
    # The sleeping blanket hangs over the rail in four slightly unequal pleats.
    blanket_y=(.602,.618,.647,.681,.742,.860,1.009,1.208,1.370,1.404)
    heights=(1.759,1.822,1.877,1.896,1.908,1.913,1.919,1.915,1.901,1.882)
    rows=[]
    for i,x in enumerate((.686,.733,1.056,1.235,1.499,1.731,1.939,2.023,2.048)):
        for j,yy in enumerate(blanket_y):
            # Fold back the aisle-side head corner exposing the white lining.
            inset = (.273*(1-min(1,max(0,(yy-.602)/.43)))) if i<2 else 0
            ripple = .010*sin(12*x+5*yy+c.n) + .005*cos(18*yy-4*x)
            zz = heights[j]+ripple-(.035 if i==8 else 0)
            rows.append((x+inset,yy,zz))
    # Thicken the blanket so the drop and its edge read correctly from the berth.
    n=len(blanket_y)
    faces=[(i*n+j,(i+1)*n+j,(i+1)*n+j+1,i*n+j+1) for i in range(8) for j in range(n-1)]
    topn=len(rows)
    boundary=list(range(n))+[i*n+n-1 for i in range(1,9)]+list(range(9*n-2,8*n-1,-1))
    boundary+=[i*n for i in range(7,0,-1)]
    shell=rows+[(x,yy,z-.013) for x,yy,z in rows]
    c.mesh("velvet",shell,faces+[tuple(topn+i for i in reversed(f)) for f in faces]+[
        (a,b,b+topn,a+topn) for a,b in zip(boundary,boundary[1:]+boundary[:1])],True)
    # Ivory fold lies proud of the cover. Positioning follows the clipped diagonal.
    for j in range(n-1):
        a,b=rows[j],rows[j+1]
        def lip(v,dx,h):
            return (v[0]+dx,v[1],v[2]+h)
        p=[lip(a,.003,.007),lip(a,.093,.021),lip(b,.093,.021),lip(b,.003,.007)]
        c.mesh("linen",p,[(0,1,2,3)])
    # Contrasting woven pinstripes at the foot of the bed.
    for x in (1.785,1.821):
        for j in range(n-1):
            def stripe(dx,k):
                yy=blanket_y[k]
                z=heights[k]+.010*sin(12*(x+dx)+5*yy+c.n)+.005*cos(18*yy-4*(x+dx))+.012
                return (x+dx,yy,z)
            c.mesh("hide",[stripe(0,j),stripe(.012,j),stripe(.012,j+1),stripe(0,j+1)],[(0,1,2,3)])


def washstand(c):
    x,y=2.437,1.09
    c.box("wood",x,y,1.777,(.457,.540,.701),.020)
    c.box("petrol",x,y,1.425,(.403,.482,.107),.013)
    c.box("brass",x,.813,1.786,(.346,.015,.464),.007)
    c.box("wood",x,.798,1.786,(.327,.017,.443),.008)
    c.cyl("gilt",(x+.115,.786,1.931),(x+.115,.762,1.931),.019,16)
    c.box("enamel",x,y,2.144,(.501,.592,.060),.021)
    # The porcelain bowl has a real hollow and drain, both visible from above.
    rings=[]
    for rx,ry,z in ((.088,.076,2.171),(.158,.146,2.195),(.188,.174,2.268),
                     (.190,.177,2.289),(.177,.164,2.301),(.162,.150,2.286),
                     (.141,.132,2.247),(.090,.079,2.201),(.029,.024,2.189)):
        rings.append([(x+rx*cos(i*pi/16),y+ry*sin(i*pi/16),z) for i in range(32)])
    c.rings("enamel",rings,True)
    c.cyl("brass",(x,y,2.189),(x,y,2.194),.021,16)
    c.cyl("ink",(x,y,2.194),(x,y,2.195),.011,12)
    for hand in (-1,1):
        yy=y+hand*.099
        c.cyl("brass",(2.643,yy,2.172),(2.643,yy,2.209),.027,16)
        c.cyl("enamel",(2.643,yy-.024,2.216),(2.643,yy+.024,2.216),.009,12)
    pipe=[(2.641,y,2.174),(2.641,y,2.329),(2.626,y,2.365),(2.591,y,2.383),
          (2.554,y,2.379),(2.533,y,2.359),(2.533,y,2.339)]
    for a,b in zip(pipe,pipe[1:]):
        c.cyl("brass",a,b,.013,12)
    for mat,xx,ry,rz in (("wood",2.667,.245,.319),("gilt",2.651,.221,.295),
                           ("mirror",2.639,.199,.269)):
        rings=[[(s,y+ry*cos(i*pi/14),2.818+rz*sin(i*pi/14)) for i in range(28)] for s in (xx,xx+.017)]
        c.rings(mat,rings)
    # A folded hand towel, draped over a rail on the washstand wall.
    for yy in (.203,.637):
        c.cyl("gilt",(2.679,yy,2.267),(2.598,yy,2.267),.018,12)
    c.cyl("brass",(2.591,.19,2.267),(2.591,.649,2.267),.015,12)
    c.box("linen",2.571,.406,2.137,(.024,.294,.280),.010)
    for z in (2.025,2.047):
        c.box("hide",2.557,.406,z,(.005,.283,.007))


def rack_and_light(c):
    # Brass rack above the berth, with triangulated brackets fixed to the bulkhead.
    for yy in (.396,1.342):
        c.beam("brass",(.073,yy,2.987),(.426,yy,3.169),.022,.024)
        c.cyl("brass",(.079,yy,3.168),(.432,yy,3.168),.017,12)
    for x in (.106,.214,.319,.426):
        c.cyl("brass",(x,.372,3.179),(x,1.37,3.179),.015,12)
    c.cyl("brass",(.424,.370,3.249),(.424,1.372,3.249),.013,12)
    for yy in (.389,.872,1.352):
        c.cyl("brass",(.424,yy,3.169),(.424,yy,3.249),.010,10)
    # Railway valise, girdled with two straps and fitted with a raised handle.
    if c.n != 3:
        y=.83 if c.n!=2 else .956
        c.box("hide",.246,y,3.302,(.294,.565,.222),.041)
        c.box("wood",.248,y,3.357,(.295,.560,.018),.007)
        for yy in (y-.161,y+.161):
            c.box("petrol",.247,yy,3.305,(.301,.032,.221),.012)
            c.box("brass",.402,yy,3.335,(.013,.055,.043),.005)
        for yy in (y-.058,y+.058):
            c.cyl("brass",(.246,yy,3.409),(.246,yy,3.442),.009,10)
        c.cyl("hide",(.246,y-.060,3.445),(.246,y+.060,3.445),.013,12)
    # Low brass reading sconce below the window sill, clear of the pillow.
    x=.29
    c.cyl("brass",(x,1.411,2.462),(x,1.377,2.462),.057,20)
    c.beam("brass",(x,1.377,2.462),(x,1.269,2.491),.022,.022)
    c.cyl("brass",(x,1.269,2.49),(x,1.269,2.539),.017,12)
    rings=[]
    for z,r in ((2.414,.080),(2.426,.088),(2.529,.045),(2.541,.037)):
        rings.append([(x+r*cos(i*pi/10),1.269+r*sin(i*pi/10),z) for i in range(20)])
    c.rings("shade",rings,True)
    c.cyl("gilt",(x,1.269,2.414),(x,1.269,2.420),.081,20)
    # Coat pegs in the clear panel above the foot of the aisle.
    for yy in (.021,.189):
        c.cyl("brass",(.056,yy,2.824),(.107,yy,2.824),.019,12)
        c.cyl("brass",(.100,yy,2.822),(.127,yy,2.869),.013,12)


def build(g):
    for x in TRANSVERSE:
        g.box("Cabin_Partitions","wood",(x,.55,(FLOOR+TOP)/2),(.072,1.72,TOP-FLOOR))
    for n,(head,direction) in enumerate(BAYS,1):
        cabin=Bay(g,n,head,direction)
        transverse_trim(cabin)
        corridor_partition(cabin)
        bed(cabin)
        washstand(cabin)
        rack_and_light(cabin)
