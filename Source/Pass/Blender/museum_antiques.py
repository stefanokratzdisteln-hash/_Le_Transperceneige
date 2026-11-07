"""The travelling collector's cabinet. Dimensions are metres; all work is original.

Reference objects and accession links are documented in the project's README.
Each mount, engraving and gem is part of the mesh seen by the player.
"""
from math import sin, cos, pi, tau, sqrt
from mathutils import Vector
import station_heritage as s
from station_heritage import part, face, turned, rod, tube, box, ellipsoid


def wire(m,p,rx,ry,w=.001,plane="xy",segments=48):
    x,y,z=p
    if plane=="xy": pts=[(x+rx*cos(t*tau/segments),y+ry*sin(t*tau/segments),z) for t in range(segments+1)]
    else: pts=[(x+rx*cos(t*tau/segments),y,z+ry*sin(t*tau/segments)) for t in range(segments+1)]
    tube(m,pts,w,8)


def pearl(p,r,material="Pearl"):
    ellipsoid(material,p,(r,r,r),12)


def stone(p,rx,ry,h,m="Emerald",sides=8,vertical=False):
    x,y,z=p
    def point(xx,yy,zz):return (x+xx,y-zz,z+yy) if vertical else (x+xx,y+yy,z+zz)
    def level(r,hh,offset):
        return [point(rx*r*cos((i+offset)*tau/sides),ry*r*sin((i+offset)*tau/sides),hh) for i in range(sides)]
    foot,rim,crown,table=level(.63,0,0),level(1,h*.25,0),level(.82,h*.73,.35),level(.58,h,0)
    for lo,hi in ((foot,rim),(rim,crown),(crown,table)):
        for i in range(sides):
            j=(i+1)%sides
            face([lo[i],lo[j],hi[j]],m)
            face([lo[i],hi[j],hi[i]],m)
    face(table,m)
    for i in range(4):
        a=(i+.5)*tau/4
        pearl(point(.91*rx*cos(a),.91*ry*sin(a),.38*h),min(rx,ry)*.105,"Gold")


def disc(tile,p,r,u=None,v=None):
    c=Vector(p);u=Vector(u if u else (r,0,0));v=Vector(v if v else (0,r,0))
    origin=(tile%2*.5+.25,.75-(tile//2)*.5)
    for i in range(64):
        a,b=i*tau/64,(i+1)*tau/64
        pts=[c,c+u*cos(a)+v*sin(a),c+u*cos(b)+v*sin(b)]
        uv=[origin,(origin[0]+.247*cos(a),origin[1]+.247*sin(a)),(origin[0]+.247*cos(b),origin[1]+.247*sin(b))]
        face(pts,"Miniature",uv=uv)


def beads(p,rx,ry,n,r,m="Pearl",upright=False):
    x,y,z=p
    for i in range(n):
        a=i*tau/n
        pearl((x+rx*cos(a),y if upright else y+ry*sin(a),z+ry*sin(a) if upright else z),r,m)


def enamel_leaf(surface,at,tip,width,colour,height=.00045):
    """An applied cloison: raised gilt edge and two sloping planes of fired enamel."""
    x,y=at;dx=tip[0]-x;dy=tip[1]-y
    length=sqrt(dx*dx+dy*dy);nx=-dy*width/length;ny=dx*width/length
    outline=((0,0),(.30,-.82),(.66,-1),(1,0),(.66,1),(.30,.82))
    gild=[];inside=[]
    for u,v in outline:
        gild.append(surface(x+u*dx+v*nx,y+u*dy+v*ny,.00008))
        inner=.50+(u-.50)*.80
        inside.append(surface(x+inner*dx+v*nx*.69,y+inner*dy+v*ny*.69,height*.63))
    ridge=surface(x+.52*dx,y+.52*dy,height)
    for i in range(6):
        j=(i+1)%6
        face((gild[i],gild[j],inside[j],inside[i]),"Gold")
        face((inside[i],inside[j],ridge),colour)


def blossom(surface,x,y,r,primary,secondary):
    for i in range(5):
        a=pi*.5+i*tau/5
        enamel_leaf(surface,(x,y),(x+r*cos(a),y+r*sin(a)),r*.35,primary if i%2 else secondary)
    ring=[surface(x+r*.16*cos(i*tau/8),y+r*.16*sin(i*tau/8),.00050) for i in range(8)]
    face(ring,"Gold")


def gold_scroll(surface,x,y,rx,ry,hand=1):
    # Raised S-scroll, terminating in a spiral with an open centre.
    tail=[(-1.16,-.72),(-.97,-.29),(-.65,.27),(-.22,.69),(.23,.82)]
    for j in range(19):
        a=1.55-j*.314;r=.77-j*.030
        tail.append((.26+r*cos(a),.04+r*sin(a)))
    points=[surface(x+hand*u*rx,y+v*ry,.00019) for u,v in tail]
    tube("Gold",points,min(rx,ry)*.068,6)


def watch():
    part("Engine-turned gold repeater and double calendar dial")
    turned("Gold",(0,0,0),[(0,.022),(.002,.030),(.006,.0325),(.012,.032),(.014,.030)])
    disc(0,(0,0,.0143),.0283)
    beads((0,0,.014),.030,.030,48,.00115)
    rod("Blued",(0,0,.015),(.012,-.013,.015),.0012,.00028)
    rod("Blued",(0,0,.016),(-.006,.021,.016),.00086,.00022)
    pearl((0,0,.0165),.0015,"Ruby")
    rod("Gold",(0,.026,.007),(0,.040,.007),.0035);wire("Gold",(0,.043,.007),.009,.007,.00165)
    part("Open enamel hunter lid and articulated chain")
    # Rest the open lid against the velvet just behind the dial.
    turned("Gold",(0,.059,0),[(0,.025),(.002,.031),(.005,.029)])
    turned("Cobalt",(0,.059,.0045),[(0,.027),(.002,.027)])
    for j in range(8):
        a=j*tau/8; tube("Gold",[(.006*cos(a),.059+.006*sin(a),.007),(.016*cos(a+.33),.059+.016*sin(a+.33),.0075),
                                (.024*cos(a),.059+.024*sin(a),.007)],.00062)
    stone((0,.059,.007),.005,.005,.003,"Ruby")
    for i in range(13):
        t=i/12;wire("Gold",(.004+t*.055,.042+.013*sin(t*5),.003),.0031,.0014 if i%2 else .002,.0007,segments=16)


def cup():
    part("Faceted rock-crystal bowl with enamelled gold foot")
    turned("Gold",(0,0,0),[(0,.042),(.005,.050),(.011,.046),(.018,.026),(.030,.014),(.042,.012),(.046,.029)],sides=24)
    turned("Ice",(0,0,.042),[(0,.028),(.016,.050),(.055,.063),(.093,.059),(.104,.056)],sides=14)
    for z,r in ((.052,.040),(.143,.059),(.148,.061)):
        wire("Gold",(0,0,z),r,r,.0024)
    turned("Ice",(0,0,.148),[(0,.057),(.008,.053),(.017,.025),(.022,.012)],sides=14)
    stone((0,0,.169),.009,.009,.012,"Ruby")
    part("Paired dragon handles and scalloped mounts")
    for k in range(10):
        a=tau*k/10
        tube("Gold",[(.061*cos(a),.061*sin(a),.144),(.054*cos(a+.22),.054*sin(a+.22),.090),
                     (.039*cos(a+.31),.039*sin(a+.31),.056)],.0015)
    for h in (-1,1):
        tube("Gold",[(h*.052,0,.080),(h*.082,0,.097),(h*.091,0,.135),(h*.079,0,.156),(h*.065,0,.148)],.0051)
        ellipsoid("Gold",(h*.073,0,.155),(.012,.005,.006));pearl((h*.071,-.004,.157),.002,"Ruby")
        rod("Gold",(h*.080,0,.158),(h*.087,0,.172),.0025,0)


def music():
    part("Geneva enamel music box with raised oval bezel")
    box("Gold",(0,0,.020),(.122,.076,.040),.007)
    box("Gold",(0,0,.0024),(.125,.077,.0048),.005)
    box("Cobalt",(0,0,.037),(.119,.071,.012),.007)
    box("Cobalt",(0,-.0382,.021),(.111,.0019,.026),.002)
    front=lambda u,v,h:(u,-.03916-h,v)
    for h in (-1,1):
        gold_scroll(front,h*.014,.021,.011,.009,h)
        gold_scroll(front,h*.036,.020,.010,.008,h)
        enamel_leaf(front,(h*.025,.017),(h*.032,.011),.0027,"Peacock")
        enamel_leaf(front,(h*.029,.024),(h*.034,.030),.0024,"Emerald")
        blossom(front,h*.045,.021,.0068,"Rose","Ivory")
    blossom(front,0,.021,.0078,"Turquoise","Pearl")
    # The central enamel boss reaches the original front-panel extent.
    rod("Gold",(0,-.03956,.021),(0,-.0399,.021),.0013,sides=10)
    for h in (-1,1):
        box("Cobalt",(h*.0613,0,.020),(.0006,.058,.022),.002)
        flank=lambda u,v,l:(h*(.06169+l),h*u,v)
        for hand in (-1,1):
            gold_scroll(flank,hand*.011,.020,.008,.007,hand)
        blossom(flank,0,.020,.0066,"Ivory","Turquoise")
    lid=lambda u,v,h:(u,v,.043+h)
    for h in (-1,1):
        for k in (-1,1):
            enamel_leaf(lid,(h*.050,k*.024),(h*.036,k*.027),.0033,"Peacock")
            enamel_leaf(lid,(h*.049,k*.023),(h*.053,k*.013),.0028,"Rose")
        gold_scroll(lid,h*.043,0,.006,.008,h)
    for r in (.025,.029):wire("Gold",(-.010,0,.044),r,r*.75,.0016)
    for h in (-1,1):
        for k in (-1,1):stone((h*.050,k*.025,.044),.004,.004,.0028,"Crystal")
    part("Gold filigree grille and miniature singing songbird")
    for i in range(10):
        a=i*tau/10; tube("Gold",[(-.01,0,.045),(-.01+.022*cos(a+.39),.014*sin(a+.39),.046),(-.01+.024*cos(a),.017*sin(a),.045)],.0007)
    for x in (-.009,.000):rod("Gold",(x,0,.045),(x,.001,.057),.0009)
    ellipsoid("Peacock",(-.006,0,.066),(.017,.008,.010));ellipsoid("Cobalt",(.006,-.0008,.078),(.008,.007,.008))
    rod("Gold",(.011,-.002,.079),(.022,-.002,.079),.002,0)
    pearl((.010,-.006,.081),.0013,"Ink")
    for h in (-1,1):
        ellipsoid("Emerald",(-.009,h*.006,.067),(.010,.0035,.005))
        for i in range(3):
            rod("Sapphire",(-.016,h*i*.002,.062),(-.038,h*(i+1)*.0025,.072+i*.001),.0018,.0003)
    # A domed lid, hinged upright behind the bird.
    turned("Gold",(-.009,.022,.065),[(0,.024),(.002,.025)],(0,1,0),oval=.79)
    disc(1,(-.009,.0218,.065),.020,(.020,0,0),(0,0,.016))


def snuff():
    part("Guilloche portrait snuffbox with pearl-set miniature")
    box("Gold",(0,0,.010),(.098,.074,.021),.009)
    box("Ruby",(0,0,.022),(.094,.070,.009),.008)
    disc(1,(0,0,.027),.028,(.029,0,0),(0,.030,0))
    beads((0,0,.027),.030,.031,45,.0012)
    lid=lambda u,v,h:(u,v,.02654+h)
    for h in (-1,1):
        for k in (-1,1):
            blossom(lid,h*.0357,k*.022,.0073,"Ivory","Rose")
            enamel_leaf(lid,(h*.037,k*.017),(h*.043,k*.009),.0031,"Peacock")
            enamel_leaf(lid,(h*.031,k*.026),(h*.018,k*.030),.0029,"Emerald")
        gold_scroll(lid,h*.0375,0,.0055,.0095,h)
    box("Sapphire",(0,-.03736,.010),(.078,.0008,.011),.001)
    apron=lambda u,v,h:(u,-.03779-h,v)
    for i in (-2,-1,0,1,2):
        gold_scroll(apron,i*.014,.010,.0057,.0041,1)
    box("Gold",(0,-.0375,.019),(.014,.005,.0032),.001)


def tiara():
    part("Platinum trellis, graduated pear drops and emerald centre")
    def axis(t):return (.100*sin(t),.069*(1-cos(t)),.012)
    for dz in (0,.007):
        tube("Silver",[(x,y,z+dz) for x,y,z in (axis(a*.045) for a in range(-31,32))],.0022)
    for j in range(-5,6):
        t=j*.255;x,y,z=axis(t);height=.055-.0063*abs(j)
        for h in (-1,1):
            tube("Silver",[(x+h*.011,y+.001,.016),(x+h*.012,y,.024),(x+h*.008,y-.001,height+.002),
                           (x,y-.001,height+.014)],.00135)
        stone((x,y-.003,.024+height*.40),.0105-abs(j)*.0008,.014-abs(j)*.001,.006,"Emerald",8,True)
        pearl((x,y,height+.015),.0030 if j%2 else .004,"Pearl")
        stone((x,y-.003,.018),.005,.004,.0033,"Crystal",8,True)
    for i in range(-18,19):
        x,y,z=axis(i*.073);pearl((x,y-.001,z),.00235,"Crystal")


def seal():
    part("Amethyst matrix and entwined serpents")
    turned("Gold",(0,0,0),[(0,.022),(.003,.024),(.008,.021),(.012,.014)],oval=.82)
    turned("Amethyst",(0,0,.010),[(0,.013),(.009,.015),(.041,.010),(.051,.006)],sides=8)
    for offset in (0,pi):
        tube("Gold",[(.017*(1-i/68)*cos(i*.18+offset),.017*(1-i/68)*sin(i*.18+offset),.010+i*.00115) for i in range(41)],.00165)
    stone((0,0,.060),.012,.010,.010,"Amethyst")
    wire("Gold",(0,0,.079),.007,.008,.0016,"xz")


def cameo():
    part("Carved onyx profile with double pearl surround")
    turned("Gold",(0,0,0),[(0,.027),(.004,.030),(.007,.029)],oval=1.28)
    turned("Carnelian",(0,0,.007),[(0,.026),(.002,.026)],oval=1.27)
    beads((0,0,.008),.028,.035,40,.0015)
    ellipsoid("Ivory",(-.002,.004,.011),(.009,.014,.003))
    face([(-.008,.015,.014),(.002,.017,.014),(.006,.010,.015),(.010,.005,.015),(.007,.003,.015),
          (.006,-.005,.014),(.001,-.008,.014),(.003,-.017,.013),(.014,-.023,.012),(-.017,-.022,.012),
          (-.005,-.012,.014),(-.005,-.005,.014),(-.011,.001,.014)],"Ivory")
    for i in range(11):
        a=i*pi/11; pearl((-.004-.008*cos(a),.006+.011*sin(a),.014),.0024,"Pearl")


def astrolabe():
    part("Pierced gilt rete, rule, throne and suspension ring")
    turned("Brass",(0,0,0),[(0,.062),(.003,.066),(.006,.066)],sides=64)
    disc(2,(0,0,.0063),.061)
    for r in (.063,.056,.050):wire("Gold",(0,0,.008),r,r,.00135)
    wire("Gold",(0,.012,.010),.039,.039,.0017)
    for i in range(12):
        a=tau*i/12
        tube("Gold",[(.052*cos(a),.052*sin(a),.009),(.039*cos(a+.30),.039*sin(a+.30),.010),
                     (.025*cos(a+.19),.025*sin(a+.19),.010)],.00105)
    rod("Gold",(-.043,-.022,.012),(.051,.026,.012),.0016);pearl((0,0,.013),.003,"Ruby")
    for h in (-1,1):tube("Gold",[(h*.020,.059,.005),(h*.010,.076,.005),(0,.081,.005)],.0022)
    wire("Gold",(0,.092,.005),.010,.009,.0019)


def globe():
    part("Three-footed clock base and celestial globe")
    for i in range(3):
        a=tau*i/3;ellipsoid("Gold",(.041*cos(a),.041*sin(a),.007),(.015,.012,.007))
    turned("Gold",(0,0,.006),[(0,.044),(.005,.048),(.011,.043),(.019,.027),(.025,.030)],sides=32)
    turned("Cobalt",(0,0,.030),[(0,.026),(.025,.020),(.032,.017)],sides=20)
    c=.121;r=.048
    ellipsoid("Cobalt",(0,0,c),(r,r,r),32)
    for t in (-.65,-.32,0,.32,.65):wire("Gold",(0,0,c+t*r),sqrt(1-t*t)*r,sqrt(1-t*t)*r,.00085)
    for j in range(8):
        a=tau*j/8
        tube("Gold",[(r*cos(a)*sin(i*pi/30),r*sin(a)*sin(i*pi/30),c+r*cos(i*pi/30)) for i in range(31)],.00085)
    for j in range(37):
        t=(j+.5)/37*2-1; a=2.4*j; h=sqrt(1-t*t)
        pearl((.048*h*cos(a),.048*h*sin(a),c+.048*t),.0015,"Crystal")
    wire("Brass",(0,0,c),.059,.059,.0025,"xz")
    wire("Gold",(0,0,c+.006),.056,.056,.0025)
    for sign in (-1,1):tube("Gold",[(sign*.018,0,.051),(sign*.052,0,.072),(sign*.054,0,.101)],.0032)
    pearl((0,0,c+.062),.006,"Gold")


def aigrette():
    part("Pearl aigrette with ruby feather vanes")
    for k in (-1,0,1):
        root=Vector((0,-.046,.004)); tip=Vector((k*.036,.046-abs(k)*.016,.014))
        tube("Silver",[root,(root+tip)*.5+Vector((.005,0,0)),tip],.0014)
        for j in range(1,8):
            p=root.lerp(tip,j/9);p.x+=.003*sin(j*pi/9)
            for h in (-1,1):
                end=p+Vector((h*.007*(1-j/12),.006,.0018))
                rod("Gold",p,end,.0008);pearl(end,.0024 if j%2 else .0028,"Pearl" if j%3 else "Ruby")
    stone((0,-.036,.006),.009,.011,.005,"Ruby")


def egg():
    part("Rose enamel lower shell and scrolled gold cradle")
    turned("Gold",(0,0,0),[(0,.029),(.004,.032),(.009,.025),(.015,.023)],sides=36)
    c=.071;h=.059;r=.041
    def surface(a,lat):
        width=r*cos(lat)*(1-.14*sin(lat))
        return (width*cos(a),width*sin(a),c+h*sin(lat))
    for half in (-1,1):
        if half==1:part("Hinged upper shell, pearl lip and diamond finial")
        for j in range(16):
            lat=((0 if half>0 else -16)+j)*pi/32
            for i in range(48):
                a=i*tau/48;b=(i+1)*tau/48
                pts=[surface(a,lat),surface(b,lat),surface(b,lat+pi/32),surface(a,lat+pi/32)]
                if half<0 and j==0:pts.pop(0)
                if half>0 and j==15:pts.pop(2)
                face(pts,"Rose",True)
        for i in range(10):
            for spiral in (-1,1):
                tube("Gold",[surface(tau*i/10+spiral*half*j*.025,half*j*.045) for j in range(31)],.00064)
        for lat in (half*.02,half*.63):
            z=surface(0,lat)[2];radius=surface(0,lat)[0]
            wire("Gold",(0,0,z),radius,radius,.0014)
            beads((0,0,z+.0004),radius,radius,40,.0011)
        if half==1:
            # Reflect a private railway collector's provenance in the inner lid.
            # Its blue face is visible after it opens around the hinge at +Y.
            disc(2,(0,0,c+.002),r*.94,(r*.94,0,0),(0,-r*.94,0))
            stone((0,0,c+h-.002),.007,.007,.007,"Crystal")
            pivot=Vector((0,r,c))
            from mathutils import Matrix
            opened=Matrix.Rotation(-1.79,3,"X")
            s.group.verts[:]=[pivot+opened@(v-pivot) for v in s.group.verts]
    part("Pearl-set pink rose cartouches")
    for i in range(4):
        a=(i+.5)*tau/4; x,y,z=surface(a,-.47)
        for j in range(5):
            t=j*tau/5;off=.0043*cos(t)
            pearl((x-sin(a)*off,y+cos(a)*off,z+.0043*sin(t)),.0031,"Pearl")
        pearl((x*1.026,y*1.026,z),.0035,"Ruby")
    part("Surprise: a gilt locomotive crossing a three-arch viaduct")
    turned("Peacock",(0,0,c-.006),[(0,r*.94),(.005,r*.96)],sides=48)
    for x in (-.030,-.010,.010,.030):
        box("Gold",(x,-.006,c+.005),(.004,.007,.010),.0006)
    for x in (-.020,0,.020):
        tube("Gold",[(x+.009*cos(t*pi/16),-.009,c+.006+.005*sin(t*pi/16)) for t in range(17)],.0011)
    box("Silver",(0,-.006,c+.012),(.067,.012,.003),.0006)
    box("Gold",(-.001,-.007,c+.017),(.043,.010,.003),.0005)
    rod("Gold",(-.010,-.007,c+.025),(.021,-.007,c+.025),.0051,sides=20)
    for z in (-.013,-.001):
        for x in (-.012,-.003,.007,.014):
            rod("Blued",(x,z,c+.017),(x,z-.0012,c+.017),.0031,sides=12)
    for z in (-.012,-.002):
        for x in (-.024,-.014):
            rod("Gold",(x,z,c+.019),(x,z,c+.032),.0008)
    box("Gold",(-.019,-.007,c+.033),(.014,.016,.0018),.0005)
    turned("Gold",(.015,-.007,c+.028),[(0,.0017),(.004,.0016),(.007,.0023)],sides=14)


def fan():
    part("Gilded pierced guards and enamelled silk fan leaf")
    n=17
    def tip(r,a,z):return (r*sin(a),r*cos(a)-.054,z)
    for i in range(n):
        a=(i/(n-1)-.5)*2.40;z=.007+.003*(i%2)
        rod("Gold",tip(.006,a,.003),tip(.136,a,z),.0017,.0022)
        if i<n-1:
            b=((i+1)/(n-1)-.5)*2.40
            face([tip(.068,b,z),tip(.068,a,z),tip(.133,a,z),tip(.133,b,.014-z+.006)],"Peacock")
            gemAt=tip(.115,a,z+.002);pearl(gemAt,.002,"Gold")
            tube("Gold",[tip(.081,a,z+.002),tip(.099,(a+b)/2,z+.004),tip(.122,b,z+.002)],.00095)
    for a in (-1.2,1.2):rod("Pearl",tip(.003,a,.006),tip(.139,a,.008),.0027,.004)
    stone((0,-.053,.008),.005,.005,.0035,"Ruby")


def sundial():
    part("Engraved boxwood diptych with inset compass and silk gnomon")
    box("Walnut",(0,0,.008),(.078,.085,.016),.004)
    box("Ivory",(0,0,.016),(.072,.080,.002),.003)
    disc(3,(0,-.002,.0172),.033)
    # An open second leaf leans back by ten degrees.
    corners=[(-.037,.041,.013),(.037,.041,.013),(.037,.053,.088),(-.037,.053,.088)]
    face(corners,"Ivory")
    for i,a in enumerate(corners):rod("Gold",a,corners[(i+1)%4],.0026)
    disc(3,(0,.046,.054),.029,(.030,0,0),(0,.003,.030))
    rod("Ink",(0,-.028,.019),(0,.050,.079),.00065)
    turned("Gold",(0,.018,.018),[(0,.010),(.001,.011)])
    rod("Blued",(-.003,.013,.020),(.004,.024,.020),.0009,.0003)


def vase():
    part("Turquoise cloisonne body with coloured lotus petals")
    profile=[(0,.023),(.005,.028),(.012,.024),(.031,.037),(.058,.042),(.078,.039),
             (.097,.028),(.111,.015),(.132,.014),(.144,.025),(.148,.026)]
    turned("Turquoise",(0,0,0),profile,sides=40)
    for z,r in ((.005,.028),(.013,.025),(.111,.016),(.143,.025),(.148,.026)):wire("Gold",(0,0,z),r,r,.00135)
    for k in range(8):
        a=k*tau/8;rad=.042
        def petal(t,z): return (rad*cos(a+t),rad*sin(a+t),z)
        for j in range(-2,3):
            center=j*.053; top=.075+.010*(1-abs(j)/2)
            pts=[petal(center-.095,.064),petal(center-.075,top),petal(center+.03,top+.003),petal(center+.095,.064),petal(0,.052)]
            face(pts,"Ruby" if abs(j)%2 else "Rose")
            tube("Gold",pts+[pts[0]],.0006,6)
        tube("Gold",[petal(t*.025,.047+.009*sin(t*.22)) for t in range(-15,16)],.00075)
    turned("Ink",(0,0,.146),[(0,.022),(.0006,.022)])


def ship():
    part("Silver-gilt nef hull, foot, rudder and gunports")
    turned("Gold",(0,0,0),[(0,.033),(.006,.036),(.014,.017),(.028,.012),(.034,.023)],oval=.58)
    levels=[]
    for r,z in ((.035,.031),(.065,.046),(.078,.075)):
        levels.append([(r*cos(a*tau/32),.027*sin(a*tau/32)*(r/.078),z+.014*abs(cos(a*tau/32))**6) for a in range(32)])
    for lo,hi in zip(levels,levels[1:]):
        for i in range(32):j=(i+1)%32;face([lo[i],lo[j],hi[j],hi[i]],"Silver")
    face(levels[-1],"Walnut");tube("Gold",levels[-1]+[levels[-1][0]],.0025)
    part("Three masts, repoussé sails and rigging")
    for x,h in ((-.039,.143),(0,.177),(.040,.152)):
        rod("Gold",(x,0,.073),(x,0,h),.0017,.0007)
        for j,(z,w,depth) in enumerate(((h-.034,.020,.029),(h-.010,.014,.019))):
            rod("Gold",(x,-w,z),(x,w,z),.0011)
            pts=[(x,-w,z),(x,w,z),(x+.004,w*.75,z-depth),(x+.004,-w*.75,z-depth)]
            mid=(x+.008,0,z-depth*.5)
            for i in range(4):face([pts[i],pts[(i+1)%4],mid],"Pearl")
        for side in (-1,1):rod("Gold",(x,0,h-.004),(x-.017,side*.021,.078),.00045)
        face([(x,0,h),(x+.014,0,h+.003),(x,0,h+.007)],"Ruby")


def flacon():
    part("Ruby glass scent flask and pierced gold cage")
    profile=[(0,.018),(.005,.024),(.014,.030),(.047,.031),(.059,.022),(.067,.013),(.074,.010)]
    turned("Ruby",(0,0,0),profile,sides=12,oval=.62)
    for i in range(10):
        a=i*tau/10
        tube("Gold",[(r*cos(a+.12*sin(z*55)),r*.62*sin(a+.12*sin(z*55)),z+.0004) for z,r in profile],.00135)
    for z,r in ((.005,.024),(.016,.030),(.060,.022),(.071,.011)):wire("Gold",(0,0,z),r,r*.62,.00135)
    def cage(a,z,lift):
        for (lo,lr),(hi,hr) in zip(profile,profile[1:]):
            if lo<=z<=hi:
                r=lr+(hr-lr)*(z-lo)/(hi-lo)
                break
        # Match the underlying twelve-sided glass, including the oval cross section.
        r=r*cos(pi/12)/cos(a%(tau/12)-pi/12)
        return ((r+lift)*cos(a),(r*.62+lift)*sin(a),z)
    for i in range(10):
        a=i*tau/10
        for sign in (-1,1):
            pts=[cage(a+sign*t*pi/10,.018+t*.021,.00025) for t in (0,.20,.40,.60,.80,1.)]
            pts.extend(cage(a+sign*(1-t)*pi/10,.039+t*.018,.00025) for t in (.25,.50,.75,1.))
            tube("Gold",pts,.00047,6)
    for i in range(5):
        a=-.43*pi+i*tau/5
        facet=lambda u,v,h:cage(a+u/.030,.035+v,.00011+h)
        for sign in (-1,1):
            enamel_leaf(facet,(0,-.001),(sign*.006,-.010),.0028,"Emerald")
        blossom(facet,0,.002,.0067,"Pearl","Turquoise")
    turned("Gold",(0,0,.073),[(0,.011),(.008,.012),(.010,.010)])
    stone((0,0,.082),.012,.009,.015,"Crystal")


ITEMS=dict(pocket_watch=watch,crystal_cup=cup,singing_bird_box=music,portrait_snuffbox=snuff,
           emerald_tiara=tiara,intaglio_seal=seal,cameo=cameo,astrolabe=astrolabe,
           astronomical_globe=globe,pearl_aigrette=aigrette,rose_egg=egg,jewelled_fan=fan,
           diptych_dial=sundial,cloisonne_vase=vase,silver_nef=ship,scent_flacon=flacon)
