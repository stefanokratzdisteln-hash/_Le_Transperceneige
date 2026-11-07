"""Eight-wheel tender with a continuous crew passage beside the coal bunker."""
from math import cos, pi, sin
from random import Random
from traction_forms import FLOOR,buffers,end_text,headlamp,pipe,plate,side_text,turned,wheel


def chassis(g):
    k="Tender_Underframe"
    for x in (-2.56,-.96,.96,2.56):
        wheel(g,x,.506,"Tender_Wheelset_"+str(int((x+3)*10)))
    for s in (-1,1):
        g.box(k,"dark",(0,s*.947,1.089),(7.32,.152,.310),.035)
        for x in (-2.56,-.96,.96,2.56):
            # Laminated leaf packs terminate in two exposed spring hangers.
            for j in range(4):
                width=.72-j*.118
                for hand in (-1,1):
                    g.beam(k,"steel",(x+hand*width*.5,s*1.045,.985+j*.024),
                           (x,s*1.045,.907+j*.024),.029,.065)
            for off in (-.36,.36):
                g.beam(k,"dark",(x+off,s*1.055,1.075),(x+off,s*1.055,.935),.045,.061)
            g.box(k,"dark",(x,s*1.002,.676),(.306,.155,.289),.037)
            g.box(k,"steel",(x,s*1.091,.669),(.229,.063,.185),.025)
            g.cyl(k,"ink",(x,s*1.098,.660),(x,s*1.149,.660),.077,14)
        for x in (-3.054,3.054):
            g.beam(k,"dark",(x,s*.60,1.02),(x,s*.865,.44),.067,.067)
        g.box(k,"steel",(0,s*.515,.927),(5.85,.115,.196),.020)
    g.box(k,"dark",(0,0,1.246),(7.64,2.79,.191),.024)
    for sign in (-1,1):
        g.box(k,"velvet",(sign*3.696,0,1.044),(.123,2.746,.335),.020)
        buffers(g,k,3.759,4.195,sign)
    g.box(k,"dark",(0,0,1.332),(7.64,2.857,.099),.012)
    for s in (-1,1):
        g.box(k,"steel",(s*4.004,0,1.338),(.392,1.36,.088))
        for j in range(4):
            g.box(k,"dark",(s*(3.89+.061*j),0,1.384),(.012,1.274,.005))


def tank_and_bunker(g):
    k="Tender_Water_Tank"
    g.box(k,"ink",(0,-.567,2.332),(5.28,1.571,1.898),.056)
    # The cambered coal sheets lean inward above the tank. The remaining section
    # of the tender width is reserved for the full-height side gangway.
    for y in (-1.305,.154):
        g.box(k,"dark",(0,y,3.241),(5.25,.105,.249),.019)
        g.box(k,"steel",(0,y,3.373),(5.26,.104,.037),.012)
    for x in (-2.598,2.598):
        g.box(k,"dark",(x,-.58,3.245),(.080,1.50,.254),.015)
    neg="Tender_Tank_Sides"
    profile=[(-3.736,1.389),(3.734,1.389),(3.734,3.472),(3.239,3.472),
             (2.887,3.397),(-2.426,3.397),(-2.692,3.330),(-2.995,3.142),
             (-3.249,2.899),(-3.443,2.799),(-3.736,2.799)]
    plate(g,neg,"ink","Y",-1.429,-1.355,profile)
    for s in (-1,1):
        kside=neg if s<0 else "Cut_Tender_Passage_Side"
        g.box(kside,"brass",(0,s*1.428,1.504),(7.36,.020,.020))
        g.box(kside,"brass",(.058,s*1.429,2.639),(6.72,.021,.020))
        for xx in (-3.31,3.43):
            g.box(kside,"brass",(xx,s*1.429,2.073),(.019,.021,1.14))
        g.box(kside,"petrol",(.035,s*1.438,2.099),(3.82,.021,.399),.008)
        g.box(kside,"brass",(.035,s*1.451,2.099),(3.77,.015,.369),.006)
        g.box(kside,"ink",(.035,s*1.460,2.099),(3.73,.012,.331),.005)
        side_text(g,kside,"gilt","W I N T E R B O U R N E",.035,s,1.47,2.023,.201)
        for xx in (-3.52,-2.70,-1.74,-.77,.19,1.17,2.14,3.14):
            for zz in (1.423,2.733):
                g.cyl(kside,"steel",(xx,s*1.423,zz),(xx,s*1.443,zz),.016,8)
    # External filler on the tank plinth behind the coal pile.
    g.box(k,"ink",(2.982,-.837,2.071),(.578,.837,1.343),.035)
    turned(g,k,"brass","Z",(2.979,-.840,0),[(2.738,.224),(2.767,.251),
          (2.791,.242),(2.809,.193)],24)
    turned(g,k,"dark","Z",(2.979,-.840,0),[(2.805,.217),(2.848,.215),(2.865,.171)],24)
    pipe(g,k,"brass",[(2.91,-.84,2.865),(2.91,-.84,2.918),
                      (3.05,-.84,2.918),(3.05,-.84,2.865)],.014)
    # Fall plate on the footplate end and actual coal door, beside the route.
    g.box(k,"dark",(-2.662,-.627,1.920),(.060,.787,.839),.019)
    g.box(k,"rubber",(-2.704,-.627,1.898),(.028,.603,.588),.012)
    for zz in (1.699,1.914,2.129):
        g.box(k,"steel",(-2.732,-.627,zz),(.025,.564,.032),.005)
    plate(g,k,"steel","Y",-.907,-.347,[(-2.744,1.626),(-3.073,1.432),(-3.073,1.461),(-2.744,1.668)])
    g.cyl(k,"brass",(-2.733,-.982,2.371),(-2.775,-.982,2.371),.044,12)
    g.beam(k,"brass",(-2.784,-.982,2.371),(-2.793,-.772,2.436),.026,.029)
    return k


def coal(g):
    rng=Random(193601)
    k="Tender_Coal"
    def z_at(x,y):
        u,v=x/2.58,(y+.576)/.719
        hill=max(0,1-abs(u)**2.9)*max(0,1-v*v)
        return 3.284+hill*(.331+.174*cos(1.36*x-.49)+.052*sin(4.31*x+2.71*y))
    nx,ny=27,9
    verts=[]
    for j in range(ny):
        for i in range(nx):
            x=-2.57+5.14*i/(nx-1)
            y=-1.288+1.414*j/(ny-1)
            if 0<i<nx-1: x+=rng.uniform(-.05,.05)
            if 0<j<ny-1: y+=rng.uniform(-.040,.040)
            verts.append((x,y,z_at(x,y)+rng.uniform(-.022,.032)))
    faces=[]
    for j in range(ny-1):
        for i in range(nx-1):
            p=j*nx+i
            faces.extend(((p,p+1,p+nx+1),(p,p+nx+1,p+nx)))
    edge=list(range(nx))+[j*nx+nx-1 for j in range(1,ny)]
    edge+=list(range(nx*ny-2,nx*(ny-1)-1,-1))+[j*nx for j in range(ny-2,0,-1)]
    base=[]
    for i in edge:
        x,y,z=verts[i];base.append(len(verts));verts.append((x,y,3.235))
    for i,a in enumerate(edge):
        j=(i+1)%len(edge);faces.append((edge[j],a,base[i],base[j]))
    faces.append(tuple(reversed(base)))
    g.mesh(k,"rubber",verts,faces)
    # Small fractured lumps cast tight, irregular occlusion above the larger heap.
    for i in range(155):
        x=rng.uniform(-2.48,2.48); y=rng.uniform(-1.225,.067)
        z=z_at(x,y)
        radius=rng.uniform(.055,.123)
        f=rng.uniform(.53,1)
        ring=[(x+radius*cos(a*2*pi/5)*rng.uniform(.77,1.04),
               y+radius*f*sin(a*2*pi/5)*rng.uniform(.8,1),z-.020) for a in range(5)]
        top=(x+rng.uniform(-.037,.037),y+rng.uniform(-.022,.022),z+radius*rng.uniform(.41,.82))
        g.mesh(k,"dark" if i%5==0 else "rubber",ring+[top],
               [(a,(a+1)%5,5) for a in range(5)]+[(4,3,2,1,0)])


def passage(g,l):
    side="Cut_Tender_Passage_Side"
    g.box(side,"ink",(0,1.39,2.104),(7.464,.075,1.438))
    for a,b in ((-3.734,-1.85),(-1.18,1.065),(1.735,3.734)):
        g.box(side,"ink",((a+b)/2,1.39,3.048),(b-a,.075,.454))
    g.box(side,"ink",(0,1.39,3.383),(7.467,.075,.228))
    for x in (-1.515,1.40):
        for z in (2.825,3.265):
            g.box(side,"brass",(x,1.396,z),(.709,.1,.032),.006)
        for off in (-.337,.337):
            g.box(side,"brass",(x+off,1.396,3.045),(.032,.1,.462),.006)
        for off in (-.204,0,.204):
            g.cyl(side,"steel",(x+off,1.398,2.84),(x+off,1.398,3.25),.014,10)
    r="Roof_Tender_Tunnel"
    g.box(r,"dark",(0,.809,3.509),(7.477,1.284,.092),.024)
    for x in (-3.177,3.177):
        g.box(r,"dark",(x,-.570,3.509),(1.112,1.495,.092),.024)
    for x in (-3.53,-2.50,-1.25,0,1.25,2.50,3.53):
        g.box(r,"steel",(x,.808,3.560),(.035,1.233,.026),.007)
    for s in (-1,1):
        tag="A" if s<0 else "B"
        k="Tender_Vestibule_"+tag
        for hand in (-1,1):
            g.box(k,"ink",(s*3.695,hand*.994,2.433),(.079,.799,2.093),.013)
            g.box(k,"brass",(s*3.728,hand*.596,2.434),(.050,.029,2.037),.006)
        g.box(k,"ink",(s*3.695,0,3.470),(.082,1.222,.085),.010)
        for yy in (-.621,.621):
            g.box(k,"rubber",(s*3.80,yy,2.431),(.166,.059,2.070),.012)
        g.box(k,"rubber",(s*3.80,0,3.453),(.167,1.278,.071),.012)
        for x in (s*3.86,s*4.10):
            for yy in (-.63,.63):
                g.cyl(k,"brass",(x,yy,1.381),(x,yy,2.309),.023,12)
        for yy in (-.633,.633):
            g.cyl(k,"brass",(s*3.80,yy,2.307),(s*4.13,yy,2.307),.024,12)
        for yy in (-1.12,1.12):
            g.cyl(k,"brass",(s*3.733,yy,2.918),(s*3.787,yy,2.918),.060,16)
        if s>0:
            headlamp(g,k,3.78,-1.110,2.077,1)
    k="Tender_Crew_Passage"
    # The interior wall is the water tank. Its riveted lining and handrail sit
    # inside the narrow shoulder, keeping at least 0.92 m unobstructed.
    for x in (-2.53,-1.69,-.845,0,.845,1.69,2.53):
        g.box(k,"dark",(x,.231,2.414),(.041,.028,2.015),.006)
        for z in (1.556,2.564,3.302):
            g.cyl(k,"steel",(x,.244,z),(x,.261,z),.020,8)
    g.box(k,"steel",(0,.244,3.300),(5.258,.023,.034))
    for j in range(29):
        x=-2.718+j*.195
        g.box(k,"steel",(x,.815,1.385),(.014,.994,.006))
    for x in (-2.48,-1.53,-.48,.52,1.51,2.48):
        g.cyl(k,"brass",(x,.239,2.279),(x,.327,2.279),.023,12)
    pipe(g,k,"brass",[(-2.59,.254,2.279),(-2.48,.332,2.279),(2.49,.332,2.279),
                       (2.604,.256,2.279)],.022,12)
    for x in (-1.79,.016,1.79):
        g.box(k,"dark",(x,.252,2.977),(.188,.055,.277),.023)
        g.box(k,"shade",(x,.298,2.977),(.121,.059,.190),.022)
        for dz in (-.096,.096):
            g.box(k,"brass",(x,.313,2.977+dz),(.169,.083,.024),.006)
        for dx in (-.048,0,.048):
            g.cyl(k,"brass",(x+dx,.341,2.889),(x+dx,.341,3.062),.006,8)
        l.lamp("Tunnel_lamp_"+str(int((x+2)*100)),(x,.42,2.958),12,(1,.76,.48),2.7)
    for x,caption in ((-2.014,"FOOTPLATE"),(2.038,"GANGWAY")):
        g.box(k,"brass",(x,.241,2.564),(.631,.018,.141),.007)
        g.box(k,"ink",(x,.254,2.564),(.609,.013,.121),.006)
        side_text(g,k,"bone",caption,x,1,.264,2.533,.081)
    # Float level indicator and a polished wheel for the water service valve.
    x=-.746
    g.box(k,"brass",(x,.242,2.736),(.190,.027,.478),.010)
    g.box(k,"enamel",(x,.260,2.736),(.159,.013,.441))
    for i in range(8):
        g.box(k,"ink",(x+.039,.272,2.568+.045*i),(.058 if i%2==0 else .037,.008,.006))
    g.box(k,"velvet",(x-.035,.273,2.695),(.036,.014,.204),.003)
    side_text(g,k,"ink","GAL",x,1,.281,2.888,.038)
    x=.880
    g.cyl(k,"brass",(x,.219,2.627),(x,.322,2.627),.041,14)
    turned(g,k,"velvet","Y",(x,.336,2.627),[(-.020,.117),(.020,.117),(.020,.095),(-.020,.095)],22,True)
    for i in range(5):
        a=i*2*pi/5
        g.beam(k,"brass",(x,.337,2.627),(x+.102*cos(a),.337,2.627+.102*sin(a)),.015,.015)
    l.box("Tender_deck",(0,0,1.325),(7.64,2.86,.114))
    for s in (-1,1):
        l.box("Tender_fallplate_"+str(s),(s*4.004,0,1.332),(.392,1.36,.10))
        l.box("Tender_side_"+str(s),(0,s*1.39,2.45),(7.47,.082,2.136))
        for hand in (-1,1):
            l.box("Portal_"+str(s)+"_"+str(hand),(s*3.70,hand*1.002,2.45),(.11,.804,2.136))
    l.box("Coal_and_water",(0,-.57,2.411),(5.28,1.62,2.058))
    l.box("Rear_filler",(2.982,-.837,2.091),(.60,.85,1.418))
    l.box("Tunnel_ceiling",(0,.809,3.514),(7.48,1.28,.10))
    l.box("Tender_underframe",(0,0,1.015),(7.44,2.08,.507))
    l.route("Cab_to_carriages",[(-4.19,0),(-3.42,0),(-3.00,.78),(-2.66,.83),
                               (2.66,.83),(3.00,.78),(3.42,0),(4.19,0)])
    l.view((2.731,1.079,2.868),(-1.51,.677,2.542),20)


def tender(g,l):
    l.socket(-4.20,4.20)
    chassis(g)
    tank_and_bunker(g)
    coal(g)
    passage(g,l)
