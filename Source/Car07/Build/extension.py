"""Longitudinal inserts in the carriage frame; keep the original joinery at 1:1.

Coordinates throughout this module use the coachbuilder's axes: X towards the
guard, Y across the track, Z above the rail. The drawings and Unity colliders
are derived from the same coordinates.
"""
from math import cos, pi, sin

CUT = 7.26
INSERT = 4.87
OUTER = 2.40
FLOOR = 1.38


def cutaway_wings(scene):
    """Open just the roof and camera-facing side of the new rooms for a section plate."""
    import bmesh
    import bpy
    changed=[]
    for obj in scene.objects:
        if obj.type!='MESH' or not obj.name.startswith(('Expansion_', 'Stillage passage','Lamp inspection aisle')):
            continue
        original=obj.data
        copy=original.copy()
        geometry=bmesh.new()
        geometry.from_mesh(copy)
        cut=[face for face in geometry.faces if all(v.co.z>3.45 for v in face.verts)
             or all(v.co.y>OUTER-.18 for v in face.verts)]
        bmesh.ops.delete(geometry,geom=cut,context='FACES')
        geometry.to_mesh(copy)
        geometry.free()
        obj.data=copy
        changed.append((obj,original,copy))
    def restore():
        for obj,original,copy in changed:
            obj.data=original
            bpy.data.meshes.remove(copy)
    return restore


def station(x, n):
    if n == 1:
        return x - 2.10 + (4.20 if x > 5.65 else 0)
    cut, extra = (2.78, 1.80) if n == 2 else (CUT, INSERT)
    return x + (extra if x > cut else -extra if x < -cut else 0)


def point(p, n):
    return (station(p[0], n), p[1], p[2])


def _half(poly, plane, right):
    result = []
    previous = poly[-1]
    for current in poly:
        a, b = previous[0]-plane, current[0]-plane
        if a*b < 0:
            t = a/(a-b)
            # Retain the source edge identity. Two different boards can touch
            # at the same point without sharing a vertex or opposite end faces.
            support=previous[3]|current[3]
            result.append((plane,previous[1]+t*(current[1]-previous[1]),
                            previous[2]+t*(current[2]-previous[2]),support))
        if b >= 0 if right else b <= 0:
            result.append(current)
        previous = current
    return result


def _open_side(data):
    vertices, faces, paints, smooth = data
    output = ([], [], [], [])
    lookup = {}
    for face, paint, normal in zip(faces, paints, smooth):
        poly = [(*vertices[i],frozenset((i,))) for i in face]
        lo, hi = min(p[0] for p in poly), max(p[0] for p in poly)
        for a, b, shift in ((-1e4,-CUT,-INSERT),(-CUT,CUT,0),(CUT,1e4,INSERT)):
            if lo >= b or hi < a: continue
            fragment = _half(poly,a,True) if lo < a else poly
            if fragment and hi > b: fragment = _half(fragment,b,False)
            if len(fragment) < 3: continue
            indices=[]
            for x,y,z,support in fragment:
                position=(round(x+shift,7),round(y,7),round(z,7))
                key=(position,support)
                if key not in lookup:
                    lookup[key]=len(output[0])
                    output[0].append(position)
                indices.append(lookup[key])
            if len(set(indices))<3: continue
            output[1].append(tuple(indices));output[2].append(paint);output[3].append(normal)
    return output


def insert_frame(g, n):
    for group, batch in list(g.batches.items()):
        vertices = batch[0]
        if n >= 3 and (group.startswith('Side_panels_') or group in ('Interior_Corridor_lining','Interior_Cabin_lining','Interior_Parquet')):
            g.batches[group] = _open_side(batch)
        elif n >= 3 and group.startswith(('Wheelset_','Bogie_')):
            offset = -INSERT if group.split('_')[1][0]=='A' else INSERT
            vertices[:] = [(x+offset,y,z) for x,y,z in vertices]
        elif n == 2 and group == 'Tender_fill_and_hatch':
            vertices[:] = [(x+1.80,y,z) for x,y,z in vertices]
        else:
            vertices[:] = [point(v,n) for v in vertices]


def expand_layout(l):
    n=l.number
    for box in l.boxes:
        centre,size=box['center'],box['size']
        if n==2 and box['name']=='Rear_filler':
            centre[2]+=1.80
            continue
        a=station(centre[2]-size[2]/2,n)
        b=station(centre[2]+size[2]/2,n)
        if n>=3 and box['name'] in ('Side Port','Side Starboard'):
            a,b=-CUT,CUT
        centre[2]=(a+b)/2
        size[2]=b-a
    l.ends=tuple(station(k,n) for k in l.ends)
    l.pivots={k:point(v,n) for k,v in l.pivots.items()}
    l.lights=[(name,point(p,n),w,c,r) for name,p,w,c,r in l.lights]
    for route in l.routes:
        for p in route['points']: p['z']=station(p['z'],n)
    if l.camera:
        a,b,f=l.camera
        l.camera=(point(a,n),point(b,n),f)


NAMES = {
    3:('LINEN DISPATCH','ARTIFICER\'S STORE'),
    4:('CORRESPONDENCE ROOM','DRAWING ROOM'),
    5:('READING SALOON','LUGGAGE RECESS'),
    6:('SILVER PANTRY','PRIVATE DINING'),
    7:('MORNING ROOM','STEWARD\'S LINEN'),
    8:('BERTH LUGGAGE','LINEN EXCHANGE'),
    9:('VALUABLES IN TRANSIT','SORTING OFFICE')
}


def joinery(g,l,sign):
    n=l.number
    x=sign*(CUT+INSERT/2)
    title=NAMES[n][int(sign>0)]
    group='Expansion_'+title.title().replace(' ','_')
    half=OUTER-.06
    def box(mat,dx,y,z,s,d,h,b=0):
        g.box(group,mat,(x+dx,y,z),(s,d,h),b)
    def solid(key,mat,dx,y,z,s,d,h,b=0):
        box(mat,dx,y,z,s,d,h,b)
        l.box(title+' '+key,(x+dx,y,z),(s,d,h))
    # Cantilevered oak floor and sloped roof shoulders with a full-width,
    # human-scale doorway back into both adjoining vestibules.
    box('parquet' if n in (4,5,6,7,8) else 'wood',0,0,1.355,INSERT,2*half,.050)
    l.box('Extension floor '+str(sign),(x,0,1.325),(INSERT,2*half,.11))
    for hand in (-1,1):
        y=hand*half
        steel=n in (3,9)
        solid('side '+str(hand),'ink' if steel else 'petrol',0,y,1.89,INSERT,.115,1.09,.012)
        box('wood',0,y-hand*.031,2.43,INSERT,.184,.097,.012)
        box('gilt',0,y-hand*.078,2.464,INSERT,.032,.018)
        box('dark',0,y,3.55,INSERT,.118,.29)
        l.box(title+' glazing '+str(hand),(x,y,2.95),(INSERT,.064,1.01))
        # Four original, recessed panes with fixed mullions and a transom.
        for i in range(5):
            u=-INSERT/2+i*INSERT/4
            box('wood',u,y,2.953,.071,.143,1.01,.008)
            box('brass',u,y-hand*.077,2.956,.013,.016,1.01)
        box('wood',0,y,3.225,INSERT,.090,.042,.005)
        box('bright',0,y-hand*.047,3.224,INSERT,.014,.010)
        box('ivory',0,y,3.452,INSERT,.138,.088)
        box('brass',0,y+hand*.065,1.58,INSERT,.014,.021)
        for i in range(4):
            dx=(i-1.5)*INSERT/4
            box('wood',dx,y-hand*.061,1.92,1.095,.046,.71,.028)
            box('petrol' if steel else 'wallcloth',dx,y-hand*.087,1.925,.956,.012,.573,.028)
        for end in (-1,1):
            if n in (3,9) and hand==-1 and end==-sign:
                continue
            edge=end*INSERT/2
            wing=half-1.447
            solid('return '+str(hand)+' '+str(end),'wood',edge,hand*(half-wing/2),2.49,.082,wing,2.22,.014)
            box('petrol',edge-end*.057,hand*(half-wing/2),2.48,.032,wing-.10,1.91,.014)
            box('brass',edge-end*.080,hand*(half-wing/2),2.91,.016,wing-.17,.021)
        # The roof's rolled outer shoulder meets the existing clerestory above the centre aisle.
        eave=(((x-INSERT/2,hand*1.42,3.88),(x+INSERT/2,hand*1.42,3.88),
               (x+INSERT/2,hand*(half+.095),3.70),(x-INSERT/2,hand*(half+.095),3.70)))
        g.mesh(group,'dark',eave,[(0,1,2,3) if hand>0 else (3,2,1,0)])
        box('wood',0,hand*(1.42+(half-1.42)/2),3.624,INSERT,half-1.42+.13,.051)
        box('brass',0,hand*(half+.073),3.66,INSERT,.029,.019)
    for edge in (-1,1):
        for h in (-1,1):
            xx=x+edge*2.15
            g.beam(group,'steel',(xx,h*1.08,1.06),(xx,h*2.28,1.31),.096,.112)
    box('petrol',0,half-.073,3.497,2.64,.035,.206,.012)
    g.text(group,'gilt',title,(x,half-.102,3.448),(pi/2,0,0),.116)
    for dx in (-1.45,1.45):
        box('brass',dx,0,3.586,.24,.16,.063,.024)
        box('shade',dx,0,3.533,.18,.135,.072,.026)
    l.lamp(title+' lamps',(x,0,3.18),32,(1,.79,.50),4.25)
    if n==7 and sign<0:
        from carbody import _curtain,_lamp
        for h in (-1,1):
            g.cyl(group,'gilt',(x-2.34,h*2.219,3.355),(x+2.34,h*2.219,3.355),.018,14)
            for i in range(4):
                for hand in (-1,1):
                    _curtain(g,group,h,x+(i-1.5)*INSERT/4,hand,.91,.83)
        _lamp(g,group,x+.04,.87,2.205)
        # A wine-coloured Persian runner beside the writing table, between
        # the entrance and the arm of the window settee.
        box('carpet',0,-.30,1.382,3.82,1.05,.004)
        for h in (-1,1):
            box('brass',0,-.30+h*.48,1.385,3.73,.013,.001)
            box('brass',h*1.86,-.30,1.385,.016,.97,.001)
    if n==4:
        # Reeded walnut pilasters and stepped brass fanlights of the drawing room.
        for h in (-1,1):
            for i in (-1,0,1):
                u=x+i*INSERT/4
                for p in (-1,0,1):
                    g.cyl(group,'wood',(u+p*.022,h*2.256,2.429),(u+p*.022,h*2.256,3.412),.012,10)
                for t in (-2,-1,0,1,2):
                    g.beam(group,'brass',(u,h*2.245,3.178),
                           (u+t*.111,h*2.245,3.365-.017*abs(t)),.010,.009)
    # Leave a through route on each side of the island in the selected lounges.
    if n==4 or (n==7 and sign<0):
        for h in (-1,1):
            solid('settee '+str(h),'velvet',-.45,h*1.93,1.70,1.58,.53,.58,.071)
            box('hide',-.45,h*2.16,1.98,1.64,.105,.80,.039)
            box('gilt',-.45,h*2.153,2.38,1.61,.112,.030,.012)
            for side in (-1,1): box('wood',-.45+side*.80,h*1.94,1.91,.078,.54,.24,.027)
    if n==4 or (n in (5,7) and sign<0) or (n==6 and sign>0):
        # Reading/dining table uses a real table-height top, four slender legs
        # and working knee-clearance. The two side aisles meet behind it.
        table_y=.72
        solid('tabletop','parquet',.30,table_y,2.172,1.08,.58,.066,.027)
        for h in (-1,1):
            for e in (-1,1): box('wood',.30+e*.433,table_y+h*.21,1.764,.066,.066,.764,.008)
        box('gilt',.30,table_y,2.139,1.082,.583,.013,.005)
        if n in (4,5,7):
            box('hide',.32,table_y,2.212,.43,.28,.013,.010)
            box('paper',.31,table_y-.013,2.224,.29,.199,.010,.003)
            for i in range(5): box('ink',.30,table_y-.07+i*.028,2.230,.18-(i%2)*.04,.005,.001)
    else:
        # Wall furniture stays shallow; a porter can carry a case around the
        # freestanding sorting table or turn into a sightline behind a locker.
        solid('sideboard','wood',.38,1.97,1.823,1.91,.53,.882,.025)
        box('brass',.38,1.97,2.273,1.98,.56,.025,.006)
        for e in (-1,1):
            u=.38+.475*e
            box('petrol_light',u,1.695,1.862,.87,.026,.638,.028)
            box('brass',u,1.657,2.038,.215,.051,.028,.009)
        if n in (7,8) or (n==3 and sign<0):
            for i in range(5):
                box('linen' if i%2 else 'ivory',-.13+.029*(i%2),1.99,2.30+i*.046,.62,.37,.048,.015)
        if n in (3,5,8,9):
            # Narrow cabinets stop short at both ends to offer a second approach.
            for e in (-1,1):
                solid('rack upright '+str(e),'wood',e*.79,-1.56,2.087,.055,.395,1.414,.007)
            for h in (1.52,2.09,2.64):
                solid('rack shelf %.2f'%h,'wood',0,-1.56,h,1.67,.39,.043,.007)
                box('gilt',0,-1.35,h+.008,1.67,.018,.022,.004)
        if n==3:
            from service import signal_lamp
            for dx,level in ((-.47,2.114),(.17,2.114),(-.26,2.664),(.45,2.664)):
                signal_lamp(g,group,x+dx,-1.55,level,dx<0)
        if n==9:
            from service import trunk
            for dx,level,w,depth,h,material in (
                (-.39,1.544,.67,.36,.37,'hide'),(.38,1.544,.62,.34,.30,'petrol'),
                (.47,2.114,.55,.32,.34,'hide'),(-.18,2.664,.72,.35,.29,'velvet')):
                trunk(g,group,x+dx,-1.55,level,w,depth,h,material)
        if n==6:
            # Chased silver service under a cloth-lined canopy above the pass.
            box('wood',0,2.245,2.91,2.92,.105,.81,.014)
            for row in range(6):
                u=x+(row-2.5)*.39
                g.cyl(group,'ivory',(u,2.177,2.91),(u,2.122,2.91),.158,24)
                g.cyl(group,'gilt',(u,2.121,2.91),(u,2.115,2.91),.127,24)
                g.cyl(group,'bone',(u,2.115,2.91),(u,2.111,2.91),.117,24)
            for h in (2.52,3.30):box('brass',0,2.147,h,2.91,.13,.031,.008)
        if n==3 and sign>0:
            for i in range(5):
                u=x+(i-2)*.20
                g.cyl(group,'bright',(u,2.18,2.52),(u+.053,2.18,2.91),.016,10)
                for h in (-1,1):
                    g.beam(group,'bright',(u+.049,2.18,2.88),(u+.053+h*.046,2.18,2.973),.025,.024)
        if n==9:
            # The valuables room has a riveted strongbox; the opposite sorting
            # office has a brass dispatch rail and sloping, labelled paper bins.
            if sign<0:
                box('dark',-.31,2.005,2.484,.54,.40,.395,.035)
                box('steel',-.31,1.795,2.485,.48,.034,.323,.021)
                g.cyl(group,'brass',(x-.28,1.764,2.48),(x-.28,1.735,2.48),.066,18)
                for i in range(6):
                    a=i*pi/3
                    g.cyl(group,'bright',(x-.28+.049*cos(a),1.732,2.48+.049*sin(a)),
                          (x-.28+.050*cos(a),1.725,2.48+.050*sin(a)),.003,6)
            else:
                for i in range(5):
                    box('wood',-.59+i*.23,2.17,2.47,.026,.26,.35,.003)
                box('wood',-.13,2.27,2.49,.96,.058,.39,.007)
                for i in range(4):box('paper',-.48+i*.23,2.12,2.42,.149,.059,.239,.004)
    if n==4 and sign>0:
        # Two openings around a frosted quarter-height screen separate the
        # conversation recess without turning passage through it into a dead end.
        for h in (-1,1):
            bx=1.62
            box('wood',bx,h*1.64,1.79,.088,.62,.818,.013)
            l.box(title+' screen '+str(h),(x+bx,h*1.64,2.143),(.09,.62,1.52))
            for edge in (-1,1):box('gilt',bx,h*1.64+edge*.29,2.55,.064,.042,.74,.005)
            box('gilt',bx,h*1.64,2.91,.072,.63,.046,.009)
    if n==8 and sign<0:
        for i in range(2):
            u=(i-.5)*.66
            solid('bed box '+str(i),'hide',u,-2.06,1.566,.55,.35,.35,.034)
            for e in (-1,1):box('brass',u+e*.19,-2.064,1.737,.029,.344,.013,.002)
    # On freestanding tables and sideboards the same placements serve physical
    # examination as well as the normal game search. Origins stay train-local.
    return group


def coach(g,l):
    for flank,hand in (('Port',-1),('Starboard',1)):
        for sign in (-1,1):
            lo,hi=CUT+INSERT,9+INSERT
            l.box('Vestibule side '+flank+str(sign),(sign*(lo+hi)/2,hand*1.473,2.48),
                  (hi-lo,.082,2.21))
    for sign in (-1,1): joinery(g,l,sign)
    if l.number in (3,9):
        for sign in (-1,1):
            dispatch_aisle(g,l,sign)
    if l.number==7:
        # Every compartment has a different purpose and a landmark visible
        # through its doorway. Objects use the existing lining and window bays.
        for x,label in [(-6.51,'DRESSING'),(-3.75,'CORRESPONDENCE'),(3.75,'TRAVELLER'),(6.51,'CALLING ROOM')]:
            group='Cabin_'+label
            g.box(group,'wood',(x,1.344,3.409),(.69,.048,.132),.016)
            g.text(group,'gilt',label,(x,1.314,3.374),(pi/2,0,0),.073)
            if x==-6.51:
                for v in range(2):
                    for mat,h,r in [('glass',.087,.021),('brass',.018,.010)]:
                        base=2.60 if mat=='glass' else 2.687
                        g.cyl(group,mat,(x+.21+v*.064,1.32,base),(x+.21+v*.064,1.32,base+h),r,16)
            elif x==-3.75:
                g.box(group,'hide',(x-.12,1.21,2.596),(.31,.17,.012),.008)
                g.box(group,'paper',(x-.10,1.20,2.606),(.18,.108,.006),.002)
            elif x==3.75:
                g.box(group,'hide',(x-.16,1.22,2.625),(.192,.138,.068),.017)
                g.box(group,'brass',(x-.16,1.147,2.637),(.035,.011,.024),.003)
            else:
                g.box(group,'paper',(x+.18,1.12,2.597),(.132,.082,.010),.004)


def dispatch_aisle(g,l,sign):
    # The stores or baggage porter can pass down the offside of the shelving.
    # Keep the middle loading doors and both end vestibules in their original
    # positions. The sealed wing ends carry the dispatcher’s fast-reference board.
    end=2.82
    length=CUT-end+.03
    x=sign*(CUT+end)/2
    inner,outer=1.44,OUTER-.06
    mid=(inner+outer)/2
    group=('Stillage passage' if l.number==9 else 'Lamp inspection aisle')+(' fore' if sign<0 else ' aft')
    def part(key,material,position,size,bevel=0):
        g.box(group,material,position,size,bevel)
        l.box(key,position,size)
    part('Extension floor '+group,'parquet',(x,-mid,FLOOR-.034),(length,outer-inner,.068))
    part(group+' panelling','petrol',(x,-outer,1.88),(length,.079,1.0),.012)
    part(group+' end wall','wood',(sign*end,-mid,2.52),(.09,outer-inner,2.28),.010)
    l.box(group+' window glass',(x,-outer,2.893),(length,.035,.99))
    part(group+' ceiling','dark',(x,-mid,3.63),(length,outer-inner+.10,.084))
    g.box(group,'wood',(x,-outer,3.49),(length,.087,.22),.008)
    g.box(group,'brass',(x,-outer+.048,2.39),(length,.026,.032),.005)
    g.box(group,'wood',(x,-outer+.012,3.113),(length,.069,.038),.004)
    for i in range(5):
        dx=(i-2)*length/4
        g.box(group,'wood',(x+dx,-outer,2.89),(.062,.115,1.02),.007)
    for dx in (-1.40,0,1.40):
        g.beam(group,'steel',(x+dx,-1.17,1.06),(x+dx,-2.26,1.28),.08,.074)
    g.box(group,'ink',(sign*(end+.055),-mid,2.78),(.027,.68,.32),.013)
    wording=('PARCEL TALLY' if l.number==9 else 'LAMP RECORD')
    g.text(group,'bone',wording,(sign*(end+.075),-mid,2.75),(pi/2,0,sign*pi/2),.069)
    l.lamp(group+' lamp',(x,-mid,3.17),14,(1,.78,.52),2.75)


def machine_room(g,l):
    """Cab and tender grow in their inhabited sections. No gauge, wheel or
    regulating handwheel is enlarged. The coal and boiler remain solid."""
    n=l.number
    if n==1:
        l.routes.clear()
        for side in (-1,1):
            y=-.43 if side<0 else .86
            l.route('Cab walk '+str(side),[(2.30,0),(4.67,y),(6.74,y),(7.40,0),(8.98,0)])
    spaces=[(5.39,'Driver\'s instrument room')] if n==1 else [
        (-3.69,'Front tool room'),(3.69,'Water service room')]
    for x,title in spaces:
        y=-.98
        span=1.18 if n==1 else .88
        h=2.237
        g.box(title,'wood',(x,y,h-.043),(span,.49,.086),.014)
        g.box(title,'brass',(x,y,h-.003),(span+.01,.504,.012),.005)
        l.box(title+' worktop',(x,y,h-.038),(span,.50,.076))
        for ax in (-1,1):
            for ay in (-1,1):
                g.box(title,'steel',(x+ax*(span/2-.073),y+ay*.18,1.79),(.065,.065,.82),.008)
        g.box(title,'ink',(x,y,1.65),(span-.08,.40,.066),.008)
        g.box(title,'brass',(x,y-.257,2.163),(.46,.015,.082),.004)
        g.text(title,'ink','NORTH FELL',(x,y-.268,2.145),(pi/2,0,0),.040)
        l.lamp(title+' lamp',(x,.38,3.01),22,(1,.76,.50),3.15)
        g.box(title,'brass',(x,.98,3.17),(.25,.14,.036),.012)
        g.box(title,'shade',(x,.98,3.11),(.18,.115,.085),.023)
        # The observer can take either side of the perforated bench in the cab.
        if n==1:
            g.box(title,'ink',(x+.40,.22,1.78),(.89,.31,.80),.021)
            l.box('Insulated back cabinet',(x+.40,.22,1.78),(.89,.31,.80))
            for k in range(3):
                g.box(title,'steel',(x+.15+.25*k,.052,1.86),(.21,.026,.38),.015)
                g.box(title,'brass',(x+.15+.25*k,.032,1.91),(.096,.018,.018),.003)
        else:
            # Fluted riveted stanchions and chained tools at each end of the bunker.
            for dx,caption in ((-.26,'FEED'),(.26,'TANK')):
                xx=x+dx
                g.box(title,'wood',(xx,-1.326,2.91),(.45,.054,.58),.021)
                g.cyl(title,'brass',(xx,-1.298,2.94),(xx,-1.254,2.94),.162,32)
                g.cyl(title,'enamel',(xx,-1.253,2.94),(xx,-1.242,2.94),.141,32)
                for j in range(13):
                    a=(-.70+1.4*j/12)*pi
                    g.beam(title,'ink',(xx+.113*sin(a),-1.238,2.94+.113*cos(a)),
                           (xx+.133*sin(a),-1.238,2.94+.133*cos(a)),.005,.004)
                g.beam(title,'red',(xx,-1.233,2.94),(xx+.05,-1.233,3.031),.009,.006)
                g.text(title,'bone',caption,(xx,-1.292,2.695),(pi/2,0,pi),.052)
            for j in range(5):
                xx=x+(j-2)*.155
                g.cyl(title,'bright',(xx,1.31,2.03),(xx,1.31,2.36+.06*(j%2)),.012,9)
                g.cyl(title,'brass',(xx,1.27,2.30),(xx,1.36,2.30),.021,10)
