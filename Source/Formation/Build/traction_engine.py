"""Winterbourne Pacific. The fireman's footplate faces a fully modelled backhead."""
from math import cos, pi, sin
from traction_forms import (FLOOR,buffers,ellipse,end_text,frame,headlamp,loft,
                            pipe,plate,side_text,turned,wheel)


def boiler(g):
    k="Engine_Boiler"
    turned(g,k,"ink","X",(0,0,2.717),[(-3.77,.789),(-2.42,.813),(-.45,.850),(2.02,.884)],64)
    for x,r in ((-3.52,.797),(-2.15,.827),(-.72,.852),(.71,.873),(1.80,.891)):
        turned(g,k,"brass","X",(x,0,2.717),[(-.018,r),(.018,r)],48)
    head="Engine_Smokebox"
    turned(g,head,"dark","X",(0,0,2.717),[(-5.84,.773),(-5.78,.827),(-3.76,.827),(-3.73,.797)],64)
    for x in (-5.71,-3.83):
        turned(g,head,"steel","X",(x,0,2.717),[(-.018,.834),(.018,.834)],48)
    # Cast saddle carries the smokebox between the two main frame plates.
    saddle=[(-.652,1.327),(.652,1.327),(.652,2.260),(.482,2.055),
            (.283,1.940),(0,1.894),(-.283,1.940),(-.482,2.055),(-.652,2.260)]
    plate(g,head,"dark","X",-4.99,-4.12,saddle)
    for s in (-1,1):
        g.box(head,"steel",(-4.555,s*.655,1.661),(.777,.046,.075),.009)
    turned(g,head,"dark","X",(0,0,2.717),[(-6.024,.382),(-6.00,.594),(-5.94,.711),(-5.846,.758)],56)
    for i in range(16):
        a=i*pi/8
        y,z=.708*cos(a),2.717+.708*sin(a)
        g.cyl(head,"steel",(-5.954,y,z),(-5.979,y,z),.023,8)
    for z in (2.44,2.94):
        g.beam(head,"steel",(-6.002,-.35,z),(-5.948,-.69,z),.056,.046)
        g.cyl(head,"dark",(-5.939,-.67,z-.064),(-5.939,-.67,z+.064),.041,12)
    g.cyl(head,"brass",(-6.04,0,2.66),(-6.102,0,2.66),.070,16)
    for y,z in ((.245,2.72),(-.097,2.455)):
        g.beam(head,"bright",(-6.108,0,2.66),(-6.12,y,z),.027,.027)
    g.box(head,"brass",(-6.022,0,3.030),(.029,.52,.170),.012)
    g.box(head,"ink",(-6.042,0,3.03),(.020,.492,.145),.010)
    end_text(g,head,"bone","601", -6.055,0,2.978,.150,-1)

    # Chimney cap is a true annulus with a dark, open throat.
    for m,p in (("dark",[(3.428,.319),(3.54,.291),(3.64,.202),(3.918,.201),(4.015,.237)]),
                ("brass",[(4.012,.239),(4.042,.266),(4.091,.261),(4.105,.239),
                          (4.105,.167),(4.026,.165),(4.012,.174)])):
        turned(g,k,m,"Z",(-5.055,0,0),p,40,m=="brass")
    g.cyl(k,"rubber",(-5.055,0,4.018),(-5.055,0,4.022),.166,32)
    for x,base,r,tall,material in ((-2.36,3.46,.336,.418,"ink"),(.255,3.51,.296,.355,"brass")):
        turned(g,k,material,"Z",(x,0,0),[(base,r*1.29),(base+.063,r*1.09),
              (base+.139,r),(base+tall*.70,r*.97),(base+tall*.91,r*.69),(base+tall,r*.30)],40)
    for x in (1.09,1.30):
        turned(g,k,"brass","Z",(x,0,0),[(3.55,.106),(3.62,.084),(3.722,.072),(3.75,.108),(3.785,.101)],20)
    for s in (-1,1):
        for x,r in ((-5.53,.83),(-4.0,.84),(-2.54,.83),(-1.11,.86),(.48,.88),(1.64,.90)):
            g.cyl(k,"brass",(x,s*(r-.027),2.950),(x,s*(r+.085),2.950),.027,10)
        pipe(g,k,"bright", [(-5.55,s*.897,2.928),(-4.98,s*.914,2.950),
              (-2.55,s*.916,2.950),(-1.11,s*.942,2.950),(1.36,s*.972,2.950),
              (1.75,s*.991,2.950)],.021)
        # Insulated main steam pipe drops into the cylinder chest.
        pipe(g,k,"steel",[(-4.20,s*.72,2.47),(-4.20,s*.885,2.35),
              (-3.92,s*1.033,1.79),(-3.61,s*1.033,1.58)],.124,18)
        g.box(k,"brass",(-1.0,s*.887,2.63),(1.74,.043,.217),.017)
        g.box(k,"velvet",(-1.0,s*.913,2.63),(1.70,.018,.187),.008)
        side_text(g,k,"gilt","N O R T H   S T A R",-1.0,s,.926,2.584,.115)

    # Widened Belpaire firebox ahead of the cab sheet.
    def belpaire(w,t):
        return [(-w,1.73),(w,1.73),(w,3.18),(w-.076,t-.102),(w-.228,t),
                (-w+.228,t),(-w+.076,t-.102),(-w,3.18)]
    loft(g,k,"ink","X",[(1.64,belpaire(.82,3.56)),(1.90,belpaire(.973,3.64)),
                        (3.345,belpaire(1.013,3.66))])
    for s in (-1,1):
        g.box(k,"brass",(2.63,s*1.011,3.16),(1.26,.022,.019))
        for x in (2.1,2.5,2.90):
            g.cyl(k,"steel",(x,s*.996,2.82),(x,s*1.036,2.82),.044,12)


def running_gear(g):
    k="Engine_Frames"
    for s in (-1,1):
        g.box(k,"dark",(-.10,s*.528,1.163),(11.94,.154,.466),.042)
        for x in (-4.69,-3.78,-1.64,.29,2.22,4.63):
            g.box(k,"steel",(x,s*.571,1.22),(.370,.158,.263),.034)
    for x in (-5.20,-3.93): wheel(g,x,.445,"Engine_Leading_Truck")
    drivers=(-1.77,.236,2.242)
    for i,x in enumerate(drivers): wheel(g,x,.924,f"Driving_Wheelset_{i+1}",True)
    wheel(g,4.61,.522,"Engine_Trailing_Truck")
    for s in (-1,1):
        y=s*1.065
        # The three cranks share phase on a side. The two sides are quartered.
        a=.25 if s<0 else 1.82
        dx,dz=.32*cos(a),.32*sin(a)
        for i,x in enumerate(drivers):
            pinx,pinz=x+dx,.924+dz
            g.beam(k,"ink",(x,s*.835,.924),(pinx,s*.835,pinz),.071,.074)
            g.cyl(k,"bright",(pinx,s*.834,pinz),(pinx,s*1.016,pinz),.092,20)
            g.cyl(k,"brass",(pinx,s*.988,pinz),(pinx,s*1.053,pinz),.061,16)
            # Thin cast splashers follow the wheel crowns, separated by real gaps.
            arc=[(x+.963*cos(.50+j*(pi-1.0)/16),.924+.963*sin(.50+j*(pi-1.0)/16)) for j in range(17)]
            inside=[(x+.907*cos(.50+j*(pi-1.0)/16),.924+.907*sin(.50+j*(pi-1.0)/16)) for j in reversed(range(17))]
            plate(g,k,"ink","Y",s*.816,s*1.012,arc+inside)
            g.cyl(k,"steel",(x,s*.39,1.62),(x,s*.64,1.62),.133,16)
        for xa,xb in zip(drivers,drivers[1:]):
            g.beam(k,"bright",(xa+dx,s*.950,.924+dz),(xb+dx,s*.950,.924+dz),.086,.084)
        chest="Engine_Cylinders_and_Valve_Gear"
        turned(g,chest,"dark","X",(-3.31,y,1.124),[(-.455,.235),(-.42,.279),(.32,.279),(.372,.242)],28)
        for xx in (-3.76,-3.0):
            turned(g,chest,"steel","X",(xx,y,1.124),[(-.031,.273),(.031,.273)],24)
        g.box(chest,"steel",(-3.299,y,1.413),(.716,.447,.129),.039)
        for xx in (-3.57,-3.06):
            for yy in (-.143,.143):
                g.cyl(chest,"brass",(xx,y+yy,1.47),(xx,y+yy,1.51),.033,8)
        piston_x=-2.44+dx*.26
        g.cyl(chest,"bright",(-3.006,y,1.124),(piston_x,y,1.124),.067,18)
        g.box(chest,"steel",(piston_x,y,1.124),(.296,.193,.195),.024)
        for zz in (.97,1.285):
            g.beam(chest,"steel",(-3.028,y,zz),(-1.98,y,zz),.052,.057)
        g.beam(chest,"bright",(piston_x,y,1.124),(.236+dx,y,.924+dz),.113,.066)
        g.beam(chest,"steel",(-3.25,s*1.176,1.411),(-1.04,s*1.175,1.569),.044,.040)
        g.beam(chest,"steel",(-1.04,s*1.175,1.569),(.236+dx+.098,s*1.146,.924+dz+.125),.040,.041)
        for xx,zz in ((piston_x,1.124),(.236+dx,.924+dz),(-1.04,1.569)):
            g.cyl(chest,"brass",(xx,s*1.093,zz),(xx,s*1.190,zz),.048,12)
    # Running boards themselves are outside the swept envelopes of the drivers.
    for s in (-1,1):
        g.box(k,"dark",(-1.14,s*1.162,1.715),(9.16,.404,.115),.025)
        g.box(k,"brass",(-1.14,s*1.367,1.735),(9.13,.015,.021))
        for x in (-5.58,-5.35,-5.12,-4.89,-4.66):
            g.box(k,"steel",(x,s*1.166,1.777),(.019,.343,.014))
    g.box(k,"dark",(-6.005,0,1.393),(.702,2.70,.142),.031)
    g.box(k,"velvet",(-6.253,0,1.054),(.173,2.67,.410),.029)
    for s in (-1,1):
        for y in (.76,1.1):
            for z in (.915,1.183):
                g.cyl(k,"steel",(-6.334,s*y,z),(-6.36,s*y,z),.025,8)
        headlamp(g,"Engine_Headlamps",-6.09,s*.902,1.59)
    buffers(g,k,6.34,6.9,-1)
    g.box(k,"dark",(4.824,0,1.285),(3.19,2.87,.150),.017)
    g.box(k,"wood",(4.805,0,1.361),(3.14,2.79,.042))
    g.box(k,"steel",(6.61,0,1.342),(.58,1.44,.080))
    g.box(k,"dark",(6.062,0,1.092),(.682,.562,.300),.029)
    g.box(k,"steel",(6.528,0,1.010),(.731,.199,.103),.027)
    g.cyl(k,"steel",(6.797,0,.913),(6.797,0,1.106),.071,16)
    for x in (3.50,3.93,4.36,4.79,5.22,5.65,6.08):
        g.box(k,"ink",(x,0,1.383),(.008,2.77,.003))
    for s in (-1,1):
        for i in range(3):
            x=5.65+i*.065
            z=1.166-.268*i
            g.box(k,"dark",(x,s*(1.402+i*.034),z),(.51,.22,.042),.011)
        g.beam(k,"steel",(5.39,s*1.35,1.32),(5.57,s*1.49,.60),.048,.040)


def spectacle(g,k,y):
    z=3.289
    inner=ellipse(y,z,.221,.223,32)
    outer=[]
    for i in range(32):
        a=i*pi/16
        r=max(abs(cos(a)),abs(sin(a)))
        outer.append((y+.306*cos(a)/r,z+.308*sin(a)/r))
    frame(g,k,"ink","X",3.321,3.386,outer,inner)
    frame(g,k,"brass","X",3.309,3.396,ellipse(y,z,.241,.243),inner)


def cab(g,l):
    k="Cab_Front_Sheet"
    g.box(k,"ink",(3.355,0,2.172),(.082,2.77,1.618))
    for y,w in ((0,1.034),(-1.260,.252),(1.260,.252)):
        g.box(k,"ink",(3.355,y,3.285),(.077,w,.609))
    for y in (-.825,.825): spectacle(g,k,y)
    g.box(k,"ink",(3.355,0,3.655),(.078,2.77,.136))
    for s in (-1,1):
        side="Cut_Cab_Port_Sheet" if s>0 else "Cab_Starboard_Sheet"
        # Cab side opening, with a low rear cutout into the tender footplate.
        low=[(3.310,1.38),(6.012,1.38),(6.012,1.69),(5.749,1.79),
             (5.467,2.21),(5.243,2.379),(3.310,2.379)]
        plate(g,side,"ink","Y",s*1.345,s*1.423,low)
        g.box(side,"ink",(3.532,s*1.387,3.043),(.444,.077,1.335))
        g.box(side,"ink",(5.417,s*1.386,3.02),(.146,.076,1.384))
        g.box(side,"ink",(4.43,s*1.386,3.643),(2.249,.076,.146))
        for z in (2.395,3.561):
            g.box(side,"brass",(4.552,s*1.409,z),(1.617,.057,.026),.006)
        for x in (3.762,5.347):
            g.box(side,"brass",(x,s*1.409,2.978),(.029,.055,1.16),.005)
        g.box(side,"brass",(4.306,s*1.427,1.939),(1.093,.018,.37),.008)
        g.box(side,"ink",(4.306,s*1.440,1.939),(1.059,.014,.34),.008)
        side_text(g,side,"gilt","6 0 1",4.306,s,1.45,1.846,.250)
        pipe(g,side,"brass",[(5.72,s*1.439,1.710),(5.72,s*1.492,1.771),
                             (5.72,s*1.492,2.70),(5.72,s*1.432,2.75)],.021,12)
        g.box(side,"wood",(4.949,s*1.193,1.83),(.549,.332,.080),.023)
        g.beam(side,"dark",(4.95,s*1.352,1.52),(4.95,s*1.086,1.80),.041,.046)
    r="Roof_Cab_Canopy"
    arch=[(-1.495,3.719),(-1.495,3.768),(-1.26,3.902),(-.81,4.021),
          (-.36,4.079),(0,4.094),(.36,4.079),(.81,4.021),(1.26,3.902),
          (1.495,3.768),(1.495,3.719),(1.25,3.837),(.80,3.957),
          (.355,4.014),(0,4.030),(-.355,4.014),(-.80,3.957),(-1.25,3.837)]
    plate(g,r,"dark","X",3.244,6.148,arch)
    g.box(r,"steel",(4.631,0,4.102),(.542,.692,.044),.016)
    g.box(r,"ink",(4.627,0,4.145),(.507,.659,.049),.018)
    for s in (-1,1):
        g.box(r,"steel",(4.697,s*1.493,3.740),(2.91,.031,.053),.009)
    # Collisions register physical barriers without closing off the rear entrance.
    l.box("Boiler_and_smokebox",(-1.19,0,2.71),(9.45,1.68,1.68))
    l.box("Front_frames",(-1.46,0,.99),(9.70,2.49,.77))
    l.box("Cab_footplate",(4.813,0,1.305),(3.22,2.83,.154))
    l.box("Cab_fallplate",(6.609,0,1.337),(.582,1.44,.09))
    l.box("Backhead",(3.53,0,2.50),(.51,2.77,2.24))
    l.box("Cab_roof",(4.697,0,3.974),(2.91,2.87,.16))
    for s in (-1,1):
        l.box("Cab_side_"+str(s),(4.68,s*1.397,1.956),(2.70,.103,1.145))
        l.box("Driver_seat_"+str(s),(4.949,s*1.193,1.81),(.55,.333,.12))


def dial(g,k,cy,cz,r,legend,max_value,needle):
    x=3.731
    for m,a,b,rad in (("rubber",0,.046,r*1.17),("brass",.043,.098,r*1.12),
                      ("gilt",.096,.109,r*1.035),("enamel",.108,.115,r*.975)):
        g.cyl(k,m,(x+a,cy,cz),(x+b,cy,cz),rad,36)
    px=x+.120
    for i in range(26):
        angle=(220-i*10.25)*pi/180
        rad=.78 if i%5==0 else .85
        g.beam(k,"ink",(px,cy+rad*r*cos(angle),cz+rad*r*sin(angle)),
               (px,cy+.935*r*cos(angle),cz+.935*r*sin(angle)),.0055,.005)
        if i%5==0:
            end_text(g,k,"ink",str(round(max_value*i/25)),px+.004,cy+.642*r*cos(angle),
                     cz+.642*r*sin(angle)-r*.062,r*.183)
    a=(220-needle*256.25)*pi/180
    g.beam(k,"velvet",(px+.006,cy-.20*r*cos(a),cz-.20*r*sin(a)),
           (px+.012,cy+.79*r*cos(a),cz+.79*r*sin(a)),.011,.008)
    g.cyl(k,"ink",(px+.011,cy,cz),(px+.024,cy,cz),.016,16)
    end_text(g,k,"ink",legend,px+.005,cy,cz-r*.41,r*.142)


def backhead(g,l):
    k="Cab_Backhead_Instruments"
    shield=[(-.967,1.40),(.967,1.40),(.967,2.600),(.873,2.879),
            (.616,3.067),(.298,3.171),(0,3.202),(-.298,3.171),
            (-.616,3.067),(-.873,2.879),(-.967,2.600)]
    plate(g,k,"dark","X",3.375,3.664,shield)
    for s in (-1,1):
        for z in (1.51,1.70,2.33,2.55,2.73):
            g.cyl(k,"steel",(3.662,s*.873,z),(3.700,s*.873,z),.032,8)
    for yy in (-.51,-.29,-.09,.09,.29,.51):
        g.cyl(k,"brass",(3.667,yy,3.00-.31*abs(yy)),(3.701,yy,3.00-.31*abs(yy)),.026,8)
    # Firehole with visible hot coals behind an iron half-open slide.
    frame(g,k,"steel","X",3.661,3.782,ellipse(0,1.907,.361,.237),ellipse(0,1.907,.292,.167))
    plate(g,k,"velvet","X",3.666,3.677,ellipse(0,1.907,.301,.176))
    for i in range(13):
        yy=-.246+.041*i
        z=1.850+.018*sin(i*2.7)
        g.cyl(k,"amber",(3.682,yy,z),(3.697,yy,z),.036+.011*cos(i),10)
        g.beam(k,"red",(3.704,yy-.008,z-.016),(3.71,yy+.020,z+.048),.016,.013)
    gate=ellipse(.279,1.907,.231,.192,24)
    plate(g,k,"dark","X",3.788,3.829,gate)
    g.beam(k,"brass",(3.835,.27,1.92),(3.875,.52,1.98),.025,.025)
    for z in (1.697,2.125): g.box(k,"steel",(3.789,.158,z),(.049,.997,.033),.007)
    # Real pressure and train-vacuum dial faces. Ticks numerals and needles are separate metalwork.
    for yy,zz,r,label,maximum,read in ((-.439,2.887,.141,"LBS / SQ IN",250,.71),
                                        (-.092,2.916,.132,"VACUUM",30,.68)):
        g.cyl(k,"dark",(3.66,yy,zz),(3.731,yy,zz),r*.91,20)
        dial(g,k,yy,zz,r,label,maximum,read)
        pipe(g,k,"brass",[(3.745,yy,zz-r),(3.745,yy,zz-.205),
                          (3.745,yy-.108,zz-.237),(3.704,yy-.108,2.524)],.017)
    # Cocks, guard strips and a blue-green water column on the gauge glass.
    yy=.611
    g.box(k,"brass",(3.698,yy,2.522),(.074,.129,.508),.011)
    g.box(k,"enamel",(3.743,yy,2.529),(.023,.096,.407))
    g.box(k,"glass",(3.760,yy,2.464),(.018,.039,.239),.006)
    for off in (-.050,.050):
        g.cyl(k,"bright",(3.770,yy+off,2.33),(3.770,yy+off,2.723),.009,10)
    for zz in (2.303,2.755):
        g.cyl(k,"brass",(3.668,yy,zz),(3.801,yy,zz),.050,12)
        g.beam(k,"petrol_light",(3.80,yy,zz),(3.824,yy+.093,zz+.037),.023,.025)
    # Regulator quadrant with arced sector and a broad polished wooden grip.
    g.cyl(k,"brass",(3.67,-.19,2.511),(3.812,-.19,2.511),.097,24)
    g.beam(k,"bright",(3.827,-.19,2.511),(3.857,.245,2.678),.051,.038)
    g.beam(k,"wood",(3.857,.173,2.651),(3.865,.365,2.725),.060,.053)
    for yy in (-.78,.82):
        pipe(g,k,"brass",[(3.709,yy,1.65),(3.745,yy,2.377),(3.745,yy*.81,2.498)],.028,12)
    # The driver's vacuum brake valve and reversing screw occupy one side only.
    q="Cab_Brake_and_Reverser"
    turned(g,q,"dark","Z",(4.19,-.972,0),[(1.383,.154),(1.424,.164),(1.484,.087),(2.18,.069),(2.231,.109)],20)
    turned(g,q,"brass","Z",(4.19,-.972,0),[(2.216,.110),(2.254,.127),(2.307,.107)],24)
    g.beam(q,"brass",(4.19,-.972,2.321),(4.39,-.826,2.332),.044,.033)
    g.beam(q,"wood",(4.338,-.859,2.332),(4.483,-.777,2.339),.057,.050)
    turned(g,q,"velvet","X",(4.038,-.99,1.909),[(-.023,.174),(.023,.174),(.023,.150),(-.023,.150)],26,True)
    for i in range(6):
        a=i*pi/3
        g.beam(q,"dark",(4.04,-.99,1.909),(4.04,-.99+.157*cos(a),1.909+.157*sin(a)),.022,.026)
    pipe(g,q,"brass",[(4.19,-.972,1.789),(4.071,-.972,1.766),(3.92,-1.11,1.766),
                       (3.755,-1.11,1.501)],.024,12)
    # Fireman's shovel hangs off the centreline against the open-side panel.
    k="Cab_Firemans_Tools"
    plate(g,k,"steel","Y",1.274,1.304,[(4.868,1.435),(5.124,1.47),(5.091,1.737),(4.921,1.719)])
    g.beam(k,"wood",(5.016,1.283,1.667),(5.329,1.283,2.608),.039,.040)
    pipe(g,k,"steel",[(5.329,1.283,2.603),(5.284,1.283,2.702),
                      (5.411,1.283,2.750),(5.329,1.283,2.603)],.016)
    l.lamp("Footplate_oil_lamp",(4.19,-.702,3.205),24,(1,.74,.43),3.4)
    l.lamp("Firehole_glow",(3.779,-.09,1.901),8,(1,.28,.048),1.4)
    headlamp(g,k,3.958,-1.257,3.188,1)
    l.box("Brake_stand",(4.246,-.972,1.861),(.389,.345,.959))


def engine(g,l):
    l.socket(-6.90,6.90)
    running_gear(g)
    boiler(g)
    cab(g,l)
    backhead(g,l)
    l.route("Footplate_to_tender",[(4.39,0),(5.82,0),(6.88,0)])
    l.view((5.97,.512,2.83),(3.59,-.092,2.397),20)
