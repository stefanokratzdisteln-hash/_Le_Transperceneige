"""Tailor the Inverness coat, cap, waistcoat and boots around the detective's rest skeleton."""
from math import sin,cos,pi,tau,exp
from mathutils import Vector
import station_heritage as m

TWEED="Holmes moor tweed"; CLOTH="Holmes peat cloth"; VEST="Holmes waistcoat"; BOOT="Holmes boot leather"
PIPE="Holmes piping"; HORN="Holmes horn"; BRASS="Holmes aged brass"; SHIRT="Holmes linen"; TIE="Holmes tie"
TROUSER="Holmes trouser twill";LINING="Holmes lining";HAIR="Holmes black hair";HAIR2="Holmes hair highlights"
SKIN="Holmes complexion";GLOVE="Holmes kid suede";WELT="Holmes suede welt"; EYE="Holmes sclera";IRIS="Holmes iris"
LASH="Holmes eyelashes";GLINT="Holmes catchlight"
SECTIONS={}


def part(name,section):
    m.part(name)
    SECTIONS[name]=section


def strip(lower,upper,mat,closed=True):
    count=len(lower) if closed else len(lower)-1
    for i in range(count):
        j=(i+1)%len(lower);m.face([lower[i],lower[j],upper[j],upper[i]],mat,True)


def roll(mat,points,r=.0015):
    m.tube(mat,points,r,6)


def hem(mat,points,r=.0014): roll(mat,points+[points[0]],r)


def cross_z(x,y,z,rx,ry,n=40):
    return [Vector((x+rx*sin(i*tau/n),y-ry*cos(i*tau/n),z)) for i in range(n)]


def lapel(mat,points):
    vs=[Vector(p) for p in points];mid=sum(vs,Vector())/len(vs);mid.y-=.006
    for a,b in zip(vs,vs[1:]+vs[:1]):
        coords=[a,b,mid]
        if (b-a).cross(mid-a).y>0:coords.reverse()
        m.face(coords,mat,True)
    roll(PIPE,vs+[vs[0]],.00105)


def body_panels(name,layers,mat,fold=.0023,section="trunk"):
    part(name,section)
    rings=[]
    for z,rx,ry,cy,gap in layers:
        ring=[]
        for i in range(69):
            angle=gap+(tau-gap*2)*i/68
            rip=fold*(sin(angle*11+.54*z)+.53*cos(angle*16-z*2))*min(1,(abs(z-1.24)+.06)*4)
            x=(rx+rip)*sin(angle);y=cy-(ry+rip)*cos(angle)
            # A slight sag in cloth between the shoulder seams.
            ring.append(Vector((x,y,z-.003*(sin(angle)**2))))
        rings.append(ring)
    for a,b in zip(rings,rings[1:]):strip(a,b,mat,False)
    for i in (0,-1):roll(PIPE,[r[i] for r in rings],.0016)
    roll(PIPE,rings[0],.0016)
    # Lining doubles back around the front facing and shows inside an open coat.
    for end in (0,-1):
        for a,b in zip(rings,rings[1:]):
            A=a[end]*Vector((.92,.92,1));B=b[end]*Vector((.92,.92,1))
            m.face([a[end],A,B,b[end]],LINING)
    return rings


def boots_and_trousers():
    for side,label in ((1,"Left"),(-1,"Right")):
        part(label+" laced Balmoral boot",label+"Foot")
        levels=[]
        for y,rx,bottom,top in ((-.174,.003,.012,.023),(-.164,.031,.009,.037),(-.140,.045,.006,.049),
             (-.105,.049,.005,.054),(-.070,.046,.004,.065),(-.043,.042,.005,.082),(-.018,.039,.006,.109),
             (.004,.034,.009,.143),(.028,.035,.010,.142),(.052,.027,.010,.097),(.059,.006,.012,.071)):
            pts=[]
            for i in range(28):
                a=i*tau/28;x=side*.149+rx*sin(a);z=bottom+(top-bottom)*max(0,min(1,(1+cos(a))*.5))**.81
                pts.append(Vector((x,y,z)))
            levels.append(pts)
        for a,b in zip(levels,levels[1:]):strip(a,b,BOOT)
        for h in (-1,1):
            pts=[(v[7 if h>0 else 21].x,v[0].y,.018) for v in levels]
            roll(PIPE,pts,.0026)
        for j in range(6):
            y=-.067+j*.011;z=.071+j*.009
            for h in (-1,1):
                a=(side*.149+h*.018,y,z-.0005);b=(side*.149-h*.016,y+.008,z+.007)
                m.ellipsoid(HORN,a,(.003,.002,.002),8);roll(PIPE,[a,((a[0]+b[0])*.5,y+.004,z+.009),b],.00125)
        part(label+" narrow checked trouser",label+" trouser")
        prev=None
        for z,x,cy,rx,ry in ((.109,.148,.011,.045,.041),(.123,.149,.012,.052,.047),(.149,.147,.005,.050,.048),
             (.193,.146,.004,.044,.044),(.269,.145,.014,.045,.044),(.355,.144,.007,.054,.059),(.433,.143,-.002,.049,.053),
             (.472,.139,-.012,.053,.057),(.497,.138,-.013,.059,.062),(.528,.133,-.008,.054,.060),
             (.611,.124,.001,.061,.069),(.732,.115,.006,.070,.077),(.851,.103,.002,.085,.089),(.947,.093,.003,.087,.093)):
            ring=cross_z(side*x,cy,z,rx,ry,36)
            for k,v in enumerate(ring):
                a=k*tau/36
                v.z+=.006*sin(2*a+side)*exp(-((z-.485)/.044)**2)+.003*sin(a+side)*exp(-((z-.13)/.02)**2)
            if prev is not None:strip(prev,ring,TROUSER)
            if z==.109:hem(PIPE,ring)
            prev=ring
        # Pressed crease follows the knee seam rather than floating on a cylinder.
        roll(PIPE,[(side*.147,-.041,.137),(side*.145,-.036,.27),(side*.144,-.053,.351),
                 (side*.139,-.067,.486),(side*.124,-.068,.609),(side*.116,-.073,.732)],.00072)


def coat():
    # Beneath the open waistcoat the white front narrows into a high detachable collar.
    body_panels("Collared cotton shirt",[(.914,.139,.081,.004,0), (1.05,.137,.094,.003,0),
        (1.215,.165,.097,.001,0),(1.31,.16,.094,.003,0),(1.395,.079,.065,-.016,0),
        (1.458,.060,.061,-.026,0)],SHIRT,0)
    body_panels("Five button wool waistcoat",[(.884,.132,.091,0,.08),(.908,.147,.095,-.002,.025),
        (.971,.148,.100,-.002,.018),(1.085,.146,.105,-.001,.028),(1.184,.158,.100,.001,.045),
        (1.24,.171,.105,.001,.206),(1.32,.157,.100,.001,.485),(1.401,.066,.067,-.017,.58)],VEST,.001)
    part("Turned horn waistcoat buttons and Albert watch chain","trunk")
    for j in range(5):
        z=.94+j*.052
        m.rod(HORN,(.004,-.107,z),(.004,-.110,z),.006,.005,12)
        for x in (-.0016,.0016):m.ellipsoid(PIPE,(.004+x,-.111,z),(.0008,.0006,.00085),8)
    for h in (-1,1):
        lapel(VEST,[(h*.066,-.099,.974),(h*.126,-.082,.985),(h*.128,-.078,.973),(h*.068,-.101,.963)])
    for j in range(35):
        u=j/34;x=.013+.100*u;z=1.086-.095*sin(pi*u*.86);y=-.113+.025*u*u
        ring=[(x+.0026*cos(a*tau/9),y+.0015*sin(a*tau/9)*(j%2),z+.0021*sin(a*tau/9)) for a in range(10)]
        roll(BRASS,ring,.00065)
    part("Wine silk four-in-hand and starched collar points","Chest")
    lapel(TIE,[(-.026,-.095,1.399),(.024,-.095,1.399),(.015,-.109,1.376),(-.014,-.108,1.375)])
    lapel(TIE,[(-.012,-.103,1.376),(.012,-.103,1.376),(.022,-.109,1.258),(.004,-.112,1.236),(-.023,-.107,1.256)])
    for h in (-1,1):
        lapel(SHIRT,[(h*.008,-.099,1.409),(h*.051,-.078,1.427),(h*.058,-.090,1.394),(h*.029,-.112,1.364)])
    body_panels("Open travelling coat, lined skirts and faced opening",[(.503,.188,.137,.012,.335),
        (.531,.189,.138,.010,.335),(.644,.174,.137,0,.304),(.800,.165,.125,.006,.300),
        (.944,.161,.113,.005,.334),(1.08,.159,.111,.002,.428),(1.223,.181,.117,.004,.454),
        (1.33,.188,.108,.002,.598),(1.407,.081,.075,-.017,.52)],TWEED,.0052)
    for h in (-1,1):
        part(("Left" if h>0 else "Right")+" notched lapel and pocket","trunk")
        lapel(TWEED,[(h*.041,-.092,1.407),(h*.100,-.096,1.370),(h*.131,-.085,1.308),
             (h*.105,-.105,1.298),(h*.133,-.098,1.280),(h*.076,-.115,1.147),(h*.053,-.106,1.093)])
        lapel(CLOTH,[(h*.078,-.111,.904),(h*.150,-.084,.915),(h*.151,-.086,.885),(h*.079,-.112,.877)])
    # The bell-shaped cape hangs in its own continuous panels over the armholes.
    rings=body_panels("Shoulder cape with scalloped winter drape",[(1.075,.258,.155,.014,.45),
        (1.102,.254,.150,.015,.446),(1.176,.236,.136,.013,.40),(1.269,.219,.121,.009,.345),
        (1.326,.207,.108,.004,.36),(1.362,.169,.093,-.002,.40),(1.41,.079,.073,-.018,.53)],
        TWEED,.0094)
    for h in (-1,1):
        r=rings[0];y=min(range(len(r)),key=lambda i:abs(r[i].x-h*.205) if r[i].y<0 else 10)
        roll(CLOTH,[ring[y]+Vector((0,-.001,0)) for ring in rings],.00135)
    part("Travel collar, throat strap and mother-of-pearl pin","Chest")
    body_panels("Raised tailored collar",[(1.381,.084,.074,-.015,.48),(1.409,.080,.073,-.023,.49),
               (1.450,.070,.063,-.025,.68)],CLOTH,.0014,"Chest")
    for h in (-1,1):
        label="Left" if h>0 else "Right"
        part(label+" fitted sleeve and working cuff",label+" sleeve")
        prev=None
        for z,x,y,rx,ry in ((.877,.243,-.064,.038,.037),(.896,.242,-.059,.040,.039),
            (.930,.237,-.052,.041,.039),(1.004,.232,-.030,.046,.044),(1.080,.227,-.009,.049,.047),
            (1.118,.223,-.005,.052,.051),(1.143,.216,-.008,.052,.050),(1.227,.193,-.009,.055,.053),
            (1.302,.173,-.011,.058,.058),(1.336,.162,-.014,.041,.040)):
            ring=cross_z(h*x,y,z,rx,ry,32)
            if prev is not None:strip(prev,ring,TWEED)
            if z==.877:hem(CLOTH,ring,.002)
            prev=ring
        cuff=cross_z(h*.244,-.066,.863,.0345,.034,32)
        strip(cuff,cross_z(h*.244,-.065,.882,.035,.035,32),SHIRT)
        hem(SHIRT,cuff,.001)
        for j in range(3):
            m.ellipsoid(HORN,(h*.277,-.080,.909+j*.014),(.0033,.005,.0052),12)

def cap():
    part("Eight dart deerstalker crown and grosgrain band","Head")
    cy=-.061
    rings=[]
    for j in range(15):
        a=j*pi/2/15;ratio=cos(a)**.83
        rx=.103*ratio*(1+.10*sin(a*2));ry=.134*ratio*(1+.06*sin(a*2))
        z=1.705+.137*sin(a)
        ring=cross_z(0,cy+.014*sin(a),z,rx,ry,72)
        for i,p in enumerate(ring):
            ang=i*tau/72
            p.z+=.012*cos(ang)*ratio+.0018*cos(ang*8)*sin(a*pi/2)
        rings.append(ring)
    for a,b in zip(rings,rings[1:]):strip(a,b,TWEED)
    pole=Vector((0,cy+.014,1.843))
    for i,p in enumerate(rings[-1]):m.face([p,rings[-1][(i+1)%72],pole],TWEED,True)
    hem(CLOTH,rings[0],.0023)
    for i in range(0,72,9):roll(CLOTH,[p[i]+Vector((0,0,.001)) for p in rings]+[pole],.00105)
    part("Independent curved front and rear cap peaks","Head")
    for sign in (-1,1):
        front=sign==-1;n=34;rows=[]
        for j in range(7):
            d=j/6;pts=[]
            for i in range(n+1):
                a=-1.29+i*2.58/n;s=sin(a);c=cos(a)
                x=.102*s*(1-.12*d)
                y=cy+sign*(.131*c+d*(.066 if front else .047)*c**1.75)
                z=1.707+(.007 if front else -.010)*c-.009*s*s-d*.017*c+.003*sin(d*pi)
                pts.append(Vector((x,y,z)))
            rows.append(pts)
        for k,(a,b) in enumerate(zip(rows,rows[1:])):
            for i in range(n):
                face=[a[i],b[i],b[i+1],a[i+1]]
                if (face[1]-face[0]).cross(face[2]-face[0]).z<0:face.reverse()
                m.face(face,TWEED,True);m.face([p-Vector((0,0,.002)) for p in face[::-1]],CLOTH,True)
        roll(PIPE,rows[-1],.0017)
    part("Tied ear flaps, woven ties, concealed crown button","Head")
    for h in (-1,1):
        rings=[]
        for i in range(12):
            a=.11+i*.102
            x=h*.104*cos(a);z=1.717+.131*sin(a);y=cy+.008
            width=.060*(1-i/21)
            section=[]
            for j in range(17):
                t=-1+2*j/16;section.append(Vector((x+h*.005*(1-t*t),y+t*width,z-.012*t*t)))
            rings.append(section)
        for a,b in zip(rings,rings[1:]):
            for j in range(16):
                pts=[a[j],a[j+1],b[j+1],b[j]]
                if h<0:pts.reverse()
                m.face(pts,TWEED,True)
        for i in (0,-1):roll(CLOTH,[p[i] for p in rings],.0013)
        roll(CLOTH,rings[-1],.0013)
        tie=[(h*.041,cy,1.834),(h*.022,cy-.005,1.852),(h*.009,cy,1.855)]
        roll(CLOTH,tie,.0029)
        loop=[(h*.004,cy,1.856),(h*.018,cy+.013,1.870),(h*.034,cy+.010,1.861),(h*.029,cy+.003,1.856),
              (h*.009,cy,1.855)]
        roll(CLOTH,loop,.0039)
        lapel(CLOTH,[(h*.002,cy-.009,1.856),(h*.008,cy-.006,1.857),
                     (h*.036,cy-.039,1.837),(h*.029,cy-.042,1.836)])
    m.ellipsoid(HORN,(0,cy,1.857),(.008,.007,.005),16)


def eyes_and_brows(joints,front):
    for h,label in ((1,"Left"),(-1,"Right")):
        centre=Vector(joints[label+"Eye"][0]);centre.y-=.003
        part(label+" eyeball, grey-hazel iris, limbal ring",label+"Eye")
        m.ellipsoid(EYE,centre,(.0187,.0214,.0189),32)
        u=.0080
        for j in range(5):
            a=u*j/5;b=u*(j+1)/5
            for i in range(44):
                points=[];uv=[]
                for rad,t in ((a,i),(b,i),(b,i+1),(a,i+1)):
                    ang=t*tau/44
                    xx=rad*cos(ang);zz=rad*sin(ang)
                    points.append(centre+Vector((xx,-.0217*(1-(rad/.0188)**2)**.50,zz)))
                    uv.append((.5+xx/(2*u),.5+zz/(2*u)))
                if j==0:points.pop(3);uv.pop(3)
                m.face(points,IRIS,True,uv=uv)
        m.ellipsoid(GLINT,centre+Vector((-.0028,-.0219,.0035)),(.00135,.00048,.0014),10)
        part(label+" swept eyebrow, tapering hairs","Head")
        cx=centre.x;z=centre.z
        rows=[]
        for j in range(27):
            t=j/26;x=cx+h*(-.023+.054*t);height=z+.019+.007*sin(t*pi)-.003*t
            width=.0043*(1-.76*t)+.0009
            rows.append([Vector((x,front(x,height+w)-.0016,height+w)) for w in (-width,0,width)])
        for a,b in zip(rows,rows[1:]):
            for k in (0,1):
                pts=[a[k],b[k],b[k+1],a[k+1]]
                if (pts[1]-pts[0]).cross(pts[2]-pts[0]).y>0:pts.reverse()
                m.face(pts,HAIR,True)
        for j in range(24):
            t=j/24;x=cx+h*(-.022+.052*t);zz=z+.019+.007*sin(t*pi)-.003*t
            pts=[(x,front(x,zz-.002)-.0022,zz-.002),(x+h*.004,front(x+h*.004,zz+.0026)-.0025,zz+.0026)]
            roll(HAIR2 if j%4==0 else HAIR,pts,.0006)
    part("Swept forelock and side-parted hair under the travelling cap","Head")
    # Tapered, sculpted locks with offset ridges. The scalp beneath them uses
    # the original shaped head topology and preserves the ear and hairline.
    for h in (-1,1):
        for j in range(13):
            x0=h*(.011+j*.0052);x1=h*(.046+j*.0035)
            pts=[];rows=[]
            for k in range(13):
                t=k/12;x=x0*(1-t)+x1*t+.008*sin(t*pi)
                z=1.733-.052*t-.020*t*t*(j/12);w=.006*(1-.77*t)
                rows.append([Vector((x+dx,front(x+dx,z)-.004-.002*(1-abs(dx)/max(w,.0001)),z)) for dx in (-w,0,w)])
            for a,b in zip(rows,rows[1:]):
                for i in (0,1):m.face([a[i],b[i],b[i+1],a[i+1]],HAIR,True)
            roll(HAIR2,[r[1] for r in rows],.00065)
