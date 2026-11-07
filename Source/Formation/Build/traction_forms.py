"""Rolled sheet, castings and lathe work shared by the Pacific and tender."""
from math import cos, sin, pi

FLOOR=1.382


def axis_pt(axis,t,a,b):
    if axis=="X": return (t,a,b)
    if axis=="Y": return (a,t,b)
    return (a,b,t)


def positive(axis,loop):
    area=sum(p[0]*q[1]-q[0]*p[1] for p,q in zip(loop,loop[1:]+loop[:1]))
    return loop if (area>0)==(axis!="Y") else list(reversed(loop))


def loft(g,k,m,axis,sections):
    n=len(sections[0][1])
    verts=[axis_pt(axis,t,a,b) for t,loop in sections for a,b in positive(axis,loop)]
    faces=[tuple(reversed(range(n))),tuple(range(len(verts)-n,len(verts)))]
    for v in range(0,len(verts)-n,n):
        faces.extend((v+i,v+(i+1)%n,v+(i+1)%n+n,v+i+n) for i in range(n))
    g.mesh(k,m,verts,faces)


def plate(g,k,m,axis,a,b,loop):
    lo,hi=sorted((a,b))
    loft(g,k,m,axis,[(lo,loop),(hi,loop)])


def ellipse(a,b,ra,rb,n=32):
    return [(a+ra*cos(2*pi*i/n),b+rb*sin(2*pi*i/n)) for i in range(n)]


def frame(g,k,m,axis,a,b,outer,inner):
    """A ring extruded along an axis with an actual hole through every face."""
    a,b=sorted((a,b))
    outer,inner=positive(axis,outer),positive(axis,inner)
    n=len(outer)
    verts=[axis_pt(axis,t,y,z) for t,loop in ((a,outer),(b,outer),(a,inner),(b,inner))
           for y,z in loop]
    faces=[]
    for i in range(n):
        j=(i+1)%n
        faces.extend(((i,j,n+j,n+i),(n+i,n+j,3*n+j,3*n+i),
                      (3*n+i,3*n+j,2*n+j,2*n+i),(2*n+i,2*n+j,j,i)))
    g.mesh(k,m,verts,faces)


def turned(g,k,m,axis,center,profile,n=36,annular=False):
    """profile is a list of (axial station, radius) in the local frame."""
    verts=[]
    for t,r in profile:
        for i in range(n):
            angle=2*pi*i/n
            local=axis_pt(axis,t,r*cos(angle),r*sin(angle)*(-1 if axis=="Y" else 1))
            verts.append(tuple(x+y for x,y in zip(center,local)))
    faces=[]
    for row in range(len(profile) if annular else len(profile)-1):
        a,b=row*n,((row+1)%len(profile))*n
        for i in range(n):
            j=(i+1)%n
            faces.append((a+i,a+j,b+j,b+i))
    if not annular:
        faces.extend((tuple(reversed(range(n))),tuple(range(len(verts)-n,len(verts)))))
    g.mesh(k,m,verts,faces,True)


def pipe(g,k,m,points,r,n=10):
    for a,b in zip(points,points[1:]):
        g.cyl(k,m,a,b,r,n)


def side_text(g,k,m,text,x,side,y,z,size):
    g.text(k,m,text,(x,side*y,z),(pi/2,0,0 if side<0 else pi),size)


def end_text(g,k,m,text,x,y,z,size,sign=1):
    g.text(k,m,text,(x,y,z),(pi/2,0,sign*pi/2),size)


def wheel(g,x,r,identifier,driver=False,tyred=True):
    k=identifier
    z=r
    g.cyl(k,"steel",(x,-.91,z),(x,.91,z),.092 if driver else .070,16)
    for sign in (-1,1):
        # Conical tyre, thin flange on its gauge face, genuinely open spokes.
        y=sign*.765
        profile=[(-.065,r-.014),(-.053,r),(.042,r),(.060,r-.018),
                 (.057,r-.086),(-.059,r-.089)]
        turned(g,k,"bright" if tyred else "steel","Y",(x,y,z),profile,40 if driver else 32,True)
        turned(g,k,"steel","Y",(x,sign*.705,z),
               [(-.016,r),(-.007,r+.013),(.006,r+.013),(.016,r),
                (.016,r-.040),(-.016,r-.040)],40 if driver else 32,True)
        spokes=15 if driver else 10
        for i in range(spokes):
            a=2*pi*i/spokes
            origin=(x+.11*cos(a),y,z+.11*sin(a))
            tip=(x+(r-.071)*cos(a),y,z+(r-.071)*sin(a))
            g.beam(k,"ink" if driver else "dark",origin,tip,.049 if driver else .041,.060)
        g.cyl(k,"ink",(x,y-.090,z),(x,y+.090,z),.177 if driver else .109,24)
        g.cyl(k,"steel",(x,y+sign*.085,z),(x,y+sign*.109,z),.102 if driver else .075,20)
        if driver:
            # Cast crescent balance weight within the tyre, opposite the crank.
            offset=(.25 if sign<0 else 1.82)+pi
            outer=[(x+(r-.055)*cos(offset-.76+i*.095),z+(r-.055)*sin(offset-.76+i*.095)) for i in range(17)]
            inner=[(x+(r-.255)*cos(offset-.71+i*.089),z+(r-.255)*sin(offset-.71+i*.089)) for i in reversed(range(17))]
            plate(g,k,"ink","Y",y+sign*.031,y+sign*.080,outer+inner)


def headlamp(g,k,x,y,z,sign=-1):
    g.box(k,"ink",(x,y,z),(.133,.187,.239),.027)
    for mat,dist,rad in (("brass",.073,.087),("enamel",.087,.071),("shade",.093,.055)):
        g.cyl(k,mat,(x+sign*dist,y,z),(x+sign*(dist+.016),y,z),rad,20)
    g.box(k,"dark",(x,y,z+.146),(.109,.142,.052),.012)
    pipe(g,k,"brass",[(x,y-.059,z+.136),(x,y-.059,z+.201),
                      (x,y+.059,z+.201),(x,y+.059,z+.136)],.008,8)


def buffers(g,k,base,outer,sign):
    for y in (-.88,.88):
        g.cyl(k,"dark",(sign*base,y,1.00),(sign*(base+.264),y,1.00),.115,18)
        g.cyl(k,"steel",(sign*(base+.206),y,1.00),(sign*(outer-.034),y,1.00),.078,16)
        g.cyl(k,"dark",(sign*(outer-.054),y,1.00),(sign*outer,y,1.00),.199,26)
    g.box(k,"steel",(sign*(base+.103),0,.965),(.255,.149,.164),.024)
    for dy in (-.056,.056):
        g.beam(k,"steel",(sign*(base+.22),dy,.955),(sign*(outer-.064),dy,.815),.034,.034)
    g.cyl(k,"dark",(sign*(outer-.064),-.068,.819),(sign*(outer-.064),.068,.819),.033,12)
