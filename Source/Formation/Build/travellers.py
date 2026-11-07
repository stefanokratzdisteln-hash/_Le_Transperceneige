"""Static, separately selectable seated travellers for the open saloon."""
from math import cos, pi, sin, sqrt
from random import Random


def unit(v):
    length=sqrt(sum(q*q for q in v))
    return tuple(q/length for q in v)


def cross(a,b):
    return (a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0])


class Figure:
    def __init__(self,g,number,cx,cy,direction):
        self.g,self.name=g,f'Traveller_{number:02}'
        self.cx,self.cy,self.direction=cx,cy,direction
        self.rng=Random(8171*number+3847)
        self.pose=(number+number//5)%6
        self.style=(number*5+number//7)%8
        self.dress=self.style in (1,2,5,7)
        self.coat=('navy','moss','oxblood','tweed','teal','ochre','plum','slate')[(number*3+number//4)%8]
        self.skin=('skin_rose','skin_honey','skin_rose','skin_umber','skin_honey')[number%5]
        self.hair=('hair_dark','hair_chestnut','hair_fair','hair_silver','hair_chestnut')[(number//2+number)%5]
        self.trousers=self.coat if self.dress else ('slate','tweed','navy','moss')[number%4]
        self.lift=self.rng.uniform(-.040,.038)
        self.lean=self.rng.uniform(-.012,.042)+(.035 if self.pose<2 else 0)
        self.neck_x=-.042+self.lean
        self.yaw=self.rng.uniform(-.13,.13) if self.pose<3 else self.rng.uniform(-.52,.52)
        self.pitch=self.rng.uniform(.12,.23) if self.pose in (0,1,2) else self.rng.uniform(-.045,.09)
        self.jaw=self.rng.uniform(.91,1.06)

    def world(self,v):
        x,y,z=v
        return self.cx+self.direction*x,self.cy+self.direction*y,z

    def head(self,p):
        x,y,z=p
        xx=cos(self.pitch)*x+sin(self.pitch)*z
        zz=cos(self.pitch)*z-sin(self.pitch)*x
        return (self.neck_x+cos(self.yaw)*xx-sin(self.yaw)*y,
                sin(self.yaw)*xx+cos(self.yaw)*y,2.548+self.lift+zz)

    def mesh(self,mat,vertices,faces,smooth=False,head=False):
        verts=[self.world(self.head(p) if head else p) for p in vertices]
        self.g.mesh(self.name,mat,verts,faces,smooth)

    def rings(self,mat,rings,smooth=True,head=False,cap=True):
        n=len(rings[0]); total=n*len(rings)
        verts=[v for row in rings for v in row]
        faces=[tuple(reversed(range(n))),tuple(range(total-n,total))] if cap else []
        for k in range(len(rings)-1):
            for i in range(n):
                j=(i+1)%n
                faces.append((k*n+i,k*n+j,(k+1)*n+j,(k+1)*n+i))
        self.mesh(mat,verts,faces,smooth,head)

    def horizontal(self,mat,profile,n=12,head=False):
        """Closed horizontal contours (centre X/Y, height, fore/aft and lateral radii)."""
        self.rings(mat,[[(x+rx*cos(i*2*pi/n),y+ry*sin(i*2*pi/n),z) for i in range(n)]
                        for x,y,z,rx,ry in profile],True,head)

    def oval(self,mat,centre,radii,head=False):
        x,y,z=centre; rx,ry,rz=radii
        n=6 if max(radii)<.021 else 8
        self.horizontal(mat,[(x,y,z+rz*lift,rx*size,ry*size) for lift,size in
                             ((-.96,.24),(-.62,.79),(0,1),(.62,.79),(.96,.24))],n,head)

    def tube(self,mat,a,b,profile,n=8,head=False):
        axis=unit(tuple(y-x for x,y in zip(a,b)))
        ref=(0,1,0) if abs(axis[1])<.92 else (1,0,0)
        proj=sum(u*v for u,v in zip(ref,axis))
        u=unit(tuple(v-proj*w for v,w in zip(ref,axis)))
        v=cross(axis,u)
        rings=[]
        for t,w,d in profile:
            c=tuple(ai+(bi-ai)*t for ai,bi in zip(a,b))
            rings.append([tuple(c[k]+u[k]*w*cos(i*2*pi/n)+v[k]*d*sin(i*2*pi/n)
                                for k in range(3)) for i in range(n)])
        self.rings(mat,rings,True,head)

    def patch(self,mat,pts,head=False,normal=(1,0,0)):
        a,b,c=pts[:3]
        n=cross(tuple(q-p for q,p in zip(b,a)),tuple(q-p for q,p in zip(c,a)))
        face=list(range(len(pts)))
        if sum(u*v for u,v in zip(n,normal))<0:
            face.reverse()
        self.mesh(mat,pts,[face],False,head)


def face(p):
    # Vary jaw width between travellers. The cheek, temple and lower lip all
    # have their own contour so the silhouette also holds up in profile.
    jaw=p.jaw
    p.horizontal(p.skin,[(.019,0,.002,.039,.032*jaw),(.007,0,.019,.065,.049*jaw),
                         (-.002,0,.052,.082,.061*jaw),(-.005,0,.091,.089,.076),
                         (-.007,0,.119,.090,.078),(-.010,0,.150,.091,.076),
                         (-.016,0,.181,.090,.077),(-.018,0,.211,.080,.067),
                         (-.018,0,.233,.044,.039)],16,True)
    for hand in (-1,1):
        p.oval(p.skin,(-.016,hand*.075,.103),(.023,.013,.032),True)
        p.oval('lip',(-.018,hand*.085,.104),(.009,.004,.017),True)
        y=hand*.034
        # An almond of warm ivory and a dark iris underneath the upper lid.
        def socket(yy):
            return -.007+.090*sqrt(1-(yy/.078)**2)
        rim=[(socket(y+d)+bulge,y+d,z) for d,z,bulge in
             ((-.019,.123,.001),(-.008,.128,.002),(.007,.129,.002),
              (.019,.123,.001),(.006,.119,.002),(-.008,.119,.002))]
        p.patch('cream',rim,True)
        p.oval('ink',(socket(y)+.0027,y,.1235),(.0028,.0056,.0048),True)
        p.patch(p.hair,[(socket(y+d)+.001,y+d,z) for d,z in
                        ((-.023,.140),(-.008,.146),(.009,.145),
                         (.021,.140),(.006,.140),(-.009,.141))],True)
    # Side faces of the bridge, lateral alae and a small philtrum.
    nose=[(.082,-.008,.142),(.082,.008,.142),(.112,0,.090),(.086,-.018,.084),
          (.086,.018,.084),(.096,0,.078)]
    for ids in ((0,2,1),(0,3,2),(1,2,4),(3,5,2),(5,4,2)):
        p.patch(p.skin,[nose[i] for i in ids],True)
    p.patch('lip',[(.080,-.022,.052),(.084,-.009,.057),(.084,0,.055),
                   (.084,.008,.057),(.080,.022,.052),(.083,.009,.050),
                   (.083,-.010,.050)],True)
    p.patch('lip',[(.080,-.017,.049),(.084,0,.050),(.080,.017,.049),(.081,.003,.046)],True)


def hair_and_hat(p):
    bob=p.style in (1,2,5,7)
    rings=[]
    for row in range(5):
        row_points=[]
        for k in range(16):
            a=k*pi/8
            edge=(.161+(.025 if bob else .012)*sin(2*a)) if cos(a)>.54 else (
                  .049+.025*max(0,cos(a)) if bob else .093+.036*max(0,cos(a)))
            z=(edge,(edge+.192)/2,.191,.219,.236)[row]
            xrad=(.094,.095,.092,.070,.034)[row]
            yrad=(.084,.083,.079,.060,.031)[row]
            row_points.append((-.012+xrad*cos(a),yrad*sin(a),z))
        rings.append(row_points)
    p.rings(p.hair,rings,True,True)
    if p.style==7:
        p.oval(p.hair,(-.098,0,.107),(.042,.054,.050),True)
    if p.style in (0,4):  # trilby or round-crowned bowler
        hat='tweed' if p.style==0 else 'navy'
        p.horizontal(hat,[(.005,0,.173,.130,.111),(.002,0,.179,.135,.114),
                          (-.006,0,.190,.103,.090)],16,True)
        if p.style==0:
            crown=[(-.009,0,.184,.099,.083),(-.015,0,.238,.091,.074),
                   (-.015,0,.262,.074,.057),(-.012,0,.256,.060,.040)]
        else:
            crown=[(-.010,0,.181,.099,.086),(-.014,0,.224,.089,.078),
                   (-.016,0,.253,.069,.062),(-.015,0,.270,.031,.033)]
        p.horizontal(hat,crown,16,True)
        p.horizontal('black',[(-.011,0,.189,.101,.086),(-.013,0,.207,.100,.085)],16,True)
    elif p.style in (1,5):  # turned-down cloche, contrasting ribbon and cockade
        shade='plum' if p.style==1 else 'moss'
        p.horizontal(shade,[(.003,0,.157,.115,.104),(-.006,0,.166,.113,.101),
                            (-.010,0,.185,.103,.094),(-.016,0,.227,.090,.079),
                            (-.017,0,.249,.060,.053),(-.016,0,.255,.032,.029)],16,True)
        p.horizontal('oxblood',[(-.009,0,.175,.107,.096),(-.011,0,.194,.104,.093)],16,True)
        p.oval('ochre',(.006,-.098,.185),(.018,.009,.017),True)
    elif p.style==3:  # wool flat cap with a short articulated peak
        p.horizontal('leather',[(.054,0,.169,.085,.082),(.049,0,.176,.092,.082)],14,True)
        p.horizontal('tweed',[(-.008,0,.171,.110,.084),(-.022,0,.202,.116,.092),
                              (-.039,0,.229,.083,.071),(-.041,0,.235,.044,.045)],16,True)
    elif p.style==2:
        p.horizontal('teal',[(-.010,0,.187,.094,.078),(-.034,.009,.206,.110,.094),
                             (-.037,.017,.233,.081,.069),(-.042,.015,.241,.041,.035)],16,True)


def lower_body(p):
    for side in (-1,1):
        hip=(-.073,side*.087,1.998)
        knee=(.318+p.rng.uniform(-.025,.018),side*(.094+p.rng.uniform(0,.025)),1.917)
        ankle=(knee[0]+p.rng.uniform(-.044,.067),side*.106,1.465)
        leg='slate' if p.dress else p.trousers
        p.tube(leg,knee,ankle,[(0,.061,.063),(.26,.050,.060),(.63,.043,.049),(1,.035,.039)],10)
        if not p.dress:
            p.tube(leg,hip,knee,[(0,.086,.079),(.22,.086,.087),(.72,.066,.080),(1,.066,.061)],10)
            p.oval(leg,knee,(.066,.060,.061))
        xx,yy,_=ankle
        p.horizontal('black',[(xx+.034,yy,1.388,.100,.044),(xx+.032,yy,1.401,.106,.047)],12)
        p.horizontal('leather',[(xx+.035,yy,1.400,.104,.046),(xx+.039,yy,1.421,.103,.045),
                                (xx+.025,yy,1.445,.073,.040),(xx,yy,1.475,.043,.036)],12)
    if p.dress:
        p.horizontal(p.coat,[(.274,0,1.650,.097,.153),(.258,0,1.710,.124,.158),
                             (.165,0,1.900,.216,.160),(.078,0,1.988,.235,.163),
                             (-.045,0,2.060,.121,.146)],16)


def jacket(p):
    height=p.lift; lean=p.lean
    profile=[(-.057,0,1.927,.121,.146),(-.058,0,2.011,.136,.158),
             (-.060+lean*.34,0,2.168+height*.3,.110,.139),
             (-.045+lean*.67,0,2.309+height*.6,.119,.155),
             (-.047+lean*.90,0,2.413+height*.87,.109,.180),
             (-.046+lean,0,2.472+height,.078,.133),(-.045+lean,0,2.488+height,.059,.073)]
    p.horizontal(p.coat,profile,16)
    p.horizontal(p.skin,[(p.neck_x,0,2.458+height,.044,.043),(p.neck_x,0,2.565+height,.043,.041)],12)
    # Silk shirt, contrasting lapels and the narrow tie/scarf sit on the
    # curved coat front, which slopes from the sternum to the neck.
    def stripe(mat,levels):
        for (x0,z0,w0),(x1,z1,w1) in zip(levels,levels[1:]):
            p.patch(mat,[(x0,-w0,z0),(x0,w0,z0),(x1,w1,z1),(x1,-w1,z1)])
    stripe('linen',[(.072+lean*.64,2.304+height*.6,.004),
                    (.075+lean*.77,2.369+height*.73,.041),
                    (.070+lean*.91,2.423+height*.90,.061),
                    (.031+lean,2.481+height,.045)])
    for side in (-1,1):
        p.patch('cream' if p.dress else p.trousers,
                [(.029+lean,side*.058,2.485+height),(.057+lean*.84,side*.095,2.434+height*.87),
                 (.071+lean*.84,side*.071,2.411+height*.82),(.073+lean*.8,side*.079,2.395+height*.76),
                 (.080+lean*.6,side*.009,2.300+height*.56)])
    if not p.dress:
        stripe('oxblood' if p.coat!='oxblood' else 'ochre',
               [(.078+lean*.64,2.306+height*.6,.004),(.081+lean*.80,2.375+height*.78,.015),
                (.072+lean*.92,2.427+height*.89,.009),(.052+lean,2.458+height,.014)])
    else:
        p.oval('brass',(.075+lean*.81,.055,2.405+height*.82),(.006,.013,.013))
    for z,x in ((2.233,.057+lean*.5),(2.147,.054+lean*.27)):
        p.oval('brass',(x,-.014,z+height*.45),(.005,.008,.008))


def paper_prop(p,wide,folded=False):
    yspan=.155 if wide else .093
    x0=.224 if wide else .200
    z0=2.162+p.lift*.45
    height=.215 if wide else .139
    if folded:
        yspan=.081
        height=.172
    def point(side,u,v,pull=0):
        return (x0+(.031 if wide else .025)*v-.039*u-pull,side*yspan*u,z0+v*height+.008*u)
    for side in (-1,1):
        corners=[point(side,0,0),point(side,1,0),point(side,1,1),point(side,0,1)]
        p.patch('paper' if wide else 'oxblood',corners,normal=(1,0,0))
        inside=[point(side,.025,.028,.003),point(side,.965,.028,.003),
                point(side,.965,.970,.003),point(side,.025,.970,.003)]
        p.patch('paper',inside,normal=(-1,0,0))
        # Printed masthead and broken columns without expensive font geometry.
        for col in range(2):
            lo=.07+col*.44
            for line in range(5):
                v=.19+.117*line
                hi=lo+.31-.045*((line+col)%3)
                p.patch('ink',[point(side,lo,v,.004),point(side,hi,v,.004),
                               point(side,hi,v+.012,.004),point(side,lo,v+.012,.004)],normal=(-1,0,0))
        if wide:
            for start,end in ((.11,.45),(.53,.87)):
                p.patch('ink',[point(side,start,.820,-.001),point(side,end,.820,-.001),
                               point(side,end,.874,-.001),point(side,start,.874,-.001)])


def arms(p):
    reading=p.pose in (0,1,2)
    for side in (-1,1):
        shoulder=(-.047+p.lean*.90,side*.156,2.404+p.lift)
        elbow=(.020,side*.185,2.138+p.lift*.5)
        if reading:
            width=.155 if p.pose==0 else (.081 if p.pose==2 else .093)
            wrist=(.179,side*(width+.012),2.165+p.lift*.45)
            palm=(.195,side*width,2.187+p.lift*.45)
        elif p.pose==3 and side>0:
            elbow=(.088,.172,2.171+p.lift*.4)
            wrist=(.094,.081,2.470+p.lift)
            palm=(.083,.050,2.517+p.lift)
        elif p.pose==4:
            wrist=(.163,side*.052,2.048)
            palm=(.190,side*.022,2.052)
        else:
            wrist=(.145,side*.157,2.105)
            palm=(.180,side*.149,2.097)
        p.tube(p.coat,shoulder,elbow,[(-.07,.055,.058),(.12,.065,.064),(.55,.052,.057),(1,.045,.044)],8)
        p.oval(p.coat,elbow,(.047,.044,.048))
        p.tube(p.coat,elbow,wrist,[(0,.046,.045),(.28,.045,.047),(.82,.034,.036),(1,.033,.033)],8)
        p.tube('linen',elbow,wrist,[(.947,.034,.034),(1.047,.031,.033)],8)
        p.tube(p.skin,wrist,palm,[(.28,.023,.025),(.62,.023,.023),(1,.019,.018)],8)
        p.oval(p.skin,palm,(.039 if not reading else .025,.022,.021 if not reading else .032))
        p.oval(p.skin,(palm[0]+.010,palm[1]-side*.017,palm[2]+.010),(.017,.011,.018))
    if reading:
        paper_prop(p,p.pose!=1,p.pose==2)


def build(g):
    number=0
    for row in range(8):
        cx=-6.40+row*1.83
        facing=1 if row%2==0 else -1
        for hand in (-1,1):
            for cy in (.724,1.176):
                number+=1
                p=Figure(g,number,cx,hand*cy,facing)
                lower_body(p)
                jacket(p)
                face(p)
                hair_and_hat(p)
                arms(p)
