"""Bench construction of the original five North Fell grand consignments, in metres."""
from math import cos, sin, pi, tau, exp, sqrt
from mathutils import Vector
import station_heritage as s
from station_heritage import part, face, rod, tube, box, ellipsoid, turned

GOLD="Hammered 22ct"
GILT="Fire gilt silver"
SILVER="Polished platinum"
DARK="Blued niello"
BLACK="Banded black onyx"
PEARL="Mother of pearl"
RUBY="Pigeon blood garnet"
GREEN="Cabochon emerald"
BLUE="Cabochon sapphire"
CLEAR="Cut diamond"
GLASS="Clear rock crystal"
IVORY="Old parchment"
WOOD="Flamed maple"
SPRUCE="Alpine varnish"
STEEL="Watered steel"
COVER="Celestial book tooling"
SHELL="Tiger striped nacre"
PIVOT=(0,-.009,.153)


def band(m, center, rx, ry, width, vertical=False, n=40):
    x,y,z=center
    def at(a):
        return (x+rx*cos(a),y,z+ry*sin(a)) if vertical else (x+rx*cos(a),y+ry*sin(a),z)
    tube(m,[at(i*tau/n) for i in range(n+1)], width,6)


def stone(x,y,z,rx,ry,h,m,flat=False):
    """Bezel, girdle and kite crown retain their actual thickness at shelf distance."""
    sides=10
    def pos(a,r,d):
        return (x+rx*r*cos(a),y-d,z+ry*r*sin(a)) if flat else (x+rx*r*cos(a),y+ry*r*sin(a),z+d)
    def level(r,d,turn):
        return [pos((i+turn)*tau/sides,r,d) for i in range(sides)]
    bezel,girdle,break_,crown=level(1.10,0,0),level(1,h*.24,0),level(.82,h*.69,.45),level(.56,h,0)
    for lower,upper,metal in ((bezel,girdle,GOLD),(girdle,break_,m),(break_,crown,m)):
        for i in range(sides):
            j=(i+1)%sides
            face((lower[i],lower[j],upper[j]),metal)
            face((lower[i],upper[j],upper[i]),metal)
    face(crown,m)
    for j in range(4):
        px,py,pz=pos((j+.5)*pi/2,1,.23*h)
        ellipsoid(GILT,(px,py,pz),(rx*.11,rx*.11,rx*.11),8)


def leaf(m, start, end, width, bulge=.0007):
    a,b=Vector(start),Vector(end)
    d=b-a
    side=Vector((-d.y,d.x,0)).normalized()*width
    l=a+d*.43+side;r=a+d*.49-side;mid=a+d*.48+Vector((0,0,bulge))
    for p,q in ((a,r),(r,b),(b,l),(l,a)):face((p,q,mid),m)


def poly_prism(mat, outline, z, thickness):
    top=[(x,y,z+thickness) for x,y in outline];bottom=[(x,y,z) for x,y in outline]
    face(top,mat);face(bottom[::-1],mat)
    for j in range(len(top)):
        k=(j+1)%len(top)
        face([bottom[j],bottom[k],top[k],top[j]],mat)


def nautilus():
    part("Pierced Renaissance foot and cast hippocamp")
    turned(GILT,(0,0,0),[(0,.042),(.003,.050),(.006,.052),(.010,.047),(.014,.034),(.019,.026),
                         (.020,.029),(.024,.024),(.027,.017)],sides=44,oval=.72)
    for z,r in ((.006,.050),(.014,.034),(.020,.027)): band(GOLD,(0,0,z),r,r*.72,.0012)
    for i in range(12):
        a=i*tau/12
        leaf(GOLD,(.020*cos(a),.014*sin(a),.020),(.042*cos(a),.030*sin(a),.0105),.002)
    # Model a seahorse in profile, its curled tail taking the bowl's weight.
    c=(.009,0,.057)
    points=[(c[0]+(.005+.025*i/28)*cos(i*.18),0,c[2]+(.005+.021*i/28)*sin(i*.18)) for i in range(29)]
    points.extend([(.018,0,.057),(.014,0,.082),(.003,0,.097),(-.010,0,.099)])
    tube(GILT,points,.0074,12)
    ellipsoid(GILT,(-.009,0,.106),(.020,.010,.014),18)
    rod(GILT,(-.017,0,.106),(-.034,0,.099),.008,.004)
    rod(GILT,(-.004,0,.114),(-.008,0,.128),.004,.0006)
    for flank in (-1,1):
        ellipsoid(RUBY,(-.015,flank*.009,.109),(.002,.0015,.002),10)
        for i in range(6):
            p=(.013-.0008*i,flank*.006,.073+i*.0046)
            face([p,(p[0]+.011,flank*.002,p[2]+.006),(p[0]+.003,flank*.004,p[2]-.003)],GOLD)
    for side in (-1,1):
        tube(GILT,[(side*.013,0,.040),(side*.030,0,.063),(side*.041,0,.106),
                   (side*.058,0,.125)],.0034)
    part("Ribbed chambered shell, natural tiger bands and nacre lip")
    steps=100; cross=38
    def cross_section(t,b,inset=0):
        a=-.36+8.5*(t-1)
        radial=.0065*exp(2.53*t)
        rr=radial*(.81-inset)
        ripple=.00043*t*cos(tau*(t*38+sin(b)*.17))
        shell_radius=radial+(rr+ripple)*cos(b)
        return Vector((-.010+shell_radius*cos(a),(.61*radial+ripple-inset*radial)*sin(b),.174+shell_radius*sin(a)))
    for j in range(steps):
        t0=j/steps;t1=(j+1)/steps
        for k in range(cross):
            u=k/cross;v=(k+1)/cross
            verts=[cross_section(t0,tau*u),cross_section(t1,tau*u),cross_section(t1,tau*v),cross_section(t0,tau*v)]
            face(verts[::-1],SHELL,True,[(t0,v),(t1,v),(t1,u),(t0,u)])
    # The entire aperture is open, with a second tapering nacre surface inside.
    for j in range(18):
        u=.77+.23*j/18;v=.77+.23*(j+1)/18
        for k in range(cross):
            a=k*tau/cross;b=(k+1)*tau/cross
            face([cross_section(v,b,.035),cross_section(u,b,.035),cross_section(u,a,.035),cross_section(v,a,.035)],
                 "Aperture pearl",True,[(v,b/tau),(u,b/tau),(u,a/tau),(v,a/tau)])
    aperture=[cross_section(1,k*tau/cross) for k in range(cross+1)]
    tube(PEARL,aperture,.0023,7)
    part("Chased gold shell straps, fluted saddle and silver rope edge")
    for zone in (.79,.91):
        path=[cross_section(zone,k*tau/48,-.015) for k in range(49)]
        tube(GILT,path,.0022,8)
        for offset in (-.0022,.0022):
            tube(GOLD,[Vector(p)+Vector((offset,0,0)) for p in path],.00065,6)
    for side in (-1,1):
        tube(GOLD,[(.024,side*.006,.079),(.035,side*.017,.103),
                   (.043,side*.030,.128),(.051,side*.032,.145)],.0026)
    band(GOLD,(0,0,.093),.015,.010,.0019)


def dagger():
    part("Watered steel blade, raised central rib and gilded forte")
    def axis(t):return -.190+.258*t,.010+.044*(1-t)**2,.013
    def blade_point(t,u):
        x,y,z=axis(t);half=.023*(sin(t*pi*.47)**.73) if t else .0004
        return (x,y+u*half,z+.0043*(1-abs(u))+.0016*t)
    for i in range(30):
        a=i/30;b=(i+1)/30
        for l,r in ((-1,-.75),(-.75,0),(0,.75),(.75,1)):
            ring=[blade_point(a,l),blade_point(b,l),blade_point(b,r),blade_point(a,r)]
            uv=[(a,(l+1)/2),(b,(l+1)/2),(b,(r+1)/2),(a,(r+1)/2)]
            face(ring,STEEL,uv=uv)
            face([(x,y,.026-z) for x,y,z in reversed(ring)],STEEL,uv=uv[::-1])
    # The back edge of the sheath only masks the lowest portion of the blade.
    for side in (-1,1):
        tube(SILVER,[blade_point(i/30,side) for i in range(31)],.00048)
    for i in range(11):
        x=.034+i*.0027
        leaf(GOLD,(x,.011,.0184),(x+.003,.019+.002*cos(i),.0185),.00065)
    part("Carved rock crystal falcon grip with kundan gem settings")
    stepped=[(0,.020),(.004,.023),(.007,.021),(.012,.018)]
    turned(GOLD,(.067,.008,.019),stepped,(1,0,0),24,oval=.65)
    # Flattened crystal grip curves up to a carved falcon head.
    rings=[]
    for x,y,w,h in ((.078,.008,.015,.010),(.090,.008,.019,.013),(.111,.007,.020,.015),(.133,.010,.017,.014),
                       (.153,.013,.017,.012),(.171,.016,.021,.017),(.184,.014,.015,.011)):
        rings.append([(x,y+w*cos(i*tau/16),.020+h*sin(i*tau/16)) for i in range(16)])
    for a,b in zip(rings,rings[1:]):
        for i in range(16):
            j=(i+1)%16;face([a[i],b[i],b[j],a[j]],GLASS)
    # Gold spine remains visible through the crystal handle.
    rod(GOLD,(.074,.008,.020),(.181,.014,.020),.004,.006)
    ellipsoid(GILT,(.173,.014,.027),(.027,.022,.010),20)
    poly_prism(GOLD,[(.175,-.001),(.190,-.008),(.199,-.020),(.196,-.026),(.192,-.019),(.177,-.011)],.023,.008)
    stone(.180,.001,.037,.0034,.0034,.0027,RUBY)
    for feather in range(6):
        y=.011+feather*.0037
        leaf(GOLD,(.180,y,.035),(.157-.0009*feather,y+.004,.031),.0022)
    tube(DARK,[(.181,-.001,.035),(.190,-.007,.031),(.194,-.015,.031),(.196,-.022,.028)],.00055,6)
    for k in range(4):
        x=.087+k*.018
        stone(x,.013,.0335,.0042,.006,.0018,GREEN if k%2 else RUBY)
        for h in (-1,1):leaf(GOLD,(x,.012,.033),(x+.008,.012+h*.009,.031),.0015)
    for h in (-1,1):
        tube(GOLD,[(.072,.008,.019),(.070,.008+h*.022,.019),(.074,.008+h*.035,.019)],.0041,10)
        ellipsoid(RUBY,(.074,.008+h*.035,.020),(.0046,.0045,.0042),10)
    part("Wine velvet scabbard, repoussé gold chape, throat and suspension ring")
    def sheath(t,side,height=0):
        width=.003+.022*min(1,t*3)
        return (-.195+.259*t,-.077+.037*(1-t)**2+side*width,.010+height)
    for i in range(32):
        a=i/32;b=(i+1)/32
        material=GOLD if i<6 or i>25 else "Oxblood silk"
        upper=[sheath(a,-1),sheath(b,-1),sheath(b,0,.0075),sheath(a,0,.0075)]
        lower=[sheath(a,0,.0075),sheath(b,0,.0075),sheath(b,1),sheath(a,1)]
        face(upper,material,True);face(lower,material,True)
        for flank in (-1,1):
            p0=Vector(sheath(a,flank));p1=Vector(sheath(b,flank))
            face([p0,p1,p1-Vector((0,0,.007)),p0-Vector((0,0,.007))],GILT)
        face([sheath(a,1,-.007),sheath(b,1,-.007),sheath(b,-1,-.007),sheath(a,-1,-.007)],material)
    for h in (-1,1):
        tube(GOLD,[sheath(i/32,h) for i in range(33)],.0015)
    for i in range(11):
        u=.14+.075*i
        x,y,z=sheath(u,0,.008)
        stone(x,y,z,.0032,.0044,.002,RUBY if i%3 else GREEN)
        for h in (-1,1):leaf(GOLD,(x,y,.017),(x+.007,y+h*.007,.0155),.0017)
    band(GOLD,(.042,-.050,.013),.007,.005,.0014)

def ephemeris():
    part("Gilt-edged vellum sections, raised spine and two binding boards")
    # Book sits with its spine at the left. Each gathering is its own thin solid.
    w=.178;d=.234
    box("Oxblood silk",(0,0,.004),(w+.010,d+.010,.008),.003)
    for i in range(15):
        box(IVORY,(-.001,0,.009+i*.00175),(w-.003*(i%3)/3,d-.003*(i%2)/2,.00182),.001)
    box("Oxblood silk",(0,0,.039),(w+.010,d+.010,.008),.003)
    rod("Oxblood silk",(-.091,-d*.50,.023),(-.091,d*.50,.023),.016,sides=24)
    for y in (-.09,-.059,0,.057,.09):
        turned(GILT,(-.091,y,.023),[(0,.0165),(.003,.0168)],(0,1,0),24)
    face([(-w/2,-d/2,.0432),(w/2,-d/2,.0432),(w/2,d/2,.0432),(-w/2,d/2,.0432)],COVER,
         uv=[(0,0),(1,0),(1,1),(0,1)])
    # Deckled fore-edge gilt, worn through in three fine sections.
    for j in range(8):
        rod(GILT,(w/2,-.106,.011+j*.0035),(w/2,.106,.011+j*.0035),.00037, sides=5)
    part("Repoussé corner bosses, wire-set garnets and raised compass rose")
    for sx in (-1,1):
        for sy in (-1,1):
            x=sx*.081;y=sy*.108
            outline=[(x,y),(x-sx*.032,y),(x-sx*.019,y-sy*.007),(x-sx*.017,y-sy*.017),
                     (x-sx*.007,y-sy*.019),(x,y-sy*.033)]
            if sx*sy<0:outline.reverse()
            poly_prism(GILT,outline,.0433,.0017)
            x-=sx*.009;y-=sy*.009
            stone(x,y,.045,.005,.0052,.004,BLUE if sx==sy else RUBY)
            for step in range(3):
                ellipsoid(GOLD,(x-sx*(.008+.003*step),y+sy*.005,.0455),(.001,.001,.001),8)
    for r in (.0246,.0272):band(GOLD,(0,.003,.045),r,r,.0011, n=54)
    for k in range(16):
        a=tau*k/16
        radius=.038 if k%2==0 else .032
        tr=(radius*cos(a),.003+radius*sin(a),.046)
        left=(.014*cos(a+.12),.003+.014*sin(a+.12),.046)
        right=(.014*cos(a-.12),.003+.014*sin(a-.12),.046)
        ridge=(.022*cos(a),.003+.022*sin(a),.049)
        face([left,ridge,tr],GILT);face([tr,ridge,right],GOLD)
    stone(0,.003,.047,.014,.011,.006,BLUE)
    for i in range(12):
        a=tau*i/12;ellipsoid(PEARL,(.016*cos(a),.003+.014*sin(a),.047),(.00125,.00125,.00125),8)
    for y in (-.072,.072):
        box(GILT,(.082,y,.045),(.022,.012,.003),.0012)
        tube(GOLD,[(.087,y,.046),(.095,y,.043),(.095,y,.015),(.088,y,.011)],.0014)
        stone(.082,y,.047,.0025,.003,.0014,RUBY)


def smooth_curve(controls,subdivisions=5):
    pts=[Vector(p) for p in controls]
    sampled=[]
    for i in range(len(pts)-1):
        a=pts[max(0,i-1)];b=pts[i];c=pts[i+1];d=pts[min(len(pts)-1,i+2)]
        for j in range(subdivisions):
            t=j/subdivisions
            sampled.append((b*2+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t**3)*.5)
    return sampled+[pts[-1]]


def violin():
    part("Rib garland, flamed maple back and carved spruce soundboard")
    # Corners on both bouts are intentional, with a narrow inward C curve.
    upper=smooth_curve([(-.292,0),(-.282,.048),(-.255,.078),(-.221,.096),(-.180,.100),
                        (-.141,.094),(-.110,.077),(-.096,.077),(-.087,.081),(-.089,.073),
                        (-.084,.059),(-.060,.049),(-.035,.048),(-.017,.057),(-.018,.065),
                        (-.010,.071),(-.009,.066),(.006,.068),(.027,.071),(.045,.064),
                        (.058,.049),(.067,.022),(.068,0)],4)
    contour=upper+[Vector((p.x,-p.y)) for p in reversed(upper[1:-1])]
    centre=-.110
    def rim(scale,height):
        return [Vector((centre+(p.x-centre)*scale,p.y*scale,height)) for p in contour]
    def mapuv(q):return ((q.x+.292)/.361, .5+q.y/.207)
    # Flat back and a projecting edge on each side of the bent, figured ribs.
    sections=[(.965,.002),(.998,.004),(1,.006),(.981,.008),(.981,.032),(1,.034),(1,.036)]
    for j,((a,z1),(b,z2)) in enumerate(zip(sections,sections[1:])):
        left=rim(a,z1);right=rim(b,z2)
        for i in range(len(left)):
            nxt=(i+1)%len(left);coords=[left[i],right[i],right[nxt],left[nxt]]
            face(coords,WOOD,True,[(i/len(left),z1*17),(i/len(left),z2*17),((i+1)/len(left),z2*17),((i+1)/len(left),z1*17)])
    rings=[rim(1-step/12,.036+.024*(1-(1-step/12)**2)**1.7) for step in range(12)]
    for a,b in zip(rings,rings[1:]):
        for i in range(len(a)):
            j=(i+1)%len(a);pts=[a[i],b[i],b[j],a[j]]
            face(pts,SPRUCE,True,[mapuv(q) for q in pts])
    cap=Vector((centre,0,.060))
    for i,q in enumerate(rings[-1]):
        coords=[q,cap,rings[-1][(i+1)%len(rings[-1])]];face(coords,SPRUCE,True,[mapuv(p) for p in coords])
    base=rim(.966,.001)
    for i,p in enumerate(base):
        pts=[p,base[(i+1)%len(base)],Vector((centre,0,.001))]
        face(pts,WOOD,True,[mapuv(q) for q in pts])
    part("Inlaid purfling, paired sound holes and small pearl fleurons")
    for size in (.951,.963):
        path=rim(size,.036+.024*(1-size**2)**1.7+.00028)
        tube(DARK,path+[path[0]],.00053,5)
    # Determine true arch height within the irregular border at any XY.
    def arch(x,y):
        point=Vector((x-centre,y))
        if point.length<.001:return .060
        smallest=100.
        for i,a in enumerate(contour):
            b=contour[(i+1)%len(contour)];dt=b-a;b0=Vector((a.x-centre,a.y))
            det=point.x*dt.y-point.y*dt.x
            if abs(det)<.000001:continue
            t=(b0.x*dt.y-b0.y*dt.x)/det
            u=(b0.x*point.y-b0.y*point.x)/det
            if t>0 and -.0001<=u<=1.0001:smallest=min(t,smallest)
        r=min(1,1/smallest)
        return .036+.024*(1-r*r)**1.7
    for h in (-1,1):
        controls=[(-.135,h*.046),(-.139,h*.038),(-.133,h*.031),(-.124,h*.032),
                  (-.112,h*.037),(-.090,h*.042),(-.073,h*.043),(-.056,h*.036),
                  (-.050,h*.030),(-.055,h*.025),(-.062,h*.025)]
        curve=smooth_curve(controls)
        edges=[]
        for j,p in enumerate(curve):
            diff=curve[min(len(curve)-1,j+1)]-curve[max(0,j-1)]
            normal=Vector((-diff.y,diff.x)).normalized()
            width=.0012+.00155*(sin(j*pi/(len(curve)-1))**1.4)
            side=[p+normal*width,p-normal*width]
            edges.append([(q.x,q.y,arch(q.x,q.y)+.00083) for q in side])
        for prev,next_ in zip(edges,edges[1:]):
            pts=[prev[0],prev[1],next_[1],next_[0]]
            if (Vector(pts[1])-Vector(pts[0])).cross(Vector(pts[2])-Vector(pts[0])).z<0:pts.reverse()
            face(pts,DARK)
        tube(WOOD,[p[0] for p in edges],.00038,5)
        for p in (curve[0],curve[-1]):
            ellipsoid(DARK,(p.x,p.y,arch(p.x,p.y)+.0007),(.0033,.003,.00042),12)
        for sign in (-1,1):
            p=curve[len(curve)//2]
            x,y=p.x+sign*.0024,p.y+sign*h*.0042
            tube(DARK,[(p.x,p.y,arch(p.x,p.y)+.0012),(x,y,arch(x,y)+.00078)],.00062)
    for x in (-.258,.035):
        for h in (-1,1):
            pts=[(x+dx,h*.026+dy,arch(x+dx,h*.026+dy)+.0008)
                 for dx,dy in ((-.005,0),(0,-.0021),(.005,0),(0,.0021))]
            face(pts,PEARL)
    part("Maple neck, ebony fingerboard, fluted pegbox and scroll")
    for i in range(13):
        x=.041+.155*i/12;w=.019-.008*i/12;z=.046+.017*i/12
        if i:
            new=[(x,-w,z),(x,-w*.83,z-.009),(x,0,z-.016),(x,w*.83,z-.009),(x,w,z)]
            for k in range(4):face([old[k],new[k],new[k+1],old[k+1]],WOOD,True)
            old=new
        else:old=[(x,-w,z),(x,-w*.83,z-.009),(x,0,z-.016),(x,w*.83,z-.009),(x,w,z)]
    top=[]
    for x,w,z in ((-.072,.019,.068),(.198,.011,.065)):
        top.append([(x,-w,z-.003),(x,-w,z),(x,0,z+.0017),(x,w,z),(x,w,z-.003)])
    for k in range(4):face([top[0][k],top[1][k],top[1][k+1],top[0][k+1]],BLACK)
    for h in (-1,1):
        tube(WOOD,[(.196,h*.009,.060),(.216,h*.012,.067),(.247,h*.013,.062),
                  (.262,h*.010,.059)],.0045,8)
    ellipsoid(WOOD,(.259,0,.063),(.019,.016,.013),18)
    for h in (-1,1):
        scroll=[(.270+.021*(1-i/48)*cos(3*pi*i/42),h*(.009+.005*i/42),
                 .065+.014*(1-i/48)*sin(3*pi*i/42)) for i in range(43)]
        tube(WOOD,scroll,.0034,8)
        ellipsoid(GILT,(.270,h*.017,.065),(.002,.001,.002),10)
    for i in range(4):
        x=.204+i*.013;h=1 if i%2 else -1
        rod(BLACK,(x,-.015,.065),(x,h*.029,.066),.0017)
        ellipsoid(BLACK,(x,h*.030,.066),(.0055,.0023,.006),12)
    part("Carved open bridge, ebony tailpiece, nut and four strings")
    box(BLACK,(.195,0,.066),(.0035,.024,.004),.001)
    # Reverse arch creates visible air between bridge feet. Small open lobes cut into each side.
    for h in (-1,1):
        x=-.104;y=h*.012
        box(WOOD,(x,y,arch(x,y)+.0015),(.005,.009,.003),.0008)
        tube(WOOD,[(x,y,.058),(x,h*.014,.065),(x,h*.006,.071),(x,h*.011,.077)],.0023,8)
    tube(WOOD,[(-.104,.018*t/12,.077+.0018*(1-(t/12)**2)) for t in range(-12,13)],.00165)
    for h in (-1,1):rod(DARK,(-.282,h*.006,.043),(-.254,h*.005,.050),.00072)
    sections=[(-.259,.006,.048),(-.239,.010,.052),(-.213,.0125,.059),(-.194,.014,.061),(-.191,.013,.060)]
    for (x,w,z),(X,W,Z) in zip(sections,sections[1:]):
        for flank in (-1,1):
            face([(x,0,z+.001),(X,0,Z+.001),(X,flank*W,Z),(x,flank*w,z)],BLACK)
    for i in range(4):
        offset=(i-1.5)
        tube(SILVER,[(-.193,offset*.0063,.062),(-.104,offset*.0074,.0783+(.0014 if i in (1,2) else 0)),
                     (.195,offset*.0048,.0682),(.204+i*.013,offset*.002,.067)],.00030+i*.000055,6)

def clock():
    part("Portico black onyx plinth and stepped platinum cornices")
    for z,w,d,h,mat in ((.005,.207,.083,.010,BLACK),(.012,.202,.081,.004,GOLD),(.017,.198,.079,.008,BLACK),
                         (.023,.183,.069,.004,SILVER),(.028,.175,.066,.008,BLACK)):
        box(mat,(0,0,z),(w,d,h),.0028)
    # Columns flank a transparent sightline through the entire dial.
    for flank in (-1,1):
        x=flank*.087
        for z,w,d,h,mat in ((.037,.031,.047,.010,GILT),(.046,.030,.040,.008,BLACK),(.052,.028,.039,.004,SILVER),
                            (.139,.021,.031,.172,BLACK),(.228,.025,.036,.007,SILVER),(.235,.031,.042,.007,GOLD)):
            box(mat,(x,0,z),(w,d,h),.0016)
        for side in (-1,1):rod(GILT,(x+side*.008,-.016,.059),(x+side*.008,-.016,.220),.0009)
        for j in range(5):
            z=.079+j*.029
            # Inlays follow the black stone columns around the edge of the aperture.
            pts=[(x,-.0168,z-.010),(x+.0031,-.0168,z),(x,-.0168,z+.010),(x-.0031,-.0168,z)]
            face(pts,PEARL)
            stone(x,-.0171,z,.0018,.003,.0012,GREEN,True)
    for z,w,h,mat in ((.241,.205,.007,BLACK),(.246,.211,.004,GOLD),(.251,.220,.006,BLACK),
                         (.256,.204,.004,SILVER),(.260,.179,.005,BLACK)):
        box(mat,(0,0,z),(w,.051,h),.0018)
    part("Suspended rock crystal dial, reeded platinum chapter ring and diamonds")
    cx,cy,cz=PIVOT
    # Very thin enclosed optical disc. Hands are separate and leave real air behind the dial.
    turned(GLASS,(0,.000,cz),[(0,.073),(.001,.075),(.003,.075),(.004,.073)],(0,1,0),sides=72)
    for r,w,y,mat in ((.079,.0024,-.001,GILT),(.076,.001,-.0017,SILVER),
                      (.073,.0012,-.0035,GOLD),(.066,.0006,-.004,SILVER)):
        band(mat,(0,y,cz),r,r,w,True,72)
    for k in range(60):
        a=tau*k/60
        r=.071 if k%5 else .066
        rod(SILVER,(r*sin(a),-.0045,cz+r*cos(a)),(.074*sin(a),-.0045,cz+.074*cos(a)),.00055 if k%5 else .0011,sides=6)
    for i in range(12):
        a=tau*i/12
        stone(.069*sin(a),-.0048,cz+.069*cos(a),.0029,.0029,.002,CLEAR,True)
    bridge=[(-.071,0,.238),(-.046,0,.238),(-.027,0,.230),(0,0,.229),(.027,0,.230),(.046,0,.238),(.071,0,.238)]
    tube(GOLD,bridge,.0020)
    ellipsoid(GREEN,(0,-.004,.231),(.005,.003,.005),12)
    for side in (-1,1):
        tube(SILVER,[(side*.087,-.001,.156),(side*.083,-.001,.153),(side*.077,-.001,.153)],.0014)
    # Rest positions are deliberately a classic ten-past-ten. The two mesh origins
    # are moved to this bearing at export so a clock drive needs only pure rotation.
    for name,angle,length,halfwidth,zoff in (("HourHand",-60,.042,.0032,.0015),("MinuteHand",60,.058,.0024,.0027)):
        part(name)
        a=angle*pi/180
        def xy(dx,dz,depth=0):
            return (dx*cos(a)+dz*sin(a),cy-zoff-depth,cz-dx*sin(a)+dz*cos(a))
        outline=[(0,-.010),(-halfwidth,-.005),(-halfwidth*.45,.010),(-halfwidth*1.3,length*.38),
                 (-halfwidth*.60,length*.70),(0,length),(halfwidth*.60,length*.70),
                 (halfwidth*1.3,length*.38),(halfwidth*.45,.010),(halfwidth,-.005)]
        face([xy(*pt) for pt in outline[::-1]],DARK)
        for h in (-1,1):
            tube(SILVER,[xy(h*halfwidth*.45,.008,.00035),xy(h*halfwidth*1.26,length*.38,.00045),
                         xy(0,length,.00045)],.00064,6)
        for i in range(5):
            x,y,z=xy(0,.010+.006*i,.0005)
            stone(x,y,z,.00155 if i<3 else .0011,.00155 if i<3 else .0011,.0011,CLEAR,True)
    part("Central rose diamond setting")
    stone(0,cy-.004,cz,.0044,.0044,.0022,CLEAR,True)


BUILDERS={"nautilus_cup":nautilus, "falcon_dagger":dagger, "gilt_ephemeris":ephemeris,
          "touring_violin":violin, "mystery_clock":clock}

