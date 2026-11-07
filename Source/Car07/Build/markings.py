"""Coach 07 signwriting. All lines and inlay remain geometry in the Unity export."""
from math import cos, pi, sin


def build(g, *, number=7, coach_name='W I N T E R B O U R N E', service='S L E E P I N G   C A R   C O.',
          door_number='1', door_class='FIRST'):
    for side, flank in ((-1, 'Port'), (1, 'Starboard')):
        group = 'Coachwork_signwriting_' + flank
        rotation = (pi/2, 0, 0 if side == -1 else pi)

        def surface(points, depth):
            return [(-side*u, side*depth, z) for u,z in points]

        def panel(material, points, depth):
            area = sum(u*v2-u2*v for (u,v),(u2,v2) in zip(points,points[1:]+points[:1]))
            face = list(range(len(points)))
            g.mesh(group, material, surface(points,depth), [face if area>0 else face[::-1]])

        def line(material, a, b, width, depth):
            du,dz = b[0]-a[0],b[1]-a[1]
            norm = (du*du+dz*dz)**.5
            u,v = -dz/norm*width/2,du/norm*width/2
            panel(material,[(a[0]+u,a[1]+v),(b[0]+u,b[1]+v),
                            (b[0]-u,b[1]-v),(a[0]-u,a[1]-v)],depth)

        def rail(material, path, width, depth=1.567):
            for a,b in zip(path,path[1:]):
                line(material,a,b,width,depth)

        def text(words,u,z,size,depth=1.572,material='gilt'):
            g.text(group,material,words,(-side*u,side*depth,z),rotation,size)

        def oval(material, cx,cz,rx,rz,depth):
            panel(material,[(cx+rx*cos(n*pi/24),cz+rz*sin(n*pi/24)) for n in range(48)],depth)

        def leaf(cx,cz,angle,length,width):
            model = [(0,0),(.35,.8),(.68,1),(1,0),(.65,-.65),(.3,-.55)]
            panel('gilt',[(cx + x*length*cos(angle)-y*width*sin(angle),
                           cz + x*length*sin(angle)+y*width*cos(angle)) for x,y in model],1.573)

        # Lacquered cartouche with fine brass fillets and two engraved laurel sprigs.
        oval('brass',0,3.0,.482,.458,1.560)
        oval('gilt',0,3.0,.458,.434,1.562)
        oval('ink',0,3.0,.446,.422,1.564)
        oval('brass',0,3.0,.409,.387,1.566)
        oval('ink',0,3.0,.397,.375,1.568)
        text('W',0,2.836,.535,1.574)
        for hand in (-1,1):
            points = [(.14,2.554),(.46,2.598),(.66,2.697),(.84,2.839),
                      (.95,3.01),(.99,3.155)]
            rail('brass',[(hand*x,z) for x,z in points],.018,1.571)
            for n,(x,z) in enumerate(points[1:-1]):
                incline = .15+n*.21
                leaf(hand*x,z,incline if hand==1 else pi-incline,.16,.036)
                incline = 1.22+n*.21
                leaf(hand*x,z,incline if hand==1 else pi-incline,.138,.031)
            rail('gilt',[(hand*.53,3.287),(hand*1.23,3.287)],.011)
            panel('gilt',[(hand*1.25,3.287),(hand*1.30,3.305),(hand*1.35,3.287),(hand*1.30,3.269)],1.568)
        # Eight-pointed railway compass above the intertwined branch ends.
        points=[]
        for j in range(16):
            ang=pi/2+j*pi/8
            radius=(.076 if j%4==0 else .045) if j%2==0 else .019
            points.append((radius*cos(ang),3.248+radius*sin(ang)))
        panel('gilt',points,1.575)
        text('NORTH FELL',0,2.216,.37)
        text(service,0,1.979,.144)
        for hand in (-1,1):
            rail('brass',[(hand*.23,1.844),(hand*.86,1.844)],.012)
        text('Nº  7',0,1.793,.15)

        # Both carriage nameboards occupy the same physical bay on the coach.
        lettering_centre = -side*4.44
        text(coach_name,lettering_centre,1.768,.39,1.576)
        for end in (-1,1):
            u = lettering_centre + end*2.29
            panel('gilt',[(u-.08,1.879),(u,1.902),(u+.08,1.879),(u,1.856)],1.576)

        # Distinctive destination boards and the conventional class on the doors.
        board = side*4.44
        g.box(group,'brass',(-side*board,side*1.571,1.865),(2.33,.046,.317),.024)
        g.box(group,'ink',(-side*board,side*1.597,1.865),(2.269,.016,.266),.014)
        text('LONDON   ·   FELLBRIDGE',board,1.807,.174,1.609,'bone')
        for end in (-1,1):
            u = board+end*1.088
            g.cyl(group,'gilt',(-side*u,side*1.600,1.862),(-side*u,side*1.613,1.862),.014,8)
        for end in (-1,1):
            text(door_number,end*8.82,2.701,.34,1.556)
            text(door_class,end*8.82,2.567,.097,1.556)
