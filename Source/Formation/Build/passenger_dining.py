"""The restaurant car's three six-cover dining bays, service hatch and galley."""
from math import cos, pi, sin

from passenger import FLOOR, CEILING, Fixture, pendant, seat, table_lamp


def setting(f,x,y,hand):
    z=2.154
    f.turn('enamel',x,y,z,[(0,.072),(.008,.088),(.020,.117),(.024,.115),(.014,.084)],18)
    f.turn('gilt',x,y,z+.022,[(0,.115),(.002,.115),(.0022,.110)],18,capped=False)
    # A folded ivory napkin sits within the hand-lined rim.
    f.box('linen',(x,y,z+.026),(.104,.062,.012),.005)
    f.box('gilt',(x,y,z+.033),(.018,.060,.006),.002)
    for shift in (-.146,.143):
        yy=y+hand*shift
        f.box('bright',(x+hand*.007,yy,z+.009),(.161,.010,.008),.003)
        f.box('brass',(x+hand*.068,yy,z+.010),(.077,.016,.010),.004)
        if shift>0:
            f.box('bright',(x-hand*.077,yy,z+.010),(.080,.019,.007),.003)
        else:
            for tine in (-.006,0,.006):
                f.box('bright',(x-hand*.083,yy+tine,z+.010),(.033,.003,.006),.001)
    # Coupe glass and a coffee cup with an open brass finger loop.
    gx,gy=x-hand*.165,y+hand*.124
    f.turn('bright',gx,gy,z,[(0,.033),(.006,.034),(.011,.012),(.072,.006),(.078,.020)],12)
    f.turn('glass',gx,gy,z,[(.075,.016),(.086,.030),(.125,.047),(.130,.045),(.091,.026)],14)
    tx,ty=x-hand*.168,y-hand*.092
    f.turn('enamel',tx,ty,z,[(0,.048),(.006,.058),(.011,.056),(.009,.043)],14)
    f.turn('enamel',tx,ty,z,[(.009,.029),(.017,.031),(.062,.041),(.066,.039),(.057,.034)],14)
    f.cyl('petrol',(tx,ty,z+.055),(tx,ty,z+.057),.033,14)
    a=(tx,ty+hand*.036,z+.058); b=(tx,ty+hand*.058,z+.054)
    c=(tx,ty+hand*.058,z+.031); d=(tx,ty+hand*.033,z+.028)
    for u,v in ((a,b),(b,c),(c,d)):
        f.cyl('enamel',u,v,.005,8)


def cloth(f,x,y,width):
    # Chamfered corners and the long lower folds let the cloth hang free.
    rings=[]
    for z,s in ((2.052,1),(2.141,1.01),(2.152,.99)):
        pts=[]
        for u,v in ((-1,-.85),(-.91,-1),(0,-1),(.91,-1),(1,-.85),
                    (1,0),(1,.85),(.91,1),(0,1),(-.91,1),(-1,.85),(-1,0)):
            hem=.034*abs(u*v) if z<2.1 else 0
            pts.append((x+u*.487*s,y+v*width*.5*s,z-hem))
        rings.append(pts)
    f.shell('linen',rings)


def dining_bay(g,l,idx,x):
    f=Fixture(g,l,'Restaurant_Bay_'+str(idx))
    for side,cy,span,seats in ((1,.870,1.045,(.624,1.122)),(-1,-1.092,.638,(-1.092,))):
        for hand in (-1,1):
            sx=x+hand*.810
            for yy in seats:
                seat(f,sx,yy,.458 if side>0 else .522,-hand,'velvet')
            l.box(f'Dining_seats_{idx}_{side}_{hand}',(sx,cy,1.930),(.61,span,1.095))
        f.turn('wood',x,cy,FLOOR,[(0,.194),(.034,.205),(.063,.142),(.092,.082),
                                  (.143,.062),(.554,.051),(.611,.087),(.670,.091)],16)
        f.box('wood',(x,cy,2.106),(.939,span-.026,.069),.021)
        l.box(f'Dining_table_{idx}_{side}',(x,cy,1.765),(.967,span,.765))
        cloth(f,x,cy,span)
        for hand in (-1,1):
            for yy in seats:
                setting(f,x+hand*.223,yy,hand)
        table_lamp(f,x,cy,2.154)
        # Slim upright menu card on the outer edge, with a gold linen weight.
        my=cy+side*span*.345
        f.box('brass',(x,my,2.161),(.133,.042,.015),.003)
        f.box('paper',(x,my,2.229),(.122,.009,.131),.003)
        f.text('MENU',(x,my-side*.007,2.240),'-y' if side>0 else '+y',.029,'ink')
    pendant(f,x,-.22,3.460,'Restaurant pendant '+str(idx),48,4.1)


def galley(g,l):
    f=Fixture(g,l,'Chefs_Galley')
    # A complete longitudinal wall separates the passenger corridor from
    # the galley. Its pantry entrance opens from the leading vestibule.
    f.solid('Galley_corridor_wall','wood',(-4.913,-.405,2.496),(4.51,.077,2.228))
    for ix in range(4):
        cx=-6.60+1.116*ix
        f.box('wallcloth',(cx,-.451,2.948),(1.015,.017,1.062),.020)
        f.box('brass',(cx,-.453,1.932),(.982,.015,.838),.011)
        f.box('wood',(cx,-.465,1.932),(.953,.015,.802),.011)
        f.box('enamel',(cx,-.359,2.574),(1.084,.012,2.025),.006)
    for zz,hh in ((1.438,.103),(2.365,.070),(3.567,.065)):
        f.box('wood',(-4.913,-.453,zz),(4.51,.041,hh),.006)
    f.box('gilt',(-4.91,-.475,2.390),(4.50,.012,.012),.003)
    # Dining-side servery. No passenger route enters the work zone.
    x=-2.66
    for label,lo,hi in (('Port',-.443,.180),('Starboard',.180,1.435)):
        if label=='Port':
            f.solid('Partition_'+label,'wood',(x,(lo+hi)/2,2.494),(.081,hi-lo,2.224),.008)
        else:
            for part,z,h in (('Lower',1.886,1.008),('Upper',3.377,.456)):
                f.solid('Hatch_'+part,'wood',(x,(lo+hi)/2,z),(.081,hi-lo,h),.006)
            for yy in (lo+.037,hi-.040):
                f.solid('Hatch_post_'+str(yy),'wood',(x,yy,2.776),(.10,.073,.775),.008)
            f.solid('Serving_ledge','wood',(x+.059,.809,2.391),(.316,1.294,.065),.018)
            f.box('gilt',(x+.219,.810,2.395),(.014,1.234,.021),.004)
    f.solid('Passage_lintel','wood',(x,-.936,3.470),(.085,.994,.281),.008)
    f.text('DINING  SALOON',(x-.045,-.936,3.446),'-x',.098)
    f.text('PASSAGE',(x+.045,-.936,3.449),'+x',.104)

    # Porcelain tiled splashbacks, contrasting dark grout and stained shelves.
    f.solid('Worktop','enamel',(-5.038,1.117,2.241),(3.93,.592,.061),.013)
    f.solid('Cupboards','ink',(-5.038,1.153,1.806),(3.871,.502,.849),.016)
    for col in range(7):
        cx=-6.68+col*.546
        f.box('wood',(cx,.894,1.884),(.505,.024,.618),.015)
        f.box('petrol',(cx,.879,1.901),(.440,.016,.502),.009)
        f.box('gilt',(cx+.125,.864,2.047),(.113,.028,.024),.008)
    for row in range(2):
        for j in range(13):
            f.box('enamel',(-6.849+j*.299,1.411,2.362+.164*row),(.292,.029,.157),.006)
    f.cyl('brass',(-6.90,.797,2.216),(-3.167,.797,2.216),.018,12)
    f.solid('Servery_return','ink',(-3.106,.497,1.808),(.627,1.262,.852),.013)
    f.solid('Servery_counter','enamel',(-3.105,.486,2.242),(.654,1.290,.061),.014)
    for yy in (.115,.658):
        f.box('wood',(-3.428,yy,1.911),(.019,.483,.557),.011)
        f.box('gilt',(-3.445,yy+.135,2.076),(.023,.093,.021),.005)
    # Coal-fired range; trivets, lidded copper saucepans, knobs and drip tray.
    f.box('dark',(-5.738,1.126,2.280),(1.487,.555,.043),.013)
    for cx,cy,r,used in ((-6.168,1.245,.150,True),(-5.512,1.241,.129,True),
                         (-6.150,.976,.102,False),(-5.580,.967,.103,False)):
        f.turn('steel',cx,cy,2.30,[(0,r),(.012,r),(.016,r*.68)],16)
        if used:
            f.turn('brass',cx,cy,2.314,[(0,r*.74),(.014,r*.84),(.167,r*.90),(.181,r*.95),
                                       (.188,r*.85),(.208,r*.53),(.212,.018)],20)
            f.cyl('petrol',(cx,cy,2.52),(cx,cy,2.54),.022,10)
            f.cyl('dark',(cx+r*.74,cy,2.439),(cx+r*1.75,cy,2.453),.018,10)
    for j in range(4):
        cx=-6.23+j*.282
        f.cyl('brass',(cx,.866,2.135),(cx,.824,2.135),.034,14)
    # Belfast sink with an open raised rim, twin taps, gooseneck and draining board.
    cx=-4.352
    f.box('dark',(cx,1.117,2.281),(.544,.401,.018),.027)
    for yy in (1.317,.915):
        f.box('enamel',(cx,yy,2.299),(.586,.037,.052),.012)
    for xx in (cx-.279,cx+.279):
        f.box('enamel',(xx,1.116,2.299),(.037,.386,.052),.010)
    for dx in (-.16,.16):
        f.cyl('gilt',(cx+dx,1.329,2.274),(cx+dx,1.329,2.444),.013,12)
        f.cyl('gilt',(cx+dx-.039,1.329,2.424),(cx+dx+.039,1.329,2.424),.009,10)
    for a,b in (((cx,1.329,2.275),(cx,1.329,2.552)),
                ((cx,1.329,2.552),(cx,1.146,2.552)),((cx,1.146,2.552),(cx,1.146,2.494))):
        f.cyl('bright',a,b,.017,12)
    for j in range(6):
        f.box('bright',(-4.977+j*.065,1.112,2.278),(.013,.36,.013),.003)
    # Cutlery board, canisters and dishes queued behind the pass.
    f.box('wood',(-3.700,1.125,2.288),(.460,.367,.032),.022)
    for i,mat in enumerate(('ivory','petrol_light','brass')):
        cy=.255+i*.227
        f.turn(mat,-3.067,cy,2.274,[(0,.075),(.012,.078),(.190,.078),(.201,.085),(.218,.066)],14)
        f.turn('gilt',-3.067,cy,2.486,[(0,.069),(.009,.07),(.013,.018)],14)
    for i in range(5):
        f.turn('enamel',-2.566,.809,2.425+i*.012,[(0,.097),(.013,.139),(.020,.137),(.014,.102)],16)
    pendant(f,-5.46,.29,3.460,'Galley opal light',41,3.5)
    pendant(f,-4.90,-.928,3.460,'Service corridor opal light',24,3.3)


def dining(g,l):
    galley(g,l)
    for i,x in enumerate((-.80,2.08,4.96),1):
        dining_bay(g,l,i,x)
    f=Fixture(g,l,'Restaurant_Service_Cabinet')
    f.solid('Buffet','wood',(6.789,1.106,1.841),(.713,.590,.918),.039)
    f.box('enamel',(6.789,1.108,2.316),(.754,.614,.048),.016)
    f.box('brass',(6.785,.800,1.958),(.61,.029,.024),.006)
    for dx in (-.194,.194):
        f.turn('bright',6.789+dx,1.142,2.345,[(0,.079),(.020,.08),(.044,.055),(.148,.091),
                                               (.19,.079),(.20,.040),(.24,.037)],16)
    l.route('Restaurant passenger passage',[(8.65,0),(7.25,-.25),(-2.08,-.26),(-2.35,-.906),
                                            (-7.50,-.906),(-8.68,0)])
    l.route('Crew vestibule to range sink and pass',[(-8.63,0),(-7.56,.265),(-6.80,.34),
                                                      (-5.88,.47),(-4.35,.47),(-3.73,.415)])
    l.route('Waiter from serving hatch to three table bays',[(-2.155,.72),(-2.145,.02),(-2.080,-.205),
                                                              (-.80,-.205),(2.08,-.205),(4.96,-.205),(6.45,-.205)])
    l.view((6.72,-.22,2.993),(-2.68,.02,2.50),23)
