"""Shared North Fell coachwork, end gangways and couplings."""
from math import pi

import carbody
import interior
import markings
import roofends
import runninggear


def van_doors(g,number):
    """The locked double loading doors are distinct from the two end gangways."""
    for side,flank in [(-1,'Port'),(1,'Starboard')]:
        group='Loading_doors_'+flank
        rot=(pi/2,0,0 if side<0 else pi)
        def box(mat,x,depth,z,size,bevel=0):
            g.box(group,mat,(x,side*depth,z),size,bevel)
        box('dark',0,1.572,2.464,(2.94,.040,2.14),.016)
        for hand in [-1,1]:
            x=hand*.725
            box('wood',x,1.604,2.466,(1.427,.042,2.04),.016)
            for index in range(6):
                box('ink' if number==3 else 'petrol',x+(index-2.5)*.221,1.629,2.481,
                    (.214,.011,1.81),.003)
            for z in (1.638,2.57,3.374):
                box('wood',x,1.645,z,(1.388,.029,.086),.007)
            for z in (1.78,3.19):
                box('steel',x+hand*.43,1.672,z,(.44,.035,.045),.005)
                for bolt in (-.13,.13):
                    xx=x+hand*.43+bolt
                    g.cyl(group,'brass',(xx,side*1.689,z),(xx,side*1.699,z),.012,8)
            box('brass',hand*.14,1.668,2.461,(.048,.044,.164),.009)
        box('dark',0,1.655,3.551,(3.01,.110,.068),.012)
        box('ink',0,1.674,2.893,(2.62,.025,.31),.012)
        g.text(group,'gilt','STORES DEPARTMENT' if number==3 else 'LUGGAGE OFFICE',
               (0,side*1.690,2.836),rot,.182)
        for end in (-1,1):
            x=end*8.82
            g.text(group,'gilt',f'{number:02}',(x,side*1.556,2.68),rot,.23)
        # Bars protect baggage and washroom glass on the service stock.
        for x in (-6.51,-5.13,5.13,6.51):
            for dx in (-.29,0,.29):
                g.cyl(group,'steel',(x+dx,side*1.414,2.62),(x+dx,side*1.414,3.32),.011,10)


def fixture(g,l,x,y,name,w=18):
    group='Ceiling_fixtures'
    for mat,levels in [('brass',[(3.54,.111),(3.57,.144),(3.614,.136)]),
                       ('shade',[(3.46,.034),(3.47,.076),(3.51,.102),(3.55,.112)])]:
        interior.turned(g,group,mat,x,y,levels,18)
    l.lamp(name,(x,y,3.37),w)


def build(g,l):
    n=l.number
    van=n in (3,9)
    # The galley worktop has clear, washable sashes above the range and drainboard.
    curtains=(lambda side,wing,i: not (side==1 and wing==-1 and i>0)) if n==6 else not van
    carbody.build(g,window_desks=False,curtains=curtains)
    # Different stock descriptions retain a common fleet identity, including
    # the lamps, gangway profile and door furniture already used by Winterbourne.
    accents={3:{'petrol':'ink','gilt':'brass'},4:{'petrol_light':'hide'},
             5:{'petrol':'ink','velvet':'ink'},6:{},
             8:{'gilt':'brass','velvet':'ink'},9:{'ivory':'petrol_light','gilt':'brass'}}
    for _,(_,_,paints,_) in g.batches.items():
        paints[:]=[accents[n].get(key,key) for key in paints]
    runninggear.build(g)
    roofends.build(g,f'{n:02}')
    interior.floor_and_headlining(g)
    del g.batches['Passage_Wilton_Runner']
    if van:
        paints=g.batches['Interior_Parquet'][2]
        paints[:]=['wood' if p=='parquet' else p for p in paints]
    if van:
        van_doors(g,n)
        for flank,side in [('Port',-1),('Starboard',1)]:
            g.text('Fleet_'+flank,'gilt','NORTH FELL RAILWAY',(-side*4.44,side*1.578,1.78),
                   (pi/2,0,0 if side<0 else pi),.261)
    else:
        labels={4:('COMPARTMENT CARRIAGE','1','FIRST'),5:('OPEN SALOON','3','THIRD'),
                6:('RESTAURANT CAR','R','DINING'),8:('SLEEPING CARRIAGE','3','THIRD')}
        name,code,role=labels[n]
        markings.build(g,number=n,coach_name=name,service='N I G H T   E X P R E S S',
                       door_number=code,door_class=role)
    for name,center,size in interior.collision_boxes()[:4]:
        l.box(name,center,size)
    for sign,tag in ((-1,'A'),(1,'B')):
        for hand in (-1,1):
            l.box(f'{tag} bulkhead {hand}',(sign*8.994,hand*1.055,2.47),(.11,.76,2.16))
            l.box(f'{tag} bellows {hand}',(sign*9.35,hand*.708,2.43),(.65,.11,2.11))
        l.box('Connection '+tag,(sign*9.34,0,1.362),(.98,1.275,.057))
        l.box('Open end leaf '+tag,(sign*8.335,.68,2.43),(1.15,.076,2.06),'EndDoor_'+tag)
        l.pivots['EndDoor_'+tag]=(sign*8.91,.68,1.39)
        for side,flank in ((-1,'Port'),(1,'Starboard')):
            l.pivots['Door_'+flank+'_'+tag]=(sign*(8.04-.492),side*1.475,1.38)
        fixture(g,l,sign*8.05,0,'Vestibule ceiling '+tag,14)
