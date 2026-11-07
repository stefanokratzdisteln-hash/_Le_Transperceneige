"""Build the metric, editable Fellbridge station props and their static game meshes.

First run ../Art/station_instruments.py with Pillow, then run Blender in background
with --factory-startup -P station_heritage.py. The fronts face Blender -Y / Unity -Z.
"""
import math
import re
import sys
import uuid
from pathlib import Path
from random import Random

import bpy
from mathutils import Matrix, Vector

ROOT = Path(__file__).resolve().parents[3]
STATION = ROOT / "Assets/Transperceneige/Pass/Station"
OUT = STATION / "Heritage"
SOURCE = ROOT / "Source/Pass/FellbridgeHeritage.blend"
sys.path.insert(0,str(ROOT/"Source/Pass/Art"))
from station_instruments import REGIONS

TAU = 2 * math.pi
materials = {}
material_assets = {}
parts = []
collection = None
group = None


def linear(c):
    return c/12.92 if c<=.04045 else ((c+.055)/1.055)**2.4


def colour(s):
    return tuple(linear(int(s[i:i+2],16)/255) for i in (0,2,4))+(1,)


def mat(name, rgb, metal, rough, game, image=None, glow=0):
    m=bpy.data.materials.new(name); m.diffuse_color=colour(rgb);m.use_nodes=True
    p=m.node_tree.nodes.get("Principled BSDF")
    p.inputs["Base Color"].default_value=m.diffuse_color
    p.inputs["Metallic"].default_value=metal
    p.inputs["Roughness"].default_value=rough
    if glow:
        p.inputs["Emission Color"].default_value=m.diffuse_color
        p.inputs["Emission Strength"].default_value=glow
    if image:
        img=bpy.data.images.load(str(image),check_existing=True)
        if not img.packed_file: img.pack()
        node=m.node_tree.nodes.new("ShaderNodeTexImage");node.image=img
        m.node_tree.links.new(node.outputs["Color"],p.inputs["Base Color"])
    materials[name]=m; material_assets[name]=game


class Part:
    """An independently selectable item in the master, batched only for the FBX."""
    def __init__(self,name):
        self.name=name;self.verts=[];self.faces=[];self.mats=[];self.smooth=[];self.uv=[]
        self.slots=[]
    def face(self, points, material, smooth=False, uvs=None):
        start=len(self.verts);ps=[Vector(p) for p in points]
        self.verts.extend(ps);self.faces.append(tuple(range(start,start+len(ps))))
        if material not in self.slots:self.slots.append(material)
        self.mats.append(self.slots.index(material));self.smooth.append(smooth)
        if uvs is None:
            n=(ps[1]-ps[0]).cross(ps[2]-ps[0])
            axis=max(range(3),key=lambda i:abs(n[i]))
            axes=[i for i in range(3) if i!=axis]
            uvs=[(v[axes[0]]*.93+.23,v[axes[1]]*.93+.39) for v in ps]
        self.uv.extend(uvs)
    def flush(self):
        if not self.faces:return
        mesh=bpy.data.meshes.new(self.name);mesh.from_pydata(self.verts,[],self.faces)
        for key in self.slots:mesh.materials.append(materials[key])
        mesh.polygons.foreach_set("material_index",self.mats)
        mesh.polygons.foreach_set("use_smooth",self.smooth)
        uv=mesh.uv_layers.new(name="Craft UV")
        uv.data.foreach_set("uv",[c for pair in self.uv for c in pair]);mesh.update()
        obj=bpy.data.objects.new(self.name,mesh);collection.objects.link(obj);parts.append(obj)


def part(name):
    global group
    if group:group.flush()
    group=Part(name)


def face(points,m,smooth=False,uv=None):group.face(points,m,smooth,uv)


def box(m, p, size, bevel=.008, angle=0):
    x,y,z=p; dx,dy,dz=[v/2 for v in size]
    r=min(bevel,dx*.49,dy*.49,dz*.63)
    def corners(w,d,h,edge):
        shape=[]
        for cx,cy,start in [(w-edge,d-edge,0),(-w+edge,d-edge,90),
                             (-w+edge,-d+edge,180),(w-edge,-d+edge,270)]:
            for k in range(3):
                a=math.radians(start+45*k)
                u=cx+edge*math.cos(a);v=cy+edge*math.sin(a)
                shape.append((x+u*math.cos(angle)-v*math.sin(angle), y+u*math.sin(angle)+v*math.cos(angle),z+h))
        return shape
    rings=[corners(dx-r*.36,dy-r*.36,-dz,r*.64),corners(dx,dy,-dz+r*.48,r),
           corners(dx,dy,dz-r*.48,r),corners(dx-r*.36,dy-r*.36,dz,r*.64)]
    face(rings[0][::-1],m)
    for l,u in zip(rings,rings[1:]):
        for i in range(len(l)):
            j=(i+1)%len(l);face([l[i],l[j],u[j],u[i]],m)
    face(rings[-1],m)


def turned(m, origin, profile, direction=(0,0,1), sides=40, oval=1):
    """Revolved machining/casting, radii listed from foot to crown."""
    q=Vector(direction).normalized().to_track_quat("Z","Y"); origin=Vector(origin)
    levels=[]
    for h,r in profile:
        ring=[origin+q@Vector((r*math.cos(i*TAU/sides),r*oval*math.sin(i*TAU/sides),h)) for i in range(sides)]
        levels.append(ring)
    face(levels[0][::-1],m)
    for k,(a,b) in enumerate(zip(levels,levels[1:])):
        for i in range(sides):
            j=(i+1)%sides;face([a[i],a[j],b[j],b[i]],m,abs(profile[k][1]-profile[k+1][1])<.001)
    face(levels[-1],m)


def rod(m, start, end, radius, top=None, sides=12):
    a=Vector(start);d=Vector(end)-a
    turned(m,a,[(0,radius),(d.length,radius if top is None else top)],d,sides)


def tube(m,coords,radius,sides=9):
    ps=[Vector(p) for p in coords];rings=[]
    for i,p in enumerate(ps):
        t=(ps[min(i+1,len(ps)-1)]-ps[max(0,i-1)]).normalized()
        ref=Vector((0,1,0)) if abs(t.y)<.94 else Vector((0,0,1))
        u=t.cross(ref).normalized();v=t.cross(u)
        rings.append([p+radius*(u*math.cos(j*TAU/sides)+v*math.sin(j*TAU/sides)) for j in range(sides)])
    face(rings[0][::-1],m)
    for a,b in zip(rings,rings[1:]):
        for i in range(sides):
            j=(i+1)%sides;face([a[i],a[j],b[j],b[i]],m,True)
    face(rings[-1],m)


def ellipsoid(m,p,sizes,sides=16):
    for j in range(1,8):
        lo=-math.pi/2+(j-1)*math.pi/7;hi=lo+math.pi/7
        def at(a,t):
            return (p[0]+sizes[0]*math.cos(t)*math.cos(a),p[1]+sizes[1]*math.cos(t)*math.sin(a),p[2]+sizes[2]*math.sin(t))
        for i in range(sides):
            a=i*TAU/sides;b=(i+1)*TAU/sides
            pts=[at(a,lo),at(b,lo),at(b,hi),at(a,hi)]
            if j==1:pts.pop(0)
            if j==7:pts.pop(2)
            face(pts,m,True)


def graphic(key, p, right, up):
    """Centre and half-axes of a printed face, viewed from cross(right,up)."""
    l,t,r,b=REGIONS[key]; uv=[((x+.5)/2048,1-(y+.5)/2048) for x,y in [(l,b),(r-1,b),(r-1,t),(l,t)]]
    p=Vector(p);u=Vector(right);v=Vector(up)
    face([p-u-v,p+u-v,p+u+v,p-u+v],"Paper and enamel print",uv=uv)


def stud(p, normal=(0,-1,0), r=.006):
    rod("Brass",p,Vector(p)+Vector(normal)*.0025,r,sides=10)


def ticket_press():
    part("Cast foot and bolted sole plate")
    turned("Graphite", (0,0,0), [(0,.072),(.003,.0762),(.009,.0762),(.014,.070),(.018,.066)])
    for a in (45,135,225,315):
        x=.063*math.cos(math.radians(a));y=.063*math.sin(math.radians(a))
        turned("Steel",(x,y,.014),[(0,.0038),(.002,.004)],sides=8)
    part("Cylindrical ribbon housing and ticket entry")
    turned("Graphite",(0,0,.016),[(0,.065),(.006,.068),(.014,.064),(.023,.061),(.128,.061),(.135,.064),(.140,.064)])
    turned("Steel",(0,0,.140),[(0,.0615),(.002,.0615)])
    # Ticket throat and the ink ribbon are recessed inside a front casting.
    box("Graphite",(0,-.059,.097),(.086,.035,.053),.007)
    box("Ebony",(0,-.0777,.099),(.070,.0018,.017),.001)
    box("Steel",(0,-.079,.089),(.081,.017,.003),.001)
    box("Brass",(0,-.078,.113),(.077,.005,.005),.0015)
    graphic("press",(0,-.0616,.055),(.026,0,0),(0,0,.0076))
    part("Removable cap, guide and palm plunger")
    turned("Graphite",(0,0,.154),[(0,.066),(.004,.067),(.008,.063),(.014,.053),(.018,.026),(.024,.025)])
    turned("Brass",(0,0,.176),[(0,.022),(.004,.024),(.007,.024),(.009,.020)])
    turned("Steel",(0,0,.185),[(0,.015),(.032,.015),(.035,.021)])
    turned("Ebony",(0,0,.217),[(0,.022),(.005,.031),(.011,.044),(.018,.047),(.025,.044),(.033,.030),(.037,.014)])
    part("Numbered third class card")
    box("Paper",(.008,-.090,.0915),(.055,.030,.0009),.00025)
    graphic("ticket",(.008,-.090,.0921),(.0268,0,0),(0,.0143,0))


def scales():
    part("Portable steelyard base and platform")
    for x in (-.27,.27):
        for y in (-.22,.21):
            turned("Graphite",(x,y,0),[(0,.040),(.012,.041),(.021,.036),(.072,.031),(.078,.028)],sides=16)
    box("Enamel",(0,.06,.096),(.66,.74,.072),.031)
    box("Graphite",(0,-.066,.150),(.65,.49,.055),.024)
    for i in range(7):
        y=-.275+i*.068
        box("Oak",(0,y,.186),(.619,.063,.023),.004)
        for x in (-.277,.277):stud((x,y,.198),(0,0,1),.0045)
    part("Ribbed scale column")
    turned("Enamel",(0,.297,.127),[(0,.121),(.019,.122),(.037,.104),(.047,.081),(.115,.060),
                                   (.173,.055),(.180,.061),(.193,.062),(.203,.049),(.694,.041),
                                   (.708,.060),(.723,.060),(.742,.076)],sides=28)
    for k in range(10):
        a=k*TAU/10; rod("Enamel",(.051*math.cos(a),.297+.051*math.sin(a),.343),
                          (.043*math.cos(a),.297+.043*math.sin(a),.805),.0036,.0027)
    turned("Brass",(0,.297,.302),[(0,.061),(.007,.061),(.009,.058)],sides=32)
    part("Beam, pivots and sliding poise")
    box("Enamel",(0,.294,.905),(.28,.105,.104),.020)
    for y in [.240,.34]:
        box("Graphite",(-.21,y,.965),(.052,.026,.14),.010)
        rod("Brass",(-.21,y-.02,.998),(-.21,y+.021,.998),.019,sides=18)
    box("Brass",(0,.260,1.010),(.68,.035,.052),.006)
    graphic("beam",(0,.2418,1.011),(.310,0,0),(0,0,.022))
    rod("Steel",(-.35,.264,1.055),(.35,.264,1.055),.006)
    for x in (-.341,.341):
        box("Enamel",(x,.268,1.037),(.021,.049,.075),.007)
    box("Graphite",(.077,.262,1.008),(.051,.066,.073),.009)
    box("Brass",(.077,.2285,1.042),(.011,.004,.021),.001)
    tube("Graphite",[(-.207,.263,.995),(-.27,.263,.977),(-.323,.263,.941),(-.323,.263,.889)],.006)
    turned("Graphite",(-.323,.263,.846),[(0,.043),(.012,.054),(.032,.054),(.041,.036),(.044,.012)],sides=24)
    box("Enamel",(0,.233,.905),(.246,.016,.073),.007)
    graphic("scale",(0,.2245,.905),(.114,0,0),(0,0,.031))
    for x in (-.119,.119):stud((x,.223,.905),r=.003)
    part("Stamped quarter weights")
    for i in range(3):
        x=.215-.082*i
        turned("Graphite",(x,.31,.136),[(0,.033),(.007,.037),(.030+.006*i,.034),(.037+.006*i,.026),
                                          (.039+.006*i,.010),(.048+.006*i,.010),(.05+.006*i,.016),(.055+.006*i,.016)],sides=20)


def fireplace():
    part("Slate hearth and stepped surround")
    box("Slate",(0,-.09,.032),(1.30,.49,.064),.016)
    box("Graphite",(0,.091,.516),(.93,.10,.89),.024)
    box("Ebony",(0,.028,.392),(.477,.070,.572),.016)
    for s in (-1,1):
        x=s*.352
        box("Graphite",(x,.017,.488),(.18,.161,.821),.016)
        box("Graphite",(s*.435,.008,.496),(.085,.176,.866),.011)
        for z,sz in [( .105,.072),(.168,.039),(.788,.046),(.844,.068)]:
            box("Graphite",(s*.435,-.004,z),(.111,.211,sz),.007)
        for k in range(4):
            rod("Steel",(s*.435+(k-1.5)*.015,-.082,.195),
                (s*.435+(k-1.5)*.015,-.082,.754),.0054)
        for k in range(5):
            z=.219+k*.127
            box("Oak",(s*.312,-.078,z),(.13,.023,.126),.003)
            graphic("tile",(s*.312,-.0900,z),(.059,0,0),(0,0,.061))
    box("Graphite",(0,-.010,.864),(.985,.206,.085),.018)
    box("Steel",(0,-.005,.909),(1.01,.216,.018),.007)
    box("Graphite",(0,-.004,.948),(1.045,.235,.069),.021)
    box("Ebony",(0,-.005,.986),(1.063,.250,.021),.009)
    part("Arched fireback and raised cast ornament")
    for i in range(12):
        a=math.pi*i/12;b=math.pi*(i+1)/12
        def arch(t,rx,rz,y):return (rx*math.cos(t),y,.555+rz*math.sin(t))
        face([arch(a,.242,.214,-.084),arch(b,.242,.214,-.084),
              arch(b,.214,.159,-.084),arch(a,.214,.159,-.084)],"Graphite")
        face([arch(a,.214,.159,-.084),arch(b,.214,.159,-.084),
              arch(b,.196,.154,-.025),arch(a,.196,.154,-.025)],"Ebony")
    for s in (-1,1):
        for offset in (.03,.142,.255):
            coords=[]
            for i in range(16):
                a=i*.19
                coords.append((s*(offset+.026+.030*math.cos(a)), -.119,
                               .866+.016*math.sin(2*a)))
            tube("Steel",coords,.003)
    box("Ebony",(0,-.117,.859),(.205,.009,.054),.005)
    graphic("fire",(0,-.122,.859),(.097,0,0),(0,0,.024))
    part("Bowed grate bars and warming coals")
    box("Graphite",(0,-.106,.160),(.475,.217,.060),.009)
    box("Ebony",(0,-.132,.105),(.391,.167,.055),.010)
    for x in (-.14,.14):stud((x,-.213,.114),r=.009)
    for z in (.213,.259,.305):
        coords=[(-.237+i*.474/16,-.123-.099*math.sin(i*math.pi/16),z) for i in range(17)]
        tube("Graphite",coords,.012)
    for x in [-.222,-.121,0,.121,.222]:
        y=-.126-.093*(1-abs(x)/.23)
        rod("Graphite",(x,y,.184),(x,y,.335),.0096)
        if abs(x)>.2:ellipsoid("Brass",(x,y,.347),(.015,.015,.021))
    rng=Random(461)
    for j in range(3):
        for i in range(6):
            p=(-.159+i*.059+rng.uniform(-.012,.012),-.143+j*.045+rng.uniform(-.012,.012),.248+rng.uniform(0,.048))
            ellipsoid("Ember" if (i+j)%4==0 else "Coal",p,(.040,.031,.035),8)
    part("Riveted coal scuttle, bail and poker")
    cx=.560;cy=-.12
    rings=[[(cx+rx*math.cos(i*TAU/30),cy+ry*math.sin(i*TAU/30),h+tilt*math.sin(i*TAU/30)) for i in range(30)]
           for rx,ry,h,tilt in [(.066,.071,.069,0),(.095,.111,.081,0),(.115,.132,.206,-.025),(.120,.139,.233,-.032)]]
    for a,b in zip(rings,rings[1:]):
        for i in range(30):
            j=(i+1)%30;face([a[i],a[j],b[j],b[i]],"Enamel",True)
    tube("Steel",rings[-1]+[rings[-1][0]],.0042)
    for i in range(10):
        a=i*TAU/10
        ellipsoid("Coal",(cx+.063*math.cos(a),cy+.071*math.sin(a),.217-.016*math.sin(a)),
                  (.039,.043,.036),7)
    tube("Steel",[(cx+.127*math.cos(i*math.pi/20),cy,.20+.127*math.sin(i*math.pi/20)) for i in range(21)],.006)
    rod("Steel",(-.49,.00,.095),(-.48,.045,.657),.006)
    turned("Brass",(-.48,.045,.651),[(0,.008),(.014,.016),(.063,.011),(.073,.016)],sides=16)


def drawer(x,z,w,h):
    # Recessed centre panel and a real drop handle with two screwed backplates.
    box("Walnut",(x,-.359,z),(w,.045,h),.008)
    box("Dark wood",(x,-.383,z),(w-.036,.013,h-.028),.008)
    box("Walnut",(x,-.391,z),(w-.051,.011,h-.045),.007)
    for s in (-1,1):
        xx=x+s*.049
        rod("Brass",(xx,-.398,z+.012),(xx,-.407,z+.012),.012,sides=18)
        stud((xx,-.408,z+.012),r=.003)
    tube("Brass",[(x-.05,-.407,z+.012),(x-.054,-.426,z+.007),(x-.042,-.432,z-.014),
                  (x-.021,-.433,z-.020),(x+.021,-.433,z-.020),(x+.042,-.432,z-.014),
                  (x+.054,-.426,z+.007),(x+.05,-.407,z+.012)],.0045)


def desk():
    part("Ogee moulded top and tooled leather writing surface")
    box("Dark wood",(0,0,.751),(1.57,.80,.052),.026)
    box("Walnut",(0,0,.787),(1.60,.822,.042),.019)
    box("Brass",(0,0,.808),(1.461,.680,.0032),.009)
    box("Leather",(0,0,.810),(1.452,.672,.0038),.009)
    for s in (-1,1):
        part(("Left" if s<0 else "Right")+" pedestal and recessed field panels")
        x=s*.549
        box("Dark wood",(x,0,.400),(.433,.727,.653),.015)
        for h,dx,dy,dh in [(.077,.46,.75,.061),(.122,.448,.739,.027),(.676,.443,.73,.025)]:
            box("Walnut",(x,0,h),(dx,dy,dh),.010)
        for side in (-1,1):
            for front in (-1,1):
                xx=x+side*.161;yy=front*.278
                turned("Walnut",(xx,yy,0),[(0,.036),(.012,.041),(.025,.055),(.046,.059),(.062,.044)],sides=20)
            xx=x+side*.219
            box("Walnut",(xx,0,.416),(.017,.597,.476),.009)
            box("Dark wood",(xx+side*.010,0,.416),(.010,.541,.429),.007)
            box("Walnut",(xx+side*.018,0,.416),(.012,.507,.391),.008)
        for i in range(3):drawer(x,.217+i*.176,.395,.161)
        box("Walnut",(x,.363,.397),(.379,.019,.515),.011)
        box("Dark wood",(x,.374,.397),(.340,.012,.475),.010)
        box("Walnut",(x,.382,.397),(.311,.011,.447),.008)
    part("Knee rail and centre drawer")
    box("Walnut",(0,.307,.499),(.669,.062,.458),.009)
    box("Walnut",(0,-.286,.675),(.651,.132,.10),.008)
    drawer(0,.676,.623,.104)
    part("Writing chair with turned legs and bentwood back")
    cy=-.724
    for s in (-1,1):
        for f in (-1,1):
            x=s*.195;y=cy+f*.184
            leg=[(0,.013),(.014,.021),(.031,.020),(.057,.014),(.096,.019),(.116,.025),(.131,.018),
                 (.283,.022),(.304,.028),(.322,.021),(.403,.026)]
            turned("Walnut",(x+s*.018,y+f*.019,0),leg,(-s*.038,-f*.043,1),sides=16)
        rod("Walnut",(s*.195,cy-.175,.186),(s*.195,cy+.174,.183),.012)
    rod("Walnut",(-.196,cy,.193),(.196,cy,.193),.012)
    box("Walnut",(0,cy,.423),(.474,.452,.052),.022)
    box("Seat hide",(0,cy,.462),(.450,.429,.051),.027)
    for s in (-1,1):
        tube("Walnut",[(s*.197,cy-.165,.43),(s*.198,cy-.194,.60),(s*.196,cy-.243,.831),(s*.173,cy-.252,.882)],.020)
    for i in range(5):
        x=(i-2)*.057
        tube("Walnut",[(x,cy-.196,.485),(x*.93,cy-.213,.613),(x*.97,cy-.240,.787),(x*1.04,cy-.245,.840)],.012)
    box("Walnut",(0,cy-.246,.858),(.373,.051,.088),.020)
    part("Green opal desk lamp with turned base")
    x=-.583;y=.143;h=.811
    turned("Brass",(x,y,h),[(0,.085),(.007,.086),(.014,.080),(.020,.067),(.026,.062),(.032,.043),(.035,.028),
                           (.049,.026),(.059,.019),(.062,.014),(.257,.012),(.267,.025),(.287,.023),(.294,.012)],sides=32,oval=.81)
    for side in (-1,1):
        tube("Brass",[(x,y,h+.259),(x+side*.087,y,h+.259),(x+side*.131,y-.016,h+.279),
                      (x+side*.131,y-.044,h+.312)],.006)
        rod("Brass",(x+side*.125,y-.046,h+.311),(x+side*.143,y-.046,h+.311),.012)
    # Elliptical trough with a fitted warm underside and rolled brass lip.
    shade_x=.127; shade_y=.074; shade_z=.076
    for i in range(20):
        a=i*math.pi/20;b=(i+1)*math.pi/20
        def rim(t,s):
            return (x+s*shade_x,y-.047+shade_y*math.cos(t),h+.296+shade_z*math.sin(t))
        face([rim(a,-1),rim(b,-1),rim(b,1),rim(a,1)],"Lamp green")
        for s in (-1,1):
            tri=[rim(a,s),rim(b,s),(x+s*shade_x,y-.047,h+.296)]
            face(tri if s<0 else tri[::-1],"Lamp green")
    box("Lamp glass",(x,y-.047,h+.294),(.244,.134,.006),.002)
    for side in (-1,1):
        rod("Brass",(x-shade_x,y-.047+side*shade_y,h+.296),(x+shade_x,y-.047+side*shade_y,h+.296),.0031)
    for i in range(13):ellipsoid("Brass",(x+.111,y-.04,h+.285-i*.006),(.002,.002,.003),7)
    part("Night duty register and working timetable")
    # Pages sit on their own bindings rather than on the leather inset.
    box("Dark wood",(-.077,-.110,.820),(.449,.290,.013),.006)
    box("Paper",(-.077,-.110,.829),(.438,.280,.010),.004)
    graphic("ledger",(-.077,-.110,.835),(.215,0,0),(0,.135,0))
    box("Cloth",(.230,.167,.823),(.190,.244,.023),.004)
    box("Paper",(.230,.167,.836),(.184,.238,.004),.002)
    graphic("timetable",(.230,.167,.839),(.089,0,0),(0,.114,0))
    part("Telegraph key, sounder and binding posts")
    x=.569; y=-.008
    box("Dark wood",(x,y,.832),(.258,.282,.043),.010)
    box("Ebony",(x,y,.855),(.246,.272,.010),.004)
    for side in (-1,1):
        box("Brass",(x+side*.041,y-.045,.878),(.016,.032,.046),.004)
        turned("Brass",(x+side*.093,y+.097,.860),[(0,.015),(.007,.016),(.011,.010),(.026,.012),(.031,.012)],sides=16)
    rod("Steel",(x-.059,y-.045,.892),(x+.059,y-.045,.892),.010)
    tube("Brass",[(x,y+.058,.884),(x,y+.025,.895),(x,y-.080,.895),(x,y-.112,.885)],.009)
    turned("Ebony",(x,y-.109,.886),[(0,.009),(.012,.009),(.017,.021),(.022,.023),(.026,.020)],sides=24)
    turned("Brass",(x+.037,y+.041,.860),[(0,.006),(.023,.005),(.025,.021),(.036,.037),(.046,.043),
                                           (.057,.039),(.071,.022),(.073,.010)],sides=28)
    for off in (-.008,.008):
        tube("Ebony",[(x-.09+off,y+.11,.877),(x-.09+off,y+.147,.86),(x-.09+off,y+.171,.824),
                      (x-.133+off,y+.242,.816),(x-.140+off,y+.39,.808),(x-.13+off,y+.41,.749)],.0026)
    graphic("desk",(x,.092,.861),(.044,0,0),(0,.009,0))
    part("Writing implements and ink bottle")
    x=-.343;y=.128
    turned("Ebony",(x,y,.812),[(0,.023),(.004,.027),(.043,.026),(.047,.020),(.056,.015)],sides=8)
    turned("Brass",(x,y,.867),[(0,.017),(.008,.017),(.012,.014)],sides=16)
    rod("Ebony",(-.354,-.228,.818),(-.398,-.071,.818),.0038,.0023)
    rod("Brass",(-.354,-.227,.818),(-.350,-.241,.818),.0032,.0004)


def stable_guid(label):
    return uuid.uuid5(uuid.NAMESPACE_URL, "winterbourne/heritage/"+label).hex


def read_guid(meta):
    return re.search(r"^guid: (\w+)",meta.read_text(),re.MULTILINE)[1]


def copied_meta(source, target, label):
    s=source.read_text()
    target.write_text(re.sub(r"^guid: \w+", "guid: "+stable_guid(label),s,count=1,flags=re.M),encoding="utf-8")


def finish_material(name,hexcolour,metal,rough,texture=False,emission=None):
    base=STATION/("Lantern glass.mat" if emission else "Station timetable ink.mat")
    data=base.read_text(encoding="utf-8").replace("m_Name: "+base.stem+"\n","m_Name: "+name+"\n")
    guid=stable_guid("Stationery.png")
    data=re.sub(r"(m_Texture: )\{fileID: 2800000, guid: \w+, type: 3}",r"\g<1>"+(
        "{fileID: 2800000, guid: "+guid+", type: 3}" if texture else "{fileID: 0}"),data)
    def fmt(h):
        ch=[int(h[i:i+2],16)/255 for i in (0,2,4)]
        return "{r: %.6f, g: %.6f, b: %.6f, a: 1}"%tuple(ch)
    for token,value in [("_Metallic",str(metal)),("_Smoothness",str(round(1-rough,3))),
                        ("_BaseColor",fmt(hexcolour)),("_Color",fmt(hexcolour))]:
        data=re.sub(r"(- "+token+r": )[^\r\n]*",r"\g<1>"+value,data)
    if emission:
        data=re.sub(r"(- _EmissionColor: )[^\r\n]*",r"\g<1>"+fmt(emission),data)
    path=OUT/(name+".mat");path.write_text(data,encoding="utf-8")
    copied_meta(base.with_suffix(".mat.meta"),path.with_suffix(".mat.meta"),path.name)
    return path


def setup_palette():
    OUT.mkdir(parents=True,exist_ok=True)
    copied_meta(STATION.with_suffix(".meta"),OUT.with_suffix(".meta"),"folder")
    copied_meta(STATION/"StationHouseEnamel.png.meta",OUT/"Stationery.png.meta","Stationery.png")
    for name,shade,metal,rough in [
        ("Graphite","353b3a",.38,.54),("Steel","72736d",.55,.40),("Ebony","232423",.18,.41),
        ("Coal","29292b",.15,.72),("Leather","38534b",.02,.62),("Seat hide","693f32",.04,.49),
        ("Lamp green","30594a",.19,.27)]:
        dest=finish_material(name,shade,metal,rough)
        mat(name,shade,metal,rough,dest)
    for key,name,h,metal,rough in [
        ("Brass","Ticket brass","bc914f",.61,.38),("Enamel","Painted iron","40574c",.24,.57),
        ("Slate","Keeper's slate","585551",.04,.75),("Paper","Unbleached silk","d4c4a0",0,.84),
        ("Oak","Waiting room oak","825b3b",.02,.66),("Cloth","Webbing","8a7650",0,.8),
        ("Lamp glass","Lantern glass","ffd9a8",0,.4)]:
        mat(key,h,metal,rough,STATION/(name+".mat"),glow=1.2 if key=="Lamp glass" else 0)
    mat("Walnut","70503a",0,.55,STATION/"Reading room walnut.mat",STATION/"WeatheredWalnut.png")
    mat("Dark wood","453526",0,.68,STATION/"Bookcase shadow.mat",STATION/"WeatheredWalnut.png")
    mat("Paper and enamel print","ffffff",.02,.66,finish_material("Stationery print","ffffff",.02,.66,True),OUT/"Stationery.png")
    mat("Ember","ac5735",.02,.83,finish_material("Banked coals","9c4427",.02,.83,emission="ff6724"),glow=.7)


def remap_fbx(path,used):
    original=(ROOT/"Assets/Transperceneige/Car07/Car07.fbx.meta").read_text()
    maps=["  externalObjects:"]
    for name in sorted(used):
        asset=material_assets[name]
        guid=read_guid(asset.with_suffix(".mat.meta"))
        maps.extend(["  - first:","      type: UnityEngine:Material","      assembly: UnityEngine.CoreModule",
                     "      name: "+name,"    second: {fileID: 2100000, guid: "+guid+", type: 2}"])
    original=re.sub(r"  externalObjects:[\s\S]*?(?=  materials:)", "\n".join(maps)+"\n", original)
    original=re.sub(r"^guid: \w+","guid: "+stable_guid(path.name),original,flags=re.M)
    path.with_suffix(".fbx.meta").write_text(original,encoding="utf-8")


def export_group(name,offset):
    global group
    group.flush();group=None
    # Weld shared positions for physically smooth turned metal and remove isolated
    # duplicate vertices, preserving the UVs for faces carrying printed artwork.
    import bmesh
    for obj in parts:
        bm=bmesh.new();bm.from_mesh(obj.data)
        bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.000008)
        bm.to_mesh(obj.data);bm.free();obj.data.update()
    bpy.ops.object.select_all(action="DESELECT")
    copies=[]
    for obj in parts:
        copy=obj.copy();copy.data=obj.data.copy()
        bpy.context.scene.collection.objects.link(copy);copies.append(copy);copy.select_set(True)
    bpy.context.view_layer.objects.active=copies[0];bpy.ops.object.join()
    merged=bpy.context.active_object;merged.name=name+" joinery"
    root=bpy.data.objects.new(name,None);bpy.context.scene.collection.objects.link(root)
    merged.parent=root;root.select_set(True)
    dest=OUT/(name+".fbx")
    bpy.ops.export_scene.fbx(filepath=str(dest),use_selection=True,object_types={"MESH","EMPTY"},
        axis_forward="-Z",axis_up="Y",use_triangles=True,apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",bake_anim=False,add_leaf_bones=False,
        path_mode="STRIP",mesh_smooth_type="FACE",use_custom_props=False)
    remap_fbx(dest,{m.name for obj in parts for m in obj.data.materials})
    print("FELLBRIDGE EXPORTED",name,len(parts),"editable assemblies",len(merged.data.vertices),"vertices")
    bpy.data.objects.remove(merged,do_unlink=True);bpy.data.objects.remove(root,do_unlink=True)
    base=bpy.data.objects.new(name,None);collection.objects.link(base)
    base.empty_display_type="PLAIN_AXES";base.empty_display_size=.04
    for obj in parts:obj.parent=base
    base.location=offset


def main():
    global collection,parts
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene=bpy.context.scene;scene.name="Fellbridge | Winter 1935"
    scene.unit_settings.system="METRIC";scene.unit_settings.length_unit="METERS"
    bpy.context.preferences.filepaths.save_version=0
    setup_palette()
    for name,make,offset,tag in [
        ("TicketDatingPress",ticket_press,(-.95,-.36,0),"COLOR_04"),
        ("ParcelsScale",scales,(-1.59,.16,0),"COLOR_05"),
        ("WaitingRoomGrate",fireplace,(-.30,1.05,0),"COLOR_02"),
        ("StationmasterDesk",desk,(1.22,.25,0),"COLOR_03")]:
        parts=[];collection=bpy.data.collections.new(name);scene.collection.children.link(collection)
        collection.color_tag=tag;make();export_group(name,offset)
    bpy.ops.object.select_all(action="DESELECT")
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=="VIEW_3D":
                view=area.spaces.active;view.region_3d.view_distance=5
                view.region_3d.view_location=(0,.18,.49)
                view.region_3d.view_rotation=Vector((-.33,.86,-.36)).to_track_quat("-Z","Y")
                view.shading.color_type="MATERIAL";view.shading.show_cavity=True;view.shading.cavity_type="WORLD"
    bpy.ops.outliner.orphans_purge(do_recursive=True)
    for img in bpy.data.images:
        if img.packed_file:img.filepath="//../../"+(OUT/"Stationery.png" if img.name=="Stationery.png" else STATION/"WeatheredWalnut.png").relative_to(ROOT).as_posix()
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE),compress=True)


if __name__=="__main__":main()
