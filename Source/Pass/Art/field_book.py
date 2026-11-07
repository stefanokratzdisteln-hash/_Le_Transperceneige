"""Original winter railway leather binding and ledger leaf. Requires Pillow and NumPy."""
from pathlib import Path
from math import sin, cos, pi
import random

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageChops

OUT = Path(__file__).resolve().parents[3] / "Assets/Transperceneige/Pass/HUD/Art"


def smooth_noise(rng, w, h, scale):
    coarse = rng.integers(0, 256, (max(3, h // scale), max(3, w // scale)), dtype=np.uint8)
    noise = Image.fromarray(coarse).resize((w, h), Image.Resampling.BICUBIC)
    return (np.asarray(noise, dtype=float) - 127.5) / 105


def coloured(light, base, tint):
    return Image.fromarray(np.uint8(np.clip(base + light[..., None] * np.asarray(tint), 0, 255)))


def leather(w, h, rng):
    y, x = np.mgrid[:h, :w]
    # Irregular compressed grain cells, with a slightly burnished lip on each crease.
    u = x / 5.9 + .29 * smooth_noise(rng, w, h, 37)
    v = (y + x * .21) / 3.9 + .29 * smooth_noise(rng, w, h, 29)
    i, j = u.astype(int) + 1, v.astype(int) + 1
    fu, fv = u % 1, v % 1
    grid = rng.uniform(.12, .88, (j.max()+2, i.max()+2, 2))
    nearest = np.full((h,w), 10.)
    second = nearest.copy()
    for dy in (-1,0,1):
        for dx in (-1,0,1):
            seed = grid[j+dy,i+dx]
            distance = (dx+seed[:,:,0]-fu)**2 + (dy+seed[:,:,1]-fv)**2
            second = np.minimum(second, np.maximum(nearest, distance))
            nearest = np.minimum(nearest, distance)
    ridge = np.minimum((np.sqrt(second)-np.sqrt(nearest))*3.2, 1)
    grain = -1.9*(1-ridge) + 4*(np.roll(ridge,1,axis=0)-ridge)
    light = grain + 2*smooth_noise(rng,w,h,12) + 3*smooth_noise(rng,w,h,71) + 1.5*smooth_noise(rng,w,h,183)
    light += rng.normal(0,.9,(h,w)) + 5*(1-y/h) - 2*(x/w)
    # Rounded spine, blind channel, then the board hinge.
    light += 12*np.exp(-((x-17)/12)**2) - 15*np.exp(-((x-43)/4)**2) + 7*np.exp(-((x-51)/4)**2)
    light -= 7*np.exp(-np.minimum.reduce([x, y, w-1-x, h-1-y])/8)
    return coloured(light, np.array([23.,43.,55.]), [.89,1.0,1.10])


class Engraver:
    """A high-resolution foil die. Black cuts out small engraved details."""
    def __init__(self, width, height):
        self.s = 3
        self.mask = Image.new("L",(width*self.s,height*self.s))
        self.pen = ImageDraw.Draw(self.mask)
    def coords(self, points):
        return [(round(self.s*x),round(self.s*y)) for x,y in points]
    def line(self, points, width=1.5, ink=255):
        self.pen.line(self.coords(points),fill=ink,width=max(1,round(width*self.s)),joint="curve")
    def polygon(self, points, ink=255):
        self.pen.polygon(self.coords(points), fill=ink)
    def ellipse(self, box, width=1.4, fill=None, ink=255):
        self.pen.ellipse(self.coords([box[:2],box[2:]]),fill=fill,outline=ink,width=max(1,round(width*self.s)))
    def output(self):
        return self.mask.resize((self.mask.width//self.s,self.mask.height//self.s),Image.Resampling.LANCZOS)


def emblem(d, cx=398, cy=236):
    # Elliptical intaglio: the engine crosses a stone viaduct above wooded fells.
    d.ellipse((cx-127,cy-91,cx+127,cy+91),2.7)
    d.ellipse((cx-121,cy-85,cx+121,cy+85),.9)
    d.ellipse((cx-115,cy-79,cx+115,cy+79),1.25)
    def points(p): return [(cx+x,cy+y) for x,y in p]
    def line(p,width=1.5,ink=255): d.line(points(p),width,ink)
    def poly(p,ink=255): d.polygon(points(p),ink)
    def oval(x,y,wx,hy,width=1.4,fill=None,ink=255):
        d.ellipse((cx+x-wx,cy+y-hy,cx+x+wx,cy+y+hy),width,fill,ink)
    line([(-101,-26),(-75,-44),(-64,-39),(-42,-58),(-34,-49),(-26,-53),(-7,-34),(17,-50),(31,-39),(46,-52),(62,-39),(73,-43),(96,-24)],1.2)
    line([(-62,-39),(-58,-39),(-52,-45),(-48,-42),(-42,-57),(-39,-48),(-33,-46)],.85)
    line([(27,-39),(32,-35),(40,-40),(45,-49),(49,-40),(55,-39)],.8)
    for side in (-1,1):
        for i in range(4):
            x=(100-i*5.7)*side
            y=4-i*1.2
            line([(x,y-13),(x,y+8)],.8)
            for k in (0,1,2,3):
                dy=y-11+4*k
                line([(x-1.3-k*.55,dy+3+k*.55),(x,dy),(x+1.3+k*.55,dy+3+k*.55)],.85)
    # Bridge silhouette is engraved with five arch openings, thinner at the ends of the oval.
    poly([(-106,8),(105,8),(98,29),(85,44),(61,59),(37,69),(17,74),(-11,75),(-42,67),(-64,56),(-87,39)])
    for x in (-80,-40,0,40,80):
        arch=[(x-16,81),(x-16,36)]
        for a in np.linspace(pi,0,32):
            arch.append((x+16*cos(a),36-16*sin(a)))
        arch.extend([(x+16,81),(x-16,81)])
        poly(arch,0)
        arc=[(x+19*cos(a),36-19*sin(a)) for a in np.linspace(pi,0,26)]
        line(arc,.9,0)
        for a in np.linspace(.15,pi-.15,11):
            line([(x+16*cos(a),36-16*sin(a)),(x+19*cos(a),36-19*sin(a))],.65,0)
    for y in (11,14): line([(-103,y),(103,y)],.85,0)
    for y in (28,34,40,46,52,58,64):
        for x in (-60,-20,20,60):
            if abs(x)<58 or y<48:
                line([(x-2.5,y),(x+2.5,y)],.55,0)
    line([(-109,6),(109,6)],1.4)
    # British express locomotive, front to the right. Windows and wheel spokes punch through the gold.
    poly([(-73,-15),(-46,-15),(-43,-12),(-34,-12),(-34,-1),(-72,-1)])
    poly([(-66,-16),(-62,-19),(-58,-18),(-54,-20),(-49,-17),(-45,-16)])
    poly([(-28,-22),(-8,-22),(-7,-18),(37,-18),(42,-16),(43,-4),(-28,-4)])
    poly([(-30,-23),(-8,-23),(-6,-21),(-30,-21)])
    for x in (-24,-18): poly([(x,-20),(x+3.7,-20),(x+3.7,-13),(x,-13)],0)
    poly([(3,-18),(5,-22),(8,-23),(10,-22),(12,-18)])
    poly([(26,-18),(28,-25),(26,-26),(26,-28),(36,-28),(36,-25),(34,-25),(35,-18)])
    line([(-31,-4),(47,-4),(48,-1),(52,-1)],2.2)
    line([(-72,0),(-31,0)],1.2)
    for x in (-65,-46,-20,-5,10,29,42):
        rad=5.0 if x in (-20,-5,10) else 3.25
        oval(x,0,rad,rad+0.2,1.4)
        if rad>4:
            line([(x-rad*.57,-2.6),(x+rad*.57,2.6)],.8)
            line([(x-rad*.57,2.6),(x+rad*.57,-2.6)],.8)
    line([(-21,1),(9,1.8),(15,-.6)],1.5)
    for x in (-4,7,18,33): line([(x,-16),(x,-6)],.75,0)
    for i in (0,1,2):
        line([(30,-29-i),(27,-31-i),(18,-33-i),(10,-31-i),(3,-31-i),(-6,-34-i),(-18,-35-i),(-28,-33-i),(-40,-32-i)],.7)
    # Close the outer beading after punching out the arches.
    d.ellipse((cx-127,cy-91,cx+127,cy+91),2.7)
    d.ellipse((cx-121,cy-85,cx+121,cy+85),.9)
    for dx in (-1,1):
        p=cx+dx*131
        for h,length in ((-4,24),(0,34),(4,20)):
            d.line([(p,cy+h),(p+dx*length,cy+h)],1.4)
        d.polygon([(p+dx*38,cy),(p+dx*42,cy-2),(p+dx*47,cy),(p+dx*42,cy+2)])


def cover():
    w,h=760,1000
    rng=np.random.default_rng(1936)
    leather_image=leather(w,h,rng)
    border=Engraver(w,h)
    for inset, step, weight in [(0,16,2.2),(6,15,.95),(13,12,.85)]:
        x1,x2,y1,y2=66+inset,726-inset,27+inset,956-inset
        border.line([(x1+step,y1),(x2-step,y1),(x2-step,y1+7),(x2,y1+7),(x2,y2-7),(x2-step,y2-7),(x2-step,y2),(x1+step,y2),(x1+step,y2-7),(x1,y2-7),(x1,y1+7),(x1+step,y1+7),(x1+step,y1)],weight)
    # Repeated fan-and-rail tooling at the four corners.
    for x,sx in ((68,1),(724,-1)):
        for y,sy in ((29,1),(954,-1)):
            def corner(p):return [(x+sx*dx,y+sy*dy) for dx,dy in p]
            for i in range(5):
                border.line(corner([(18+7*i,17),(18+7*i,22),(24,24),(22,18+7*i),(17,18+7*i)]),1.0)
            border.line(corner([(16,76),(16,55),(25,51),(30,34),(34,30),(51,25),(55,16),(76,16)]),1.25)
            border.polygon(corner([(32,32),(41,36),(46,46),(36,41)]))
            px,py=corner([(50,50)])[0]
            border.ellipse((px-1.8,py-1.8,px+1.8,py+1.8),.9)
    for y in (113,867):
        for x in (81,708):
            border.line([(x,y-13),(x,y+13)],1.1)
            border.polygon([(x,y-4),(x+2.8,y),(x,y+4),(x-2.8,y)])
    emblem(border)
    # Tiny repeated diamonds act as a printer's terminal near the foot.
    for sign in (-1,1):
        border.line([(398+sign*11,894),(398+sign*45,894)],.9)
        border.polygon([(398+sign*49,894),(398+sign*53,892),(398+sign*58,894),(398+sign*53,896)])
    border.polygon([(391,894),(398,890),(405,894),(398,898)])
    mask=border.output()
    y,x=np.mgrid[:h,:w]
    foil_tone=14*np.sin(x/103+y/127)+5*np.sin(y/12+x/48)+11*smooth_noise(rng,w,h,21)+rng.normal(0,9,(h,w))
    foil=coloured(foil_tone,np.array([192.,153.,96.]), [1,.89,.78])
    lip=ImageChops.offset(mask,1,2).filter(ImageFilter.GaussianBlur(.55))
    leather_image.paste((9,23,30),(0,0,w,h),lip)
    leather_image.paste(foil,(0,0),mask)
    frost=Image.new("L",(w,h));f=ImageDraw.Draw(frost)
    rr=random.Random(62)
    for cx,cy,sx,sy,count in [(734,13,-1,1,46),(735,967,-1,-1,22),(53,954,1,-1,15)]:
        for _ in range(count):
            a=rr.uniform(.1,1.45);length=rr.uniform(5,37)
            xx=cx+sx*rr.randrange(26); yy=cy+sy*rr.randrange(30)
            vx,vy=cos(a)*sx,sin(a)*sy
            f.line([(xx,yy),(xx+vx*length,yy+vy*length)],fill=rr.randint(32,64))
            for k in (.30,.50,.69,.83):
                for hand in (-1,1):
                    arm=(1-k)*length*rr.uniform(.37,.67);ca=.5;sa=.866*hand
                    tx,ty=xx+vx*length*k,yy+vy*length*k
                    f.line([(tx,ty),(tx+(vx*ca-vy*sa)*arm,ty+(vy*ca+vx*sa)*arm)],fill=rr.randint(18,39))
    leather_image.paste((185,211,218),(0,0,w,h),frost)
    # The right and lower edges expose the compressed signatures and the rear board.
    paper=coloured(rng.normal(0,1.7,(h,w)),np.array([157.,149.,128.]),[1,.9,.7])
    edges=ImageDraw.Draw(paper)
    for k in range(0,17,2):
        edges.line([(w-15+k,22),(w-15+k,h-26)],fill=(157+k,148+k,127+k))
        edges.line([(30,h-24+k),(w-26,h-24+k)],fill=(153+k,143+k,122+k))
    cover_mask=Image.new("L",(w,h));ImageDraw.Draw(cover_mask).rounded_rectangle((4,3,w-16,h-25),radius=15,fill=255)
    image=Image.new("RGBA",(w,h))
    back=ImageDraw.Draw(image)
    back.rounded_rectangle((6,11,w-3,h-4),radius=17,fill=(19,35,43))
    sheet=Image.new("L",(w,h));ImageDraw.Draw(sheet).rounded_rectangle((11,12,w-6,h-11),radius=14,fill=255)
    image.paste(paper,(0,0),sheet);image.paste(leather_image,(0,0),cover_mask)
    trim=ImageDraw.Draw(image)
    trim.rounded_rectangle((4,3,w-16,h-25),radius=15,outline=(65,78,77),width=2)
    return image


def leaf():
    w,h=690,740
    rng=np.random.default_rng(2712)
    y,x=np.mgrid[:h,:w]
    edge=np.minimum.reduce([x,w-1-x,y,h-1-y])
    light=2.2*smooth_noise(rng,w,h,89)+1.3*smooth_noise(rng,w,h,28)+rng.normal(0,1.6,(h,w))
    light+=.65*np.sin(x*1.17)+.5*np.sin(y*2.31)
    light-=9*np.exp(-edge/7)+19*np.exp(-x/16)
    light+=3*np.exp(-((x-38)/17)**2)
    fill=coloured(light,np.array([233.,222.,195.]),[.85,1.0,1.2])
    # Fine laid-paper fibres are sparse and follow the grain of a pressed leaf.
    fibres=Image.new("RGBA",(w,h)); f=ImageDraw.Draw(fibres)
    rr=random.Random(511)
    for _ in range(3300):
        px=rr.randrange(w);py=rr.randrange(h)
        f.line([(px,py),(px+rr.randrange(1,7),py+rr.randrange(-1,2))],fill=(97,84,61,rr.randrange(3,18)))
    for py in range(56,h-34,30):
        f.line([(24,py),(w-21,py)],fill=(78,102,112,19))
    for px,alpha in ((42,40),(45,20)):
        f.line([(px,17),(px,h-17)],fill=(144,76,60,alpha))
    f.line([(14,5),(w-11,5),(w-8,9),(w-8,h-9),(14,h-9)],fill=(103,93,75,54))
    f.line([(4,14),(4,h-18)],fill=(103,93,75,70),width=2)
    return Image.alpha_composite(fill.convert("RGBA"),fibres).convert("RGB")


if __name__=="__main__":
    OUT.mkdir(parents=True,exist_ok=True)
    cover().save(OUT/"FieldBook.png",optimize=True)
    leaf().save(OUT/"LedgerLeaf.png",optimize=True)
