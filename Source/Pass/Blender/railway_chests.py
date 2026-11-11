"""Editable railway crates, treasury cabinet and riding gloves. Mesh axes are X across, Y back, Z up."""
import math
import sys
import uuid
from pathlib import Path

import bpy
import bmesh
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
import station_heritage as shop
from station_heritage import box, rod, tube, ellipsoid, turned, part

ROOT=Path(__file__).resolve().parents[3]
TARGET=ROOT/"Assets/Transperceneige/Pass/Cases"
STATION=shop.STATION
TAU=math.tau


def cornice(width,depth,z,layers):
    for height,depth_change,finish in layers:
        box(finish,(0,0,z+height/2),(width+depth_change,depth+depth_change,height),.009)
        z+=height


def cartouche(x,y,z,rx,rz,metal,thickness=.002):
    points=[(x+rx*math.cos(i*TAU/56),y,z+rz*math.sin(i*TAU/56)) for i in range(57)]
    tube(metal,points,thickness)


def screw(x,y,z,size=.004):
    turned("Brass",(x,y,z),[(0,size),(.0015,size),(.0026,size*.70)],(0,-1,0),12)
    rod("Iron",(x-size*.55,y-.0028,z),(x+size*.55,y-.0028,z),size*.12,sides=7)


def badge(x,y,z,scale=1):
    """A four-point North Fell wayfinder in gold on midnight blue enamel."""
    turned("Gilt",(x,y,z),[(0,.039*scale),(.002*scale,.041*scale),(.003*scale,.037*scale)],(0,-1,0),40)
    turned("Enamel",(x,y-.003*scale,z),[(0,.035*scale),(.002*scale,.035*scale)],(0,-1,0),40)
    for j in range(8):
        a=j*TAU/8; length=(.029 if j%2==0 else .017)*scale
        def p(r,t=0,d=0):
            return (x+math.sin(a)*r+math.cos(a)*t,y-(.006*scale+d),z+math.cos(a)*r-math.sin(a)*t)
        shop.face([p(.003*scale),p(.010*scale,-.004*scale),p(length),p(.011*scale,0,.002*scale)][::-1],"Gilt")
        shop.face([p(.003*scale),p(.011*scale,0,.002*scale),p(length),p(.010*scale,.004*scale)][::-1],"Brass")
    ellipsoid("Gilt",(x,y-.009*scale,z),(.005*scale,.003*scale,.005*scale))


def carrying_handle(x,side,z):
    for y in (-.061,.061):
        ellipsoid("Brass",(x,y,z),(.006,.014,.017))
        rod("Iron",(x+side*.003,y,z),(x+side*.016,y,z),.0055)
    pts=[(x+side*.014,-.061,z),(x+side*.020,-.061,z-.030),(x+side*.018,-.044,z-.048),
         (x+side*.018,.044,z-.048),(x+side*.020,.061,z-.030),(x+side*.014,.061,z)]
    tube("Iron",pts,.0055)
    rod("Leather",(x+side*.018,-.039,z-.048),(x+side*.018,.039,z-.048),.009)


def slab(poly,z0,z1,finish):
    points=[(x,y,z0) for x,y in poly]
    shop.face(points[::-1],finish)
    shop.face([(x,y,z1) for x,y in poly],finish)
    for i,(x,y) in enumerate(poly):
        u,v=poly[(i+1)%len(poly)];shop.face([(x,y,z0),(u,v,z0),(u,v,z1),(x,y,z1)],finish)


def trunk_section(x0,x1,half_depth,z0,arch,finish):
    # An arched travelling-trunk stave section, kept thin to leave a lined hollow interior.
    for i in range(16):
        y0=-half_depth+2*half_depth*i/16;y1=y0+half_depth/8
        a=z0+arch*(1-(y0/half_depth)**2);b=z0+arch*(1-(y1/half_depth)**2)
        shop.face([(x0,y0,a),(x1,y0,a),(x1,y1,b),(x0,y1,b)],finish)
        shop.face([(x0,y1,b-.014),(x1,y1,b-.014),(x1,y0,a-.014),(x0,y0,a-.014)],"Oxblood wood")
        for x,sign in ((x0,-1),(x1,1)):
            face=[(x,y0,a-.014),(x,y1,b-.014),(x,y1,b),(x,y0,a)]
            shop.face(face if sign>0 else face[::-1],finish)
    for y,sign in ((-half_depth,-1),(half_depth,1)):
        face=[(x0,y,z0-.014),(x1,y,z0-.014),(x1,y,z0),(x0,y,z0)]
        shop.face(face if sign==-1 else face[::-1],finish)


def parcel():
    part("ParcelBody")
    box("Oxblood wood",(0,0,.040),(.667,.403,.062),.007)
    box("Velvet",(0,0,.069),(.623,.363,.013),.004)
    for f in (-1,1):
        for i in range(3):
            box("Oxblood wood",(0,f*.192,.103+i*.083),(.651,.020,.083),.002)
            box("Oxblood wood",(f*.321,0,.103+i*.083),(.019,.380,.083),.002)
        for x in (-.308,.308):
            box("Bridle",(x,f*.200,.163),(.043,.011,.304),.005)
            for z in (.040,.258):screw(x,f*.209,z,.0038)
        carrying_handle(f*.325,f,.184)
    for x in (-.184,.184):
        box("Bridle",(x,-.202,.182),(.035,.006,.245),.003)
        for z in (.081,.136,.178):screw(x,-.208,z,.0033)
        rod("Brass",(x-.027,.197,.308),(x+.027,.197,.308),.007,sides=18)
    part("ParcelLid")
    z=.323;d=.201;arch=.131
    for i in range(5):
        x=-.333+i*.1332
        trunk_section(x,x+.1329,d,z,arch,"Oxblood wood")
    for sign in (-1,1):
        x=sign*.331
        edge=[(x,-d,z-.012)]+[(x,-d+d*i/8,z+arch*(1-(-1+i/8)**2)) for i in range(17)]+[(x,d,z-.012)]
        shop.face(edge if sign==1 else edge[::-1],"Oxblood wood")
        trunk_section(min(x,x-sign*.035),max(x,x-sign*.035),d+.002,z+.005,arch,"Bridle")
    for x in (-.184,.184):
        trunk_section(x-.0175,x+.0175,d+.003,z+.007,arch,"Bridle")
        for y in (-.177,-.119,.116,.177):
            ellipsoid("Brass",(x,y,z+.008+arch*(1-(y/.204)**2)),(.0035,.0035,.0025),12)
    for side in (-1,1):
        part("ParcelLatch" if side==1 else "ParcelLeftLatch")
        x=side*.184
        box("Bridle",(x,-.210,.306),(.038,.012,.074),.006)
        for dx in (-.018,.018):box("Brass",(x+dx,-.219,.312),(.004,.005,.029),.002)
        for zz in (.297,.327):rod("Brass",(x-.018,-.219,zz),(x+.018,-.219,zz),.0026)
        rod("Brass",(x,-.220,.303),(x,-.222,.328),.0024)
        rod("Brass",(x-.022,-.208,.342),(x+.022,-.208,.342),.0048)
    part("ParcelTicket")
    # A small owner's impressed cartouche on the end. The plain front and two fastenings read at a distance.
    box("Leather",(.327,-.095,.182),(.008,.045,.048),.003)


def case_outline(half_width,back,wing_front,centre_front,cut,centre=.213):
    return [(-half_width+cut,wing_front),(-centre-.025,wing_front),(-centre,centre_front),(centre,centre_front),
        (centre+.025,wing_front),(half_width-cut,wing_front),(half_width,wing_front+cut),
        (half_width,back-cut*.5),(half_width-cut*.5,back),(-half_width+cut*.5,back),(-half_width,back-cut*.5),(-half_width,wing_front+cut)]


def front_bead(x,z,size,y):
    ellipsoid("Gilt",(x,y,z),(size,size*.67,size),9)


def scroll_xz(x,y,z,u,v,metal="Gilt",radius=.0025,turn=1.55):
    pts=[]
    for i in range(45):
        q=i/44;t=q*TAU*turn;f=.13+.87*q
        pts.append((x+u*f*math.cos(t),y,z+v*f*math.sin(t)))
    tube(metal,pts,radius,7)


def sprig(x,y,z,side,up,scale=1):
    tube("Brass",[(x,y,z),(x+side*.017*scale,y,z+up*.008*scale),
        (x+side*.040*scale,y,z+up*.040*scale),(x+side*.049*scale,y,z+up*.077*scale)],.0038*scale,9)
    for i in range(3):
        for h in (-1,1):
            a=Vector((x+side*(.014+i*.011)*scale,y,z+up*(.012+i*.018)*scale))
            b=a+Vector((side*(.015+h*.013)*scale,-.0016*scale,up*(.021-h*.009)*scale))
            mid=(a+b)*.5
            dx=(b.x-a.x);dz=(b.z-a.z);ln=math.hypot(dx,dz)
            perp=Vector((-dz/ln*.0068*scale,0,dx/ln*.0068*scale))
            crown=mid+Vector((0,-.005*scale,0))
            for tri in ([a,mid+perp,crown],[a,crown,mid-perp],[b,crown,mid+perp],[b,mid-perp,crown]):
                seq=[tuple(t) for t in tri]
                if (tri[1]-tri[0]).cross(tri[2]-tri[0]).y>0:seq.reverse()
                shop.face(seq,"Gilt")
            tube("Brass",[a,crown,b],.0013*scale,7)


def treasury_leaf(side):
    part("TreasuryLeft" if side<0 else "TreasuryRight")
    cx=side*.379
    box("Walnut",(cx,-.202,.351),(.283,.031,.571),.006)
    box("Clouded stone",(cx,-.220,.351),(.278,.011,.563),.003)
    box("Clouded stone",(cx,-.183,.351),(.263,.006,.541),.004)
    # Keep the tall inset panel simple. Its upper outside corner is cut off on the diagonal.
    outline=[(-.103,.092),(.102,.092),(.102,.488),(.029,.58),(-.103,.58),(-.103,.092)]
    for delta,finish,r in ((0,"Brass",.0062),(.014,"Gilt",.0022)):
        pts=[]
        for dx,z in outline:
            pts.append((cx+side*(dx*(1-delta*3)), -.229-delta*.08,.336+(z-.336)*(1-delta*2.9)))
        tube(finish,pts,r,9)
    for y in (-.232,):
        sprig(cx+side*.078,y,.598,-side,-1,.59)
        scroll_xz(cx+side*.079,y,.556,.015*side,.013,"Brass",.0022)
    knob=cx-side*.114
    for z in (.297,.364):
        turned("Brass",(knob,-.226,z),[(0,.009),(.006,.009),(.009,.006)],(0,-1,0),16)
    tube("Brass",[(knob,-.236,.297),(knob-side*.009,-.247,.310),(knob-side*.009,-.248,.350),(knob,-.235,.364)],.0048)
    for zz in (.116,.552):
        rod("Brass",(side*.523,-.207,zz-.019),(side*.523,-.207,zz+.019),.0062,sides=18)


def treasury():
    part("TreasuryCarcass")
    for a,b,delta,finish in ((.005,.021,0,"Iron"),(.020,.036,.006,"Brass"),(.034,.057,.002,"Gilt"),(.055,.068,-.004,"Clouded stone")):
        slab(case_outline(.548+delta,.245+delta,-.222-delta,-.264-delta,.053),a,b,finish)
    box("Walnut",(0,0,.065),(1.06,.47,.020),.005)
    box("Velvet",(0,0,.074),(1.04,.433,.007),.003)
    box("Velvet",(0,.223,.358),(1.050,.019,.587),.004)
    for side in (-1,1):
        box("Clouded stone",(side*.530,.018,.353),(.027,.445,.585),.004)
        # Gold acanthus leg follows the cut corner and plain stone side.
        x=side*.526
        box("Brass",(x,-.183,.351),(.045,.053,.58),.009)
        tube("Gilt",[(x,-.215,.071),(x-side*.014,-.223,.181),(x-side*.005,-.226,.380),
                     (x+side*.006,-.211,.514),(x-side*.009,-.220,.604)],.008)
        for zz,up in ((.096,1),(.608,-1)):
            sprig(x+side*.004,-.226,zz,-side,up,.92)
            scroll_xz(x,-.226,zz+up*.040,.020*side,.031*up,radius=.004)
    for a,b,delta,finish in ((.637,.650,-.009,"Brass"),(.648,.660,.005,"Gilt"),
        (.658,.674,-.001,"Brass"),(.671,.699,.011,"Brass"),(.695,.713,.008,"Gilt"),(.709,.722,-.005,"Brass")):
        slab(case_outline(.548+delta,.245+delta,-.225-delta,-.267-delta,.054),a,b,finish)
    slab(case_outline(.516,.218,-.198,-.229,.043,.192),.718,.724,"Clouded stone")
    # A raised, cranked top border follows the projecting central bay; the inside remains cream marble.
    outline=case_outline(.377,.125,-.125,-.160,.020,.173)
    tube("Gilt",[(x,y,.729) for x,y in outline+[outline[0]]],.0065,9)
    for i in range(113):
        x=(i-56)*.0092
        y=-.277 if abs(x)<.210 else -.233
        front_bead(x,.652,.0035,y)
    # The middle marble door front stays attached after the two narrow leaves open.
    box("Clouded stone",(0,-.230,.354),(.466,.060,.577),.009)
    for x in (-.227,.227):
        box("Brass",(x,-.264,.352),(.014,.013,.569),.004)
        rod("Gilt",(x,-.275,.079),(x,-.275,.620),.0037)
    for z in (.078,.622):
        box("Brass",(0,-.265,z),(.466,.015,.018),.004)
    for side in (-1,1):
        for z,up in ((.094,1),(.607,-1)):
            sprig(side*.192,-.272,z,-side,up,.78)
            scroll_xz(side*.174,-.272,z+up*.026,.018*side,.013*up,radius=.0023)
    for j in range(3):
        sprig(-.018+j*.018,-.278,.598,(-1 if j==0 else 1),1,.82)
    # Dominant gilt lock boss: moulded stationary escutcheon, studwork,
    # a moving relief-faced wheel and cast finger stop on the front.
    gz=.345
    turned("Brass",(0,-.263,gz),[(0,.156),(.006,.166),(.015,.166),(.019,.158),(.024,.156)],(0,-1,0),72)
    cartouche(0,-.292,gz,.151,.151,"Gilt",.0051)
    for j in range(40):
        a=TAU*j/40;front_bead(.157*math.cos(a),gz+.157*math.sin(a),.0029,-.286)
    # The left hand braces this fixed, capped rim grip.
    rod("Brass",(-.115,-.284,gz+.098),(-.115,-.315,gz+.098),.010,sides=18)
    rod("Gilt",(-.115,-.317,gz+.083),(-.115,-.317,gz+.110),.008,sides=18)
    box("Gilt",(0,-.268,.639),(.120,.028,.012),.004)
    ellipsoid("Opal",(0,-.281,.633),(.039,.006,.004),14)
    for side in (-1,1):
        # Perimeter pads keep the centre of each consignment bay completely clear.
        box("Velvet",(side*.372,.211,.477),(.274,.009,.251),.011)
        box("Velvet",(side*.523,.014,.248),(.006,.366,.300),.003)
        box("Velvet",(side*.232,.017,.247),(.005,.359,.301),.002)
        box("Velvet",(side*.375,-.208,.080),(.278,.010,.011),.003)
        for dx in (-.120,.120):
            rod("Brass",(side*.372+dx,.207,.107),(side*.372+dx,.207,.618),.0032,sides=9)
        # Low output festoon bulbs under the two recesses illuminate real content after opening.
        box("Brass",(side*.374,-.108,.640),(.084,.044,.007),.004)
        ellipsoid("Opal",(side*.374,-.108,.636),(.026,.011,.004),14)
    treasury_leaf(-1);treasury_leaf(1)
    part("TreasuryLock")
    turned("Brass",(0,-.288,gz),[(0,.136),(.004,.140),(.008,.128),(.018,.121),(.024,.103),(.028,.078)],(0,-1,0),72)
    for r,depth in ((.133,.295),(.114,.306),(.078,.316)):
        cartouche(0,-depth,gz,r,r,"Gilt",.0025)
    def relief_ribbon(path,half_widths):
        def face_depth(x,z):
            r=math.hypot(x,z)
            levels=((0,.316),(.078,.316),(.103,.312),(.121,.306),(.128,.296),(.140,.292))
            for (small,a),(large,b) in zip(levels,levels[1:]):
                if r<=large:return a+(b-a)*max(0,min(1,(r-small)/(large-small)))
            return .292
        rim=[]
        for i,(x,z) in enumerate(path):
            x0,z0=path[max(0,i-1)];x1,z1=path[min(len(path)-1,i+1)]
            length=max(.001,math.hypot(x1-x0,z1-z0));n=Vector((-(z1-z0)/length,0,(x1-x0)/length))
            width=half_widths[i]
            def at(off,lift):
                xx=x+n.x*off;zz=z+n.z*off
                return Vector((xx,-face_depth(xx,zz)-lift,gz+zz))
            rim.append((at(width,-.0005),at(width,.0013),at(0,.0024+width*.31),at(-width,.0013),at(-width,-.0005)))
        for left,right in zip(rim,rim[1:]):
            for j,finish in ((0,"Brass"),(1,"Gilt"),(2,"Brass"),(3,"Brass")):
                face=[left[j],right[j],right[j+1],left[j+1]]
                if (face[1]-face[0]).cross(face[2]-face[0]).y>0:face.reverse()
                shop.face(face,finish,True)
    # Four interlaced, modelled acanthus volutes with fleshy spines and
    # narrower curled terminals, surrounding the domed central boss.
    for j in range(4):
        phi=j*TAU/4+.27
        def turn(x,z):return (x*math.cos(phi)-z*math.sin(phi),x*math.sin(phi)+z*math.cos(phi))
        p0=Vector((.001,.022)); p1=Vector((.035,-.019));p2=Vector((.075,-.030));p3=Vector((.092,.021))
        curve=[];width=[]
        for i in range(21):
            t=i/20;v=p0*(1-t)**3+3*p1*t*(1-t)**2+3*p2*t*t*(1-t)+p3*t**3
            curve.append(turn(v.x,v.y));width.append(.004+.014*math.sin(math.pi*t)**.78)
        for i in range(1,33):
            t=i/32;a=t*math.pi*1.88
            curve.append(turn(.070+.022*(1-.41*t)*math.cos(a),.021+.036*(1-.52*t)*math.sin(a)))
            width.append(.0045+.005*(1-t))
        relief_ribbon(curve,width)
        for k in range(3):
            points=[];ws=[]
            for i in range(14):
                t=i/13
                points.append(turn(.030+.013*k+.028*t,.004-.007*k+.044*t-.014*t*t))
                ws.append(.001+.009*math.sin(t*math.pi)**.75)
            relief_ribbon(points,ws)
    ellipsoid("Gilt",(0,-.319,gz),(.018,.007,.018),20)
    rod("Brass",(.112,-.301,gz-.018),(.112,-.337,gz-.018),.0098,sides=20)
    rod("Gilt",(.113,-.337,gz-.037),(.113,-.337,gz+.004),.0078,sides=20)



def finger_skin(name,points,radii,side):
    """One uninterrupted kid-suede surface across all three articulated joints."""
    part(name)
    points=list(map(Vector,points))
    rings=[]
    for j in range(3):
        a,b=points[j],points[j+1];segment=b-a
        for t in ((-.20,0,.18,.44,.68,.86,1) if j==0 else (.17,.42,.70,.89,1.0)):
            centre=a+segment*t
            before=segment.normalized() if j==0 else (points[j+1]-points[j-1]).normalized()
            after=segment.normalized() if j==2 else (points[j+2]-points[j]).normalized()
            axis=before.lerp(after,max(0,min(1,t))).normalized()
            width=Vector((1,0,0));width=(width-axis*axis.dot(width)).normalized()
            dorsal=width.cross(axis).normalized()
            rad=(radii[j]+(radii[j+1]-radii[j])*min(1,max(t,0)))
            # Knuckles widen subtly. No circumferential seam around a joint.
            rad*=1+.038*math.cos(t*TAU)
            rings.append([centre+width*(rad*math.cos(i*TAU/16))+dorsal*(rad*.84*math.sin(i*TAU/16))
                          for i in range(16)])
    axis=(points[-1]-points[-2]).normalized();tip=points[-1];rr=radii[-1]
    for factor,shift in ((.80,.38),(.49,.70),(.13,.87)):
        rings.append([tip+axis*(rr*shift)+width*(rr*factor*math.cos(i*TAU/16))+
                      dorsal*(rr*.83*factor*math.sin(i*TAU/16)) for i in range(16)])
    for band,(lo,hi) in enumerate(zip(rings,rings[1:])):
        for i in range(16):
            j=(i+1)%16
            uv=[(i/16,band/(len(rings)-1)),(i/16,(band+1)/(len(rings)-1)),((i+1)/16,(band+1)/(len(rings)-1)),((i+1)/16,band/(len(rings)-1))]
            shop.face([lo[i],hi[i],hi[j],lo[j]],"Greige suede",True,uv)
    shop.face(rings[0],"Greige suede",True);shop.face(rings[-1][::-1],"Greige suede",True)



def fingers(side):
    sy="Left" if side<0 else "Right"
    def p(x,y,z):return (side*x,y,z)
    for i,(label,x,y,length,r) in enumerate((("Index",-.030,.090,.074,.0099),
        ("Middle",-.0097,.095,.080,.0101),("Ring",.0111,.092,.073,.0094),("Little",.0289,.085,.055,.0081))):
        fan= -.002 if i==0 else .002
        points=[p(x,y,.005),p(x+fan,y+length*.43,.007),p(x+fan*1.45,y+length*.72,-.002),
                p(x+fan*1.6,y+length*.91,-.015)]
        finger_skin(sy+label+"Skin",points,[r,.91*r,.75*r,.59*r],side)
    pts=[p(-.027,.042,-.003),p(-.0515,.060,-.012),p(-.050,.083,-.030),p(-.035,.095,-.039)]
    finger_skin(sy+"ThumbSkin",pts,[.012,.011,.0098,.0075],side)


def palm(side):
    part("LeftGlove" if side<0 else "RightGlove")
    # Cross sections follow the wrist, thenar eminence, four metacarpals and webbing.
    rings=[]
    for y,width,dorsal,volar,shift in ((-.012,.027,.013,.012,0),(.012,.030,.016,.014,-.001),
        (.032,.036,.020,.021,-.0025),(.055,.041,.021,.023,-.0020),
        (.077,.040,.019,.016,0),(.089,.038,.014,.012,0),(.098,.032,.010,.010,0)):
        ring=[]
        for i in range(28):
            a=i*TAU/28;cx=math.cos(a)
            ring.append(Vector((side*(shift+width*cx),y,(dorsal if math.sin(a)>0 else volar)*math.sin(a))))
        rings.append(ring)
    for k,(lo,hi) in enumerate(zip(rings,rings[1:])):
        for i in range(28):
            j=(i+1)%28;surface=[lo[i],lo[j],hi[j],hi[i]]
            uv=[(i/28,k/(len(rings)-1)),((i+1)/28,k/(len(rings)-1)),((i+1)/28,(k+1)/(len(rings)-1)),(i/28,(k+1)/(len(rings)-1))]
            if side>0:surface.reverse();uv.reverse()
            shop.face(surface,"Greige suede",True,uv)
    shop.face(rings[0],"Greige suede",True);shop.face(rings[-1],"Greige suede",True)
    # A tailored linen cuff sets off the matte greige suede at the wrist.
    turned("Shirt linen",(0,-.020,0),[(0,.033),(.014,.034),(.024,.032)],(0,1,0),30,.72)
    turned("Shirt linen",(0,-.006,0),[(0,.033),(.004,.033)],(0,1,0),30,.72)
    ellipsoid("Horn",(side*.032,-.011,.004),(.0025,.0048,.0048),16)



def coat(side,upper):
    name=("Left" if side<0 else "Right")+("Upper" if upper else "Sleeve")
    part(name)
    profile=([(0,.054),(.025,.055),(.074,.056),(.128,.058),(.166,.064),(.204,.065),(.249,.071),(.279,.079)] if upper else
             [(0,.034),(.016,.035),(.048,.037),(.078,.041),(.123,.047),(.155,.050),(.175,.052),(.195,.050),(.221,.053),(.243,.054),(.266,.055)])
    rings=[]
    for t,r in profile:
        rings.append([Vector((math.cos(a*TAU/28)*r,-t,math.sin(a*TAU/28)*r*.89+
            (.0021*math.sin(a*TAU/28*4+t*23) if t>.071 else 0))) for a in range(28)])
    for k,(lo,hi) in enumerate(zip(rings,rings[1:])):
        for i in range(28):
            j=(i+1)%28
            u0=i/28*TAU*profile[k][1]/.08;u1=(i+1)/28*TAU*profile[k][1]/.08
            v0=profile[k][0]/.08;v1=profile[k+1][0]/.08
            shop.face([lo[i],lo[j],hi[j],hi[i]],"Twill",True,[(u0,v0),(u1,v0),(u1,v1),(u0,v1)])
    shop.face(rings[0][::-1],"Twill");shop.face(rings[-1],"Twill")
    if not upper:
        # Tonal cuff facing, woven edge, two horn buttons with sewn-through holes.
        turned("Twill",(0,-.009,0),[(0,.035),(.022,.036)],(0,-1,0),24,.91)
        for i in range(2):
            x=side*.033; y=-.023-i*.018
            turned("Horn",(x,y,.003),[(0,.0055),(.002,.0057),(.003,.0047)],(side,0,0),18)
        tube("Seam",[(side*.025,-.005,.018),(side*.030,-.066,.020),(side*.039,-.156,.025),(side*.044,-.243,.028)],.0011,5)


def gloves():
    for side in (-1,1):
        palm(side);fingers(side);coat(side,False);coat(side,True)



def export(name,build,offset):
    shop.collection=bpy.data.collections.new(name);bpy.context.scene.collection.children.link(shop.collection)
    shop.parts=[];shop.group=None;build();shop.group.flush();shop.group=None
    base=bpy.data.objects.new(name,None);shop.collection.objects.link(base)
    for item in shop.parts:
        mesh=bmesh.new();mesh.from_mesh(item.data);bmesh.ops.remove_doubles(mesh,verts=list(mesh.verts),dist=.000012)
        mesh.to_mesh(item.data);mesh.free();item.data.update();item.parent=base
    # These four empties let the importer build a proper (unscaled) hinge even
    # when a Blender or Unity update changes the FBX basis conversion.
    markers=[]
    for axis,where in (("Origin",(0,0,0)),("Right",(.1,0,0)),("Back",(0,.1,0)),("Up",(0,0,.1))):
        marker=bpy.data.objects.new(name+"_"+axis,None);shop.collection.objects.link(marker)
        marker.parent=base;marker.location=where;marker.empty_display_size=.012;markers.append(marker)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in [base]+shop.parts+markers:obj.select_set(True)
    bpy.context.view_layer.objects.active=base
    path=TARGET/(name+".fbx")
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={"MESH","EMPTY"},
        axis_forward="-Z",axis_up="Y",use_triangles=True,apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",bake_anim=False,add_leaf_bones=False,path_mode="STRIP",mesh_smooth_type="FACE")
    shop.remap_fbx(path,{m.name for obj in shop.parts for m in obj.data.materials})
    base.location=offset


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True);bpy.context.preferences.filepaths.save_version=0
    bpy.context.scene.unit_settings.system="METRIC";TARGET.mkdir(parents=True,exist_ok=True)
    shop.stable_guid=lambda key:uuid.uuid5(uuid.NAMESPACE_URL,"winterbourne/chest-art/"+key).hex
    shop.copied_meta(STATION.with_suffix(".meta"),TARGET.with_suffix(".meta"),"folder")
    for key,hexrgb,destination,metal,rough in (
        ("Oak","865c3f","Waiting room oak",.03,.58),("Walnut","805a41","Reading room walnut",.03,.5),
        ("Leather","735344","Heritage/Seat hide",.03,.48),("Velvet","38534b","Heritage/Leather",0,.88),
        ("Paper","e4d7b7","Unbleached silk",0,.65),("Ivory","d9ccac","../Valuables/Pearl",.09,.35),
        
        ("Iron","3b3736","Heritage/Graphite",.5,.42),("Enamel","22495e","../Valuables/Cobalt",.15,.22),
        ("Oxblood","802d29","../Valuables/Carnelian",.06,.48),("Wool","343b40","Keeper's slate",0,.88),
        ("Stitch","987856","Webbing",0,.7)):
        texture=STATION/"WeatheredWalnut.png" if key=="Walnut" else None
        shop.mat(key,hexrgb,metal,rough,STATION/(destination+".mat"),texture)
    shop.OUT=TARGET
    for key,file,base,metal,rough in (
        ("Oxblood wood","Oxblood trunk boards.png","884d37",0,.50),
        ("Clouded stone","Marbled cream panels.png","decfb5",.07,.32)):
        photo=TARGET/file
        shop.copied_meta(STATION/"StationHouseEnamel.png.meta",photo.with_suffix(".png.meta"),file)
        material=shop.finish_material(key,"ffffff",metal,rough,texture=True)
        material.write_text(material.read_text().replace(shop.stable_guid("Stationery.png"),shop.stable_guid(file)),encoding="utf8")
        shop.mat(key,base,metal,rough,material,photo)
    for key,name,color,metal,rough in (("Brass","Cast antique brass","bc935e",.55,.45),
            ("Gilt","Chased ormolu","d5ac6b",.68,.38)):
        native=shop.finish_material(name,color,metal,rough);shop.mat(key,color,metal,rough,native)
    bridle=shop.finish_material("Worn bridle hide","76523c",.02,.57)
    shop.mat("Bridle","76523c",.02,.57,bridle)
    for key,color,rough in (("Horn","63523e",.46),("Seam","665d50",.82)):
        m=shop.finish_material(key,color,.015,rough);shop.mat(key,color,.015,rough,m)
    photo=TARGET/"Greige suede.png";nap=TARGET/"Suede nap.png"
    for swatch in (photo,nap):
        shop.copied_meta(STATION/"StationHouseEnamel.png.meta",swatch.with_suffix(".png.meta"),swatch.name)
    meta=nap.with_suffix(".png.meta")
    meta.write_text(meta.read_text().replace("textureType: 0","textureType: 1").replace("sRGBTexture: 1","sRGBTexture: 0"),encoding="utf8")
    suede=shop.finish_material("Greige suede","ffffff",0,.87,texture=True)
    import re
    matdata=suede.read_text().replace(shop.stable_guid("Stationery.png"),shop.stable_guid(photo.name))
    matdata=re.sub(r"(- _BumpMap:\s*\n\s*m_Texture: )\{fileID: 0\}",r"\g<1>{fileID: 2800000, guid: "+shop.stable_guid(nap.name)+", type: 3}",matdata)
    matdata=matdata.replace("m_ValidKeywords: []","m_ValidKeywords:\n  - _NORMALMAP").replace("- _BumpScale: 1","- _BumpScale: 0.38")
    suede.write_text(matdata,encoding="utf8")
    shop.mat("Greige suede","b1a596",0,.87,suede,photo)
    shader=shop.materials["Greige suede"].node_tree.nodes.get("Principled BSDF")
    shader.inputs["Sheen Weight"].default_value=.19;shader.inputs["Sheen Roughness"].default_value=.80
    detective=STATION.parent/"Detective"
    cloth=detective/"Holmes moor tweed.mat"
    pattern=detective/"Moor herringbone.png"
    if not cloth.with_suffix(".mat.meta").is_file() or not pattern.is_file():
        raise RuntimeError("Generate the detective's shared tweed material before fitting the coat cuffs.")
    shop.mat("Twill","504c43",0,.91,cloth,pattern)
    shop.mat("Shirt linen","d4c9b0",0,.84,detective/"Holmes linen.mat")
    shop.mat("Opal","f2dcaa",.02,.31,STATION/"Lit station opal.mat",glow=.5)
    export("ParcelChest",parcel,(-.65,0,0));export("TreasuryChest",treasury,(.35,0,0))
    export("CabinetGloves",gloves,(-.2,-.56,.34))
    bpy.ops.object.select_all(action="DESELECT")
    for area in bpy.context.screen.areas:
        if area.type=="VIEW_3D":
            area.spaces.active.region_3d.view_distance=2.8
            area.spaces.active.shading.color_type="MATERIAL"
    bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/"Source/Pass/FellbridgeChests.blend"),compress=True)


if __name__=="__main__":main()
