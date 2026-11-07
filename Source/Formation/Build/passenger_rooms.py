"""Six-seat drawing rooms and double-tier shared sleeper accommodation."""
from math import cos, pi, sin

from passenger import FLOOR, CEILING, Fixture, case, pendant, seat, table_lamp


STILE_Y = -.368
WALL_Y = .535
INTERIOR_WIDTH = 1.814
WALL_HEIGHT = CEILING-FLOOR


def partition(f,x):
    """Two decorated faces of a transverse compartment wall."""
    f.solid('Wall','wood',(x,WALL_Y,(FLOOR+CEILING)/2),(.075,INTERIOR_WIDTH,WALL_HEIGHT))
    for side in (-1,1):
        f.box('wallcloth',(x+side*.043,WALL_Y,2.980),(.012,1.738,1.095),.005)
        for y in (-.015,.538,1.091):
            f.box('petrol',(x+side*.046,y,1.907),(.019,.465,.806),.009)
            f.box('brass',(x+side*.056,y,1.907),(.008,.439,.774),.003)
            f.box('wood',(x+side*.063,y,1.907),(.013,.419,.751),.008)
        for z,h in ((1.433,.101),(2.356,.084),(3.568,.070)):
            f.box('wood',(x+side*.050,WALL_Y,z),(.035,INTERIOR_WIDTH,h),.007)
        f.box('gilt',(x+side*.070,WALL_Y,2.388),(.009,1.776,.014),.003)


def corridor_front(f,a,b,door_x,label,door_id):
    """Sliding leaf parked in front of its fixed panel and reachable from the corridor."""
    clear=.955
    for suffix,left,right in (('Left',a,door_x-clear/2),('Right',door_x+clear/2,b)):
        if right-left<.016:
            continue
        width=right-left
        cx=(left+right)/2
        f.solid('Front_'+suffix,'wood',(cx,STILE_Y, (FLOOR+CEILING)/2),
                (width,.079,WALL_HEIGHT))
        if width>.32:
            for side in (-1,1):
                f.box('wallcloth',(cx,STILE_Y+side*.044,2.933),(width-.081,.011,1.002),.007)
                f.box('petrol',(cx,STILE_Y+side*.045,1.894),(width-.082,.016,.791),.010)
                f.box('brass',(cx,STILE_Y+side*.054,1.894),(width-.107,.008,.766),.004)
                f.box('wood',(cx,STILE_Y+side*.061,1.894),(width-.130,.008,.742),.005)
        for z,h in ((1.439,.114),(2.371,.077),(3.566,.072)):
            f.box('wood',(cx,STILE_Y,z),(width,.104,h),.003)
        f.box('gilt',(cx,STILE_Y,2.395),(width,.119,.015),.003)
    head=3.368
    f.solid('Lintel','wood',(door_x,STILE_Y,(head+CEILING)/2),(clear,.100,CEILING-head),.004)
    f.box('ink',(door_x,-.427,3.490),(.741,.026,.145),.011)
    f.text(label,(door_x,-.444,3.463),'-y',.080)

    # Door has a framed privacy panel and a recessed grip on either face.
    sx=door_x-.982
    leaf=Fixture(f.g,f.l,door_id)
    centre=(sx,-.454,2.387)
    size=(.971,.049,1.984)
    glazed=door_id.startswith('DrawingRoomDoor_')
    if glazed:
        # Real clear opening in the joinery. The glass is parented to the sliding leaf.
        low,high=2.837-.349,2.837+.349
        for bottom,top in ((centre[2]-size[2]/2,low),(high,centre[2]+size[2]/2)):
            leaf.box('wood',(sx,centre[1],(bottom+top)/2),(size[0],size[1],top-bottom),.004)
        stile=(size[0]-.694)/2
        for hand in (-1,1):
            leaf.box('wood',(sx+hand*(.347+stile/2),centre[1],2.837),(stile,size[1],high-low),.004)
    else:
        leaf.box('wood',centre,size,.013)
    for side in (-1,1):
        y=-.454+side*.029
        if glazed:
            for hand in (-1,1):
                leaf.box('brass',(sx+hand*.354,y,2.837),(.022,.016,.722),.003)
                leaf.box('brass',(sx,y,2.837+hand*.355),(.719,.016,.023),.003)
        else:
            leaf.box('gilt',(sx,y,2.837),(.780,.012,.794),.022)
            leaf.box('ink',(sx,y+side*.010,2.837),(.752,.011,.766),.020)
            leaf.box('wallcloth',(sx,y+side*.017,2.837),(.686,.012,.695),.019)
        leaf.box('petrol',(sx,y,1.898),(.779,.014,.716),.016)
        leaf.box('brass',(sx,y+side*.010,1.898),(.741,.007,.675),.011)
        leaf.box('wood',(sx,y+side*.017,1.898),(.717,.012,.650),.010)
        leaf.box('gilt',(sx+.358,y+side*.019,2.263),(.063,.022,.184),.013)
        leaf.box('ink',(sx+.358,y+side*.031,2.264),(.037,.005,.121),.008)
    f.l.door(door_id,centre,size,(.982,0,0))


def six_seat_room(f,a,b,n):
    mid=(a+b)/2
    corridor_front(f,a,b,mid,'COMPARTMENT '+str(n),f'DrawingRoomDoor_{n:02}')
    for hand in (-1,1):
        x=a+.471 if hand<0 else b-.471
        facing=-hand
        for y in (-.04,.535,1.110):
            seat(f,x,y,.552,facing,'velvet' if n%2 else 'ink',True)
        f.l.box(f.name+'_Settee_'+str(hand),(x,.535,1.969),(.604,1.745,1.174))
        # Brass-net shelf above the settee, with brackets and a token valise.
        rx=x+hand*.186
        for yy in (-.191,.534,1.266):
            f.cyl('gilt',(rx+hand*.197,yy,3.082),(rx-hand*.123,yy,3.275),.013,10)
            f.cyl('brass',(rx+hand*.192,yy,3.275),(rx-hand*.128,yy,3.275),.014,10)
        for dx in (-.114,-.005,.103):
            f.cyl('brass',(rx+dx,-.223,3.281),(rx+dx,1.299,3.281),.013,10)
        f.cyl('wood',(rx-hand*.128,-.221,3.340),(rx-hand*.128,1.300,3.340),.016,12)
        if (n+hand)%3:
            # A compact overnight case is supported completely by the rack.
            f.box('hide',(rx,.72,3.394),(.244,.568,.187),.025)
            for yy in (.562,.879):
                f.box('petrol',(rx,yy,3.395),(.250,.027,.195),.006)
                f.box('brass',(rx-hand*.128,yy,3.426),(.011,.041,.032),.003)
    # Fixed window table folds against the wall, leaving a standing aisle.
    f.solid('Writing_table','wood',(mid,1.225,2.237),(.695,.396,.055),.022)
    for xx in (mid-.21,mid+.21):
        f.cyl('brass',(xx,1.397,1.994),(xx,1.114,2.207),.016,12)
    table_lamp(f,mid,1.267,2.265)
    f.box('paper',(mid+.194,1.151,2.271),(.178,.128,.011),.004)
    pendant(f,mid,.525,3.459,'Drawing-room light '+str(n),45,3.6)
    f.l.route('Enter drawing room '+str(n),[(mid,-.904),(mid,.20),(mid,.70)])


def pillow(f,x,y,z,width):
    rings=[]
    for dz,sc in ((0,.86),(.026,1),(.076,.90),(.097,.73)):
        ring=[]
        for j in range(16):
            c,s=cos(j*pi/8),sin(j*pi/8)
            xx=(1 if c>=0 else -1)*abs(c)**.48
            yy=(1 if s>=0 else -1)*abs(s)**.48
            ring.append((x+.182*sc*xx,y+width*.46*sc*yy,z+dz))
        rings.append(ring)
    f.shell('linen',rings,True)


def bunks(f,a,n):
    length,width=1.972,.561
    x=a+.099+length/2
    foot=x+length/2
    for j,cy in enumerate((-.019,1.122)):
        inward=1 if j==0 else -1
        for tier,z in enumerate((1.776,2.653)):
            bedname=f'Stack_{j+1}_{tier+1}'
            f.solid(bedname,'wood',(x,cy,z),(length,width,.097),.024)
            f.box('brass',(x,cy+inward*width*.50,z+.011),(length-.049,.017,.023),.005)
            f.box('linen',(x,cy,z+.093),(length-.039,width-.018,.117),.041)
            f.box('velvet' if n in (1,3) else 'ink',(x+.174,cy,z+.152),
                  (length-.423,width+.004,.046),.018)
            f.box('linen',(x-.415,cy,z+.177),(.115,width-.015,.020),.008)
            for offset in (.420,.495):
                f.box('brass',(x+offset,cy,z+.176),(.016,width-.012,.008),.002)
            pillow(f,x-.719,cy,z+.148,width)
            # A folded berth curtain on its own rail, parked at the pillow end.
            # Both faces and the rolled edge are visible from inside the compartment.
            curtain=Fixture(f.g,f.l,f"BerthCurtain_{(n-1)*4+j*2+tier+1:02}")
            rail_y=cy+inward*(width*.5-.026)
            top=z+.790
            curtain.cyl('brass',(x-.964,rail_y,top),(x+.947,rail_y,top),.011,12)
            cloth=[]
            for k,(h,w) in enumerate(((.19,.235),(.396,.106),(.595,.212),(.773,.337))):
                for q in range(11):
                    t=q/10
                    cloth.append((x-.925+t*w,rail_y-inward*(.022+.019*cos(t*pi*6)),
                                  z+h+(.014*cos(t*3*pi) if k==0 else 0)))
            back=[(a,b-inward*.009,h) for a,b,h in cloth]
            faces=[(j,j+1,j+12,j+11) for k in range(3) for q in range(10) for j in [k*11+q]]
            panel=faces+[tuple(44+i for i in reversed(face)) for face in faces]
            curtain.g.mesh(curtain.name,'velvet' if n%2 else 'ink',cloth+back,panel)
            for pos in (0,.5,1):
                xx=x-.925+pos*.337
                curtain.cyl('brass',(xx,rail_y,z+.766),(xx,rail_y,top+.001),.008,8)
            curtain.box('gilt',(x-.872,rail_y+inward*.003,z+.397),(.12,.010,.019),.004)
            if tier:
                # Raised guard, leaving a gap at the foot opposite the ladder.
                yy=cy+inward*(width/2-.014)
                for dx in (-.756,.381):
                    f.cyl('gilt',(x+dx,yy,z+.009),(x+dx,yy,z+.278),.014,12)
                f.cyl('gilt',(x-.756,yy,z+.278),(x+.381,yy,z+.278),.017,12)
                f.cyl('brass',(x-.756,yy,z+.214),(x+.381,yy,z+.214),.008,10)
        for yoff in (-.153,.153):
            f.cyl('brass',(foot+.076,cy+yoff,FLOOR+.018),
                  (foot+.036,cy+yoff,2.832),.019,12)
        for rung in range(5):
            z=1.613+rung*.241
            xx=foot+.073-(z-FLOOR)*.027
            f.cyl('wood',(xx,cy-.162,z),(xx,cy+.162,z),.020,12)
        f.l.box(f.name+'_Ladder_'+str(j),(foot+.054,cy,2.115),(.086,.360,1.464))
        for sx in (x-.729,x+.740):
            f.box('petrol',(sx,cy,1.574),(.120,width-.044,.364),.011)


def shared_sleeping_room(f,a,b,n):
    door=a+2.532
    corridor_front(f,a,b,door,'BERTHS  %02d - %02d'%(n*4-3,n*4),f'SharedSleeperDoor_{n:02}')
    bunks(f,a,n)
    # Four individual draped berth cards and head-end reading lamps.
    for j,cy in enumerate((-.019,1.122)):
        for k,z in enumerate((2.151,3.052)):
            xx=a+.060
            f.box('ink',(xx,cy,z),(.019,.258,.105),.008)
            f.text('%02d'%(4*n-3+j*2+k),(xx+.012,cy,z-.025),'+x',.076)
            f.cyl('gilt',(a+.055,cy+.13,z+.143),(a+.148,cy+.13,z+.143),.022,12)
            f.turn('shade',a+.143,cy+.13,z+.101,[(0,.047),(.016,.050),(.077,.030)],12)
    pendant(f,door,.563,3.459,'Shared sleeping-room light '+str(n),32,3.0)
    f.l.route('Enter shared berths '+str(n),[(door,-.905),(door,.553),(a+1.765,.553),(a+.400,.553)])


def washroom(f,a,b):
    door=a+1.510
    corridor_front(f,a,b,door,'LAVATORY','SharedWashroomDoor')
    # Wash table and recessed oval bowl, enamelled sanitary fittings, mirror.
    x=a+.461
    f.solid('Washstand','wood',(x,1.152,1.817),(.762,.530,.870),.027)
    f.box('enamel',(x,1.145,2.275),(.791,.561,.055),.017)
    # A deep curved inner bowl with a dark drain.
    rings=[]
    for z,sc in ((2.306,1),(2.326,1.03),(2.330,.93),(2.313,.76),(2.302,.35)):
        ring=[(x+.238*sc*cos(j*pi/12),1.130+.153*sc*sin(j*pi/12),z) for j in range(24)]
        rings.append(ring)
    f.shell('enamel',rings,True)
    f.cyl('steel',(x,1.130,2.305),(x,1.130,2.310),.022,12)
    for dx in (-.126,.126):
        f.cyl('gilt',(x+dx,1.330,2.307),(x+dx,1.330,2.449),.013,12)
        f.cyl('gilt',(x+dx-.041,1.330,2.440),(x+dx+.041,1.330,2.440),.009,10)
        f.cyl('gilt',(x+dx,1.330,2.419),(x+dx,1.245,2.419),.012,10)
    f.box('gilt',(x,1.405,2.912),(.607,.039,.681),.046)
    f.box('mirror',(x,1.377,2.912),(.557,.018,.632),.034)
    # Cistern, chain and water closet against the opposite bulkhead.
    px,py=b-.322,.858
    f.turn('enamel',px,py,1.385,[(0,.139),(.035,.147),(.194,.100),(.251,.134),
                                  (.315,.180),(.377,.197),(.399,.201)],24)
    f.turn('wood',px,py,1.783,[(0,.196),(.020,.205),(.025,.162),(.012,.151)],24)
    f.cyl('dark',(px,py,1.794),(px,py,1.798),.144,20)
    f.solid('Water_closet','enamel',(b-.141,py,2.166),(.155,.388,.393),.038)
    f.l.box('Lavatory_bowl',(px,py,1.590),(.46,.425,.415))
    f.cyl('brass',(b-.073,py+.220,2.376),(b-.073,py+.220,2.064),.006,8)
    pendant(f,door,.477,3.461,'Lavatory light',23,2.6)
    f.l.route('Reach shared washstand',[(door,-.905),(door,.126),(x,.423)])


def accommodation(number,g,l):
    if number==4:
        walls=[-7.16+i*3.58 for i in range(5)]
    else:
        walls=[-7.16]+[-5.08+i*3.06 for i in range(5)]
    for i,x in enumerate(walls):
        partition(Fixture(g,l,'Passenger_bulkhead_'+str(i)),x)
    for i,(a,b) in enumerate(zip(walls,walls[1:])):
        if number==4:
            six_seat_room(Fixture(g,l,'Drawing_compartment_'+str(i+1)),a,b,i+1)
        elif i==0:
            washroom(Fixture(g,l,'Shared_washroom'),a,b)
        else:
            shared_sleeping_room(Fixture(g,l,'Four_berth_compartment_'+str(i)),a,b,i)
    f=Fixture(g,l,'Corridor_Globes')
    for idx,x in enumerate((-5.40,-1.8,1.8,5.40)):
        pendant(f,x,-.917,3.460,'Corridor light '+str(idx+1),23,3.1)
    l.route('Side corridor',[(-8.76,0),(-7.81,-.9),(7.79,-.9),(8.76,0)])
    if number==4:
        l.view((-5.37,-.266,2.979),(-6.57,.923,2.349),19)
    else:
        l.view((-2.526,.527,2.947),(-4.771,.570,2.162),18)
