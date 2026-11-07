"""Original spruce foliage atlas. Render seeded whorls, drooping twigs and intercepted snow."""
from math import sin, cos, pi
from pathlib import Path
from random import Random

from PIL import Image, ImageDraw

SCALE = 2
TILE = 320
HEIGHT = 576
atlas = Image.new("RGBA", (3*TILE*SCALE, HEIGHT*SCALE))


def spruce(index):
    rng = Random(1932+index*91)
    tile = Image.new("RGBA", (TILE*SCALE, HEIGHT*SCALE))
    draw = ImageDraw.Draw(tile)
    h = 532
    tx = TILE*.49
    lean = rng.uniform(-5, 6)
    def xy(p):
        x, y, z = p
        return ((tx + x + lean*y/h)*SCALE,(553-y+z*.10)*SCALE)
    def line(points,colour,width):
        draw.line([xy(p) for p in points], tuple(int(c) for c in colour)+(255,),
                  width=max(1,round(width*SCALE)),joint="curve")
    def lerp(a,b,t):
        return tuple(v*(1-t)+w*t for v,w in zip(a,b))
    def stroke(p,angle,length,lift):
        return (p[0]+cos(angle)*length,p[1]+lift,p[2]+sin(angle)*length)
    line([(0,0,0),(0,h,0)],(73,67,56),4.4)
    line([(-1,0,-1),(-1,h*.64,-1)],(104,98,81),1.2)
    for k in range(32):
        level=(k+rng.uniform(-.26,.26))/32
        y=h*(.11+.88*level)
        yaw=k*2.39+rng.uniform(-.2,.2)
        reach=135*(1-level)**.79*(.90+.10*sin(k*1.41+index))
        whorl=[]
        for j in range(10):
            angle=yaw+2*pi*j/10+rng.uniform(-.18,.18)
            length=reach*rng.uniform(.76,1.03)
            p=(0,y+rng.uniform(-3,3),0)
            q=stroke(p,angle,length*.53,-length*.19)
            tip=stroke(q,angle+rng.uniform(-.11,.11),length*.47,-length*.025)
            whorl.append((tip[2],p,q,tip,angle,length))
        for depth,base,q,tip,angle,length in sorted(whorl):
            value=rng.uniform(-7,7)+depth/max(reach,1)*7
            leaf=(44+value,69+value,59+value)
            line([base,q,tip],(68+value,74+value,62+value),max(.55,length*.032))
            shaggy=[]
            for n in range(15):
                t=(n+rng.uniform(.08,.83))/15
                p=lerp(base,q,t*2) if t<.5 else lerp(q,tip,(t-.5)*2)
                for hand in (-1,1):
                    l=length*(.24*(1-t)**.7+.035)*rng.uniform(.72,1.28)
                    theta=angle+hand*rng.uniform(.55,1.09)
                    end=stroke(p,theta,l,-l*rng.uniform(.60,1.18))
                    bend=lerp(p,end,.61)
                    line([p,bend,end],leaf,max(.67,l*.094))
                    for f in (.13,.27,.44,.61,.78,.93):
                        a=lerp(p,end,f)
                        for side in (-1,1):
                            l2=l*(.23*(1-f)+.11)
                            b=stroke(a,theta+side*.69,l2,-l2*.78-.55)
                            line([a,b],(leaf[0]+2,leaf[1]+4,leaf[2]+3),rng.uniform(.6,1.25))
                    if rng.random() < (.77 if index==0 else .60 if index==1 else .85):
                        grey=rng.uniform(-16,13)+depth/max(reach,1)*6
                        c=(163+grey,180+grey,186+grey)
                        a=(p[0],p[1]+1,p[2])
                        b=lerp(a,end,rng.uniform(.40,.78))
                        shaggy.append((a,b,c,max(.84,l*rng.uniform(.070,.16))))
            for a,b,c,w in shaggy:
                line([a,b],c,w)
                if w>1.65:
                    line([(a[0],a[1]+.65,a[2]),lerp(a,b,.68)],(c[0]+19,c[1]+18,c[2]+16),w*.45)
    line([(0,h*.96,0),(0,h+1,0)],(122,142,143),1.9)
    return tile


for variant in range(3):
    atlas.paste(spruce(variant), (variant*TILE*SCALE,0))
target=Path(__file__).resolve().parents[3]/"Assets/Transperceneige/Pass/Scenery/FellSpruces.png"
atlas.resize((TILE*3,HEIGHT),Image.Resampling.LANCZOS).save(target)
