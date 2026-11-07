"""The stores van and guard's brake van. All dimensions are metres at rail level."""
from math import cos, pi, sin

from interior import turned
from stock import fixture


def notice(g,group,words,x,y,z,width,size,side=-1):
    """An enamel board mounted on the inner side of a cupboard/wall."""
    g.box(group,'brass',(x,y,z),(width,.023,size*1.33),.012)
    g.box(group,'ink',(x,y+side*.016,z),(width-.027,.014,size*1.10),.006)
    g.text(group,'bone',words,(x,y+side*.025,z-size*.24),
           (pi/2,0,0 if side<0 else pi),size)


def bulkhead(g,l,x,caption):
    group=caption+' partition'
    for h in (-1,1):
        g.box(group,'wood',(x,h*1.008,2.485),(.087,.886,2.19),.009)
        l.box(group+str(h),(x,h*1.008,2.485),(.087,.886,2.19))
        for face in (-1,1):
            for z,t,material in ((1.455,.10,'petrol'),(2.26,.055,'brass'),(3.42,.06,'petrol')):
                g.box(group,material,(x+face*.052,h*1.005,z),(.019,.87,t),.004)
    g.box(group,'wood',(x,0,3.516),(.090,1.25,.195),.008)
    l.box(group+' lintel',(x,0,3.516),(.09,1.25,.195))
    for side in (-1,1):
        g.box(group,'ink',(x+side*.052,0,3.515),(.019,1.02,.152),.007)
        g.text(group,'gilt',caption.upper(),(x+side*.066,0,3.473),(pi/2,0,side*pi/2),.103)


def shelf(g,l,group,x,y,width,depth,zs):
    side=1 if y>0 else -1
    center=(x,y,(zs[-1]+1.38)/2)
    l.box(group,center,(width,depth,zs[-1]-1.38+.08))
    for dx in (-width/2+.04,width/2-.04):
        for dy in (-depth/2+.035,depth/2-.035):
            g.box(group,'ink',(x+dx,y+dy,(zs[-1]+1.40)/2),(.060,.060,zs[-1]-1.38),.007)
    for z in zs:
        g.box(group,'wood',(x,y,z),(width,depth,.045),.010)
        g.box(group,'brass',(x,y-side*depth/2,z+.002),(width,.018,.022),.003)
    for ix in range(round(width/.85)):
        dx=(ix+.5)*width/round(width/.85)-width/2
        g.beam(group,'brass',(x+dx,y+side*(depth*.45),zs[-1]-.27),
               (x+dx,y-side*(depth*.41),zs[-1]-.021),.027)


def signal_lamp(g,group,x,y,z,red=False):
    """Foot, reservoir, ventilated hood, ruby or clear lens and a real bail handle."""
    turned(g,group,'ink',x,y,[(z,.10),(z+.025,.113),(z+.044,.092),(z+.105,.087)],16)
    g.box(group,'dark',(x,y,z+.194),(.177,.167,.194),.021)
    for a,b,rad,mat in ((.082,.103,.080,'brass'),(.102,.112,.063,'red' if red else 'shade')):
        g.cyl(group,mat,(x,y-a,z+.197),(x,y-b,z+.197),rad,20)
    for a,b,rad,mat in ((.276,.302,.10,'ink'),(.302,.337,.073,'steel'),(.337,.351,.083,'ink')):
        g.cyl(group,mat,(x,y,z+a),(x,y,z+b),rad,16)
    path=[(x-.083*cos(i*pi/10),y,z+.349+.072*sin(i*pi/10)) for i in range(11)]
    for a,b in zip(path,path[1:]):
        g.cyl(group,'brass',a,b,.007,7)


def trunk(g,group,x,y,z,w,d,h,finish='hide',tag=True):
    g.box(group,finish,(x,y,z+h/2),(w,d,h),min(h*.16,.046))
    g.box(group,'wood',(x,y,z+h*.77),(w+.005,d+.008,.018),.007)
    for hand in (-1,1):
        xx=x+hand*w*.32
        g.box(group,'petrol',(xx,y,z+h/2),(.037,d+.014,h+.005),.008)
        for face in (-1,1):
            g.box(group,'brass',(xx,y+face*(d/2+.012),z+h*.63),(.054,.016,.049),.005)
        for end in (-1,1):
            g.box(group,'brass',(x+hand*(w/2-.027),y+end*(d/2-.021),z+h-.019),
                  (.059,.052,.040),.011)
    for dx in (-.056,.056):
        g.cyl(group,'brass',(x+dx,y,z+h),(x+dx,y,z+h+.035),.009,10)
    g.cyl(group,'petrol',(x-.059,y,z+h+.036),(x+.059,y,z+h+.036),.016,10)
    if tag:
        g.beam(group,'brass',(x+.03,y,z+h+.032),(x+.12,y-.082,z+h+.013),.006)
        g.box(group,'paper',(x+.132,y-.109,z+h+.009),(.070,.11,.005),.002)
        for j in range(3):
            g.box(group,'ink',(x+.132,y-.14+j*.014,z+h+.0125),(.042,.003,.002))


def folded_linen(g,group,x,y,z,w,d,mat,stacks):
    for j in range(stacks):
        shift=.009*sin(j*3.7+x)
        base=z+.033+j*.058
        g.box(group,mat,(x+shift,y,base),(w-.007*(j%3),d,.066),.019)
        g.box(group,'bone',(x+shift,y-d/2+.004,base+.006),(w-.052,.010,.008),.003)
        if j%2==0:
            for dx in (-w*.34,-w*.30):
                g.box(group,'brass',(x+dx,y,base+.034),(.006,d-.034,.002))


def bench(g,l):
    group='Artificers repair bench'
    x,y=4.89,1.02
    l.box(group,(x,y,1.83),(2.98,.70,.90))
    for xx in (3.49,6.26):
        for yy in (.735,1.31):
            g.box(group,'ink',(xx,yy,1.785),(.081,.081,.81),.009)
    g.box(group,'wood',(x,y,1.59),(2.85,.62,.061),.008)
    g.box(group,'wood',(x,y,2.219),(3.03,.71,.11),.021)
    g.box(group,'dark',(4.16,y,2.280),(.88,.61,.018),.007)
    for xx in (4.90,5.52,6.09):
        g.box(group,'ink',(xx,.719,2.05),(.56,.22,.202),.010)
        g.box(group,'brass',(xx,.594,2.057),(.16,.044,.021),.004)
    # Bench vice, moving jaw, screw, sliding tommy bar.
    turned(g,group,'ink',4.0,.80,[(2.288,.118),(2.306,.126),(2.334,.085)],18)
    g.box(group,'steel',(4.00,.798,2.382),(.27,.132,.114),.014)
    for xx in (3.912,4.106):
        g.box(group,'ink',(xx,.798,2.460),(.058,.166,.149),.009)
        g.box(group,'bright',(xx+(1 if xx<4 else -1)*.031,.798,2.498),(.009,.162,.046))
    g.cyl(group,'bright',(3.76,.798,2.388),(4.081,.798,2.388),.022,14)
    g.cyl(group,'bright',(3.753,.769,2.307),(3.753,.827,2.494),.008,9)
    # Individually shaped open-jaw spanners on a tool board with brass hooks.
    g.box(group,'ink',(5.14,1.389,2.887),(2.41,.065,.79),.024)
    for k in range(6):
        xx=4.23+k*.325
        z=2.678+.018*k
        a=(xx,1.335,z)
        b=(xx+.04,1.335,3.038+.021*k)
        g.beam(group,'steel',a,b,.034,.014)
        for h in (-1,1):
            g.beam(group,'bright',b,(b[0]+h*.048,1.335,b[2]+.036),.030,.016)
            g.beam(group,'bright',(b[0]+h*.048,1.335,b[2]+.036),
                   (b[0]+h*.057,1.335,b[2]+.081),.021,.016)
        g.cyl(group,'brass',(xx+.012,1.357,z+.045),(xx+.012,1.317,z+.045),.010,10)
    notice(g,group,'FITTER’S BENCH',5.11,1.345,3.37,1.27,.121)
    for i in range(3):
        trunk(g,group,4.2+i*.79,1.02,1.633,.65,.39,.23,'petrol_light',False)


def stores(g,l):
    bulkhead(g,l,-2.48,'Linen Room')
    bulkhead(g,l,2.52,'Workshop')
    for x,name in [(-4.79,'Linen'),(.1,'Lamp room'),(4.91,'Workshop')]:
        fixture(g,l,x,-.10,name+' ceiling')
    for side in (-1,1):
        center=-4.89
        group='Linen press '+('west' if side>0 else 'east')
        y=side*1.14
        zs=[1.582,2.077,2.615,3.164]
        shelf(g,l,group,center,y,3.45,.53,zs)
        for j,z in enumerate(zs[:3]):
            for k in range(5):
                xx=center+(k-2)*.647
                folded_linen(g,group,xx,y,z+.028,.52,.403,('linen','bone','ivory')[(j+k)%3],3+(k+j)%3)
        notice(g,group,'CLEAN LINEN' if side>0 else 'BLANKETS',center,y-side*.275,3.220,1.29,.105,-side)
    group='Lamp stores'
    shelf(g,l,group,-2.02,1.15,.82,.47,[1.62,2.154,2.724,3.226])
    for j,z in enumerate((1.649,2.183,2.753)):
        for k in range(2):
            signal_lamp(g,group,-2.22+k*.40,1.11,z,(k+j)%3==1)
    notice(g,group,'LAMP STORES',-2.02,.898,3.27,.80,.087)
    group='Wet locker'
    g.box(group,'steel',(2.0,-1.14,1.416),(.88,.50,.055),.016)
    l.box(group,(2.0,-1.17,2.05),(.88,.43,1.33))
    g.box(group,'ink',(2.0,-1.378,2.363),(.88,.06,1.76),.014)
    for j in range(2):
        x=1.72+j*.28
        g.cyl(group,'wood',(x,-1.068,1.535),(x+.089,-1.316,3.021-j*.057),.016,10)
        g.box(group,'wood',(x,-1.082,1.506),(.237,.083,.055),.012)
        for k in range(7):
            g.beam(group,'linen' if j%2 else 'brass',(x+(k-3)*.031,-1.08,1.492),
                   (x+(k-3)*.037,-1.065,1.421),.024,.042)
    turned(g,group,'steel',2.277,-1.01,[(1.446,.102),(1.461,.110),(1.704,.147),
           (1.719,.145),(1.721,.134),(1.464,.095)],20)
    notice(g,group,'WET LOCKER',2.0,-1.328,3.295,.82,.086,1)
    bench(g,l)
    group='Workshop bins'
    shelf(g,l,group,4.83,-1.13,2.72,.51,[1.56,2.04,2.47])
    for row,z in enumerate((1.589,2.069)):
        for j in range(5):
            x=3.78+j*.526
            g.box(group,'petrol_light',(x,-1.14,z+.13),(.449,.38,.25),.011)
            g.box(group,'brass',(x,-.943,z+.138),(.124,.011,.060),.005)
            g.box(group,'paper',(x,-.935,z+.138),(.107,.008,.042),.002)
    l.view((-6.68,-.38,2.95),(-2.5,.40,2.36),21)
    l.route('Stores through route',[(-9.64,0),(-6.75,0),(-2.48,0),(2.52,0),(7.2,0),(9.64,0)])
    l.route('Issue linen and repair kit',[(-5.1,.64),(-2.52,0),(-.5,.56),(2.50,0),(4.5,.34)])


def scale(g,l):
    group='Parcels weighing machine'
    x,y=2.26,-.96
    l.box(group,(x,y,1.668),(1.12,.76,.55))
    g.box(group,'ink',(x,y,1.457),(1.11,.76,.142),.037)
    g.box(group,'steel',(x,y,1.549),(1.05,.713,.052),.022)
    for j in range(9):
        g.box(group,'dark',(x+(j-4)*.105,y,1.578),(.010,.64,.004))
    g.box(group,'ink',(x,-1.270,2.038),(.092,.102,.958),.019)
    for mat,a,b,r in [('ink',-1.34,-1.18,.248),('brass',-1.186,-1.159,.222),('enamel',-1.160,-1.150,.197)]:
        g.cyl(group,mat,(x,a,2.607),(x,b,2.607),r,40)
    for k in range(25):
        a=(-.75+1.5*k/24)*pi
        def tick(r):
            return (x+r*sin(a),-1.145,2.607+r*cos(a))
        g.beam(group,'ink',tick(.146 if k%4==0 else .164),tick(.181),.008 if k%4==0 else .004,.005)
    g.beam(group,'red',(x,-1.130,2.607),(x-.11,-1.130,2.723),.012,.004)
    g.cyl(group,'brass',(x,-1.15,2.607),(x,-1.125,2.607),.017,12)
    g.text(group,'ink','LBS',(x,-1.14,2.523),(pi/2,0,pi),.049)
    trunk(g,group,x+.08,y,1.582,.59,.35,.31)


def guard_room(g,l):
    group='Guard duty room'
    bulkhead(g,l,3.10,'Guard')
    x,y=5.39,1.10
    l.box(group+' desk',(x,y,1.807),(2.245,.60,.848))
    for dx in (-.98,.98):
        for yy in (.86,1.33):
            g.box(group,'wood',(x+dx,yy,1.777),(.075,.077,.78),.011)
    g.box(group,'wood',(x,y,2.201),(2.246,.607,.075),.017)
    g.box(group,'ink',(x,y,2.243),(1.25,.457,.009),.004)
    # The duty log opens to two ruled pages under the guard's timing watch.
    g.box(group,'hide',(5.52,1.04,2.261),(.492,.323,.026),.007)
    for hand in (-1,1):
        cx=5.52+hand*.118
        g.box(group,'paper',(cx,1.04,2.282),(.218,.298,.019),.005)
        for k in range(9):
            g.box(group,'brass',(cx,.926+k*.026,2.293),(.171,.002,.0015))
    g.cyl(group,'ink',(5.06,.986,2.255),(5.25,1.025,2.255),.007,9)
    signal_lamp(g,group,6.24,1.105,2.238,True)
    notice(g,group,"GUARD'S JOURNAL",5.38,1.365,2.557,1.30,.104)
    g.box(group,'ink',(4.99,-1.23,1.66),(1.42,.376,.54),.021)
    g.box(group,'velvet',(4.99,-1.19,1.965),(1.46,.418,.101),.038)
    l.box('Duty bench',(4.99,-1.21,1.694),(1.46,.42,.619))
    # Cast handbrake wheel with actual rim, spokes and a freely standing screw column.
    x,y=6.64,-.954
    l.box('Brake stand',(x,y,1.95),(.60,.49,1.13))
    turned(g,group,'dark',x,y,[(1.382,.169),(1.412,.176),(1.464,.102),(1.976,.080),
                              (2.031,.113),(2.070,.064),(2.265,.048)],18)
    turned(g,group,'brass',x,y,[(2.235,.103),(2.278,.098),(2.290,.054)],16)
    points=[(x+.264*cos(i*pi/18),y+.264*sin(i*pi/18),2.303) for i in range(36)]
    for i,p in enumerate(points):
        g.cyl(group,'ink',p,points[(i+1)%len(points)],.022,8)
        if i%6==0:
            g.beam(group,'steel',(x,y,2.264),p,.034,.016)
    g.cyl(group,'wood',(x+.239,y,2.309),(x+.239,y,2.429),.020,12)
    notice(g,group,'HAND BRAKE',6.50,-1.381,2.77,.91,.092,1)
    for j,(xx,colour) in enumerate(((3.62,'red'),(3.91,'ink'))):
        g.cyl(group,'wood',(xx,1.31,2.51),(xx,1.31,3.085),.013,10)
        g.mesh(group,colour,[(xx,1.30,3.07),(xx+.185,1.26,3.022),
                (xx+.204,1.274,2.805),(xx,1.30,2.85)],[(0,3,2,1),(0,1,2,3)])


def brake_van(g,l):
    for x,name in [(-5.50,'Luggage'),(-1.80,'Consignment'),(1.82,'Scale'),(5.30,'Guard')]:
        fixture(g,l,x,0,name+' ceiling')
    for side in (-1,1):
        group='Checked baggage '+str(side)
        y=side*1.109
        shelf(g,l,group,-4.79,y,4.68,.60,[1.52,2.231,2.91])
        for row,z in enumerate((1.548,2.259)):
            for j in range(5):
                x=-6.63+j*.887+.05*row
                if (j+row+side)%4==0:
                    turned(g,group,'hide',x,y,[(z,.214),(z+.020,.222),(z+.246,.222),
                           (z+.250,.232),(z+.278,.232),(z+.281,.215)],24)
                    g.box(group,'brass',(x,y-side*.22,z+.17),(.050,.019,.045),.003)
                else:
                    trunk(g,group,x,y,z,.744-(j%2)*.08,.466,.31+.045*((row+j)%3),
                          ('hide','wood','ink')[(j+row)%3])
        notice(g,group,'CHECKED LUGGAGE',-4.81,y-side*.306,2.948,1.60,.116,-side)
    group='Parcel dispatch'
    shelf(g,l,group,-2.0,1.15,.78,.44,[1.63,2.285,2.872])
    for j in range(1):
        for k,z in enumerate((1.658,2.313)):
            x=-2.0
            w=.448+.056*((j+k)%2)
            g.box(group,'paper',(x,1.133,z+.149),(w,.342,.292),.010)
            for mx in (-1,1):
                g.box(group,'hide',(x+mx*w*.25,1.133,z+.148),(.010,.346,.298),.003)
            g.box(group,'hide',(x,1.133,z+.148),(w+.002,.012,.298),.002)
            g.box(group,'ivory',(x+.02,.956,z+.134),(.125,.009,.079))
    notice(g,group,'FELLBRIDGE',-2.0,.918,2.921,.76,.083)
    scale(g,l)
    guard_room(g,l)
    l.view((2.77,.21,2.966),(-2.64,-.02,2.24),22)
    l.route('Luggage and guard through route',[(-9.65,0),(-7.11,0),(-1.92,0),(3.10,0),(7.24,0),(9.65,0)])
    l.route('Bag to scale and guard journal',[(-5.31,.54),(-2.25,0),(1.04,-.291),(3.12,0),(5.37,.49)])


def build(number,g,l):
    (stores if number==3 else brake_van)(g,l)
