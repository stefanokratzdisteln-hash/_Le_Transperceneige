"""Gameplay dimensions live beside the geometry that defines each obstruction."""
from math import isfinite


TITLES = {1:'North Fell Pacific', 2:'Corridor tender', 3:'Railway stores',
          4:'Compartment carriage', 5:'Open saloon', 6:'Restaurant carriage',
          7:'Winterbourne', 8:'Sleeping carriage', 9:"Guard's brake van"}


def unity(point):
    x,y,z=point
    return [-y,z,x]


class Layout:
    """Author in Blender metres. Serialize root-relative Unity metres, with +Z aft."""
    def __init__(self,number):
        self.number=number
        self.title=TITLES[number]
        self.ends=(-9.83,9.83)
        self.boxes=[]
        self.lights=[]
        self.doors=[]
        self.routes=[]
        self.pivots={}
        self.camera=None

    def socket(self,a,b):
        assert a<b
        self.ends=(a,b)

    def box(self,name,center,size,parent=''):
        assert all(isfinite(v) for v in center) and all(v>0 for v in size),name
        self.boxes.append(dict(name=name,parent=parent,center=unity(center),
                               size=[size[1],size[2],size[0]]))

    def lamp(self,name,center,watts=20,color=(1,.77,.53),range_m=3):
        self.lights.append((name,tuple(center),watts,tuple(color),range_m))

    def door(self,name,center,size,closed_offset):
        assert name not in self.pivots,name
        self.pivots[name]=tuple(center)
        self.box(name+' leaf',center,size,name)
        self.doors.append(dict(name=name,closedOffset=unity(closed_offset)))

    def view(self,position,target,lens=20):
        self.camera=(tuple(position),tuple(target),lens)

    def route(self,name,points):
        def foot(p):
            pos=unity((*p,1.395) if len(p)==2 else p)
            return dict(zip('xyz',pos))
        self.routes.append(dict(name=name,points=[foot(p) for p in points]))

    def serialize(self):
        if not self.camera or not self.routes or not self.boxes:
            raise ValueError(f'Incomplete playable layout for car {self.number}')
        position,target,lens=self.camera
        lamps=[dict(name=name,center=unity(p),color=c,range=r,intensity=w*.066)
               for name,p,w,c,r in self.lights]
        return dict(number=self.number,title=self.title,socketA=self.ends[0],socketB=self.ends[1],
                    boxes=self.boxes,doors=self.doors,lights=lamps,routes=self.routes,
                    view=dict(position=unity(position),target=unity(target),lens=lens))
