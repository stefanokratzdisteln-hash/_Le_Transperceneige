"""Walkable common spaces of Winterbourne. Coordinates are Blender metres."""
from math import cos, pi, sin

FLOOR = 1.382
COVE = [(-1.458,3.443),(-1.436,3.516),(-1.365,3.572),(-1.237,3.611),
        (-1.03,3.63),(-.72,3.637),(.72,3.637),(1.03,3.63),
        (1.237,3.611),(1.365,3.572),(1.436,3.516),(1.458,3.443)]


def turned(g,group,mat,x,y,profile,n=20):
    verts=[(x+r*cos(i*2*pi/n), y+r*sin(i*2*pi/n), z) for z,r in profile for i in range(n)]
    faces=[(j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i)
           for j in range(len(profile)-1) for i in range(n)]
    g.mesh(group,mat,verts,faces+[tuple(reversed(range(n))),tuple(range(len(verts)-n,len(verts)))])


def shell_bowl(g,group,x,y,base,rx,ry,depth,seat=False):
    profile=[(.25,0),(.58,.12),(.78,.30),(.99,.78),(1,.90),(.95,1),
             (.81,.98),(.76,.83),(.53,.47),(.20,.29)]
    n=28
    rings=[[(x+rx*r*cos(2*pi*j/n),y+ry*r*sin(2*pi*j/n),base+depth*h) for j in range(n)]
           for r,h in profile]
    for i,(a,b) in enumerate(zip(rings,rings[1:])):
        g.mesh(group,'hide' if seat and i in (3,4,5) else 'enamel',a+b,
               [(j,(j+1)%n,n+(j+1)%n,n+j) for j in range(n)])
    g.cyl(group,'dark',(x,y,base+depth*.25),(x,y,base+depth*.294),rx*.13,14)


def floor_and_headlining(g):
    for i in range(24):
        x0=-9+i*.75
        g.box('Interior_Parquet','parquet',(x0+.375,0,1.373),(.75,2.915,.016))
    # A full woven repeat in each three-quarter-metre length; brass binding at the ends.
    for i in range(20):
        x=-7.20+(i+.5)*.72
        g.box('Passage_Wilton_Runner','carpet',(x,-.895,1.384),(.72,.802,.006))
    for x in (-7.204,7.204):
        g.box('Passage_Wilton_Runner','brass',(x,-.895,1.386),(.018,.82,.010),.003)
    for section in range(9):
        x0=-9+section*2
        group='Coved_headlining_%02d'%section
        # Underside faces the occupants. Fine gilt fillets pick out the coving.
        for (a,h),(b,k) in zip(COVE,COVE[1:]):
            g.mesh(group,'ivory',[(x0,a,h),(x0,b,k),(x0+2,b,k),(x0+2,a,h)],[(0,1,2,3)])
        for hand in (-1,1):
            for y,z,w,h,mat in ((1.422,3.488,.034,.050,'wood'),
                                  (1.395,3.526,.033,.015,'gilt'),(1.059,3.615,.014,.013,'brass')):
                g.box(group,mat,(x0+1,hand*y,z),(2,w,h))
        for (a,h),(b,k) in zip(COVE,COVE[1:]):
            g.beam(group,'wood',(x0+.03,a,h-.010),(x0+.03,b,k-.010),.032,.022)


def light_positions():
    lights=[]
    for i,(x,y) in enumerate([(-8.08,0),(-6.12,-.90),(-3.30,-.90),(0,-.88),
                               (3.30,-.90),(6.12,-.90),(8.08,0)]):
        lights.append(('Passage lamp %02d'%i,(x,y,3.392),(1,.77,.52),3.6,2.3,25))
    for i,x in enumerate((-5.82,-3.06,3.06,5.82)):
        lights.append(('Compartment ceiling %02d'%i,(x,.31,3.398),(1,.79,.57),2.9,1.8,17))
    for i,x in enumerate((-6.51,-3.75,3.75,6.51)):
        lights.append(('Reading light %02d'%i,(x,1.15,2.88),(1,.64,.32),1.9,.85,4.5))
    lights.extend([('Writing desk',(-.93,.87,2.60),(1,.70,.39),2.9,1.5,10),
                   ('Lavatory lamp',(1.08,.40,3.398),(1,.83,.67),2.3,1.4,13)])
    return lights


def lighting_fittings(g):
    group='Interior_Brass_and_Opaline'
    for label,(x,y,z),*rest in light_positions():
        if not (label.startswith('Passage') or 'ceiling' in label or 'Lavatory' in label):
            continue
        for mat,profile in (
                ('wood',[(3.60,.150),(3.612,.186),(3.634,.180)]),
                ('gilt',[(3.566,.134),(3.577,.167),(3.593,.178),(3.608,.153)]),
                ('shade',[(3.458,.026),(3.466,.074),(3.484,.105),(3.513,.128),(3.568,.139)]),
                ('brass',[(3.451,.027),(3.466,.051),(3.474,.043)])):
            turned(g,group,mat,x,y,profile,24)
        for k in range(4):
            a=k*pi/2+.45
            g.beam(group,'brass',(x+cos(a)*.136,y+sin(a)*.136,3.566),
                   (x+cos(a)*.120,y+sin(a)*.120,3.516),.009)


def bureau(g):
    group='Stewards_Writing_Bureau'
    for x in (-1.47,.13):
        for y in (.80,1.31):
            turned(g,group,'wood',x,y,[(1.382,.047),(1.42,.056),(1.48,.041),
                   (1.88,.039),(1.94,.057),(2.07,.055)],12)
    g.box(group,'wood',(-.67,1.055,2.015),(1.75,.615,.15),.022)
    g.box(group,'brass',(-.67,1.055,2.099),(1.80,.657,.023),.007)
    g.box(group,'wood',(-.67,1.055,2.122),(1.83,.68,.044),.014)
    g.box(group,'ink',(-.73,1.008,2.146),(1.13,.411,.008),.007)
    for x in (-1.18,-.66,-.14):
        g.box(group,'petrol',(x,.740,2.016),(.462,.030,.093),.009)
        g.cyl(group,'gilt',(x,.714,2.017),(x,.734,2.017),.024,12)
    # A porter's register with bound spine, endpapers, ruled pages and a fountain pen.
    g.box(group,'hide',(-.69,.926,2.158),(.385,.258,.017),.004)
    for hand in (-1,1):
        x=-.69+hand*.092
        g.box(group,'paper',(x,.924,2.174),(.17,.239,.026),.005)
        for n in range(8):
            g.box(group,'brass',(x,.839+n*.022,2.188),(.137,.0015,.001))
    g.cyl(group,'ink',(-.37,.862,2.158),(-.22,.91,2.158),.006,8)
    g.cyl(group,'gilt',(-.22,.91,2.158),(-.203,.916,2.158),.005,8)
    turned(g,group,'gilt',-.03,.94,[(2.145,.073),(2.153,.075),(2.162,.060),
           (2.184,.056),(2.20,.045),(2.208,.015),(2.227,.011),(2.233,.020)],20)
    # Pigeonholes and station master's keys against the real blank exterior panel.
    g.box(group,'wood',(-.65,1.392,2.814),(1.72,.058,.731),.015)
    for x in (-1.48,-.96,-.43,.18):
        g.box(group,'wood',(x,1.308,2.847),(.036,.206,.58),.009)
    for z in (2.535,2.813,3.134):
        g.box(group,'wood',(-.65,1.302,z),(1.72,.226,.044),.007)
        g.box(group,'brass',(-.65,1.184,z),(1.70,.012,.012))
    for i in range(7):
        x=-1.425+i*.056
        h=.185+.026*(i%3)
        g.box(group,('ink','velvet','hide')[i%3],(x,1.273,2.557+h/2),(.049,.12,h),.005)
        for dh in (.028,h-.029):
            g.box(group,'brass',(x,1.211,2.557+dh),(.040,.006,.007))
    for x in (-.815,-.68,-.545):
        g.cyl(group,'brass',(x,1.325,2.754),(x,1.38,2.754),.013,10)
        g.cyl(group,'gilt',(x,1.316,2.625),(x,1.316,2.749),.008,10)
        g.box(group,'gilt',(x+.011,1.316,2.635),(.03,.012,.022))
    g.box(group,'brass',(-.65,1.336,3.244),(.93,.041,.155),.012)
    g.box(group,'ink',(-.65,1.308,3.244),(.90,.018,.127),.006)
    g.text(group,'gilt',"STEWARD'S OFFICE",(-.65,1.296,3.218),(pi/2,0,0),.081)
    # Banker's lamp: a curved, dark green enamel shade with opal underside.
    x,y=-1.26,.973
    turned(g,group,'brass',x,y,[(2.146,.095),(2.156,.101),(2.178,.065),
        (2.194,.028),(2.466,.019),(2.482,.046)],18)
    g.cyl(group,'brass',(x-.12,y,2.492),(x+.12,y,2.492),.015,12)
    sections=[(-.093,2.492),(-.071,2.546),(-.024,2.574),(.031,2.568),(.084,2.528),(.093,2.488)]
    for (a,z),(b,h) in zip(sections,sections[1:]):
        g.mesh(group,'ink',[(x-.164,y+a,z),(x+.164,y+a,z),(x+.164,y+b,h),(x-.164,y+b,h)],[(0,1,2,3)])
    g.box(group,'shade',(x,y,2.491),(.314,.166,.012),.004)
    for hand in (-1,1):
        g.cyl(group,'brass',(x+hand*.163,y,2.514),(x+hand*.177,y,2.514),.036,12)
    # Upright leather clerk's chair, pulled partly out under the desk.
    chair='Stewards_Chair'
    for x in (-.92,-.47):
        for y in (.147,.53):
            g.beam(chair,'wood',(x,y,1.385),(x,y,1.82),.050)
    g.box(chair,'wood',(-.695,.343,1.798),(.52,.49,.063),.018)
    g.box(chair,'hide',(-.695,.344,1.849),(.51,.46,.095),.041)
    g.box(chair,'wood',(-.695,.134,2.018),(.521,.072,.421),.029)
    g.box(chair,'hide',(-.695,.180,2.049),(.451,.067,.290),.031)
    for hand in (-1,1):
        x=-.695+hand*.248
        g.beam(chair,'wood',(x,.432,1.877),(x,.444,2.026),.032)
        g.box(chair,'wood',(x,.311,2.035),(.052,.304,.050),.014)


def lavatory(g):
    group='Lavatory'
    # Tiled sanitary room between the writing bureau and compartment three.
    g.box(group,'wood',(.477,.55,2.485),(.075,1.79,2.20),.006)
    for i in range(6):
        for j in range(5):
            g.box(group,'enamel',(.523,-.298+i*.289+.144,1.49+j*.217),
                  (.015,.284,.212),.003)
    for y in (-.19,.29,.77,1.27):
        g.box(group,'gilt',(.432,y,2.61),(.011,.015,1.57))
    for start,end in ((.47,.584),(1.514,1.68)):
        g.box(group,'wood',((start+end)/2,-.309,2.48),(end-start,.074,2.19))
    g.box(group,'wood',(1.077,-.31,3.522),(1.22,.077,.183),.006)
    g.box(group,'brass',(1.048,-.358,3.502),(.627,.022,.117),.009)
    g.box(group,'ink',(1.048,-.372,3.502),(.60,.011,.098),.004)
    g.text(group,'gilt','LAVATORY',(1.048,-.379,3.480),(pi/2,0,0),.076)
    # W.C. door parked inside the right jamb, leaving the central opening clear.
    g.box(group,'wood',(1.55,.147,2.39),(.055,.898,2.01),.011)
    g.box(group,'wallcloth',(1.516,.147,2.649),(.015,.740,.866),.006)
    g.box(group,'gilt',(1.507,.489,2.357),(.015,.075,.038),.005)
    # High cistern, pull chain, flush tube, pedestal and horseshoe seat.
    g.box(group,'enamel',(1.065,1.322,2.895),(.595,.180,.35),.035)
    g.box(group,'wood',(1.065,1.317,3.081),(.624,.205,.034),.009)
    g.cyl(group,'bright',(1.061,1.255,1.672),(1.061,1.255,2.73),.036,14)
    g.cyl(group,'brass',(.718,1.245,2.325),(.718,1.245,2.928),.006,8)
    turned(g,group,'wood',.718,1.245,[(2.248,.024),(2.259,.033),(2.321,.016),(2.334,.022)],12)
    turned(g,group,'enamel',1.06,1.044,[(1.382,.15),(1.413,.165),(1.538,.119),(1.598,.136)],24)
    shell_bowl(g,group,1.06,.985,1.52,.245,.308,.314,True)
    # Compact hand basin, nickel tap, mirror with a brass bevel.
    shell_bowl(g,group,.653,.432,2.018,.153,.206,.139)
    g.cyl(group,'bright',(.556,.432,1.665),(.556,.432,2.025),.022,12)
    g.cyl(group,'bright',(.548,.51,2.113),(.548,.51,2.253),.012,12)
    g.cyl(group,'bright',(.548,.51,2.25),(.641,.51,2.25),.012,12)
    for mat,x,h,w in [('wood',.536,.57,.424),('gilt',.552,.545,.403),('mirror',.561,.503,.364)]:
        g.box(group,mat,(x,.45,2.695),(.016,w,h),.008)
    for x in (.65,.73,.81):
        g.box(group,'linen',(x,.96,1.392),(.075,.417,.014))


def collision_boxes():
    return [('Coach floor',(0,0,1.329),(18.02,2.96,.102)),
            ('Side Port',(0,-1.473,2.48),(18.0,.082,2.21)),
            ('Side Starboard',(0,1.473,2.48),(18.0,.082,2.21)),
            ('Coach ceiling',(0,0,3.686),(18.0,2.91,.10)),
            ('Writing bureau',(-.67,1.055,1.78),(1.81,.68,.79)),
            ('Leather chair',(-.695,.346,1.8),(.52,.51,.84)),
            ('Lavatory partition',(.477,.55,2.485),(.075,1.79,2.20)),
            ('Lavatory left jamb',(.527,-.309,2.48),(.114,.074,2.19)),
            ('Lavatory right jamb',(1.597,-.309,2.48),(.166,.074,2.19)),
            ('Lavatory open leaf',(1.55,.147,2.39),(.055,.898,2.01)),
            ('Lavatory pedestal',(1.06,.99,1.62),(.48,.60,.48)),
            ('Lavatory basin',(.653,.432,2.08),(.30,.41,.15))]


def build(g):
    floor_and_headlining(g)
    lighting_fittings(g)
    bureau(g)
    lavatory(g)
