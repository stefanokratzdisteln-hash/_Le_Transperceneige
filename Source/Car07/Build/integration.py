"""Emit verified physical geometry for the Unity prefab authoring step."""
import json
from pathlib import Path
import cabins
import carbody
import interior


def unity(v):
    x,y,z=v
    return [-y,z,x]


def write(folder):
    box_list=[]
    def box(name,center,size,parent=''):
        box_list.append(dict(name=name,parent=parent,center=unity(center),
                             size=[size[1],size[2],size[0]]))
    for name,center,size in interior.collision_boxes()+list(cabins.collision_boxes()):
        box(name,center,size)
    for wing in (-1, 1):
        for bay in carbody.WINDOW_DESK_BAYS:
            station = carbody.window_station(wing, bay)
            box(f"Window writing shelf {station:.2f}", carbody.window_desk_center(station), carbody.WINDOW_DESK_SIZE)
    for sign,tag in ((-1,'A'),(1,'B')):
        for hand in (-1,1):
            box(f'End {tag} wing {hand}',(sign*8.994,hand*1.055,2.47),(.11,.76,2.16))
            box(f'End {tag} bellows {hand}',(sign*9.35,hand*.708,2.43),(.65,.11,2.11))
        box('Bridge '+tag,(sign*9.34,0,1.362),(.98,1.275,.057))
        box('Open end leaf '+tag,(sign*8.335,.68,2.43),(1.15,.076,2.06),'EndDoor_'+tag)
    doors=[]
    for name,start,end,size in cabins.door_poses():
        box(name+' solid',start,size,name)
        doors.append(dict(name=name,closedOffset=unity(tuple(b-a for a,b in zip(start,end)))))
    lights=[dict(name=name,center=unity(center),color=color,range=r,intensity=power)
            for name,center,color,r,power,_ in interior.light_positions()]
    folder=Path(folder)
    for filename,values in (('Collision',box_list),('Doors',doors),('Lights',lights)):
        (folder/('Car07'+filename+'.json')).write_text(json.dumps(values,indent=2),encoding='utf-8')
