"""Emit verified physical geometry for the Unity prefab authoring step."""
import json
from pathlib import Path
import cabins
import carbody
import interior


def unity(v):
    x,y,z=v
    return [-y,z,x]


def layout():
    from layout import Layout
    spec=Layout(7)
    box=spec.box
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
    for name,start,end,size in cabins.door_poses():
        spec.door(name,start,size,tuple(b-a for a,b in zip(start,end)))
    for name,center,color,r,power,_ in interior.light_positions():
        overhead=center[2]>=3.11
        spec.lamp(name,center,power/(.066*(.8 if overhead else .44)),color,r)
    spec.route('Sleeper side corridor',[(-8.80,0),(-7.55,0),(-7.55,-.90),
               (7.55,-.90),(7.55,0),(8.80,0)])
    for z in (-5.21,-3.67,3.67,5.21):
        spec.route('Compartment doorway '+str(z),[(z,-.90),(z,.13)])
    spec.view((-4.83,-.455,2.865),(-6.09,.89,2.341),15.5)
    return spec


def write(folder,manifest):
    folder=Path(folder)
    for filename,values in (('Collision',manifest['boxes']),('Doors',manifest['doors']),('Lights',manifest['lights'])):
        (folder/('Car07'+filename+'.json')).write_text(json.dumps(values,indent=2),encoding='utf-8')
